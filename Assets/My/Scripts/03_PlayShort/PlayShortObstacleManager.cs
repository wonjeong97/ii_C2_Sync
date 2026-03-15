using System.Collections.Generic;
using My.Scripts._02_PlayTutorial.Components;
using UnityEngine;

namespace My.Scripts._03_PlayShort
{
    public class PlayShortObstacleManager : MonoBehaviour
    {
        [Header("Obstacle Settings")]
        [SerializeField] private GameObject obstaclePrefab;
        [SerializeField] private int playerIndex; 

        [Header("Generation Settings")]
        [SerializeField] private float startGenDistance = 10f;
        [Tooltip("플레이어 기준 몇 미터 앞에서 장애물을 스폰할지 결정")]
        [SerializeField] private float spawnAheadDistance = 30f; 
        [Tooltip("플레이어를 지나쳐 몇 미터 뒤로 가면 삭제(풀 반환)할지 결정")]
        [SerializeField] private float despawnBehindDistance = -5f;

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

        [Header("Fader Settings")]
        [SerializeField] private bool useDistanceFade = true;
        [SerializeField] private float fullyVisibleDist = 10f;
        [SerializeField] private float invisibleDist = 30f;

        [Header("Auto Movement Settings")]
        [Tooltip("진행도 0m일 때 장애물이 다가오는 기본 속도")]
        [SerializeField] private float minApproachSpeed = 2.0f; 
        [Tooltip("최고 속도")]
        [SerializeField] private float maxApproachSpeed = 6.0f; 

        private Queue<GameObject> _obstaclePool = new Queue<GameObject>();
        private List<GameObject> _activeObstacles = new List<GameObject>();
        
        private Vector3 _segmentVector;
        private Vector3 _moveDirection; 
        private Vector3 _laneOffsetVector;
        private float _worldDistPerUV;
        private Camera _targetCamera; 

        private float _virtualScrolledDistance = 0f;
        private float _nextSpawnTargetDist = 10f;

        /// <summary>
        /// 경로 벡터 및 스폰 설정을 초기화합니다.
        /// 이유: 씬 진입 시 카메라 타겟을 설정하고 장애물 스폰 준비를 위함.
        /// </summary>
        public void Init(Camera cam)
        {
            _targetCamera = cam;

            if (InitializePathVectors())
            {
                _virtualScrolledDistance = 0f;
                _nextSpawnTargetDist = startGenDistance;
                CheckAndSpawnObstacles();
            }
        }

        /// <summary>
        /// 경로 기반의 이동 방향 및 레인 오프셋을 계산합니다.
        /// 이유: 대각선 방향으로 배치된 환경에 맞춰 정확한 이동 벡터를 얻기 위함.
        /// </summary>
        private bool InitializePathVectors()
        {
            if (virtualDistStartToEnd <= 0f) return false;

            _segmentVector = pathEnd - pathStart;
            _moveDirection = -_segmentVector.normalized; 

            Vector3 forwardDir = _segmentVector.normalized;

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

            float worldDistPerLoop = _segmentVector.magnitude * (virtualMetersPerLoop / virtualDistStartToEnd);
            if (uvLoopSize > 0f) _worldDistPerUV = worldDistPerLoop / uvLoopSize;

            return true;
        }

        /// <summary>
        /// 매 프레임 장애물을 플레이어 방향으로 이동시키고 새로 스폰합니다.
        /// 이유: 플레이어가 진행함에 따라 난이도(속도)를 점진적으로 올리고 장애물을 무한 관리하기 위함.
        /// </summary>
        private void Update()
        {
            if (PlayShortManager.Instance)
            {
                if (!PlayShortManager.Instance.IsGameStarted) return;
                
                if (PlayShortManager.Instance.IsPlayerPaused(playerIndex) || 
                    PlayShortManager.Instance.IsPlayerStunned(playerIndex)) 
                    return;
            }

            // 요청에 따라 170m 부근에서 최고 속도에 도달하도록 설정
            float progressRatio = Mathf.Clamp01(_virtualScrolledDistance / 170f);
            float currentApproachSpeed = Mathf.Lerp(minApproachSpeed, maxApproachSpeed, progressRatio);

            if (currentApproachSpeed > 0f)
            {
                float moveDistance = currentApproachSpeed * Time.deltaTime;
                MoveActiveObstacles(moveDistance);
                
                float worldUnitsPerMeter = _segmentVector.magnitude / virtualDistStartToEnd;
                _virtualScrolledDistance += moveDistance / worldUnitsPerMeter;
            }

            CheckAndSpawnObstacles();
            CleanupObstacles();
        }

        /// <summary>
        /// 바닥 스크롤 연동 시 장애물도 함께 이동시킵니다.
        /// 이유: 플레이어 이동 거리와 장애물 이동 거리를 물리적으로 동기화하기 위함.
        /// </summary>
        public void ScrollObstacles(float uvSpeed)
        {
            if (PlayShortManager.Instance)
            {
                if (!PlayShortManager.Instance.IsGameStarted) return;
                
                if (PlayShortManager.Instance.IsPlayerPaused(playerIndex) || 
                    PlayShortManager.Instance.IsPlayerStunned(playerIndex)) 
                    return;
            }

            if (uvSpeed <= 0f) return;

            float moveDistance = uvSpeed * _worldDistPerUV * Time.deltaTime;
            if (moveDistance > 0f)
            {
                MoveActiveObstacles(moveDistance);
                
                float worldUnitsPerMeter = _segmentVector.magnitude / virtualDistStartToEnd;
                _virtualScrolledDistance += moveDistance / worldUnitsPerMeter;
                
                CheckAndSpawnObstacles();
                CleanupObstacles();
            }
        }

