using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Wonjeong.Data; 
using Wonjeong.UI;

namespace My.Scripts._03_Play150M.Managers
{
    public class Play150MUIManager : MonoBehaviour
    {
        [Header("Question Popup UI - Left (P1)")]
        [SerializeField] private CanvasGroup popupQuestionLeft;
        [SerializeField] private Text textLeftDistance;
        [Tooltip("Page1의 CanvasGroup")]
        [SerializeField] private CanvasGroup cgLeftPage1;
        [Tooltip("Page2의 CanvasGroup")]
        [SerializeField] private CanvasGroup cgLeftPage2;
        [SerializeField] private Text textLeftQuestionP1; 
        [SerializeField] private Text textLeftQuestionP2; 
        [SerializeField] private Text textLeftInfo;       

        [Header("Gauge Images (Left P1)")]
        [Tooltip("Yes_In 하위의 1~5 이미지")]
        [SerializeField] private Image[] p1YesImages; 
        [Tooltip("No_In 하위의 1~5 이미지")]
        [SerializeField] private Image[] p1NoImages;
        
        [Tooltip("게이지 완료 시 색상을 바꿀 Image_Yes")]
        [SerializeField] private Image p1ImageYes; 
        [Tooltip("게이지 완료 시 색상을 바꿀 Image_No")]
        [SerializeField] private Image p1ImageNo;

        [Header("Question Popup UI - Right (P2)")]
        [SerializeField] private CanvasGroup popupQuestionRight;
        [SerializeField] private Text textRightDistance;
        [Tooltip("Page1의 CanvasGroup")]
        [SerializeField] private CanvasGroup cgRightPage1;
        [Tooltip("Page2의 CanvasGroup")]
        [SerializeField] private CanvasGroup cgRightPage2;
        [SerializeField] private Text textRightQuestionP1;
        [SerializeField] private Text textRightQuestionP2; 
        [SerializeField] private Text textRightInfo;       

        [Header("Gauge Images (Right P2)")]
        [SerializeField] private Image[] p2YesImages; 
        [SerializeField] private Image[] p2NoImages;
        
        [Tooltip("게이지 완료 시 색상을 바꿀 Image_Yes")]
        [SerializeField] private Image p2ImageYes; 
        [Tooltip("게이지 완료 시 색상을 바꿀 Image_No")]
        [SerializeField] private Image p2ImageNo;

        [Header("Question Answer Feedback (Outlines)")]
        [SerializeField] private Image p1YesOut; 
        [SerializeField] private Image p1NoOut;  
        [SerializeField] private Image p2YesOut;
        [SerializeField] private Image p2NoOut;

        [Header("Common UI")]
        [SerializeField] private Text centerText; 
        [SerializeField] private GaugeController p1Gauge; 
        [SerializeField] private GaugeController p2Gauge; 
        
        [Header("Gauge Finish Sprites")]
        [SerializeField] private Sprite gaugeFinishSprite;
        
        [Header("Waiting Popup (Finish)")]
        [SerializeField] private CanvasGroup popupFinishP1;
        [SerializeField] private Text textFinishP1;
        [SerializeField] private CanvasGroup popupFinishP2;
        [SerializeField] private Text textFinishP2;
        
        [Header("Center Popup (All Finish)")]
        [SerializeField] private CanvasGroup popupCenter;
        [SerializeField] private Text textCenter;

        private Coroutine[] runningPageRoutines = new Coroutine[2];
        private readonly Color activeColor = new Color(248f/255f, 237f/255f, 166f/255f);
        
        public void InitUI(float maxDistance)
        {
            if (p1Gauge) 
            {
                p1Gauge.UpdateGauge(0, maxDistance);
                p1Gauge.ResetSprite();
            }
            if (p2Gauge) 
            {
                p2Gauge.UpdateGauge(0, maxDistance);
                p2Gauge.ResetSprite();
            }
            if (centerText) centerText.gameObject.SetActive(false);
            
            HidePopupImmediate(popupQuestionLeft);
            HidePopupImmediate(popupQuestionRight);

            ResetAnswerFeedback(0);
            ResetAnswerFeedback(1);
            
            ResetGaugeImages(0);
            ResetGaugeImages(1);
            
            HidePopupImmediate(popupFinishP1);
            HidePopupImmediate(popupFinishP2);
            HidePopupImmediate(popupCenter);
        }
        
        // 대기 팝업 모두 숨기기 (게임 종료 시 호출)
        public void HideWaitingPopups()
        {
            if (popupFinishP1 && popupFinishP1.gameObject.activeSelf)
                StartCoroutine(FadeCanvasGroup(popupFinishP1, popupFinishP1.alpha, 0f, 0.1f));

            if (popupFinishP2 && popupFinishP2.gameObject.activeSelf)
                StartCoroutine(FadeCanvasGroup(popupFinishP2, popupFinishP2.alpha, 0f, 0.1f));
        }

