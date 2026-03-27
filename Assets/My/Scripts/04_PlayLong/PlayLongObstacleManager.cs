using System.Collections;
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
            while (_virtualScrolledDistance + spawnAheadDistance >= _nextSpawnTargetDist)
            {
                SpawnForMilestone(_nextSpawnTargetDist);
                
                float interval;
                
                // 이유: 중후반부 장애물이 너무 자주 등장한다는 피드백을 반영하여 150m 이후의 스폰 주기를 늘려 난이도를 완화함
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

        private void SpawnForMilestone(float targetDist)
        {
            int obstacleCount = 1;
            
            // 이유: 초반 난이도 상승 기획에 맞춰 다중(2개) 스폰이 발생할 확률을 각 구간별로 상향 조정함
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

        /// <summary>
        /// 장애물 생성을 중단하고 화면에 남은 장애물들을 페이드아웃 후 비활성화합니다.
        /// </summary>
        /// <param name="duration">페이드아웃에 걸리는 시간</param>
        public void StopAndFadeOutObstacles(float duration)
        {
            if (!_isSpawningActive) return;
            
            _isSpawningActive = false;
            StartCoroutine(FadeOutRoutine(duration));
        }

        private IEnumerator FadeOutRoutine(float duration)
        {
            List<GameObject> targets = new List<GameObject>(_activeObstacles);

            // 이유: 기존 Fader 컴포넌트가 Update 루프에서 알파값을 덮어쓰지 못하도록 사전에 비활성화함
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