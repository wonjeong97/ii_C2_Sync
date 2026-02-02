using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using My.Scripts.Core;
using My.Scripts.Global;
using Wonjeong.Data;
using Wonjeong.UI;

namespace My.Scripts._01_Tutorial.Pages
{
    [Serializable]
    public class TutorialPage3Data
    {
        public TextSetting descriptionText;
    }

    public class TutorialPage3Controller : GamePage<TutorialPage3Data>
    {
        [Header("UI Components")]
        [SerializeField] private Text descriptionText;
        [SerializeField] private Image checkImageA;
        [SerializeField] private Image checkImageB;

        [Header("Settings")]
        [SerializeField] private float jumpLandingTolerance = 0.25f; // 동시 착지 허용 시간 (초)

        private bool _isAFinished = false;
        private bool _isBFinished = false;

        // === 상태 관리 변수 ===
        private bool _pA_Pad0, _pA_Pad1;
        private bool _pB_Pad0, _pB_Pad1;

        // 점프 시퀀스 플래그
        private bool _pA_IsReady = false;
        private bool _pA_HasJumped = false;
        private float _pA_FirstFootTime = 0f; // A의 첫 발이 닿은 시간

        private bool _pB_IsReady = false;
        private bool _pB_HasJumped = false;
        private float _pB_FirstFootTime = 0f; // B의 첫 발이 닿은 시간

        protected override void SetupData(TutorialPage3Data data)
        {
            if (data == null) return;
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

        public override void OnEnter()
        {
            base.OnEnter();
            
            // 1. 변수 초기화
            _isAFinished = false;
            _isBFinished = false;
            _pA_IsReady = _pA_HasJumped = false;
            _pB_IsReady = _pB_HasJumped = false;
            _pA_FirstFootTime = _pB_FirstFootTime = 0f;

            InitCheckImage(checkImageA);
            InitCheckImage(checkImageB);

            // 2. 초기 입력 상태 동기화 (이미 밟고 있는 경우 처리)
            SyncInitialInputState();

            // 3. 이벤트 구독
            if (InputManager.Instance != null)
            {
                InputManager.Instance.OnPadDown += HandlePadDown;
                InputManager.Instance.OnPadUp += HandlePadUp;
            }

            Debug.Log("[TutorialPage3] 로직 시작: 이미 밟고 있다면 즉시 Ready 상태가 됩니다.");
        }

        public override void OnExit()
        {
            base.OnExit();
            if (InputManager.Instance != null)
            {
                InputManager.Instance.OnPadDown -= HandlePadDown;
                InputManager.Instance.OnPadUp -= HandlePadUp;
            }
        }

        /// <summary>
        /// 진입 시점에 이미 키를 누르고 있는지 확인하여 상태 동기화
        /// </summary>
        private void SyncInitialInputState()
        {
            // Player A (3, 4번 키)
            _pA_Pad0 = Input.GetKey(KeyCode.Alpha3);
            _pA_Pad1 = Input.GetKey(KeyCode.Alpha4);
            
            // 이미 둘 다 밟고 있다면 -> 준비 완료 상태로 시작
            if (_pA_Pad0 && _pA_Pad1)
            {
                _pA_IsReady = true;
                Debug.Log("Player A: 이미 올라서 있음 -> Ready");
            }

            // Player B (9, 0번 키)
            _pB_Pad0 = Input.GetKey(KeyCode.Alpha9);
            _pB_Pad1 = Input.GetKey(KeyCode.Alpha0);

            if (_pB_Pad0 && _pB_Pad1)
            {
                _pB_IsReady = true;
                Debug.Log("Player B: 이미 올라서 있음 -> Ready");
            }
        }

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

        private void UpdateLogic(int playerIdx, int laneIdx, int padIdx, bool isDown)
        {
            if (laneIdx != 1) return; // 중앙 라인만

            if (playerIdx == 0)
            {
                // 상태 갱신
                if (padIdx == 0) _pA_Pad0 = isDown;
                else if (padIdx == 1) _pA_Pad1 = isDown;

                CheckSequence(0, _pA_Pad0, _pA_Pad1, ref _pA_IsReady, ref _pA_HasJumped, ref _pA_FirstFootTime, ref _isAFinished, checkImageA);
            }
            else if (playerIdx == 1)
            {
                if (padIdx == 0) _pB_Pad0 = isDown;
                else if (padIdx == 1) _pB_Pad1 = isDown;

                CheckSequence(1, _pB_Pad0, _pB_Pad1, ref _pB_IsReady, ref _pB_HasJumped, ref _pB_FirstFootTime, ref _isBFinished, checkImageB);
            }
            
            if (_isAFinished && _isBFinished) CompleteStep();
        }

        /// <summary>
        /// 점프 시퀀스 및 동시 착지 판정 로직
        /// </summary>
        private void CheckSequence(int pIdx, bool p0, bool p1, ref bool isReady, ref bool hasJumped, ref float firstFootTime, ref bool isFinished, Image checkImg)
        {
            if (isFinished) return;

            int padCount = (p0 ? 1 : 0) + (p1 ? 1 : 0);
            
            // [상태: 공중] (0개)
            if (padCount == 0)
            {
                if (isReady) hasJumped = true; // 준비된 상태에서 떴다면 점프 중
                firstFootTime = 0f; // 타이머 리셋
            }
            // [상태: 한 발 착지] (1개)
            else if (padCount == 1)
            {
                // 아직 첫 발 시간이 기록 안 됐다면 지금이 첫 발 닿은 순간
                if (firstFootTime <= 0f)
                {
                    firstFootTime = Time.time;
                }
            }
            // [상태: 두 발 착지] (2개)
            else if (padCount == 2)
            {
                // 이미 점프 상태였고(hasJumped),
                // && 첫 발 닿은 후 허용 시간 내에 두 번째 발이 닿았는가? (동시 착지 체크)
                bool isSimultaneous = (Time.time - firstFootTime) <= jumpLandingTolerance;

                if (hasJumped && isSimultaneous)
                {
                    // 성공!
                    isFinished = true;
                    StartCoroutine(FadeInRoutine(checkImg, 1.0f));
                    Debug.Log($"[TutorialPage3] P{pIdx} 성공! (착지 시간차: {Time.time - firstFootTime:F3}초)");
                }
                else
                {
                    // 실패: 그냥 걸어 올라왔거나(hasJumped false), 너무 느리게 착지함
                    if (!isReady) Debug.Log($"[TutorialPage3] P{pIdx} 준비 완료. 점프하세요!");
                    else if (hasJumped && !isSimultaneous) Debug.Log($"[TutorialPage3] P{pIdx} 실패: 착지가 너무 느립니다. (동시 착지 필요)");

                    // 상태 리셋: 다시 준비(Ready) 상태로 돌아감
                    isReady = true;
                    hasJumped = false;
                    firstFootTime = 0f;
                }
            }
        }

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

        private void Update()
        {
            // 디버그용 (엔터)
            if (Input.GetKeyDown(KeyCode.Return))
            {
                if (!_isAFinished) { _isAFinished = true; StartCoroutine(FadeInRoutine(checkImageA, 1.0f)); }
                if (!_isBFinished) { _isBFinished = true; StartCoroutine(FadeInRoutine(checkImageB, 1.0f)); }
                if (_isAFinished && _isBFinished) CompleteStep();
            }
        }
    }
}