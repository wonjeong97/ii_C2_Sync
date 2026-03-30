using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Wonjeong.Data; 
using Wonjeong.UI;
using Wonjeong.Utils;
using My.Scripts.UI; 

namespace My.Scripts._03_PlayShort
{
    /// <summary>
    /// PlayShort 씬의 전반적인 UI 상태와 연출을 관리하는 클래스.
    /// </summary>
    public class PlayShortUIManager : MonoBehaviour
    {
        [Header("Player Name UI")]
        [SerializeField] private Text p1NameText;
        [SerializeField] private Text p2NameText;

        [Header("Player Color Balls")]
        [SerializeField] private Image ballImageA;
        [SerializeField] private Image ballImageB;

        [Header("Question Popup UI - Left (P1)")]
        [SerializeField] private CanvasGroup popupQuestionLeft;
        [SerializeField] private Text textLeftDistance;
        [Tooltip("Page1 CanvasGroup")]
        [SerializeField] private CanvasGroup cgLeftPage1;
        [Tooltip("Page2 CanvasGroup")]
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
        [Tooltip("Page1 CanvasGroup")]
        [SerializeField] private CanvasGroup cgRightPage1;
        [Tooltip("Page2 CanvasGroup")]
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
        
        /// <summary>
        /// UI 컴포넌트의 초기 상태를 설정함.
        /// </summary>
        /// <param name="maxDistance">게이지 최대 목표 거리</param>
        public void InitUI(float maxDistance)
        {
            if (p1Gauge) 
            { 
                p1Gauge.UpdateGauge(0, maxDistance); 
                p1Gauge.ResetSprite(); 
            }
            else Debug.LogWarning("p1Gauge 누락됨.");

            if (p2Gauge) 
            { 
                p2Gauge.UpdateGauge(0, maxDistance); 
                p2Gauge.ResetSprite(); 
            }
            else Debug.LogWarning("p2Gauge 누락됨.");

            if (centerText) centerText.gameObject.SetActive(false);
            
            HidePopupImmediate(popupQuestionLeft);
            HidePopupImmediate(popupQuestionRight);

            if (cgLeftYesNo) 
            { 
                cgLeftYesNo.alpha = 0f; 
                cgLeftYesNo.gameObject.SetActive(false); 
            }
            
            if (cgRightYesNo) 
            { 
                cgRightYesNo.alpha = 0f; 
                cgRightYesNo.gameObject.SetActive(false); 
            }

            ResetAnswerFeedback(0);
            ResetAnswerFeedback(1);
            ResetGaugeImages(0);
            ResetGaugeImages(1);
            
            HidePopupImmediate(popupFinishP1);
            HidePopupImmediate(popupFinishP2);
            HidePopupImmediate(popupCenter);
        }

        /// <summary>
        /// 플레이어 이름 UI를 설정함.
        /// </summary>
        /// <param name="nameA">1P 이름</param>
        /// <param name="nameB">2P 이름</param>
        /// <param name="settingA">1P 텍스트 설정 데이터</param>
        /// <param name="settingB">2P 텍스트 설정 데이터</param>
        public void SetPlayerNames(string nameA, string nameB, TextSetting settingA, TextSetting settingB)
        {
            // 이유: 중복 코드 방지를 위해 공용 유틸리티 메서드 재사용.
            UIUtils.ApplyPlayerNames(p1NameText, p2NameText, nameA, nameB, settingA, settingB);
        }

        /// <summary>
        /// 플레이어 색상 스프라이트를 지정함.
        /// </summary>
        /// <param name="spriteA">1P 색상 스프라이트</param>
        /// <param name="spriteB">2P 색상 스프라이트</param>
        public void SetPlayerBalls(Sprite spriteA, Sprite spriteB)
        {
            if (ballImageA)
            {
                if (spriteA) ballImageA.sprite = spriteA;
                else Debug.LogWarning("Player A 컬러 스프라이트 누락됨.");
            }
            else Debug.LogWarning("ballImageA 누락됨.");

            if (ballImageB)
            {
                if (spriteB) ballImageB.sprite = spriteB;
                else Debug.LogWarning("Player B 컬러 스프라이트 누락됨.");
            }
            else Debug.LogWarning("ballImageB 누락됨.");
        }
        
