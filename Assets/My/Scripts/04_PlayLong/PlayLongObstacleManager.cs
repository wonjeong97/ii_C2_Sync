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
        [Tooltip("플레이어 기준 몇 미터 앞에서 장애물을 스폰할지 결정")]
        [SerializeField] private float spawnAheadDistance = 40f; 
        [Tooltip("플레이어를 지나쳐 몇 미터 뒤로 가면 삭제(풀 반환)할지 결정")]
        [SerializeField] private float despawnBehindDistance = -5f;

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

        [Header("Auto Movement Settings")]
        [Tooltip("진행도 0m일 때 장애물이 다가오는 기본 속도")]
        [SerializeField] private float minApproachSpeed = 3.0f; 
        [Tooltip("진행도 500m(도착)일 때 장애물이 다가오는 최대 속도")]
        [SerializeField] private float maxApproachSpeed = 8.0f; 

        private Queue<GameObject> _obstaclePool = new Queue<GameObject>();
        private List<GameObject> _activeObstacles = new List<GameObject>();
        
        private Vector3 _moveDirection; 
        private Vector3 _laneOffsetVector;
        private Vector3 _forwardDir; 
        private float _worldPerVirtualMeter; 
        private Camera _targetCamera; 

        private float _virtualScrolledDistance = 0f;
        private float _nextSpawnTargetDist = 20f;
        private bool _isSpawningActive = false;

        public void Init(Camera cam, bool spawnRandom = true)
        {
            _targetCamera = cam;

            if (InitializePathVectors())
            {
                if (spawnRandom) GenerateProgressiveObstacles();
            }
        }

        private bool InitializePathVectors()
        {
            if (virtualDistStartToEnd <= 0f) return false;

            Vector3 segmentVector = pathEnd - pathStart;
            _forwardDir = segmentVector.normalized;
            _moveDirection = -_forwardDir; 
            _worldPerVirtualMeter = segmentVector.magnitude / virtualDistStartToEnd;

            Vector3 geomRight = Vector3.Cross(Vector3.up, _forwardDir).normalized;
            
            float correctionFactor = 1.0f;
            if (Mathf.Abs(geomRight.x) > 0.001f) correctionFactor = 1.0f / Mathf.Abs(geomRight.x);
            _laneOffsetVector = Vector3.right * (laneWidth * correctionFactor);

            return true;
        }

        public void GenerateProgressiveObstacles()
        {   
            ResetObstacles();
            _virtualScrolledDistance = 0f;
            _nextSpawnTargetDist = startSpawnDistance;
            _isSpawningActive = true;
            CheckAndSpawnObstacles();
        }
        
        public void ResetObstacles()
        {
            foreach (GameObject obj in _activeObstacles)
            {
                if (obj) 
                {
                    obj.SetActive(false);
                    _obstaclePool.Enqueue(obj);
                }
            }
            _activeObstacles.Clear();
            _isSpawningActive = false;
        }

        private void Update()
        {
            if (!_isSpawningActive) return;

            if (PlayLongManager.Instance)
            {
                if (!PlayLongManager.Instance.IsGameActive) return;
                if (PlayLongManager.Instance.IsAnyPlayerStunned()) return;
            }

            if (_activeObstacles.Count == 0 && _virtualScrolledDistance >= maxSpawnDistance) return;

            float progressRatio = Mathf.Clamp01(_virtualScrolledDistance / maxSpawnDistance);
            float currentApproachSpeed = Mathf.Lerp(minApproachSpeed, maxApproachSpeed, progressRatio);

            if (currentApproachSpeed > 0f)
            {
                float moveDistance = currentApproachSpeed * Time.deltaTime;
                MoveActiveObstacles(moveDistance);
                
                float metersMoved = moveDistance / _worldPerVirtualMeter;
                _virtualScrolledDistance += metersMoved;
            }

            CheckAndSpawnObstacles();
            CleanupObstacles();
        }

        public void SpawnSingleObstacle(float distMeters, int laneIdx)
        {
            if (!obstaclePrefab) return;

            Vector3 centerPos = pathStart + (_forwardDir * (distMeters * _worldPerVirtualMeter));
            SpawnSingleObstacleFromPool(centerPos, laneIdx);
        }

        private void SpawnSingleObstacleFromPool(Vector3 centerPos, int laneIdx)
        {
            Vector3 finalPos = centerPos + (_laneOffsetVector * laneIdx);

            GameObject obj = GetFromPool();
            obj.transform.position = finalPos;
    
            ObstacleHitChecker hitChecker = obj.GetComponent<ObstacleHitChecker>();
            if (!hitChecker) hitChecker = obj.AddComponent<ObstacleHitChecker>();
            
            hitChecker.Setup(-1, laneIdx); 

            if (useDistanceFade)
            {
                FrameDistanceFader fader = obj.GetComponent<FrameDistanceFader>();
                if (!fader) fader = obj.AddComponent<FrameDistanceFader>();
                
                if (_targetCamera) fader.targetTransform = _targetCamera.transform;
                else if (Camera.main) fader.targetTransform = Camera.main.transform;
                else fader.targetTransform = obj.transform;
                
                fader.fullyVisibleDist = fullyVisibleDist;
                fader.invisibleDist = invisibleDist;
                fader.ForceUpdateAlpha();
            }

            _activeObstacles.Add(obj);
        }

        public void MoveObstacles(float meters)
        {
            if (!_isSpawningActive) return;
            
            if (PlayLongManager.Instance)
            {
                if (!PlayLongManager.Instance.IsGameActive) return;
                if (PlayLongManager.Instance.IsAnyPlayerStunned()) return;
            }

            if (meters <= 0f) return;

            float moveDistanceWorld = meters * _worldPerVirtualMeter;
            MoveActiveObstacles(moveDistanceWorld);
            
            _virtualScrolledDistance += meters;
            
            CheckAndSpawnObstacles();
            CleanupObstacles();
        }

        /// <summary>
        /// 튜토리얼 연출 등 게임 시작 전 조건 검사를 무시하고 강제로 장애물을 플레이어에게 다가오게 할 때 사용합니다.
        /// </summary>
        public void ForceMoveActiveObstacles(float meters)
        {
            if (meters <= 0f) return;

            float moveDistanceWorld = meters * _worldPerVirtualMeter;
            MoveActiveObstacles(moveDistanceWorld);
        }

        private void MoveActiveObstacles(float moveDistanceWorld)
        {
            Vector3 displacement = _moveDirection * moveDistanceWorld;

            for (int i = _activeObstacles.Count - 1; i >= 0; i--)
            {
                if (_activeObstacles[i])
                {
                    ObstacleHitChecker hitChecker = _activeObstacles[i].GetComponent<ObstacleHitChecker>();
                    if (hitChecker && hitChecker.IsStopMove) continue;

                    _activeObstacles[i].transform.position += displacement;
                }
            }
        }

        private void CheckAndSpawnObstacles()
        {
            while (_virtualScrolledDistance + spawnAheadDistance >= _nextSpawnTargetDist && _nextSpawnTargetDist <= maxSpawnDistance)
            {
                SpawnForMilestone(_nextSpawnTargetDist);
                
                float interval;
                if (_nextSpawnTargetDist < 150f) interval = Random.Range(15f, 20f);
                else if (_nextSpawnTargetDist < 350f) interval = Random.Range(10f, 15f);
                else interval = Random.Range(7f, 10f);

                _nextSpawnTargetDist += interval;
            }
        }

        private void SpawnForMilestone(float targetDist)
        {
            int obstacleCount = 1;
            if (targetDist >= 150f && targetDist < 350f) obstacleCount = (Random.value > 0.7f) ? 2 : 1;
            else if (targetDist >= 350f) obstacleCount = (Random.value > 0.5f) ? 2 : 1;

            List<int> lanes = new List<int> { -1, 0, 1 };
            for (int i = 0; i < lanes.Count; i++)
            {
                int rnd = Random.Range(i, lanes.Count);
                int temp = lanes[i];
                lanes[i] = lanes[rnd];
                lanes[rnd] = temp;
            }

            float metersAhead = targetDist - _virtualScrolledDistance; 
            Vector3 centerPos = pathStart + (_forwardDir * (metersAhead * _worldPerVirtualMeter));

            for (int i = 0; i < obstacleCount; i++)
            {
                SpawnSingleObstacleFromPool(centerPos, lanes[i]);
            }
        }

        private void CleanupObstacles()
        {
            float despawnWorldDist = despawnBehindDistance * _worldPerVirtualMeter;

            for (int i = _activeObstacles.Count - 1; i >= 0; i--)
            {
                GameObject obj = _activeObstacles[i];
                if (!obj) continue;

                float distFromStart = Vector3.Dot(obj.transform.position - pathStart, _forwardDir);

                if (distFromStart < despawnWorldDist)
                {
                    obj.SetActive(false);
                    _obstaclePool.Enqueue(obj);
                    _activeObstacles.RemoveAt(i);
                }
            }
        }

        private GameObject GetFromPool()
        {
            if (_obstaclePool.Count > 0)
            {
                GameObject obj = _obstaclePool.Dequeue();
                obj.SetActive(true);
                return obj;
            }
            return Instantiate(obstaclePrefab, transform);
        }
    }
}