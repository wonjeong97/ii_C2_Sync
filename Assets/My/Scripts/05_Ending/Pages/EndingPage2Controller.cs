using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Microsoft.Extensions.Logging;
using My.Scripts.Core;
using My.Scripts.UI;
using UnityEngine;
using UnityEngine.UI;
using VContainer;
using Wonjeong.Data;
using Wonjeong.UI;
using ZLogger;

namespace My.Scripts._05_Ending.Pages
{
    [Serializable]
    public class EndingPage2Data
    {
        public TextSetting topTextFormat;
        public TextSetting bottomTextFormat;
    }

    /// <summary>
    /// 엔딩 씬의 마음 조각 획득 연출을 담당하는 페이지 컨트롤러.
    /// UniTask 비동기 처리와 DI를 통해 안정성을 최적화함.
    /// </summary>
    public class EndingPage2Controller : GamePage<EndingPage2Data>
    {
        [Header("UI Groups")]
        [SerializeField] private CanvasGroup heartsCg;
        [SerializeField] private CanvasGroup textsCg;

        [Header("Texts")]
        [SerializeField] private Text topText;
        [SerializeField] private Text bottomText;

        [Header("Heart Images")]
        [SerializeField] private Image[] heartImages;
        [SerializeField] private Sprite heartGetSprite;
        [SerializeField] private Sprite heartDontGetSprite;

        private EndingPage2Data _data;
        private bool _hasSentPieceUpdate;
        private CancellationTokenSource _cts;

        private ILogger<EndingPage2Controller> _logger;
        private GameManager _gameManager;
        private UIManager _uiManager;
        private SoundManager _soundManager;

        [Inject]
        public void Construct(ILogger<EndingPage2Controller> logger, GameManager gameManager, UIManager uiManager, SoundManager soundManager)
        {
            _logger = logger;
            _gameManager = gameManager;
            _uiManager = uiManager;
            _soundManager = soundManager;
        }

        protected override void SetupData(EndingPage2Data data)
        {
            if (data == null) return;
            _data = data;
        }

        public override void OnEnter()
        {
            base.OnEnter();
            _cts = new CancellationTokenSource();

            ResetUIState();
            
            var (fragments, totalPieces) = ProcessGameLogic();
            
            SetupTexts(fragments, totalPieces);
            InitializeHearts(fragments);

            EntranceSequenceAsync(fragments, _cts.Token).Forget();
        }
        
        private void ResetUIState()
        {
            if (heartsCg) heartsCg.alpha = 0f;
            if (textsCg) textsCg.alpha = 0f;
        }
        
        private (int fragments, int totalPieces) ProcessGameLogic()
        {
            if (!_gameManager) return (0, 0);

            float dist = _gameManager.lastPlayDistance;
            int fragments = Mathf.Clamp(Mathf.FloorToInt(dist / 100f), 0, 5);

            _gameManager.PieceC2 = fragments;
            int totalPieces = _gameManager.TotalPieces + fragments;

            if (!_hasSentPieceUpdate)
            {
                _gameManager.SendPieceUpdateAPI(fragments);
                _hasSentPieceUpdate = true;
            }

            return (fragments, totalPieces);
        }
        
        private void SetupTexts(int fragments, int totalPieces)
        {
            if (_data == null) return;

            if (topText && _data.topTextFormat != null)
            {
                _uiManager?.SetText(topText.gameObject, _data.topTextFormat);
                topText.text = string.Format(_data.topTextFormat.text, fragments);
            }

            if (bottomText && _data.bottomTextFormat != null)
            {
                _uiManager?.SetText(bottomText.gameObject, _data.bottomTextFormat);
                bottomText.text = string.Format(_data.bottomTextFormat.text, totalPieces);
            }
        }
        
        private void InitializeHearts(int fragments)
        {
            if (heartImages == null) return;

            foreach (Image img in heartImages)
            {
                if (img)
                {
                    img.sprite = heartDontGetSprite;
                    Color c = img.color;
                    c.a = 0.0f;
                    img.color = c;
                }
            }
        }
        
        private async UniTaskVoid EntranceSequenceAsync(int fragments, CancellationToken ct)
        {
            try
            {
                await UniTask.Delay(TimeSpan.FromSeconds(0.5f), cancellationToken: ct);
                if (heartsCg) await UIUtils.FadeCanvasGroupAsync(heartsCg, 0f, 1f, 0.5f, ct);

                for (int i = 0; i < fragments; i++)
                {
                    if (i < heartImages.Length && heartImages[i])
                    {
                        _soundManager?.PlaySFX("공통_6");
                        heartImages[i].sprite = heartGetSprite;
                        await FadeImageAlphaAsync(heartImages[i], 0f, 1f, 0.5f, ct);
                    }
                }

                await UniTask.Delay(TimeSpan.FromSeconds(0.5f), cancellationToken: ct);
                if (textsCg) await UIUtils.FadeCanvasGroupAsync(textsCg, 0f, 1f, 1.0f, ct);

                await UniTask.Delay(TimeSpan.FromSeconds(2.0f), cancellationToken: ct);
                CompleteStep();
            }
            catch (OperationCanceledException) { /* 정상 종료 */ }
        }

        private async UniTask FadeImageAlphaAsync(Image img, float start, float end, float duration, CancellationToken ct)
        {
            if (!img) return;
            
            float elapsed = 0f;
            Color c = img.color;
            c.a = start;
            img.color = c;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                c.a = Mathf.Lerp(start, end, elapsed / duration);
                img.color = c;
                await UniTask.Yield(PlayerLoopTiming.Update, ct);
            }
            
            c.a = end;
            img.color = c;
        }

        public override void OnExit()
        {
            base.OnExit();
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
        }
    }
}