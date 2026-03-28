using System.Collections;
using System.Collections.Generic;
using My.Scripts._02_PlayTutorial.Components;
using UnityEngine;

namespace My.Scripts._04_PlayLong
{
    /// <summary>
    /// PlayLong 씬에서 중앙(단일) 라인을 공유하는 장애물들의 생성, 이동, 풀링을 제어하는 매니저 클래스.
    /// </summary>
    public class PlayLongObstacleManager : MonoBehaviour
    {
        [Header("Obstacle Settings")]
        [SerializeField] private GameObject obstaclePrefab;

        [Header("Generation Settings")]
        [SerializeField] private float startSpawnDistance = 20f;
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

        private float _virtualScrolledDistance;
        private float _nextSpawnTargetDist = 20f;
        private bool _isSpawningActive;

        /// <summary>
        /// 초기 벡터 계산을 수행하고 조건에 따라 즉시 장애물 생성을 시작함.
        /// </summary>
        /// <param name="cam">타겟 카메라</param>
        /// <param name="spawnRandom">초기화 직후 스폰 루틴 활성화 여부</param>
        public void Init(Camera cam, bool spawnRandom = true)
        {
            if (!cam) Debug.LogWarning("타겟 카메라 컴포넌트 누락됨.");
            
            _targetCamera = cam;

            if (InitializePathVectors())
            {
                if (spawnRandom) GenerateProgressiveObstacles();
            }
            else
            {
                Debug.LogWarning("경로 벡터 초기화 실패.");
            }
        }

        /// <summary>
        /// 이동 연산에 필요한 경로 방향, 레인 오프셋, 스케일 비율 등을 사전 계산함.
        /// </summary>
        /// <returns>초기화 성공 여부</returns>
        private bool InitializePathVectors()
        {
            if (virtualDistStartToEnd <= 0f) return false;

            Vector3 segmentVector = pathEnd - pathStart;
            _forwardDir = segmentVector.normalized;
            _moveDirection = -_forwardDir; 
            
            // 예시 입력: segmentVector.magnitude(12.5) / virtualDistStartToEnd(10) -> 결과값 = 1.25 (가상 1m당 실제 월드 거리)
            _worldPerVirtualMeter = segmentVector.magnitude / virtualDistStartToEnd;

            Vector3 geomRight = Vector3.Cross(Vector3.up, _forwardDir).normalized;
            
            float correctionFactor = 1.0f;
            if (Mathf.Abs(geomRight.x) > 0.001f) correctionFactor = 1.0f / Mathf.Abs(geomRight.x);
            
            _laneOffsetVector = Vector3.right * (laneWidth * correctionFactor);

            return true;
        }

        /// <summary>
        /// 기존 장애물을 모두 풀로 반환하고 새로운 점진적 스폰 루틴을 시작함.
        /// </summary>
        public void GenerateProgressiveObstacles()
        {   
            ResetObstacles();
            _virtualScrolledDistance = 0f;
            _nextSpawnTargetDist = startSpawnDistance;
            _isSpawningActive = true;
            CheckAndSpawnObstacles();
        }
        
        /// <summary>
        /// 활성화된 모든 장애물을 강제로 비활성화하고 풀로 회수함.
        /// </summary>
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

        /// <summary>
        /// 매 프레임 장애물 자체 속도 이동 및 스폰 조건을 검사함.
        /// </summary>
        private void Update()
        {
            if (!_isSpawningActive) return;

            if (PlayLongManager.Instance)
            {
                if (!PlayLongManager.Instance.IsGameActive) return;
                
                // 이유: 플레이어가 기절(스턴) 상태일 때는 장애물 접근도 멈춰야 함.
                if (PlayLongManager.Instance.IsAnyPlayerStunned()) return;
            }

            // # TODO: Clamp01과 Lerp 연산이 매 프레임 발생하므로 최적화를 위해 목표 도달 시 캐싱하는 방안 고려 필요.
            // 예시 입력: progressRatio(0.5)일 때 min(3), max(8)의 보간 -> 결과 속도 = 5.5
            float progressRatio = Mathf.Clamp01(_virtualScrolledDistance / 500f);
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

        /// <summary>
        /// 특정 거리와 레인에 단일 장애물을 생성함. (외부 강제 스폰용)
        /// </summary>
        /// <param name="distMeters">생성 위치(가상 미터 거리)</param>
        /// <param name="laneIdx">배치할 레인 인덱스</param>
        public void SpawnSingleObstacle(float distMeters, int laneIdx)
        {
            if (!obstaclePrefab)
            {
                Debug.LogWarning("장애물 프리팹 누락됨.");
                return;
            }

            Vector3 centerPos = pathStart + (_forwardDir * (distMeters * _worldPerVirtualMeter));
            SpawnSingleObstacleFromPool(centerPos, laneIdx);
        }

        /// <summary>
        /// 풀에서 장애물을 가져와 위치를 설정하고 피격/페이드 컴포넌트를 초기화함.
        /// </summary>
        /// <param name="centerPos">기준 월드 좌표</param>
        /// <param name="laneIdx">레인 오프셋을 적용할 인덱스</param>
        private void SpawnSingleObstacleFromPool(Vector3 centerPos, int laneIdx)
        {
            Vector3 finalPos = centerPos + (_laneOffsetVector * laneIdx);

            GameObject obj = GetFromPool();
            obj.transform.position = finalPos;
    
            ObstacleHitChecker hitChecker = obj.GetComponent<ObstacleHitChecker>();
            if (!hitChecker) hitChecker = obj.AddComponent<ObstacleHitChecker>();
            
            // 이유: PlayLong 모드는 중앙 라인을 공유하므로 소유자 인덱스를 -1로 지정.
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
                
                fader.enabled = true;
                fader.ForceUpdateAlpha();
            }

            _activeObstacles.Add(obj);
        }

