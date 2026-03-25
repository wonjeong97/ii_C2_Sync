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

        // 연속 충돌 검사(CCD)를 위한 이전 위치 저장
        private Vector3 _prevPos;
        private bool _hasPrevPos;
        private readonly RaycastHit[] _ccdHits = new RaycastHit[8];

        private void Awake()
        {
            _animator = GetComponent<Animator>();
            if (_animator == null) _animator = GetComponentInChildren<Animator>();
        }

        // 이유: 오브젝트 풀링 환경에서 재사용될 때 이전 궤적 데이터가 남아 엉뚱한 위치에서 충돌하는 것을 방지함
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

            // 이유: 속도가 최고조일 때 장애물이 단 1프레임 만에 판정선을 건너뛰는 터널링을 막기 위해 궤적을 훑어 충돌을 검사함
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
                    if (hit.collider.name.Contains("Left") || hit.collider.name.Contains("Center") ||
                        hit.collider.name.Contains("Right") || hit.collider.CompareTag("Player"))
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
            
            if (other.name.Contains("Left") || other.name.Contains("Center") ||
                other.name.Contains("Right") || other.CompareTag("Player"))
            {   
                CheckHitLogic();
            }
        }

        private void OnTriggerStay(Collider other)
        {
            if (_isHitProcessed) return;

            if (other.name.Contains("Left") || other.name.Contains("Center") ||
                other.name.Contains("Right") || other.CompareTag("Player"))
            {   
                CheckHitLogic();
            }
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