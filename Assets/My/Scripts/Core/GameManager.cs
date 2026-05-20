using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using MessagePipe;
using Microsoft.Extensions.Logging;
using My.Scripts.Core.Data;
using My.Scripts.Global;
using My.Scripts.Hardware;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using VContainer;
using Wonjeong.Core;
using Wonjeong.Data;
using Wonjeong.UI;
using Wonjeong.Utils;
using ZLogger;

namespace My.Scripts.Core
{
    public enum InactivityTextType
    {
        Warning,
        Tag
    }

    [Serializable]
    public class SystemData
    {
        public TextSetting inactivityWarningText;
        public TextSetting inactivityResetText;
        public TextSetting tagText;
    }

    /// <summary>
    /// 게임 전반의 시스템 상태, 데이터 공유, 무입력 방치 타이머, API 통신, 씬 전환 등을 관리하는 코어 매니저 클래스.
    /// </summary>
    public class GameManager : GameManagerBase<GameManager>
    {
        [Header("System Popup (Inactivity)")]
        public GameObject systemPopupPrefab;

        [Header("Debug / Testing")]
        public float lastPlayDistance = 100f;
        
        private bool isAutoProgressing = false;

        private SystemData _systemData;
        private float _currentInactivityTimer;
        private bool _isTransitioning;
        private float _fadeTime = 0.5f;

        private bool _isInactivitySequenceRunning;
        private CancellationTokenSource _inactivityCts;

        private CanvasGroup _systemPopupCg;
        private Text _systemPopupText;

        private bool _isQuitting;
        private bool _isQuitSafe;

        public bool IsAutoProgressing
        {
            get => isAutoProgressing;
            set => isAutoProgressing = value;
        }

        public InactivityTextType CurrentInactivityTextType { get; set; } = InactivityTextType.Warning;
        public ApiSettings ApiConfig { get; set; }

        // VContainer 인젝션을 위한 매니저 변수들
        private ILogger<GameManager> _logger;
        private SessionManager _sessionManager;
        private InputManager _inputManager;
        private ArduinoManager _arduinoManager;
        private SoundManager _soundManager;
        private UIManager _uiManager;
        private FadeManager _fadeManager;

        // 속성들도 모두 주입받은 인스턴스(의존성)를 사용하도록 변경
        public int CurrentUserId => _sessionManager != null ? _sessionManager.CurrentUserId : 0;
        public string CurrentLanguage => _sessionManager != null ? _sessionManager.CurrentLanguage : "ko";
        public string Cartridge => _sessionManager != null ? _sessionManager.Cartridge : "";

        public bool IsOtherCartridgeContentsCleared =>
            _sessionManager != null && _sessionManager.IsOtherCartridgeContentsCleared;

        public string PlayerAName => _sessionManager != null ? _sessionManager.PlayerAFirstName : "Player A";
        public string PlayerBName => _sessionManager != null ? _sessionManager.PlayerBFirstName : "Player B";

        public ColorData PlayerAColor => _sessionManager != null ? _sessionManager.PlayerAColor : ColorData.NotSet;
        public ColorData PlayerBColor => _sessionManager != null ? _sessionManager.PlayerBColor : ColorData.NotSet;

        public UserType currentUserType
        {
            get => _sessionManager ? _sessionManager.CurrentUserType : UserType.A1;
            set
            {
                if (_sessionManager != null) _sessionManager.CurrentUserType = value;
            }
        }

        public int PieceC2
        {
            get => _sessionManager != null ? _sessionManager.PieceC2 : 0;
            set
            {
                if (_sessionManager != null) _sessionManager.PieceC2 = value;
            }
        }

        public int TotalPieces => _sessionManager != null ? _sessionManager.TotalPieces : 0;

        public event Action OnUserDataUpdated;

        [Header("Player Color Sprites")]
        public Sprite[] playerColorSprites;

