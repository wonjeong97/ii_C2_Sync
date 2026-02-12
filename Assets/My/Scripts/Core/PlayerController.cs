using System;
using System.Collections;
using UnityEngine;

namespace My.Scripts.Core
{
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

    public class PlayerController : MonoBehaviour
    {
        [Header("Components")]
        [SerializeField] private RectTransform characterUI;
        [SerializeField] private CanvasGroup characterCanvasGroup;
        [SerializeField] private Animator characterAnimator;

        [Header("Animation Settings")]
        [Tooltip("이동 속도 대비 애니메이션 재생 속도 비율 (기본값 1.0)")]
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

        public event Action<int, float, float> OnDistanceChanged;

        private PlayerPhysicsConfig _config;
        private Vector2[] _lanePositions;
        private readonly bool[] _leftPadFlags = new bool[3];
        private readonly bool[] _rightPadFlags = new bool[3];
        private Coroutine _stunCoroutine;
        
        // 이동 코루틴 참조 변수
        private Coroutine _moveCoroutine;

        private static readonly int RunSpeedParam = Animator.StringToHash("RunSpeed");

        public void Setup(int index, Vector2[] lanePositions, PlayerPhysicsConfig config)
        {
            if (characterAnimator == null)
            {
                Debug.LogWarning("[PlayerController] characterAnimator is null");
            }
            
            playerIndex = index;
            _lanePositions = lanePositions;
            _config = config;

            currentSpeed = 0f;
            currentDistance = 0f;
        
            if (!characterCanvasGroup && characterUI) 
                characterCanvasGroup = characterUI.GetComponent<CanvasGroup>() ?? characterUI.gameObject.AddComponent<CanvasGroup>();

            // 초기화 시 코루틴 상태 리셋 (버그 방지)
            if (_moveCoroutine != null) StopCoroutine(_moveCoroutine);
            _moveCoroutine = null;

            // 초기화 시 중앙(1번) 라인에 즉시 배치 (점프 X)
            if (_lanePositions != null && _lanePositions.Length > 1)
            {
                currentLane = 1;
                if (characterUI) characterUI.anchoredPosition = _lanePositions[1];
            }
            
            NotifyDistanceChanged();
        }

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

            float normalizedSpeed = (_config.maxScrollSpeed > 0) 
                ? (currentSpeed / _config.maxScrollSpeed) 
                : 0f;

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

        // ★ [수정] 점프 중 이동 불가 로직 추가
        public void MoveToLane(int laneIdx)
        {
            if (laneIdx < 0 || laneIdx >= _lanePositions.Length) return;
            
            // 1. 이미 이동(점프) 중이라면 입력 무시 (중복 실행 방지)
            if (_moveCoroutine != null) return;

            // 2. 현재 위치와 같은 라인으로 이동하려 하면 무시
            if (currentLane == laneIdx) return;
        
            currentLane = laneIdx;

            if (characterUI)
            {
                Vector2 startPos = characterUI.anchoredPosition;
                Vector2 targetPos = _lanePositions[laneIdx];

                // 새로운 이동 시작
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

                // 포물선 효과 (0 -> 1 -> 0)
                float heightOffset = Mathf.Sin(t * Mathf.PI) * jumpArcHeight;
                currentPos.y += heightOffset;

                characterUI.anchoredPosition = currentPos;
                yield return null;
            }

            characterUI.anchoredPosition = targetPos;
            
            // ★ 이동 완료 시 변수 초기화하여 다음 입력 허용
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