        /// <summary>
        /// 결승선 도달 대기 팝업을 숨김.
        /// </summary>
        public void HideWaitingPopups()
        {
            if (popupFinishP1 && popupFinishP1.gameObject.activeSelf)
                StartCoroutine(FadeCanvasGroup(popupFinishP1, popupFinishP1.alpha, 0f, 0.1f));

            if (popupFinishP2 && popupFinishP2.gameObject.activeSelf)
                StartCoroutine(FadeCanvasGroup(popupFinishP2, popupFinishP2.alpha, 0f, 0.1f));
        }

        /// <summary>
        /// 양 플레이어 완료 시 뜨는 중앙 팝업을 표시함.
        /// </summary>
        /// <param name="textData">표시할 텍스트 설정</param>
        public void ShowCenterFinishPopup(TextSetting textData)
        {
            if (!popupCenter) 
            {
                Debug.LogWarning("popupCenter 누락됨.");
                return;
            }

            ApplyTextSetting(textCenter, textData);
            popupCenter.gameObject.SetActive(true);
            popupCenter.alpha = 0f;
            StartCoroutine(FadeCanvasGroup(popupCenter, 0f, 1f, 0.1f));
        }
        
        /// <summary>
        /// 진행도 게이지를 완료 상태의 스프라이트로 변경함.
        /// </summary>
        /// <param name="playerIdx">플레이어 인덱스</param>
        public void SetGaugeFinish(int playerIdx)
        {
            if (playerIdx == 0)
            {
                if (p1Gauge && gaugeFinishSprite) p1Gauge.SetFillSprite(gaugeFinishSprite);
                else Debug.LogWarning("p1Gauge 혹은 gaugeFinishSprite 누락됨.");
            }
            else
            {
                if (p2Gauge && gaugeFinishSprite) p2Gauge.SetFillSprite(gaugeFinishSprite);
                else Debug.LogWarning("p2Gauge 혹은 gaugeFinishSprite 누락됨.");
            }
        }
        
        /// <summary>
        /// 특정 플레이어의 결승선 도달 대기 팝업을 띄움.
        /// </summary>
        /// <param name="playerIdx">플레이어 인덱스</param>
        /// <param name="textData">출력할 텍스트 설정</param>
        public void ShowWaitingPopup(int playerIdx, TextSetting textData)
        {
            CanvasGroup targetPopup = (playerIdx == 0) ? popupFinishP1 : popupFinishP2;
            Text targetText = (playerIdx == 0) ? textFinishP1 : textFinishP2;
            
            if (!targetPopup) 
            {
                Debug.LogWarning("대기 팝업 대상 CanvasGroup 누락됨.");
                return;
            }

            ApplyTextSetting(targetText, textData);
            targetPopup.gameObject.SetActive(true);
            targetPopup.alpha = 0f;
            
            if (SoundManager.Instance) SoundManager.Instance.PlaySFX("공통_7");
            StartCoroutine(FadeCanvasGroup(targetPopup, 0f, 1f, 0.1f));
        }

        /// <summary>
        /// 캔버스 그룹을 즉시 투명화 및 비활성화함.
        /// </summary>
        /// <param name="cg">대상 CanvasGroup</param>
        private void HidePopupImmediate(CanvasGroup cg)
        {
            if (cg)
            {
                cg.alpha = 0f;
                cg.blocksRaycasts = false;
                cg.gameObject.SetActive(false);
            }
        }

        /// <summary>
        /// 특정 플레이어의 UI 게이지 값을 갱신함.
        /// </summary>
        /// <param name="playerIdx">플레이어 인덱스</param>
        /// <param name="current">현재 수치</param>
        /// <param name="max">최대 수치</param>
        public void UpdateGauge(int playerIdx, float current, float max)
        {
            if (playerIdx == 0 && p1Gauge) p1Gauge.UpdateGauge(current, max);
            else if (playerIdx == 1 && p2Gauge) p2Gauge.UpdateGauge(current, max);
        }

        /// <summary>
        /// 특정 플레이어의 질문 팝업을 활성화함.
        /// </summary>
        /// <param name="playerIdx">플레이어 인덱스</param>
        /// <param name="distance">현재 거리 지점</param>
        /// <param name="questionData">질문 텍스트 설정</param>
        /// <param name="infoData">안내 텍스트 설정</param>
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

            if (!targetPopup) 
            {
                Debug.LogWarning("질문 팝업 대상 CanvasGroup 누락됨.");
                return;
            }

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
            