        // 중앙 완료 팝업 표시
        public void ShowCenterFinishPopup(TextSetting textData)
        {
            if (!popupCenter) return;

            // 텍스트 설정
            ApplyTextSetting(textCenter, textData);

            // 팝업 등장
            popupCenter.gameObject.SetActive(true);
            popupCenter.alpha = 0f;
            StartCoroutine(FadeCanvasGroup(popupCenter, 0f, 1f, 0.1f));
        }
        
        public void SetGaugeFinish(int playerIdx)
        {
            if (playerIdx == 0)
            {
                if (p1Gauge && gaugeFinishSprite) 
                    p1Gauge.SetFillSprite(gaugeFinishSprite);
            }
            else
            {
                if (p2Gauge && gaugeFinishSprite) 
                    p2Gauge.SetFillSprite(gaugeFinishSprite);
            }
        }
        
        public void ShowWaitingPopup(int playerIdx, TextSetting textData)
        {
            CanvasGroup targetPopup = (playerIdx == 0) ? popupFinishP1 : popupFinishP2;
            Text targetText = (playerIdx == 0) ? textFinishP1 : textFinishP2;

            if (targetPopup == null) return;

            // 텍스트 설정 (JSON 데이터 적용)
            ApplyTextSetting(targetText, textData);

            // 팝업 등장 (Fade In)
            targetPopup.gameObject.SetActive(true);
            targetPopup.alpha = 0f;
            StartCoroutine(FadeCanvasGroup(targetPopup, 0f, 1f, 0.1f));
        }

        private void HidePopupImmediate(CanvasGroup cg)
        {
            if (cg)
            {
                cg.alpha = 0f;
                cg.blocksRaycasts = false;
                cg.gameObject.SetActive(false);
            }
        }

        public void UpdateGauge(int playerIdx, float current, float max)
        {
            if (playerIdx == 0 && p1Gauge) p1Gauge.UpdateGauge(current, max);
            else if (playerIdx == 1 && p2Gauge) p2Gauge.UpdateGauge(current, max);
        }

        // --- Question Popup ---

        public void ShowQuestionPopup(int playerIdx, int distance, TextSetting questionData, TextSetting infoData)
        {
            CanvasGroup targetPopup = (playerIdx == 0) ? popupQuestionLeft : popupQuestionRight;
            Text targetDistText = (playerIdx == 0) ? textLeftDistance : textRightDistance;
            
            Text targetQueP1 = (playerIdx == 0) ? textLeftQuestionP1 : textRightQuestionP1;
            Text targetQueP2 = (playerIdx == 0) ? textLeftQuestionP2 : textRightQuestionP2;
            Text targetInfo = (playerIdx == 0) ? textLeftInfo : textRightInfo;

            CanvasGroup targetPage1 = (playerIdx == 0) ? cgLeftPage1 : cgRightPage1;
            CanvasGroup targetPage2 = (playerIdx == 0) ? cgLeftPage2 : cgRightPage2;

            if (targetPopup == null) return;

            // 초기화
            ResetAnswerFeedback(playerIdx);
            
            // 팝업 뜰 때 게이지 및 하단 아이콘 색상 리셋
            ResetGaugeImages(playerIdx);

            // 팝업 첫 등장 시 Page1만 보이게 설정
            if (targetPage1) targetPage1.alpha = 1f;
            if (targetPage2) targetPage2.alpha = 0f;

            // 텍스트 데이터 세팅
            if (targetDistText) targetDistText.text = $"{distance}M";
            
            ApplyTextSetting(targetQueP1, questionData);
            if (targetQueP2 != null) targetQueP2.text = (questionData != null) ? questionData.text : "";
            ApplyTextSetting(targetInfo, infoData);

            // 팝업 등장
            targetPopup.gameObject.SetActive(true);
            targetPopup.alpha = 0f; 
            targetPopup.blocksRaycasts = true; 
            StartCoroutine(FadeCanvasGroup(targetPopup, 0f, 1f, 1.0f)); 
        }

        private void ResetGaugeImages(int playerIdx)
        {
            Image[] yesImgs = (playerIdx == 0) ? p1YesImages : p2YesImages;
            Image[] noImgs = (playerIdx == 0) ? p1NoImages : p2NoImages;
            
            ClearImages(yesImgs);
            ClearImages(noImgs);

            // 하단 아이콘 색상 초기화 (흰색)
            Image iconYes = (playerIdx == 0) ? p1ImageYes : p2ImageYes;
            Image iconNo = (playerIdx == 0) ? p1ImageNo : p2ImageNo;

            if (iconYes) iconYes.color = Color.white;
            if (iconNo) iconNo.color = Color.white;
        }

