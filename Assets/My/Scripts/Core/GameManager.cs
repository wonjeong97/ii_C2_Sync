using System;
using System.Collections;
using UnityEngine;
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
    }
}