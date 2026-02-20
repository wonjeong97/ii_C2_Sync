using System;
using System.Collections;
using UnityEngine;

namespace My.Scripts.Core
{
    /// <summary>
    /// 플레이어 이동 연산에 필요한 상수들을 정의하는 물리 설정 구조체.
    /// </summary>
    [Serializable]
    public struct PlayerPhysicsConfig
    {
        public float runSpeedBoost;    
        public float maxScrollSpeed;   
        public float speedDecay;       
        public float stopThreshold;    
        public float maxDistance;      
        
        public bool useMetricDistance; 
        public float metricMultiplier; 
    }

    /// <summary>
    /// 플레이어 캐릭터의 이동, 애니메이션, 피격 상태 및 손 위치 정보를 관리하는 컨트롤러.
    /// </summary>
    public class PlayerController : MonoBehaviour
    {
        [Header("Components")]
        [SerializeField] private RectTransform characterUI;
        [SerializeField] private CanvasGroup characterCanvasGroup;
        [SerializeField] private Animator characterAnimator;

        [Header("Hand Anchors (Animation)")]
        [Tooltip("애니메이션에 맞춰 실이 따라갈 손 Bone (Transform)")]
        [SerializeField] private Transform leftHandTransform;
        [Tooltip("애니메이션에 맞춰 실이 따라갈 손 Bone (Transform)")]
        [SerializeField] private Transform rightHandTransform;

        [Header("Default Hand Offsets (UI Pixels)")]
        [Tooltip("Bone이 없을 때 사용할 왼쪽 손 기본 좌표 (-86, -33)")]
        [SerializeField] private Vector2 leftHandDefaultOffset = new Vector2(-86f, -33f);
        [Tooltip("Bone이 없을 때 사용할 오른쪽 손 기본 좌표 (86, -33)")]
        [SerializeField] private Vector2 rightHandDefaultOffset = new Vector2(86f, -33f);

        [Header("Animation Settings")]
        [Tooltip("이동 속도 대비 애니메이션 재생 속도 비율")]
        [SerializeField] private float runSpeedMultiplier = 1.0f;

        [Header("Movement Settings")]
        [Tooltip("라인 이동 시 점프 높이 (UI 좌표 기준)")]
        [SerializeField] private float jumpArcHeight = 50f; 
        [Tooltip("라인 이동에 걸리는 시간 (초)")]
        [SerializeField] private float jumpDuration = 0.25f;

        [Header("State (Read Only)")]
        public int playerIndex;
        public float currentSpeed;
        public float currentDistance;
        public int currentLane = 1; 
        public bool IsStunned { get; private set; }
        public RectTransform CharacterRect => characterUI;

        public event Action<int, float, float> OnDistanceChanged;

        private PlayerPhysicsConfig _config;
        private Vector2[] _lanePositions;
        private readonly bool[] _leftPadFlags = new bool[3];
        private readonly bool[] _rightPadFlags = new bool[3];
        private Coroutine _stunCoroutine;
        private Coroutine _moveCoroutine;

        private static readonly int RunSpeedParam = Animator.StringToHash("RunSpeed");

        /// <summary>
        /// 초기 데이터 및 좌표 설정을 수행합니다.
        /// </summary>
        public void Setup(int index, Vector2[] lanePositions, PlayerPhysicsConfig config)
        {
            playerIndex = index;
            _lanePositions = lanePositions;
            _config = config;

            currentSpeed = 0f;
            currentDistance = 0f;
        
            if (!characterCanvasGroup && characterUI) 
                characterCanvasGroup = characterUI.GetComponent<CanvasGroup>() ?? characterUI.gameObject.AddComponent<CanvasGroup>();

            if (_moveCoroutine != null) StopCoroutine(_moveCoroutine);
            _moveCoroutine = null;

            // 초기화 시 중앙(1번) 라인에 즉시 배치
            if (_lanePositions != null && _lanePositions.Length > 1)
            {
                currentLane = 1;
                if (characterUI) characterUI.anchoredPosition = _lanePositions[1];
            }
            
            NotifyDistanceChanged();
        }

        /// <summary>
        /// 요청된 방향에 따른 손의 현재 월드 좌표를 반환합니다. 
        /// Bone이 할당되지 않은 경우 지정된 UI 오프셋을 캐릭터 좌표에 더해 계산합니다.
        /// </summary>
        /// <param name="isRightHand">true면 오른손, false면 왼손</param>
        public Vector3 GetHandWorldPosition(bool isRightHand)
        {
            Transform target = isRightHand ? rightHandTransform : leftHandTransform;
            
            // 1. 애니메이션 Bone이 할당되어 있다면 해당 월드 좌표 반환
            if (target != null) return target.position;

            // 2. Bone이 없다면 캐릭터 UI 위치 기준 상대 좌표(-86 또는 86)를 월드 좌표로 변환하여 반환
            Vector2 offset = isRightHand ? rightHandDefaultOffset : leftHandDefaultOffset;
            if (characterUI == null) return transform.position;

            return characterUI.TransformPoint(offset);
        }

