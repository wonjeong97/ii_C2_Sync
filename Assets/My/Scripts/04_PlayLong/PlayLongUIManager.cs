using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Microsoft.Extensions.Logging;
using My.Scripts.UI;
using UnityEngine;
using UnityEngine.UI;
using VContainer;
using Wonjeong.Data;
using Wonjeong.UI;
using ZLogger;

namespace My.Scripts._04_PlayLong
{
    public class PlayLongUIManager : MonoBehaviour
    {
        [Header("Player Name UI")]
        [SerializeField] private Text p1NameText;
        [SerializeField] private Text p2NameText;

        [Header("Player Color Balls")]
        [SerializeField] private Image ballImageA;
        [SerializeField] private Image ballImageB;

        [Header("Formatting Settings")]
        [SerializeField] private string[] formattedTextNames = { "PopupText_4" };

        [Header("Popup")]
        [SerializeField] private CanvasGroup popup;
        [SerializeField] private Text popupText;

        [Header("HUD")]
        [SerializeField] private Text centerText;
        [SerializeField] private Text timerText;
        [SerializeField] private Image timerIconImage;
        [SerializeField] private CanvasGroup padImagesCg;

        [Header("Red String Animation")]
        [SerializeField] private CanvasGroup redStringCanvasGroup;

        [Header("Side HUD")]
        [SerializeField] private PlayLongGaugeController p1LongGauge;
        [SerializeField] private PlayLongGaugeController p2LongGauge;
        [SerializeField] private CanvasGroup p1SideDistCg;
        [SerializeField] private CanvasGroup p2SideDistCg;

        [Header("Marker Assets")]
        [SerializeField] private Image[] p1DistMarkers;
        [SerializeField] private Image[] p2DistMarkers;
        [SerializeField] private Sprite[] originalMarkerSprites;
        [SerializeField] private Sprite heartFragmentSprite;

        private readonly static Vector2 OriginalMarkerSize = new Vector2(85f, 35f);
        private readonly static Vector2 HeartFragmentSize = new Vector2(144f, 138f);

        private CancellationTokenSource _cts;
        private CancellationTokenSource _blinkCts;
        private string _originalFullText;
        private int _lastActiveMarkerCount;
        private Color _defaultTimerColor = Color.white;
        private Color _defaultTimerIconColor = Color.white;

        private ILogger<PlayLongUIManager> _logger;
        private SoundManager _soundManager;
        private UIManager _uiManager;

        [Inject]
        public void Construct(ILogger<PlayLongUIManager> logger, SoundManager soundManager, UIManager uiManager)
        {
            _logger = logger;
            _soundManager = soundManager;
            _uiManager = uiManager;
        }

        private void OnDestroy()
        {
            _blinkCts?.Cancel();
            _blinkCts?.Dispose();
            _cts?.Cancel();
            _cts?.Dispose();
        }

        public void InitUI(float maxDistance)
        {
            _cts?.Cancel();
            _cts = new CancellationTokenSource();
            if (popup)
            {
                popup.alpha = 0f;
                popup.gameObject.SetActive(false);
                popup.blocksRaycasts = true;
            }

            if (redStringCanvasGroup) redStringCanvasGroup.alpha = 0f;
            if (centerText) centerText.gameObject.SetActive(false);
            _defaultTimerColor = timerText ? timerText.color : Color.white;
            _defaultTimerIconColor = timerIconImage ? timerIconImage.color : Color.white;
            if (p1LongGauge) p1LongGauge.ResetGauge();
            if (p2LongGauge) p2LongGauge.ResetGauge();
            if (p1SideDistCg) p1SideDistCg.alpha = 0f;
            if (p2SideDistCg) p2SideDistCg.alpha = 0f;
            if (padImagesCg) padImagesCg.alpha = 1f;
            _lastActiveMarkerCount = 0;
        }

        public void SetPlayerNames(string nameA, string nameB, TextSetting settingA, TextSetting settingB)
        {
            UIUtils.ApplyPlayerNames(_uiManager, p1NameText, p2NameText, nameA, nameB, settingA, settingB);
        }

