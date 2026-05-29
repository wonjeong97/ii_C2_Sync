using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Microsoft.Extensions.Logging;
using My.Scripts.Core;
using UnityEngine;
using UnityEngine.UI;
using VContainer;
using Wonjeong.Data;
using Wonjeong.UI;
using ZLogger;

namespace My.Scripts._01_Tutorial.Pages
{
    [Serializable]
    public class TutorialPage2Data
    {
        public TextSetting descriptionText;
    }

    public class TutorialPage2Controller : GamePage<TutorialPage2Data>
    {
        [Header("UI Components")]
        [SerializeField] private Text descriptionText;

        private CancellationTokenSource _cts;
        private ILogger<TutorialPage2Controller> _logger;
        private GameManager _gameManager;
        private SoundManager _soundManager;
        private UIManager _uiManager;

        [Inject]
        public void Construct(
            ILogger<TutorialPage2Controller> logger,
            GameManager gameManager,
            SoundManager soundManager,
            UIManager uiManager)
        {
            _logger = logger;
            _gameManager = gameManager;
            _soundManager = soundManager;
            _uiManager = uiManager;
        }

        protected override void SetupData(TutorialPage2Data data)
        {
            if (data == null)
            {
                _logger?.ZLogWarning($"[TutorialPage2] 데이터 누락.");
                return;
            }

            if (descriptionText && _uiManager)
            {
                _uiManager.SetText(descriptionText.gameObject, data.descriptionText);
            }
            else
            {
                _logger?.ZLogWarning($"[TutorialPage2] UI 컴포넌트 또는 UIManager 누락.");
            }
        }

        public override void OnEnter()
        {
            base.OnEnter();
            _cts = new CancellationTokenSource();

            if (_gameManager) _gameManager.IsAutoProgressing = true;

            AutoPassAsync(_cts.Token).Forget();
        }

        private async UniTaskVoid AutoPassAsync(CancellationToken ct)
        {
            if (_soundManager) 
                _soundManager.PlaySFX("공통_6");
            else 
                _logger?.ZLogWarning($"[TutorialPage2] SoundManager 누락.");

            // 3초 대기 (UniTask 기반)
            await UniTask.Delay(TimeSpan.FromSeconds(3.0f), cancellationToken: ct);

            // 취소되지 않았다면 다음 단계 진행
            if (!ct.IsCancellationRequested)
            {
                CompleteStep();
            }
        }

        public override void OnExit()
        {
            // 작업 취소 및 정리
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
            
            base.OnExit();
        }
    }
}