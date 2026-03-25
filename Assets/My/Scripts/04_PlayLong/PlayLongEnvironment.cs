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

            Camera targetCam = Camera.main;
            if (!targetCam)
            {
                GameObject camObj = GameObject.FindWithTag("MainCamera");
                if (camObj) targetCam = camObj.GetComponent<Camera>();
            }

            if (!targetCam)
            {
                Debug.LogWarning("[PlayLongEnvironment] 메인 카메라를 찾을 수 없어 Fader의 타겟 설정에 문제가 발생할 수 있습니다.");
            }

            if (obstacleManager) obstacleManager.Init(targetCam, false);
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
                    if (frameManager) frameManager.MoveFrames(movedMeters); 
                }
            }
        }

        public IEnumerator SmoothResetEnvironment(float duration = 1.0f)
        {
            _isSmoothResetting = true; 
            
            try
            {
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
                    if (uvPerMeter > 0.000001f)
                    {
                        float movedMeters = uvDelta / uvPerMeter;
                        if (frameManager) frameManager.MoveFrames(movedMeters);
                        if (obstacleManager) obstacleManager.MoveObstacles(movedMeters);
                    }

                    yield return null;
                }
            }
            finally
            {
                ResetEnvironmentScroll();
                
                if (frameManager) frameManager.ResetFrames();
                if (obstacleManager) obstacleManager.ResetObstacles();

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
        
        /// <summary>
        /// 게임 종료 시 환경에 남아있는 장애물들을 부드럽게 지웁니다.
        /// </summary>
        /// <param name="duration">페이드아웃 시간</param>
        public void ClearObstacles(float duration)
        {
            if (obstacleManager) obstacleManager.StopAndFadeOutObstacles(duration);
            else Debug.LogWarning("[PlayLongEnvironment] 장애물 매니저가 연결되지 않아 클리어할 수 없습니다.");
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