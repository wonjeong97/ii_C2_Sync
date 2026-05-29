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
    public class TutorialPage4Data
    {
        public TextSetting[] descriptionTexts;
    }

    public class TutorialPage4Controller : GamePage<TutorialPage4Data>
    {
        [Header("UI Components")]
        [SerializeField] private Text descriptionText; 

        private TutorialPage4Data _data;
        private CancellationTokenSource _cts;
        
        private const float FadeInTime = 0.5f;
        private const float DisplayTime = 3f;
        private const float FadeOutTime = 0.5f;

        private ILogger<TutorialPage4Controller> _logger;
        private GameManager _gameManager;
        private UIManager _uiManager;

        [Inject]
        public void Construct(ILogger<TutorialPage4Controller> logger, GameManager gameManager, UIManager uiManager)
        {
            _logger = logger;
            _gameManager = gameManager;
            _uiManager = uiManager;
        }

        protected override void SetupData(TutorialPage4Data data)
        {
            if (data == null)
            {
                _logger?.ZLogWarning($"TutorialPage4Data 데이터가 누락됨.");
                return;
            }

            _data = data;
            
            if (descriptionText)
            {
                SetTextAlpha(0f);
                descriptionText.gameObject.SetActive(false);
            }
            else
            {
                _logger?.ZLogWarning($"descriptionText 컴포넌트가 누락됨.");
            }
        }

        public override void OnEnter()
        {
            base.OnEnter();
            _cts = new CancellationTokenSource();

            if (_gameManager) _gameManager.IsAutoProgressing = true;

            if (_data?.descriptionTexts != null && _data.descriptionTexts.Length > 0)
            {
                ScenarioAsync(_cts.Token).Forget();
            }
            else
            {
                _logger?.ZLogWarning($"descriptionTexts 배열 데이터가 부족하여 연출을 스킵함.");
                CompleteStep();
            }
        }

        public override void OnExit()
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
            base.OnExit();
        }

        private async UniTaskVoid ScenarioAsync(CancellationToken ct)
        {
            for (int i = 0; i < _data.descriptionTexts.Length; i++)
            {
                var setting = _data.descriptionTexts[i];
                if (setting == null || !descriptionText || !_uiManager) continue;
                
                _uiManager.SetText(descriptionText.gameObject, setting);
                descriptionText.gameObject.SetActive(true);
                
                // Fade In
                await FadeTextAsync(0f, 1f, FadeInTime, ct);
                
                // Display
                await UniTask.Delay(TimeSpan.FromSeconds(DisplayTime), cancellationToken: ct);
                
                // Fade Out (마지막 텍스트는 유지)
                if (i < _data.descriptionTexts.Length - 1)
                {
                    await FadeTextAsync(1f, 0f, FadeOutTime, ct);
                }
            }
            
            CompleteStep();
        }

        private async UniTask FadeTextAsync(float start, float end, float duration, CancellationToken ct)
        {
            if (!descriptionText) return;

            float elapsed = 0f;
            Color color = descriptionText.color;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                color.a = Mathf.Lerp(start, end, elapsed / duration);
                descriptionText.color = color;
                await UniTask.Yield(PlayerLoopTiming.Update, ct);
            }

            color.a = end;
            descriptionText.color = color;
        }

        private void SetTextAlpha(float alpha)
        {
            if (!descriptionText) return;
            Color c = descriptionText.color;
            c.a = alpha;
            descriptionText.color = c;
        }
    }
}