            if (questionData != null && !string.IsNullOrEmpty(questionData.text))
            {
                string[] texts = questionData.text.Split(new string[] { "||" }, System.StringSplitOptions.None);
                
                if (targetQueP1) 
                {
                    targetQueP1.text = texts[0];
                }
                
                // 이유: Page2 출력 시 구분자가 없어도 줄바꿈을 유지하도록 원본 문자열을 그대로 사용함.
                if (targetQueP2) 
                {
                    targetQueP2.text = texts.Length > 1 ? texts[1] : texts[0];
                }
            }
            else
            {
                if (targetQueP1) targetQueP1.text = "";
                if (targetQueP2) targetQueP2.text = "";
                Debug.LogWarning("questionData가 유효하지 않음.");
            }

            ApplyTextSetting(targetInfo, infoData);

            targetPopup.gameObject.SetActive(true);
            targetPopup.alpha = 0f; 
            targetPopup.blocksRaycasts = true; 
            StartCoroutine(FadeCanvasGroup(targetPopup, 0f, 1f, 0.5f)); 
        }

        /// <summary>
        /// 질문 팝업의 두 번째 페이지(선택지 노출)로 전환함.
        /// </summary>
        /// <param name="playerIdx">플레이어 인덱스</param>
        /// <param name="duration">페이드 소요 시간</param>
        /// <param name="distance">현재 거리</param>
        /// <returns>IEnumerator 루틴</returns>
        public IEnumerator ShowQuestionPhase2Routine(int playerIdx, float duration, int distance)
        {
            CanvasGroup targetYesNo = (playerIdx == 0) ? cgLeftYesNo : cgRightYesNo;
            Text targetInfo = (playerIdx == 0) ? textLeftInfo : textRightInfo; 

            if (_infoFadeRoutines[playerIdx] != null)
            {
                StopCoroutine(_infoFadeRoutines[playerIdx]);
                _infoFadeRoutines[playerIdx] = null;
            }

            // 이유: 초반 질문이 아닐 경우 조작 유도를 위한 안내 텍스트를 무입력 시에만 페이드인함.
            if (distance > 10)
            {
                _infoFadeRoutines[playerIdx] = StartCoroutine(InactivityInfoFadeRoutine(playerIdx, targetInfo, 3.0f, 0.5f));
            }
            else
            {
                if (targetInfo)
                {
                    Color c = targetInfo.color;
                    c.a = 1f;
                    targetInfo.color = c;
                }
            }

            if (targetYesNo)
            {
                targetYesNo.gameObject.SetActive(true);
                StartCoroutine(FadeCanvasGroup(targetYesNo, 0f, 1f, duration));
            }

            SwitchPageState(playerIdx, true); 

            yield return CoroutineData.GetWaitForSeconds(duration);
        }
        
        /// <summary>
        /// 무입력 상태가 일정 시간 지속되면 안내 텍스트를 페이드인함.
        /// </summary>
        /// <param name="playerIdx">플레이어 인덱스</param>
        /// <param name="infoText">대상 텍스트 컴포넌트</param>
        /// <param name="waitTime">대기 시간</param>
        /// <param name="fadeDuration">페이드 소요 시간</param>
        /// <returns>IEnumerator 루틴</returns>
        private IEnumerator InactivityInfoFadeRoutine(int playerIdx, Text infoText, float waitTime, float fadeDuration)
        {
            if (!infoText) yield break;

            Color c = infoText.color;
            c.a = 0f;
            infoText.color = c;

            _lastInputTime[playerIdx] = Time.time;

            // # TODO: 루프 대기 조건을 WaitUntil로 변경하여 연산 최적화 고려 필요.
            while (true)
            {
                float idleTime = Time.time - _lastInputTime[playerIdx];

                if (idleTime >= waitTime)
                {
                    yield return StartCoroutine(FadeTextAlpha(infoText, infoText.color.a, 1f, fadeDuration));
                    yield break; 
                }

                yield return null;
            }
        }

        /// <summary>
        /// 답변 입력 게이지 이미지를 초기화함.
        /// </summary>
        /// <param name="playerIdx">플레이어 인덱스</param>
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

        /// <summary>
        /// 이미지 배열의 Fill Amount를 0으로 초기화함.
        /// </summary>
        /// <param name="imgs">초기화할 이미지 배열</param>
        private void ClearImages(Image[] imgs)
        {
            if (imgs == null) return;

            foreach (Image img in imgs)
            {
                if (img) img.fillAmount = 0f;
            }
        }

