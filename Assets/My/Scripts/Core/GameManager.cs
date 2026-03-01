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
    /// <summary>
    /// 방치 팝업에 띄울 텍스트의 종류를 구분하기 위한 열거형.
    /// </summary>
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
        A,  // 커플 표준
        B,  // 친구
        C,  // 동료
        D,  // 부모-성인 자녀
        E,  // 부모-사춘기 자녀
        F   // 부부사이
    }

    /// <summary> 게임 전반적인 상태, 씬 전환 및 글로벌 방치 타이머를 관리하는 매니저 </summary>
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance; 

        [SerializeField] private Reporter reporter; 

        [Header("System Popup (Inactivity)")]
        [Tooltip("방치 시 띄울 시스템 팝업 프리팹 (Canvas - 팝업 이미지 - 팝업 텍스트 구조)")]
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
        public ApiSettings ApiConfig { get; private set; }
        public int CurrentUserId { get; set; } = 0; 
        public string PlayerALastName { get; set; } = "NoNameA";
        public string PlayerBLastName { get; set; } = "NoNameB";
        public ColorData PlayerAColor { get; set; } = ColorData.NotSet;
        public ColorData PlayerBColor { get; set; } = ColorData.NotSet;
        
        [Header("Player Color Sprites")]
        [Tooltip("인덱스 순서대로 등록하세요. 0:Cyan, 1:Pink, 2:Orange, 3:Green, 4:Red, 5:Yellow")]
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
                    Debug.LogWarning("[GameManager] 팝업 프리팹 최상위에 CanvasGroup이 없어 자동 추가했습니다.");
                }
                
                _systemPopupText = popupInstance.GetComponentInChildren<Text>();
                if (!_systemPopupText)
                {
                    Debug.LogWarning("[GameManager] 팝업 프리팹 하위에서 Text 컴포넌트를 찾을 수 없습니다.");
                }

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
        
        /// <summary> ColorData에 해당하는 스프라이트 반환 </summary>
        public Sprite GetColorSprite(ColorData color)
        {
            int index = (int)color;
            if (index >= 0 && playerColorSprites != null && index < playerColorSprites.Length)
            {
                return playerColorSprites[index];
            }
            return null;
        }
        
        /// <summary>
        /// API의 ColorData Enum을 지정된 실제 RGB(Color32) 데이터로 매핑하여 반환함.
        /// </summary>
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
                default:               return Color.white; // 색상이 지정되지 않은 경우의 폴백
            }
        }

        private void HandleInactivity()
        {
            if (SceneManager.GetActiveScene().name == GameConstants.Scene.Title || isAutoProgressing)
            {
                ResetInactivityTimer();
                return;
            }

            // 발판을 밟고만 있는 상태(Input.anyKey)가 타이머를 무한정 리셋하는 것을 막고, 
            // 실제 새로운 물리적 동작이나 마우스 클릭(anyKeyDown)이 일어날 때만 초기화하도록 개선함.
            if (Input.anyKeyDown || Input.GetMouseButtonDown(0) || (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began))
            {
                ResetInactivityTimer();
            }
            else
            {
                if (!_isInactivitySequenceRunning)
                {
                    _currentInactivityTimer += Time.deltaTime;
                    
                    // # TODO: 하드코딩된 방치 기준 시간(20f)을 외부 환경 변수로 분리하여 관리할 것
                    if (_currentInactivityTimer >= 20f)
                    {
                        _inactivityCoroutine = StartCoroutine(InactivitySequenceRoutine());
                    }
                }
            }
        }

        /// <summary>
        /// 방치 타이머를 초기화하고, 재생 중이던 팝업, 코루틴, 사운드 등 관련된 연출을 모두 즉시 중지시킴.
        /// </summary>
        public void ResetInactivityTimer()
        {
            _currentInactivityTimer = 0f;
            
            if (_isInactivitySequenceRunning)
            {
                _isInactivitySequenceRunning = false;
                
                if (_inactivityCoroutine != null) 
                    StopCoroutine(_inactivityCoroutine);
                
                if (_systemPopupCg)
                {
                    if (_popupFadeCoroutine != null) 
                        StopCoroutine(_popupFadeCoroutine);
                        
                    _systemPopupCg.alpha = 0f;
                    _systemPopupCg.gameObject.SetActive(false);
                }

                if (SoundManager.Instance)
                {
                    SoundManager.Instance.StopSFX();
                }
            }
        }

        /// <summary>
        /// 특정 페이지(예: 점프 튜토리얼)에서 로직상 방치 상태로 판단되었을 때 팝업 시퀀스를 강제 실행함.
        /// </summary>
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
                if (CurrentInactivityTextType == InactivityTextType.Tag)
                {
                    targetText = _systemData.tagText;
                }
                else
                {
                    targetText = _systemData.inactivityWarningText;
                }
            }

            if (targetText != null && _systemPopupText)
            {
                if (UIManager.Instance)
                {
                    UIManager.Instance.SetText(_systemPopupText.gameObject, targetText);
                }
                else
                {
                    _systemPopupText.text = targetText.text;
                    Debug.LogWarning("[GameManager] UIManager.Instance가 존재하지 않아 기본 text에만 할당합니다.");
                }
            }
            else if (_systemPopupText) 
            {
                _systemPopupText.text = "움직여주세요"; 
                Debug.LogWarning("[GameManager] 출력할 텍스트 데이터를 찾을 수 없어 폴백 텍스트를 사용합니다.");
            }

            _popupFadeCoroutine = StartCoroutine(FadeSystemPopup(0f, 1f, 0.5f));
            yield return _popupFadeCoroutine;

            yield return new WaitForSeconds(3.0f);
            _popupFadeCoroutine = StartCoroutine(FadeSystemPopup(1f, 0f, 0.5f));
            yield return _popupFadeCoroutine;

            if (SoundManager.Instance)
            {
                SoundManager.Instance.PlaySFX("공통_15_10초");
            }

            yield return new WaitForSeconds(10.0f);

            if (_systemData != null && _systemData.inactivityResetText != null && _systemPopupText)
            {
                if (UIManager.Instance)
                {
                    UIManager.Instance.SetText(_systemPopupText.gameObject, _systemData.inactivityResetText);
                }
                else
                {
                    _systemPopupText.text = _systemData.inactivityResetText.text;
                }
            }
            else if (_systemPopupText) 
            {
                _systemPopupText.text = "동작이 인식 되지 않아 초기화 됩니다"; 
            }

            _popupFadeCoroutine = StartCoroutine(FadeSystemPopup(0f, 1f, 0.5f));
            yield return _popupFadeCoroutine;

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
            Debug.Log($"[GameManager] Scene Transition Requested: {sceneName}");
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
            
            Debug.Log("[GameManager] Returning to Title...");

            firstTaggedPlayer = 0; 
            isAutoProgressing = false;
            CurrentInactivityTextType = InactivityTextType.Warning; 
            ResetInactivityTimer();

            ChangeScene(GameConstants.Scene.Title);
        }
        
         #region API 호출 로직 (시간 및 값 기록)

        /// <summary> 콘텐츠 시작/종료 시간을 서버에 기록합니다. </summary>
        public void SendTimeUpdateAPI(string option)
        {
            if (CurrentUserId == 0)
            {
                Debug.LogWarning($"[GameManager] CurrentUserId가 0입니다. {option} API 호출을 건너뜁니다.");
                return;
            }
            StartCoroutine(TimeUpdateRoutine(option));
        }

        private IEnumerator TimeUpdateRoutine(string option)
        {
            if (ApiConfig == null) yield break;

            // ApiConfig.UpdateTimeUrl 사용
            string urlLeft = $"{ApiConfig.UpdateTimeUrl}?idx_user={CurrentUserId}&option={option}&side=left&code=a1";
            string urlRight = $"{ApiConfig.UpdateTimeUrl}?idx_user={CurrentUserId}&option={option}&side=right&code=a1";

            // Left 통신
            using (UnityWebRequest reqLeft = UnityWebRequest.Get(urlLeft))
            {
                yield return reqLeft.SendWebRequest();
                if (reqLeft.result != UnityWebRequest.Result.Success) Debug.LogError($"[Time API Left] 에러: {reqLeft.error}");
                else Debug.Log($"[Time API Left] {option} 업데이트 성공!");
            }

            // Right 통신
            using (UnityWebRequest reqRight = UnityWebRequest.Get(urlRight))
            {
                yield return reqRight.SendWebRequest();
                if (reqRight.result != UnityWebRequest.Result.Success) Debug.LogError($"[Time API Right] 에러: {reqRight.error}");
                else Debug.Log($"[Time API Right] {option} 업데이트 성공!");
            }
        }

        /// <summary> 사용자의 질문 응답 값을 서버에 업데이트합니다. </summary>
        public void SendValueUpdateAPI(int qNo, string side, int value)
        {
            if (CurrentUserId == 0)
            {
                Debug.LogWarning("[GameManager] CurrentUserId가 0입니다. Value 업데이트를 건너뜁니다.");
                return;
            }
            StartCoroutine(ValueUpdateRoutine(qNo, side, value));
        }

        private IEnumerator ValueUpdateRoutine(int qNo, string side, int value)
        {
            if (ApiConfig == null) yield break; // 안전장치

            // ApiConfig.UpdateValueUrl 사용
            string url = $"{ApiConfig.UpdateValueUrl}?idx_user={CurrentUserId}&q_no={qNo}&side={side}&code=a1&value={value}";
            
            using (UnityWebRequest req = UnityWebRequest.Get(url))
            {
                yield return req.SendWebRequest();
                if (req.result != UnityWebRequest.Result.Success) Debug.LogError($"[Value API] 통신 에러: {req.error}");
                else Debug.Log($"[Value API] {side} Q{qNo} 값({value}) 업데이트 성공!");
            }
        }

        #endregion
    }
}