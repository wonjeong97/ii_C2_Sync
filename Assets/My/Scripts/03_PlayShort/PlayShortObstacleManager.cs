using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Microsoft.Extensions.Logging;
using My.Scripts._02_PlayTutorial.Components;
using UnityEngine;
using VContainer;
using ZLogger;

namespace My.Scripts._03_PlayShort
{
    public class PlayShortObstacleManager : MonoBehaviour, IPlayHitHandler
    {
        private readonly static int ColorPropertyId = Shader.PropertyToID("_Color");
        private readonly static int BaseColorPropertyId = Shader.PropertyToID("_BaseColor");

        [Header("Obstacle Settings")]
        [SerializeField] private GameObject obstaclePrefab;
        [SerializeField] private int playerIndex;

        [Header("Generation Settings")]
        [SerializeField] private float startGenDistance = 10f;
        [SerializeField] private float spawnAheadDistance = 30f;
        [SerializeField] private float despawnBehindDistance = -5f;

        [Header("Lane Settings")]
        [SerializeField] private float laneWidth = 1.5f;
        [SerializeField] private bool useZCompensatedLanes = true;

        [Header("Path Settings")]
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
        [SerializeField] private float minApproachSpeed = 2.0f;
        [SerializeField] private float maxApproachSpeed = 6.0f;

        private readonly Queue<GameObject> _obstaclePool = new Queue<GameObject>();
        private readonly List<GameObject> _activeObstacles = new List<GameObject>();
        
        private Vector3 _segmentVector;
        private Vector3 _moveDirection;
        private Vector3 _laneOffsetVector;
        private float _worldDistPerUV;
        private Camera _targetCamera;

        private float _virtualScrolledDistance;
        private float _nextSpawnTargetDist = 10f;
        private bool _isSpawningActive;
        private CancellationTokenSource _fadeCts;

        private ILogger<PlayShortObstacleManager> _logger;
        private IPlayHitHandler _managerHandler;

        [Inject]
        public void Construct(ILogger<PlayShortObstacleManager> logger)
        {
            _logger = logger;
        }

        // 외부에서 매니저 핸들러 주입
        public void SetHitHandler(IPlayHitHandler handler) => _managerHandler = handler;

        private void OnDestroy()
        {
            _fadeCts?.Cancel();
            _fadeCts?.Dispose();
        }

        public void Init(Camera cam, IPlayHitHandler handler)
        {
            if (!cam)
            {
                _logger?.ZLogWarning($"Init 대상 카메라 컴포넌트 누락됨.");
                return;
            }
            
            _targetCamera = cam;
            _managerHandler = handler;

            if (InitializePathVectors())
            {
                _virtualScrolledDistance = 0f;
                _nextSpawnTargetDist = startGenDistance;
                _isSpawningActive = true;
                CheckAndSpawnObstacles();
            }
        }

        private bool InitializePathVectors()
        {
            if (virtualDistStartToEnd <= 0f) return false;

            _segmentVector = pathEnd - pathStart;
            _moveDirection = -_segmentVector.normalized;
            Vector3 forwardDir = _segmentVector.normalized;

            Vector3 geomRight = Vector3.Cross(Vector3.up, forwardDir).normalized;
            float correctionFactor = 1.0f;
            if (useZCompensatedLanes && Mathf.Abs(geomRight.x) > 0.001f)
                correctionFactor = 1.0f / Mathf.Abs(geomRight.x);
            
            _laneOffsetVector = (useZCompensatedLanes ? Vector3.right : geomRight) * (laneWidth * correctionFactor);

            float worldDistPerLoop = _segmentVector.magnitude * (virtualMetersPerLoop / virtualDistStartToEnd);
            if (uvLoopSize > 0f) _worldDistPerUV = worldDistPerLoop / uvLoopSize;

            return true;
        }

