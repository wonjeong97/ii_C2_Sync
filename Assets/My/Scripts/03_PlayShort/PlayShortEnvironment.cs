using UnityEngine;

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

        /// <summary>
        /// 환경 요소 및 안개 설정을 초기화함.
        /// </summary>
        public void InitEnvironment()
        {
            if (p1Floor) 
            { 
                p1Floor.enableScroll = false; 
                p1Floor.scrollSpeedY = 0f; 
            }
            else
            {
                Debug.LogWarning("p1Floor 컴포넌트 누락됨.");
            }

            if (p2Floor) 
            { 
                p2Floor.enableScroll = false; 
                p2Floor.scrollSpeedY = 0f; 
            }
            else
            {
                Debug.LogWarning("p2Floor 컴포넌트 누락됨.");
            }

            if (p1Obstacles && leftCamera) 
            {
                p1Obstacles.Init(leftCamera);
            }
            else
            {
                Debug.LogWarning("p1Obstacles 또는 leftCamera 누락됨.");
            }

            if (p2Obstacles && rightCamera) 
            {
                p2Obstacles.Init(rightCamera);
            }
            else
            {
                Debug.LogWarning("p2Obstacles 또는 rightCamera 누락됨.");
            }

            // 이유: 씬 전환 시 다른 씬의 렌더 세팅을 오염시키지 않기 위해 원본 값을 보관함.
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
            float dt = Time.deltaTime;

            // # TODO: 매 프레임 다수의 컴포넌트에 접근하여 스크롤을 갱신하므로 이벤트 기반 처리나 통합 매니저 고려 필요.
            if (p1Floor) ApplyScrollToFloor(p1Floor, p1Speed * dt);
            if (p1Frames) p1Frames.ScrollFrames(p1Speed);
            if (p1Obstacles) p1Obstacles.ScrollObstacles(p1Speed);

            if (p2Floor) ApplyScrollToFloor(p2Floor, p2Speed * dt);
            if (p2Frames) p2Frames.ScrollFrames(p2Speed);
            if (p2Obstacles) p2Obstacles.ScrollObstacles(p2Speed);
        }

        /// <summary>
        /// 바닥 텍스처의 UV 오프셋을 스크롤하고 커스텀 루프 범위를 유지함.
        /// </summary>
        /// <param name="floor">스크롤할 텍스처 조정 컴포넌트</param>
        /// <param name="uvDelta">프레임당 이동할 UV 변화량</param>
        private void ApplyScrollToFloor(TextureAdjuster floor, float uvDelta)
        {
            if (uvDelta == 0f) return;
            
            floor.offset.y += uvDelta;
            
            if (floor.useCustomLoop)
            {
                float loopSize = floor.loopMaxY - floor.loopMinY;
                if (loopSize > 0.0001f)
                {
                    // 예시: offset.y(1.5), loopMax(1.0), loopMin(0.5), loopSize(0.5) -> 결과값 = 1.0 (루프 범위 내로 순환)
                    while (floor.offset.y > floor.loopMaxY) floor.offset.y -= loopSize;
                    while (floor.offset.y < floor.loopMinY) floor.offset.y += loopSize;
                }
            }
            
            floor.UpdateUVs();
        }

        /// <summary>
        /// 카메라 시야를 가리지 않도록 가장 가까운 프레임과 주변 장애물을 트랙 뒤쪽으로 재배치하거나 제거함.
        /// </summary>
        /// <param name="playerIdx">대상 플레이어 인덱스</param>
        public void RecycleFrameClosestToCamera(int playerIdx)
        {
            // 플레이어가 멈췄을 때 카메라 바로 앞 구조물뿐만 아니라 장애물도 함께 치워 답변 조작을 방해하지 않게 함.
            if (playerIdx == 0)
            {
                if (p1Frames && leftCamera) p1Frames.ForceRecycleFrameClosestToCamera(leftCamera.transform);
                if (p1Obstacles) p1Obstacles.ClearObstaclesNearPlayer();
            }
            else if (playerIdx == 1)
            {
                if (p2Frames && rightCamera) p2Frames.ForceRecycleFrameClosestToCamera(rightCamera.transform);
                if (p2Obstacles) p2Obstacles.ClearObstaclesNearPlayer();
            }
        }

        /// <summary>
        /// 목표 지점을 통과한 플레이어 라인의 남은 장애물을 부드럽게 제거함.
        /// </summary>
        /// <param name="playerIdx">대상 플레이어 인덱스</param>
        /// <param name="duration">페이드아웃 진행 시간</param>
        public void ClearObstaclesForPlayer(int playerIdx, float duration)
        {
            if (playerIdx == 0)
            {
                if (p1Obstacles) p1Obstacles.StopAndFadeOutObstacles(duration);
                else Debug.LogWarning("p1Obstacles 누락됨.");
            }
            else if (playerIdx == 1)
            {
                if (p2Obstacles) p2Obstacles.StopAndFadeOutObstacles(duration);
                else Debug.LogWarning("p2Obstacles 누락됨.");
            }
        }

        /// <summary>
        /// 컴포넌트 비활성화 시 호출됨.
        /// 안개 설정을 씬 진입 전 원본 상태로 복원함.
        /// </summary>
        private void OnDisable()
        {   
            if (!_hasFogBackup) return;
            
            // 이유: 씬 이동 시 이전 씬의 안개 세팅이 남아서 다른 씬 화면을 오염시키는 것을 막음.
            RenderSettings.fog = _prevFog;
            RenderSettings.fogColor = _prevFogColor;
            RenderSettings.fogMode = _prevFogMode;
            RenderSettings.fogStartDistance = _prevFogStartDistance;
            RenderSettings.fogEndDistance = _prevFogEndDistance;
            RenderSettings.fogDensity = _prevFogDensity; 
        }
    }
}