        public void SetPlayerBalls(Sprite spriteA, Sprite spriteB)
        {
            if (ballImageA && spriteA) ballImageA.sprite = spriteA;
            if (ballImageB && spriteB) ballImageB.sprite = spriteB;
        }

        public void UpdateTimer(float time)
        {
            if (timerText)
            {
                timerText.text = Mathf.CeilToInt(Mathf.Max(0f, time)).ToString();
                timerText.color = (time <= 5f) ? Color.red : _defaultTimerColor;
            }

            if (timerIconImage) timerIconImage.color = (time <= 5f) ? Color.red : _defaultTimerIconColor;
        }

        public void UpdateLongCoopGauge(float current, float max)
        {
            p1LongGauge?.UpdateGauge(current, max);
            p2LongGauge?.UpdateGauge(current, max);
        }

        public void UpdateDistanceMarkers(float currentDist)
        {
            int activeCount = Mathf.FloorToInt(currentDist / 100f);
            if (activeCount > _lastActiveMarkerCount)
            {
                _soundManager?.PlaySFX("달리기_3");
                _lastActiveMarkerCount = activeCount;
            }

            for (int i = 0; i < Mathf.Min(p1DistMarkers.Length, p2DistMarkers.Length); i++)
            {
                if (i < activeCount)
                {
                    UpdateMarkerAppearance(p1DistMarkers[i], heartFragmentSprite, HeartFragmentSize);
                    UpdateMarkerAppearance(p2DistMarkers[i], heartFragmentSprite, HeartFragmentSize);
                }
            }
        }

        private void UpdateMarkerAppearance(Image targetImg, Sprite sprite, Vector2 size)
        {
            if (!targetImg || !sprite) return;

            targetImg.sprite = sprite;
            targetImg.rectTransform.sizeDelta = size;
        }

        /// <summary>
        /// 팝업 시퀀스에 매핑되는 데이터를 순차적으로 출력.
        /// </summary>
        public async UniTask ShowPopupSequenceAsync(TextSetting[] textDatas, float durationPerText,
            bool hideAtEnd = true, CancellationToken ct = default)
        {
            if (!popup || !popupText || textDatas == null || textDatas.Length == 0) return;

            popupText.color = new Color(popupText.color.r, popupText.color.g, popupText.color.b, 0f);
            popup.gameObject.SetActive(true);
            await UIUtils.FadeCanvasGroupAsync(popup, popup.alpha, 1f, 0.5f, ct);
            for (int i = 0; i < textDatas.Length; i++)
            {
                TextSetting data = textDatas[i];
                bool isSpecial = Array.Exists(formattedTextNames, targetName => targetName == data.name);
                if (isSpecial)
                {
                    string[] lines = data.text.Split('\n');
                    popupText.text = lines.Length >= 2 ? $"{lines[0]}\n<size=40>{lines[1]}</size>" : data.text;
                }
                else _uiManager?.SetText(popupText.gameObject, data);

                await FadeTextAlphaAsync(popupText, 0f, 1f, 0.25f, ct);
                await UniTask.Delay(TimeSpan.FromSeconds(durationPerText), cancellationToken: ct);
                if (i < textDatas.Length - 1 || hideAtEnd) await FadeTextAlphaAsync(popupText, 1f, 0f, 0.25f, ct);
            }

            if (hideAtEnd)
            {
                await UIUtils.FadeCanvasGroupAsync(popup, 1f, 0f, 0.5f, ct);
                popup.gameObject.SetActive(false);
            }
        }

        public async UniTaskVoid StartPopupTextBlinkingAsync(float interval, CancellationToken ct)
        {
            StopPopupTextBlinking(); // 혹시 모를 중복 실행 방지
            
            // 전달받은 메인 토큰과 연동되는 깜빡임 전용 토큰 생성
            _blinkCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            CancellationToken blinkToken = _blinkCts.Token;

            _originalFullText = popupText.text;
            string[] lines = _originalFullText.Split('\n');
            if (lines.Length < 2) return;

            bool isVisible = true;
            try
            {
                while (!blinkToken.IsCancellationRequested)
                {
                    if (!popupText) break;
                    popupText.text = isVisible ? _originalFullText : $"{lines[0]}\n<color=#00000000>{lines[1]}</color>";
                    await UniTask.Delay(TimeSpan.FromSeconds(interval), cancellationToken: blinkToken);
                    isVisible = !isVisible;
                }
            }
            catch (OperationCanceledException)
            {
            }
        }

