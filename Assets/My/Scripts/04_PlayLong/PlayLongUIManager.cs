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
        [Header("Popup")]
        [SerializeField] private CanvasGroup popup; 
        [SerializeField] private Text popupText;    

        [Header("HUD")]
        [SerializeField] private Text timerText;
        [SerializeField] private GaugeController p1Gauge; 
        [SerializeField] private GaugeController p2Gauge; 

        public void InitUI(float maxDistance)
        {
            if (popup)
            {
                popup.alpha = 0f;
                popup.gameObject.SetActive(false);
            }

            if (p1Gauge) p1Gauge.UpdateGauge(0, maxDistance);
            if (p2Gauge) p2Gauge.UpdateGauge(0, maxDistance);
            if (timerText) timerText.text = "";
        }

        /// <summary>
        /// 텍스트 배열을 순차적으로 보여주는 팝업 시퀀스
        /// </summary>
        public IEnumerator ShowPopupSequence(TextSetting[] textDatas, float durationPerText)
        {
            if (!popup || !popupText) yield break;
            if (textDatas == null || textDatas.Length == 0) yield break;

            // 1. 팝업 배경 페이드 인
            popup.gameObject.SetActive(true);
            popupText.color = new Color(popupText.color.r, popupText.color.g, popupText.color.b, 0f);
            
            yield return StartCoroutine(FadeCanvasGroup(popup, 0f, 1f, 0.5f));

            // 2. 텍스트 배열 순회
            foreach (var textData in textDatas)
            {
                if (textData == null) continue;

                if (UIManager.Instance != null) 
                    UIManager.Instance.SetText(popupText.gameObject, textData);
                else 
                    popupText.text = textData.text;

                // 텍스트 페이드 인
                yield return StartCoroutine(FadeTextAlpha(popupText, 0f, 1f, 0.5f));

                // 대기
                yield return CoroutineData.GetWaitForSeconds(durationPerText);

                // 텍스트 페이드 아웃
                yield return StartCoroutine(FadeTextAlpha(popupText, 1f, 0f, 0.5f));
            }

            // 3. 팝업 배경 페이드 아웃
            yield return StartCoroutine(FadeCanvasGroup(popup, 1f, 0f, 0.5f));
            popup.gameObject.SetActive(false);
        }

        public void UpdateGauge(int playerIdx, float current, float max)
        {
            if (playerIdx == 0 && p1Gauge) p1Gauge.UpdateGauge(current, max);
            else if (playerIdx == 1 && p2Gauge) p2Gauge.UpdateGauge(current, max);
        }

        public void UpdateTimer(float time)
        {
            if (timerText)
            {
                time = Mathf.Max(0, time);
                timerText.text = Mathf.CeilToInt(time).ToString();
            }
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
            txt.color = new Color(c.r, c.g, c.b, start);
            
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