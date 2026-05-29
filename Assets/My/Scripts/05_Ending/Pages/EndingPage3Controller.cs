using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Microsoft.Extensions.Logging;
using My.Scripts.Core;
using My.Scripts.Global;
using UnityEngine;
using UnityEngine.UI;
using VContainer;
using Wonjeong.Data;
using Wonjeong.UI;
using ZLogger;

namespace My.Scripts._05_Ending.Pages
{
    [Serializable]
    public class EndingPage3Data
    {
        public TextSetting resultText;
        public TextSetting allFinishedText;
    }

    /// <summary>
    /// 엔딩 씬의 마지막 페이지 컨트롤러.
    /// 진엔딩 여부에 따른 분기 로직과 UI 연출을 비동기로 처리함.
    /// </summary>
    public class EndingPage3Controller : GamePage<EndingPage3Data>
    {
        [Header("UI References")]
        [SerializeField] private Text result;
        [SerializeField] private Image redLineImage;

        private EndingPage3Data _data;
        private bool _isAllFinished;
        private bool _hasSentEndTime;
        private CancellationTokenSource _cts;

        private ILogger<EndingPage3Controller> _logger;
        private SessionManager _sessionManager;
        private GameManager _gameManager;
        private UIManager _uiManager;
        private SoundManager _soundManager;

        [Inject]
        public void Construct(ILogger<EndingPage3Controller> logger, SessionManager sessionManager, 
                              GameManager gameManager, UIManager uiManager, SoundManager soundManager)
        {
            _logger = logger;
            _sessionManager = sessionManager;
            _gameManager = gameManager;
            _uiManager = uiManager;
            _soundManager = soundManager;
        }

        protected override void SetupData(EndingPage3Data data)
        {
            _data = data;
        }

        public override void OnEnter()
        {
            base.OnEnter();
            _cts = new CancellationTokenSource();
            _isAllFinished = _sessionManager && _sessionManager.IsOtherCartridgeContentsCleared;

            // UI 및 텍스트 설정
            if (_data != null)
            {
                TextSetting textToUse = (_isAllFinished && _data.allFinishedText != null) 
                    ? _data.allFinishedText 
                    : _data.resultText;

                if (result && _uiManager)
                {
                    _uiManager.SetText(result.gameObject, textToUse);
                }
            }

            if (redLineImage)
            {
                redLineImage.type = Image.Type.Filled;
                redLineImage.fillAmount = 0f;
                redLineImage.gameObject.SetActive(_isAllFinished);
            }

            // API 호출
            if (!_hasSentEndTime && _gameManager)
            {
                _hasSentEndTime = true;
                _gameManager.SendTimeUpdateAPI();
                _gameManager.SendExitRoomAPI();
            }

            SequenceAsync(_cts.Token).Forget();
        }

        private async UniTaskVoid SequenceAsync(CancellationToken ct)
        {
            try
            {
                await UniTask.Delay(TimeSpan.FromSeconds(1.5f), cancellationToken: ct);

                if (_isAllFinished && redLineImage)
                {
                    await FillImageAsync(redLineImage, 0f, 1f, 2.0f, ct);
                    _soundManager?.FadeOutBGM(5.0f);
                    await UniTask.Delay(TimeSpan.FromSeconds(5.0f), cancellationToken: ct);
                }
                else
                {
                    await UniTask.Delay(TimeSpan.FromSeconds(2.0f), cancellationToken: ct);
                    _soundManager?.FadeOutBGM(5.0f);
                    await UniTask.Delay(TimeSpan.FromSeconds(5.0f), cancellationToken: ct);
                }

                CompleteStep();
            }
            catch (OperationCanceledException) { /* 정상 종료 */ }
        }

        private async UniTask FillImageAsync(Image image, float start, float end, float duration, CancellationToken ct)
        {
            if (!image) return;
            float elapsed = 0f;
            image.fillAmount = start;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                image.fillAmount = Mathf.Lerp(start, end, elapsed / duration);
                await UniTask.Yield(PlayerLoopTiming.Update, ct);
            }
            image.fillAmount = end;
        }

        public override void OnExit()
        {
            base.OnExit();
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;

            if (redLineImage)
            {
                redLineImage.fillAmount = 0f;
                redLineImage.gameObject.SetActive(false);
            }
        }
    }
}