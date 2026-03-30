using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using My.Scripts.Core;
using Wonjeong.Data;
using Wonjeong.UI;

namespace My.Scripts._01_Tutorial.Pages
{
    [Serializable]
    public class TutorialPage3Data
    {
        public TextSetting playerAName;
        public TextSetting playerBName;
        public TextSetting descriptionText;
    }

    public class TutorialPage3Controller : GamePage<TutorialPage3Data>
    {
        [Header("UI Components")]
        [SerializeField] private Text descriptionText;
        [SerializeField] private Image checkImageA;
        [SerializeField] private Image checkImageB;
        [SerializeField] private Text playerAText; 
        [SerializeField] private Text playerBText; 
        [SerializeField] private Image ballImageA; 
        [SerializeField] private Image ballImageB; 

        [Header("Settings")]
        [SerializeField] private float jumpLandingTolerance = 0.25f; 

        private TutorialPage3Data _data; 

        private bool _isAFinished;
        private bool _isBFinished;
        private bool _isStepCompleted; 

        private bool _pAPad0, _pAPad1;
        private bool _pBPad0, _pBPad1;

        private bool _pAIsReady;
        private bool _pAHasJumped;
        private float _pAFirstFootTime; 

        private bool _pBIsReady;
        private bool _pBHasJumped;
        private float _pBFirstFootTime; 

        /// <summary>
        /// 외부 데이터를 받아 UI 텍스트 컴포넌트를 세팅함.
        /// </summary>
        /// <param name="data">적용할 텍스트 설정 데이터</param>
        protected override void SetupData(TutorialPage3Data data)
        {
            if (data == null)
            {
                Debug.LogWarning("TutorialPage3Data 데이터 누락됨.");
                return;
            }

            _data = data; 

            if (!playerAText)
            {
                Debug.LogWarning("playerAText 컴포넌트 누락됨.");
            }
            else if (data.playerAName == null)
            {
                Debug.LogWarning("playerAName 데이터 누락됨.");
            }
            else if (UIManager.Instance)
            {
                UIManager.Instance.SetText(playerAText.gameObject, data.playerAName);
            }

            if (!playerBText)
            {
                Debug.LogWarning("playerBText 컴포넌트 누락됨.");
            }
            else if (data.playerBName == null)
            {
                Debug.LogWarning("playerBName 데이터 누락됨.");
            }
            else if (UIManager.Instance)
            {
                UIManager.Instance.SetText(playerBText.gameObject, data.playerBName);
            }

            if (!descriptionText)
            {
                Debug.LogWarning("descriptionText 컴포넌트 누락됨.");
            }
            else if (data.descriptionText == null)
            {
                Debug.LogWarning("descriptionText 데이터 누락됨.");
            }
            else
            {
                descriptionText.supportRichText = true;
                if (UIManager.Instance) UIManager.Instance.SetText(descriptionText.gameObject, data.descriptionText);
            }
        }

        /// <summary>
        /// 페이지 진입 시 호출됨.
        /// 사용자 점프 입력을 대기하기 위해 상태를 초기화함.
        /// </summary>
        public override void OnEnter()
        {
            base.OnEnter();
            
            // 이유: 사용자가 직접 점프 입력을 해야 하는 구간이므로 글로벌 방치 타이머를 다시 가동시킴.
            if (GameManager.Instance) GameManager.Instance.IsAutoProgressing = false;

            _isAFinished = false;
            _isBFinished = false;
            _isStepCompleted = false; 

            _pAIsReady = false;
            _pAHasJumped = false;
            _pBIsReady = false;
            _pBHasJumped = false;
            _pAFirstFootTime = 0f;
            _pBFirstFootTime = 0f;

            InitCheckImage(checkImageA);
            InitCheckImage(checkImageB);

            ApplyDynamicNames();
            ApplyPlayerColors();
            SyncInitialInputState();

            if (InputManager.Instance)
            {
                InputManager.Instance.OnPadDown += HandlePadDown;
                InputManager.Instance.OnPadUp += HandlePadUp;
            }
        }

