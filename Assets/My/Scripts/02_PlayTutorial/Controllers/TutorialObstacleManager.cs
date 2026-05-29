using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Microsoft.Extensions.Logging;
using My.Scripts._02_PlayTutorial.Components;
using My.Scripts._02_PlayTutorial.Managers;
using UnityEngine;
using VContainer;
using ZLogger;

namespace My.Scripts._02_PlayTutorial.Controllers
{
    [System.Serializable]
    public struct ObstacleSpawnData
    {
        public float distance;
        public int laneIndex;
    }

    /// <summary>
    /// 튜토리얼 장애물 관리 클래스. 
    /// IPlayHitHandler를 구현하여 ObstacleHitChecker에 자신의 핸들러 기능을 주입합니다.
    /// </summary>
    public class TutorialObstacleManager : MonoBehaviour, IPlayHitHandler
    {
        private readonly static int ColorPropertyId = Shader.PropertyToID("_Color");
        private readonly static int BaseColorPropertyId = Shader.PropertyToID("_BaseColor");

        [Header("Obstacle Settings")]
        [SerializeField] private GameObject obstaclePrefab;
        [SerializeField] private ObstacleSpawnData[] spawnData;
        [SerializeField] private float laneWidth = 1.5f;

        [Header("Path Settings")]
        public Vector3 pathStart = new Vector3(3.286f, -3.5f, 2.52f);
        public Vector3 pathEnd = new Vector3(23.278f, -3.5f, 17.57f);
        public float virtualDistStartToEnd = 10f;

        [Header("Sync Settings")]
        [SerializeField] private float uvLoopSize = 0.025f;
        [SerializeField] private float virtualMetersPerLoop = 5f;
        [SerializeField] private bool useZCompensatedLanes = true;

        public int playerIndex;

        private readonly List<GameObject> _spawnedObstacles = new List<GameObject>();
        private CancellationTokenSource _cts;
        private Vector3 _moveDirection;
        private Vector3 _laneOffsetVector;
        private float _worldDistPerUV;
        
        private ILogger<TutorialObstacleManager> _logger;
        private PlayTutorialManager _playTutorialManager;
        private IObjectResolver _resolver;

        [Inject]
        public void Construct(ILogger<TutorialObstacleManager> logger, PlayTutorialManager playTutorialManager, IObjectResolver resolver)
        {
            _logger = logger;
            _playTutorialManager = playTutorialManager;
            _resolver = resolver;
        }

        private void Start() => InitializeManager();

        private void OnDestroy()
        {
            _cts?.Cancel();
            _cts?.Dispose();
        }

        private void InitializeManager()
        {
            _cts = new CancellationTokenSource();
            if (virtualDistStartToEnd <= 0f) return;

            Vector3 segmentVector = pathEnd - pathStart;
            _moveDirection = -segmentVector.normalized;

            CalculateLaneOffset(segmentVector.normalized);

            float worldDistPerLoop = segmentVector.magnitude * (virtualMetersPerLoop / virtualDistStartToEnd);
            if (uvLoopSize > 0f) _worldDistPerUV = worldDistPerLoop / uvLoopSize;

            SpawnObstacles(segmentVector);
        }

        private void CalculateLaneOffset(Vector3 forwardDir)
        {
            Vector3 geomRight = Vector3.Cross(Vector3.up, forwardDir).normalized;
            float correctionFactor = 1.0f;
            if (useZCompensatedLanes && Mathf.Abs(geomRight.x) > 0.001f)
            {
                correctionFactor = 1.0f / Mathf.Abs(geomRight.x);
            }
            _laneOffsetVector = (useZCompensatedLanes ? Vector3.right : geomRight) * (laneWidth * correctionFactor);
        }

