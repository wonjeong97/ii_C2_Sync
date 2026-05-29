using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Microsoft.Extensions.Logging;
using My.Scripts._02_PlayTutorial.Components;
using UnityEngine;
using VContainer;
using ZLogger;

namespace My.Scripts._04_PlayLong
{
    public class PlayLongObstacleManager : MonoBehaviour
    {
        private readonly static int ColorPropertyId = Shader.PropertyToID("_Color");
        private readonly static int BaseColorPropertyId = Shader.PropertyToID("_BaseColor");

        [Header("Obstacle Settings")]
        [SerializeField] private GameObject obstaclePrefab;

        [Header("Generation Settings")]
        [SerializeField] private float startSpawnDistance = 20f;
        [SerializeField] private float spawnAheadDistance = 40f;
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
        [SerializeField] private float minApproachSpeed = 3.0f;
        [SerializeField] private float maxApproachSpeed = 8.0f;

        private readonly Queue<GameObject> _obstaclePool = new();
        private readonly List<GameObject> _activeObstacles = new();
        private readonly int[] _laneIndices = { -1, 0, 1 };

        private Vector3 _moveDirection;
        private Vector3 _laneOffsetVector;
        private Vector3 _forwardDir;
        private float _worldPerVirtualMeter;
        private Camera _targetCamera;

        private float _virtualScrolledDistance;
        private float _nextSpawnTargetDist = 20f;
        private bool _isSpawningActive;
        private CancellationTokenSource _cts;

        private ILogger<PlayLongObstacleManager> _logger;
        private PlayLongManager _playLongManager;

        [Inject]
        public void Construct(ILogger<PlayLongObstacleManager> logger, PlayLongManager playLongManager)
        {
            _logger = logger;
            _playLongManager = playLongManager;
        }

        private void OnDestroy()
        {
            _cts?.Cancel();
            _cts?.Dispose();
        }