        /// <summary>
        /// GameManager의 사용자 이름을 UI에 동적으로 치환함.
        /// </summary>
        private void ApplyDynamicNames()
        {
            if (_data == null) return;

            if (!GameManager.Instance)
            {
                Debug.LogWarning("GameManager 인스턴스 누락됨.");
                return;
            }

            string nameA = GameManager.Instance.PlayerAName;
            string nameB = GameManager.Instance.PlayerBName;

            // 이유: 데이터가 없는 경우 Fallback 대신 로그를 남겨 디버깅을 용이하게 함.
            if (string.IsNullOrEmpty(nameA) || nameA == "NoNameA") Debug.LogWarning("Player A 이름 데이터 누락됨.");
            if (string.IsNullOrEmpty(nameB) || nameB == "NoNameB") Debug.LogWarning("Player B 이름 데이터 누락됨.");

            if (playerAText && _data.playerAName != null)
            {
                playerAText.text = _data.playerAName.text.Replace("{nameA}", nameA);
            }

            if (playerBText && _data.playerBName != null)
            {
                playerBText.text = _data.playerBName.text.Replace("{nameB}", nameB);
            }
        }

        /// <summary>
        /// 페이지 퇴장 시 호출됨.
        /// 이벤트 구독을 해제하고 타이머를 정지함.
        /// </summary>
        public override void OnExit()
        {
            base.OnExit();
            
            // 이유: 페이지 퇴장 시 다시 타이머를 잠가 다음 연출 중 예기치 않게 팝업이 뜨는 것을 방지함.
            if (GameManager.Instance) GameManager.Instance.IsAutoProgressing = true;
            
            if (InputManager.Instance)
            {
                InputManager.Instance.OnPadDown -= HandlePadDown;
                InputManager.Instance.OnPadUp -= HandlePadUp;
            }
        }
        
        /// <summary>
        /// 객체 파괴 시 호출됨.
        /// 메모리 누수를 막기 위해 이벤트 구독을 해제함.
        /// </summary>
        private void OnDestroy()
        {
            if (InputManager.Instance)
            {
                InputManager.Instance.OnPadDown -= HandlePadDown;
                InputManager.Instance.OnPadUp -= HandlePadUp;
            }
        }

        /// <summary>
        /// GameManager의 사용자 색상 정보를 UI 이미지에 적용함.
        /// </summary>
        private void ApplyPlayerColors()
        {
            if (!GameManager.Instance) return;

            if (ballImageA)
            {
                Sprite spriteA = GameManager.Instance.GetColorSprite(GameManager.Instance.PlayerAColor);
                if (!spriteA) Debug.LogWarning("Player A 컬러 스프라이트 누락됨.");
                else ballImageA.sprite = spriteA;
            }

            if (ballImageB)
            {
                Sprite spriteB = GameManager.Instance.GetColorSprite(GameManager.Instance.PlayerBColor);
                if (!spriteB) Debug.LogWarning("Player B 컬러 스프라이트 누락됨.");
                else ballImageB.sprite = spriteB;
            }
        }

        /// <summary>
        /// 키보드 디버깅용 초기 입력 상태를 동기화함.
        /// </summary>
        private void SyncInitialInputState()
        {
            // # TODO: Input.GetKey 호출 비용 최소화를 위해 하드웨어 입력 체계와 통합 고려.
            _pAPad0 = Input.GetKey(KeyCode.Alpha3);
            _pAPad1 = Input.GetKey(KeyCode.Alpha4);
            
            if (_pAPad0 && _pAPad1) _pAIsReady = true;

            _pBPad0 = Input.GetKey(KeyCode.Alpha9);
            _pBPad1 = Input.GetKey(KeyCode.Alpha0);

            if (_pBPad0 && _pBPad1) _pBIsReady = true;
        }

        /// <summary>
        /// 완료 체크 이미지의 초기 상태를 투명 및 비활성화로 설정함.
        /// </summary>
        /// <param name="img">설정할 대상 이미지</param>
        private void InitCheckImage(Image img)
        {
            if (!img)
            {
                Debug.LogWarning("InitCheckImage 대상 이미지 누락됨.");
                return;
            }
            
            Color c = img.color;
            c.a = 0f;
            img.color = c;
            img.gameObject.SetActive(false);
        }

        /// <summary>
        /// 발판 눌림 이벤트 핸들러.
        /// </summary>
        private void HandlePadDown(int playerIdx, int laneIdx, int padIdx) => UpdateLogic(playerIdx, laneIdx, padIdx, true);
        
