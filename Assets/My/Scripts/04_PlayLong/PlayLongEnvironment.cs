using UnityEngine;

namespace My.Scripts._04_PlayLong
{
    public class PlayLongEnvironment : MonoBehaviour
    {
        [Header("Fog Settings")] 
        [SerializeField] private bool useFog = true;
        [SerializeField] private Color fogColor = new Color(0.9f, 0.9f, 0.9f, 1f); // 밝은 회색톤
        [SerializeField] private float fogStartDistance = 10f; 
        [SerializeField] private float fogEndDistance = 60f; 
        
        // 안개 설정 복구용 변수
        private bool _prevFog;
        private Color _prevFogColor;
        private FogMode _prevFogMode;
        private float _prevFogStart;
        private float _prevFogEnd;

        private void Start()
        {
            InitEnvironment();
        }

        public void InitEnvironment()
        {
            // 안개 설정 백업 및 적용
            BackupFogSettings();
            ApplyFogSettings();
        }

        /// <summary>
        /// 매니저에서 호출하지만, 현재 스크롤 기능은 비활성화 상태입니다.
        /// </summary>
        public void ScrollEnvironment(float p1Speed, float p2Speed)
        {
            // 테스트를 위해 스크롤 로직 비워둠
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
            // 씬 종료 시 원래 안개 설정으로 복구
            RenderSettings.fog = _prevFog;
            RenderSettings.fogColor = _prevFogColor;
            RenderSettings.fogMode = _prevFogMode;
            RenderSettings.fogStartDistance = _prevFogStart;
            RenderSettings.fogEndDistance = _prevFogEnd;
        }
    }
}