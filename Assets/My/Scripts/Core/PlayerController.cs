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
        
        public bool useMetricDistance; // True면 속도 기반(m), False면 횟수 기반(Step)
        public float metricMultiplier; // 속도(UV)를 거리(m)로 변환할 비율
    }

    public class PlayerController : MonoBehaviour
    {
        [Header("Components")]
        [SerializeField] private RectTransform characterUI;
        [SerializeField] private CanvasGroup characterCanvasGroup;
        [SerializeField] private Animator characterAnimator;

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

            MoveToLane(1); 
            NotifyDistanceChanged();
        }

        public void OnUpdate(bool isAutoRun, float autoRunTargetSpeed, float autoRunSmoothTime)
        {
            // 스턴 상태일 때는 이동 속도 0으로 고정
            if (IsStunned)
            {
                currentSpeed = 0f;
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

            // 속도 기반 거리 누적 (150M 모드 등)
            if (_config.useMetricDistance)
            {
                float distanceDelta = currentSpeed * Time.deltaTime * _config.metricMultiplier;
                if (distanceDelta > 0)
                {
                    currentDistance += distanceDelta;
                    NotifyDistanceChanged();
                }
            }
        }

        // 피격 처리: 지정된 시간(duration) 동안 스턴 및 점멸
        public void OnHit(float duration)
        {
            if (_stunCoroutine != null) StopCoroutine(_stunCoroutine);
            _stunCoroutine = StartCoroutine(StunRoutine(duration));
        }

        // 시간 기반 점멸 코루틴 (0.2초 간격 깜빡임)
        private IEnumerator StunRoutine(float duration)
        {
            IsStunned = true; // 스턴 상태 (입력 차단, 이동 정지)
            float elapsed = 0f;
            float blinkTimer = 0f;
            float blinkInterval = 0.2f; // 깜빡임 간격

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                blinkTimer += Time.deltaTime;

                if (blinkTimer >= blinkInterval)
                {
                    blinkTimer = 0f;
                    // 알파값 토글 (켜져있으면 끄고, 꺼져있으면 켬)
                    if (characterCanvasGroup) 
                        characterCanvasGroup.alpha = (characterCanvasGroup.alpha > 0.5f) ? 0.3f : 1.0f;
                }
                
                yield return null;
            }

            // 종료 시 상태 복구
            if (characterCanvasGroup) characterCanvasGroup.alpha = 1.0f;
            IsStunned = false;
            _stunCoroutine = null;
        }

        public bool HandleInput(int laneIdx, int padIdx)
        {
            if (IsStunned) return false; // 스턴 중 입력 무시
            
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

            // 횟수 기반일 때만 +1 (튜토리얼용), 150M 모드(Metric)에서는 OnUpdate에서 처리
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

        public void MoveToLane(int laneIdx)
        {
            if (laneIdx < 0 || laneIdx >= _lanePositions.Length) return;
        
            currentLane = laneIdx;
            if (characterUI) characterUI.anchoredPosition = _lanePositions[laneIdx];
        }
        
        public void ForceStop()
        {
            currentSpeed = 0f;
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