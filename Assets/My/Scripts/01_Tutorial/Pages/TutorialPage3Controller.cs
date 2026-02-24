using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using My.Scripts.Core;
using Wonjeong.Data;
using Wonjeong.UI;
using Wonjeong.Utils; 

namespace My.Scripts._01_Tutorial.Pages
{
    /// <summary>
    /// 튜토리얼 3페이지의 데이터 구조.
    /// JSON의 "page3" 섹션과 매핑되며 설명 텍스트 정보를 포함함.
    /// </summary>
    [Serializable]
    public class TutorialPage3Data
    {
        public TextSetting descriptionText;
    }

    /// <summary>
    /// 튜토리얼의 세 번째 페이지인 '점프 동작'을 관리하는 컨트롤러.
    /// 두 개의 발판을 동시에 밟는(점프 후 착지) 동작을 감지하고 성공 여부를 판단함.
    /// 입력 없이 방치될 경우 게임 매니저의 글로벌 방치 팝업을 강제 호출함.
    /// </summary>
    public class TutorialPage3Controller : GamePage<TutorialPage3Data>
    {
        [Header("UI Components")]
        [SerializeField] private Text descriptionText;
        [SerializeField] private Image checkImageA;
        [SerializeField] private Image checkImageB;

        [Header("Settings")]
        [SerializeField] private float jumpLandingTolerance = 0.25f; 

        private bool _isAFinished;
        private bool _isBFinished;
        private bool _isStepCompleted; 
        private float _lastInputTime; 

        // === 상태 관리 변수 ===
        private bool _pAPad0, _pAPad1;
        private bool _pBPad0, _pBPad1;

        // 점프 시퀀스 플래그
        private bool _pAIsReady;
        private bool _pAHasJumped;
        private float _pAFirstFootTime; 

        private bool _pBIsReady;
        private bool _pBHasJumped;
        private float _pBFirstFootTime; 

        /// <summary>
        /// 외부 데이터를 UI에 반영함.
        /// 데이터 기반으로 UI를 갱신하여 유지보수성을 높이기 위함.
        /// </summary>
        /// <param name="data">설명 텍스트 정보가 담긴 데이터 객체</param>
        protected override void SetupData(TutorialPage3Data data)
        {
            if (data == null)
            {
                Debug.LogWarning("[TutorialPage3Controller] 전달된 데이터가 없습니다.");
                return;
            }
            
            if (descriptionText && data.descriptionText != null)
            {
                descriptionText.supportRichText = true;
                if (UIManager.Instance)
                {
                    UIManager.Instance.SetText(descriptionText.gameObject, data.descriptionText);
                }
                else
                {
                    Debug.LogWarning("[TutorialPage3Controller] UIManager.Instance를 찾을 수 없습니다.");
                }
            }
        }

        /// <summary>
        /// 페이지 진입 시 초기화 및 하드웨어 입력 이벤트 구독을 수행함.
        /// </summary>
        public override void OnEnter()
        {
            base.OnEnter();
            
            // 재진입 시 이전 상태가 남아 로직이 꼬이는 것을 방지하기 위해 완전 초기화함
            _isAFinished = false;
            _isBFinished = false;
            _isStepCompleted = false; 
            _lastInputTime = Time.time;

            _pAIsReady = _pAHasJumped = false;
            _pBIsReady = _pBHasJumped = false;
            _pAFirstFootTime = _pBFirstFootTime = 0f;

            InitCheckImage(checkImageA);
            InitCheckImage(checkImageB);

            // 사용자가 이미 키를 누르고 있는 상태에서 진입했을 때의 예외 처리를 위해 상태를 동기화함
            SyncInitialInputState();

            // 입력 매니저를 통해 하드웨어 입력 이벤트를 구독하여 물리적 발판 입력을 받음
            if (InputManager.Instance)
            {
                InputManager.Instance.OnPadDown += HandlePadDown;
                InputManager.Instance.OnPadUp += HandlePadUp;
            }
            else
            {
                Debug.LogWarning("[TutorialPage3Controller] InputManager.Instance를 찾을 수 없습니다.");
            }
        }

        /// <summary>
        /// 페이지 퇴장 시 이벤트 구독 해제.
        /// 메모리 누수 및 다른 페이지에서의 오동작을 방지하기 위함.
        /// </summary>
        public override void OnExit()
        {
            base.OnExit();
            
            if (InputManager.Instance)
            {
                InputManager.Instance.OnPadDown -= HandlePadDown;
                InputManager.Instance.OnPadUp -= HandlePadUp;
            }
        }

