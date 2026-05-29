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
    public class EndingPage1Data
    {
        public TextSetting distanceFormatText;
    }

    public class EndingPage1Controller : GamePage<EndingPage1Data>
    {
        private readonly static int Jump = Animator.StringToHash("Jump");
        private readonly static int Idle = Animator.StringToHash("Idle");

        [Header("UI Groups")]
        [SerializeField] private CanvasGroup picketAndCharsCg;
        [SerializeField] private CanvasGroup particleCg;
        [SerializeField] private Text distanceTextUI;

        [Header("Animators")]
        [SerializeField] private Animator p1Animator;
        [SerializeField] private Animator p2Animator;

        [Header("Character Parts")]
        [SerializeField] private Image p1Body;
        [SerializeField] private Image p1LeftHand;
        [SerializeField] private Image p1RightHand;
        [SerializeField] private Image p2Body;
        [SerializeField] private Image p2LeftHand;
        [SerializeField] private Image p2RightHand;

        private EndingPage1Data _data;
        private CancellationTokenSource _cts;

        private ILogger<EndingPage1Controller> _logger;
        private GameManager _gameManager;
        private UIManager _uiManager;
        private SoundManager _soundManager;

        [Inject]
        public void Construct(ILogger<EndingPage1Controller> logger, GameManager gameManager, UIManager uiManager, SoundManager soundManager)
        {
            _logger = logger;
            _gameManager = gameManager;
            _uiManager = uiManager;
            _soundManager = soundManager;
        }

        protected override void SetupData(EndingPage1Data data)
        {
            if (data == null)
            {
                _logger.ZLogWarning($"EndingPage1Data 누락됨.");
                return;
            }
            _data = data;
        }

        public override void OnEnter()
        {
            base.OnEnter();
            _cts = new CancellationTokenSource();

            if (picketAndCharsCg) picketAndCharsCg.alpha = 0f;
            if (particleCg)
            {
                particleCg.alpha = 0f;
                particleCg.gameObject.SetActive(false);
            }

            ApplyPlayerColors();

            float dist = _gameManager ? _gameManager.lastPlayDistance : 0f;
            int finalDistance = Mathf.FloorToInt(dist);

            if (_data?.distanceFormatText != null && distanceTextUI)
            {
                if (_uiManager) _uiManager.SetText(distanceTextUI.gameObject, _data.distanceFormatText);
                distanceTextUI.text = string.Format(_data.distanceFormatText.text, finalDistance);
            }
            else
            {
                _logger.ZLogWarning($"거리 텍스트 데이터 혹은 UI 컴포넌트가 누락됨.");
            }

            if (distanceTextUI)
            {
                Color c = distanceTextUI.color;
                distanceTextUI.color = new Color(c.r, c.g, c.b, 0f);
            }

            EntranceSequenceAsync(finalDistance, _cts.Token).Forget();
        }

        private void ApplyPlayerColors()
        {
            if (!_gameManager) return;

            ApplyColorToParts(p1Body, p1LeftHand, p1RightHand, _gameManager.PlayerAColor);
            ApplyColorToParts(p2Body, p2LeftHand, p2RightHand, _gameManager.PlayerBColor);
        }

        private void ApplyColorToParts(Image body, Image left, Image right, ColorData colorData)
        {
            Sprite sprite = _gameManager.GetColorSprite(colorData);
            if (sprite)
            {
                if (body) body.sprite = sprite;
                if (left) left.sprite = sprite;
                if (right) right.sprite = sprite;
            }
            else
            {
                Color c = _gameManager.GetColorFromData(colorData);
                if (body) SetTint(body, c);
                if (left) SetTint(left, c);
                if (right) SetTint(right, c);
            }
        }

        private void SetTint(Image img, Color c) => img.color = new Color(c.r, c.g, c.b, img.color.a);

        private async UniTaskVoid EntranceSequenceAsync(int finalDistance, CancellationToken ct)
        {
            try
            {
                if (picketAndCharsCg) await UIUtils.FadeCanvasGroupAsync(picketAndCharsCg, 0f, 1f, 1.0f, ct);
                if (distanceTextUI) await FadeTextAlphaAsync(distanceTextUI, 0f, 1f, 1.0f, ct);

                if (finalDistance >= 500 && particleCg)
                {
                    _soundManager?.PlaySFX("달리기_4");
                    particleCg.gameObject.SetActive(true);
                    BlinkAsync(particleCg, 0.5f, ct).Forget();
                }
                else
                {
                    _soundManager?.PlaySFX("달리기_5");
                }

                p1Animator?.SetTrigger(Jump);
                p2Animator?.SetTrigger(Jump);

                await UniTask.Delay(TimeSpan.FromSeconds(2.0f), cancellationToken: ct);

                p1Animator?.SetTrigger(Idle);
                p2Animator?.SetTrigger(Idle);

                CompleteStep();
            }
            catch (OperationCanceledException) { /* 정상적인 취소 처리 */ }
        }

        public override void OnExit()
        {
            base.OnExit();
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
        }

        private async UniTask FadeTextAlphaAsync(Text txt, float start, float end, float duration, CancellationToken ct)
        {
            float elapsed = 0f;
            Color c = txt.color;
            txt.color = new Color(c.r, c.g, c.b, start);

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                txt.color = new Color(c.r, c.g, c.b, Mathf.Lerp(start, end, elapsed / duration));
                await UniTask.Yield(PlayerLoopTiming.Update, ct);
            }
            txt.color = new Color(c.r, c.g, c.b, end);
        }

        private async UniTaskVoid BlinkAsync(CanvasGroup cg, float interval, CancellationToken ct)
        {
            try
            {
                bool isVisible = true;
                while (!ct.IsCancellationRequested)
                {
                    cg.alpha = isVisible ? 1f : 0f;
                    await UniTask.Delay(TimeSpan.FromSeconds(interval), cancellationToken: ct);
                    isVisible = !isVisible;
                }
            }
            catch (OperationCanceledException) { /* 정상적인 취소 처리 */ }
        }
    }
}