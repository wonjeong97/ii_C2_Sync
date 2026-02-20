using System.Collections;
using UnityEngine;

namespace My.Scripts._04_PlayLong
{
    public class PlayLongEnvironment : MonoBehaviour
    {
        [Header("Environment References")]
        [SerializeField] private TextureAdjuster mainFloor;
        [SerializeField] private PlayLongObstacleManager obstacleManager;
        [SerializeField] private PlayLongFrameManager frameManager;

        [Header("Scroll Settings")]
        [Tooltip("1미터 이동 시 UV 스크롤 변화량")]
        [SerializeField] private float uvPerMeter = 0.0025f; 
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

        private float _targetOffsetY;
        private float _currentOffsetY;
        private bool _isSmoothResetting;

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

            if (obstacleManager) obstacleManager.Init(Camera.main, false);
            if (frameManager) frameManager.Init();

            BackupFogSettings();
            ApplyFogSettings();
        }

        public void ScrollByMeter(float meters)
        {
            if (mainFloor)
            {
                _targetOffsetY += meters * uvPerMeter;
            }
        }

        private void Update()
        {
            if (mainFloor && !_isSmoothResetting)
            {
                float prevOffset = _currentOffsetY;

                _currentOffsetY = Mathf.Lerp(_currentOffsetY, _targetOffsetY, Time.deltaTime * scrollSmoothing);
                if (Mathf.Abs(_targetOffsetY - _currentOffsetY) < 0.0001f) _currentOffsetY = _targetOffsetY;

                mainFloor.offset = new Vector2(mainFloor.offset.x, _currentOffsetY);
                mainFloor.UpdateUVs();

                float uvDelta = _currentOffsetY - prevOffset;
                if (uvPerMeter > 0.000001f)
                {   
                    float movedMeters = uvDelta / uvPerMeter;
                    if (obstacleManager) obstacleManager.MoveObstacles(movedMeters);
                    if (frameManager) frameManager.MoveFrames(movedMeters); // ★ 중복된 movedMeters 연산 통합 완료
                }
            }
        }

        public IEnumerator SmoothResetEnvironment(float duration = 1.0f)
        {
            _isSmoothResetting = true; // 플래그 설정
            
            float elapsed = 0f;
            float startOffsetY = _currentOffsetY;
            float targetOffsetY = 0f; 

            _targetOffsetY = targetOffsetY;

            while (elapsed < duration)
            {
                float prevOffset = _currentOffsetY;
                elapsed += Time.deltaTime;
        
                float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
                _currentOffsetY = Mathf.Lerp(startOffsetY, targetOffsetY, t);

                if (mainFloor)
                {
                    mainFloor.offset = new Vector2(mainFloor.offset.x, _currentOffsetY);
                    mainFloor.UpdateUVs();
                }

                float uvDelta = _currentOffsetY - prevOffset; 
                if (frameManager && uvPerMeter > 0.000001f)
                {
                    frameManager.MoveFrames(uvDelta / uvPerMeter);
                }

                yield return null;
            }

            ResetEnvironmentScroll();
            if (frameManager)
            {   
                frameManager.ResetFrames();
            }

            _isSmoothResetting = false; // 플래그 해제
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