        private void Update()
        {
            if (!_isSpawningActive || _managerHandler == null) return;
            if (_managerHandler.IsPlayerPaused(playerIndex)) return;
            if (_managerHandler is PlayShortManager manager && !manager.IsGameStarted) return; // 게임이 시작된 후 장애물이 움직이도록 함.

            float progressRatio = Mathf.Clamp01(_virtualScrolledDistance / 170f);
            float currentApproachSpeed = Mathf.Lerp(minApproachSpeed, maxApproachSpeed, progressRatio);

            if (currentApproachSpeed > 0f)
            {
                float moveDistance = currentApproachSpeed * Time.deltaTime;
                MoveActiveObstacles(moveDistance);
                _virtualScrolledDistance += moveDistance / (_segmentVector.magnitude / virtualDistStartToEnd);
            }

            CheckAndSpawnObstacles();
            CleanupObstacles();
        }

        public void ScrollObstacles(float uvSpeed)
        {
            if (!_isSpawningActive || _managerHandler == null || _managerHandler.IsPlayerPaused(playerIndex)) return;
            if (uvSpeed <= 0f) return;

            float moveDistance = uvSpeed * _worldDistPerUV * Time.deltaTime;
            MoveActiveObstacles(moveDistance);
            _virtualScrolledDistance += moveDistance / (_segmentVector.magnitude / virtualDistStartToEnd);
            CheckAndSpawnObstacles();
            CleanupObstacles();
        }

        private void MoveActiveObstacles(float moveDistance)
        {
            Vector3 displacement = _moveDirection * moveDistance;
            for (int i = _activeObstacles.Count - 1; i >= 0; i--)
            {
                // 파괴된 오브젝트 예외 처리
                if (!_activeObstacles[i])
                {
                    _activeObstacles.RemoveAt(i);
                    continue;
                }

                // 장애물이 충돌하여 정지 상태(IsStopMove)가 되면 더 이상 이동하지 않고 그 자리에 멈춤
                if (_activeObstacles[i].TryGetComponent(out ObstacleHitChecker hit) && hit.IsStopMove)
                {
                    continue;
                }

                _activeObstacles[i].transform.position += displacement;
            }
        }

        private void CheckAndSpawnObstacles()
        {
            if (virtualDistStartToEnd <= 0f) return;
            while (_virtualScrolledDistance + spawnAheadDistance >= _nextSpawnTargetDist)
            {
                SpawnForMilestone(_nextSpawnTargetDist);
                _nextSpawnTargetDist += (_nextSpawnTargetDist < 50f) ? Random.Range(10f, 15f) : Random.Range(5f, 7f);
            }
        }

        private void SpawnForMilestone(float targetDist)
        {
            int count = (targetDist >= 50f) ? (Random.value > 0.7f ? 2 : 1) : 1;
            List<int> lanes = new List<int> { -1, 0, 1 };
            
            float metersAhead = targetDist - _virtualScrolledDistance;
            Vector3 centerPos = pathStart + (_segmentVector.normalized * (metersAhead * (_segmentVector.magnitude / virtualDistStartToEnd)));

            for (int i = 0; i < count; i++)
            {
                int idx = Random.Range(0, lanes.Count);
                SpawnSingleObstacle(centerPos, lanes[idx]);
                lanes.RemoveAt(idx);
            }
        }

        private void SpawnSingleObstacle(Vector3 centerPos, int laneIdx)
        {
            if (!obstaclePrefab) return;

            GameObject obj = GetFromPool();
            obj.transform.position = centerPos + (_laneOffsetVector * laneIdx);
    
            if (!obj.TryGetComponent<ObstacleHitChecker>(out var hitChecker))
                hitChecker = obj.AddComponent<ObstacleHitChecker>();
            
            // 핸들러 주입 (매니저 자신을 전달)
            hitChecker.Setup(playerIndex, laneIdx, this);

            if (useDistanceFade) ApplyDistanceFade(obj);

            obj.SetActive(true);
            _activeObstacles.Add(obj);
        }

        private void ApplyDistanceFade(GameObject obj)
        {
            if (!obj.TryGetComponent<FrameDistanceFader>(out var fader))
                fader = obj.AddComponent<FrameDistanceFader>();
    
            fader.targetTransform = _targetCamera?.transform ?? Camera.main?.transform ?? obj.transform;
            fader.fullyVisibleDist = fullyVisibleDist;
            fader.invisibleDist = invisibleDist;
            fader.ForceUpdateAlpha();
        }