        public void StopPopupTextBlinking()
        {
            if (_blinkCts != null)
            {
                _blinkCts.Cancel();
                _blinkCts.Dispose();
                _blinkCts = null;
            }

            if (popupText && !string.IsNullOrEmpty(_originalFullText)) 
            {
                popupText.text = _originalFullText;
            }
        }

        public async UniTask ShowRedStringStep1Async(TextSetting textData, CancellationToken ct)
        {
            popupText.supportRichText = true;
            string[] lines = textData.text.Split('\n');
            popupText.text = lines.Length >= 2 ? $"{lines[0]}\n<color=#00000000>{lines[1]}</color>" : textData.text;
            await FadeTextAlphaAsync(popupText, 0f, 1f, 0.25f, ct);
            if (redStringCanvasGroup) await UIUtils.FadeCanvasGroupAsync(redStringCanvasGroup, 0f, 1f, 2.0f, ct);
        }

        public async UniTask FadeInSecondLineAsync(TextSetting textData, float duration, CancellationToken ct)
        {
            string[] lines = textData.text.Split('\n');
            if (lines.Length < 2) return;

            float elapsed = 0f;
            string hexColor = ColorUtility.ToHtmlStringRGB(popupText.color);
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float alpha = Mathf.Clamp01(elapsed / duration);
                popupText.text =
                    $"{lines[0]}\n<size=40><color=#{hexColor}{(int)(alpha * 255):X2}>{lines[1]}</color></size>";
                await UniTask.Yield(PlayerLoopTiming.Update, ct);
            }

            popupText.text = $"{lines[0]}\n<size=40>{lines[1]}</size>";
        }

        public async UniTask BlinkRedStringAsync(int count, float duration, CancellationToken ct)
        {
            if (!redStringCanvasGroup) return;

            _soundManager?.PlaySFX("달리기_6");
            float waitTime = duration / (count * 2);
            for (int i = 0; i < count; i++)
            {
                redStringCanvasGroup.alpha = 0f;
                await UniTask.Delay(TimeSpan.FromSeconds(waitTime), cancellationToken: ct);
                redStringCanvasGroup.alpha = 1f;
                await UniTask.Delay(TimeSpan.FromSeconds(waitTime), cancellationToken: ct);
            }
        }

        private async UniTask FadeTextAlphaAsync(Text txt, float start, float end, float duration, CancellationToken ct)
        {
            float t = 0f;
            Color c = txt.color;
            while (t < duration)
            {
                t += Time.deltaTime;
                txt.color = new Color(c.r, c.g, c.b, Mathf.Lerp(start, end, t / duration));
                await UniTask.Yield(PlayerLoopTiming.Update, ct);
            }

            txt.color = new Color(c.r, c.g, c.b, end);
        }
        
        /// <summary>
        /// 결과 팝업을 표시하고 지정된 시간 후 페이드아웃함
        /// </summary>
        public async UniTask ShowCenterResultPopupAsync(TextSetting textData, float duration, CancellationToken ct)
        {
            if (!popup || !popupText || textData == null) return;

            if (_uiManager) _uiManager.SetText(popupText.gameObject, textData);
            else popupText.text = textData.text;

            popup.gameObject.SetActive(true);
            popup.blocksRaycasts = true;
            popupText.color = new Color(popupText.color.r, popupText.color.g, popupText.color.b, 1f);
            
            // 0.5초 페이드 인 -> duration 대기 -> 0.5초 페이드 아웃
            await UIUtils.FadeCanvasGroupAsync(popup, 0f, 1f, 0.5f, ct);
            await UniTask.Delay(TimeSpan.FromSeconds(duration), cancellationToken: ct);
            await UIUtils.FadeCanvasGroupAsync(popup, 1f, 0f, 0.5f, ct, isFadeIn: false);
        }

