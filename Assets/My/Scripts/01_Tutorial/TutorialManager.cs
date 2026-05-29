using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Microsoft.Extensions.Logging;
using My.Scripts._01_Tutorial.Pages;
using My.Scripts.Core;
using My.Scripts.Core.FlowSystem;
using VContainer;
using Wonjeong.Utils;
using ZLogger;

namespace My.Scripts._01_Tutorial
{
    [Serializable]
    public class TutorialSetting
    {
        public TutorialPage1Data page1;
        public TutorialPage2Data page2;
        public TutorialPage3Data page3;
        public TutorialPage4Data page4;
    }

    public class TutorialManager : BaseFlowManager
    {
        private IObjectResolver _resolver;
        private GameManager _gameManager;

        [Inject]
        public void Construct(IObjectResolver resolver, ILogger<TutorialManager> logger, GameManager gameManager)
        {
            _resolver = resolver;
            _logger = logger;
            _gameManager = gameManager;
        }

        protected override void LoadSettings()
        {
            if (_resolver != null)
            {
                foreach (GamePage page in pages)
                {
                    if (page) _resolver.Inject(page);
                }
            }

            string localizedPath = GameConstants.Path.GetLocalizedPath(GameConstants.Path.Tutorial);
            TutorialSetting setting = JsonLoader.Load<TutorialSetting>(localizedPath);

            if (setting == null)
            {
                _logger?.ZLogError($"[TutorialManager] JSON 데이터 로드 실패: {localizedPath}");
                return;
            }

            SetupPageData(setting);
        }

        private void SetupPageData(TutorialSetting setting)
        {
            TrySetupPage(0, setting.page1);
            TrySetupPage(1, setting.page2);
            TrySetupPage(2, setting.page3);
            TrySetupPage(3, setting.page4);
        }
        
        private void TrySetupPage(int index, object pageData)
        {
            if (index < 0 || index >= pages.Length || pages[index] == null || pageData == null)
            {   
                _logger?.ZLogWarning($"[TutorialManager] 데이터 주입 실패(index: {index}, pageData: {pageData}).");
                return;
            }
            pages[index].SetupData(pageData);
        }

        protected override void OnAllFinished()
        {
            if (_gameManager)
            {
                _gameManager.ChangeScene(GameConstants.Scene.PlayTutorial);
            }
        }

        protected override async UniTask TransitionAsync(int targetIndex, int info, CancellationToken ct)
        {
            // 초기 씬 진입 시 첫 페이지 연출 생략 처리
            if (currentPageIndex == -1 && targetIndex == 0)
            {
                currentPageIndex = 0;
                GamePage next = pages[0];

                if (next)
                {
                    next.OnEnter();
                    next.SetAlpha(1f);
                }

                isTransitioning = false;
                return;
            }

            // 기본 전환 로직 호출
            await base.TransitionAsync(targetIndex, info, ct);
        }
    }
}