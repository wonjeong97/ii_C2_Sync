using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Wonjeong.Data; 
using Wonjeong.UI;
using Wonjeong.Utils;

namespace My.Scripts._03_PlayShort
{
    public class PlayShortUIManager : MonoBehaviour
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

        [Header("YesNo UI (P1)")]
        [SerializeField] private CanvasGroup cgLeftYesNo;

        [Header("Gauge Images (Left P1)")]
        [SerializeField] private Image[] p1YesImages; 
        [SerializeField] private Image[] p1NoImages;
        [SerializeField] private Image p1ImageYes; 
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

        [Header("YesNo UI (P2)")]
        [SerializeField] private CanvasGroup cgRightYesNo;

        [Header("Gauge Images (Right P2)")]
        [SerializeField] private Image[] p2YesImages; 
        [SerializeField] private Image[] p2NoImages;
        [SerializeField] private Image p2ImageYes; 
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
        [SerializeField] private Sprite gaugeFinishSprite;
        
        [Header("Waiting Popup (Finish)")]
        [SerializeField] private CanvasGroup popupFinishP1;
        [SerializeField] private Text textFinishP1;
        [SerializeField] private CanvasGroup popupFinishP2;
        [SerializeField] private Text textFinishP2;
        
        [Header("Center Popup (All Finish)")]
        [SerializeField] private CanvasGroup popupCenter;
        [SerializeField] private Text textCenter;

        private readonly Coroutine[] runningPageRoutines = new Coroutine[2];
        private readonly Color activeColor = new Color(248f/255f, 237f/255f, 166f/255f);
        
        private float[] _lastInputTime = new float[2];
        private readonly Coroutine[] _infoFadeRoutines = new Coroutine[2];
        
        public void InitUI(float maxDistance)
        {
            if (p1Gauge) { p1Gauge.UpdateGauge(0, maxDistance); p1Gauge.ResetSprite(); }
            if (p2Gauge) { p2Gauge.UpdateGauge(0, maxDistance); p2Gauge.ResetSprite(); }
            if (centerText) centerText.gameObject.SetActive(false);
            
            HidePopupImmediate(popupQuestionLeft);
            HidePopupImmediate(popupQuestionRight);

            if (cgLeftYesNo) { cgLeftYesNo.alpha = 0f; cgLeftYesNo.gameObject.SetActive(false); }
            if (cgRightYesNo) { cgRightYesNo.alpha = 0f; cgRightYesNo.gameObject.SetActive(false); }

            ResetAnswerFeedback(0);
            ResetAnswerFeedback(1);
            ResetGaugeImages(0);
            ResetGaugeImages(1);
            
            HidePopupImmediate(popupFinishP1);
            HidePopupImmediate(popupFinishP2);
            HidePopupImmediate(popupCenter);
        }
        
        public void HideWaitingPopups()
        {
            if (popupFinishP1 && popupFinishP1.gameObject.activeSelf)
                StartCoroutine(FadeCanvasGroup(popupFinishP1, popupFinishP1.alpha, 0f, 0.1f));

            if (popupFinishP2 && popupFinishP2.gameObject.activeSelf)
                StartCoroutine(FadeCanvasGroup(popupFinishP2, popupFinishP2.alpha, 0f, 0.1f));
        }

        public void ShowCenterFinishPopup(TextSetting textData)
        {
            if (!popupCenter) return;
            ApplyTextSetting(textCenter, textData);
            popupCenter.gameObject.SetActive(true);
            popupCenter.alpha = 0f;
            StartCoroutine(FadeCanvasGroup(popupCenter, 0f, 1f, 0.1f));
        }
        
        public void SetGaugeFinish(int playerIdx)
        {
            if (playerIdx == 0)
            {
                if (p1Gauge && gaugeFinishSprite) p1Gauge.SetFillSprite(gaugeFinishSprite);
            }
            else
            {
                if (p2Gauge && gaugeFinishSprite) p2Gauge.SetFillSprite(gaugeFinishSprite);
            }
        }
        
