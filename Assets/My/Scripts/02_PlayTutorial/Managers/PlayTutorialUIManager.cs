using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Wonjeong.Data;
using Wonjeong.UI;

namespace My.Scripts._02_PlayTutorial.Managers
{
    public class PlayTutorialUIManager : MonoBehaviour
    {
        [Header("Popup UI")]
        [SerializeField] private CanvasGroup popup;
        [SerializeField] private Text popupText;

        [Header("Success UI")]
        [SerializeField] private Text centerText;

        [Header("Arrow UI")] 
        [SerializeField] private UIArrowAnimator p1RightArrow;
        [SerializeField] private UIArrowAnimator p2RightArrow;
        [SerializeField] private UIArrowAnimator p1LeftArrow;
        [SerializeField] private UIArrowAnimator p2LeftArrow;

        [Header("Gauge UI")] 
        [SerializeField] private GaugeController p1Gauge;
        [SerializeField] private GaugeController p2Gauge;

        [Header("Final Page UI")] 
        [SerializeField] private CanvasGroup finalPageCanvasGroup;
        [SerializeField] private Text finalPageText;

        public void InitUI(float maxDistance)
        {
            if (p1Gauge) p1Gauge.UpdateGauge(0, maxDistance);
            if (p2Gauge) p2Gauge.UpdateGauge(0, maxDistance);

            if (centerText != null) centerText.gameObject.SetActive(false);

            StopAllArrows();

            if (finalPageCanvasGroup != null)
            {
                finalPageCanvasGroup.alpha = 0f;
                finalPageCanvasGroup.gameObject.SetActive(false);
                finalPageCanvasGroup.blocksRaycasts = false;
            }
        }

        public void UpdateGauge(int playerIdx, float current, float max)
        {
            if (playerIdx == 0 && p1Gauge) p1Gauge.UpdateGauge(current, max);
            else if (playerIdx == 1 && p2Gauge) p2Gauge.UpdateGauge(current, max);
        }

        // --- Arrow Control ---
        private void StopAllArrows()
        {
            if (p1RightArrow) p1RightArrow.Stop();
            if (p2RightArrow) p2RightArrow.Stop();
            if (p1LeftArrow) p1LeftArrow.Stop();
            if (p2LeftArrow) p2LeftArrow.Stop();
        }

        public void PlayArrow(int playerIdx, bool isRight)
        {
            if (playerIdx == 0)
            {
                if (isRight && p1RightArrow) p1RightArrow.Play();
                else if (!isRight && p1LeftArrow) p1LeftArrow.Play();
            }
            else
            {
                if (isRight && p2RightArrow) p2RightArrow.Play();
                else if (!isRight && p2LeftArrow) p2LeftArrow.Play();
            }
        }

        public void StopArrowFadeOut(int playerIdx, bool isRight, float duration)
        {
            UIArrowAnimator target = null;
            if (playerIdx == 0) target = isRight ? p1RightArrow : p1LeftArrow;
            else target = isRight ? p2RightArrow : p2LeftArrow;

            if (target != null && target.gameObject.activeSelf)
                target.FadeOutAndStop(duration);
        }

        // --- Popup & Text Control (Tutorial Generic) ---

        public void ShowPopupImmediately(string text)
        {
            if (popupText) popupText.text = text;
            if (popup)
            {
                popup.alpha = 1;
                popup.blocksRaycasts = true;
            }
        }

        public void PreparePopup(string text)
        {
            if (popupText) popupText.text = text;
            if (popup)
            {
                popup.alpha = 0;
                popup.blocksRaycasts = true;
            }
        }

        public IEnumerator FadeInPopup(float duration)
        {   
            if (!popup) yield break;
            if (!popup.gameObject.activeInHierarchy) popup.gameObject.SetActive(true);
            yield return StartCoroutine(FadeCanvasGroup(popup, 0f, 1f, duration));
        }

        public void HidePopup(float duration)
        {
            if (!popup) return;
            StartCoroutine(FadeCanvasGroup(popup, popup.alpha, 0f, duration));
            popup.blocksRaycasts = false;
        }
        public IEnumerator FadeOutPopupTextAndChange(string newText, float fadeOutTime, float fadeInTime)
        {
            yield return StartCoroutine(FadeTextAlpha(popupText, 1f, 0f, fadeOutTime));
            if (popupText) popupText.text = newText;
            yield return StartCoroutine(FadeTextAlpha(popupText, 0f, 1f, fadeInTime));
        }

        public IEnumerator ShowSuccessText(string message, float duration)
        {
            if (!centerText) yield break;

            centerText.text = message;
            centerText.gameObject.SetActive(true);

            yield return StartCoroutine(FadeTextAlpha(centerText, 0f, 1f, 0.1f));
            yield return new WaitForSeconds(duration);
            yield return StartCoroutine(FadeTextAlpha(centerText, 1f, 0f, 0.1f));

            centerText.gameObject.SetActive(false);
        }

        public IEnumerator RunFinalPageSequence(TextSetting[] texts)
        {
            // 예외 처리
            if (!finalPageCanvasGroup || !finalPageText) yield break;
            if (texts == null || texts.Length == 0) yield break;

            // 1. 패널 활성화 (아직 투명한 상태)
            finalPageCanvasGroup.gameObject.SetActive(true);
            finalPageCanvasGroup.alpha = 0f;
            
            if (texts[0] != null)
            {
                if (UIManager.Instance) UIManager.Instance.SetText(finalPageText.gameObject, texts[0]);
                else finalPageText.text = texts[0].text;
            }

            // 텍스트를 투명하게 초기화 (페이드 인 효과를 위해)
            Color c = finalPageText.color;
            finalPageText.color = new Color(c.r, c.g, c.b, 0f);

            // 2. 페이지 페이드 인
            yield return StartCoroutine(FadeCanvasGroup(finalPageCanvasGroup, 0f, 1f, 0.1f));

            // 3. 텍스트 순차 재생
            foreach (var setting in texts)
            {
                if (setting == null) continue;

                // 텍스트 교체
                if (UIManager.Instance)
                {
                    UIManager.Instance.SetText(finalPageText.gameObject, setting);
                }
                else
                {
                    finalPageText.text = setting.text;
                }

                // 텍스트 페이드 인 (0 -> 1)
                yield return StartCoroutine(FadeTextAlpha(finalPageText, 0f, 1f, 0.1f));
        
                // 3초 대기
                yield return new WaitForSeconds(3.0f);

                // 텍스트 페이드 아웃 (1 -> 0)
                yield return StartCoroutine(FadeTextAlpha(finalPageText, 1f, 0f, 0.1f));
            }
        }

        // --- Utility Coroutines ---

        private IEnumerator FadeCanvasGroup(CanvasGroup cg, float start, float end, float duration)
        {
            if (!cg) yield break;
            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                cg.alpha = Mathf.Lerp(start, end, t / duration);
                yield return null;
            }

            cg.alpha = end;
            if (end <= 0f) cg.gameObject.SetActive(false);
        }

        private IEnumerator FadeTextAlpha(Text txt, float start, float end, float duration)
        {
            if (!txt) yield break;
            float t = 0f;
            Color c = txt.color;
            while (t < duration)
            {
                t += Time.deltaTime;
                float a = Mathf.Lerp(start, end, t / duration);
                txt.color = new Color(c.r, c.g, c.b, a);
                yield return null;
            }

            txt.color = new Color(c.r, c.g, c.b, end);
        }
    }
}