        /// <summary>
        /// VContainer를 통한 의존성 주입.
        /// 기존의 .Instance 접근을 완전히 대체합니다.
        /// </summary>
        [Inject]
        public void InjectDependencies(
            ILogger<GameManager> logger,
            SessionManager sessionManager,
            InputManager inputManager,
            ArduinoManager arduinoManager,
            SoundManager soundManager,
            UIManager uiManager,
            FadeManager fadeManager)
        {
            _logger = logger;
            _sessionManager = sessionManager;
            _inputManager = inputManager;
            _arduinoManager = arduinoManager;
            _soundManager = soundManager;
            _uiManager = uiManager;
            _fadeManager = fadeManager;
        }

        protected override void Awake()
        {
            base.Awake();
            Application.wantsToQuit += WantsToQuit;
        }

        protected override void Start()
        {
            base.Start();
            Application.runInBackground = true;

            if (systemPopupPrefab)
            {
                GameObject popupInstance = Instantiate(systemPopupPrefab, transform);
                _systemPopupCg = popupInstance.GetComponent<CanvasGroup>();

                if (!_systemPopupCg) _systemPopupCg = popupInstance.AddComponent<CanvasGroup>();

                _systemPopupText = popupInstance.GetComponentInChildren<Text>();

                if (_systemPopupCg)
                {
                    _systemPopupCg.alpha = 0f;
                    _systemPopupCg.gameObject.SetActive(false);
                }
            }

            if (_inputManager != null)
            {
                _inputManager.OnPadDown += HandlePadInputForInactivity;
                _inputManager.OnPadUp += HandlePadInputForInactivity;
            }

            if (_arduinoManager != null)
            {
                _arduinoManager.OnHardwareInput += HandleRawHardwareInput;
            }
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();

            Application.wantsToQuit -= WantsToQuit;
            if (_inputManager != null)
            {
                _inputManager.OnPadDown -= HandlePadInputForInactivity;
                _inputManager.OnPadUp -= HandlePadInputForInactivity;
            }

            if (_arduinoManager != null)
            {
                _arduinoManager.OnHardwareInput -= HandleRawHardwareInput;
            }

            CancelInactivitySequence();
        }

        /// <summary>
        /// JSON 파일로부터 시스템 및 API 설정 데이터를 비동기로 로드함.
        /// UniTaskVoid 에러를 피하기 위해 base 호출 대신 부모의 로직을 포함하여 재작성.
        /// </summary>
        protected override async UniTaskVoid LoadSettingsAsync()
        {
            CancellationToken ct = this.GetCancellationTokenOnDestroy();

            // 1. Base 클래스의 로직(Settings.json 로드)을 직접 await 처리
            settings = await JsonLoader.LoadAsync<Settings>(GameConstants.Path.JsonSetting, ct);
            if (settings == null && _logger != null)
            {
                _logger.ZLogError($"[GameManager] Settings file not found.");
            }

            _fadeTime = settings != null ? settings.fadeTime : 1.0f;

            // 2. 추가 데이터 비동기 로드
            _systemData =
                await JsonLoader.LoadAsync<SystemData>(GameConstants.Path.GetLocalizedPath(GameConstants.Path.System),
                    ct);
            ApiConfig = await JsonLoader.LoadAsync<ApiSettings>("JSON/" + GameConstants.Path.ApiSetting, ct);

            if (ApiConfig == null && _logger != null)
            {
                _logger.ZLogWarning($"API 설정 파일 로드 실패.");
            }
        }

        private void Update()
        {
            if (_isTransitioning) return;

            HandleInactivity();
        }

        public void NotifyUserDataUpdated() => OnUserDataUpdated?.Invoke();

        public Sprite GetColorSprite(ColorData color)
        {
            int index = (int)color;
            if (index >= 0 && playerColorSprites != null && index < playerColorSprites.Length)
                return playerColorSprites[index];

            return null;
        }

        public Color GetColorFromData(ColorData colorData)
        {
            switch (colorData)
            {
                case ColorData.Cyan: return new Color32(113, 177, 158, 255);
                case ColorData.Pink: return new Color32(240, 60, 102, 255);
                case ColorData.Orange: return new Color32(240, 103, 27, 255);
                case ColorData.Green: return new Color32(98, 125, 23, 255);
                case ColorData.Red: return new Color32(191, 82, 77, 255);
                case ColorData.Yellow: return new Color32(243, 203, 38, 255);
                default: return Color.white;
            }
        }

