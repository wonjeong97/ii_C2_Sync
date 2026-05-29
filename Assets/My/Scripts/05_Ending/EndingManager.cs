using Microsoft.Extensions.Logging;
using My.Scripts.Core;
using My.Scripts.Core.FlowSystem;
using My.Scripts._05_Ending.Pages;
using UnityEngine.SceneManagement;
using VContainer;
using ZLogger;
using Wonjeong.Utils;

namespace My.Scripts._05_Ending
{
    [System.Serializable]
    public class EndingSetting
    {
        public EndingPage1Data page1;
        public EndingPage2Data page2;
        public EndingPage3Data page3;
    }

    /// <summary>
    /// 엔딩 씬의 전체 흐름을 관리하는 매니저.
    /// VContainer 의존성 주입 및 BaseFlowManager의 비동기 흐름 제어를 활용함.
    /// </summary>
    public class EndingManager : BaseFlowManager
    {
        private GameManager _gameManager;
        private IObjectResolver _resolver;

        [Inject]
        public void Construct(ILogger<EndingManager> logger, GameManager gameManager, IObjectResolver resolver)
        {
            _logger = logger;
            _gameManager = gameManager;
            _resolver = resolver;
        }

        /// <summary>
        /// 엔딩 설정 데이터를 로드하고 각 페이지를 초기화함
        /// </summary>
        protected override void LoadSettings()
        {
            InjectDependenciesToPages();

            EndingSetting setting = LoadLocalizedSetting();
            if (setting == null) return;

            SetupPageData(setting);
        }

        /// <summary>
        /// 등록된 모든 페이지에 의존성을 강제 주입함
        /// </summary>
        private void InjectDependenciesToPages()
        {
            if (_resolver == null || pages == null) return;

            // 누락된 페이지의 널 참조 방지
            foreach (GamePage page in pages)
            {
                if (page) _resolver.Inject(page);
            }
        }

        /// <summary>
        /// 다국어 경로를 확인하여 엔딩 설정 데이터를 반환함
        /// </summary>
        private EndingSetting LoadLocalizedSetting()
        {
            string localizedPath = GameConstants.Path.GetLocalizedPath(GameConstants.Path.Ending);
            EndingSetting setting = JsonLoader.Load<EndingSetting>(localizedPath);

            if (setting == null)
            {
                _logger?.ZLogError($"EndingManager: JSON 데이터 로드 실패. 경로: {localizedPath}");
            }

            return setting;
        }

        /// <summary>
        /// 로드된 설정 데이터를 각 페이지 컨트롤러에 할당함
        /// </summary>
        private void SetupPageData(EndingSetting setting)
        {
            if (pages == null || pages.Length == 0) return;

            // 각 페이지의 타입 캐스팅 안전성 보장
            if (pages.Length > 0 && pages[0] is EndingPage1Controller page1) 
                page1.SetupData(setting.page1);
    
            if (pages.Length > 1 && pages[1] is EndingPage2Controller page2) 
                page2.SetupData(setting.page2);
    
            if (pages.Length > 2 && pages[2] is EndingPage3Controller page3) 
                page3.SetupData(setting.page3);
        }

        protected override void OnAllFinished()
        {
            _logger?.ZLogInformation($"플레이 완료. 타이틀 씬으로 복귀합니다.");

            if (_gameManager)
            {
                _gameManager.ReturnToTitle();
            }
            else
            {
                SceneManager.LoadScene(GameConstants.Scene.Title);
            }
        }
    }
}