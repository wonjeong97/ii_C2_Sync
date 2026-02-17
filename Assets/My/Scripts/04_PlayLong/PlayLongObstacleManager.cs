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
        [Tooltip("장애물 생성 시작 거리 (미터)")]
        [SerializeField] private float startSpawnDistance = 10f;
        
        [Tooltip("장애물 생성 종료 거리 (미터)")]
        [SerializeField] private float maxSpawnDistance = 500f;

        [Tooltip("장애물 생성 간격 (미터)")]
        [SerializeField] private float spawnInterval = 10f;

        [Header("Lane Settings")]
        [Tooltip("라인 간격 (폭)")]
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

        /// <summary>
        /// 매니저 초기화. spawnRandom이 false면 초기 랜덤 장애물을 생성하지 않습니다.
        /// </summary>
        /// <param name="cam">거리 계산용 카메라</param>
        /// <param name="spawnRandom">즉시 랜덤 생성을 시작할지 여부</param>
        public void Init(Camera cam, bool spawnRandom = true) //
        {
            _targetCamera = cam;

            if (InitializePathVectors())
            {
                // 튜토리얼 단계 등에서는 생성을 건너뛸 수 있도록 조건부 호출
                if (spawnRandom)
                {
                    GenerateRandomObstacles();
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
        /// 실제 게임 시작 시 호출하여 랜덤 장애물 패턴을 생성합니다.
        /// </summary>
        public void GenerateRandomObstacles() //
        {
            float currentDist = startSpawnDistance;

            while (currentDist <= maxSpawnDistance)
            {
                int randomLane = Random.Range(-1, 2);
                SpawnSingleObstacle(currentDist, randomLane);
                currentDist += spawnInterval;
            }
        }

        public void SpawnSingleObstacle(float dist, int laneIdx)
        {
            if (obstaclePrefab == null) return;

            Vector3 pathDir = (pathEnd - pathStart).normalized;
            Vector3 centerPos = pathStart + (pathDir * (dist * _worldPerVirtualMeter));
            Vector3 finalPos = centerPos + (_laneOffsetVector * laneIdx);

            GameObject obj = Instantiate(obstaclePrefab, transform);
            obj.transform.position = finalPos;
    
            var hitChecker = obj.GetComponent<ObstacleHitChecker>();
            if (hitChecker == null) hitChecker = obj.AddComponent<ObstacleHitChecker>();
            
            hitChecker.Setup(-1, laneIdx); 

            if (useDistanceFade)
            {
                var fader = obj.AddComponent<FrameDistanceFader>();
                if (_targetCamera != null) fader.targetTransform = _targetCamera.transform;
                else if (Camera.main != null) fader.targetTransform = Camera.main.transform;
                
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

            for (int i = _spawnedObstacles.Count - 1; i >= 0; i--)
            {
                if (_spawnedObstacles[i] != null)
                {
                    _spawnedObstacles[i].transform.position += displacement;
                }
            }
        }
    }
}