using System.Collections;
using My.Scripts._02_PlayTutorial.Managers;
using My.Scripts._03_Play150M;
using UnityEngine;

namespace My.Scripts._02_PlayTutorial.Components
{
    /// <summary>
    /// 장애물 객체에 부착되어 플레이어와의 충돌을 감지하고 피격 로직을 처리하는 클래스.
    /// </summary>
    public class ObstacleHitChecker : MonoBehaviour
    {
        [Header("Settings")]
        [Tooltip("피격 후 파괴되기까지 대기 시간 (애니메이션 재생 시간)")]
        public float hitDuration = 2.0f;

        private int _ownerPlayerIdx; 
        private int _obstacleLaneIndex; 
        private bool _isHitProcessed; 
        private Animator _animator;

        private void Awake()
        {
            // 최상위 또는 자식 객체에 있는 애니메이터를 찾아 피격 애니메이션 재생을 준비함
            _animator = GetComponent<Animator>();
            if (_animator == null) _animator = GetComponentInChildren<Animator>();
        }

        /// <summary>
        /// 장애물 생성 시 매니저로부터 초기화 데이터를 주입받음.
        /// </summary>
        /// <param name="playerIdx">이 장애물이 타겟팅하는 플레이어 인덱스 (0 or 1)</param>
        /// <param name="laneIndex">장애물이 위치한 라인 인덱스 (-1: 좌, 0: 중, 1: 우)</param>
        public void Setup(int playerIdx, int laneIndex)
        {
            _ownerPlayerIdx = playerIdx;
            _obstacleLaneIndex = laneIndex;
        }

        private void OnTriggerEnter(Collider other)
        {   
            // 이미 충돌 처리가 끝난 장애물에 대해 중복 처리를 방지함
            if (_isHitProcessed) return;

            // 플레이어의 실제 판정 범위(Debug Cube 등)와 닿았을 때만 충돌 로직을 수행하기 위해 이름으로 필터링
            if (other.name.Contains("Left") || other.name.Contains("Center") || other.name.Contains("Right"))
            {   
                CheckHitLogic();
            }
        }

        /// <summary>
        /// 실제 플레이어 위치와 장애물 위치를 비교하여 최종 피격 여부를 판단함.
        /// 튜토리얼 및 150M 모드 양쪽을 지원함.
        /// </summary>
        private void CheckHitLogic()
        {
            _isHitProcessed = true;
            int playerCurrentLane = -999;
            bool isManagerFound = false;

            // 1. 튜토리얼 모드 확인
            if (PlayTutorialManager.Instance != null)
            {
                playerCurrentLane = PlayTutorialManager.Instance.GetCurrentLane(_ownerPlayerIdx);
                isManagerFound = true;
            }
            // 2. 150M 모드 확인 (수정됨: Play150MManager 사용)
            else if (Play150MManager.Instance != null)
            {
                playerCurrentLane = Play150MManager.Instance.GetCurrentLane(_ownerPlayerIdx);
                isManagerFound = true;
            }

            if (!isManagerFound) return;

            // 매니저의 라인 인덱스(0,1,2)와 장애물 생성 데이터의 인덱스(-1,0,1) 체계가 다르므로 변환하여 비교
            int obstacleLaneConverted = _obstacleLaneIndex + 1; 

            // 플레이어가 장애물과 같은 라인에 위치하고 있다면 피격으로 판정
            if (playerCurrentLane == obstacleLaneConverted)
            {
                // 시각적 피드백(애니메이션) 제공
                if (_animator != null) 
                {
                    _animator.SetTrigger("Hit"); 
                }

                // 게임 로직상 페널티 적용 (매니저 분기 처리)
                if (PlayTutorialManager.Instance != null)
                {
                    PlayTutorialManager.Instance.OnPlayerHit(_ownerPlayerIdx);
                }
                else if (Play150MManager.Instance != null)
                {
                    Play150MManager.Instance.OnPlayerHit(_ownerPlayerIdx);
                }

                // 즉시 삭제하지 않고 피격 애니메이션을 보여줄 시간을 확보한 뒤 제거함
                StartCoroutine(DestroyRoutine());
            }
        }

        /// <summary>
        /// 피격 연출 후 객체를 파괴하는 코루틴.
        /// </summary>
        private IEnumerator DestroyRoutine()
        {
            yield return new WaitForSeconds(hitDuration);
            Destroy(gameObject);
        }
    }
}