        /// <summary>
        /// 결과 팝업을 표시하고 지정된 시간 후 페이드아웃함
        /// </summary>
        public async UniTask ShowCenterResultPopupAsync(string message, float duration, CancellationToken ct)
        {
            if (!popup || !popupText) return;

            popupText.text = message;

            popup.gameObject.SetActive(true);
            popup.blocksRaycasts = true;
            popupText.color = new Color(popupText.color.r, popupText.color.g, popupText.color.b, 1f);
            
            await UIUtils.FadeCanvasGroupAsync(popup, 0f, 1f, 0.5f, ct);
            await UniTask.Delay(TimeSpan.FromSeconds(duration), cancellationToken: ct);
            await UIUtils.FadeCanvasGroupAsync(popup, 1f, 0f, 0.5f, ct, isFadeIn: false);
        }

        public void SetCenterText(string message, bool isActive)
        {
            if (centerText)
            {
                centerText.text = message;
                centerText.gameObject.SetActive(isActive);
            }
        }

        public void SetCenterText(TextSetting setting)
        {
            if (centerText && setting != null)
            {
                centerText.gameObject.SetActive(true);
                if (_uiManager) _uiManager.SetText(centerText.gameObject, setting);
                else centerText.text = setting.text;
            }
        }

        // --- 새로 추가된 메서드들 (심볼 해결용) ---

        public async UniTask FadeTransitionTutorialReadyAsync(float duration, CancellationToken ct)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float progress = Mathf.Clamp01(elapsed / duration);
                if (p1SideDistCg) p1SideDistCg.alpha = progress;
                if (p2SideDistCg) p2SideDistCg.alpha = progress;
                if (padImagesCg) padImagesCg.alpha = 1f - progress;
                await UniTask.Yield(PlayerLoopTiming.Update, ct);
            }

            if (p1SideDistCg) p1SideDistCg.alpha = 1f;
            if (p2SideDistCg) p2SideDistCg.alpha = 1f;
            if (padImagesCg) padImagesCg.alpha = 0f;
        }

        public void HideQuestionPopup(float duration)
        {
            if (popup && popup.gameObject.activeInHierarchy)
            {
                popup.blocksRaycasts = false;
                UIUtils.FadeCanvasGroupAsync(popup, popup.alpha, 0f, duration, _cts.Token, isFadeIn: false).Forget();
            }
        }

        public async UniTask ShowMissionPopupAsync(TextSetting text, float duration, CancellationToken ct)
        {
            if (!popup || !popupText) return;

            _uiManager?.SetText(popupText.gameObject, text);
            popup.gameObject.SetActive(true);
            await UIUtils.FadeCanvasGroupAsync(popup, popup.alpha, 1f, 0.5f, ct);
            await UniTask.Delay(TimeSpan.FromSeconds(duration), cancellationToken: ct);
            await UIUtils.FadeCanvasGroupAsync(popup, 1f, 0f, 0.5f, ct, isFadeIn: false);
        }
        
        // ==========================================
        // 팝업을 띄우기만 하고 유지
        // ==========================================
        public async UniTask ShowMissionPopupKeepAsync(TextSetting text, CancellationToken ct)
        {
            if (!popup || !popupText) return;

            _uiManager?.SetText(popupText.gameObject, text);
            popup.gameObject.SetActive(true);
            await UIUtils.FadeCanvasGroupAsync(popup, popup.alpha, 1f, 0.5f, ct);
        }

        // ==========================================
        // 유지되고 있는 팝업을 닫음
        // ==========================================
        public async UniTask HideMissionPopupAsync(float duration, CancellationToken ct)
        {
            if (!popup) return;
            
            popup.blocksRaycasts = false;
            await UIUtils.FadeCanvasGroupAsync(popup, popup.alpha, 0f, duration, ct, isFadeIn: false);
        }
    }
}