        public string GetLevelSuffix(int questionNumber)
        {
            if (questionNumber <= 0) return "";

            string typeStr = currentUserType.ToString();
            char relationChar = typeStr.Length > 1 ? typeStr[1] : '1';

            switch (relationChar)
            {
                case '1': return "_A";
                case '2': return (questionNumber == 4) ? "_B" : "_A";
                case '3':
                    if (questionNumber == 4 || questionNumber == 10 || questionNumber == 11 ||
                        questionNumber == 13 || questionNumber == 14 || questionNumber == 15) return "_C";

                    return "_A";
                case '4': return "_D";
                case '5': return "_E";
                case '6': return "_F";
                default: return "_A";
            }
        }

        private void HandleInactivity()
        {
            // 1. 방치 체크가 필요 없는 경우 즉시 종료
            if (!CanCheckInactivity())
            {
                ResetInactivityTimer();
                return;
            }

            // 2. 유저 활동이 감지되면 타이머 초기화
            if (IsUserActive())
            {
                ResetInactivityTimer();
                return;
            }

            // 3. 타이머 카운트
            if (!_isInactivitySequenceRunning)
            {
                _currentInactivityTimer += Time.deltaTime;
                if (_currentInactivityTimer >= 20f)
                {
                    StartInactivitySequence();
                }
            }
        }

        private bool CanCheckInactivity() =>
            SceneManager.GetActiveScene().name != GameConstants.Scene.Title &&
            !isAutoProgressing &&
            SceneManager.GetActiveScene().name != GameConstants.Scene.Ending;

        private bool IsUserActive() =>
            Input.anyKeyDown ||
            Input.GetMouseButtonDown(0) ||
            (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began);

        private void HandlePadInputForInactivity(int playerIdx, int laneIdx, int padIdx) => ResetInactivityTimer();
        private void HandleRawHardwareInput(int padNumber, bool isDown) => ResetInactivityTimer();

        public void ResetInactivityTimer()
        {
            _currentInactivityTimer = 0f;

            if (_isInactivitySequenceRunning)
            {
                CancelInactivitySequence();

                if (_systemPopupCg)
                {
                    _systemPopupCg.alpha = 0f;
                    _systemPopupCg.gameObject.SetActive(false);
                }

                if (_soundManager) _soundManager.StopSFX();
            }
        }

        public void ForceInactivitySequence()
        {
            if (!_isInactivitySequenceRunning)
            {
                _currentInactivityTimer = 20f;
                StartInactivitySequence();
            }
        }

        private void StartInactivitySequence()
        {
            CancelInactivitySequence();
            _inactivityCts = new CancellationTokenSource();
            InactivitySequenceAsync(_inactivityCts.Token).Forget();
        }

        private void CancelInactivitySequence()
        {
            _isInactivitySequenceRunning = false;
            if (_inactivityCts != null)
            {
                _inactivityCts.Cancel();
                _inactivityCts.Dispose();
                _inactivityCts = null;
            }
        }

        private async UniTaskVoid InactivitySequenceAsync(CancellationToken ct)
        {
            _isInactivitySequenceRunning = true;

            try
            {
                // 1. 경고 단계
                UpdatePopupText(CurrentInactivityTextType == InactivityTextType.Tag ? _systemData.tagText : _systemData.inactivityWarningText);
                await FadeSystemPopupAsync(0f, 1f, 0.5f, ct);
                await UniTask.Delay(TimeSpan.FromSeconds(3.0), cancellationToken: ct);
                await FadeSystemPopupAsync(1f, 0f, 0.5f, ct);

                // 2. 카운트다운 및 대기
                if (_soundManager) _soundManager.PlaySFX("공통_15_10초");
                await UniTask.Delay(TimeSpan.FromSeconds(10.0), cancellationToken: ct);

                // 3. 리셋 경고 단계
                UpdatePopupText(_systemData.inactivityResetText);
                await FadeSystemPopupAsync(0f, 1f, 0.5f, ct);

                // 4. API 실행 및 종료 처리
                PerformInactivityReset();
                await UniTask.Delay(TimeSpan.FromSeconds(3.0), cancellationToken: ct);

                ReturnToTitle();
            }
            catch (OperationCanceledException)
            {
                /* 정상 취소 처리 */
            }
        }

