using System.Collections;
using UnityEngine;

namespace My.Scripts._04_PlayLong
{
    /// <summary>
    /// PlayLong 씬의 배경 스크롤, 장애물 이동, 안개 효과 등 환경 요소를 총괄하는 매니저 클래스.
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

        private float _targetOffsetY;
        private float _currentOffsetY;
        private bool _isSmoothResetting;

        /// <summary>
        /// 인스턴스 활성화 시 환경 초기화를 수행함.
        /// </summary>
        private void Start()
        {
            InitEnvironment();
        }

        /// <summary>
        /// 바닥 스크롤 상태, 카메라 타겟, 안개 설정 등 환경 요소를 초기화함.
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
            else
            {
                Debug.LogWarning("mainFloor 컴포넌트 누락됨.");
            }

            Camera targetCam = Camera.main;
            if (!targetCam)
            {
                GameObject camObj = GameObject.FindWithTag("MainCamera");
                if (camObj) targetCam = camObj.GetComponent<Camera>();
            }

            if (!targetCam)
            {
                Debug.LogWarning("메인 카메라를 찾을 수 없어 Fader의 타겟 설정에 문제가 발생할 수 있음.");
            }

            if (obstacleManager) 
            {
                obstacleManager.Init(targetCam, false);
            }
            else
            {
                Debug.LogWarning("obstacleManager 컴포넌트 누락됨.");
            }

            if (frameManager) 
            {
                frameManager.Init();
            }
            else
            {
                Debug.LogWarning("frameManager 컴포넌트 누락됨.");
            }

            BackupFogSettings();
            ApplyFogSettings();
        }

        /// <summary>
        /// 입력된 미터(m) 단위 거리를 UV 오프셋으로 변환하여 스크롤 목표값을 누적함.
        /// </summary>
        /// <param name="meters">이동할 거리(미터)</param>
        public void ScrollByMeter(float meters)
        {
            if (mainFloor)
            {
                // 예시 입력: meters(10) * uvPerMeter(0.0025) -> 결과값 = 0.025 (스크롤 목표 오프셋 추가량)
                _targetOffsetY += meters * uvPerMeter;
            }
        }

        /// <summary>
        /// 매 프레임 목표 스크롤 위치로 부드럽게 보간 이동함.
        /// </summary>
        private void Update()
        {
            // # TODO: 매 프레임 다수의 매니저 함수를 호출하므로 이벤트 구독 방식으로 스크롤 동기화 최적화 검토 필요.
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
                    // 예시 입력: uvDelta(0.025) / uvPerMeter(0.0025) -> 결과값 = 10 (실제 이동한 미터 거리)
                    float movedMeters = uvDelta / uvPerMeter;
                    if (obstacleManager) obstacleManager.MoveObstacles(movedMeters);
                    if (frameManager) frameManager.MoveFrames(movedMeters); 
                }
            }
        }

        /// <summary>
        /// 환경 스크롤을 부드럽게 초기 지점으로 되감기 연출함.
        /// </summary>
        /// <param name="duration">진행 시간</param>
        /// <returns>IEnumerator 루틴</returns>
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

        /// <summary>
        /// 바닥 스크롤 오프셋을 즉시 초기화함.
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
        
        /// <summary>
        /// 게임 종료 시 환경에 남아있는 장애물들을 부드럽게 지움.
        /// </summary>
        /// <param name="duration">페이드아웃 시간</param>
        public void ClearObstacles(float duration)
        {
            if (obstacleManager) 
            {
                obstacleManager.StopAndFadeOutObstacles(duration);
            }
            else 
            {
                Debug.LogWarning("obstacleManager 컴포넌트 누락됨.");
            }
        }
        
        /// <summary>
        /// 현재 적용된 글로벌 안개 설정을 백업함.
        /// </summary>
        private void BackupFogSettings()
        {
            // 이유: 씬 이동 시 다른 씬의 렌더 세팅을 오염시키는 것을 막기 위함.
            _prevFog = RenderSettings.fog;
            _prevFogColor = RenderSettings.fogColor;
            _prevFogMode = RenderSettings.fogMode;
            _prevFogStart = RenderSettings.fogStartDistance;
            _prevFogEnd = RenderSettings.fogEndDistance;
        }

        /// <summary>
        /// 인스펙터에 지정된 안개 설정을 글로벌 렌더 세팅에 적용함.
        /// </summary>
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

        /// <summary>
        /// 컴포넌트 비활성화 시 호출됨.
        /// 스크롤 상태를 초기화하고 백업된 안개 설정을 복원함.
        /// </summary>
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