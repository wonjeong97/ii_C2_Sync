using System;
using System.Collections;
using Cysharp.Threading.Tasks;
using My.Scripts.Core.Data;
using My.Scripts.Global;
using My.Scripts.Hardware;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Wonjeong.Data;
using Wonjeong.Reporter;
using Wonjeong.UI;
using Wonjeong.Utils;

namespace My.Scripts.Core
{
    public enum InactivityTextType { Warning, Tag }

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
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance; 

        [SerializeField] private Reporter reporter; 

        [Header("System Popup (Inactivity)")]
        public GameObject systemPopupPrefab;

        [Header("Debug / Testing")]
        public float lastPlayDistance = 100f;
        [Tooltip("체크 시 서버 API 응답을 무시하고 강제로 아래의 UserType을 적용합니다.")]
        public bool forceUserType = false; 
        public UserType debugUserType = UserType.A1; 
        
        private bool isAutoProgressing = false; 

        private SystemData _systemData; 
        private float _currentInactivityTimer; 
        private bool _isTransitioning; 
        private float _fadeTime = 0.5f; 
        
        private bool _isInactivitySequenceRunning;
        private Coroutine _inactivityCoroutine;
        private Coroutine _popupFadeCoroutine;

        private CanvasGroup _systemPopupCg;
        private Text _systemPopupText;
        
        private bool _isQuitting;
        private bool _isQuitSafe;

        public bool IsAutoProgressing { get => isAutoProgressing; set => isAutoProgressing = value; }
        public InactivityTextType CurrentInactivityTextType { get; set; } = InactivityTextType.Warning;
        
        public ApiSettings ApiConfig { get; set; } 
        
        public int CurrentUserId => SessionManager.Instance ? SessionManager.Instance.CurrentUserId : 0;
        public string CurrentLanguage => SessionManager.Instance ? SessionManager.Instance.CurrentLanguage : "ko";
        public string Cartridge => SessionManager.Instance ? SessionManager.Instance.Cartridge : "";
        public bool IsOtherCartridgeContentsCleared => SessionManager.Instance && SessionManager.Instance.IsOtherCartridgeContentsCleared;

        public string PlayerAName => SessionManager.Instance ? SessionManager.Instance.PlayerAFirstName : "Player A";
        public string PlayerBName => SessionManager.Instance ? SessionManager.Instance.PlayerBFirstName : "Player B";
        
        public ColorData PlayerAColor => SessionManager.Instance ? SessionManager.Instance.PlayerAColor : ColorData.NotSet;
        public ColorData PlayerBColor => SessionManager.Instance ? SessionManager.Instance.PlayerBColor : ColorData.NotSet;

        public UserType currentUserType 
        {
            get 
            {
                if (forceUserType) return debugUserType;
                return SessionManager.Instance ? SessionManager.Instance.CurrentUserType : UserType.A1;
            }
            set { if (SessionManager.Instance) SessionManager.Instance.CurrentUserType = value; }
        }

        public int PieceC2 
        {
            get => SessionManager.Instance ? SessionManager.Instance.PieceC2 : 0;
            set { if (SessionManager.Instance) SessionManager.Instance.PieceC2 = value; }
        }
        
        public int TotalPieces => SessionManager.Instance ? SessionManager.Instance.TotalPieces : 0;

        public event Action OnUserDataUpdated;
        
        [Header("Player Color Sprites")]
        public Sprite[] playerColorSprites;
        
        /// <summary>
        /// 싱글톤 초기화 및 필수 세션 매니저 동적 생성.
        /// </summary>
        private void Awake()
        {
            if (!Instance)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
                
                if (!SessionManager.Instance)
                {
                    GameObject sessionObj = new GameObject("SessionManager");
                    sessionObj.AddComponent<SessionManager>();
                }
                
                // 이유: 게임 종료 프로세스 가로채기를 통해 서버에 종료 및 리셋 API를 전송하기 위함.
                Application.wantsToQuit += WantsToQuit; 
            }
            else
            {
                Destroy(gameObject);
                return;
            }

