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
        
        public bool IsStopMove { get; private set; }

        private void Awake()
        {
            _animator = GetComponent<Animator>();
            if (_animator == null) _animator = GetComponentInChildren<Animator>();
        }

        public void Setup(int playerIdx, int laneIndex)
        {
            _ownerPlayerIdx = playerIdx;
            _obstacleLaneIndex = laneIndex;
        }

        private void OnTriggerEnter(Collider other)
        {   
            if (_isHitProcessed) return;
            
            // DebugTrackerVisualizer 큐브 등 판정 범위 확인
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

            if (PlayLongManager.Instance != null)
            {
                int p1Lane = PlayLongManager.Instance.GetCurrentLane(0); 
                int p2Lane = PlayLongManager.Instance.GetCurrentLane(1); 

                // [A] 직접 충돌: 통일된 인덱스(0, 1, 2)로 비교
                bool p1DirectHit = (p1Lane == obstacleLaneConverted);
                bool p2DirectHit = (p2Lane == obstacleLaneConverted);

                // [B] 붉은 실 충돌: 중앙 장애물(변환 후 1)일 때 양 끝(0과 2)으로 갈라졌는가
                bool redStringHit = false;
                if (obstacleLaneConverted == 1)
                {
                    redStringHit = (p1Lane == 0 && p2Lane == 2) || (p1Lane == 2 && p2Lane == 0);
                }

                if (p1DirectHit || p2DirectHit || redStringHit)
                {
                    _isHitProcessed = true;
                    IsStopMove = true; // ★ 충돌한 순간 이 장애물은 멈춤 상태로 전환
            
                    PlayLongManager.Instance.OnBothPlayersHit();
            
                    if (_animator != null) _animator.SetTrigger(Hit); 
                    StartCoroutine(DestroyRoutine());
                }
            }
            // 2. Tutorial 모드 (개별 판정)
            else if (PlayTutorialManager.Instance != null)
            {
                int playerCurrentLane = PlayTutorialManager.Instance.GetCurrentLane(_ownerPlayerIdx); //
                if (playerCurrentLane == obstacleLaneConverted)
                {
                    _isHitProcessed = true;
                    isHit = true;
                    PlayTutorialManager.Instance.OnPlayerHit(_ownerPlayerIdx); //
                }
            }
            // 3. PlayShort 모드 (개별 판정)
            else if (PlayShortManager.Instance != null)
            {
                int playerCurrentLane = PlayShortManager.Instance.GetCurrentLane(_ownerPlayerIdx); //
                if (playerCurrentLane == obstacleLaneConverted)
                {
                    _isHitProcessed = true;
                    isHit = true;
                    PlayShortManager.Instance.OnPlayerHit(_ownerPlayerIdx); //
                }
            }

            if (isHit)
            {
                if (_animator != null) _animator.SetTrigger("Hit"); 
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