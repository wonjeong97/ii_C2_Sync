using System;
using System.Collections;
using System.IO;
using Cysharp.Threading.Tasks;
using My.Scripts.Core.Data;
using My.Scripts.Global;
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
        
        // --- Session Data Proxy ---
        // 하위 호환성을 유지하기 위해 데이터는 SessionManager에서 꺼내오도록 중계(Proxy)합니다.
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
            get => SessionManager.Instance ? SessionManager.Instance.CurrentUserType : UserType.A;
            set { if (SessionManager.Instance) SessionManager.Instance.CurrentUserType = value; }
        }

        public int PieceC2 
        {
            get => SessionManager.Instance ? SessionManager.Instance.PieceC2 : 0;
            set { if (SessionManager.Instance) SessionManager.Instance.PieceC2 = value; }
        }
        
        public int TotalPieces => SessionManager.Instance ? SessionManager.Instance.TotalPieces : 0;
        // -----------------------------

        public event Action OnUserDataUpdated;
        
        [Header("Player Color Sprites")]
        public Sprite[] playerColorSprites;
        
        private void Awake()
        {
            if (!Instance)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
                
                // SessionManager 자동 생성 보장
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
        }

        private void OnDestroy()
        {
            if (Instance == this) Application.wantsToQuit -= WantsToQuit;
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

            switch (currentUserType)
            {
                case UserType.A: return "_A"; 
                case UserType.B: return (questionNumber == 4) ? "_B" : "_A";
                case UserType.C:
                    if (questionNumber == 4 || questionNumber == 10 || questionNumber == 11 || 
                        questionNumber == 13 || questionNumber == 14 || questionNumber == 15) return "_C";
                    return "_A";
                case UserType.D: return "_D"; 
                case UserType.E: return "_E"; 
                case UserType.F: return "_F";
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

            _isInactivitySequenceRunning = false;
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
            FadeManager.Instance.FadeOut(_fadeTime, () => { fadeDone = true; });
            while (!fadeDone) yield return null;

            AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(sceneName);
            while (asyncLoad != null && !asyncLoad.isDone) yield return null;

            FadeManager.Instance.FadeIn(_fadeTime);
            _isTransitioning = false;
        }

        public void ReturnToTitle()
        {
            if (_isTransitioning) return;
            
            isAutoProgressing = false;
            CurrentInactivityTextType = InactivityTextType.Warning; 
            ResetInactivityTimer();
            
            if (SessionManager.Instance) SessionManager.Instance.ClearSession();

            ChangeScene(GameConstants.Scene.Title);
        }
        
        #region API 호출 로직

        public IEnumerator CheckRoomStateRoutine(Action<string> callback)
        {
            if (ApiConfig == null)
            {
                if (callback != null) callback("EMPTY");
                yield break;
            }

            string url = $"{ApiConfig.CheckRoomStateUrl}?code=c2";
            using (UnityWebRequest req = UnityWebRequest.Get(url))
            {
                req.timeout = 5;
                yield return req.SendWebRequest();

                if (req.result == UnityWebRequest.Result.Success)
                {
                    if (callback != null) callback(req.downloadHandler.text.Trim());
                }
                else
                {
                    Debug.LogWarning($"[API] CheckRoomState 통신 에러: {req.error}");
                    if (callback != null) callback("EMPTY");
                }
            }
        }

        public IEnumerator GetCurrentRoomUserRoutine(Action<string> callback)
        {
            if (ApiConfig == null)
            {
                if (callback != null) callback("EMPTY");
                yield break;
            }

            string url = $"{ApiConfig.GetCurrentRoomUserUrl}?code=c2";
            using (UnityWebRequest req = UnityWebRequest.Get(url))
            {
                req.timeout = 5;
                yield return req.SendWebRequest();

                if (req.result == UnityWebRequest.Result.Success)
                {
                    if (callback != null) callback(req.downloadHandler.text.Trim());
                }
                else
                {
                    Debug.LogWarning($"[API] GetCurrentRoomUser 통신 에러: {req.error}");
                    if (callback != null) callback("EMPTY");
                }
            }
        }

        public void SendResetStartAPI()
        {
            if (CurrentUserId == 0 || ApiConfig == null) return;
            StartCoroutine(ResetStartRoutine());
        }

        private IEnumerator ResetStartRoutine()
        {
            string url = $"{ApiConfig.ResetStartUrl}?idx_user={CurrentUserId}&code=c2";
            using (UnityWebRequest req = UnityWebRequest.Get(url))
            {
                req.timeout = 5;
                yield return req.SendWebRequest();
            }
        }

        public void SendExitRoomAPI()
        {
            if (CurrentUserId == 0 || ApiConfig == null) return;
            StartCoroutine(ExitRoomRoutine());
        }

        private IEnumerator ExitRoomRoutine()
        {
            string url = $"{ApiConfig.ExitRoomUrl}?code=c2&idx_user={CurrentUserId}";
            using (UnityWebRequest req = UnityWebRequest.Get(url))
            {
                req.timeout = 5;
                yield return req.SendWebRequest();
            }
        }

        public void SendTimeUpdateAPI()
        {
            if (CurrentUserId == 0 || ApiConfig == null) return;
            StartCoroutine(TimeUpdateRoutine());
        }

        private IEnumerator TimeUpdateRoutine()
        {
            string url = $"{ApiConfig.UpdateTimeUrl}?idx_user={CurrentUserId}&option=end&code=c2";
            using (UnityWebRequest req = UnityWebRequest.Get(url))
            {
                req.timeout = 10;
                yield return req.SendWebRequest();
            }
        }

        public void SendValueUpdateAPI(int qNo, string side, int value)
        {
            if (CurrentUserId == 0 || ApiConfig == null) return;
            StartCoroutine(ValueUpdateRoutine(qNo, side, value));
        }

        private IEnumerator ValueUpdateRoutine(int qNo, string side, int value)
        {
            string safeSide = Uri.EscapeDataString(side ?? string.Empty);
            string url = $"{ApiConfig.UpdateValueUrl}?idx_user={CurrentUserId}&q_no={qNo}&side={safeSide}&code=c2&value={value}";
            using (UnityWebRequest req = UnityWebRequest.Get(url))
            {
                req.timeout = 10;
                yield return req.SendWebRequest();
            }
        }

        public void SendPieceUpdateAPI(int value)
        {
            if (CurrentUserId == 0 || ApiConfig == null) return;
            StartCoroutine(PieceUpdateRoutine(value));
        }

        private IEnumerator PieceUpdateRoutine(int value)
        {
            string url = $"{ApiConfig.UpdatePieceUrl}?idx_user={CurrentUserId}&code=c2&value={value}";
            using (UnityWebRequest req = UnityWebRequest.Get(url))
            {
                req.timeout = 10;
                yield return req.SendWebRequest();
            }
        }

        #endregion
        
        #region 프로그램 강제 종료 시 예외 처리

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

            yield return ClearSourceFoldersAsync().ToCoroutine();
            
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

            if (CurrentUserId != 0 && ApiConfig != null)
            {   
                string resetUrl = $"{ApiConfig.ResetStartUrl}?idx_user={CurrentUserId}&code=c2";
                using (UnityWebRequest req = UnityWebRequest.Get(resetUrl))
                {   
                    req.timeout = 2;
                    UnityWebRequestAsyncOperation op = req.SendWebRequest();
                    float deadline = Time.realtimeSinceStartup + 2.0f;
                    while (!op.isDone && Time.realtimeSinceStartup < deadline)
                    {
                        System.Threading.Thread.Sleep(10);
                    }
                }

                string exitUrl = $"{ApiConfig.ExitRoomUrl}?code=c2&idx_user={CurrentUserId}";
                using (UnityWebRequest req = UnityWebRequest.Get(exitUrl))
                {   
                    req.timeout = 2;
                    UnityWebRequestAsyncOperation op = req.SendWebRequest();
                    float deadline = Time.realtimeSinceStartup + 2.0f;
                    while (!op.isDone && Time.realtimeSinceStartup < deadline)
                    {
                        System.Threading.Thread.Sleep(10);
                    }
                }
            }

            ClearSourceFoldersAsync().Forget();
        }
#endif

        private async UniTask ClearSourceFoldersAsync()
        {
            string dataPath = Application.dataPath;
            string dateFolder = DateTime.Now.ToString("yyyy-MM-dd");

            try
            {
                await UniTask.RunOnThreadPool(() =>
                {
                    DirectoryInfo parentDir = Directory.GetParent(dataPath);
                    string rootPath = parentDir != null ? parentDir.FullName : dataPath;

                    string timelapseSource = Path.Combine(rootPath, "Timelapse", "Timelapse_Source", dateFolder);
                    string realtimeSource = Path.Combine(rootPath, "Timelapse", "Realtime_Source", dateFolder);

                    if (Directory.Exists(timelapseSource))
                    {
                        foreach (string file in Directory.GetFiles(timelapseSource))
                        {
                            try { File.Delete(file); } catch { }
                        }
                    }

                    if (Directory.Exists(realtimeSource))
                    {
                        foreach (string file in Directory.GetFiles(realtimeSource))
                        {
                            try { File.Delete(file); } catch { }
                        }
                    }
                });
            }
            catch { }
        }
        #endregion
    }
}