        /// <summary>
        /// 현재 활성화된 모든 장애물을 갱신된 거리만큼 이동시킵니다.
        /// 이유: 반복 연산을 줄이기 위한 헬퍼 메서드.
        /// </summary>
        private void MoveActiveObstacles(float moveDistance)
        {
            Vector3 displacement = _moveDirection * moveDistance;

            for (int i = _activeObstacles.Count - 1; i >= 0; i--)
            {
                if (_activeObstacles[i])
                {
                    _activeObstacles[i].transform.position += displacement;
                }
            }
        }

        /// <summary>
        /// 목표 거리에 도달할 때마다 간격을 좁히며 장애물을 스폰합니다.
        /// 이유: 플레이어가 이동하는 동안 한계치 제약 없이 무한정 장애물을 생성하기 위함.
        /// </summary>
        private void CheckAndSpawnObstacles()
        {
            if (virtualDistStartToEnd <= 0f) return;

            while (_virtualScrolledDistance + spawnAheadDistance >= _nextSpawnTargetDist)
            {
                SpawnForMilestone(_nextSpawnTargetDist);
                
                float interval;
                if (_nextSpawnTargetDist < 50f) 
                    interval = Random.Range(10f, 15f);
                else if (_nextSpawnTargetDist < 100f) 
                    interval = Random.Range(7f, 10f);
                else 
                    interval = Random.Range(5f, 7f);

                _nextSpawnTargetDist += interval;
            }
        }

        /// <summary>
        /// 특정 거리 구간에 장애물 군집(1~2개)을 무작위 레인에 생성합니다.
        /// 이유: 단조로움을 피하기 위해 다중 스폰 확률을 거리에 따라 조절.
        /// </summary>
        private void SpawnForMilestone(float targetDist)
        {
            int count = 1;
            if (targetDist >= 50f && targetDist < 100f) count = (Random.value > 0.8f) ? 2 : 1;
            else if (targetDist >= 100f) count = (Random.value > 0.6f) ? 2 : 1;

            List<int> availableLanes = new List<int> { -1, 0, 1 };
            
            for (int i = 0; i < availableLanes.Count; i++)
            {
                int rnd = Random.Range(i, availableLanes.Count);
                int temp = availableLanes[i];
                availableLanes[i] = availableLanes[rnd];
                availableLanes[rnd] = temp;
            }

            float metersAhead = targetDist - _virtualScrolledDistance; 
            Vector3 vectorPerMeter = _segmentVector / virtualDistStartToEnd; 
            Vector3 centerPos = pathStart + (vectorPerMeter * metersAhead);

            for (int i = 0; i < count; i++)
            {
                int laneIdx = availableLanes[i];
                SpawnSingleObstacle(centerPos, laneIdx);
            }
        }

        /// <summary>
        /// 단일 장애물을 풀에서 가져와 위치와 알파값을 초기화한 후 화면에 노출합니다.
        /// 이유: SetActive 전에 알파값을 갱신하여 렌더링 시 깜빡이는 현상(Flicker) 방지.
        /// </summary>
        private void SpawnSingleObstacle(Vector3 centerPos, int laneIdx)
        {
            if (!obstaclePrefab)
            {
                Debug.LogWarning("[PlayShortObstacleManager] obstaclePrefab 설정 누락으로 스폰 불가");
                return;
            }

            Vector3 finalPos = centerPos + (_laneOffsetVector * laneIdx);

            GameObject obj = GetFromPool();
            obj.transform.position = finalPos;
    
            ObstacleHitChecker hitChecker = obj.GetComponent<ObstacleHitChecker>();
            if (!hitChecker) hitChecker = obj.AddComponent<ObstacleHitChecker>();
            hitChecker.Setup(playerIndex, laneIdx);

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

            obj.SetActive(true); 
            _activeObstacles.Add(obj);
        }

        /// <summary>
        /// 화면(카메라) 뒤로 넘어간 장애물을 비활성화하고 풀에 반환합니다.
        /// 이유: 지속적인 Instantiate/Destroy 호출로 인한 메모리 파편화 및 성능 저하 방지.
        /// </summary>
        private void CleanupObstacles()
        {
            Vector3 forwardDir = _segmentVector.normalized;
            float worldUnitsPerMeter = _segmentVector.magnitude / virtualDistStartToEnd;
            float despawnWorldDist = despawnBehindDistance * worldUnitsPerMeter;

            for (int i = _activeObstacles.Count - 1; i >= 0; i--)
            {
                GameObject obj = _activeObstacles[i];
                if (!obj) continue;

                float distFromStart = Vector3.Dot(obj.transform.position - pathStart, forwardDir);
                
                if (distFromStart < despawnWorldDist)
                {
                    obj.SetActive(false);
                    _obstaclePool.Enqueue(obj);
                    _activeObstacles.RemoveAt(i);
                }
            }
        }

        /// <summary>
        /// 비활성 상태의 장애물 객체를 풀에서 꺼내 반환합니다.
        /// 이유: 활성화(SetActive)는 위치 및 Fader 세팅 이후에 해야만 반짝임을 막을 수 있으므로 꺼내기만 수행.
        /// </summary>
        private GameObject GetFromPool()
        {
            if (_obstaclePool.Count > 0)
            {
                GameObject obj = _obstaclePool.Dequeue();
                return obj;
            }
            
            GameObject newObj = Instantiate(obstaclePrefab, transform);
            newObj.SetActive(false); 
            return newObj;
        }
    }
}