        private void CleanupObstacles()
        {
            float limit = despawnBehindDistance * (_segmentVector.magnitude / virtualDistStartToEnd);
            for (int i = _activeObstacles.Count - 1; i >= 0; i--)
            {
                if (_activeObstacles[i] == null)
                {
                    _activeObstacles.RemoveAt(i);
                    continue;
                }

                // 충돌 후 스스로 SetActive(false)가 된 오브젝트를 발견하면 풀에 반환
                if (!_activeObstacles[i].activeSelf)
                {
                    _obstaclePool.Enqueue(_activeObstacles[i]);
                    _activeObstacles.RemoveAt(i);
                    continue;
                }

                // 기존 거리 기반 풀 반환 로직
                if (Vector3.Dot(_activeObstacles[i].transform.position - pathStart, _segmentVector.normalized) < limit)
                {
                    _activeObstacles[i].SetActive(false);
                    _obstaclePool.Enqueue(_activeObstacles[i]);
                    _activeObstacles.RemoveAt(i);
                }
            }
        }

        private GameObject GetFromPool() => _obstaclePool.Count > 0 ? _obstaclePool.Dequeue() : Instantiate(obstaclePrefab, transform);

        public void StopAndFadeOutObstacles(float duration)
        {
            if (!_isSpawningActive) return;
            _isSpawningActive = false;
            FadeOutAsync(duration, _fadeCts?.Token ?? CancellationToken.None).Forget();
        }

        private async UniTaskVoid FadeOutAsync(float duration, CancellationToken ct)
        {
            List<GameObject> targets = new List<GameObject>(_activeObstacles);
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float alpha = Mathf.Clamp01(1f - (elapsed / duration));
                foreach (GameObject obj in targets) 
                {
                    if (obj) SetAlphaRecursive(obj, alpha);
                }
                await UniTask.Yield(PlayerLoopTiming.Update, ct);
            }
            foreach (GameObject obj in targets) 
            { 
                if (obj) 
                {
                    obj.SetActive(false); 
                    _obstaclePool.Enqueue(obj); 
                }
            }
            _activeObstacles.Clear();
        }

        private void SetAlphaRecursive(GameObject obj, float alpha)
        {
            foreach (var r in obj.GetComponentsInChildren<Renderer>())
            {
                if (r is SpriteRenderer sr) sr.color = new Color(sr.color.r, sr.color.g, sr.color.b, alpha);
                else if (r is MeshRenderer mr) ApplyMeshMaterialAlpha(mr, alpha);
            }
        }

        private void ApplyMeshMaterialAlpha(Renderer r, float alpha)
        {
            foreach (var m in r.materials)
            {
                if (m.HasProperty(ColorPropertyId)) m.color = new Color(m.color.r, m.color.g, m.color.b, alpha);
                else if (m.HasProperty(BaseColorPropertyId)) 
                    m.SetColor(BaseColorPropertyId, new Color(m.GetColor(BaseColorPropertyId).r, m.GetColor(BaseColorPropertyId).g, m.GetColor(BaseColorPropertyId).b, alpha));
            }
        }

        public void OnPlayerHit(int playerIdx) => _managerHandler?.OnPlayerHit(playerIdx);
        public bool IsPlayerPaused(int playerIdx) => _managerHandler?.IsPlayerPaused(playerIdx) ?? false;
        public int GetCurrentLane(int playerIdx) => _managerHandler?.GetCurrentLane(playerIdx) ?? 0;
        
        public void ClearObstaclesNearPlayer()
        {
            float worldUnitsPerMeter = _segmentVector.magnitude / virtualDistStartToEnd;
            float minWorldDist = -2f * worldUnitsPerMeter;
            float maxWorldDist = 2f * worldUnitsPerMeter;

            for (int i = _activeObstacles.Count - 1; i >= 0; i--)
            {
                if (!_activeObstacles[i])
                {
                    _activeObstacles.RemoveAt(i);
                    continue;
                }

                float distFromStart = Vector3.Dot(_activeObstacles[i].transform.position - pathStart, _segmentVector.normalized);
        
                if (distFromStart >= minWorldDist && distFromStart <= maxWorldDist)
                {
                    _activeObstacles[i].SetActive(false);
                    _obstaclePool.Enqueue(_activeObstacles[i]);
                    _activeObstacles.RemoveAt(i);
                }
            }
        }
    }
}