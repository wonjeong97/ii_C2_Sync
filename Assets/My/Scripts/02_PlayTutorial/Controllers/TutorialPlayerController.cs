using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Serialization;

namespace My.Scripts._02_PlayTutorial.Controllers
{
    [Serializable]
    public struct PlayerPhysicsConfig
    {
        public float runSpeedBoost;    
        public float maxScrollSpeed;   
        public float speedDecay;       
        public float stopThreshold;    
        public float maxDistance;      
        
        // ★ [추가] 거리 계산 모드 옵션
        public bool useMetricDistance; // True면 속도 기반(m), False면 횟수 기반(Step)
        public float metricMultiplier; // 속도(UV)를 거리(m)로 변환할 비율 (기본 200)
    }

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

        public event Action<int, float, float> OnDistanceChanged;

        private PlayerPhysicsConfig _config;
        private Vector2[] _lanePositions;
        private readonly bool[] _leftPadFlags = new bool[3];
        private readonly bool[] _rightPadFlags = new bool[3];
        private Coroutine _stunCoroutine;

        public void Setup(int index, Vector2[] lanePositions, PlayerPhysicsConfig config)
        {
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

            // ★ [추가] 속도 기반 거리 누적 (150M 모드용)
            // 실제 이동한 거리 = 속도 * 시간 * 변환비율
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

            // ★ [수정] 횟수 기반일 때만 +1 (튜토리얼용)
            // 속도 기반일 때는 OnUpdate에서 거리가 늘어나므로 여기서는 더하지 않음
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

        public void OnHit(float duration)
        {
            if (_stunCoroutine != null) StopCoroutine(_stunCoroutine);
            _stunCoroutine = StartCoroutine(StunRoutine(duration));
        }

        private IEnumerator StunRoutine(float duration)
        {
            IsStunned = true;
            float elapsed = 0f;
            float blinkInterval = 0.2f;

            while (elapsed < duration)
            {
                if (characterCanvasGroup) 
                    characterCanvasGroup.alpha = (characterCanvasGroup.alpha > 0.5f) ? 0.3f : 1.0f;
                yield return new WaitForSeconds(blinkInterval);
                elapsed += blinkInterval;
            }

            if (characterCanvasGroup) characterCanvasGroup.alpha = 1.0f;
            IsStunned = false;
            _stunCoroutine = null;
        }
        
        public void ForceStop()
        {
            currentSpeed = 0f;
        }
    }
}