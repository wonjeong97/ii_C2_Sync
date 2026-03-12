using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Wonjeong.UI;
using Wonjeong.Utils;

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

        [Header("Character Parts (For Color)")]
        [SerializeField] private Image bodyImage;
        [SerializeField] private Image leftHandImage;
        [SerializeField] private Image rightHandImage;

        [Header("Hand Anchors (Animation)")]
        [SerializeField] private Transform leftHandTransform;
        [SerializeField] private Transform rightHandTransform;

        [Header("Default Hand Offsets (UI Pixels)")]
        [SerializeField] private Vector2 leftHandDefaultOffset = new Vector2(-86f, -33f);
        [SerializeField] private Vector2 rightHandDefaultOffset = new Vector2(86f, -33f);

        [Header("Animation Settings")]
        [SerializeField] private float runSpeedMultiplier = 1.0f;

        [Header("Movement Settings")]
        [SerializeField] private float jumpArcHeight = 50f; 
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

        private readonly static int RunSpeedParam = Animator.StringToHash("RunSpeed");
        private readonly static int Finish = Animator.StringToHash("Finish");
        private readonly static int Jump = Animator.StringToHash("Jump");
        private readonly static int Idle = Animator.StringToHash("Idle");
        
        public Animator CharacterAnimator => characterAnimator;

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

            if (_lanePositions != null && _lanePositions.Length > 1)
            {
                currentLane = 1;
                if (characterUI) characterUI.anchoredPosition = _lanePositions[1];
            }
            
            NotifyDistanceChanged();
        }

        /// <summary> 캐릭터의 파츠(몸, 손)에 스프라이트 이미지를 직접 씌웁니다. </summary>
        public void SetCharacterSprite(Sprite sprite)
        {
            if (!sprite) return;

            if (bodyImage) bodyImage.sprite = sprite;
            if (leftHandImage) leftHandImage.sprite = sprite;
            if (rightHandImage) rightHandImage.sprite = sprite;
        }

        /// <summary>
        /// 스프라이트가 없을 경우를 대비한 틴트 색상 덮어쓰기 로직입니다.
        /// </summary>
        public void SetCharacterColor(Color color)
        {
            if (bodyImage) bodyImage.color = new Color(color.r, color.g, color.b, bodyImage.color.a);
            if (leftHandImage) leftHandImage.color = new Color(color.r, color.g, color.b, leftHandImage.color.a);
            if (rightHandImage) rightHandImage.color = new Color(color.r, color.g, color.b, rightHandImage.color.a);
        }

        public Vector3 GetHandUIPosition(bool isRightHand)
        {
            Transform target = isRightHand ? rightHandTransform : leftHandTransform;
            if (target) return target.position;

            Vector2 offset = isRightHand ? rightHandDefaultOffset : leftHandDefaultOffset;
            if (!characterUI) return transform.position;

            return characterUI.TransformPoint(offset);
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
            if (!characterAnimator) return;
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

        public void MoveToLane(int laneIdx)
        {
            if (laneIdx < 0 || laneIdx >= _lanePositions.Length) return;
            
            if (_moveCoroutine != null || currentLane == laneIdx) return;
        
            int laneDiff = Mathf.Max(1, Mathf.Abs(laneIdx - currentLane));
            currentLane = laneIdx;

            if (characterUI)
            {
                Vector2 startPos = characterUI.anchoredPosition;
                Vector2 targetPos = _lanePositions[laneIdx];
                
                float actualArcHeight = jumpArcHeight * laneDiff; 
                float actualDuration = jumpDuration * (1f + 0.3f * (laneDiff - 1));
                
                SoundManager.Instance?.PlaySFX("달리기_1");
                _moveCoroutine = StartCoroutine(MoveLaneRoutine(startPos, targetPos, actualDuration, actualArcHeight));
            }
        }

        private IEnumerator MoveLaneRoutine(Vector2 startPos, Vector2 targetPos, float duration, float arcHeight)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                Vector2 currentPos = Vector2.Lerp(startPos, targetPos, t);

                float heightOffset = Mathf.Sin(t * Mathf.PI) * arcHeight;
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
            if (characterAnimator)
            {
                characterAnimator.SetTrigger(Finish);
                characterAnimator.SetTrigger(Jump);
            }
        }

        public IEnumerator SetFinishRoutine()
        {
            if (characterAnimator)
            {
                characterAnimator.SetTrigger(Finish);
                characterAnimator.SetTrigger(Jump);
                yield return CoroutineData.GetWaitForSeconds(1.0f);
                characterAnimator.SetTrigger(Idle);
            }
        }
    }
}