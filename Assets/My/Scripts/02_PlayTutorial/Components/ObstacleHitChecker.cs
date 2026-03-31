using System.Collections;
using My.Scripts._02_PlayTutorial.Managers;
using My.Scripts._03_PlayShort;
using My.Scripts._04_PlayLong;
using UnityEngine;
using Wonjeong.Utils;

namespace My.Scripts._02_PlayTutorial.Components
{
    public class ObstacleHitChecker : MonoBehaviour
    {
        private readonly static int Hit = Animator.StringToHash("Hit");

        [Header("Settings")]
        public float hitDuration = 2.0f; 

        private int _ownerPlayerIdx; 
        private int _obstacleLaneIndex; 
        private bool _isHitProcessed; 
        private Animator _animator;
        private static float _lastSoundPlayTime = -1f;
        
        public bool IsStopMove { get; private set; }

        private Vector3 _prevPos; 
        private bool _hasPrevPos; 
        
        private readonly RaycastHit[] _ccdHits = new RaycastHit[8];

        /// <summary>
        /// 객체 생성 시 초기화.
        /// 애니메이터 컴포넌트를 캐싱함.
        /// </summary>
        private void Awake()
        {
            _animator = GetComponent<Animator>();
            if (!_animator) _animator = GetComponentInChildren<Animator>();
        }

        /// <summary>
        /// 객체 활성화 시 상태 초기화.
        /// </summary>
        private void OnEnable()
        {
            _hasPrevPos = false;
            _isHitProcessed = false;
            IsStopMove = false;
        }

        /// <summary>
        /// 장애물 소유자 및 레인 정보 설정.
        /// </summary>
        /// <param name="playerIdx">플레이어 인덱스</param>
        /// <param name="laneIndex">장애물 레인 인덱스</param>
        public void Setup(int playerIdx, int laneIndex)
        {
            _ownerPlayerIdx = playerIdx;
            _obstacleLaneIndex = laneIndex;
            // 이유: 풀링 후 위치가 강제로 이동되었을 때 첫 프레임 레이캐스트가 길게 뻗어나가는 것을 방지함.
            _hasPrevPos = false; 
        }

        /// <summary>
        /// 매 프레임 이동 거리를 기반으로 레이캐스트 충돌 검사 수행.
        /// </summary>
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
            // 예: current(0,0,1) - prev(0,0,0) = (0,0,1)
            Vector3 dir = currentPos - _prevPos;
            float dist = dir.magnitude;

            if (dist > 0.001f)
            {
                // # TODO: 레이캐스트 검사 빈도를 줄이거나 충돌 레이어를 세분화하여 연산 최적화 필요.
                int hitCount = Physics.RaycastNonAlloc(
                    _prevPos,
                    dir.normalized,
                    _ccdHits,
                    dist,
                    Physics.DefaultRaycastLayers,
                    QueryTriggerInteraction.Collide);
                    
                for (int i = 0; i < hitCount; i++)
                {   
                    RaycastHit hit = _ccdHits[i];
                    
                    if (IsValidTarget(hit.collider))
                    {
                        CheckHitLogic();
                        if (_isHitProcessed) break;
                    }
                }
            }

            _prevPos = currentPos;
        }

        /// <summary>
        /// 트리거 진입 시 충돌 처리.
        /// </summary>
        /// <param name="other">충돌한 콜라이더</param>
        private void OnTriggerEnter(Collider other)
        {   
            if (_isHitProcessed) return;
            if (IsValidTarget(other)) CheckHitLogic();
        }

        /// <summary>
        /// 트리거 머무름 시 충돌 처리.
        /// </summary>
        /// <param name="other">충돌한 콜라이더</param>
        private void OnTriggerStay(Collider other)
        {
            if (_isHitProcessed) return;
            if (IsValidTarget(other)) CheckHitLogic();
        }