        private void Update()
        {
            if (_isStepCompleted) return;

            // # TODO: 방치 기준 시간(20f)을 하드코딩하지 않고 외부 설정값으로 분리할 것
            // 발판을 밟은 채로 가만히 있는 경우 GameManager의 글로벌 타이머가 차단될 수 있으므로, 페이지 내에서 직접 시간을 측정하여 방치 시퀀스를 강제 호출함
            if (Time.time - _lastInputTime >= 20f)
            {
                if (GameManager.Instance)
                {
                    GameManager.Instance.ForceInactivitySequence();
                }
                else
                {
                    Debug.LogWarning("[TutorialPage3Controller] GameManager.Instance를 찾을 수 없어 방치 팝업을 띄울 수 없습니다.");
                }
                _lastInputTime = float.MaxValue; 
            }
        }

        /// <summary>
        /// 페이지 진입 시점에 이미 눌려있는 키 상태를 확인하여 로직에 반영함.
        /// </summary>
        private void SyncInitialInputState()
        {
            _pAPad0 = Input.GetKey(KeyCode.Alpha3);
            _pAPad1 = Input.GetKey(KeyCode.Alpha4);
            
            if (_pAPad0 && _pAPad1) _pAIsReady = true;

            _pBPad0 = Input.GetKey(KeyCode.Alpha9);
            _pBPad1 = Input.GetKey(KeyCode.Alpha0);

            if (_pBPad0 && _pBPad1) _pBIsReady = true;
        }

        /// <summary>
        /// 완료 체크 이미지를 초기화(투명화 및 비활성화)함.
        /// </summary>
        private void InitCheckImage(Image img)
        {
            if (img)
            {
                Color c = img.color;
                c.a = 0f;
                img.color = c;
                img.gameObject.SetActive(false);
            }
        }

        private void HandlePadDown(int playerIdx, int laneIdx, int padIdx)
            => UpdateLogic(playerIdx, laneIdx, padIdx, true);

        private void HandlePadUp(int playerIdx, int laneIdx, int padIdx)
            => UpdateLogic(playerIdx, laneIdx, padIdx, false);

        /// <summary>
        /// 입력 상태가 변경될 때마다 호출되어 점프 로직 및 방치 타이머를 갱신함.
        /// </summary>
        private void UpdateLogic(int playerIdx, int laneIdx, int padIdx, bool isDown)
        {
            _lastInputTime = Time.time;

            // 점프 튜토리얼은 중앙 발판에서만 진행되므로 다른 라인 입력은 무시함
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
            
            // 두 플레이어 모두 성공했고 아직 완료 처리되지 않았다면 1초 대기 후 다음 튜토리얼로 넘어감
            if (_isAFinished && _isBFinished && !_isStepCompleted)
            {
                _isStepCompleted = true;
                StartCoroutine(WaitAndCompleteRoutine());
            }
        }

        /// <summary>
        /// 점프 동작의 유효성을 검증하는 핵심 로직.
        /// 발판 위 대기 -> 공중부양(두 발 뗌) -> 동시 착지 순서를 확인함.
        /// </summary>
        private void CheckSequence(int pIdx, bool p0, bool p1, ref bool isReady, ref bool hasJumped, ref float firstFootTime, ref bool isFinished, Image checkImg)
        {
            if (isFinished) return;

            int padCount = (p0 ? 1 : 0) + (p1 ? 1 : 0);
            
            // [상태 1: 공중] 준비된 상태에서 두 발을 모두 뗐다면 점프 중인 것으로 간주함
            if (padCount == 0)
            {
                if (isReady) hasJumped = true; 
                firstFootTime = 0f; 
            }
            // [상태 2: 한 발 착지] 공중에서 내려와서 첫 발이 닿는 순간의 시간을 기록함 (동시 착지 판정의 기준점)
            else if (padCount == 1)
            {
                if (firstFootTime <= 0f) firstFootTime = Time.time;
            }
            // [상태 3: 두 발 착지] 점프 상태를 거쳤는지, 첫 발 착지 후 허용 오차 내에 두 번째 발이 착지했는지 확인함
            else if (padCount == 2)
            {
                bool isSimultaneous = (Time.time - firstFootTime) <= jumpLandingTolerance;

                if (hasJumped && isSimultaneous)
                {   
                    if (SoundManager.Instance) SoundManager.Instance.PlaySFX("공통_5");
                    
                    isFinished = true;
                    StartCoroutine(FadeInRoutine(checkImg, 1f));
                }
                else
                {
                    // 실패 시 상태를 리셋하여 다시 점프를 시도하게 유도함
                    isReady = true;
                    hasJumped = false;
                    firstFootTime = 0f;
                }
            }
        }

        /// <summary>
        /// 성공 체크 이미지를 부드럽게 나타나게 하는 코루틴.
        /// </summary>
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
        /// 마지막 성공 연출을 보기 위해 1초 대기 후 완료 처리함.
        /// </summary>
        private IEnumerator WaitAndCompleteRoutine()
        {
            yield return CoroutineData.GetWaitForSeconds(1.0f);
            CompleteStep();
        }
    }
}