        /// <summary>
        /// 발판 떨어짐 이벤트 핸들러.
        /// </summary>
        private void HandlePadUp(int playerIdx, int laneIdx, int padIdx) => UpdateLogic(playerIdx, laneIdx, padIdx, false);

        /// <summary>
        /// 발판 입력에 따른 점프 로직 상태를 업데이트함.
        /// </summary>
        private void UpdateLogic(int playerIdx, int laneIdx, int padIdx, bool isDown)
        {
            // 이유: 튜토리얼 3페이지는 중앙(1번) 레인의 점프만 처리함.
            if (laneIdx != 1) return; 

            if (playerIdx == 0)
            {
                if (padIdx == 0) _pAPad0 = isDown;
                else if (padIdx == 1) _pAPad1 = isDown;

                CheckSequence(0, _pAPad0, _pAPad1, ref _pAIsReady, ref _pAHasJumped, ref _pAFirstFootTime, ref _isAFinished, checkImageA);
            }
            else if (playerIdx == 1)
            {
                if (padIdx == 0) _pBPad0 = isDown;
                else if (padIdx == 1) _pBPad1 = isDown;

                CheckSequence(1, _pBPad0, _pBPad1, ref _pBIsReady, ref _pBHasJumped, ref _pBFirstFootTime, ref _isBFinished, checkImageB);
            }
            
            if (_isAFinished && _isBFinished && !_isStepCompleted)
            {
                _isStepCompleted = true;
                StartCoroutine(WaitAndCompleteRoutine());
            }
        }

        /// <summary>
        /// 양발 동시 체공 및 착지 여부를 판정하여 점프 성공을 결정함.
        /// </summary>
        private void CheckSequence(int pIdx, bool p0, bool p1, ref bool isReady, ref bool hasJumped, ref float firstFootTime, ref bool isFinished, Image checkImg)
        {
            if (isFinished) return;

            int padCount = (p0 ? 1 : 0) + (p1 ? 1 : 0);
            
            if (padCount == 0)
            {
                // 발이 모두 떨어졌을 때 점프한 것으로 판정함.
                if (isReady) hasJumped = true; 
                firstFootTime = 0f; 
            }
            else if (padCount == 1)
            {
                // 한쪽 발이 먼저 닿은 시간을 기록하여 동시 착지 여부 판단에 사용함.
                if (firstFootTime <= 0f) firstFootTime = Time.time;
            }
            else if (padCount == 2)
            {
                // 예: Time.time(10.5) - firstFootTime(10.3) = 0.2 <= 0.25 (성공)
                bool isSimultaneous = (Time.time - firstFootTime) <= jumpLandingTolerance;

                if (hasJumped && isSimultaneous)
                {   
                    if (SoundManager.Instance) SoundManager.Instance.PlaySFX("공통_5");
                    
                    isFinished = true;
                    StartCoroutine(FadeInRoutine(checkImg, 1f));
                }
                else
                {
                    // 점프 판정 실패 시 다음 시도를 위해 상태를 초기화함.
                    isReady = true;
                    hasJumped = false;
                    firstFootTime = 0f;
                }
            }
        }

        /// <summary>
        /// 대상 이미지의 알파값을 서서히 증가시켜 나타나게 함.
        /// </summary>
        /// <param name="targetImg">페이드인 할 이미지</param>
        /// <param name="duration">페이드인 진행 시간</param>
        /// <returns>IEnumerator 루틴</returns>
        private IEnumerator FadeInRoutine(Image targetImg, float duration)
        {
            if (!targetImg) yield break;
            
            targetImg.gameObject.SetActive(true);
            Color initialColor = targetImg.color;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float alpha = Mathf.Clamp01(elapsed / duration);
                targetImg.color = new Color(initialColor.r, initialColor.g, initialColor.b, alpha);
                yield return null;
            }
            targetImg.color = new Color(initialColor.r, initialColor.g, initialColor.b, 1f);
        }

        /// <summary>
        /// 두 플레이어 모두 점프 성공 후 지정된 시간 대기한 뒤 완료 처리함.
        /// </summary>
        /// <returns>IEnumerator 루틴</returns>
        private IEnumerator WaitAndCompleteRoutine()
        {
            yield return new WaitForSeconds(1.0f);
            CompleteStep();
        }
    }
}