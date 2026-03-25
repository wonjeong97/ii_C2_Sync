using System.Collections;
using My.Scripts._02_PlayTutorial.Managers;
using My.Scripts._03_PlayShort;
using My.Scripts._04_PlayLong;
using UnityEngine;
using Wonjeong.UI;
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

        private void Awake()
        {
            _animator = GetComponent<Animator>();
            if (!_animator) _animator = GetComponentInChildren<Animator>();
        }

        private void OnEnable()
        {
            _hasPrevPos = false;
            _isHitProcessed = false;
            IsStopMove = false;
        }

        public void Setup(int playerIdx, int laneIndex)
        {
            _ownerPlayerIdx = playerIdx;
            _obstacleLaneIndex = laneIndex;
            // 이유: 풀링 후 위치가 강제로 이동되었을 때 첫 프레임 레이캐스트가 길게 뻗어나가는 것을 방지함
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

        private void OnTriggerEnter(Collider other)
        {   
            if (_isHitProcessed) return;
            if (IsValidTarget(other)) CheckHitLogic();
        }

        private void OnTriggerStay(Collider other)
        {
            if (_isHitProcessed) return;
            if (IsValidTarget(other)) CheckHitLogic();
        }

        private bool IsValidTarget(Collider col)
        {
            if (!col) return false;

            // 1. 자기 자신의 부위(자식 콜라이더) 무조건 무시
            if (col.transform.IsChildOf(this.transform)) return false;

            // 2. 다른 장애물 무조건 무시
            // 이유: 대각선 트랙에서 다중 스폰 시, 다른 레인의 장애물 콜라이더를 레이저가 뚫고 지나갈 때 판정선으로 오인(팀킬)하는 것을 완벽 차단함
            if (col.name.Contains("Obstacle")) return false;

            // 3. 물리적인 바닥 무시 (오직 트리거 판정선과 플레이어 바디만 감지)
            if (!col.isTrigger && !col.CompareTag("Player")) return false;

            // 4. 플레이어 직접 충돌 허용
            if (col.CompareTag("Player")) return true;

            // 5. 판정선(가이드 큐브) 검사
            string layerName = LayerMask.LayerToName(col.gameObject.layer);
            string objName = col.name;

            if (layerName.Contains("Left") || layerName.Contains("Center") || layerName.Contains("Right")) return true;
            if (objName.Contains("Left") || objName.Contains("Center") || objName.Contains("Right")) return true;

            return false;
        }

        private void CheckHitLogic()
        {
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

        private IEnumerator DestroyRoutine()
        {
            yield return CoroutineData.GetWaitForSeconds(hitDuration);
            Destroy(gameObject);
        }
    }
}