        public void ShowWaitingPopup(int playerIdx, TextSetting textData)
        {
            CanvasGroup targetPopup = (playerIdx == 0) ? popupFinishP1 : popupFinishP2;
            Text targetText = (playerIdx == 0) ? textFinishP1 : textFinishP2;
            if (!targetPopup) return;
            ApplyTextSetting(targetText, textData);
            targetPopup.gameObject.SetActive(true);
            targetPopup.alpha = 0f;
            
            SoundManager.Instance?.PlaySFX("공통_7");
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

        public void ShowQuestionPopup(int playerIdx, int distance, TextSetting questionData, TextSetting infoData)
        {
            CanvasGroup targetPopup = (playerIdx == 0) ? popupQuestionLeft : popupQuestionRight;
            Text targetDistText = (playerIdx == 0) ? textLeftDistance : textRightDistance;
            
            Text targetQueP1 = (playerIdx == 0) ? textLeftQuestionP1 : textRightQuestionP1;
            Text targetQueP2 = (playerIdx == 0) ? textLeftQuestionP2 : textRightQuestionP2;
            Text targetInfo = (playerIdx == 0) ? textLeftInfo : textRightInfo;

            CanvasGroup targetPage1 = (playerIdx == 0) ? cgLeftPage1 : cgRightPage1;
            CanvasGroup targetPage2 = (playerIdx == 0) ? cgLeftPage2 : cgRightPage2;
            CanvasGroup targetYesNo = (playerIdx == 0) ? cgLeftYesNo : cgRightYesNo;

            if (!targetPopup) return;

            ResetAnswerFeedback(playerIdx);
            ResetGaugeImages(playerIdx);

            if (targetPage1) 
            {
                targetPage1.alpha = 1f;
                targetPage1.gameObject.SetActive(true);
            }
            if (targetPage2) 
            {
                targetPage2.alpha = 0f;
                targetPage2.gameObject.SetActive(false);
            }
            if (targetYesNo) 
            { 
                targetYesNo.alpha = 0f; 
                targetYesNo.gameObject.SetActive(false); 
            }

            if (targetDistText) targetDistText.text = $"{distance}M";
            ApplyTextSetting(targetQueP1, questionData);
            if (targetQueP2) targetQueP2.text = (questionData != null) ? questionData.text : "";
            ApplyTextSetting(targetInfo, infoData);

            targetPopup.gameObject.SetActive(true);
            targetPopup.alpha = 0f; 
            targetPopup.blocksRaycasts = true; 
            StartCoroutine(FadeCanvasGroup(targetPopup, 0f, 1f, 1.0f)); 
        }

        public IEnumerator ShowQuestionPhase2Routine(int playerIdx, float duration, int distance)
        {
            CanvasGroup targetYesNo = (playerIdx == 0) ? cgLeftYesNo : cgRightYesNo;
            Text targetInfo = (playerIdx == 0) ? textLeftInfo : textRightInfo; 

            // 기존에 돌고 있던 텍스트 제어 코루틴 중지
            if (_infoFadeRoutines[playerIdx] != null)
            {
                StopCoroutine(_infoFadeRoutines[playerIdx]);
                _infoFadeRoutines[playerIdx] = null;
            }

            if (distance > 10)
            {
                // 20M 이상: 게이지를 다 채우지 않은 상태에서 3초간 반응이 없을 때만 등장시키는 코루틴 실행
                _infoFadeRoutines[playerIdx] = StartCoroutine(InactivityInfoFadeRoutine(playerIdx, targetInfo, 3.0f, 0.5f));
            }
            else
            {
                // 10M (처음): 페이지 전환 시 바로 보이도록 Alpha 1을 고정
                if (targetInfo)
                {
                    Color c = targetInfo.color;
                    c.a = 1f;
                    targetInfo.color = c;
                }
            }

            // 1. YesNo 그룹 페이드 인
            if (targetYesNo)
            {
                targetYesNo.gameObject.SetActive(true);
                StartCoroutine(FadeCanvasGroup(targetYesNo, 0f, 1f, duration));
            }

            // 2. Page 1 -> Page 2 전환 (순차 페이드)
            SwitchPageState(playerIdx, true); 

            yield return CoroutineData.GetWaitForSeconds(duration);
        }
        
        /// <summary>
        /// 3초 동안 입력이 없으면 텍스트를 표시하고, 입력이 들어오면 텍스트를 즉시 숨깁니다.
        /// </summary>
        private IEnumerator InactivityInfoFadeRoutine(int playerIdx, Text infoText, float waitTime, float fadeDuration)
        {
            if (!infoText) yield break;

            // 초기에는 안 보이는 상태로 설정
            Color c = infoText.color;
            c.a = 0f;
            infoText.color = c;

            _lastInputTime[playerIdx] = Time.time;

            while (true)
            {
                float idleTime = Time.time - _lastInputTime[playerIdx];

                // 3초간 방치되었다면 페이드 인 시키고 감시 루프를 완전히 종료함
                if (idleTime >= waitTime)
                {
                    yield return StartCoroutine(FadeTextAlpha(infoText, infoText.color.a, 1f, fadeDuration));
                    yield break; 
                }

                yield return null;
            }
        }

        private void ResetGaugeImages(int playerIdx)
        {
            Image[] yesImgs = (playerIdx == 0) ? p1YesImages : p2YesImages;
            Image[] noImgs = (playerIdx == 0) ? p1NoImages : p2NoImages;
            ClearImages(yesImgs);
            ClearImages(noImgs);
            Image iconYes = (playerIdx == 0) ? p1ImageYes : p2ImageYes;
            Image iconNo = (playerIdx == 0) ? p1ImageNo : p2ImageNo;
            if (iconYes) iconYes.color = Color.white;
            if (iconNo) iconNo.color = Color.white;
        }

        private void ClearImages(Image[] imgs)
        {
            if (imgs == null) return;

            foreach (Image img in imgs)
            {
                if (img) img.fillAmount = 0f;
            }
        }

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
                if (!targetImages[i]) continue;
                float amount = Mathf.Clamp01(totalFillNeeded);
                targetImages[i].fillAmount = amount;
                totalFillNeeded -= 1.0f;
            }
            if (stepCount >= 10)
            {
                if (targetIcon) targetIcon.color = activeColor;
                SoundManager.Instance?.PlaySFX("공통_22");
                return true; 
            }
            return false; 
        }

        /// <summary>
        /// 질문 팝업을 숨기고 관련된 진행 중인 상태들을 초기화함.
        /// </summary>
        public void HideQuestionPopup(int playerIdx, float duration)
        {
            // 팝업을 닫을 때 진행 중이던 방치 텍스트 코루틴이 있다면 정리함
            if (_infoFadeRoutines[playerIdx] != null)
            {
                StopCoroutine(_infoFadeRoutines[playerIdx]);
                _infoFadeRoutines[playerIdx] = null;
            }

            // 다음 팝업 호출 시 이전 텍스트가 남아 보이지 않도록 즉시 투명화 처리함
            Text targetInfo = (playerIdx == 0) ? textLeftInfo : textRightInfo;
            if (targetInfo)
            {
                Color c = targetInfo.color;
                c.a = 0f;
                targetInfo.color = c;
            }

            CanvasGroup targetPopup = (playerIdx == 0) ? popupQuestionLeft : popupQuestionRight;
            if (targetPopup && targetPopup.gameObject.activeInHierarchy)
            {
                targetPopup.blocksRaycasts = false; 
                StartCoroutine(FadeCanvasGroup(targetPopup, targetPopup.alpha, 0f, duration));
            }
        }

        private void ApplyTextSetting(Text targetText, TextSetting setting)
        {
            if (!targetText) return;
            if (setting != null)
            {
                if (UIManager.Instance) UIManager.Instance.SetText(targetText.gameObject, setting);
                else targetText.text = setting.text;
            }
            else targetText.text = ""; 
        }

        public void SetAnswerFeedback(int playerIdx, bool isYes)
        {
            Image targetYes = (playerIdx == 0) ? p1YesOut : p2YesOut;
            Image targetNo = (playerIdx == 0) ? p1NoOut : p2NoOut;

            if (targetYes) targetYes.color = isYes ? activeColor : Color.white;
            if (targetNo) targetNo.color = !isYes ? activeColor : Color.white;
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
            
            if (toPage2) 
                runningPageRoutines[playerIdx] = StartCoroutine(SequentialPageTransition(p1, p2, 0.5f, 0.5f));
            else 
                runningPageRoutines[playerIdx] = StartCoroutine(SequentialPageTransition(p2, p1, 0.5f, 0.5f));
        }

        public IEnumerator ShowSuccessText(string message, float duration)
        {
            if (!centerText) yield break;
            centerText.text = message;
            centerText.gameObject.SetActive(true);
            yield return StartCoroutine(FadeTextAlpha(centerText, 0f, 1f, 1.0f));
            yield return CoroutineData.GetWaitForSeconds(duration);
            yield return StartCoroutine(FadeTextAlpha(centerText, 1f, 0f, 1.0f));
            centerText.gameObject.SetActive(false);
        }
        
        /// <summary>
        /// 외부(Manager)에서 사용자 입력이 발생했음을 알림.
        /// 방치 타이머를 현재 시간으로 갱신함.
        /// </summary>
        public void NotifyInput(int playerIdx)
        {
            if (playerIdx >= 0 && playerIdx < 2)
            {
                _lastInputTime[playerIdx] = Time.time;
            }
        }
        
        private IEnumerator SequentialPageTransition(CanvasGroup fromGroup, CanvasGroup toGroup, float fadeOutTime, float fadeInTime)
        {
            // 1. 현재 페이지 Fade Out
            if (fromGroup.gameObject.activeSelf)
            {
                yield return StartCoroutine(FadeCanvasGroup(fromGroup, fromGroup.alpha, 0f, fadeOutTime));
            }
            
            // 2. 다음 페이지 Fade In
            toGroup.gameObject.SetActive(true);
            toGroup.alpha = 0f;
            yield return StartCoroutine(FadeCanvasGroup(toGroup, 0f, 1f, fadeInTime));
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