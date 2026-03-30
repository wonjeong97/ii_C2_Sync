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

    /// <summary>
    /// 게임 내 플레이어의 이동 속도, 거리 누적, 레인 변경 애니메이션 및 발판 입력을 관리하는 컨트롤러 클래스.
    /// </summary>
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
        
        private Coroutine _stunCoroutine;
        private Coroutine _moveCoroutine;

        private int _lastPadIdx = -1;

        private readonly static int RunSpeedParam = Animator.StringToHash("RunSpeed");
        private readonly static int Finish = Animator.StringToHash("Finish");
        private readonly static int Jump = Animator.StringToHash("Jump");
        private readonly static int Idle = Animator.StringToHash("Idle");
        
        public Animator CharacterAnimator => characterAnimator;

        /// <summary>
        /// 플레이어의 초기 상태와 물리 설정값을 부여함.
        /// </summary>
        /// <param name="index">플레이어 인덱스</param>
        /// <param name="lanePositions">이동 가능한 레인 좌표 배열</param>
        /// <param name="config">이동 관련 물리 설정 데이터</param>
        public void Setup(int index, Vector2[] lanePositions, PlayerPhysicsConfig config)
        {
            playerIndex = index;
            _lanePositions = lanePositions;
            _config = config;

            currentSpeed = 0f;
            currentDistance = 0f;
            _lastPadIdx = -1; 
        
            if (!characterCanvasGroup)
            {
                if (characterUI)
                {
                    characterCanvasGroup = characterUI.GetComponent<CanvasGroup>();
                    if (!characterCanvasGroup)
                    {
                        // # TODO: 런타임 컴포넌트 추가 비용 절감을 위해 프리팹 단계에서 미리 할당 필요.
                        characterCanvasGroup = characterUI.gameObject.AddComponent<CanvasGroup>();
                    }
                }
                else
                {
                    Debug.LogWarning("characterUI 컴포넌트 누락됨.");
                }
            }

            if (_moveCoroutine != null) StopCoroutine(_moveCoroutine);
            _moveCoroutine = null;

            if (_lanePositions != null && _lanePositions.Length > 1)
            {
                currentLane = 1;
                if (characterUI) characterUI.anchoredPosition = _lanePositions[1];
            }
            else
            {
                Debug.LogWarning("lanePositions 배열 데이터 누락됨.");
            }
            
            NotifyDistanceChanged();
        }

        /// <summary>
        /// 캐릭터의 파츠 이미지에 직접 스프라이트를 적용함.
        /// </summary>
        /// <param name="sprite">적용할 스프라이트 에셋</param>
        public void SetCharacterSprite(Sprite sprite)
        {
            if (!sprite)
            {
                Debug.LogWarning("적용할 스프라이트 데이터 누락됨.");
                return;
            }

            if (bodyImage) bodyImage.sprite = sprite;
            if (leftHandImage) leftHandImage.sprite = sprite;
            if (rightHandImage) rightHandImage.sprite = sprite;
        }

        /// <summary>
        /// 스프라이트 에셋이 없을 경우 캐릭터 부위의 틴트 색상을 덮어씌움.
        /// </summary>
        /// <param name="color">적용할 색상 값</param>
        public void SetCharacterColor(Color color)
        {
            if (bodyImage) bodyImage.color = new Color(color.r, color.g, color.b, bodyImage.color.a);
            if (leftHandImage) leftHandImage.color = new Color(color.r, color.g, color.b, leftHandImage.color.a);
            if (rightHandImage) rightHandImage.color = new Color(color.r, color.g, color.b, rightHandImage.color.a);
        }

        /// <summary>
        /// 연결된 선 애니메이션을 위해 캐릭터 손의 UI 월드 좌표를 반환함.
        /// </summary>
        /// <param name="isRightHand">오른손 여부</param>
        /// <returns>손의 월드 좌표</returns>
        public Vector3 GetHandUIPosition(bool isRightHand)
        {
            Transform target = isRightHand ? rightHandTransform : leftHandTransform;
            if (target) 
            {
                return target.position;
            }
            else
            {
                // 이유: 예비값 대신 명시적 경고를 출력하여 데이터 누락을 인지하게 함.
                Debug.LogWarning("손 앵커 트랜스폼 누락됨.");
                return transform.position;
            }
        }

        /// <summary>
        /// 매 프레임 캐릭터의 이동 속도, 거리, 애니메이션 갱신을 처리함.
        /// </summary>
        /// <param name="isAutoRun">자동 달리기 상태 여부</param>
        /// <param name="autoRunTargetSpeed">목표 자동 속도</param>
        /// <param name="autoRunSmoothTime">자동 속도 보간 시간</param>
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
                // 예시 입력: currentSpeed(5) * Time.deltaTime(0.016) * metricMultiplier(100) -> 결과값 = 8 (가상 미터 증가량)
                float distanceDelta = currentSpeed * Time.deltaTime * _config.metricMultiplier;
                if (distanceDelta > 0)
                {
                    currentDistance += distanceDelta;
                    NotifyDistanceChanged();
                }
            }

            UpdateAnimationSpeed();
        }

        /// <summary>
        /// 현재 이동 속도에 비례하여 애니메이션 재생 속도를 동기화함.
        /// </summary>
        private void UpdateAnimationSpeed()
        {
            if (!characterAnimator) return;
            
            // 예시 입력: currentSpeed(5) / maxScrollSpeed(10) -> 결과값 = 0.5 (애니메이션 배율)
            float normalizedSpeed = (_config.maxScrollSpeed > 0) ? (currentSpeed / _config.maxScrollSpeed) : 0f;
            if (normalizedSpeed < 0.1f) normalizedSpeed = 0f;
            
            characterAnimator.SetFloat(RunSpeedParam, normalizedSpeed * runSpeedMultiplier);
        }

        /// <summary>
        /// 피격 시 지정된 시간 동안 기절(스턴) 루틴을 시작함.
        /// </summary>
        /// <param name="duration">기절 지속 시간</param>
        public void OnHit(float duration)
        {
            if (_stunCoroutine != null) StopCoroutine(_stunCoroutine);
            _stunCoroutine = StartCoroutine(StunRoutine(duration));
        }

        /// <summary>
        /// 기절 상태를 유지하며 캐릭터 알파값을 점멸시켜 피격 효과를 줌.
        /// </summary>
        /// <param name="duration">기절 지속 시간</param>
        /// <returns>IEnumerator 루틴</returns>
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
                    {
                        characterCanvasGroup.alpha = (characterCanvasGroup.alpha > 0.5f) ? 0.3f : 1.0f;
                    }
                }
                yield return null;
            }

            if (characterCanvasGroup) characterCanvasGroup.alpha = 1.0f;
            
            IsStunned = false;
            _stunCoroutine = null;
        }

        /// <summary>
        /// 입력된 발판이 유효한 연속 걷기 조작인지 판별함.
        /// </summary>
        /// <param name="laneIdx">이동할 레인 인덱스</param>
        /// <param name="padIdx">밟은 패드(좌/우) 인덱스</param>
        /// <returns>정상 조작 여부</returns>
        public bool HandleInput(int laneIdx, int padIdx)
        {
            if (IsStunned) return false; 
            if (_lanePositions == null || laneIdx < 0 || laneIdx >= _lanePositions.Length) return false;
            if (padIdx != 0 && padIdx != 1) return false;

            // 이유: 플레이어가 편법으로 한쪽 발판만 연속해서 밟는 것을 방지함.
            if (_lastPadIdx == padIdx) return false;

            _lastPadIdx = padIdx;
            return true;
        }

        /// <summary>
        /// 정상 입력 발생 시 목표 레인으로 이동시키고 전진 속도를 누적함.
        /// </summary>
        /// <param name="laneIdx">목표 레인 인덱스</param>
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

        /// <summary>
        /// 진행 거리를 임의로 증가시키고 이벤트를 호출함.
        /// </summary>
        /// <param name="amount">증가할 거리</param>
        public void AddDistance(float amount)
        {
            currentDistance += amount;
            NotifyDistanceChanged(); 
        }

        /// <summary>
        /// 거리 갱신 이벤트를 외부 매니저로 전달함.
        /// </summary>
        private void NotifyDistanceChanged()
        {
            if (OnDistanceChanged != null)
            {
                OnDistanceChanged.Invoke(playerIndex, currentDistance, _config.maxDistance);
            }
        }

        /// <summary>
        /// 지정된 레인으로 이동하는 궤적 코루틴을 실행함.
        /// </summary>
        /// <param name="laneIdx">목표 레인 인덱스</param>
        public void MoveToLane(int laneIdx)
        {
            if (_lanePositions == null || laneIdx < 0 || laneIdx >= _lanePositions.Length)
            {
                Debug.LogWarning("유효하지 않은 레인 인덱스 입력됨.");
                return;
            }
            
            if (_moveCoroutine != null || currentLane == laneIdx) return;
        
            int laneDiff = Mathf.Max(1, Mathf.Abs(laneIdx - currentLane));
            currentLane = laneIdx;

            if (characterUI)
            {
                Vector2 startPos = characterUI.anchoredPosition;
                Vector2 targetPos = _lanePositions[laneIdx];
                
                float actualArcHeight = jumpArcHeight * laneDiff; 
                float actualDuration = jumpDuration * (1f + 0.3f * (laneDiff - 1));
                
                if (SoundManager.Instance) SoundManager.Instance.PlaySFX("달리기_1");
                _moveCoroutine = StartCoroutine(MoveLaneRoutine(startPos, targetPos, actualDuration, actualArcHeight));
            }
        }

        /// <summary>
        /// 포물선을 그리며 목표 레인 UI 좌표로 이동하는 연출 루틴.
        /// </summary>
        /// <param name="startPos">시작 좌표</param>
        /// <param name="targetPos">목표 좌표</param>
        /// <param name="duration">이동 소요 시간</param>
        /// <param name="arcHeight">최대 도약 높이</param>
        /// <returns>IEnumerator 루틴</returns>
        private IEnumerator MoveLaneRoutine(Vector2 startPos, Vector2 targetPos, float duration, float arcHeight)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                Vector2 currentPos = Vector2.Lerp(startPos, targetPos, t);

                // 예시 입력: t(0.5) * PI -> Sin(PI/2)=1 * arcHeight(50) -> 결과값 = 50 (최고점 도달 높이)
                float heightOffset = Mathf.Sin(t * Mathf.PI) * arcHeight;
                currentPos.y += heightOffset;
                
                if (characterUI) characterUI.anchoredPosition = currentPos;
                yield return null;
            }
            
            if (characterUI) characterUI.anchoredPosition = targetPos;
            _moveCoroutine = null; 
        }
        
        /// <summary>
        /// 달리기 애니메이션과 이동 속도를 즉시 0으로 강제 정지함.
        /// </summary>
        public void ForceStop()
        {
            currentSpeed = 0f;
            if (characterAnimator) characterAnimator.SetFloat(RunSpeedParam, 0f);
        }
        
        /// <summary>
        /// 도달 완료 시 재생되는 환호 점프 애니메이션 트리거를 작동시킴.
        /// </summary>
        public void SetFinishAnimation()
        {
            if (characterAnimator)
            {
                characterAnimator.SetTrigger(Finish);
                characterAnimator.SetTrigger(Jump);
            }
        }

        /// <summary>
        /// 도달 애니메이션 재생 후 일정 시간 뒤 기본 상태로 복귀함.
        /// </summary>
        /// <returns>IEnumerator 루틴</returns>
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