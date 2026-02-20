using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Wonjeong.Data;
using Wonjeong.UI;
using Wonjeong.Utils;

namespace My.Scripts._04_PlayLong
{
    public class PlayLongUIManager : MonoBehaviour
    {
        [Header("Formatting Settings")]
        [SerializeField] private string[] formattedTextNames = new string[] { "PopupText_4" };

        [Header("Popup")]
        [SerializeField] private CanvasGroup popup; 
        [SerializeField] private Text popupText;    

        [Header("HUD")]
        [SerializeField] private Text centerText;
        [SerializeField] private Text timerText;
        [SerializeField] private CanvasGroup padImagesCG;
        
        [Header("Red String Animation")]
        [SerializeField] private CanvasGroup redStringCanvasGroup;
        
        [Header("Side HUD")]
        [SerializeField] private PlayLongGaugeController p1LongGauge; 
        [SerializeField] private PlayLongGaugeController p2LongGauge;
        [SerializeField] private CanvasGroup p1SideDistCG; 
        [SerializeField] private CanvasGroup p2SideDistCG; 

        [Header("Side HUD - Distance Markers")]
        [SerializeField] private Image[] p1DistMarkers; 
        [SerializeField] private Image[] p2DistMarkers; 

        [Header("Marker Assets")]
        [SerializeField] private Sprite[] originalMarkerSprites; 
        [SerializeField] private Sprite heartFragmentSprite; 

        private readonly static Vector2 OriginalMarkerSize = new Vector2(85f, 35f);
        private readonly static Vector2 HeartFragmentSize = new Vector2(144f, 138f);
        
        private string _originalFullText;
        private Coroutine _textBlinkCoroutine;

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
            if (timerText) timerText.text = "";
            
            if (p1LongGauge) p1LongGauge.ResetGauge();
            if (p2LongGauge) p2LongGauge.ResetGauge();
            
            if (p1SideDistCG) p1SideDistCG.alpha = 0f;
            if (p2SideDistCG) p2SideDistCG.alpha = 0f;
            if (padImagesCG) padImagesCG.alpha = 1f;

            ResetDistMarkers();
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

            popup.gameObject.SetActive(true);
            popupText.supportRichText = true;

            yield return StartCoroutine(FadeCanvasGroup(popup, popup.alpha, 1f, 0.5f));

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

                yield return StartCoroutine(FadeTextAlpha(popupText, 0f, 1f, 0.5f));
                yield return CoroutineData.GetWaitForSeconds(durationPerText);
        
                if (i < textDatas.Length - 1 || hideAtEnd)
                {
                    yield return StartCoroutine(FadeTextAlpha(popupText, 1f, 0f, 0.5f));
                }
            }

            if (hideAtEnd)
            {
                yield return StartCoroutine(FadeCanvasGroup(popup, 1f, 0f, 0.5f));
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

            bool isVisible = false; 
            while (true)
            {
                isVisible = !isVisible;
                if (isVisible)
                {
                    popupText.text = _originalFullText;
                }
                else
                {
                    popupText.text = $"{lines[0]}\n<color=#00000000>{lines[1]}</color>";
                }
                yield return CoroutineData.GetWaitForSeconds(interval);
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

            yield return StartCoroutine(FadeTextAlpha(popupText, 0f, 1f, 0.5f));
            if (redStringCanvasGroup) yield return StartCoroutine(FadeCanvasGroup(redStringCanvasGroup, 0f, 1f, 2.0f));
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
            float waitTime = duration / (count * 2);
            for (int i = 0; i < count; i++)
            {
                redStringCanvasGroup.alpha = 0f;
                yield return CoroutineData.GetWaitForSeconds(waitTime);
                redStringCanvasGroup.alpha = 1f;
                yield return CoroutineData.GetWaitForSeconds(waitTime);
            }
        }

        public void UpdateTimer(float time)
        {
            if (timerText) timerText.text = Mathf.CeilToInt(Mathf.Max(0f, time)).ToString();
        }

        private IEnumerator FadeCanvasGroup(CanvasGroup cg, float start, float end, float duration)
        {
            float t = 0f;
            cg.alpha = start;
            while (t < duration)
            {
                t += Time.deltaTime;
                cg.alpha = Mathf.Lerp(start, end, t / duration);
                yield return null;
            }
            cg.alpha = end;
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
            yield return StartCoroutine(FadeCanvasGroup(popup, popup.alpha, 0f, duration));
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

                if (p1SideDistCG) p1SideDistCG.alpha = progress;
                if (p2SideDistCG) p2SideDistCG.alpha = progress;

                if (padImagesCG) padImagesCG.alpha = 1f - progress; 
        
                yield return null;
            }
    
            if (p1SideDistCG) p1SideDistCG.alpha = 1f;
            if (p2SideDistCG) p2SideDistCG.alpha = 1f;
            if (padImagesCG) padImagesCG.alpha = 0f;
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