        /// <summary>
        /// 충돌한 객체가 유효한 타겟인지 판별함.
        /// </summary>
        /// <param name="col">검사할 콜라이더</param>
        /// <returns>유효성 여부</returns>
        private bool IsValidTarget(Collider col)
        {
            if (!col) return false;

            // 자기 자신의 자식 콜라이더와 충돌하는 자가 당착 방지.
            if (col.transform.IsChildOf(transform)) return false;

            // 대각선 트랙에서 다중 스폰 시 다른 레인의 장애물을 판정선으로 오인하는 현상 완벽 차단.
            if (col.name.Contains("Obstacle")) return false;

            // 바닥 등 불필요한 물리 객체 연산 제외.
            if (!col.isTrigger && !col.CompareTag("Player")) return false;

            // 플레이어 본체와의 직접 충돌 허용.
            if (col.CompareTag("Player")) return true;

            // # TODO: 문자열 비교(Contains) 대신 LayerMask 비트 연산으로 판정선 검사 최적화 필요.
            string layerName = LayerMask.LayerToName(col.gameObject.layer);
            string objName = col.name;

            if (layerName.Contains("Left") || layerName.Contains("Center") || layerName.Contains("Right")) return true;
            if (objName.Contains("Left") || objName.Contains("Center") || objName.Contains("Right")) return true;

            return false;
        }

        /// <summary>
        /// 게임 모드별 피격 로직 처리 및 상태 업데이트.
        /// </summary>
        private void CheckHitLogic()
        {
            // PlayShort 모드에서 질문 팝업이 떠 있는 동안에는 레인 이동 시 장애물 충돌을 무시해야 함.
            if (PlayShortManager.Instance && PlayShortManager.Instance.IsPlayerPaused(_ownerPlayerIdx))
            {
                return;
            }

            // 예: _obstacleLaneIndex(-1) + 1 = 0 (Left Lane)
            int obstacleLaneConverted = _obstacleLaneIndex + 1; 
            bool isHit = false;

            if (PlayLongManager.Instance)
            {
                int p1Lane = PlayLongManager.Instance.GetCurrentLane(0); 
                int p2Lane = PlayLongManager.Instance.GetCurrentLane(1); 

                bool p1DirectHit = (p1Lane == obstacleLaneConverted);
                bool p2DirectHit = (p2Lane == obstacleLaneConverted);

                bool redStringHit = false;
                if (obstacleLaneConverted == 1)
                {
                    redStringHit = (p1Lane == 0 && p2Lane == 2) || (p1Lane == 2 && p2Lane == 0);
                }

                if (p1DirectHit || p2DirectHit || redStringHit)
                {
                    _isHitProcessed = true;
                    IsStopMove = true;
            
                    PlayLongManager.Instance.OnBothPlayersHit();
            
                    if (_animator) _animator.SetTrigger(Hit); 
                    StartCoroutine(DestroyRoutine());
                }
            }
            else if (PlayTutorialManager.Instance)
            {
                int playerCurrentLane = PlayTutorialManager.Instance.GetCurrentLane(_ownerPlayerIdx); 
                if (playerCurrentLane == obstacleLaneConverted)
                {
                    _isHitProcessed = true;
                    isHit = true;
                    PlayTutorialManager.Instance.OnPlayerHit(_ownerPlayerIdx);
                }
            }
            else if (PlayShortManager.Instance)
            {
                int playerCurrentLane = PlayShortManager.Instance.GetCurrentLane(_ownerPlayerIdx); 
                if (playerCurrentLane == obstacleLaneConverted)
                {
                    _isHitProcessed = true;
                    isHit = true;
                    PlayShortManager.Instance.OnPlayerHit(_ownerPlayerIdx);
                }
            }

            if (isHit)
            {       
                if (_animator) _animator.SetTrigger(Hit);
                if (Time.time - _lastSoundPlayTime > 0.1f)
                {
                    _lastSoundPlayTime = Time.time;
                }
                StartCoroutine(DestroyRoutine());
            }
        }

        /// <summary>
        /// 타격 연출 대기 후 객체 파괴.
        /// </summary>
        /// <returns>IEnumerator 루틴</returns>
        private IEnumerator DestroyRoutine()
        {
            yield return CoroutineData.GetWaitForSeconds(hitDuration);
            Destroy(gameObject);
        }
    }
}