        /// <summary>
        /// 플레이어 스텝에 의한 외부 스크롤 거리만큼 장애물을 함께 이동시킴.
        /// </summary>
        /// <param name="meters">스크롤 이동 거리(가상 미터)</param>
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
        /// 강제 이벤트 연출 시 상태를 무시하고 지정된 거리만큼 장애물을 이동시킴.
        /// </summary>
        /// <param name="meters">이동할 거리(가상 미터)</param>
        public void ForceMoveActiveObstacles(float meters)
        {
            if (meters <= 0f) return;

            float moveDistanceWorld = meters * _worldPerVirtualMeter;
            MoveActiveObstacles(moveDistanceWorld);
        }

        /// <summary>
        /// 활성화된 모든 장애물 객체를 월드 이동 방향으로 실제 갱신함.
        /// </summary>
        /// <param name="moveDistanceWorld">실제 이동할 월드 거리</param>
        private void MoveActiveObstacles(float moveDistanceWorld)
        {
            Vector3 displacement = _moveDirection * moveDistanceWorld;

            for (int i = _activeObstacles.Count - 1; i >= 0; i--)
            {
                if (_activeObstacles[i])
                {
                    ObstacleHitChecker hitChecker = _activeObstacles[i].GetComponent<ObstacleHitChecker>();
                    
                    // 이유: 이미 피격 연출 중이거나 강제 정지 상태인 장애물은 이동하지 않음.
                    if (hitChecker && hitChecker.IsStopMove) continue;

                    _activeObstacles[i].transform.position += displacement;
                }
            }
        }

        /// <summary>
        /// 현재 진행 상황을 기반으로 스폰 목표 거리에 도달했는지 확인하고 생성 주기를 결정함.
        /// </summary>
        private void CheckAndSpawnObstacles()
        {
            while (_virtualScrolledDistance + spawnAheadDistance >= _nextSpawnTargetDist)
            {
                SpawnForMilestone(_nextSpawnTargetDist);
                
                float interval;
                
                // 이유: 중후반부 장애물이 너무 자주 등장한다는 피드백을 반영하여 150m 이후의 스폰 주기를 늘려 난이도를 완화함.
                if (_nextSpawnTargetDist < 150f) 
                {
                    interval = Random.Range(10f, 15f);
                }
                else if (_nextSpawnTargetDist < 350f) 
                {
                    // 기존 7~10m -> 12~16m 간격으로 대폭 완화
                    interval = Random.Range(12f, 16f);
                }
                else 
                {
                    // 기존 5~7m -> 10~14m 간격으로 대폭 완화
                    interval = Random.Range(10f, 14f);
                }

                _nextSpawnTargetDist += interval;
            }
        }

        /// <summary>
        /// 특정 목표 거리에서 배치할 장애물 개수와 레인을 결정하여 생성함.
        /// </summary>
        /// <param name="targetDist">생성 목표 거리</param>
        private void SpawnForMilestone(float targetDist)
        {
            int obstacleCount = 1;
            
            // 이유: 초반 난이도 상승 기획에 맞춰 다중(2개) 스폰이 발생할 확률을 각 구간별로 상향 조정함.
            if (targetDist >= 150f && targetDist < 350f) obstacleCount = (Random.value > 0.6f) ? 2 : 1;
            else if (targetDist >= 350f) obstacleCount = (Random.value > 0.4f) ? 2 : 1;

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

        /// <summary>
        /// 화면 뒤로 벗어난 장애물을 검사하여 비활성화 및 풀로 회수함.
        /// </summary>
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

        /// <summary>
        /// 비활성화된 장애물을 풀에서 꺼내오거나 새로 생성함.
        /// </summary>
        /// <returns>장애물 게임 오브젝트</returns>
        private GameObject GetFromPool()
        {
            if (_obstaclePool.Count > 0)
            {
                GameObject obj = _obstaclePool.Dequeue();
                obj.SetActive(true);
                return obj;
            }
            
            if (!obstaclePrefab)
            {
                Debug.LogWarning("프리팹 누락됨.");
                return null;
            }
            
            return Instantiate(obstaclePrefab, transform);
        }

        /// <summary>
        /// 장애물 생성을 중단하고 화면에 남은 장애물들을 페이드아웃 후 비활성화함.
        /// </summary>
        /// <param name="duration">페이드아웃에 걸리는 시간</param>
        public void StopAndFadeOutObstacles(float duration)
        {
            if (!_isSpawningActive) return;
            
            _isSpawningActive = false;
            StartCoroutine(FadeOutRoutine(duration));
        }

        /// <summary>
        /// 활성화된 모든 장애물을 부드럽게 투명화시키고 삭제(풀링)함.
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
        /// 단일 렌더러의 머티리얼 또는 스프라이트 컬러 알파값을 조절함.
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