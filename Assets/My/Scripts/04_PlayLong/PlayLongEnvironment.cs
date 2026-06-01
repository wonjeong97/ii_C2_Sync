using System.Threading;
using Cysharp.Threading.Tasks;
using Microsoft.Extensions.Logging;
using My.Scripts.Core;
using My.Scripts.Environment;
using UnityEngine;
using VContainer;
using ZLogger;

namespace My.Scripts._04_PlayLong
{
    /// <summary>
    /// PlayLong 씬의 환경 요소를 제어하는 매니저 클래스.
    /// VContainer 의존성 주입과 UniTask 기반의 비동기 제어를 지원합니다.
    /// </summary>
    public class PlayLongEnvironment : MonoBehaviour
    {
        [Header("Environment References")]
        [SerializeField] private TextureAdjuster mainFloor;
        [SerializeField] private PlayLongObstacleManager obstacleManager;
        [SerializeField] private PlayLongFrameManager frameManager;

        [Header("Scroll Settings")]
        [SerializeField] private float uvPerMeter = 0.0025f;
        [SerializeField] private float scrollSmoothing = 5.0f;

        [Header("Fog Settings")]
        [SerializeField] private bool useFog = true;
        [SerializeField] private Color fogColor = new Color(0.9f, 0.9f, 0.9f, 1f);
        [SerializeField] private float fogStartDistance = 10f;
        [SerializeField] private float fogEndDistance = 60f;

        private bool _prevFog;
        private Color _prevFogColor;
        private FogMode _prevFogMode;
        private float _prevFogStart;
        private float _prevFogEnd;

        private float _targetOffsetY;
        private float _currentOffsetY;
        private bool _isSmoothResetting;
        private CancellationTokenSource _cts;

        private ILogger<PlayLongEnvironment> _logger;

        [Inject]
        public void Construct(ILogger<PlayLongEnvironment> logger)
        {
            _logger = logger;
        }

        private void Start()
        {
            _cts = new CancellationTokenSource();
            InitEnvironment();
        }

        private void OnDestroy()
        {
            _cts?.Cancel();
            _cts?.Dispose();
        }

        public void InitEnvironment()
        {
            if (mainFloor)
            {
                mainFloor.enableScroll = false;
                mainFloor.scrollSpeedY = 0f;
                _targetOffsetY = mainFloor.offset.y;
                _currentOffsetY = _targetOffsetY;
            }
            else _logger?.ZLogWarning($"mainFloor 컴포넌트 누락됨.");

            Camera targetCam = Camera.main ?? GameObject.FindWithTag("MainCamera")?.GetComponent<Camera>();

            if (obstacleManager) obstacleManager.Init(targetCam, false);
            else _logger?.ZLogWarning($"obstacleManager 컴포넌트 누락됨.");

            if (frameManager) frameManager.Init();
            else _logger?.ZLogWarning($"frameManager 컴포넌트 누락됨.");

            BackupFogSettings();
            ApplyFogSettings();
        }

        public void ScrollByMeter(float meters)
        {
            if (mainFloor) _targetOffsetY += meters * uvPerMeter;
        }

        private void Update()
        {
            if (!mainFloor || _isSmoothResetting) return;

            float prevOffset = _currentOffsetY;
            _currentOffsetY = Mathf.Lerp(_currentOffsetY, _targetOffsetY, Time.deltaTime * scrollSmoothing);
    
            if (Mathf.Abs(_targetOffsetY - _currentOffsetY) < 0.0001f) _currentOffsetY = _targetOffsetY;

            ApplyOffset(_currentOffsetY);
            NotifyScrollToManagers(_currentOffsetY - prevOffset);
        }
        
        private void ApplyOffset(float offset)
        {
            if (!mainFloor) return;
            mainFloor.offset = new Vector2(mainFloor.offset.x, offset);
            mainFloor.UpdateUVs();
        }

        private void NotifyScrollToManagers(float uvDelta)
        {
            if (uvPerMeter <= 0.000001f) return;

            float movedMeters = uvDelta / uvPerMeter;
            if (obstacleManager) obstacleManager.ScrollObstacles(movedMeters);
            if (frameManager) frameManager.MoveFrames(movedMeters);
        }

        public async UniTask SmoothResetEnvironmentAsync(float duration, CancellationToken ct)
        {
            _isSmoothResetting = true;
            try
            {
                float elapsed = 0f;
                float startOffsetY = _currentOffsetY;

                while (elapsed < duration)
                {
                    float prevOffset = _currentOffsetY;
                    elapsed += Time.deltaTime;
            
                    float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
                    _currentOffsetY = Mathf.Lerp(startOffsetY, 0f, t);

                    ApplyOffset(_currentOffsetY);
                    NotifyScrollToManagers(_currentOffsetY - prevOffset);
            
                    await UniTask.Yield(PlayerLoopTiming.Update, ct);
                }
            }
            finally
            {
                ResetEnvironmentScroll();
                frameManager?.RebuildFramesFromZero();
                obstacleManager?.ResetObstacles();
                _isSmoothResetting = false;
            }
        }

        public void ResetEnvironmentScroll()
        {
            _targetOffsetY = 0f;
            _currentOffsetY = 0f;
            if (mainFloor)
            {
                mainFloor.offset = new Vector2(mainFloor.offset.x, 0f);
                mainFloor.UpdateUVs();
            }
        }

        public void ClearObstacles(float duration) => obstacleManager?.StopAndFadeOutObstacles(duration);

        private void BackupFogSettings()
        {
            _prevFog = RenderSettings.fog;
            _prevFogColor = RenderSettings.fogColor;
            _prevFogMode = RenderSettings.fogMode;
            _prevFogStart = RenderSettings.fogStartDistance;
            _prevFogEnd = RenderSettings.fogEndDistance;
        }

        private void ApplyFogSettings()
        {
            RenderSettings.fog = useFog;
            if (useFog)
            {
                RenderSettings.fogColor = fogColor;
                RenderSettings.fogMode = FogMode.Linear;
                RenderSettings.fogStartDistance = fogStartDistance;
                RenderSettings.fogEndDistance = fogEndDistance;
            }
        }

        private void OnDisable()
        {
            _isSmoothResetting = false;
            ResetEnvironmentScroll();
            RenderSettings.fog = _prevFog;
            RenderSettings.fogColor = _prevFogColor;
            RenderSettings.fogMode = _prevFogMode;
            RenderSettings.fogStartDistance = _prevFogStart;
            RenderSettings.fogEndDistance = _prevFogEnd;
        }
    }
}