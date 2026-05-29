using System;
using System.Threading;
using Cysharp.Text;
using Cysharp.Threading.Tasks;
using Microsoft.Extensions.Logging;
using My.Scripts.Core;
using UnityEngine;
using UnityEngine.UI;
using VContainer;
using Wonjeong.Data;
using Wonjeong.UI;
using Wonjeong.Utils;
using ZLogger;

namespace My.Scripts._04_PlayLong
{
    [Serializable]
    public class IntroPageData
    {
        public TextSetting introText1;
        public TextSetting introText2;
        public TextSetting introText3;
    }

    public class Page_Intro : GamePage<IntroPageData>
    {
        [Header("UI References")]
        [SerializeField] private Text textIntro;

        private IntroPageData _data;
        private CancellationTokenSource _cts;

        private ILogger<Page_Intro> _logger;
        private SoundManager _soundManager;
        private UIManager _uiManager;
        private GameManager _gameManager;

        private const float FadeDuration = 1f;

        [Inject]
        public void Construct(ILogger<Page_Intro> logger, SoundManager soundManager, UIManager uiManager, GameManager gameManager)
        {
            _logger = logger;
            _soundManager = soundManager;
            _uiManager = uiManager;
            _gameManager = gameManager;
        }

        protected override void SetupData(IntroPageData data)
        {
            if (data == null)
            {
                _logger.ZLogWarning($"IntroPageData 누락됨.");
                return;
            }
            _data = data;
        }

        public override void OnEnter()
        {
            base.OnEnter();
            _cts = new CancellationTokenSource();

            if (textIntro) textIntro.supportRichText = true;
            else _logger?.ZLogWarning($"textIntro 컴포넌트 누락됨.");

            if (_gameManager) _gameManager.IsAutoProgressing = true;

            if (_data == null)
            {
                _logger?.ZLogError($"데이터가 설정되지 않음.");
                CompleteStep();
                return;
            }

            SetAlpha(1f);
            IntroSequenceAsync(_cts.Token).Forget();
        }

        private async UniTaskVoid IntroSequenceAsync(CancellationToken ct)
        {
            try
            {
                // 1. 첫 번째 문구
                if (_data.introText1 != null)
                {
                    _soundManager?.PlaySFX("공통_13");
                    _uiManager?.SetText(textIntro.gameObject, _data.introText1);
                    SetTextAlpha(1f);
                    await UniTask.Delay(TimeSpan.FromSeconds(2.0f), cancellationToken: ct);
                    await FadeTextAlphaAsync(1f, 0f, FadeDuration, ct);
                }

                // 2. 두 번째 문구 (줄바꿈 처리)
                if (_data.introText2 != null)
                {
                    string fullText = _data.introText2.text;
                    string[] lines = fullText.Split('\n');

                    if (lines.Length >= 2)
                    {
                        textIntro.text = $"{lines[0]}\n<color=#00000000>{lines[1]}</color>";
                        await FadeTextAlphaAsync(0f, 1f, FadeDuration, ct);
                        await UniTask.Delay(TimeSpan.FromSeconds(1.0f), cancellationToken: ct);
                        await FadeInSecondLineAsync(lines[0], lines[1], FadeDuration, ct);
                    }
                    else
                    {
                        _uiManager?.SetText(textIntro.gameObject, _data.introText2);
                        await FadeTextAlphaAsync(0f, 1f, FadeDuration, ct);
                    }
                }
                await UniTask.Delay(TimeSpan.FromSeconds(2.0f), cancellationToken: ct);
                await FadeTextAlphaAsync(1f, 0f, FadeDuration, ct);

                // 3. 마지막 문구
                if (_data.introText3 != null)
                {
                    _uiManager?.SetText(textIntro.gameObject, _data.introText3);
                }
                await FadeTextAlphaAsync(0f, 1f, FadeDuration, ct);
                await UniTask.Delay(TimeSpan.FromSeconds(2.0f), cancellationToken: ct);

                // 4. 종료 연출
                await FadeCanvasGroupAsync(1f, 0f, 0.5f, ct);
                CompleteStep();
            }
            catch (OperationCanceledException) { /* 정상 종료 */ }
        }

        private async UniTask FadeCanvasGroupAsync(float start, float end, float duration, CancellationToken ct)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                SetAlpha(Mathf.Lerp(start, end, elapsed / duration));
                await UniTask.Yield(PlayerLoopTiming.Update, ct);
            }
            SetAlpha(end);
        }

        private async UniTask FadeInSecondLineAsync(string line1, string line2, float duration, CancellationToken ct)
        {
            float elapsed = 0f;
            Color baseColor = textIntro.color;
            string hexRGB = ColorUtility.ToHtmlStringRGB(baseColor);

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float alpha = Mathf.Clamp01(elapsed / duration);

                using (var sb = ZString.CreateStringBuilder())
                {
                    sb.Append(line1);
                    sb.Append("\n<size=40><color=#");
                    sb.Append(hexRGB);
                    sb.Append(((int)(alpha * 255)).ToString("X2"));
                    sb.Append(">");
                    sb.Append(line2);
                    sb.Append("</color></size>");
                    textIntro.text = sb.ToString();
                }
                await UniTask.Yield(PlayerLoopTiming.Update, ct);
            }
            textIntro.text = $"{line1}\n<size=40>{line2}</size>";
        }

        private async UniTask FadeTextAlphaAsync(float start, float end, float duration, CancellationToken ct)
        {
            float elapsed = 0f;
            SetTextAlpha(start);
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                SetTextAlpha(Mathf.Lerp(start, end, elapsed / duration));
                await UniTask.Yield(PlayerLoopTiming.Update, ct);
            }
            SetTextAlpha(end);
        }

        private void SetTextAlpha(float alpha)
        {
            if (textIntro)
            {
                Color c = textIntro.color;
                c.a = alpha;
                textIntro.color = c;
            }
        }

        public override void OnExit()
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
            base.OnExit();
        }
    }
}