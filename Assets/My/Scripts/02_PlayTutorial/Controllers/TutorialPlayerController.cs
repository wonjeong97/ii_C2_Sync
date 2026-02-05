using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Serialization;

namespace My.Scripts._02_PlayTutorial.Controllers
{
    /// <summary>
    /// 플레이어의 물리 이동과 관련된 설정값들을 묶어서 관리하는 구조체.
    /// 매개변수의 개수를 줄이고 설정값 전달을 간소화하기 위해 사용함.
    /// </summary>
    [Serializable]
    public struct PlayerPhysicsConfig
    {
        public float runSpeedBoost;    // 한 번 움직일 때 증가하는 속도량
        public float maxScrollSpeed;   // 최대 속도 제한
        public float speedDecay;       // 속도 감쇠율 (마찰력)
        public float stopThreshold;    // 정지 판정 기준 속도
        public float maxDistance;      // 최대 이동 거리 (게이지 표시용)
    }

    /// <summary>
    /// 개별 플레이어 캐릭터의 이동, 입력 처리, 상태(스턴 등)를 관리하는 컨트롤러.
    /// UI와 직접적인 의존성을 없애기 위해 거리 변경 시 이벤트를 발생시킴.
    /// </summary>
    public class TutorialPlayerController : MonoBehaviour
    {
        [Header("Components")]
        [SerializeField] private RectTransform characterUI;
        [SerializeField] private CanvasGroup characterCanvasGroup; 

        [Header("State (Read Only)")]
        public int playerIndex;
        public float currentSpeed;
        public float currentDistance;
        public int currentLane = 1; 
        public bool IsStunned { get; private set; }

        // 외부(Manager/UI)에서 거리 변화를 감지할 수 있도록 이벤트 정의 (Decoupling)
        public event Action<float, float> OnDistanceChanged;

        private PlayerPhysicsConfig _config;
        private Vector2[] _lanePositions;
        private readonly bool[] _leftPadFlags = new bool[3];
        private readonly bool[] _rightPadFlags = new bool[3];

        /// <summary>
        /// 매니저로부터 초기 설정값과 위치 정보를 주입받아 초기화함.
        /// </summary>
        /// <param name="index">플레이어 인덱스 (0 or 1)</param>
        /// <param name="lanePositions">라인별 UI 좌표 배열</param>
        /// <param name="config">물리 설정 구조체</param>
        public void Setup(int index, Vector2[] lanePositions, PlayerPhysicsConfig config)
        {
            playerIndex = index;
            _lanePositions = lanePositions;
            _config = config;

            currentSpeed = 0f;
            currentDistance = 0f;
        
            // 컴포넌트가 누락되었을 경우 자동으로 추가하여 안정성 확보
            if (!characterCanvasGroup && characterUI) 
                characterCanvasGroup = characterUI.GetComponent<CanvasGroup>() ?? characterUI.gameObject.AddComponent<CanvasGroup>();

            MoveToLane(1); 
        
            // UI 갱신을 위해 초기 상태 이벤트 발송
            NotifyDistanceChanged();
        }

        /// <summary>
        /// 매 프레임 속도 물리 연산을 수행함.
        /// </summary>
        /// <param name="isAutoRun">자동 달리기 모드 여부</param>
        /// <param name="autoRunTargetSpeed">자동 달리기 시 목표 속도</param>
        /// <param name="autoRunSmoothTime">속도 보간 시간</param>
        public void OnUpdate(bool isAutoRun, float autoRunTargetSpeed, float autoRunSmoothTime)
        {
            // 피격 상태(스턴)일 때는 움직임을 멈춤
            if (IsStunned)
            {
                currentSpeed = 0f;
                return;
            }

            if (isAutoRun)
            {
                // 자동 모드: 목표 속도까지 부드럽게 가속/감속
                currentSpeed = Mathf.Lerp(currentSpeed, autoRunTargetSpeed, Time.deltaTime * autoRunSmoothTime);
            }
            else
            {
                // 수동 모드: 입력이 없으면 마찰력에 의해 서서히 감속
                currentSpeed = Mathf.Lerp(currentSpeed, 0f, _config.speedDecay * Time.deltaTime);
            
                // 미세한 속도 값은 0으로 절삭하여 연산 낭비 방지
                if (currentSpeed < _config.stopThreshold) currentSpeed = 0f;
            }
        }