        // 헬퍼 메서드: 텍스트 업데이트 캡슐화
        private void UpdatePopupText(TextSetting setting)
        {
            if (!_systemPopupText) return;

            if (_uiManager && setting != null)
                _uiManager.SetText(_systemPopupText.gameObject, setting);
            else
                _systemPopupText.text = setting?.text ?? "동작이 인식되지 않아 초기화됩니다.";
        }

        // 헬퍼 메서드: API 호출 캡슐화
        private void PerformInactivityReset()
        {
            SendResetStartAPI();
            SendExitRoomAPI();
        }

        private async UniTask FadeSystemPopupAsync(float start, float end, float duration, CancellationToken ct)
        {
            if (!_systemPopupCg) return;

            _systemPopupCg.gameObject.SetActive(true);
            _systemPopupCg.alpha = start;

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                _systemPopupCg.alpha = Mathf.Lerp(start, end, elapsed / duration);
                await UniTask.Yield(PlayerLoopTiming.Update, ct);
            }

            _systemPopupCg.alpha = end;
            if (end <= 0f) _systemPopupCg.gameObject.SetActive(false);
        }

        public void ChangeScene(string sceneName)
        {
            if (_isTransitioning) return;

            _isTransitioning = true;
            ResetInactivityTimer();
            ChangeSceneAsync(sceneName).Forget();
        }

        /// <summary>
        /// 글로벌 페이드 매니저를 활용한 비동기 씬 전환. (UniTask 리팩토링)
        /// </summary>
        /// <param name="sceneName">이동할 씬 이름</param>
        private async UniTaskVoid ChangeSceneAsync(string sceneName)
        {
            if (!_fadeManager)
            {
                await SceneManager.LoadSceneAsync(sceneName);
                _isTransitioning = false;
                return;
            }

            try
            {
                await _fadeManager.FadeOutAsync(_fadeTime, this.GetCancellationTokenOnDestroy());
            }
            catch (Exception e)
            {
                if (_logger != null) _logger.ZLogWarning($"FadeOutAsync 호출 실패. 강제 진행: {e.Message}");
            }

            await SceneManager.LoadSceneAsync(sceneName);

            try
            {
                await _fadeManager.FadeInAsync(_fadeTime, this.GetCancellationTokenOnDestroy());
            }
            catch (Exception e)
            {
                if (_logger != null) _logger.ZLogWarning($"FadeInAsync 호출 실패: {e.Message}");
            }

            _isTransitioning = false;
        }

        public void ReturnToTitle()
        {
            if (_isTransitioning) return;

            isAutoProgressing = false;
            CurrentInactivityTextType = InactivityTextType.Warning;

            if (_systemPopupCg)
            {
                _systemPopupCg.alpha = 0f;
                _systemPopupCg.gameObject.SetActive(false);
            }

            ResetInactivityTimer();

            if (_sessionManager) _sessionManager.ClearSession();

            ChangeScene(GameConstants.Scene.Title);
        }

        public async UniTask<string> CheckRoomStateAsync()
        {
#if UNITY_EDITOR
            return "USING";
#endif
            if (ApiConfig == null) return "EMPTY";

            string url = $"{ApiConfig.CheckRoomStateUrl}?code=c2";
            int maxRetries = 10;

            for (int i = 0; i < maxRetries; i++)
            {
                using (UnityWebRequest req = UnityWebRequest.Get(url))
                {
                    req.timeout = 5;
                    try
                    {
                        await req.SendWebRequest().ToUniTask();
                        return req.downloadHandler.text.Trim();
                    }
                    catch (Exception e)
                    {
                        _logger?.ZLogWarning($"CheckRoomState 통신 에러 ({i + 1}/{maxRetries}): {e.Message}");
                        if (i < maxRetries - 1) await UniTask.Delay(TimeSpan.FromSeconds(1.0));
                    }
                }
            }

            return "EMPTY";
        }