        /// <summary>
        /// 매 프레임 속도 감쇠 및 물리 상태를 업데이트합니다.
        /// </summary>
        public void OnUpdate(bool isAutoRun, float autoRunTargetSpeed, float autoRunSmoothTime)
        {
            if (IsStunned)
            {
                currentSpeed = 0f;
                if (characterAnimator) characterAnimator.SetFloat(RunSpeedParam, 0f);
                return; 
            }

            if (isAutoRun)
            {
                currentSpeed = Mathf.Lerp(currentSpeed, autoRunTargetSpeed, Time.deltaTime * autoRunSmoothTime);
            }
            else
            {
                currentSpeed = Mathf.Lerp(currentSpeed, 0f, _config.speedDecay * Time.deltaTime);
                if (currentSpeed < _config.stopThreshold) currentSpeed = 0f;
            }

            if (_config.useMetricDistance)
            {
                float distanceDelta = currentSpeed * Time.deltaTime * _config.metricMultiplier;
                if (distanceDelta > 0)
                {
                    currentDistance += distanceDelta;
                    NotifyDistanceChanged();
                }
            }

            UpdateAnimationSpeed();
        }

        private void UpdateAnimationSpeed()
        {
            if (characterAnimator == null) return;
            float normalizedSpeed = (_config.maxScrollSpeed > 0) ? (currentSpeed / _config.maxScrollSpeed) : 0f;
            if (normalizedSpeed < 0.1f) normalizedSpeed = 0f;
            characterAnimator.SetFloat(RunSpeedParam, normalizedSpeed * runSpeedMultiplier);
        }

        public void OnHit(float duration)
        {
            if (_stunCoroutine != null) StopCoroutine(_stunCoroutine);
            _stunCoroutine = StartCoroutine(StunRoutine(duration));
        }

        private IEnumerator StunRoutine(float duration)
        {
            IsStunned = true; 
            if (characterAnimator) characterAnimator.SetFloat(RunSpeedParam, 0f);

            float elapsed = 0f;
            float blinkTimer = 0f;
            float blinkInterval = 0.2f; 

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                blinkTimer += Time.deltaTime;
                if (blinkTimer >= blinkInterval)
                {
                    blinkTimer = 0f;
                    if (characterCanvasGroup) 
                        characterCanvasGroup.alpha = (characterCanvasGroup.alpha > 0.5f) ? 0.3f : 1.0f;
                }
                yield return null;
            }

            if (characterCanvasGroup) characterCanvasGroup.alpha = 1.0f;
            IsStunned = false;
            _stunCoroutine = null;
        }

        /// <summary>
        /// 발판 입력 이벤트를 처리하여 한 라인의 두 발판이 모두 눌렸는지 판단합니다.
        /// </summary>
        public bool HandleInput(int laneIdx, int padIdx)
        {
            if (IsStunned) return false; 
            if (laneIdx < 0 || laneIdx >= _leftPadFlags.Length) return false;
            if (padIdx != 0 && padIdx != 1) return false;

            if (padIdx == 0) _leftPadFlags[laneIdx] = true;
            else _rightPadFlags[laneIdx] = true;

            if (_leftPadFlags[laneIdx] && _rightPadFlags[laneIdx])
            {
                _leftPadFlags[laneIdx] = false;
                _rightPadFlags[laneIdx] = false;
                return true;
            }
            return false;
        }

        /// <summary>
        /// 캐릭터를 해당 라인으로 이동시키고 속도를 증가시킵니다.
        /// </summary>
        public void MoveAndAccelerate(int laneIdx)
        {
            MoveToLane(laneIdx);
        
            currentSpeed += _config.runSpeedBoost;
            if (currentSpeed > _config.maxScrollSpeed) currentSpeed = _config.maxScrollSpeed;

            if (!_config.useMetricDistance)
            {
                currentDistance += 1f;
                NotifyDistanceChanged(); 
            }
        }

        public void AddDistance(float amount)
        {
            currentDistance += amount;
            NotifyDistanceChanged(); 
        }

        private void NotifyDistanceChanged()
        {
            OnDistanceChanged?.Invoke(playerIndex, currentDistance, _config.maxDistance);
        }

        /// <summary>
        /// 지정된 인덱스의 라인으로 캐릭터를 이동(점프)시킵니다.
        /// 외부 매니저에서 접근 가능하도록 public으로 선언되어 있습니다.
        /// </summary>
        public void MoveToLane(int laneIdx)
        {
            if (laneIdx < 0 || laneIdx >= _lanePositions.Length) return;
            
            // 이동(점프) 중이거나 현재 위치와 같은 라인이면 무시
            if (_moveCoroutine != null || currentLane == laneIdx) return;
        
            currentLane = laneIdx;

            if (characterUI)
            {
                Vector2 startPos = characterUI.anchoredPosition;
                Vector2 targetPos = _lanePositions[laneIdx];
                _moveCoroutine = StartCoroutine(MoveLaneRoutine(startPos, targetPos, jumpDuration));
            }
        }

        private IEnumerator MoveLaneRoutine(Vector2 startPos, Vector2 targetPos, float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                // 직선 이동 보간
                Vector2 currentPos = Vector2.Lerp(startPos, targetPos, t);

                // 포물선 효과 적용
                float heightOffset = Mathf.Sin(t * Mathf.PI) * jumpArcHeight;
                currentPos.y += heightOffset;

                characterUI.anchoredPosition = currentPos;
                yield return null;
            }

            characterUI.anchoredPosition = targetPos;
            _moveCoroutine = null; 
        }
        
        public void ForceStop()
        {
            currentSpeed = 0f;
            if (characterAnimator) characterAnimator.SetFloat(RunSpeedParam, 0f);
        }
        
        public void SetFinishAnimation()
        {
            if (characterAnimator != null)
            {
                characterAnimator.SetTrigger("Finish");
            }
        }
    }
}