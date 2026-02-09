using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Wonjeong.Data; 
using Wonjeong.UI;

namespace My.Scripts._03_Play150M.Managers
{
    /// <summary>
    /// 150M 모드 전용 UI 매니저.
    /// 질문 팝업, 정답 피드백, 거리 게이지, 성공 메시지를 관리합니다.
    /// </summary>
    public class Play150MUIManager : MonoBehaviour
    {
        [Header("Question Popup UI")]
        [SerializeField] private CanvasGroup popupQuestionLeft;
        [SerializeField] private Text textLeftDistance;
        [SerializeField] private Text textLeftQuestion;

        [SerializeField] private CanvasGroup popupQuestionRight;
        [SerializeField] private Text textRightDistance;
        [SerializeField] private Text textRightQuestion;

        [Header("Question Answer Feedback (Outlines)")]
        [SerializeField] private Image p1YesOut; // 왼쪽 라인(Yes) 선택 시 강조
        [SerializeField] private Image p1NoOut;  // 오른쪽 라인(No) 선택 시 강조
        [SerializeField] private Image p2YesOut;
        [SerializeField] private Image p2NoOut;

        [Header("Common UI")]
        [SerializeField] private Text centerText; // FINISH 등 중앙 메시지
        [SerializeField] private GaugeController p1Gauge; 
        [SerializeField] private GaugeController p2Gauge; 
        
        public void InitUI(float maxDistance)
        {
            if (p1Gauge) p1Gauge.UpdateGauge(0, maxDistance);
            if (p2Gauge) p2Gauge.UpdateGauge(0, maxDistance);
            
            if (centerText) centerText.gameObject.SetActive(false);
            
            // 팝업 초기화
            if (popupQuestionLeft) { popupQuestionLeft.alpha = 0; popupQuestionLeft.gameObject.SetActive(false); }
            if (popupQuestionRight) { popupQuestionRight.alpha = 0; popupQuestionRight.gameObject.SetActive(false); }

            // 피드백 이미지 초기화
            ResetAnswerFeedback(0);
            ResetAnswerFeedback(1);
        }

        public void UpdateGauge(int playerIdx, float current, float max)
        {
            if (playerIdx == 0 && p1Gauge) p1Gauge.UpdateGauge(current, max);
            else if (playerIdx == 1 && p2Gauge) p2Gauge.UpdateGauge(current, max);
        }

        // --- Question Popup ---

        public void ShowQuestionPopup(int playerIdx, int distance, TextSetting questionData)
        {
            CanvasGroup targetPopup = (playerIdx == 0) ? popupQuestionLeft : popupQuestionRight;
            Text targetDistText = (playerIdx == 0) ? textLeftDistance : textRightDistance;
            Text targetQueText = (playerIdx == 0) ? textLeftQuestion : textRightQuestion;

            if (targetPopup == null) return;

            // 팝업 표시 전 선택 상태 초기화
            ResetAnswerFeedback(playerIdx);

            if (targetDistText) targetDistText.text = $"{distance}M";
            
            if (targetQueText)
            {
                if (questionData != null && UIManager.Instance != null)
                {
                    UIManager.Instance.SetText(targetQueText.gameObject, questionData);
                }
                else
                {
                    targetQueText.text = questionData != null ? questionData.text : "No Data";
                }
            }

            // 페이드 인 등장
            targetPopup.gameObject.SetActive(true);
            targetPopup.alpha = 0f; 
            StartCoroutine(FadeCanvasGroup(targetPopup, 0f, 1f, 1.0f)); 
        }

        public void HideQuestionPopup(int playerIdx, float duration)
        {
            CanvasGroup targetPopup = (playerIdx == 0) ? popupQuestionLeft : popupQuestionRight;
            if (targetPopup != null && targetPopup.gameObject.activeSelf)
            {
                StartCoroutine(FadeCanvasGroup(targetPopup, targetPopup.alpha, 0f, duration));
            }
        }

        // --- Answer Feedback ---

        public void SetAnswerFeedback(int playerIdx, bool isYes)
        {
            Image targetYes = (playerIdx == 0) ? p1YesOut : p2YesOut;
            Image targetNo = (playerIdx == 0) ? p1NoOut : p2NoOut;

            if (targetYes) targetYes.color = isYes ? Color.yellow : Color.white;
            if (targetNo) targetNo.color = !isYes ? Color.yellow : Color.white;
        }

        public void ResetAnswerFeedback(int playerIdx)
        {
            Image targetYes = (playerIdx == 0) ? p1YesOut : p2YesOut;
            Image targetNo = (playerIdx == 0) ? p1NoOut : p2NoOut;

            if (targetYes) targetYes.color = Color.white;
            if (targetNo) targetNo.color = Color.white;
        }

        // --- Common ---

        public IEnumerator ShowSuccessText(string message, float duration)
        {
            if (!centerText) yield break;
            
            centerText.text = message;
            centerText.gameObject.SetActive(true);
            
            yield return StartCoroutine(FadeTextAlpha(centerText, 0f, 1f, 1.0f));
            yield return new WaitForSeconds(duration);
            yield return StartCoroutine(FadeTextAlpha(centerText, 1f, 0f, 1.0f));
            
            centerText.gameObject.SetActive(false);
        }

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