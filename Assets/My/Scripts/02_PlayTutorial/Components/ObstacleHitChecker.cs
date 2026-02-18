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
        [Header("Settings")]
        public float hitDuration = 2.0f;

        private int _ownerPlayerIdx; 
        private int _obstacleLaneIndex; 
        private bool _isHitProcessed; 
        private Animator _animator;

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
            int obstacleLaneConverted = _obstacleLaneIndex; 
            bool isHit = false;

            // 1. PlayLong 모드 (통합 장애물 판정)
            if (PlayLongManager.Instance != null)
            {
                int p1Lane = PlayLongManager.Instance.GetCurrentLane(0); //
                int p2Lane = PlayLongManager.Instance.GetCurrentLane(1); //

                // [A] 직접 충돌: 누구든 해당 라인에 있는가
                bool p1DirectHit = (p1Lane == obstacleLaneConverted);
                bool p2DirectHit = (p2Lane == obstacleLaneConverted);

                // [B] 붉은 실 충돌: 중앙 장애물일 때 양 끝으로 갈라졌는가
                bool redStringHit = false;
                if (obstacleLaneConverted == 1)
                {
                    redStringHit = (p1Lane == 0 && p2Lane == 2) || (p1Lane == 2 && p2Lane == 0); //
                }

                // 한 명이라도 맞았거나 실에 걸렸다면 판정 성공
                if (p1DirectHit || p2DirectHit || redStringHit)
                {
                    _isHitProcessed = true;
                    isHit = true;
                    
                    PlayLongManager.Instance.OnBothPlayersHit();
                    
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    Debug.Log($"Red String/Obstacle Hit! P1:{p1Lane}, P2:{p2Lane}, Obstacle:{obstacleLaneConverted}");
#endif
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