using System.Collections;
using System.Collections.Generic;
using My.Scripts._02_PlayTutorial.Components;
using UnityEngine;

namespace My.Scripts._03_PlayShort
{
    /// <summary>
    /// PlayShort 씬에서 개별 플레이어의 장애물 생성, 이동, 풀링 및 소멸을 관리하는 클래스.
    /// </summary>
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
        private bool _isSpawningActive = false;

        /// <summary>
        /// 장애물 매니저 초기화 및 생성 루틴을 시작함.
        /// </summary>
        /// <param name="cam">타겟 카메라</param>
        public void Init(Camera cam)
        {
            if (!cam) Debug.LogWarning("Init 대상 카메라 컴포넌트 누락됨.");
            
            _targetCamera = cam;

            if (InitializePathVectors())
            {
                _virtualScrolledDistance = 0f;
                _nextSpawnTargetDist = startGenDistance;
                _isSpawningActive = true;
                CheckAndSpawnObstacles();
            }
        }

        /// <summary>
        /// 생성 및 이동 연산에 필요한 벡터와 비율을 사전 계산함.
        /// </summary>
        /// <returns>초기화 성공 여부</returns>
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
        /// 매 프레임 플레이어 상태 확인 및 장애물 자체 이동/생성/삭제를 처리함.
        /// </summary>
        private void Update()
        {
            if (!_isSpawningActive) return;

            if (PlayShortManager.Instance)
            {
                if (!PlayShortManager.Instance.IsGameStarted) return;
                
                // 이유: 플레이어가 정지하거나 스턴 상태일 때는 장애물도 함께 멈춤.
                if (PlayShortManager.Instance.IsPlayerPaused(playerIndex) || 
                    PlayShortManager.Instance.IsPlayerStunned(playerIndex)) 
                    return;
            }

            // # TODO: 반복적인 Clamp01, Lerp 연산 최적화를 위해 구간별 상태값 캐싱 고려.
            // 예시 입력: _virtualScrolledDistance(85) / 170 = 0.5 -> 결과 속도 = (2.0 + 6.0) / 2 = 4.0
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
        /// 외부(바닥 스크롤 등) 속도에 맞춰 장애물 이동 위치를 동기화함.
        /// </summary>
        /// <param name="uvSpeed">스크롤 UV 속도</param>
        public void ScrollObstacles(float uvSpeed)
        {
            if (!_isSpawningActive) return;

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
        /// 활성화된 모든 장애물을 지정된 거리만큼 이동시킴.
        /// </summary>
        /// <param name="moveDistance">이동 거리</param>
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
        /// 목표 거리에 도달 시 장애물을 스폰하고 다음 스폰 목표 거리를 갱신함.
        /// </summary>
        private void CheckAndSpawnObstacles()
        {
            if (virtualDistStartToEnd <= 0f) return;

            while (_virtualScrolledDistance + spawnAheadDistance >= _nextSpawnTargetDist)
            {
                SpawnForMilestone(_nextSpawnTargetDist);
                
                float interval;
                
                // 이유: 거리에 따라 장애물 출현 빈도를 점진적으로 높여 난이도를 상승시킴.
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
        /// 특정 목표 거리에서 배치할 장애물 개수와 레인을 결정하고 생성함.
        /// </summary>
        /// <param name="targetDist">생성 목표 거리</param>
        private void SpawnForMilestone(float targetDist)
        {
            int count = 1;
            
            // 이유: 거리가 멀어질수록 다중(2개) 장애물 스폰 확률을 높임.
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
        /// 지정된 위치와 레인에 단일 장애물을 생성 및 초기화함.
        /// </summary>
        /// <param name="centerPos">중앙 레인 기준 월드 좌표</param>
        /// <param name="laneIdx">배치할 레인 인덱스</param>
        private void SpawnSingleObstacle(Vector3 centerPos, int laneIdx)
        {
            if (!obstaclePrefab)
            {
                Debug.LogWarning("장애물 프리팹 누락됨.");
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
                
                fader.enabled = true;
                fader.ForceUpdateAlpha(); 
            }

            obj.SetActive(true); 
            _activeObstacles.Add(obj);
        }

        /// <summary>
        /// 플레이어 뒤로 지나간 장애물을 비활성화하고 풀로 반환함.
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
        /// 비활성화된 장애물을 풀에서 가져오거나 부족할 경우 새로 생성함.
        /// </summary>
        /// <returns>장애물 게임 오브젝트</returns>
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

        /// <summary>
        /// 장애물 생성을 멈추고 남은 장애물들을 서서히 페이드아웃 시켜 비활성화함.
        /// </summary>
        /// <param name="duration">페이드아웃 소요 시간</param>
        public void StopAndFadeOutObstacles(float duration)
        {
            if (!_isSpawningActive) return;
            
            _isSpawningActive = false;
            StartCoroutine(FadeOutRoutine(duration));
        }

        /// <summary>
        /// 활성화된 모든 장애물을 부드럽게 지우고 풀로 반환함.
        /// </summary>
        /// <param name="duration">진행 시간</param>
        /// <returns>IEnumerator 루틴</returns>
        private IEnumerator FadeOutRoutine(float duration)
        {
            List<GameObject> targets = new List<GameObject>(_activeObstacles);

            // 이유: 기존 Fader 컴포넌트가 Update 루프에서 알파값을 덮어쓰지 못하도록 사전에 비활성화함.
            foreach (GameObject obj in targets)
            {
                if (obj)
                {
                    FrameDistanceFader fader = obj.GetComponent<FrameDistanceFader>();
                    if (fader) fader.enabled = false;
                }
            }

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float alpha = Mathf.Lerp(1f, 0f, elapsed / duration);

                foreach (GameObject obj in targets)
                {
                    if (obj)
                    {
                        Renderer[] renderers = obj.GetComponentsInChildren<Renderer>();
                        foreach (Renderer r in renderers)
                        {
                            SetAlpha(r, alpha);
                        }
                    }
                }
                yield return null;
            }

            foreach (GameObject obj in targets)
            {
                if (obj)
                {
                    obj.SetActive(false);
                    if (_activeObstacles.Contains(obj))
                    {
                        _obstaclePool.Enqueue(obj);
                        _activeObstacles.Remove(obj);
                    }
                }
            }
        }

        /// <summary>
        /// 렌더러의 머티리얼 알파값을 일괄 조정함.
        /// </summary>
        /// <param name="r">대상 렌더러</param>
        /// <param name="alpha">적용할 알파값</param>
        private void SetAlpha(Renderer r, float alpha)
        {
            if (!r) return;
            
            if (r is SpriteRenderer sr)
            {
                sr.color = new Color(sr.color.r, sr.color.g, sr.color.b, alpha);
            }
            else if (r is MeshRenderer)
            {
                foreach (Material m in r.materials)
                {
                    if (m.HasProperty("_Color"))
                    {
                        m.color = new Color(m.color.r, m.color.g, m.color.b, alpha);
                    }
                    else if (m.HasProperty("_BaseColor"))
                    {
                        Color baseColor = m.GetColor("_BaseColor");
                        m.SetColor("_BaseColor", new Color(baseColor.r, baseColor.g, baseColor.b, alpha));
                    }
                }
            }
        }
    }
}