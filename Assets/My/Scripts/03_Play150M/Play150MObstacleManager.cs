using System.Collections.Generic;
using My.Scripts._02_PlayTutorial.Components;
using UnityEngine;

namespace My.Scripts._03_Play150M
{
    public class Play150MObstacleManager : MonoBehaviour
    {
        [Header("Obstacle Settings")]
        [SerializeField] private GameObject obstaclePrefab;
        [SerializeField] private int playerIndex; // 0 or 1

        [Header("Generation Settings")]
        [SerializeField] private float startGenDistance = 10f;
        [SerializeField] private float endGenDistance = 150f;

        [Header("Lane Settings")]
        [SerializeField] private float laneWidth = 1.5f;
        [SerializeField] private bool useZCompensatedLanes = true;

        [Header("Path Settings (Sync with FrameManager)")]
        public Vector3 pathStart = new Vector3(3.286f, -3.5f, 2.52f);
        public Vector3 pathEnd = new Vector3(23.278f, -3.5f, 17.57f);
        public float virtualDistStartToEnd = 10f;
        
        [Header("Sync Settings")]
        [SerializeField] private float uvLoopSize = 0.025f;
        [SerializeField] private float virtualMetersPerLoop = 5f;

        // 생성된 장애물 리스트
        private readonly List<GameObject> _spawnedObstacles = new List<GameObject>();
        
        private Vector3 _moveDirection; 
        private Vector3 _laneOffsetVector;
        private float _worldDistPerUV;

        private void Start()
        {
            InitializePathVectors();
            if (virtualDistStartToEnd <= 0) return;
            GenerateRandomObstacles();
        }

        private void InitializePathVectors()
        {
            if (virtualDistStartToEnd <= 0) return;

            Vector3 segmentVector = pathEnd - pathStart;
            _moveDirection = -segmentVector.normalized; // 역방향 이동

            Vector3 forwardDir = segmentVector.normalized;

            // 라인 간격 보정 벡터 계산
            if (useZCompensatedLanes)
            {
                Vector3 geomRight = Vector3.Cross(Vector3.up, forwardDir).normalized;
                float correctionFactor = 1.0f;
                if (Mathf.Abs(geomRight.x) > 0.001f) correctionFactor = 1.0f / Mathf.Abs(geomRight.x);
                _laneOffsetVector = Vector3.right * (laneWidth * correctionFactor);
            }
            else
            {
                Vector3 geomRight = Vector3.Cross(Vector3.up, forwardDir).normalized;
                _laneOffsetVector = geomRight * laneWidth;
            }

            // 스크롤 속도 동기화 계수
            float worldDistPerLoop = segmentVector.magnitude * (virtualMetersPerLoop / virtualDistStartToEnd);
            if (uvLoopSize > 0) _worldDistPerUV = worldDistPerLoop / uvLoopSize;
        }

        /// <summary>
        /// 10M 지점부터 150M 지점까지 난이도를 높여가며 장애물을 배치합니다.
        /// </summary>
        private void GenerateRandomObstacles()
        {
            float currentDist = startGenDistance;
            Vector3 vectorPerMeter = (pathEnd - pathStart) / virtualDistStartToEnd;

            while (currentDist < endGenDistance)
            {
                // 1. 난이도 결정 (거리별 구간)
                float interval;
                int count;

                if (currentDist < 50f) // [Easy] 10~50m
                {
                    // 간격 넓음, 무조건 1개
                    interval = Random.Range(10f, 15f);
                    count = 1;
                }
                else if (currentDist < 100f) // [Normal] 50~100m
                {
                    // 간격 보통, 가끔 2개
                    interval = Random.Range(7f, 10f);
                    count = (Random.value > 0.8f) ? 2 : 1; 
                }
                else // [Hard] 100~150m
                {
                    // 간격 좁음, 자주 2개
                    interval = Random.Range(5f, 7f);
                    count = (Random.value > 0.6f) ? 2 : 1;
                }

                // 다음 위치 설정
                currentDist += interval;
                if (currentDist >= endGenDistance) break;

                // 2. 장애물 배치 (라인 선택)
                // 라인 인덱스: -1(좌), 0(중), 1(우)
                List<int> availableLanes = new List<int> { -1, 0, 1 };
                
                // 랜덤으로 라인 섞기
                for (int i = 0; i < availableLanes.Count; i++)
                {
                    int rnd = Random.Range(i, availableLanes.Count);
                    (availableLanes[i], availableLanes[rnd]) = (availableLanes[rnd], availableLanes[i]);
                }

                // 결정된 개수만큼 배치
                for (int i = 0; i < count; i++)
                {
                    int laneIdx = availableLanes[i];
                    SpawnSingleObstacle(currentDist, laneIdx, vectorPerMeter);
                }
            }
        }

        private void SpawnSingleObstacle(float dist, int laneIdx, Vector3 vectorPerMeter)
        {
            if (obstaclePrefab == null) return;

            // 위치 계산: 시작점 + (진행방향 * 거리) + (라인오프셋 * 라인인덱스)
            Vector3 centerPos = pathStart + (vectorPerMeter * dist);
            Vector3 finalPos = centerPos + (_laneOffsetVector * laneIdx);

            GameObject obj = Instantiate(obstaclePrefab, transform);
            obj.transform.position = finalPos;
            
            // HitChecker 설정
            var hitChecker = obj.GetComponent<ObstacleHitChecker>();
            if (hitChecker == null) hitChecker = obj.AddComponent<ObstacleHitChecker>();
            hitChecker.Setup(playerIndex, laneIdx); // 플레이어 인덱스, 라인 인덱스 전달

            _spawnedObstacles.Add(obj);
        }

        public void ScrollObstacles(float uvSpeed)
        {
            if (_spawnedObstacles.Count == 0) return;

            float moveDistance = uvSpeed * _worldDistPerUV * Time.deltaTime;
            Vector3 displacement = _moveDirection * moveDistance;

            for (int i = _spawnedObstacles.Count - 1; i >= 0; i--)
            {
                if (_spawnedObstacles[i] != null)
                {
                    _spawnedObstacles[i].transform.position += displacement;
                    
                    // (옵션) 너무 뒤로 지나가면 제거 or 비활성화 가능
                    // 여기서는 150m 고정이므로 별도 제거 로직 없이 유지
                }
            }
        }
    }
}