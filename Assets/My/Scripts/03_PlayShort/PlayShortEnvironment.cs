using UnityEngine;

namespace My.Scripts._03_PlayShort
{
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

        public void InitEnvironment()
        {
            // [수정됨] TextureAdjuster의 자체 스크롤(Update 루프)을 끄고 외부에서 직접 제어합니다.
            if (p1Floor) { p1Floor.enableScroll = false; p1Floor.scrollSpeedY = 0f; }
            if (p2Floor) { p2Floor.enableScroll = false; p2Floor.scrollSpeedY = 0f; }

            if (p1Obstacles && leftCamera) p1Obstacles.Init(leftCamera);
            if (p2Obstacles && rightCamera) p2Obstacles.Init(rightCamera);

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

            // Player 1
            // [수정됨] 바닥 스크롤을 동기화 전용 메서드로 직접 처리합니다.
            if (p1Floor) ApplyScrollToFloor(p1Floor, p1Speed * dt);
            if (p1Frames) p1Frames.ScrollFrames(p1Speed);
            if (p1Obstacles) p1Obstacles.ScrollObstacles(p1Speed);

            // Player 2
            // [수정됨] 바닥 스크롤을 동기화 전용 메서드로 직접 처리합니다.
            if (p2Floor) ApplyScrollToFloor(p2Floor, p2Speed * dt);
            if (p2Frames) p2Frames.ScrollFrames(p2Speed);
            if (p2Obstacles) p2Obstacles.ScrollObstacles(p2Speed);
        }

        // [추가됨] TextureAdjuster에 직접 UV 오프셋을 더하고 루프를 처리하여 프레임 지연을 완벽히 없앱니다.
        private void ApplyScrollToFloor(TextureAdjuster floor, float uvDelta)
        {
            if (uvDelta == 0f) return;
            
            floor.offset.y += uvDelta;
            
            if (floor.useCustomLoop)
            {
                float loopSize = floor.loopMaxY - floor.loopMinY;
                if (loopSize > 0.0001f)
                {
                    while (floor.offset.y > floor.loopMaxY) floor.offset.y -= loopSize;
                    while (floor.offset.y < floor.loopMinY) floor.offset.y += loopSize;
                }
            }
            
            floor.UpdateUVs();
        }

        public void RecycleFrameClosestToCamera(int playerIdx)
        {
            if (playerIdx == 0 && p1Frames && leftCamera)
            {
                p1Frames.ForceRecycleFrameClosestToCamera(leftCamera.transform);
            }
            else if (playerIdx == 1 && p2Frames && rightCamera)
            {
                p2Frames.ForceRecycleFrameClosestToCamera(rightCamera.transform);
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