            if (!reporter) reporter = FindObjectOfType<Reporter>();
        }

        /// <summary>
        /// 설정 데이터 로드 및 전역 방치 타이머 팝업 초기화.
        /// </summary>
        private void Start()
        {
            Cursor.visible = false;
            Cursor.lockState = CursorLockMode.Locked;
            Application.runInBackground = true;
            
            LoadSettings();

            if (reporter && reporter.show) reporter.show = false;

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

            if (InputManager.Instance)
            {
                InputManager.Instance.OnPadDown -= HandlePadInputForInactivity; 
                InputManager.Instance.OnPadDown += HandlePadInputForInactivity;
                
                InputManager.Instance.OnPadUp -= HandlePadInputForInactivity; 
                InputManager.Instance.OnPadUp += HandlePadInputForInactivity;
            }

            if (ArduinoManager.Instance)
            {
                ArduinoManager.Instance.OnHardwareInput -= HandleRawHardwareInput;
                ArduinoManager.Instance.OnHardwareInput += HandleRawHardwareInput;
            }
        }

        /// <summary>
        /// 객체 파괴 시 이벤트 구독 해제.
        /// </summary>
        private void OnDestroy()
        {
            if (Instance == this) Application.wantsToQuit -= WantsToQuit;

            if (InputManager.Instance)
            {
                InputManager.Instance.OnPadDown -= HandlePadInputForInactivity;
                InputManager.Instance.OnPadUp -= HandlePadInputForInactivity;
            }

            if (ArduinoManager.Instance)
            {
                ArduinoManager.Instance.OnHardwareInput -= HandleRawHardwareInput;
            }
        }

        /// <summary>
        /// JSON 파일로부터 시스템 및 API 설정 데이터를 로드함.
        /// </summary>
        private void LoadSettings()
        {
            Settings settings = JsonLoader.Load<Settings>(GameConstants.Path.JsonSetting); 
            if (settings != null) _fadeTime = settings.fadeTime;
            else _fadeTime = 1.0f;

            _systemData = JsonLoader.Load<SystemData>(GameConstants.Path.System);
            ApiConfig = JsonLoader.Load<ApiSettings>(GameConstants.Path.ApiSetting);
            
            if (ApiConfig == null) Debug.LogWarning("API 설정 파일 로드 실패.");
        }

        /// <summary>
        /// 매 프레임 디버그 단축키 처리 및 무입력 방치 타이머 갱신.
        /// </summary>
        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.D) && reporter)
            {
                reporter.showGameManagerControl = !reporter.showGameManagerControl;
                if (reporter.show) reporter.show = false;
            }
            else if (Input.GetKeyDown(KeyCode.M)) 
            {
                Cursor.visible = !Cursor.visible;
                Cursor.lockState = Cursor.visible ? CursorLockMode.None : CursorLockMode.Locked;
            }

            if (_isTransitioning) return;
            HandleInactivity();
        }
        
        /// <summary>
        /// 유저 데이터가 로드되거나 변경되었을 때 구독자들에게 알림.
        /// </summary>
        public void NotifyUserDataUpdated() => OnUserDataUpdated?.Invoke();

        /// <summary>
        /// 색상 데이터에 해당하는 스프라이트 에셋을 반환함.
        /// </summary>
        /// <param name="color">열거형 색상 데이터</param>
        /// <returns>매칭된 스프라이트 또는 null</returns>
        public Sprite GetColorSprite(ColorData color)
        {
            int index = (int)color;
            if (index >= 0 && playerColorSprites != null && index < playerColorSprites.Length)
                return playerColorSprites[index];
            return null;
        }
        
        /// <summary>
        /// 색상 데이터에 해당하는 실제 Color32 구조체를 반환함. (스프라이트가 없을 때 Fallback 용도)
        /// </summary>
        /// <param name="colorData">열거형 색상 데이터</param>
        /// <returns>매칭된 Color</returns>
        public Color GetColorFromData(ColorData colorData)
        {
            switch (colorData)
            {
                case ColorData.Cyan:   return new Color32(113, 177, 158, 255);
                case ColorData.Pink:   return new Color32(240, 60, 102, 255);
                case ColorData.Orange: return new Color32(240, 103, 27, 255);
                case ColorData.Green:  return new Color32(98, 125, 23, 255);
                case ColorData.Red:    return new Color32(191, 82, 77, 255);
                case ColorData.Yellow: return new Color32(243, 203, 38, 255);
                default:               return Color.white;
            }
        }

        /// <summary>
        /// 현재 유저 타입 및 질문 번호에 따라 서버에 전송할 레벨 접미사를 생성함.
        /// </summary>
        /// <param name="questionNumber">현재 진행 중인 질문 번호</param>
        /// <returns>가치관 접미사 문자열 (예: "_A")</returns>
        public string GetLevelSuffix(int questionNumber)
        {
            if (questionNumber <= 0) return ""; 

            string typeStr = currentUserType.ToString();
            char relationChar = typeStr.Length > 1 ? typeStr[1] : '1';

            // 이유: 기획된 관계성(1~6)과 특정 질문 번호의 분기 조합에 따라 결과값을 반환함.
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

        /// <summary>
        /// 전역 무입력 감지 루틴.
        /// 지정된 시간(20초) 동안 키보드, 마우스, 패드, 터치 입력이 없으면 경고 팝업을 띄움.
        /// </summary>
        private void HandleInactivity()
        {
            // 이유: 타이틀 화면이거나 자동 진행(연출) 중이거나 엔딩 씬에서는 무입력 방치 체크를 하지 않음.
            if (SceneManager.GetActiveScene().name == GameConstants.Scene.Title || isAutoProgressing
                || SceneManager.GetActiveScene().name == GameConstants.Scene.Ending)
            {
                ResetInactivityTimer();
                return;
            }

            if (Input.anyKeyDown || Input.GetMouseButtonDown(0) || (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began))
            {
                ResetInactivityTimer();
            }
            else
            {
                if (!_isInactivitySequenceRunning)
                {
                    _currentInactivityTimer += Time.deltaTime;
                    if (_currentInactivityTimer >= 20f)
                    {
                        _inactivityCoroutine = StartCoroutine(InactivitySequenceRoutine());
                    }
                }
            }
        }

        /// <summary> 발판 입력 시 방치 타이머 리셋. </summary>
        private void HandlePadInputForInactivity(int playerIdx, int laneIdx, int padIdx)
        {
            ResetInactivityTimer();
        }

        /// <summary> 하드웨어 직접 입력 시 방치 타이머 리셋. </summary>
        private void HandleRawHardwareInput(int padNumber, bool isDown)
        {
            ResetInactivityTimer();
        }

        /// <summary>
        /// 무입력 방치 타이머를 초기화하고 실행 중이던 경고 팝업을 강제 종료함.
        /// </summary>
        public void ResetInactivityTimer()
        {
            _currentInactivityTimer = 0f;
            
            if (_isInactivitySequenceRunning)
            {
                _isInactivitySequenceRunning = false;
                if (_inactivityCoroutine != null) StopCoroutine(_inactivityCoroutine);
                
                if (_systemPopupCg)
                {
                    if (_popupFadeCoroutine != null) StopCoroutine(_popupFadeCoroutine);
                    _systemPopupCg.alpha = 0f;
                    _systemPopupCg.gameObject.SetActive(false);
                }
                
                if (SoundManager.Instance) SoundManager.Instance.StopSFX();
            }
        }

        /// <summary>
        /// 타이머 대기 없이 즉시 방치 시퀀스를 실행함.
        /// </summary>
        public void ForceInactivitySequence()
        {
            if (!_isInactivitySequenceRunning)
            {
                _currentInactivityTimer = 20f; 
                _inactivityCoroutine = StartCoroutine(InactivitySequenceRoutine());
            }
        }

        /// <summary>
        /// 방치 경고 팝업 노출, 카운트다운 사운드 재생 후 제한 시간 초과 시 타이틀로 강제 귀환 처리함.
        /// </summary>
        /// <returns>IEnumerator 루틴</returns>
        private IEnumerator InactivitySequenceRoutine()
        {
            _isInactivitySequenceRunning = true;

            TextSetting targetText = null;
            if (_systemData != null) targetText = CurrentInactivityTextType == InactivityTextType.Tag ? _systemData.tagText : _systemData.inactivityWarningText;

            if (targetText != null && _systemPopupText)
            {
                if (UIManager.Instance) UIManager.Instance.SetText(_systemPopupText.gameObject, targetText);
                else _systemPopupText.text = targetText.text;
            }
            else if (_systemPopupText)
            {
                // 이유: 텍스트 데이터가 없을 때를 대비한 기본 문자열 설정.
                _systemPopupText.text = "움직여주세요"; 
            }

            _popupFadeCoroutine = StartCoroutine(FadeSystemPopup(0f, 1f, 0.5f));
            yield return _popupFadeCoroutine;

            yield return CoroutineData.GetWaitForSeconds(3.0f);
            
            _popupFadeCoroutine = StartCoroutine(FadeSystemPopup(1f, 0f, 0.5f));
            yield return _popupFadeCoroutine;

            if (SoundManager.Instance) SoundManager.Instance.PlaySFX("공통_15_10초");
            
            yield return CoroutineData.GetWaitForSeconds(10.0f);

            if (_systemData != null && _systemData.inactivityResetText != null && _systemPopupText)
            {
                if (UIManager.Instance) UIManager.Instance.SetText(_systemPopupText.gameObject, _systemData.inactivityResetText);
                else _systemPopupText.text = _systemData.inactivityResetText.text;
            }
            else if (_systemPopupText)
            {
                _systemPopupText.text = "동작이 인식 되지 않아 초기화 됩니다"; 
            }

            _popupFadeCoroutine = StartCoroutine(FadeSystemPopup(0f, 1f, 0.5f));
            yield return _popupFadeCoroutine;

            SendResetStartAPI();
            SendExitRoomAPI();

            yield return CoroutineData.GetWaitForSeconds(3.0f);

            ReturnToTitle();
        }

        /// <summary>
        /// 시스템 경고 팝업의 알파값을 보간하여 부드럽게 나타내거나 숨김.
        /// </summary>
        /// <param name="start">시작 알파값</param>
        /// <param name="end">종료 알파값</param>
        /// <param name="duration">소요 시간</param>
        /// <returns>IEnumerator 루틴</returns>
        private IEnumerator FadeSystemPopup(float start, float end, float duration)
        {
            if (!_systemPopupCg) yield break;
            
            _systemPopupCg.gameObject.SetActive(true);
            _systemPopupCg.alpha = start;
            
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                _systemPopupCg.alpha = Mathf.Lerp(start, end, elapsed / duration);
                yield return null;
            }
            
            _systemPopupCg.alpha = end;
            if (end <= 0f) _systemPopupCg.gameObject.SetActive(false);
        }
    
        /// <summary>
        /// 씬 전환 전역 메서드. 전환 중 방치 타이머를 리셋하고 페이드아웃 후 로드함.
        /// </summary>
        /// <param name="sceneName">이동할 씬 이름</param>
        public void ChangeScene(string sceneName)
        {
            if (_isTransitioning) return;
            
            _isTransitioning = true;
            ResetInactivityTimer();
            StartCoroutine(ChangeSceneRoutine(sceneName));
        }

        /// <summary>
        /// 글로벌 페이드 매니저를 활용한 비동기 씬 전환 코루틴.
        /// </summary>
        /// <param name="sceneName">이동할 씬 이름</param>
        /// <returns>IEnumerator 루틴</returns>
        private IEnumerator ChangeSceneRoutine(string sceneName)
        {
            if (!FadeManager.Instance)
            {
                SceneManager.LoadScene(sceneName);
                _isTransitioning = false;
                yield break;
            }

            bool fadeDone = false;
            try
            {
                FadeManager.Instance.FadeOut(_fadeTime, () => { fadeDone = true; });
            }
            catch (Exception e)
            {
                Debug.LogWarning($"FadeOut 호출 실패. 강제 진행: {e.Message}");
                fadeDone = true;
            }

            float timeout = Time.unscaledTime + _fadeTime + 1.0f;
            while (!fadeDone && Time.unscaledTime < timeout) 
            {
                yield return null;
            }

            AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(sceneName);
            while (asyncLoad != null && !asyncLoad.isDone) 
            {
                yield return null;
            }

            try
            {
                FadeManager.Instance.FadeIn(_fadeTime);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"FadeIn 호출 실패: {e.Message}");
            }
            
            _isTransitioning = false;
        }

        /// <summary>
        /// 팝업 노출 및 서버 API 통신 후 타이틀 화면으로 안전하게 복귀함.
        /// </summary>
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
            
            if (SessionManager.Instance) SessionManager.Instance.ClearSession();

            ChangeScene(GameConstants.Scene.Title);
        }

        /// <summary>
        /// 서버에 질의하여 현재 방의 사용 상태(EMPTY, USING 등)를 체크함.
        /// </summary>
        /// <param name="callback">응답 문자열을 받을 콜백 함수</param>
        /// <returns>IEnumerator 루틴</returns>
        public IEnumerator CheckRoomStateRoutine(Action<string> callback)
        {
#if UNITY_EDITOR
            callback?.Invoke("USING");
            yield break;
#endif
            if (ApiConfig == null)
            {
                callback?.Invoke("EMPTY");
                yield break;
            }

            string url = $"{ApiConfig.CheckRoomStateUrl}?code=c2";
            int maxRetries = 10;

            // # TODO: 반복적인 재시도 로직을 공용 메서드로 분리하여 코드 중복 방지 필요.
            for (int i = 0; i < maxRetries; i++)
            {
                using (UnityWebRequest req = UnityWebRequest.Get(url))
                {
                    req.timeout = 5;
                    yield return req.SendWebRequest();

                    if (req.result == UnityWebRequest.Result.Success)
                    {
                        callback?.Invoke(req.downloadHandler.text.Trim());
                        yield break;
                    }
                    
                    Debug.LogWarning($"CheckRoomState 통신 에러 ({i + 1}/{maxRetries}): {req.error}");
                    if (i < maxRetries - 1) yield return CoroutineData.GetWaitForSeconds(1.0f);
                }
            }

            callback?.Invoke("EMPTY");
        }

        /// <summary>
        /// 서버에 질의하여 현재 방에 할당된 유저의 UID 정보를 체크함.
        /// </summary>
        /// <param name="callback">응답 문자열을 받을 콜백 함수</param>
        /// <returns>IEnumerator 루틴</returns>
        public IEnumerator GetCurrentRoomUserRoutine(Action<string> callback)
        {
#if UNITY_EDITOR
            callback?.Invoke("TEST_UID_A,TEST_UID_B");
            yield break;
#endif
            if (ApiConfig == null)
            {
                callback?.Invoke("EMPTY");
                yield break;
            }

            string url = $"{ApiConfig.GetCurrentRoomUserUrl}?code=c2";
            int maxRetries = 10;

            for (int i = 0; i < maxRetries; i++)
            {
                using (UnityWebRequest req = UnityWebRequest.Get(url))
                {
                    req.timeout = 5;
                    yield return req.SendWebRequest();

                    if (req.result == UnityWebRequest.Result.Success)
                    {
                        callback?.Invoke(req.downloadHandler.text.Trim());
                        yield break;
                    }
                    
                    Debug.LogWarning($"GetCurrentRoomUser 통신 에러 ({i + 1}/{maxRetries}): {req.error}");
                    if (i < maxRetries - 1) yield return CoroutineData.GetWaitForSeconds(1.0f);
                }
            }
            
            callback?.Invoke("EMPTY");
        }

        /// <summary>
        /// 방에 할당된 유저의 시작 상태를 서버에서 초기화함.
        /// </summary>
        public void SendResetStartAPI()
        {
#if UNITY_EDITOR
            // 이유: 에디터 테스트 중 실수로 실제 DB 룸을 리셋시키는 현상을 방지함.
            Debug.Log("에디터 모드: 룸 리셋 API 전송 생략");
            return;
#endif
            if (CurrentUserId == 0 || ApiConfig == null) return;
            StartCoroutine(ResetStartRoutine());
        }

        private IEnumerator ResetStartRoutine()
        {
            string url = $"{ApiConfig.ResetStartUrl}?idx_user={CurrentUserId}&code=c2";
            int maxRetries = 10;

            for (int i = 0; i < maxRetries; i++)
            {
                using (UnityWebRequest req = UnityWebRequest.Get(url))
                {
                    req.timeout = 5;
                    yield return req.SendWebRequest();
                    
                    if (req.result == UnityWebRequest.Result.Success) yield break;
                    
                    Debug.LogWarning($"ResetStart 통신 에러 ({i + 1}/{maxRetries}): {req.error}");
                    if (i < maxRetries - 1) yield return CoroutineData.GetWaitForSeconds(1.0f);
                }
            }
        }

        /// <summary>
        /// 유저가 방에서 퇴장했음을 서버에 알림.
        /// </summary>
        public void SendExitRoomAPI()
        {
#if UNITY_EDITOR
            // 이유: 에디터 테스트 중 실제 유저의 퇴장 상태를 오염시키지 않기 위함.
            Debug.Log("에디터 모드: 룸 퇴장 API 전송 생략");
            return;
#endif
            if (CurrentUserId == 0 || ApiConfig == null) return;
            StartCoroutine(ExitRoomRoutine());
        }

        private IEnumerator ExitRoomRoutine()
        {
            string url = $"{ApiConfig.ExitRoomUrl}?code=c2&idx_user={CurrentUserId}";
            int maxRetries = 10;

            for (int i = 0; i < maxRetries; i++)
            {
                using (UnityWebRequest req = UnityWebRequest.Get(url))
                {
                    req.timeout = 5;
                    yield return req.SendWebRequest();
                    
                    if (req.result == UnityWebRequest.Result.Success) yield break;
                    
                    Debug.LogWarning($"ExitRoom 통신 에러 ({i + 1}/{maxRetries}): {req.error}");
                    if (i < maxRetries - 1) yield return CoroutineData.GetWaitForSeconds(1.0f);
                }
            }
        }

        /// <summary>
        /// 현재 진행 상황에 대한 종료 타임스탬프를 서버에 전송함.
        /// </summary>
        public void SendTimeUpdateAPI()
        {
#if UNITY_EDITOR
            // 이유: 가짜 유저의 종료 시간이 DB에 삽입되는 것을 방지함.
            Debug.Log("에디터 모드: 게임 종료 시간 업데이트 API 전송 생략");
            return;
#endif
            if (CurrentUserId == 0 || ApiConfig == null) return;
            StartCoroutine(TimeUpdateRoutine());
        }

        private IEnumerator TimeUpdateRoutine()
        {
            string url = $"{ApiConfig.UpdateTimeUrl}?idx_user={CurrentUserId}&option=end&code=c2";
            int maxRetries = 10;

            for (int i = 0; i < maxRetries; i++)
            {
                using (UnityWebRequest req = UnityWebRequest.Get(url))
                {
                    req.timeout = 10;
                    yield return req.SendWebRequest();
                    
                    if (req.result == UnityWebRequest.Result.Success) yield break;
                    
                    Debug.LogWarning($"TimeUpdate 통신 에러 ({i + 1}/{maxRetries}): {req.error}");
                    if (i < maxRetries - 1) yield return CoroutineData.GetWaitForSeconds(1.0f);
                }
            }
        }

        /// <summary> 
        /// PlayShort 등의 질문에 대한 플레이어의 응답 값을 API 서버에 전송함.
        /// </summary>
        /// <param name="qNo">질문 번호</param>
        /// <param name="side">응답한 플레이어 측 (left/right)</param>
        /// <param name="value">응답 값</param>
        public void SendValueUpdateAPI(int qNo, string side, int value)
        {
#if UNITY_EDITOR
            // 이유: 가치관 테스트 결과 더미 데이터가 라이브 DB에 업로드되는 것을 방지함.
            Debug.Log($"에디터 모드: 가치관 데이터 전송 생략. 문항:{qNo}, 방향:{side}, 응답:{value}");
            return;
#endif
            if (CurrentUserId == 0 || ApiConfig == null)
            {
                Debug.LogWarning("CurrentUserId가 0이거나 ApiConfig가 없음. 데이터 전송 취소.");
                return;
            }
            StartCoroutine(ValueUpdateRoutine(qNo, side, value));
        }

        /// <summary>
        /// 답변을 서버에 업로드하는 실질적인 통신 코루틴.
        /// </summary>
        /// <param name="qNo">질문 번호</param>
        /// <param name="side">응답한 방향</param>
        /// <param name="value">응답 값</param>
        /// <returns>IEnumerator 루틴</returns>
        private IEnumerator ValueUpdateRoutine(int qNo, string side, int value)
        {
            string safeSide = Uri.EscapeDataString(side ?? string.Empty);
            string url = $"{ApiConfig.UpdateValueUrl}?idx_user={CurrentUserId}&q_no={qNo}&side={safeSide}&code=c2&value={value}";
            
            // 이유: 일시적인 네트워크 장애를 극복하기 위한 최대 재시도 횟수 지정.
            int maxRetries = 10; 

            for (int i = 0; i < maxRetries; i++)
            {
                using (UnityWebRequest req = UnityWebRequest.Get(url))
                {
                    // 이유: 서버 지연 시 무한 대기를 막기 위한 타임아웃 설정.
                    req.timeout = 10; 
                    yield return req.SendWebRequest();
                    
                    // 이유: 정상적으로 데이터가 전송되었다면 즉시 코루틴을 종료함.
                    if (req.result == UnityWebRequest.Result.Success) yield break;
                    
                    Debug.LogWarning($"ValueUpdate 통신 에러 ({i + 1}/{maxRetries}): {req.error}");
                    
                    // 이유: 재시도 전 서버 및 네트워크 부하를 줄이기 위해 1초간 대기함.
                    if (i < maxRetries - 1) yield return CoroutineData.GetWaitForSeconds(1.0f);
                }
            }
        }

        /// <summary>
        /// 수집한 마음 조각의 증가분을 서버에 전송함.
        /// </summary>
        /// <param name="value">획득한 조각 개수</param>
        public void SendPieceUpdateAPI(int value)
        {
#if UNITY_EDITOR
            // 이유: 더미 마음 조각 데이터가 DB에 업로드되어 다른 콘텐츠 진엔딩 판별에 오류를 주는 것을 방지함.
            Debug.Log($"에디터 모드: 마음 조각 개수 갱신 API 전송 생략. 추가 획득량:{value}");
            return;
#endif
            if (CurrentUserId == 0 || ApiConfig == null) return;
            StartCoroutine(PieceUpdateRoutine(value));
        }

        private IEnumerator PieceUpdateRoutine(int value)
        {
            string url = $"{ApiConfig.UpdatePieceUrl}?idx_user={CurrentUserId}&code=c2&value={value}";
            int maxRetries = 10;

            for (int i = 0; i < maxRetries; i++)
            {
                using (UnityWebRequest req = UnityWebRequest.Get(url))
                {
                    req.timeout = 10;
                    yield return req.SendWebRequest();
                    
                    if (req.result == UnityWebRequest.Result.Success) yield break;
                    
                    Debug.LogWarning($"PieceUpdate 통신 에러 ({i + 1}/{maxRetries}): {req.error}");
                    if (i < maxRetries - 1) yield return CoroutineData.GetWaitForSeconds(1.0f);
                }
            }
        }

        /// <summary>
        /// 게임이 외부 요인(Alt+F4 등)으로 종료될 때 호출되어 안전한 종료 시퀀스를 삽입함.
        /// </summary>
        /// <returns>즉시 종료 허용 여부</returns>
        private bool WantsToQuit()
        {
            if (_isQuitSafe) return true;

            if (!_isQuitting)
            {
                _isQuitting = true;
                StartCoroutine(QuitRoutine());
            }
            return false; 
        }

        /// <summary>
        /// 종료 전 서버에 초기화 및 퇴장 API를 동기적으로 전송하고 강제 종료함.
        /// </summary>
        /// <returns>IEnumerator 루틴</returns>
        private IEnumerator QuitRoutine()
        {
#if !UNITY_EDITOR
            if (CurrentUserId != 0 && ApiConfig != null)
            {   
                // 이유: 게임이 비정상 종료되더라도 다음 게임에 영향을 주지 않게 강제로 상태를 롤백함.
                string resetUrl = $"{ApiConfig.ResetStartUrl}?idx_user={CurrentUserId}&code=c2";
                using (UnityWebRequest req = UnityWebRequest.Get(resetUrl))
                {   
                    req.timeout = 2; 
                    yield return req.SendWebRequest();
                }

                string exitUrl = $"{ApiConfig.ExitRoomUrl}?code=c2&idx_user={CurrentUserId}";
                using (UnityWebRequest req = UnityWebRequest.Get(exitUrl))
                {   
                    req.timeout = 2;
                    yield return req.SendWebRequest();
                }
            }
#endif
            _isQuitSafe = true; 
            
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit(); 
#endif
            yield break;
        }

#if UNITY_EDITOR
        private void OnApplicationQuit()
        {
            // 에디터에서는 강제 종료 시 API 통신 생략
            if (_isQuitSafe) return; 
            _isQuitSafe = true;
        }
#endif
    }
}