        public async UniTask<string> GetCurrentRoomUserAsync()
        {
#if UNITY_EDITOR
            return "TEST_UID_A,TEST_UID_B";
#endif
            if (ApiConfig == null) return "EMPTY";

            string url = $"{ApiConfig.GetCurrentRoomUserUrl}?code=c2";
            int maxRetries = 10;

            for (int i = 0; i < maxRetries; i++)
            {
                using (UnityWebRequest req = UnityWebRequest.Get(url))
                {
                    req.timeout = 5;
                    try
                    {
                        await req.SendWebRequest().ToUniTask();
                        return req.downloadHandler.text.Trim();
                    }
                    catch (Exception e)
                    {
                        if (_logger != null)
                            _logger.ZLogWarning($"GetCurrentRoomUser 통신 에러 ({i + 1}/{maxRetries}): {e.Message}");
                        if (i < maxRetries - 1) await UniTask.Delay(TimeSpan.FromSeconds(1.0));
                    }
                }
            }

            return "EMPTY";
        }

        public void SendResetStartAPI()
        {
#if UNITY_EDITOR
            if (_logger != null) _logger.ZLogInformation($"에디터 모드: 룸 리셋 API 전송 생략");
            return;
#endif
            if (CurrentUserId == 0 || ApiConfig == null) return;

            ResetStartAsync().Forget();
        }

        private async UniTaskVoid ResetStartAsync()
        {
            string url = $"{ApiConfig.ResetStartUrl}?idx_user={CurrentUserId}&code=c2";
            int maxRetries = 10;

            for (int i = 0; i < maxRetries; i++)
            {
                using (UnityWebRequest req = UnityWebRequest.Get(url))
                {
                    req.timeout = 5;
                    try
                    {
                        await req.SendWebRequest().ToUniTask();
                        return;
                    }
                    catch (Exception e)
                    {
                        _logger?.ZLogWarning($"ResetStart 통신 에러 ({i + 1}/{maxRetries}): {e.Message}");
                        if (i < maxRetries - 1) await UniTask.Delay(TimeSpan.FromSeconds(1.0));
                    }
                }
            }
        }

        public void SendExitRoomAPI()
        {
#if UNITY_EDITOR
            if (_logger != null) _logger.ZLogInformation($"에디터 모드: 룸 퇴장 API 전송 생략");
            return;
#endif
            if (CurrentUserId == 0 || ApiConfig == null) return;

            ExitRoomAsync().Forget();
        }

        private async UniTaskVoid ExitRoomAsync()
        {
            string url = $"{ApiConfig.ExitRoomUrl}?code=c2&idx_user={CurrentUserId}";
            int maxRetries = 10;

            for (int i = 0; i < maxRetries; i++)
            {
                using (UnityWebRequest req = UnityWebRequest.Get(url))
                {
                    req.timeout = 5;
                    try
                    {
                        await req.SendWebRequest().ToUniTask();
                        return;
                    }
                    catch (Exception e)
                    {
                        if (_logger != null) _logger.ZLogWarning($"ExitRoom 통신 에러 ({i + 1}/{maxRetries}): {e.Message}");
                        if (i < maxRetries - 1) await UniTask.Delay(TimeSpan.FromSeconds(1.0));
                    }
                }
            }
        }

        public void SendTimeUpdateAPI()
        {
#if UNITY_EDITOR
            if (_logger != null) _logger.ZLogInformation($"에디터 모드: 게임 종료 시간 업데이트 API 전송 생략");
            return;
#endif
            if (CurrentUserId == 0 || ApiConfig == null) return;

            TimeUpdateAsync().Forget();
        }

        private async UniTaskVoid TimeUpdateAsync()
        {
            string url = $"{ApiConfig.UpdateTimeUrl}?idx_user={CurrentUserId}&option=end&code=c2";
            int maxRetries = 10;

            for (int i = 0; i < maxRetries; i++)
            {
                using (UnityWebRequest req = UnityWebRequest.Get(url))
                {
                    req.timeout = 10;
                    try
                    {
                        await req.SendWebRequest().ToUniTask();
                        return;
                    }
                    catch (Exception e)
                    {
                        if (_logger != null)
                            _logger.ZLogWarning($"TimeUpdate 통신 에러 ({i + 1}/{maxRetries}): {e.Message}");
                        if (i < maxRetries - 1) await UniTask.Delay(TimeSpan.FromSeconds(1.0));
                    }
                }
            }
        }