        public void Init(Camera cam, bool spawnRandom = true)
        {
            _targetCamera = cam;
            _cts = new CancellationTokenSource();

            if (InitializePathVectors())
            {
                if (spawnRandom) GenerateProgressiveObstacles();
            }
            else
            {
                _logger?.ZLogWarning($"경로 벡터 초기화 실패.");
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
            float correctionFactor = Mathf.Abs(geomRight.x) > 0.001f ? 1.0f / Mathf.Abs(geomRight.x) : 1.0f;
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
        
        public void ScrollObstacles(float meters)
        {
            if (!_isSpawningActive || (_playLongManager && _playLongManager.IsAnyPlayerStunned())) return;
            if (meters <= 0f) return;

            MoveActiveObstacles(meters * _worldPerVirtualMeter);
            _virtualScrolledDistance += meters;
            CheckAndSpawnObstacles();
            CleanupObstacles();
        }
        
        public void ForceMoveActiveObstacles(float meters)
        {
            if (meters > 0f) MoveActiveObstacles(meters * _worldPerVirtualMeter);
        }

        // ==========================================
        // 누락되었던 팝업 이벤트용 강제 스폰 메서드 복구
        // ==========================================
        public void SpawnSingleObstacle(float distMeters, int laneIdx)
        {
            if (!obstaclePrefab) return;
            Vector3 centerPos = pathStart + (_forwardDir * (distMeters * _worldPerVirtualMeter));
            SpawnSingleObstacleFromPool(centerPos, laneIdx);
        }

        private void Update()
        {
            if (!_isSpawningActive || (_playLongManager && (!_playLongManager.IsGameActive || _playLongManager.IsAnyPlayerStunned())))
                return;

            float progressRatio = Mathf.Clamp01(_virtualScrolledDistance / 500f);
            float currentApproachSpeed = Mathf.Lerp(minApproachSpeed, maxApproachSpeed, progressRatio);

            if (currentApproachSpeed > 0f)
            {
                float moveDistance = currentApproachSpeed * Time.deltaTime;
                MoveActiveObstacles(moveDistance);
                _virtualScrolledDistance += (moveDistance / _worldPerVirtualMeter);
            }

            CheckAndSpawnObstacles();
            CleanupObstacles();
        }

        private void MoveActiveObstacles(float moveDistanceWorld)
        {
            Vector3 displacement = _moveDirection * moveDistanceWorld;
            for (int i = _activeObstacles.Count - 1; i >= 0; i--)
            {
                if (_activeObstacles[i] && _activeObstacles[i].TryGetComponent<ObstacleHitChecker>(out var hit) && !hit.IsStopMove)
                    _activeObstacles[i].transform.position += displacement;
            }
        }

        private void CheckAndSpawnObstacles()
        {
            while (_virtualScrolledDistance + spawnAheadDistance >= _nextSpawnTargetDist)
            {
                SpawnForMilestone(_nextSpawnTargetDist);
                _nextSpawnTargetDist += (_nextSpawnTargetDist < 150f) ? Random.Range(10f, 15f) : Random.Range(10f, 14f);
            }
        }

        private void SpawnForMilestone(float targetDist)
        {
            int obstacleCount = (targetDist >= 150f) ? (Random.value > 0.6f ? 2 : 1) : 1;
            
            // Fisher-Yates Shuffle
            for (int i = _laneIndices.Length - 1; i > 0; i--)
            {
                int rnd = Random.Range(0, i + 1);
                (_laneIndices[i], _laneIndices[rnd]) = (_laneIndices[rnd], _laneIndices[i]);
            }

            float metersAhead = targetDist - _virtualScrolledDistance;
            Vector3 centerPos = pathStart + (_forwardDir * (metersAhead * _worldPerVirtualMeter));

            for (int i = 0; i < obstacleCount; i++)
                SpawnSingleObstacleFromPool(centerPos, _laneIndices[i]);
        }

        /// <summary>
        /// 풀에서 장애물을 가져와 배치하고 관련 컴포넌트를 초기화함.
        /// </summary>
        private void SpawnSingleObstacleFromPool(Vector3 centerPos, int laneIdx)
        {
            GameObject obj = GetFromPool();
            obj.transform.position = centerPos + (_laneOffsetVector * laneIdx);

            if (!obj.TryGetComponent(out ObstacleHitChecker hitChecker))
            {
                hitChecker = obj.AddComponent<ObstacleHitChecker>();
            }
        
            hitChecker.enabled = true;

            if (obj.TryGetComponent(out Collider col))
            {
                col.enabled = true;
            }

            hitChecker.Setup(-1, laneIdx, _playLongManager);

            if (obj.TryGetComponent(out FrameDistanceFader fader))
            {
                fader.enabled = true;
            }

            if (useDistanceFade)
            {
                if (!fader) fader = obj.AddComponent<FrameDistanceFader>();
            
                Transform camTransform = _targetCamera ? _targetCamera.transform : (Camera.main ? Camera.main.transform : obj.transform);
                fader.targetTransform = camTransform;
                fader.fullyVisibleDist = fullyVisibleDist;
                fader.invisibleDist = invisibleDist;
                fader.ForceUpdateAlpha();
            }
            else
            {
                SetAlphaRecursive(obj, 1f);
            }
        
            _activeObstacles.Add(obj);
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

        private void CleanupObstacles()
        {
            float limit = despawnBehindDistance * _worldPerVirtualMeter;
            for (int i = _activeObstacles.Count - 1; i >= 0; i--)
            {
                if (!_activeObstacles[i])
                {
                    _activeObstacles.RemoveAt(i);
                    continue;
                }

                // 충돌 후 스스로 비활성화된 오브젝트를 발견하면 풀에 반환
                if (!_activeObstacles[i].activeSelf)
                {
                    _obstaclePool.Enqueue(_activeObstacles[i]);
                    _activeObstacles.RemoveAt(i);
                    continue;
                }

                // 기존 거리 기반 풀 반환 로직
                if (Vector3.Dot(_activeObstacles[i].transform.position - pathStart, _forwardDir) < limit)
                {
                    _activeObstacles[i].SetActive(false);
                    _obstaclePool.Enqueue(_activeObstacles[i]);
                    _activeObstacles.RemoveAt(i);
                }
            }
        }

        public void StopAndFadeOutObstacles(float duration)
        {
            if (!_isSpawningActive) return;
            _isSpawningActive = false;
            FadeOutAsync(duration, _cts.Token).Forget();
        }

        private async UniTask FadeOutAsync(float duration, CancellationToken ct)
        {
            List<GameObject> targets = new List<GameObject>(_activeObstacles);
            foreach (var obj in targets)
                if (obj && obj.TryGetComponent<FrameDistanceFader>(out var fader)) fader.enabled = false;

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float alpha = Mathf.Clamp01(1f - (elapsed / duration));
                foreach (var obj in targets) if (obj) SetAlphaRecursive(obj, alpha);
                await UniTask.Yield(PlayerLoopTiming.Update, ct);
            }

            foreach (var obj in targets)
            {
                if (obj) { obj.SetActive(false); _obstaclePool.Enqueue(obj); _activeObstacles.Remove(obj); }
            }
        }

        private void SetAlphaRecursive(GameObject obj, float alpha)
        {
            foreach (Renderer r in obj.GetComponentsInChildren<Renderer>())
            {
                if (r is SpriteRenderer sr) sr.color = new Color(sr.color.r, sr.color.g, sr.color.b, alpha);
                else if (r is MeshRenderer mr) ApplyMeshRendererAlpha(mr, alpha);
            }
        }

        private void ApplyMeshRendererAlpha(Renderer r, float alpha)
        {
            foreach (Material m in r.materials)
            {
                if (m.HasProperty(ColorPropertyId))
                    m.color = new Color(m.color.r, m.color.g, m.color.b, alpha);
                else if (m.HasProperty(BaseColorPropertyId))
                {
                    Color c = m.GetColor(BaseColorPropertyId);
                    m.SetColor(BaseColorPropertyId, new Color(c.r, c.g, c.b, alpha));
                }
            }
        }
        
        private async UniTaskVoid FadeOutSpecificObstaclesAsync(List<GameObject> targets, float duration, CancellationToken ct)
        {
            // 거리 기반 페이더 비활성화
            foreach (GameObject obj in targets)
                if (obj && obj.TryGetComponent<FrameDistanceFader>(out var fader)) fader.enabled = false;

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float alpha = Mathf.Clamp01(1f - (elapsed / duration));
                foreach (var obj in targets) if (obj) SetAlphaRecursive(obj, alpha);
                await UniTask.Yield(PlayerLoopTiming.Update, ct);
            }

            // 완전 투명해진 후 비활성화하고 오브젝트 풀로 반환
            foreach (GameObject obj in targets)
            {
                if (obj) 
                { 
                    obj.SetActive(false); 
                    _obstaclePool.Enqueue(obj); 
                }
            }
        }
        
        public void FadeOutAdjacentObstacles(float duration)
        {
            FadeOutAdjacentObstaclesTaskAsync(duration, _cts.Token).Forget();
        }

        /// <summary>
        /// 동시 스폰된 주변 장애물을 찾아 물리 충돌을 끄고 페이드아웃 처리함.
        /// </summary>
        private async UniTaskVoid FadeOutAdjacentObstaclesTaskAsync(float duration, CancellationToken ct)
        {
            await UniTask.Yield(PlayerLoopTiming.Update, ct);

            float hitDist = -1f;
            foreach (GameObject obj in _activeObstacles)
            {
                ObstacleHitChecker hit;
                if (obj && obj.TryGetComponent<ObstacleHitChecker>(out hit) && hit.IsStopMove)
                {
                    hitDist = Vector3.Dot(obj.transform.position - pathStart, _forwardDir);
                    break;
                }
            }

            if (hitDist < 0f) return;

            List<GameObject> siblingTargets = new List<GameObject>();
            for (int i = _activeObstacles.Count - 1; i >= 0; i--)
            {
                GameObject obj = _activeObstacles[i];
                if (obj && obj.TryGetComponent(out ObstacleHitChecker hitChecker) && !hitChecker.IsStopMove)
                {
                    float dist = Vector3.Dot(obj.transform.position - pathStart, _forwardDir);
                    if (Mathf.Abs(dist - hitDist) < 2.0f * _worldPerVirtualMeter)
                    {
                        hitChecker.enabled = false;
                    
                        // 투명화되는 장애물의 물리 충돌을 완전히 차단
                        Collider col;
                        if (obj.TryGetComponent<Collider>(out col))
                        {
                            col.enabled = false;
                        }

                        siblingTargets.Add(obj);
                        _activeObstacles.RemoveAt(i);
                    }
                }
            }

            if (siblingTargets.Count > 0)
            {
                FadeOutSpecificObstaclesAsync(siblingTargets, duration, ct).Forget();
            }
        }
    }
}