        /// <summary>
        /// 양발 입력(패드 2개 동시 입력)을 감지하고 성공 여부를 반환함.
        /// </summary>
        public bool HandleInput(int laneIdx, int padIdx)
        {
            if (IsStunned) return false;

            // 해당 라인의 패드 입력 상태 갱신
            if (padIdx == 0) _leftPadFlags[laneIdx] = true;
            else _rightPadFlags[laneIdx] = true;

            // 두 패드가 모두 눌렸을 때만 유효한 입력으로 처리 (양발 달리기 메커니즘)
            if (_leftPadFlags[laneIdx] && _rightPadFlags[laneIdx])
            {
                // 입력 소비 (플래그 리셋)
                _leftPadFlags[laneIdx] = false;
                _rightPadFlags[laneIdx] = false;
                return true;
            }
            return false;
        }

        /// <summary>
        /// 캐릭터를 해당 라인으로 이동시키고 속도를 증가시킴.
        /// </summary>
        public void MoveAndAccelerate(int laneIdx)
        {
            MoveToLane(laneIdx);
        
            // 달리기 액션에 따른 속도 부스트 적용 및 최대 속도 클램핑
            currentSpeed += _config.runSpeedBoost;
            if (currentSpeed > _config.maxScrollSpeed) currentSpeed = _config.maxScrollSpeed;

            // 이동 거리 누적 및 UI 갱신 이벤트 전파
            currentDistance += 1f;
            NotifyDistanceChanged(); 
        }

        /// <summary>
        /// 강제로 이동 거리를 추가함 (자동 진행 등에서 사용).
        /// </summary>
        public void AddDistance(float amount)
        {
            currentDistance += amount;
            NotifyDistanceChanged(); 
        }

        /// <summary>
        /// 거리 변경 이벤트를 발생시켜 구독자(UI 등)에게 알림.
        /// </summary>
        private void NotifyDistanceChanged()
        {
            OnDistanceChanged?.Invoke(currentDistance, _config.maxDistance);
        }

        /// <summary>
        /// 캐릭터 UI를 지정된 라인 좌표로 즉시 이동시킴.
        /// </summary>
        public void MoveToLane(int laneIdx)
        {
            if (laneIdx < 0 || laneIdx >= _lanePositions.Length) return;
        
            currentLane = laneIdx;
            if (characterUI) characterUI.anchoredPosition = _lanePositions[laneIdx];
        }

        /// <summary>
        /// 장애물 충돌 시 페널티(스턴) 효과를 적용함.
        /// </summary>
        /// <param name="duration">스턴 지속 시간</param>
        public void OnHit(float duration)
        {
            StartCoroutine(StunRoutine(duration));
        }

        /// <summary>
        /// 일정 시간 동안 캐릭터를 깜빡이며 조작 불능 상태로 만드는 코루틴.
        /// </summary>
        private IEnumerator StunRoutine(float duration)
        {
            IsStunned = true;
            float elapsed = 0f;
            float blinkInterval = 0.2f;

            // 지속 시간 동안 깜빡임 효과 반복
            while (elapsed < duration)
            {
                if (characterCanvasGroup) 
                    characterCanvasGroup.alpha = (characterCanvasGroup.alpha > 0.5f) ? 0.3f : 1.0f;
                yield return new WaitForSeconds(blinkInterval);
                elapsed += blinkInterval;
            }

            // 상태 복구
            if (characterCanvasGroup) characterCanvasGroup.alpha = 1.0f;
            IsStunned = false;
        }
    }
}