        public void SendValueUpdateAPI(int qNo, string side, int value)
        {
#if UNITY_EDITOR
            if (_logger != null) _logger.ZLogInformation($"에디터 모드: 가치관 데이터 전송 생략. 문항:{qNo}, 방향:{side}, 응답:{value}");
            return;
#endif
            if (CurrentUserId == 0 || ApiConfig == null)
            {
                if (_logger != null) _logger.ZLogWarning($"CurrentUserId가 0이거나 ApiConfig가 없음. 데이터 전송 취소.");
                return;
            }

            ValueUpdateAsync(qNo, side, value).Forget();
        }

        private async UniTaskVoid ValueUpdateAsync(int qNo, string side, int value)
        {
            string safeSide = Uri.EscapeDataString(side ?? string.Empty);
            string url =
                $"{ApiConfig.UpdateValueUrl}?idx_user={CurrentUserId}&q_no={qNo}&side={safeSide}&code=c2&value={value}";
            int maxRetries = 10;

            for (int i = 0; i < maxRetries; i++)
            {
                using (UnityWebRequest req = UnityWebRequest.Get(url))
                {
                    req.timeout = 10;
                    try
                    {
                        await req.SendWebRequest().ToUniTask();
                        return;
                    }
                    catch (Exception e)
                    {
                        if (_logger != null)
                            _logger.ZLogWarning($"ValueUpdate 통신 에러 ({i + 1}/{maxRetries}): {e.Message}");
                        if (i < maxRetries - 1) await UniTask.Delay(TimeSpan.FromSeconds(1.0));
                    }
                }
            }
        }

        public void SendPieceUpdateAPI(int value)
        {
#if UNITY_EDITOR
            if (_logger != null) _logger.ZLogInformation($"에디터 모드: 마음 조각 개수 갱신 API 전송 생략. 추가 획득량:{value}");
            return;
#endif
            if (CurrentUserId == 0 || ApiConfig == null) return;

            PieceUpdateAsync(value).Forget();
        }

        private async UniTaskVoid PieceUpdateAsync(int value)
        {
            string url = $"{ApiConfig.UpdatePieceUrl}?idx_user={CurrentUserId}&code=c2&value={value}";
            int maxRetries = 10;

            for (int i = 0; i < maxRetries; i++)
            {
                using (UnityWebRequest req = UnityWebRequest.Get(url))
                {
                    req.timeout = 10;
                    try
                    {
                        await req.SendWebRequest().ToUniTask();
                        return;
                    }
                    catch (Exception e)
                    {
                        if (_logger != null)
                            _logger.ZLogWarning($"PieceUpdate 통신 에러 ({i + 1}/{maxRetries}): {e.Message}");
                        if (i < maxRetries - 1) await UniTask.Delay(TimeSpan.FromSeconds(1.0));
                    }
                }
            }
        }

        private bool WantsToQuit()
        {
            if (_isQuitSafe) return true;

            if (!_isQuitting)
            {
                _isQuitting = true;
                QuitAsync().Forget();
            }

            return false;
        }

        private async UniTaskVoid QuitAsync()
        {
#if !UNITY_EDITOR
            if (CurrentUserId != 0 && ApiConfig != null)
            {   
                string resetUrl = $"{ApiConfig.ResetStartUrl}?idx_user={CurrentUserId}&code=c2";
                using (UnityWebRequest req = UnityWebRequest.Get(resetUrl))
                {   
                    req.timeout = 2; 
                    try { await req.SendWebRequest().ToUniTask(); } catch { }
                }

                string exitUrl = $"{ApiConfig.ExitRoomUrl}?code=c2&idx_user={CurrentUserId}";
                using (UnityWebRequest req = UnityWebRequest.Get(exitUrl))
                {   
                    req.timeout = 2;
                    try { await req.SendWebRequest().ToUniTask(); } catch { }
                }
            }
#endif
            _isQuitSafe = true;

#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

#if UNITY_EDITOR
        private void OnApplicationQuit()
        {
            if (_isQuitSafe) return;

            _isQuitSafe = true;
        }
#endif
    }
}