        private void SpawnObstacles(Vector3 segmentVector)
        {
            if (!obstaclePrefab || spawnData == null) return;

            Vector3 vectorPerMeter = segmentVector / virtualDistStartToEnd;

            foreach (ObstacleSpawnData data in spawnData)
            {
                Vector3 finalPos = pathStart + (vectorPerMeter * data.distance) + (_laneOffsetVector * data.laneIndex);
                GameObject obj = Instantiate(obstaclePrefab, transform);
                obj.transform.position = finalPos;

                _spawnedObstacles.Add(obj);

                if (!obj.TryGetComponent(out ObstacleHitChecker hitChecker))
                {
                    hitChecker = obj.AddComponent<ObstacleHitChecker>();
                }
                
                _resolver.Inject(hitChecker);
                hitChecker.Setup(playerIndex, data.laneIndex, this);
                SetAlphaRecursive(obj, 0f);
            }
        }

        public void ScrollObstacles(float uvSpeed)
        {
            if (_spawnedObstacles.Count == 0) return;

            float moveDistance = uvSpeed * _worldDistPerUV * Time.deltaTime;
            Vector3 displacement = _moveDirection * moveDistance;

            foreach (var obj in _spawnedObstacles)
            {
                if (obj) obj.transform.position += displacement;
            }
        }

        public void FadeInSpecificObstacles(float duration, int startIndex, int count)
        {
            List<Renderer> targetRenderers = new List<Renderer>();
            for (int i = startIndex; i < startIndex + count; i++)
            {
                if (i >= 0 && i < _spawnedObstacles.Count && _spawnedObstacles[i])
                {
                    targetRenderers.AddRange(_spawnedObstacles[i].GetComponentsInChildren<Renderer>());
                }
            }

            if (targetRenderers.Count > 0)
            {
                FadeTaskAsync(targetRenderers, 0f, 1f, duration, _cts.Token).Forget();
            }
        }

        private async UniTaskVoid FadeTaskAsync(List<Renderer> targets, float start, float end, float duration, CancellationToken ct)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                if (ct.IsCancellationRequested) return;

                elapsed += Time.deltaTime;
                float alpha = Mathf.Lerp(start, end, elapsed / duration);
                
                foreach (var r in targets) SetAlpha(r, alpha);
                await UniTask.Yield(PlayerLoopTiming.Update, ct);
            }
            
            foreach (var r in targets) SetAlpha(r, end);
        }

        private void SetAlphaRecursive(GameObject obj, float alpha)
        {
            foreach (var r in obj.GetComponentsInChildren<Renderer>())
            {
                SetAlpha(r, alpha);
            }
        }

        private void SetAlpha(Renderer r, float alpha)
        {
            if (!r) return;

            if (r is SpriteRenderer sr)
            {
                sr.color = new Color(sr.color.r, sr.color.g, sr.color.b, alpha);
            }
            else if (r is MeshRenderer mr)
            {
                ApplyMeshMaterialAlpha(mr, alpha);
            }
        }

        private void ApplyMeshMaterialAlpha(Renderer r, float alpha)
        {
            foreach (Material m in r.materials)
            {
                if (!m) continue;

                if (m.HasProperty(ColorPropertyId))
                {
                    m.color = new Color(m.color.r, m.color.g, m.color.b, alpha);
                }
                else if (m.HasProperty(BaseColorPropertyId))
                {
                    Color baseColor = m.GetColor(BaseColorPropertyId);
                    m.SetColor(BaseColorPropertyId, new Color(baseColor.r, baseColor.g, baseColor.b, alpha));
                }
            }
        }

        // IPlayHitHandler 구현체
        public void OnPlayerHit(int playerIdx) 
        {
            _playTutorialManager?.OnPlayerHit(playerIdx);
        }
        
        public bool IsPlayerPaused(int playerIdx) 
        {
            return _playTutorialManager?.IsPlayerStunned(playerIdx) ?? false;
        }

        public int GetCurrentLane(int playerIdx) 
        {
            return _playTutorialManager?.GetCurrentLane(playerIdx) ?? 1;
        }
    }
}