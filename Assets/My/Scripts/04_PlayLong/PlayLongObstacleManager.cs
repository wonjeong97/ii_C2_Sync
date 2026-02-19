using System.Collections.Generic;
using My.Scripts._02_PlayTutorial.Components;
using UnityEngine;

namespace My.Scripts._04_PlayLong
{
    public class PlayLongObstacleManager : MonoBehaviour
    {
        [Header("Obstacle Settings")]
        [SerializeField] private GameObject obstaclePrefab;

        [Header("Generation Settings")]
        [SerializeField] private float startSpawnDistance = 20f;
        [SerializeField] private float maxSpawnDistance = 500f;

        [Header("Lane Settings")]
        [SerializeField] private float laneWidth = 3f;

        [Header("Path Settings")]
        public Vector3 pathStart = new Vector3(0f, -1.5f, 1.534f);
        public Vector3 pathEnd = new Vector3(0f, -1.5f, 14.034f);
        public float virtualDistStartToEnd = 10f; 

        [Header("Fader Settings")]
        [SerializeField] private bool useDistanceFade = true;
        [SerializeField] private float fullyVisibleDist = 10f;
        [SerializeField] private float invisibleDist = 30f;

        private readonly List<GameObject> _spawnedObstacles = new List<GameObject>();
        private Vector3 _moveDirection; 
        private Vector3 _laneOffsetVector;
        private float _worldPerVirtualMeter; 
        private Camera _targetCamera; 

        public void Init(Camera cam, bool spawnRandom = true)
        {
            _targetCamera = cam;

            if (InitializePathVectors())
            {
                // 본 게임 시작 시 Manager에서 호출하도록 설계
                if (spawnRandom)
                {
                    GenerateProgressiveObstacles();
                }
            }
        }

        private bool InitializePathVectors()
        {
            if (virtualDistStartToEnd <= 0) return false;

            Vector3 segmentVector = pathEnd - pathStart;
            _moveDirection = -segmentVector.normalized; 
            _worldPerVirtualMeter = segmentVector.magnitude / virtualDistStartToEnd;

            Vector3 forwardDir = segmentVector.normalized;
            Vector3 geomRight = Vector3.Cross(Vector3.up, forwardDir).normalized;
            
            float correctionFactor = 1.0f;
            if (Mathf.Abs(geomRight.x) > 0.001f) correctionFactor = 1.0f / Mathf.Abs(geomRight.x);
            _laneOffsetVector = Vector3.right * (laneWidth * correctionFactor);

            return true;
        }

        /// <summary>
        /// 거리에 따라 난이도가 상승하는 장애물 생성 로직
        /// </summary>
        public void GenerateProgressiveObstacles()
        {
            float currentDist = startSpawnDistance;

            while (currentDist <= maxSpawnDistance)
            {
                float interval;
                int obstacleCount;

                // 1. 거리별 난이도 구간 설정
                if (currentDist < 150f) // [Easy] 20~150m
                {
                    interval = Random.Range(15f, 20f); // 넓은 간격
                    obstacleCount = 1;                 // 장애물 1개
                }
                else if (currentDist < 350f) // [Normal] 150~350m
                {
                    interval = Random.Range(10f, 15f); // 중간 간격
                    obstacleCount = (Random.value > 0.7f) ? 2 : 1; // 30% 확률로 2개
                }
                else // [Hard] 350~500m
                {
                    interval = Random.Range(7f, 10f);  // 좁은 간격 (빠른 대응 필요)
                    obstacleCount = (Random.value > 0.5f) ? 2 : 1; // 50% 확률로 2개
                }

                // 2. 장애물 배치 실행
                SpawnRandomLaneObstacles(currentDist, obstacleCount);
                
                currentDist += interval;
            }
        }

        private void SpawnRandomLaneObstacles(float dist, int count)
        {
            List<int> lanes = new List<int> { -1, 0, 1 };
            // 랜덤 셔플
            for (int i = 0; i < lanes.Count; i++)
            {
                int rnd = Random.Range(i, lanes.Count);
                (lanes[i], lanes[rnd]) = (lanes[rnd], lanes[i]);
            }

            // 결정된 개수만큼 배치
            for (int i = 0; i < count; i++)
            {
                SpawnSingleObstacle(dist, lanes[i]);
            }
        }

        public void SpawnSingleObstacle(float dist, int laneIdx)
        {
            if (!obstaclePrefab) return;

            Vector3 pathDir = (pathEnd - pathStart).normalized;
            Vector3 centerPos = pathStart + (pathDir * (dist * _worldPerVirtualMeter));
            Vector3 finalPos = centerPos + (_laneOffsetVector * laneIdx);

            GameObject obj = Instantiate(obstaclePrefab, transform);
            obj.transform.position = finalPos;
    
            var hitChecker = obj.GetComponent<ObstacleHitChecker>();
            if (!hitChecker) hitChecker = obj.AddComponent<ObstacleHitChecker>();
            
            // PlayLong 모드 전역 판정을 위해 -1 전달
            hitChecker.Setup(-1, laneIdx); 

            if (useDistanceFade)
            {
                var fader = obj.AddComponent<FrameDistanceFader>();
                if (_targetCamera) fader.targetTransform = _targetCamera.transform;
                else if (Camera.main) fader.targetTransform = Camera.main.transform;
                
                fader.fullyVisibleDist = fullyVisibleDist;
                fader.invisibleDist = invisibleDist;
            }

            _spawnedObstacles.Add(obj);
        }

        public void MoveObstacles(float meters)
        {
            if (_spawnedObstacles.Count == 0) return;

            float moveDistance = meters * _worldPerVirtualMeter;
            Vector3 displacement = _moveDirection * moveDistance;
            Vector3 forwardDir = (pathEnd - pathStart).normalized;

            // 리스트를 역순으로 순회하며 이동 및 파괴 처리
            for (int i = _spawnedObstacles.Count - 1; i >= 0; i--)
            {
                GameObject obj = _spawnedObstacles[i];
                if (!obj)
                {
                    _spawnedObstacles.RemoveAt(i);
                    continue;
                }

                // 1. 충돌 여부 확인: 이미 충돌하여 멈춰야 하는 장애물인지 체크
                var hitChecker = obj.GetComponent<ObstacleHitChecker>();
                if (hitChecker && hitChecker.IsStopMove) 
                {
                    // 부딪힌 장애물은 바닥 스크롤(displacement)을 적용하지 않고 그 자리에 고정
                    continue; 
                }

                // 2. 물리적 위치 이동: 부딪히지 않은 장애물들만 플레이어 쪽으로 이동
                obj.transform.position += displacement;

                // 3. 카메라 뒤(기준점 0M 보다 뒤)로 넘어갔는지 체크
                float distFromStart = Vector3.Dot(obj.transform.position - pathStart, forwardDir);

                // 기준점보다 약 5M 뒤로 가면 파괴 (여유 공간 확보)
                if (distFromStart < -5f * _worldPerVirtualMeter)
                {
                    Destroy(obj);
                    _spawnedObstacles.RemoveAt(i);
                }
            }
        }
    }
}