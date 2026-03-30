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

        /// <summary>
        /// 환경 요소 및 안개 설정을 초기화함.
        /// </summary>
        public void InitEnvironment()
        {
            if (p1Floor) 
            { 
                p1Floor.enableScroll = true; 
                p1Floor.scrollSpeedY = 0f; 
            }
            else
            {
                Debug.LogWarning("p1Floor 설정 누락됨.");
            }

            if (p2Floor) 
            { 
                p2Floor.enableScroll = true; 
                p2Floor.scrollSpeedY = 0f; 
            }
            else
            {
                Debug.LogWarning("p2Floor 설정 누락됨.");
            }
    
            // 이유: 글로벌 렌더 세팅 변경 시, 다른 씬으로 넘어갈 때 발생할 수 있는 시각적 오류를 방지하기 위해 원본 값을 보관함.
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
        /// <param name="p1Speed">1P 이동 속도</param>
        /// <param name="p2Speed">2P 이동 속도</param>
        public void ScrollEnvironment(float p1Speed, float p2Speed)
        {
            // # TODO: Update 루프에서 매번 스크롤 연산을 수행하므로 텍스처 오프셋 셰이더 프로퍼티 제어로 최적화 검토 필요.
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
        /// <param name="playerIdx">대상 플레이어 인덱스</param>
        /// <param name="startIndex">생성 시작 인덱스</param>
        /// <param name="count">생성할 장애물 개수</param>
        /// <param name="duration">페이드인 진행 시간</param>
        public void FadeInObstacles(int playerIdx, int startIndex, int count, float duration)
        {
            TutorialObstacleManager target = null;
            if (playerIdx == 0) target = p1Obstacles;
            else if (playerIdx == 1) target = p2Obstacles;
            else return;

            if (target) 
            {
                target.FadeInSpecificObstacles(duration, startIndex, count);
            }
            else
            {
                Debug.LogWarning($"FadeInObstacles: {playerIdx}번 플레이어의 장애물 매니저가 누락됨.");
            }
        }

        /// <summary>
        /// 양쪽 플레이어 라인의 지정된 범위 장애물을 동시에 페이드인 처리함.
        /// </summary>
        /// <param name="startIndex">생성 시작 인덱스</param>
        /// <param name="count">생성할 장애물 개수</param>
        /// <param name="duration">페이드인 진행 시간</param>
        public void FadeInAllObstacles(int startIndex, int count, float duration)
        {
            if (p1Obstacles) p1Obstacles.FadeInSpecificObstacles(duration, startIndex, count);
            if (p2Obstacles) p2Obstacles.FadeInSpecificObstacles(duration, startIndex, count);
        }
        
        /// <summary>
        /// 스크립트 비활성화 시 호출됨.
        /// </summary>
        private void OnDisable()
        {   
            if (!_hasFogBackup) return;

            // 이유: 씬을 벗어나거나 오브젝트가 꺼질 때 다른 시스템에 영향을 주지 않도록 안개 설정을 초기 상태로 되돌림.
            RenderSettings.fog = _prevFog;
            RenderSettings.fogColor = _prevFogColor;
            RenderSettings.fogMode = _prevFogMode;
            RenderSettings.fogStartDistance = _prevFogStartDistance;
            RenderSettings.fogEndDistance = _prevFogEndDistance;
            RenderSettings.fogDensity = _prevFogDensity; 
        }
    }
}