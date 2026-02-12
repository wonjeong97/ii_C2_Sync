using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using My.Scripts.Core;
using My.Scripts.Global;
using Wonjeong.Data;
using Wonjeong.UI;
using Wonjeong.Utils; // CoroutineData 사용을 위해 필요

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
    /// </summary>
    public class TutorialPage3Controller : GamePage<TutorialPage3Data>
    {
        [Header("UI Components")]
        [SerializeField] private Text descriptionText;
        [SerializeField] private Image checkImageA;
        [SerializeField] private Image checkImageB;

        [Header("Settings")]
        [SerializeField] private float jumpLandingTolerance = 0.25f; // 두 발이 닿는 시간차 허용 범위

        private bool _isAFinished;
        private bool _isBFinished;
        private bool _isStepCompleted; // ★ 중복 완료 방지용 플래그

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
        /// </summary>
        /// <param name="data">설명 텍스트 정보가 담긴 데이터 객체</param>
        protected override void SetupData(TutorialPage3Data data)
        {
            if (data == null) return;
            
            // 데이터 기반으로 UI를 갱신하여 유지보수성을 높임
            if (descriptionText != null && data.descriptionText != null)
            {
                descriptionText.supportRichText = true;
                if (UIManager.Instance != null)
                {
                    UIManager.Instance.SetText(descriptionText.gameObject, data.descriptionText);
                }
                else
                {
                    Debug.LogWarning("[TutorialPage3Controller] UIManager.Instance가 null입니다.");
                }
            }
        }

        /// <summary>
        /// 페이지 진입 시 초기화 및 이벤트 구독 수행.
        /// </summary>
        public override void OnEnter()
        {
            base.OnEnter();
            
            // 재진입 시 이전 상태가 남아있으면 로직이 꼬일 수 있으므로 완전 초기화
            _isAFinished = false;
            _isBFinished = false;
            _isStepCompleted = false; // ★ 초기화

            _pAIsReady = _pAHasJumped = false;
            _pBIsReady = _pBHasJumped = false;
            _pAFirstFootTime = _pBFirstFootTime = 0f;

            InitCheckImage(checkImageA);
            InitCheckImage(checkImageB);

            // 사용자가 이미 키를 누르고 있는 상태에서 진입했을 때의 예외 처리를 위해 상태 동기화
            SyncInitialInputState();

            // 입력 매니저를 통해 하드웨어 입력 이벤트를 구독
            if (InputManager.Instance != null)
            {
                InputManager.Instance.OnPadDown += HandlePadDown;
                InputManager.Instance.OnPadUp += HandlePadUp;
            }

            Debug.Log("[TutorialPage3] 로직 시작: 이미 밟고 있다면 즉시 Ready 상태가 됩니다.");
        }

        /// <summary>
        /// 페이지 퇴장 시 이벤트 구독 해제.
        /// </summary>
        public override void OnExit()
        {
            base.OnExit();
            
            // 메모리 누수 및 다른 페이지에서의 오동작 방지를 위해 구독 해제 필수
            if (InputManager.Instance != null)
            {
                InputManager.Instance.OnPadDown -= HandlePadDown;
                InputManager.Instance.OnPadUp -= HandlePadUp;
            }
        }

        /// <summary>
        /// 페이지 진입 시점에 이미 눌려있는 키 상태를 확인하여 로직에 반영함.
        /// </summary>
        private void SyncInitialInputState()
        {
            // Player A (3, 4번 키)
            _pAPad0 = Input.GetKey(KeyCode.Alpha3);
            _pAPad1 = Input.GetKey(KeyCode.Alpha4);
            
            // 튜토리얼 중간에 멈췄다가 다시 시작하거나, 키를 누른 채로 넘어왔을 경우 바로 준비 상태로 인식하게 함
            if (_pAPad0 && _pAPad1)
            {
                _pAIsReady = true;
                Debug.Log("Player A: 이미 올라서 있음 -> Ready");
            }

            // Player B (9, 0번 키)
            _pBPad0 = Input.GetKey(KeyCode.Alpha9);
            _pBPad1 = Input.GetKey(KeyCode.Alpha0);

            if (_pBPad0 && _pBPad1)
            {
                _pBIsReady = true;
                Debug.Log("Player B: 이미 올라서 있음 -> Ready");
            }
        }

        /// <summary>
        /// 완료 체크 이미지를 초기화(숨김 처리)함.
        /// </summary>
        private void InitCheckImage(Image img)
        {
            if (img != null)
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
        /// 입력 상태가 변경될 때마다 호출되어 점프 로직을 갱신함.
        /// </summary>
        private void UpdateLogic(int playerIdx, int laneIdx, int padIdx, bool isDown)
        {
            // 점프 튜토리얼은 중앙 발판에서만 진행됨
            if (laneIdx != 1) return; 

            if (playerIdx == 0)
            {
                if (padIdx == 0) _pAPad0 = isDown;
                else if (padIdx == 1) _pAPad1 = isDown;

                // 상태 변경에 따른 시퀀스 판정 수행
                CheckSequence(0, _pAPad0, _pAPad1, ref _pAIsReady, ref _pAHasJumped, ref _pAFirstFootTime, ref _isAFinished, checkImageA);
            }
            else if (playerIdx == 1)
            {
                if (padIdx == 0) _pBPad0 = isDown;
                else if (padIdx == 1) _pBPad1 = isDown;

                CheckSequence(1, _pBPad0, _pBPad1, ref _pBIsReady, ref _pBHasJumped, ref _pBFirstFootTime, ref _isBFinished, checkImageB);
            }
            
            // 두 플레이어 모두 성공했고 아직 완료 처리되지 않았다면 1초 대기 후 이동
            if (_isAFinished && _isBFinished && !_isStepCompleted)
            {
                _isStepCompleted = true;
                StartCoroutine(WaitAndCompleteRoutine());
            }
        }

        /// <summary>
        /// 점프 동작의 유효성을 검증하는 핵심 로직.
        /// (발판 위 대기 -> 공중부양(두 발 뗌) -> 동시 착지) 순서를 확인.
        /// </summary>
        /// <param name="pIdx">플레이어 인덱스</param>
        /// <param name="p0">첫 번째 발판 입력 상태</param>
        /// <param name="p1">두 번째 발판 입력 상태</param>
        /// <param name="isReady">준비 상태(두 발 다 올림) 플래그 참조</param>
        /// <param name="hasJumped">점프 상태(두 발 다 뗌) 플래그 참조</param>
        /// <param name="firstFootTime">첫 번째 발이 착지한 시간 참조</param>
        /// <param name="isFinished">성공 여부 플래그 참조</param>
        /// <param name="checkImg">성공 시 표시할 UI 이미지</param>
        private void CheckSequence(int pIdx, bool p0, bool p1, ref bool isReady, ref bool hasJumped, ref float firstFootTime, ref bool isFinished, Image checkImg)
        {
            if (isFinished) return;

            int padCount = (p0 ? 1 : 0) + (p1 ? 1 : 0);
            
            // [상태 1: 공중] (0개 입력)
            // 준비된 상태에서 두 발을 모두 뗐다면 점프 중인 것으로 간주
            if (padCount == 0)
            {
                if (isReady) hasJumped = true; 
                firstFootTime = 0f; // 착지 타이머 초기화
            }
            // [상태 2: 한 발 착지] (1개 입력)
            else if (padCount == 1)
            {
                // 공중에서 내려와서 첫 발이 닿는 순간의 시간을 기록 (동시 착지 판정의 기준점)
                if (firstFootTime <= 0f)
                {
                    firstFootTime = Time.time;
                }
            }
            // [상태 3: 두 발 착지] (2개 입력)
            else if (padCount == 2)
            {
                // 점프 상태를 거쳤는지 확인 (그냥 걸어 올라온 것인지 점프한 것인지 구분)
                // 첫 발 착지 후 허용 오차 내에 두 번째 발이 착지했는지 확인 (동시 착지 판정)
                // 예: jumpLandingTolerance = 0.25s
                bool isSimultaneous = (Time.time - firstFootTime) <= jumpLandingTolerance;

                if (hasJumped && isSimultaneous)
                {
                    // 성공 조건 충족
                    isFinished = true;
                    StartCoroutine(FadeInRoutine(checkImg, 1f));
                    Debug.Log($"[TutorialPage3] P{pIdx} 성공! (착지 시간차: {Time.time - firstFootTime:F3}초)");
                }
                else
                {
                    // 실패 케이스 로깅
                    if (!isReady) Debug.Log($"[TutorialPage3] P{pIdx} 준비 완료. 점프하세요!");
                    else if (hasJumped && !isSimultaneous) Debug.Log($"[TutorialPage3] P{pIdx} 실패: 착지가 너무 느립니다. (동시 착지 필요)");

                    // 실패 시 상태를 리셋하여 다시 시도하게 함 (준비 상태로 복귀)
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
            if (targetImg == null) yield break;
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
        /// 마지막 성공 연출을 보기 위해 1초 대기 후 완료 처리하는 코루틴
        /// </summary>
        private IEnumerator WaitAndCompleteRoutine()
        {
            yield return CoroutineData.GetWaitForSeconds(1.0f);
            CompleteStep();
        }
    }
}