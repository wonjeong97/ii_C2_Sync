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
                
                Application.wantsToQuit += WantsToQuit; 
            }
            else
            {
                Destroy(gameObject);
                return;
            }

            if (!reporter) reporter = FindObjectOfType<Reporter>();
        }

        private void Start()
        {
            Cursor.visible = false;
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

        private void LoadSettings()
        {
            Settings settings = JsonLoader.Load<Settings>(GameConstants.Path.JsonSetting); 
            if (settings != null) _fadeTime = settings.fadeTime;
            else _fadeTime = 1.0f;

            _systemData = JsonLoader.Load<SystemData>(GameConstants.Path.System);
            ApiConfig = JsonLoader.Load<ApiSettings>(GameConstants.Path.ApiSetting);
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.D) && reporter)
            {
                reporter.showGameManagerControl = !reporter.showGameManagerControl;
                if (reporter.show) reporter.show = false;
            }
            else if (Input.GetKeyDown(KeyCode.M)) Cursor.visible = !Cursor.visible;

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
                case ColorData.Cyan:   return new Color32(113, 177, 158, 255);
                case ColorData.Pink:   return new Color32(240, 60, 102, 255);
                case ColorData.Orange: return new Color32(240, 103, 27, 255);
                case ColorData.Green:  return new Color32(98, 125, 23, 255);
                case ColorData.Red:    return new Color32(191, 82, 77, 255);
                case ColorData.Yellow: return new Color32(243, 203, 38, 255);
                default:               return Color.white;
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

        private void HandlePadInputForInactivity(int playerIdx, int laneIdx, int padIdx)
        {
            ResetInactivityTimer();
        }

        private void HandleRawHardwareInput(int padNumber, bool isDown)
        {
            ResetInactivityTimer();
        }

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

        public void ForceInactivitySequence()
        {
            if (!_isInactivitySequenceRunning)
            {
                _currentInactivityTimer = 20f; 
                _inactivityCoroutine = StartCoroutine(InactivitySequenceRoutine());
            }
        }

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
            else if (_systemPopupText) _systemPopupText.text = "움직여주세요"; 

            _popupFadeCoroutine = StartCoroutine(FadeSystemPopup(0f, 1f, 0.5f));
            yield return _popupFadeCoroutine;

            yield return new WaitForSeconds(3.0f);
            _popupFadeCoroutine = StartCoroutine(FadeSystemPopup(1f, 0f, 0.5f));
            yield return _popupFadeCoroutine;

            if (SoundManager.Instance) SoundManager.Instance.PlaySFX("공통_15_10초");
            yield return new WaitForSeconds(10.0f);

            if (_systemData != null && _systemData.inactivityResetText != null && _systemPopupText)
            {
                if (UIManager.Instance) UIManager.Instance.SetText(_systemPopupText.gameObject, _systemData.inactivityResetText);
                else _systemPopupText.text = _systemData.inactivityResetText.text;
            }
            else if (_systemPopupText) _systemPopupText.text = "동작이 인식 되지 않아 초기화 됩니다"; 

            _popupFadeCoroutine = StartCoroutine(FadeSystemPopup(0f, 1f, 0.5f));
            yield return _popupFadeCoroutine;

            SendResetStartAPI();
            SendExitRoomAPI();

            yield return new WaitForSeconds(3.0f);

            ReturnToTitle();
        }

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
    
        public void ChangeScene(string sceneName)
        {
            if (_isTransitioning) return;
            _isTransitioning = true;
            ResetInactivityTimer();
            StartCoroutine(ChangeSceneRoutine(sceneName));
        }

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
                Debug.LogWarning($"[GameManager] FadeOut 호출 실패. 강제 진행: {e.Message}");
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
                Debug.LogWarning($"[GameManager] FadeIn 호출 실패: {e.Message}");
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
            
            if (SessionManager.Instance) SessionManager.Instance.ClearSession();

            ChangeScene(GameConstants.Scene.Title);
        }

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
                    
                    Debug.LogWarning($"[API] CheckRoomState 통신 에러 ({i + 1}/{maxRetries}): {req.error}");
                    if (i < maxRetries - 1) yield return new WaitForSeconds(1.0f);
                }
            }

            callback?.Invoke("EMPTY");
        }

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
                    
                    Debug.LogWarning($"[API] GetCurrentRoomUser 통신 에러 ({i + 1}/{maxRetries}): {req.error}");
                    if (i < maxRetries - 1) yield return new WaitForSeconds(1.0f);
                }
            }
            
            callback?.Invoke("EMPTY");
        }

        public void SendResetStartAPI()
        {
#if UNITY_EDITOR
            // 이유: 에디터 테스트 중 실수로 실제 DB 룸을 리셋시키는 현상을 방지함.
            Debug.Log("[API - Editor] 룸 리셋 API 전송 생략");
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
                    
                    Debug.LogWarning($"[API] ResetStart 통신 에러 ({i + 1}/{maxRetries}): {req.error}");
                    if (i < maxRetries - 1) yield return new WaitForSeconds(1.0f);
                }
            }
        }

        public void SendExitRoomAPI()
        {
#if UNITY_EDITOR
            // 이유: 에디터 테스트 중 실제 유저의 퇴장 상태를 오염시키지 않기 위함.
            Debug.Log("[API - Editor] 룸 퇴장 API 전송 생략");
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
                    
                    Debug.LogWarning($"[API] ExitRoom 통신 에러 ({i + 1}/{maxRetries}): {req.error}");
                    if (i < maxRetries - 1) yield return new WaitForSeconds(1.0f);
                }
            }
        }

        public void SendTimeUpdateAPI()
        {
#if UNITY_EDITOR
            // 이유: 가짜 유저의 종료 시간이 DB에 삽입되는 것을 방지함.
            Debug.Log("[API - Editor] 게임 종료 시간 업데이트 API 전송 생략");
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
                    
                    Debug.LogWarning($"[API] TimeUpdate 통신 에러 ({i + 1}/{maxRetries}): {req.error}");
                    if (i < maxRetries - 1) yield return new WaitForSeconds(1.0f);
                }
            }
        }

        /// <summary> PlayShort 등의 질문에 대한 플레이어의 응답 값을 API 서버에 전송합니다. </summary>
        public void SendValueUpdateAPI(int qNo, string side, int value)
        {
#if UNITY_EDITOR
            // 이유: 가치관 테스트 결과 더미 데이터가 라이브 DB에 업로드되는 것을 방지함.
            Debug.Log($"[API - Editor] 가치관 데이터 전송 생략 (문항:{qNo}, 방향:{side}, 응답:{value})");
            return;
#endif
            if (CurrentUserId == 0 || ApiConfig == null)
            {
                Debug.LogWarning("[GameManager] CurrentUserId가 0이거나 ApiConfig가 없습니다. 가치관 데이터 전송 실패.");
                return;
            }
            StartCoroutine(ValueUpdateRoutine(qNo, side, value));
        }

        /// <summary>
        /// 답변을 서버에 업로드하는 실질적인 통신 코루틴.
        /// 통신이 불안정할 경우 데이터 유실을 방지하기 위해 타임아웃 10초, 실패 시 1초 대기 후 최대 10회까지 재시도함.
        /// </summary>
        private IEnumerator ValueUpdateRoutine(int qNo, string side, int value)
        {
            string safeSide = Uri.EscapeDataString(side ?? string.Empty);
            string url = $"{ApiConfig.UpdateValueUrl}?idx_user={CurrentUserId}&q_no={qNo}&side={safeSide}&code=c2&value={value}";
            int maxRetries = 10; // 이유: 일시적인 네트워크 장애를 극복하기 위한 최대 재시도 횟수 지정

            for (int i = 0; i < maxRetries; i++)
            {
                using (UnityWebRequest req = UnityWebRequest.Get(url))
                {
                    req.timeout = 10; // 이유: 서버 지연 시 무한 대기를 막기 위한 10초 타임아웃 설정
                    yield return req.SendWebRequest();
                    
                    // 이유: 정상적으로 데이터가 전송되었다면 즉시 코루틴을 종료함
                    if (req.result == UnityWebRequest.Result.Success) yield break;
                    
                    Debug.LogWarning($"[API] ValueUpdate 통신 에러 ({i + 1}/{maxRetries}): {req.error}");
                    
                    // 이유: 재시도 전 서버 및 네트워크 부하를 줄이기 위해 1초간 대기함
                    if (i < maxRetries - 1) yield return CoroutineData.GetWaitForSeconds(1.0f);
                }
            }
        }

        public void SendPieceUpdateAPI(int value)
        {
#if UNITY_EDITOR
            // 이유: 더미 마음 조각 데이터가 DB에 업로드되어 다른 콘텐츠 진엔딩 판별에 오류를 주는 것을 방지함.
            Debug.Log($"[API - Editor] 마음 조각 개수 갱신 API 전송 생략 (추가 획득량:{value})");
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
                    
                    Debug.LogWarning($"[API] PieceUpdate 통신 에러 ({i + 1}/{maxRetries}): {req.error}");
                    if (i < maxRetries - 1) yield return new WaitForSeconds(1.0f);
                }
            }
        }

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

        private IEnumerator QuitRoutine()
        {
#if !UNITY_EDITOR
            if (CurrentUserId != 0 && ApiConfig != null)
            {   
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