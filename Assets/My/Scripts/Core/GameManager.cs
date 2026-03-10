using System;
using System.Collections;
using My.Scripts.Core.Data;
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
    public enum ColorData
    {   
        NotSet = -1,
        Cyan = 0,
        Pink = 1,
        Orange = 2,
        Green = 3,
        Red = 4,
        Yellow = 5
    }

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

    public enum UserType
    {
        A, B, C, D, E, F
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
        private float _fadeTime = 1.0f; 

        public int firstTaggedPlayer = 0;
        
        private bool _isInactivitySequenceRunning;
        private Coroutine _inactivityCoroutine;
        private Coroutine _popupFadeCoroutine;

        private CanvasGroup _systemPopupCg;
        private Text _systemPopupText;
        
        public bool IsAutoProgressing { get => isAutoProgressing; set => isAutoProgressing = value; }
        public InactivityTextType CurrentInactivityTextType { get; set; } = InactivityTextType.Warning;
        public UserType currentUserType = UserType.A;
        
        // [수정됨] APIManager에서 최신 설정을 주입할 수 있도록 private set 제한을 해제함
        public ApiSettings ApiConfig { get; set; } 
        
        // --- API 연동 데이터 캐싱 ---
        public int CurrentUserIdx { get; set; } = 0; 
        public string CurrentLanguage { get; set; } = "ko"; 
        
        // [추가됨] 타 카트리지 클리어 상태 확인용 변수
        public string Cartridge { get; set; } = "";
        public bool IsOtherCartridgeContentsCleared { get; set; } = false;
        
        public string PlayerAName { get; set; } = "NoNameA";
        public string PlayerBName { get; set; } = "NoNameB";
        
        public ColorData PlayerAColor { get; set; } = ColorData.NotSet;
        public ColorData PlayerBColor { get; set; } = ColorData.NotSet;

        public int PieceA1 { get; set; }
        public int PieceA2 { get; set; }
        public int PieceA3 { get; set; }
        public int PieceB1 { get; set; }
        public int PieceB2 { get; set; }
        public int PieceB3 { get; set; }
        public int PieceC1 { get; set; }
        public int PieceC2 { get; set; }
        public int PieceC3 { get; set; }
        public int PieceD1 { get; set; }
        public int PieceD2 { get; set; }
        public int PieceD3 { get; set; }
        
        public int TotalPieces => PieceA1 + PieceA2 + PieceA3 + 
                                  PieceB1 + PieceB2 + PieceB3 + 
                                  PieceC1 + PieceC2 + PieceC3 + 
                                  PieceD1 + PieceD2 + PieceD3; 
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
                if (!_systemPopupCg)
                {
                    _systemPopupCg = popupInstance.AddComponent<CanvasGroup>();
                }
                
                _systemPopupText = popupInstance.GetComponentInChildren<Text>();

                if (_systemPopupCg)
                {
                    _systemPopupCg.alpha = 0f;
                    _systemPopupCg.gameObject.SetActive(false);
                }
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
        
        public void NotifyUserDataUpdated()
        {
            OnUserDataUpdated?.Invoke();
        }

        public Sprite GetColorSprite(ColorData color)
        {
            int index = -1;
            switch (color)
            {
                case ColorData.Cyan:   index = 0; break;
                case ColorData.Pink:   index = 1; break;
                case ColorData.Orange: index = 2; break;
                case ColorData.Green:  index = 3; break;
                case ColorData.Red:    index = 4; break;
                case ColorData.Yellow: index = 5; break;
            }

            if (index >= 0 && playerColorSprites != null && index < playerColorSprites.Length)
            {
                return playerColorSprites[index];
            }
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

        private void HandleInactivity()
        {
            if (SceneManager.GetActiveScene().name == GameConstants.Scene.Title || isAutoProgressing)
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
            if (_systemData != null)
            {
                targetText = CurrentInactivityTextType == InactivityTextType.Tag ? _systemData.tagText : _systemData.inactivityWarningText;
            }

            if (targetText != null && _systemPopupText)
            {
                if (UIManager.Instance) UIManager.Instance.SetText(_systemPopupText.gameObject, targetText);
                else _systemPopupText.text = targetText.text;
            }
            else if (_systemPopupText) 
            {
                _systemPopupText.text = "움직여주세요"; 
            }

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
            else if (_systemPopupText) 
            {
                _systemPopupText.text = "동작이 인식 되지 않아 초기화 됩니다"; 
            }

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

            firstTaggedPlayer = 0; 
            isAutoProgressing = false;
            CurrentInactivityTextType = InactivityTextType.Warning; 
            ResetInactivityTimer();
            
            CurrentUserIdx = 0;
            Cartridge = "";
            IsOtherCartridgeContentsCleared = false;

            ChangeScene(GameConstants.Scene.Title);
        }
        
        #region API 호출 로직 (시간 및 값 기록)

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
            if (CurrentUserIdx == 0 || ApiConfig == null) return;
            StartCoroutine(ResetStartRoutine());
        }

        private IEnumerator ResetStartRoutine()
        {
            string url = $"{ApiConfig.ResetStartUrl}?idx_user={CurrentUserIdx}&code=c2";
            using (UnityWebRequest req = UnityWebRequest.Get(url))
            {
                req.timeout = 5;
                yield return req.SendWebRequest();
                if (req.result != UnityWebRequest.Result.Success)
                    Debug.LogWarning($"[API] ResetStart 실패: {req.error}");
            }
        }

        public void SendExitRoomAPI()
        {
            if (CurrentUserIdx == 0 || ApiConfig == null) return;
            StartCoroutine(ExitRoomRoutine());
        }

        private IEnumerator ExitRoomRoutine()
        {
            string url = $"{ApiConfig.ExitRoomUrl}?code=c2&idx_user={CurrentUserIdx}";
            using (UnityWebRequest req = UnityWebRequest.Get(url))
            {
                req.timeout = 5;
                yield return req.SendWebRequest();
                if (req.result != UnityWebRequest.Result.Success)
                    Debug.LogWarning($"[API] ExitRoom 실패: {req.error}");
            }
        }

        public void SendTimeUpdateAPI()
        {
            if (CurrentUserIdx == 0 || ApiConfig == null)
            {
                Debug.LogWarning($"[GameManager] CurrentUserId가 0이거나 ApiConfig가 없습니다. end API 호출을 건너뜁니다.");
                return;
            }
            StartCoroutine(TimeUpdateRoutine());
        }

        private IEnumerator TimeUpdateRoutine()
        {
            string url = $"{ApiConfig.UpdateTimeUrl}?idx_user={CurrentUserIdx}&option=end&code=c2";

            using (UnityWebRequest req = UnityWebRequest.Get(url))
            {
                req.timeout = 10;
                yield return req.SendWebRequest();
                
                if (req.result != UnityWebRequest.Result.Success) 
                    Debug.LogError($"[Time API] 에러: {req.error}");
                else 
                    Debug.Log($"[Time API] end 업데이트 성공! (URL: {url})");
            }
        }

        public void SendValueUpdateAPI(int qNo, string side, int value)
        {
            if (CurrentUserIdx == 0 || ApiConfig == null)
            {
                Debug.LogWarning("[GameManager] CurrentUserId가 0이거나 ApiConfig가 없습니다. Value 업데이트를 건너뜁니다.");
                return;
            }
            StartCoroutine(ValueUpdateRoutine(qNo, side, value));
        }

        private IEnumerator ValueUpdateRoutine(int qNo, string side, int value)
        {
            string safeSide = Uri.EscapeDataString(side ?? string.Empty);
            string url = $"{ApiConfig.UpdateValueUrl}?idx_user={CurrentUserIdx}&q_no={qNo}&side={safeSide}&code=c2&value={value}";
            
            using (UnityWebRequest req = UnityWebRequest.Get(url))
            {
                req.timeout = 10;
                yield return req.SendWebRequest();
                if (req.result != UnityWebRequest.Result.Success) Debug.LogError($"[Value API] 통신 에러: {req.error}");
                else Debug.Log($"[Value API] {side} Q{qNo} 값({value}) 업데이트 성공!");
            }
        }

        public void SendPieceUpdateAPI(int value)
        {
            if (CurrentUserIdx == 0 || ApiConfig == null)
            {
                Debug.LogWarning("[GameManager] CurrentUserId가 0이거나 ApiConfig가 없습니다. Piece 업데이트를 건너뜁니다.");
                return;
            }
            StartCoroutine(PieceUpdateRoutine(value));
        }

        private IEnumerator PieceUpdateRoutine(int value)
        {
            string url = $"{ApiConfig.UpdatePieceUrl}?idx_user={CurrentUserIdx}&code=c2&value={value}";
            
            using (UnityWebRequest req = UnityWebRequest.Get(url))
            {
                req.timeout = 10;
                yield return req.SendWebRequest();
                if (req.result != UnityWebRequest.Result.Success) 
                    Debug.LogError($"[Piece API] 에러: {req.error}");
                else 
                    Debug.Log($"[Piece API] 마음 조각({value}개) 업데이트 성공! (URL: {url})");
            }
        }

        #endregion
    }
}