        private void ClearImages(Image[] imgs)
        {
            if (imgs == null) return;
            foreach (var img in imgs)
            {
                if (img != null) img.fillAmount = 0f;
            }
        }

        // [수정] 반환 타입을 bool로 변경 (완료 여부 반환)
        public bool UpdateStepGauge(int playerIdx, bool isYesLane, int stepCount)
        {
            Image[] targetImages;
            Image targetIcon;

            if (playerIdx == 0)
            {
                targetImages = isYesLane ? p1YesImages : p1NoImages;
                targetIcon = isYesLane ? p1ImageYes : p1ImageNo;
            }
            else
            {
                targetImages = isYesLane ? p2YesImages : p2NoImages;
                targetIcon = isYesLane ? p2ImageYes : p2ImageNo;
            }

            if (targetImages == null || targetImages.Length == 0) return false;

            float totalFillNeeded = stepCount * 0.5f;

            for (int i = targetImages.Length - 1; i >= 0; i--)
            {
                if (targetImages[i] == null) continue;
                float amount = Mathf.Clamp01(totalFillNeeded);
                targetImages[i].fillAmount = amount;
                totalFillNeeded -= 1.0f;
            }

            // 스텝 10 이상이면 아이콘 색상 변경 및 완료(true) 반환
            if (stepCount >= 10)
            {
                if (targetIcon) targetIcon.color = activeColor;
                return true; // ★ 완료됨
            }

            return false; // ★ 아직 미완료
        }

        public void HideQuestionPopup(int playerIdx, float duration)
        {
            CanvasGroup targetPopup = (playerIdx == 0) ? popupQuestionLeft : popupQuestionRight;
            if (targetPopup != null && targetPopup.gameObject.activeInHierarchy)
            {
                targetPopup.blocksRaycasts = false; 
                StartCoroutine(FadeCanvasGroup(targetPopup, targetPopup.alpha, 0f, duration));
            }
        }

        private void ApplyTextSetting(Text targetText, TextSetting setting)
        {
            if (targetText == null) return;
            if (setting != null)
            {
                if (UIManager.Instance != null) UIManager.Instance.SetText(targetText.gameObject, setting);
                else targetText.text = setting.text;
            }
            else
            {
                targetText.text = ""; 
            }
        }

        // --- Answer Feedback ---

        public void SetAnswerFeedback(int playerIdx, bool isYes)
        {
            Image targetYes = (playerIdx == 0) ? p1YesOut : p2YesOut;
            Image targetNo = (playerIdx == 0) ? p1NoOut : p2NoOut;

            // ★ [수정] 아웃라인 색상도 activeColor로 통일
            if (targetYes) targetYes.color = isYes ? activeColor : Color.white;
            if (targetNo) targetNo.color = !isYes ? activeColor : Color.white;

            SwitchPageState(playerIdx, true); 
        }

        public void ResetAnswerFeedback(int playerIdx)
        {
            Image targetYes = (playerIdx == 0) ? p1YesOut : p2YesOut;
            Image targetNo = (playerIdx == 0) ? p1NoOut : p2NoOut;

            if (targetYes) targetYes.color = Color.white;
            if (targetNo) targetNo.color = Color.white;
        }

        private void SwitchPageState(int playerIdx, bool toPage2)
        {
            CanvasGroup p1 = (playerIdx == 0) ? cgLeftPage1 : cgRightPage1;
            CanvasGroup p2 = (playerIdx == 0) ? cgLeftPage2 : cgRightPage2;

            if (!p1 || !p2) return;

            if (runningPageRoutines[playerIdx] != null) StopCoroutine(runningPageRoutines[playerIdx]);

            if (toPage2) runningPageRoutines[playerIdx] = StartCoroutine(CrossFadePages(p1, p2, 0.1f));
            else runningPageRoutines[playerIdx] = StartCoroutine(CrossFadePages(p2, p1, 0.1f));
        }

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

        private IEnumerator CrossFadePages(CanvasGroup fromGroup, CanvasGroup toGroup, float duration)
        {
            float t = 0f;
            float startFrom = fromGroup.alpha;
            float startTo = toGroup.alpha;

            while (t < duration)
            {
                t += Time.deltaTime;
                float normalizedTime = t / duration;
                fromGroup.alpha = Mathf.Lerp(startFrom, 0f, normalizedTime);
                toGroup.alpha = Mathf.Lerp(startTo, 1f, normalizedTime);
                yield return null;
            }
            fromGroup.alpha = 0f;
            toGroup.alpha = 1f;
        }

        private IEnumerator FadeCanvasGroup(CanvasGroup cg, float start, float end, float duration)
        {
            if (!cg) yield break;
            if (duration <= 0f)
            {
                cg.alpha = end;
                if (end <= 0f) cg.gameObject.SetActive(false);
                yield break;
            }
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