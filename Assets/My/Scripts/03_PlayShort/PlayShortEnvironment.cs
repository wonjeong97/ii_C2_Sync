using Microsoft.Extensions.Logging;
using My.Scripts.Core;
using My.Scripts.Environment;
using UnityEngine;
using VContainer;
using ZLogger;

namespace My.Scripts._03_PlayShort
{
    /// <summary>
    /// PlayShort 씬의 배경 및 환경 요소(바닥, 프레임, 장애물)를 제어하는 매니저 클래스.
    /// 플레이어의 이동 속도에 맞춰 환경을 역방향으로 스크롤하여 달리는 연출을 구현함.
    /// </summary>
    public class PlayShortEnvironment : MonoBehaviour
    {
        [Header("Floor Settings")] 
        [SerializeField] private TextureAdjuster p1Floor;
        [SerializeField] private TextureAdjuster p2Floor;

        [Header("Frame Settings")] 
        [SerializeField] private FrameScrollManager p1Frames;
        [SerializeField] private FrameScrollManager p2Frames;

        [Header("Obstacle Settings")]
        [SerializeField] private PlayShortObstacleManager p1Obstacles; 
        [SerializeField] private PlayShortObstacleManager p2Obstacles;

        [Header("Cameras")] 
        [SerializeField] private Camera leftCamera;
        [SerializeField] private Camera rightCamera;

        [Header("Fog Settings")] 
        [SerializeField] private bool useFog = true;
        [SerializeField] private Color fogColor = new Color(0.1f, 0.1f, 0.1f, 1f); 
        [SerializeField] private FogMode fogMode = FogMode.Linear; 
        [SerializeField] private float fogStartDistance = 10f; 
        [SerializeField] private float fogEndDistance = 40f; 
        
        private bool _prevFog;
        private Color _prevFogColor;
        private FogMode _prevFogMode;
        private float _prevFogStartDistance;
        private float _prevFogEndDistance;
        private float _prevFogDensity;
        
        private bool _hasFogBackup;

        private ILogger<PlayShortEnvironment> _logger;
        private PlayShortManager _playShortManager;

        [Inject]
        public void Construct(ILogger<PlayShortEnvironment> logger, PlayShortManager playShortManager)
        {
            _logger = logger;
            _playShortManager = playShortManager;
        }

        public void InitEnvironment()
        {
            InitializeFloorComponent(p1Floor, "p1Floor");
            InitializeFloorComponent(p2Floor, "p2Floor");

            InitializeObstacleComponent(p1Obstacles, leftCamera, "p1Obstacles");
            InitializeObstacleComponent(p2Obstacles, rightCamera, "p2Obstacles");

            BackupAndApplyFogSettings();
        }

        private void InitializeFloorComponent(TextureAdjuster floor, string componentName)
        {
            if (floor)
            {
                floor.enableScroll = false;
                floor.scrollSpeedY = 0f;
            }
            else
            {
                _logger?.ZLogWarning($"{componentName} 컴포넌트 누락됨.");
            }
        }

        private void InitializeObstacleComponent(PlayShortObstacleManager obstacles, Camera targetCamera, string componentName)
        {
            if (obstacles && targetCamera)
            {
                // 주입받은 _playShortManager를 IPlayHitHandler로 전달
                obstacles.Init(targetCamera, _playShortManager);
            }
            else
            {
                _logger?.ZLogWarning($"{componentName} 또는 카메라 누락됨.");
            }
        }

        private void BackupAndApplyFogSettings()
        {
            _prevFog = RenderSettings.fog;
            _prevFogColor = RenderSettings.fogColor;
            _prevFogMode = RenderSettings.fogMode;
            _prevFogStartDistance = RenderSettings.fogStartDistance;
            _prevFogEndDistance = RenderSettings.fogEndDistance;
            _prevFogDensity = RenderSettings.fogDensity;
            _hasFogBackup = true;

            RenderSettings.fog = useFog;

            if (useFog)
            {
                RenderSettings.fogColor = fogColor;
                RenderSettings.fogMode = fogMode;

                if (fogMode == FogMode.Linear)
                {
                    RenderSettings.fogStartDistance = fogStartDistance;
                    RenderSettings.fogEndDistance = fogEndDistance;
                }
                else
                {
                    RenderSettings.fogDensity = 0.05f;
                }
            }
        }

        public void ScrollEnvironment(float p1Speed, float p2Speed)
        {
            float dt = Time.deltaTime;

            if (p1Floor) ApplyScrollToFloor(p1Floor, p1Speed * dt);
            if (p1Frames) p1Frames.ScrollFrames(p1Speed);
            if (p1Obstacles) p1Obstacles.ScrollObstacles(p1Speed);

            if (p2Floor) ApplyScrollToFloor(p2Floor, p2Speed * dt);
            if (p2Frames) p2Frames.ScrollFrames(p2Speed);
            if (p2Obstacles) p2Obstacles.ScrollObstacles(p2Speed);
        }

        private void ApplyScrollToFloor(TextureAdjuster floor, float uvDelta)
        {
            if (uvDelta == 0f) return;
            
            floor.offset.y += uvDelta;
            
            if (floor.useCustomLoop)
            {
                float loopSize = floor.loopMaxY - floor.loopMinY;
                if (loopSize > 0.0001f)
                {
                    float currentLength = floor.offset.y - floor.loopMinY;
                    floor.offset.y = floor.loopMinY + Mathf.Repeat(currentLength, loopSize);
                }
            }
            
            floor.UpdateUVs();
        }

        public void RecycleFrameClosestToCamera(int playerIdx)
        {
            if (playerIdx == 0)
                ExecuteRecycleFrames(p1Frames, leftCamera, p1Obstacles);
            else if (playerIdx == 1)
                ExecuteRecycleFrames(p2Frames, rightCamera, p2Obstacles);
        }

        private void ExecuteRecycleFrames(FrameScrollManager frames, Camera targetCamera, PlayShortObstacleManager obstacles)
        {
            if (frames && targetCamera)
            {
                frames.ForceRecycleFrameClosestToCamera(targetCamera.transform);
            }
            if (obstacles)
            {
                obstacles.ClearObstaclesNearPlayer();
            }
        }

        public void ClearObstaclesForPlayer(int playerIdx, float duration)
        {
            if (playerIdx == 0)
                ExecuteClearObstacles(p1Obstacles, duration, "p1Obstacles");
            else if (playerIdx == 1)
                ExecuteClearObstacles(p2Obstacles, duration, "p2Obstacles");
        }

        private void ExecuteClearObstacles(PlayShortObstacleManager obstacles, float duration, string componentName)
        {
            if (obstacles)
            {
                obstacles.StopAndFadeOutObstacles(duration);
            }
            else
            {
                _logger?.ZLogWarning($"{componentName} 누락됨.");
            }
        }

        private void OnDisable()
        {   
            if (!_hasFogBackup) return;
            
            RenderSettings.fog = _prevFog;
            RenderSettings.fogColor = _prevFogColor;
            RenderSettings.fogMode = _prevFogMode;
            RenderSettings.fogStartDistance = _prevFogStartDistance;
            RenderSettings.fogEndDistance = _prevFogEndDistance;
            RenderSettings.fogDensity = _prevFogDensity; 
        }
    }
}