using My.Scripts._02_PlayTutorial.Controllers;
using UnityEngine;

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

        public void InitEnvironment()
        {
            if (p1Floor) { p1Floor.enableScroll = true; p1Floor.scrollSpeedY = 0f; }
            if (p2Floor) { p2Floor.enableScroll = true; p2Floor.scrollSpeedY = 0f; }
    
            // [Fix] 안개 설정을 변경하기 전에 먼저 백업합니다.
            _prevFog = RenderSettings.fog;
            _prevFogColor = RenderSettings.fogColor;
            _prevFogMode = RenderSettings.fogMode;
            _prevFogStartDistance = RenderSettings.fogStartDistance;
            _prevFogEndDistance = RenderSettings.fogEndDistance;
            _prevFogDensity = RenderSettings.fogDensity;
            _hasFogBackup = true;

            // 백업 완료 후 새로운 설정 적용
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
            if (p1Floor) p1Floor.scrollSpeedY = p1Speed;
            if (p1Frames) p1Frames.ScrollFrames(p1Speed);
            if (p1Obstacles) p1Obstacles.ScrollObstacles(p1Speed);

            if (p2Floor) p2Floor.scrollSpeedY = p2Speed;
            if (p2Frames) p2Frames.ScrollFrames(p2Speed);
            if (p2Obstacles) p2Obstacles.ScrollObstacles(p2Speed);
        }

        public void FadeInObstacles(int playerIdx, int startIndex, int count, float duration)
        {
            TutorialObstacleManager target = null;
            if (playerIdx == 0) target = p1Obstacles;
            else if (playerIdx == 1) target = p2Obstacles;
            else return;

            if (target != null) target.FadeInSpecificObstacles(duration, startIndex, count);
        }

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