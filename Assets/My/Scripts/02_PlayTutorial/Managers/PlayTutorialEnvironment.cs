using Microsoft.Extensions.Logging;
using My.Scripts._02_PlayTutorial.Controllers;
using My.Scripts.Environment;
using UnityEngine;
using VContainer;
using ZLogger;

namespace My.Scripts._02_PlayTutorial.Managers
{
    /// <summary>
    /// 튜토리얼 씬의 배경 및 환경 요소(바닥, 프레임, 장애물)를 제어하는 매니저 클래스.
    /// 플레이어의 이동 속도에 맞춰 환경을 역방향으로 스크롤하여 달리는 연출을 구현함.
    /// </summary>
    public class PlayTutorialEnvironment : MonoBehaviour
    {
        [Header("Floor Settings")] 
        [SerializeField] private TextureAdjuster p1Floor;
        [SerializeField] private TextureAdjuster p2Floor;

        [Header("Frame Settings")] 
        [SerializeField] private FrameScrollManager p1Frames;
        [SerializeField] private FrameScrollManager p2Frames;

        [Header("Obstacle Settings")]
        [SerializeField] private TutorialObstacleManager p1Obstacles;
        [SerializeField] private TutorialObstacleManager p2Obstacles;

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

        private ILogger<PlayTutorialEnvironment> _logger;

        [Inject]
        public void Construct(ILogger<PlayTutorialEnvironment> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// 환경 요소 및 안개 설정을 초기화함.
        /// </summary>
        public void InitEnvironment()
        {
            InitializeFloorComponent(p1Floor, "p1Floor");
            InitializeFloorComponent(p2Floor, "p2Floor");
    
            BackupAndApplyFogSettings();
        }

        private void InitializeFloorComponent(TextureAdjuster floor, string componentName)
        {
            if (floor)
            {
                floor.enableScroll = true;
                floor.scrollSpeedY = 0f;
            }
            else
            {
                _logger?.ZLogWarning($"{componentName} 설정 누락됨.");
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

        /// <summary>
        /// 플레이어별 이동 속도에 맞춰 바닥, 프레임, 장애물을 스크롤함.
        /// </summary>
        public void ScrollEnvironment(float p1Speed, float p2Speed)
        {
            if (p1Floor) p1Floor.scrollSpeedY = p1Speed;
            if (p1Frames) p1Frames.ScrollFrames(p1Speed);
            if (p1Obstacles) p1Obstacles.ScrollObstacles(p1Speed);

            if (p2Floor) p2Floor.scrollSpeedY = p2Speed;
            if (p2Frames) p2Frames.ScrollFrames(p2Speed);
            if (p2Obstacles) p2Obstacles.ScrollObstacles(p2Speed);
        }

        /// <summary>
        /// 특정 플레이어 라인의 일부 장애물만 나타나도록 페이드인 처리함.
        /// </summary>
        public void FadeInObstacles(int playerIdx, int startIndex, int count, float duration)
        {
            if (playerIdx == 0)
            {
                ExecuteObstacleFadeIn(p1Obstacles, startIndex, count, duration, 0);
            }
            else if (playerIdx == 1)
            {
                ExecuteObstacleFadeIn(p2Obstacles, startIndex, count, duration, 1);
            }
        }

        private void ExecuteObstacleFadeIn(TutorialObstacleManager target, int startIndex, int count, float duration, int playerIdx)
        {
            if (target)
            {
                target.FadeInSpecificObstacles(duration, startIndex, count);
            }
            else
            {
                _logger?.ZLogWarning($"FadeInObstacles: {playerIdx}번 플레이어의 장애물 매니저가 누락됨.");
            }
        }

        /// <summary>
        /// 양쪽 플레이어 라인의 지정된 범위 장애물을 동시에 페이드인 처리함.
        /// </summary>
        public void FadeInAllObstacles(int startIndex, int count, float duration)
        {
            if (p1Obstacles) p1Obstacles.FadeInSpecificObstacles(duration, startIndex, count);
            if (p2Obstacles) p2Obstacles.FadeInSpecificObstacles(duration, startIndex, count);
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