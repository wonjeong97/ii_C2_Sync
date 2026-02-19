using System.Collections;
using UnityEngine;

namespace My.Scripts._04_PlayLong
{
    /// <summary>
    /// PlayLong 모드의 전체 환경(바닥 스크롤, 장애물, 프레임) 및 Fog 연출을 관리하는 클래스.
    /// </summary>
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

        private float _targetOffsetY = 0f;
        private float _currentOffsetY = 0f;

        private void Start()
        {
            InitEnvironment();
        }

        /// <summary>
        /// 환경 요소를 초기 위치로 설정하고 관련 매니저들을 초기화함.
        /// </summary>
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
                // 초기화 시 랜덤 생성을 하지 않도록 false 설정 (필요 시 Manager에서 별도 호출)
                obstacleManager.Init(Camera.main, false);
            }
            
            if (frameManager)
            {
                frameManager.Init();
            }

            BackupFogSettings();
            ApplyFogSettings();
        }

        /// <summary>
        /// 스턴 발생 시 호출하여 스크롤을 즉시 멈춤.
        /// </summary>
        public void StopScroll()
        {
           
        }

        /// <summary>
        /// 가상 미터 단위의 이동량을 입력받아 바닥 스크롤 목표치를 갱신함.
        /// </summary>
        /// <param name="meters">이동할 가상 거리 (예: delta 스텝 * 1.0m)</param>
        public void ScrollByMeter(float meters)
        {
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

                // scrollSmoothing(5.0)에 의해 목표 지점까지 부드럽게 이동합니다.
                _currentOffsetY = Mathf.Lerp(_currentOffsetY, _targetOffsetY, Time.deltaTime * scrollSmoothing);
        
                if (Mathf.Abs(_targetOffsetY - _currentOffsetY) < 0.0001f) _currentOffsetY = _targetOffsetY;

                mainFloor.offset = new Vector2(mainFloor.offset.x, _currentOffsetY);
                mainFloor.UpdateUVs();

                // 물리적 이동량을 프레임 매니저에 전달 (항상 정확한 거리만큼 이동)
                float uvDelta = _currentOffsetY - prevOffset;
                if (uvPerMeter > 0.000001f)
                {   
                    float movedMeters = uvDelta / uvPerMeter;
                    if (obstacleManager) obstacleManager.MoveObstacles(movedMeters);
                    frameManager.MoveFrames(uvDelta / uvPerMeter);
                }
            }
        }

        /// <summary>
        /// 튜토리얼 종료 시 진행된 거리만큼 역방향으로 1초간 부드럽게 초기화함.
        /// </summary>
        public IEnumerator SmoothResetEnvironment(float duration = 1.0f)
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

                // 되돌아가는 동안의 변화량(음수)을 전달하여 프레임도 함께 역이동
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
                // 연출 완료 후 모든 프레임의 위치와 데이터(100M 단위 가시성 등)를 0M 기준으로 최종 정렬
                frameManager.ResetFrames();
            }
        }

        /// <summary>
        /// 환경 스크롤 데이터를 0으로 즉시 리셋함.
        /// </summary>
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