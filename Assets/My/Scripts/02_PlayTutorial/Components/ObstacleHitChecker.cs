using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Microsoft.Extensions.Logging;
using UnityEngine;
using VContainer;
using ZLogger;

namespace My.Scripts._02_PlayTutorial.Components
{
    public interface IPlayHitHandler
    {
        void OnPlayerHit(int playerIdx);
        bool IsPlayerPaused(int playerIdx);
        int GetCurrentLane(int playerIdx);
    }

    public class ObstacleHitChecker : MonoBehaviour
    {
        private readonly static int HitTrigger = Animator.StringToHash("Hit");

        [Header("Settings")]
        [SerializeField] private float hitDuration = 2.0f;
        [SerializeField] private float hitRadius = 0.1f;

        private int _ownerPlayerIdx;
        private int _obstacleLaneIndex;
        private bool _isHitProcessed;
        private Animator _animator;
        private static float _lastSoundPlayTime = -1f;

        public bool IsStopMove { get; private set; }

        private Vector3 _prevPos;
        private bool _hasPrevPos;
        private readonly RaycastHit[] _ccdHits = new RaycastHit[8];
        private CancellationTokenSource _destroyCts;
        private IPlayHitHandler _hitHandler;
        private ILogger<ObstacleHitChecker> _logger;

        [Inject]
        public void Construct(ILogger<ObstacleHitChecker> logger)
        {
            _logger = logger;
        }

        private void Awake()
        {
            _destroyCts = new CancellationTokenSource();
            if (!TryGetComponent(out _animator))
            {
                _animator = GetComponentInChildren<Animator>();
            }
        }

        private void OnEnable()
        {
            _hasPrevPos = false;
            _isHitProcessed = false;
            IsStopMove = false;
        }

        private void OnDestroy()
        {
            _destroyCts?.Cancel();
            _destroyCts?.Dispose();
        }

        public void Setup(int playerIdx, int laneIndex, IPlayHitHandler handler)
        {
            _ownerPlayerIdx = playerIdx;
            _obstacleLaneIndex = laneIndex;
            _hitHandler = handler;
            _hasPrevPos = false;
        }

        private void Update()
        {
            if (_isHitProcessed || IsStopMove) return;

            if (!_hasPrevPos)
            {
                _prevPos = transform.position;
                _hasPrevPos = true;
                return;
            }

            Vector3 currentPos = transform.position;
            Vector3 dir = currentPos - _prevPos;
            float dist = dir.magnitude;

            if (dist > 0.001f)
            {
                int hitCount = Physics.SphereCastNonAlloc(
                    _prevPos, hitRadius, dir.normalized, _ccdHits, dist,
                    1 << gameObject.layer, QueryTriggerInteraction.Collide);

                for (int i = 0; i < hitCount; i++)
                {
                    Collider hitCol = _ccdHits[i].collider;
                    if (IsValidTarget(hitCol))
                    {
                        ProcessHit();
                        if (_isHitProcessed) break;
                    }
                }
            }
            _prevPos = currentPos;
        }

        private void OnTriggerEnter(Collider other) => TryProcessTrigger(other);
        private void OnTriggerStay(Collider other) => TryProcessTrigger(other);

        private void TryProcessTrigger(Collider other)
        {
            if (_isHitProcessed) return;
            if (IsValidTarget(other))
            {
                ProcessHit();
            }
        }

        private bool IsValidTarget(Collider col)
        {
            if (!col) return false;
            if (col.transform.IsChildOf(transform)) return false;

            return col.gameObject.layer == gameObject.layer;
        }

        private void ProcessHit()
        {
            if (_hitHandler == null) return;

            int convertedObstacleLane = _obstacleLaneIndex + 1;

            int p1Lane = _hitHandler.GetCurrentLane(0);
            int p2Lane = _hitHandler.GetCurrentLane(1);
    
            if (_ownerPlayerIdx == -1) 
            {
                bool hitP1 = p1Lane == convertedObstacleLane;
                bool hitP2 = p2Lane == convertedObstacleLane;
                bool hitRedString = convertedObstacleLane == 1 && ((p1Lane == 0 && p2Lane == 2) || (p1Lane == 2 && p2Lane == 0));

                if (hitP1 || hitP2 || hitRedString)
                {
                    _hitHandler.OnPlayerHit(-1);
                    FinalizeHit();
                }
            }
            else 
            {
                // 개인 장애물도 동일하게 수정
                int playerLane = _hitHandler.GetCurrentLane(_ownerPlayerIdx);
                if (playerLane == convertedObstacleLane)
                {
                    _hitHandler.OnPlayerHit(_ownerPlayerIdx);
                    FinalizeHit();
                }
            }
        }

        private void FinalizeHit()
        {
            _isHitProcessed = true;
            IsStopMove = true;

            if (_animator) _animator.SetTrigger(HitTrigger);

            if (Time.time - _lastSoundPlayTime > 0.1f)
            {
                _lastSoundPlayTime = Time.time;
            }

            DestroyTaskAsync(_destroyCts.Token).Forget();
        }

        private async UniTaskVoid DestroyTaskAsync(CancellationToken ct)
        {
            await UniTask.Delay(TimeSpan.FromSeconds(hitDuration), cancellationToken: ct);
            if (this && gameObject)
            {
                // Destroy(gameObject) 대신 풀링을 위해 오브젝트 비활성화
                gameObject.SetActive(false);
            }
        }
        
#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (_isHitProcessed || IsStopMove) return;

            // 장애물 이동 방향 시각화 (현재 위치에서 이전 위치로 향하는 벡터)
            Vector3 dir = (_hasPrevPos) ? (transform.position - _prevPos) : Vector3.zero;
            float dist = dir.magnitude;

            Gizmos.color = Color.yellow;
        
            // SphereCast의 두께(Radius)만큼 기즈모 구를 그림
            Gizmos.DrawWireSphere(transform.position, hitRadius);
        
            if (dist > 0.001f)
            {
                // 이동 경로를 따라 기둥 형태로 기즈모 표시
                Gizmos.DrawLine(_prevPos, transform.position);
                Gizmos.DrawWireSphere(_prevPos, hitRadius);
            }
        }
#endif
    }
}