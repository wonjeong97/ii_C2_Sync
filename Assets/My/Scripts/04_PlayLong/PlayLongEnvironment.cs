using UnityEngine;

namespace My.Scripts._04_PlayLong
{
    public class PlayLongEnvironment : MonoBehaviour
    {
        [Header("Environment References")]
        [SerializeField] private TextureAdjuster mainFloor;

        [Header("Obstacle Manager")]
        [SerializeField] private PlayLongObstacleManager obstacleManager;

        [Header("Scroll Settings")]
        [Tooltip("1미터 이동 시 UV 스크롤 변화량")]
        [SerializeField] private float uvPerMeter = 0.005f; 
        [Tooltip("스크롤 부드러움 정도")]
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

        private float _targetOffsetY = 0f;
        private float _currentOffsetY = 0f;

        private void Start()
        {
            InitEnvironment();
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

            if (obstacleManager)
            {
                obstacleManager.Init(Camera.main, false);
            }

            BackupFogSettings();
            ApplyFogSettings();
        }

        /// <summary>
        /// 스턴 발생 시 호출하여 현재 위치에서 스크롤을 즉시 멈춤
        /// </summary>
        public void StopScroll()
        {
            // 목표 위치를 현재 위치로 강제 고정하여 Lerp에 의한 미끄러짐 방지
            _targetOffsetY = _currentOffsetY;
        }

        public void ScrollByMeter(float meters)
        {
            // 플레이어 중 한 명이라도 스턴 상태라면 새로운 이동 명령을 무시하도록 매니저에서 제어 필요
            if (mainFloor)
            {
                _targetOffsetY += meters * uvPerMeter;
            }
        }

        private void Update()
        {
            if (mainFloor)
            {
                float prevOffset = _currentOffsetY;

                // 보간 속도(scrollSmoothing)에 의해 스턴 후에도 조금 더 움직일 수 있음
                _currentOffsetY = Mathf.Lerp(_currentOffsetY, _targetOffsetY, Time.deltaTime * scrollSmoothing);
            
                // 두 값의 차이가 아주 작으면 완전히 일치시켜 불필요한 연산 방지
                if (Mathf.Abs(_targetOffsetY - _currentOffsetY) < 0.0001f) _currentOffsetY = _targetOffsetY;

                mainFloor.offset = new Vector2(mainFloor.offset.x, _currentOffsetY);
                mainFloor.UpdateUVs();

                float uvDelta = _currentOffsetY - prevOffset;

                if (obstacleManager && uvPerMeter > 0.000001f)
                {
                    float movedMeters = uvDelta / uvPerMeter;
                    obstacleManager.MoveObstacles(movedMeters);
                }
            }
        }

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
            RenderSettings.fog = _prevFog;
            RenderSettings.fogColor = _prevFogColor;
            RenderSettings.fogMode = _prevFogMode;
            RenderSettings.fogStartDistance = _prevFogStart;
            RenderSettings.fogEndDistance = _prevFogEnd;
        }
    }
}