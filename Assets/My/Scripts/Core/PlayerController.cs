using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Microsoft.Extensions.Logging;
using My.Scripts.Core;
using UnityEngine;
using UnityEngine.UI;
using VContainer;
using Wonjeong.UI;
using ZLogger;

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

    [RequireComponent(typeof(RectTransform))]
    public class PlayerController : MonoBehaviour
    {
        [Header("Components")]
        [SerializeField] private RectTransform characterUI;
        [SerializeField] private CanvasGroup characterCanvasGroup;
        [SerializeField] private Animator characterAnimator;

        [Header("Character Parts")]
        [SerializeField] private Image bodyImage;
        [SerializeField] private Image leftHandImage;
        [SerializeField] private Image rightHandImage;

        [Header("Hand Anchors")]
        [SerializeField] private Transform leftHandTransform;
        [SerializeField] private Transform rightHandTransform;

        [Header("Animation Settings")]
        [SerializeField] private float runSpeedMultiplier = 1.0f;
        [SerializeField] private float jumpArcHeight = 50f;
        [SerializeField] private float jumpDuration = 0.25f;

        public int playerIndex;
        public float currentSpeed;
        public float currentDistance;
        public int currentLane = 1;
        public bool IsStunned { get; private set; }
        public RectTransform CharacterRect => characterUI;

        public event Action<int, float, float> OnDistanceChanged;

        private PlayerPhysicsConfig _config;
        private Vector2[] _lanePositions;
        private int[] _lastPadIdxByLane;
        private CancellationTokenSource _cts;

        private ILogger<PlayerController> _logger;
        private SoundManager _soundManager;

        private readonly static int RunSpeedParam = Animator.StringToHash("RunSpeed");
        private readonly static int Finish = Animator.StringToHash("Finish");
        private readonly static int Jump = Animator.StringToHash("Jump");
        private readonly static int Idle = Animator.StringToHash("Idle");

        [Inject]
        public void Construct(ILogger<PlayerController> logger, SoundManager soundManager)
        {
            _logger = logger;
            _soundManager = soundManager;
        }

        private void OnDestroy()
        {
            _cts?.Cancel();
            _cts?.Dispose();
        }

        public void Setup(int index, Vector2[] lanePositions, PlayerPhysicsConfig config)
        {
            playerIndex = index;
            _lanePositions = lanePositions;
            _config = config;
            currentSpeed = 0f;
            currentDistance = 0f;
            
            _lastPadIdxByLane = new int[_lanePositions != null ? _lanePositions.Length : 3];
            for (int i = 0; i < _lastPadIdxByLane.Length; i++)
            {
                _lastPadIdxByLane[i] = -1;
            }
            
            if (!characterCanvasGroup && characterUI)
            {
                characterCanvasGroup = characterUI.GetComponent<CanvasGroup>() ?? characterUI.gameObject.AddComponent<CanvasGroup>();
            }

            _cts?.Cancel();
            _cts = new CancellationTokenSource();

            if (_lanePositions != null && _lanePositions.Length > 1)
            {
                currentLane = 1;
                characterUI.anchoredPosition = _lanePositions[1];
            }
            NotifyDistanceChanged();
        }

        /// <summary>
        /// 플레이어 이동 및 가속/감속 상태를 업데이트함
        /// </summary>
        public void OnUpdate(bool isAutoRun, float autoRunTargetSpeed, float autoRunSmoothTime)
        {
            if (IsStunned)
            {
                currentSpeed = 0f;
                if (characterAnimator) characterAnimator.SetFloat(RunSpeedParam, 0f);
                return;
            }

            currentSpeed = isAutoRun 
                ? Mathf.Lerp(currentSpeed, autoRunTargetSpeed, Time.deltaTime * autoRunSmoothTime)
                : Mathf.Max(0f, Mathf.Lerp(currentSpeed, 0f, _config.speedDecay * Time.deltaTime));

            // Lerp 초기 가속값이 임계값에 막혀 속도가 무한 리셋되는 현상 방지
            if (!isAutoRun && currentSpeed < _config.stopThreshold)
            {
                currentSpeed = 0f;
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

            if (characterAnimator)
            {
                float normalizedSpeed = (_config.maxScrollSpeed > 0) ? (currentSpeed / _config.maxScrollSpeed) : 0f;
                characterAnimator.SetFloat(RunSpeedParam, (normalizedSpeed < 0.1f ? 0f : normalizedSpeed) * runSpeedMultiplier);
            }
        }

        public void OnHit(float duration) 
        {
            if (IsStunned) return; 
            StunTaskAsync(duration, _cts.Token).Forget();
        }

        private async UniTaskVoid StunTaskAsync(float duration, CancellationToken ct)
        {
            IsStunned = true;
            characterAnimator?.SetFloat(RunSpeedParam, 0f);

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                if (characterCanvasGroup)
                    characterCanvasGroup.alpha = (Mathf.Sin(Time.time * 20f) > 0) ? 1.0f : 0.3f;
                
                await UniTask.Yield(PlayerLoopTiming.Update, ct);
            }

            if (characterCanvasGroup) characterCanvasGroup.alpha = 1.0f;
            IsStunned = false;
        }

        public void MoveToLane(int laneIdx)
        {
            if (_lanePositions == null || laneIdx < 0 || laneIdx >= _lanePositions.Length || currentLane == laneIdx) return;

            int laneDiff = Mathf.Max(1, Mathf.Abs(laneIdx - currentLane));
            currentLane = laneIdx;
            _soundManager?.PlaySFX("달리기_1");

            MoveLaneTaskAsync(characterUI.anchoredPosition, _lanePositions[laneIdx], 
                jumpDuration * (1f + 0.3f * (laneDiff - 1)), jumpArcHeight * laneDiff, _cts.Token).Forget();
        }

        private async UniTaskVoid MoveLaneTaskAsync(Vector2 start, Vector2 target, float duration, float arcHeight, CancellationToken ct)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                Vector2 pos = Vector2.Lerp(start, target, t);
                pos.y += Mathf.Sin(t * Mathf.PI) * arcHeight;
                characterUI.anchoredPosition = pos;
                await UniTask.Yield(PlayerLoopTiming.Update, ct);
            }
            characterUI.anchoredPosition = target;
        }

        public void SetFinishAnimation()
        {
            characterAnimator?.SetTrigger(Finish);
            characterAnimator?.SetTrigger(Jump);
        }

        public async UniTaskVoid SetFinishTaskAsync(CancellationToken ct)
        {
            SetFinishAnimation();
            await UniTask.Delay(TimeSpan.FromSeconds(1.0f), cancellationToken: ct);
            characterAnimator?.SetTrigger(Idle);
        }

        private void NotifyDistanceChanged() => OnDistanceChanged?.Invoke(playerIndex, currentDistance, _config.maxDistance);

        public bool HandleInput(int laneIdx, int padIdx)
        {
            if (IsStunned || _lanePositions == null || laneIdx < 0 || laneIdx >= _lanePositions.Length || (padIdx != 0 && padIdx != 1)) return false;
    
            // 밟은 레인의 이전 입력과 현재 입력을 비교
            if (_lastPadIdxByLane[laneIdx] == padIdx) return false;
    
            // 유효한 교차 입력이면 해당 레인의 마지막 입력 상태 갱신
            _lastPadIdxByLane[laneIdx] = padIdx;
            return true;
        }
        
        public void MoveAndAccelerate(int laneIdx)
        {
            MoveToLane(laneIdx);
            currentSpeed = Mathf.Min(currentSpeed + _config.runSpeedBoost, _config.maxScrollSpeed);
            if (!_config.useMetricDistance) AddDistance(1f);
        }
        
        public void AddDistance(float amount) { currentDistance += amount; NotifyDistanceChanged(); }
        public void ForceStop() { currentSpeed = 0f; characterAnimator?.SetFloat(RunSpeedParam, 0f); }
        public void SetCharacterSprite(Sprite sprite) { if (bodyImage) bodyImage.sprite = sprite; if (leftHandImage) leftHandImage.sprite = sprite; if (rightHandImage) rightHandImage.sprite = sprite; }
        public void SetCharacterColor(Color color) { if (bodyImage) bodyImage.color = new Color(color.r, color.g, color.b, bodyImage.color.a); if (leftHandImage) leftHandImage.color = new Color(color.r, color.g, color.b, leftHandImage.color.a); if (rightHandImage) rightHandImage.color = new Color(color.r, color.g, color.b, rightHandImage.color.a); }
        public Vector3 GetHandUIPosition(bool isRightHand) => (isRightHand ? rightHandTransform : leftHandTransform)?.position ?? transform.position;
    }
}