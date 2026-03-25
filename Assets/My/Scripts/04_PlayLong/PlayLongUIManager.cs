using System.Collections;
using My.Scripts.UI;
using UnityEngine;
using UnityEngine.UI;
using Wonjeong.Data;
using Wonjeong.UI;
using Wonjeong.Utils;

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
        [SerializeField] private string[] formattedTextNames = new string[] { "PopupText_4" };

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

        [Header("Side HUD - Distance Markers")]
        [SerializeField] private Image[] p1DistMarkers;
        [SerializeField] private Image[] p2DistMarkers;

        [Header("Marker Assets")]
        [SerializeField] private Sprite[] originalMarkerSprites;
        [SerializeField] private Sprite heartFragmentSprite;

        private static readonly Vector2 OriginalMarkerSize = new Vector2(85f, 35f);
        private static readonly Vector2 HeartFragmentSize = new Vector2(144f, 138f);

        private string _originalFullText;
        private Coroutine _textBlinkCoroutine;
        
        private int _lastActiveMarkerCount;
        
        private Color _defaultTimerColor = Color.white;
        private bool _isTimerColorSaved;
        
        private Color _defaultTimerIconColor = Color.white;
        private bool _isTimerIconColorSaved;

        /// <summary>
        /// UI 컴포넌트들의 초기 상태를 설정.
        /// </summary>
        /// <param name="maxDistance">목표 최대 거리</param>
        public void InitUI(float maxDistance)
        {
            if (popup)
            {
                popup.alpha = 0f;
                popup.gameObject.SetActive(false);
                popup.blocksRaycasts = true;
            }

            if (redStringCanvasGroup)
            {
                redStringCanvasGroup.alpha = 0f;
            }

            if (centerText) centerText.gameObject.SetActive(false);
            
            if (timerText) 
            {
                timerText.text = "60";
                if (_isTimerColorSaved)
                {
                    timerText.color = _defaultTimerColor;
                }
            }
            else
            {
                Debug.LogWarning("[PlayLongUIManager] timerText 컴포넌트 누락");
            }

            if (timerIconImage)
            {
                if (_isTimerIconColorSaved)
                {
                    timerIconImage.color = _defaultTimerIconColor;
                }
            }
            else
            {
                Debug.LogWarning("[PlayLongUIManager] timerIconImage 컴포넌트 누락");
            }

            if (p1LongGauge) p1LongGauge.ResetGauge();
            if (p2LongGauge) p2LongGauge.ResetGauge();

            if (p1SideDistCg) p1SideDistCg.alpha = 0f;
            if (p2SideDistCg) p2SideDistCg.alpha = 0f;
            if (padImagesCg) padImagesCg.alpha = 1f;

            _lastActiveMarkerCount = 0;
            ResetDistMarkers();
        }

        public void SetPlayerNames(string nameA, string nameB, TextSetting settingA, TextSetting settingB)
        {
            UIUtils.ApplyPlayerNames(p1NameText, p2NameText, nameA, nameB, settingA, settingB);
        }

        public void SetPlayerBalls(Sprite spriteA, Sprite spriteB)
        {
            if (ballImageA)
            {
                if (spriteA) ballImageA.sprite = spriteA;
                else Debug.LogWarning("[PlayLongUIManager] Player A 컬러 스프라이트(spriteA)가 누락되어 기본 이미지를 유지합니다.");
            }
            else Debug.LogWarning("[PlayLongUIManager] ballImageA 컴포넌트가 연결되지 않았습니다.");

            if (ballImageB)
            {
                if (spriteB) ballImageB.sprite = spriteB;
                else Debug.LogWarning("[PlayLongUIManager] Player B 컬러 스프라이트(spriteB)가 누락되어 기본 이미지를 유지합니다.");
            }
            else Debug.LogWarning("[PlayLongUIManager] ballImageB 컴포넌트가 연결되지 않았습니다.");
        }

        private void ResetDistMarkers()
        {
            if (p1DistMarkers == null || p2DistMarkers == null) return;

            int originLen = originalMarkerSprites != null ? originalMarkerSprites.Length : 0;
            int maxLen = Mathf.Min(p1DistMarkers.Length, p2DistMarkers.Length, originLen);

            for (int i = 0; i < maxLen; i++)
            {
                if (originalMarkerSprites != null)
                {
                    Sprite origin = originalMarkerSprites[i];
                    if (origin)
                    {
                        if (p1DistMarkers[i]) UpdateMarkerAppearance(p1DistMarkers[i], origin, OriginalMarkerSize);
                        if (p2DistMarkers[i]) UpdateMarkerAppearance(p2DistMarkers[i], origin, OriginalMarkerSize);
                    }
                }
            }
        }

        public void UpdateDistanceMarkers(float currentDist)
        {
            if (p1DistMarkers == null || p2DistMarkers == null) return;

            int activeCount = Mathf.FloorToInt(currentDist / 100f);
            
            if (activeCount > _lastActiveMarkerCount)
            {
                if (SoundManager.Instance) SoundManager.Instance.PlaySFX("달리기_3");
                _lastActiveMarkerCount = activeCount;
            }
            
            int len = Mathf.Min(p1DistMarkers.Length, p2DistMarkers.Length);
            
            for (int i = 0; i < len; i++)
            {
                if (i < activeCount)
                {
                    if (p1DistMarkers[i] && p1DistMarkers[i].sprite != heartFragmentSprite)
                        UpdateMarkerAppearance(p1DistMarkers[i], heartFragmentSprite, HeartFragmentSize);

                    if (p2DistMarkers[i] && p2DistMarkers[i].sprite != heartFragmentSprite)
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
                if (UIManager.Instance)
                    UIManager.Instance.SetText(centerText.gameObject, setting);
                else
                    centerText.text = setting.text;
            }
        }

        public IEnumerator ShowPopupSequence(TextSetting[] textDatas, float durationPerText, bool hideAtEnd = true)
        {
            if (!popup || !popupText || textDatas == null || textDatas.Length == 0) yield break;

            Color c = popupText.color;
            c.a = 0f;
            popupText.color = c;

            popup.gameObject.SetActive(true);
            popupText.supportRichText = true;
            
            yield return StartCoroutine(UIUtils.FadeCanvasGroup(popup, popup.alpha, 1f, 0.5f));

            for (int i = 0; i < textDatas.Length; i++)
            {
                TextSetting textData = textDatas[i];
                if (textData == null) continue;

                bool applySpecialFormat = false;

                if (formattedTextNames != null && !string.IsNullOrEmpty(textData.name))
                {
                    foreach (string targetName in formattedTextNames)
                    {
                        if (textData.name == targetName)
                        {
                            applySpecialFormat = true;
                            break;
                        }
                    }
                }

                if (applySpecialFormat)
                {
                    string[] lines = textData.text.Split('\n');
                    if (lines.Length >= 2)
                    {
                        popupText.text = $"{lines[0]}\n<size=40>{lines[1]}</size>";
                    }
                    else
                    {
                        popupText.text = textData.text;
                    }
                }
                else
                {
                    if (UIManager.Instance) UIManager.Instance.SetText(popupText.gameObject, textData);
                    else popupText.text = textData.text;
                }

                yield return StartCoroutine(FadeTextAlpha(popupText, 0f, 1f, 0.25f));
                yield return CoroutineData.GetWaitForSeconds(durationPerText);

                if (i < textDatas.Length - 1 || hideAtEnd)
                {
                    yield return StartCoroutine(FadeTextAlpha(popupText, 1f, 0f, 0.25f));
                }
            }

            if (hideAtEnd)
            {
                yield return StartCoroutine(UIUtils.FadeCanvasGroup(popup, 1f, 0f, 0.5f));

                popup.gameObject.SetActive(false);
            }
        }

        public void StartPopupTextBlinking(float interval = 0.5f)
        {
            if (!popupText) return;

            _originalFullText = popupText.text;

            if (_textBlinkCoroutine != null) StopCoroutine(_textBlinkCoroutine);
            _textBlinkCoroutine = StartCoroutine(BlinkSecondLineRoutine(interval));
        }

        public void StopPopupTextBlinking()
        {
            if (_textBlinkCoroutine != null)
            {
                StopCoroutine(_textBlinkCoroutine);
                _textBlinkCoroutine = null;
            }

            if (popupText && !string.IsNullOrEmpty(_originalFullText))
            {
                popupText.text = _originalFullText;
            }
        }

        private IEnumerator BlinkSecondLineRoutine(float interval)
        {
            if (!popupText) yield break;

            string[] lines = _originalFullText.Split('\n');
            if (lines.Length < 2) yield break;

            bool isVisible = true;
            while (true)
            {
                if (isVisible)
                {
                    popupText.text = _originalFullText;
                }
                else
                {
                    popupText.text = $"{lines[0]}\n<color=#00000000>{lines[1]}</color>";
                }

                yield return CoroutineData.GetWaitForSeconds(interval);

                isVisible = !isVisible;
            }
        }

        public IEnumerator ShowRedStringStep1(TextSetting textData)
        {
            if (textData == null || !popup || !popupText) yield break;

            popupText.supportRichText = true;
            string fullText = textData.text;
            string[] lines = fullText.Split('\n');

            if (lines.Length >= 2) popupText.text = $"{lines[0]}\n<color=#00000000>{lines[1]}</color>";
            else popupText.text = fullText;

            yield return StartCoroutine(FadeTextAlpha(popupText, 0f, 1f, 0.25f));

            if (redStringCanvasGroup) yield return StartCoroutine(UIUtils.FadeCanvasGroup(redStringCanvasGroup, 0f, 1f, 2.0f));
        }

        public IEnumerator FadeInSecondLine(TextSetting textData, float duration)
        {
            if (textData == null || !popupText) yield break;

            string fullText = textData.text;
            string[] lines = fullText.Split('\n');
            if (lines.Length < 2) yield break;

            float elapsed = 0f;
            Color originColor = popupText.color;
            string hexColor = ColorUtility.ToHtmlStringRGB(originColor);

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float alpha = Mathf.Clamp01(elapsed / duration);
                string alphaHex = Mathf.RoundToInt(alpha * 255f).ToString("X2");
                popupText.text = $"{lines[0]}\n<size=40><color=#{hexColor}{alphaHex}>{lines[1]}</color></size>";
                yield return null;
            }

            popupText.text = $"{lines[0]}\n<size=40>{lines[1]}</size>";
        }

        public IEnumerator BlinkRedString(int count, float duration)
        {
            if (!redStringCanvasGroup) yield break;
            if (SoundManager.Instance) SoundManager.Instance.PlaySFX("달리기_6");

            float waitTime = duration / (count * 2);
            for (int i = 0; i < count; i++)
            {
                redStringCanvasGroup.alpha = 0f;
                yield return CoroutineData.GetWaitForSeconds(waitTime);

                redStringCanvasGroup.alpha = 1f;
                yield return CoroutineData.GetWaitForSeconds(waitTime);
            }
        }

        /// <summary>
        /// 화면에 타이머 시간을 갱신.
        /// </summary>
        /// <param name="time">남은 시간</param>
        public void UpdateTimer(float time)
        {
            if (timerText)
            {
                if (!_isTimerColorSaved)
                {
                    _defaultTimerColor = timerText.color;
                    _isTimerColorSaved = true;
                }

                timerText.text = Mathf.CeilToInt(Mathf.Max(0f, time)).ToString();

                // 남은 시간이 5초 이하일 때 유저에게 직관적인 경고 효과를 주기 위해 텍스트 색상을 빨간색으로 변경함
                if (time <= 5f)
                {
                    timerText.color = Color.red;
                }
                else
                {
                    timerText.color = _defaultTimerColor;
                }
            }

            if (timerIconImage)
            {
                if (!_isTimerIconColorSaved)
                {
                    _defaultTimerIconColor = timerIconImage.color;
                    _isTimerIconColorSaved = true;
                }

                // 시계 아이콘도 텍스트와 함께 빨간색으로 변경하여 경고 시인성을 높임
                if (time <= 5f)
                {
                    timerIconImage.color = Color.red;
                }
                else
                {
                    timerIconImage.color = _defaultTimerIconColor;
                }
            }
        }

        private IEnumerator FadeTextAlpha(Text txt, float start, float end, float duration)
        {
            float t = 0f;
            Color c = txt.color;
            while (t < duration)
            {
                t += Time.deltaTime;
                txt.color = new Color(c.r, c.g, c.b, Mathf.Lerp(start, end, t / duration));
                yield return null;
            }

            txt.color = new Color(c.r, c.g, c.b, end);
        }

        public void HideQuestionPopup(float duration)
        {
            if (popup && popup.gameObject.activeInHierarchy)
            {
                popup.blocksRaycasts = false;
                StartCoroutine(HideQuestionPopupRoutine(duration));
            }
        }

        private IEnumerator HideQuestionPopupRoutine(float duration)
        {
            yield return StartCoroutine(UIUtils.FadeCanvasGroup(popup, popup.alpha, 0f, duration));

            popup.gameObject.SetActive(false);
            popup.blocksRaycasts = true;
        }

        public IEnumerator FadeTransitionTutorialReady(float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float progress = Mathf.Clamp01(elapsed / duration);

                if (p1SideDistCg) p1SideDistCg.alpha = progress;
                if (p2SideDistCg) p2SideDistCg.alpha = progress;

                if (padImagesCg) padImagesCg.alpha = 1f - progress;

                yield return null;
            }

            if (p1SideDistCg) p1SideDistCg.alpha = 1f;
            if (p2SideDistCg) p2SideDistCg.alpha = 1f;
            if (padImagesCg) padImagesCg.alpha = 0f;
        }

        public void UpdateLongCoopGauge(float current, float max)
        {
            if (p1LongGauge) p1LongGauge.UpdateGauge(current, max);
            if (p2LongGauge) p2LongGauge.UpdateGauge(current, max);
        }

        public void ShowCenterResultPopup(TextSetting textData)
        {
            if (!popup || !popupText || textData == null) return;

            popup.gameObject.SetActive(true);
            popup.alpha = 1f;
            popup.blocksRaycasts = true;

            Color c = popupText.color;
            c.a = 1f;
            popupText.color = c;

            if (UIManager.Instance)
                UIManager.Instance.SetText(popupText.gameObject, textData);
            else
                popupText.text = textData.text;
        }

        public void ShowCenterResultPopup(string message)
        {
            if (!popup || !popupText) return;

            popup.gameObject.SetActive(true);
            popup.alpha = 1f;
            popup.blocksRaycasts = true;

            Color c = popupText.color;
            c.a = 1f;
            popupText.color = c;

            popupText.text = message;
        }
    }
}