        /// <summary>
        /// 답변 선택지 발판 입력에 따라 게이지 UI를 갱신함.
        /// </summary>
        /// <param name="playerIdx">플레이어 인덱스</param>
        /// <param name="isYesLane">Yes 레인 여부</param>
        /// <param name="stepCount">입력 누적 횟수</param>
        /// <returns>게이지 완충 여부</returns>
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
            
            float totalFillNeeded = stepCount * 1.0f;
            
            for (int i = targetImages.Length - 1; i >= 0; i--)
            {
                if (!targetImages[i]) continue;
                
                // 예시: totalFillNeeded(2.5) -> amount = 1.0, total = 1.5 -> 다음 루프 amount = 1.0, total = 0.5 -> 마지막 amount = 0.5
                float amount = Mathf.Clamp01(totalFillNeeded);
                targetImages[i].fillAmount = amount;
                totalFillNeeded -= 1.0f;
            }
            
            if (stepCount >= 5)
            {
                if (targetIcon) targetIcon.color = activeColor;
                if (SoundManager.Instance) SoundManager.Instance.PlaySFX("공통_22");
                return true; 
            }
            return false; 
        }

        /// <summary>
        /// 특정 플레이어의 질문 팝업을 페이드아웃하여 숨김.
        /// </summary>
        /// <param name="playerIdx">플레이어 인덱스</param>
        /// <param name="duration">페이드 소요 시간</param>
        public void HideQuestionPopup(int playerIdx, float duration)
        {
            if (_infoFadeRoutines[playerIdx] != null)
            {
                StopCoroutine(_infoFadeRoutines[playerIdx]);
                _infoFadeRoutines[playerIdx] = null;
            }

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

        /// <summary>
        /// Text 컴포넌트에 TextSetting 데이터를 적용함.
        /// </summary>
        /// <param name="targetText">대상 텍스트 컴포넌트</param>
        /// <param name="setting">적용할 데이터</param>
        private void ApplyTextSetting(Text targetText, TextSetting setting)
        {
            if (!targetText) return;

            if (setting != null)
            {
                if (UIManager.Instance) UIManager.Instance.SetText(targetText.gameObject, setting);
                else targetText.text = setting.text;
            }
            else
            {
                // 이유: Fallback으로 빈 문자열을 넣지 않고 오류 로그로 대체함.
                Debug.LogWarning("TextSetting 데이터가 누락됨.");
            }
        }

        /// <summary>
        /// 현재 밟고 있는 선택지 방향에 피드백(색상)을 적용함.
        /// </summary>
        /// <param name="playerIdx">플레이어 인덱스</param>
        /// <param name="isYes">Yes 선택 여부</param>
        public void SetAnswerFeedback(int playerIdx, bool isYes)
        {
            Image targetYes = (playerIdx == 0) ? p1YesOut : p2YesOut;
            Image targetNo = (playerIdx == 0) ? p1NoOut : p2NoOut;

            if (targetYes) targetYes.color = isYes ? activeColor : Color.white;
            if (targetNo) targetNo.color = !isYes ? activeColor : Color.white;
        }

        /// <summary>
        /// 선택지 피드백 색상을 원래대로 복구함.
        /// </summary>
        /// <param name="playerIdx">플레이어 인덱스</param>
        public void ResetAnswerFeedback(int playerIdx)
        {
            Image targetYes = (playerIdx == 0) ? p1YesOut : p2YesOut;
            Image targetNo = (playerIdx == 0) ? p1NoOut : p2NoOut;

            if (targetYes) targetYes.color = Color.white;
            if (targetNo) targetNo.color = Color.white;
        }

        /// <summary>
        /// 질문 팝업 내부의 페이지(질문 -> 선택지)를 전환함.
        /// </summary>
        /// <param name="playerIdx">플레이어 인덱스</param>
        /// <param name="toPage2">2페이지로 전환 여부</param>
        private void SwitchPageState(int playerIdx, bool toPage2)
        {
            CanvasGroup p1 = (playerIdx == 0) ? cgLeftPage1 : cgRightPage1;
            CanvasGroup p2 = (playerIdx == 0) ? cgLeftPage2 : cgRightPage2;

            if (!p1 || !p2) 
            {
                Debug.LogWarning("SwitchPageState 캔버스 그룹 누락됨.");
                return;
            }

            if (runningPageRoutines[playerIdx] != null) StopCoroutine(runningPageRoutines[playerIdx]);
            
            if (toPage2) 
                runningPageRoutines[playerIdx] = StartCoroutine(SequentialPageTransition(p1, p2, 0.5f, 0.5f));
            else 
                runningPageRoutines[playerIdx] = StartCoroutine(SequentialPageTransition(p2, p1, 0.5f, 0.5f));
        }

        /// <summary>
        /// 화면 중앙에 성공 메시지를 잠시 노출함.
        /// </summary>
        /// <param name="message">출력할 메시지</param>
        /// <param name="duration">노출 시간</param>
        /// <returns>IEnumerator 루틴</returns>
        public IEnumerator ShowSuccessText(string message, float duration)
        {
            if (!centerText) yield break;

            centerText.text = message;
            centerText.gameObject.SetActive(true);
            yield return StartCoroutine(FadeTextAlpha(centerText, 0f, 1f, 0.5f));
            yield return CoroutineData.GetWaitForSeconds(duration);
            yield return StartCoroutine(FadeTextAlpha(centerText, 1f, 0f, 0.5f));
            
            centerText.gameObject.SetActive(false);
        }
        
        /// <summary>
        /// 플레이어 입력 발생 시간을 기록하여 방치 상태를 체크함.
        /// </summary>
        /// <param name="playerIdx">플레이어 인덱스</param>
        public void NotifyInput(int playerIdx)
        {
            if (playerIdx >= 0 && playerIdx < 2)
            {
                _lastInputTime[playerIdx] = Time.time;
            }
        }
        
        /// <summary>
        /// 두 CanvasGroup을 순차적으로 페이드 전환함.
        /// </summary>
        /// <param name="fromGroup">퇴장할 CanvasGroup</param>
        /// <param name="toGroup">등장할 CanvasGroup</param>
        /// <param name="fadeOutTime">퇴장 소요 시간</param>
        /// <param name="fadeInTime">등장 소요 시간</param>
        /// <returns>IEnumerator 루틴</returns>
        private IEnumerator SequentialPageTransition(CanvasGroup fromGroup, CanvasGroup toGroup, float fadeOutTime, float fadeInTime)
        {
            if (fromGroup.gameObject.activeSelf)
            {
                yield return StartCoroutine(FadeCanvasGroup(fromGroup, fromGroup.alpha, 0f, fadeOutTime));
            }
            
            toGroup.gameObject.SetActive(true);
            toGroup.alpha = 0f;
            yield return StartCoroutine(FadeCanvasGroup(toGroup, 0f, 1f, fadeInTime));
        }

        /// <summary>
        /// CanvasGroup의 알파값을 선형 보간하여 투명도를 조절함.
        /// </summary>
        /// <param name="cg">대상 CanvasGroup</param>
        /// <param name="start">시작 알파값</param>
        /// <param name="end">종료 알파값</param>
        /// <param name="duration">소요 시간</param>
        /// <returns>IEnumerator 루틴</returns>
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
            
            // # TODO: 다중 UI 갱신 최적화를 위해 Canvas 배치 처리 분리 고려.
            while (t < duration)
            {
                t += Time.deltaTime;
                // 예시 입력: start(0f), end(1f), t(0.25f), duration(0.5f) -> 결과값 = 0.5f
                cg.alpha = Mathf.Lerp(start, end, t / duration);
                yield return null;
            }

            cg.alpha = end;
            if (end <= 0f) cg.gameObject.SetActive(false);
        }

        /// <summary>
        /// 텍스트 컴포넌트의 폰트 색상 알파값을 선형 보간하여 투명도를 조절함.
        /// </summary>
        /// <param name="txt">대상 텍스트 컴포넌트</param>
        /// <param name="start">시작 알파값</param>
        /// <param name="end">종료 알파값</param>
        /// <param name="duration">소요 시간</param>
        /// <returns>IEnumerator 루틴</returns>
        private IEnumerator FadeTextAlpha(Text txt, float start, float end, float duration)
        {
            if (!txt) yield break;

            float t = 0f;
            Color c = txt.color;
            
            while (t < duration)
            {
                t += Time.deltaTime;
                // 예시 입력: start(1f), end(0f), t(0.1f), duration(0.2f) -> 결과값 = 0.5f
                float a = Mathf.Lerp(start, end, t / duration);
                txt.color = new Color(c.r, c.g, c.b, a);
                yield return null;
            }
            
            txt.color = new Color(c.r, c.g, c.b, end);
        }
    }
}