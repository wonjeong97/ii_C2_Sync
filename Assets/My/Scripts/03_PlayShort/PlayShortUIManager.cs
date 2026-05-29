using System;
using System.Threading;
using Cysharp.Text;
using Cysharp.Threading.Tasks;
using My.Scripts.UI;
using R3;
using UnityEngine;
using UnityEngine.UI;
using VContainer;
using Wonjeong.Data;
using Wonjeong.UI;

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

        private readonly Color activeColor = new Color(248f / 255f, 237f / 255f, 166f / 255f);
        private float[] _lastInputTime = new float[2];
        
        private DisposableBag[] _infoFadeBags = new DisposableBag[2];
        private DisposableBag _pageTransitionBag = new DisposableBag();
        private CancellationTokenSource _destroyCts;

        private SoundManager _soundManager;
        private UIManager _uiManager;

        private struct InfoFadeState
        {
            public PlayShortUIManager Manager;
            public Text InfoText;
            public int PlayerIndex;
            public float Duration;
        }

        [Inject]
        public void Construct(SoundManager soundManager, UIManager uiManager)
        {
            _soundManager = soundManager;
            _uiManager = uiManager;
        }
        
        private void Awake()
        {
            _destroyCts = new CancellationTokenSource();
        }
        
        private void OnDestroy()
        {
            _destroyCts?.Cancel();
            _destroyCts?.Dispose();

            _pageTransitionBag.Dispose();
            for (int i = 0; i < _infoFadeBags.Length; i++)
            {
                _infoFadeBags[i].Dispose();
            }
        }
        
        /// <summary>
        /// UI 컴포넌트의 초기 상태를 설정함.
        /// </summary>
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

            DisableCanvasGroup(cgLeftYesNo);
            DisableCanvasGroup(cgRightYesNo);

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
        public void SetPlayerNames(string nameA, string nameB, TextSetting settingA, TextSetting settingB)
        {
            UIUtils.ApplyPlayerNames(_uiManager, p1NameText, p2NameText, nameA, nameB, settingA, settingB);
        }

        /// <summary>
        /// 플레이어 색상 스프라이트를 지정함.
        /// </summary>
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
            CancellationToken token = _destroyCts.Token;
            if (popupFinishP1 && popupFinishP1.gameObject.activeSelf)
            {
                UIUtils.FadeCanvasGroupAsync(popupFinishP1, popupFinishP1.alpha, 0f, 0.1f, token, false).Forget();
            }
            if (popupFinishP2 && popupFinishP2.gameObject.activeSelf)
            {
                UIUtils.FadeCanvasGroupAsync(popupFinishP2, popupFinishP2.alpha, 0f, 0.1f, token, false).Forget();
            }
        }

        /// <summary>
        /// 양 플레이어 완료 시 뜨는 중앙 팝업을 표시함.
        /// </summary>
        public void ShowCenterFinishPopup(TextSetting textData)
        {
            if (!popupCenter) return;

            ApplyTextSetting(textCenter, textData);
            popupCenter.gameObject.SetActive(true);
            popupCenter.alpha = 0f;
            UIUtils.FadeCanvasGroupAsync(popupCenter, 0f, 1f, 0.1f, _destroyCts.Token).Forget();
        }
        
        /// <summary>
        /// 진행도 게이지를 완료 상태의 스프라이트로 변경함.
        /// </summary>
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
        public void ShowWaitingPopup(int playerIdx, TextSetting textData)
        {
            CanvasGroup targetPopup = (playerIdx == 0) ? popupFinishP1 : popupFinishP2;
            Text targetText = (playerIdx == 0) ? textFinishP1 : textFinishP2;
            
            if (!targetPopup) return;

            ApplyTextSetting(targetText, textData);
            targetPopup.gameObject.SetActive(true);
            targetPopup.alpha = 0f;
            
            if (_soundManager) _soundManager.PlaySFX("공통_7");
            UIUtils.FadeCanvasGroupAsync(targetPopup, 0f, 1f, 0.1f, _destroyCts.Token).Forget();
        }

        /// <summary>
        /// 캔버스 그룹을 즉시 투명화 및 비활성화함.
        /// </summary>
        private void HidePopupImmediate(CanvasGroup cg)
        {
            if (cg)
            {
                cg.alpha = 0f;
                cg.blocksRaycasts = false;
                cg.gameObject.SetActive(false);
            }
        }
        
        private void DisableCanvasGroup(CanvasGroup cg)
        {
            if (cg)
            {
                cg.alpha = 0f;
                cg.gameObject.SetActive(false);
            }
        }

        /// <summary>
        /// 특정 플레이어의 UI 게이지 값을 갱신함.
        /// </summary>
        public void UpdateGauge(int playerIdx, float current, float max)
        {
            if (playerIdx == 0 && p1Gauge) p1Gauge.UpdateGauge(current, max);
            else if (playerIdx == 1 && p2Gauge) p2Gauge.UpdateGauge(current, max);
        }

        /// <summary>
        /// 특정 플레이어의 질문 팝업을 활성화함.
        /// </summary>
        public void ShowQuestionPopup(int playerIdx, int distance, TextSetting questionDataPage1, TextSetting questionDataPage2, TextSetting infoData)
        {
            CanvasGroup targetPopup = (playerIdx == 0) ? popupQuestionLeft : popupQuestionRight;
            if (!targetPopup) return;

            ResetAnswerFeedback(playerIdx);
            ResetGaugeImages(playerIdx);
            InitializePopupPanels(playerIdx);

            Text targetDistText = (playerIdx == 0) ? textLeftDistance : textRightDistance;
            if (targetDistText)
            {
                targetDistText.text = ZString.Concat(distance, "M");
            }

            SetupQuestionTexts(playerIdx, questionDataPage1, questionDataPage2, infoData);

            Text targetInfo = (playerIdx == 0) ? textLeftInfo : textRightInfo;
            if (targetInfo)
            {
                Color c = targetInfo.color;
                targetInfo.color = new Color(c.r, c.g, c.b, distance <= 10 ? 1f : 0f);
            }

            targetPopup.gameObject.SetActive(true);
            targetPopup.alpha = 0f; 
            targetPopup.blocksRaycasts = true; 
            UIUtils.FadeCanvasGroupAsync(targetPopup, 0f, 1f, 0.5f, _destroyCts.Token).Forget();
        }
        
        private void InitializePopupPanels(int playerIdx)
        {
            CanvasGroup targetPage1 = (playerIdx == 0) ? cgLeftPage1 : cgRightPage1;
            CanvasGroup targetPage2 = (playerIdx == 0) ? cgLeftPage2 : cgRightPage2;
            CanvasGroup targetYesNo = (playerIdx == 0) ? cgLeftYesNo : cgRightYesNo;

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
        }
        
        private void SetupQuestionTexts(int playerIdx, TextSetting questionDataPage1, TextSetting questionDataPage2, TextSetting infoData)
        {
            Text targetQueP1 = (playerIdx == 0) ? textLeftQuestionP1 : textRightQuestionP1;
            Text targetQueP2 = (playerIdx == 0) ? textLeftQuestionP2 : textRightQuestionP2;
            Text targetInfo = (playerIdx == 0) ? textLeftInfo : textRightInfo;

            ApplyTextSetting(targetQueP1, questionDataPage1);
            if (targetQueP1) targetQueP1.text = ZString.Concat("Q.", targetQueP1.text);

            ApplyTextSetting(targetQueP2, questionDataPage2);
            if (targetQueP2) targetQueP2.text = ZString.Concat("Q.", targetQueP2.text);

            ApplyTextSetting(targetInfo, infoData);
        }

        /// <summary>
        /// 질문 팝업의 두 번째 페이지(선택지 노출)로 전환함.
        /// </summary>
        public async UniTask ShowQuestionPhase2RoutineAsync(int playerIdx, float duration, int distance)
        {
            CanvasGroup targetYesNo = (playerIdx == 0) ? cgLeftYesNo : cgRightYesNo;
            Text targetInfo = (playerIdx == 0) ? textLeftInfo : textRightInfo; 

            _infoFadeBags[playerIdx].Dispose();
            _infoFadeBags[playerIdx] = new DisposableBag();

            if (targetYesNo)
            {
                targetYesNo.gameObject.SetActive(true);
                UIUtils.FadeCanvasGroupAsync(targetYesNo, 0f, 1f, duration, _destroyCts.Token).Forget();
            }

            SwitchPageState(playerIdx, true); 
    
            // Page2 연출이 완전히 끝날 때까지 대기
            await UniTask.Delay(TimeSpan.FromSeconds(duration), cancellationToken: _destroyCts.Token);

            // 연출 완료 후부터 3초 입력 대기 타이머 시작
            if (distance > 10)
            {
                _lastInputTime[playerIdx] = Time.time;

                InfoFadeState state = new InfoFadeState
                {
                    Manager = this,
                    InfoText = targetInfo,
                    PlayerIndex = playerIdx,
                    Duration = 0.5f // Fade duration
                };

                Observable.Interval(TimeSpan.FromSeconds(0.1f))
                    .Where(state, (tick, s) => (Time.time - s.Manager._lastInputTime[s.PlayerIndex]) >= 3.0f)
                    .Take(1)
                    .Subscribe(state, (tick, s) => 
                    {
                        s.Manager.FadeTextAlphaAsync(s.InfoText, 0f, 1f, s.Duration, s.Manager._destroyCts.Token).Forget();
                    })
                    .AddTo(ref _infoFadeBags[playerIdx]);
            }
            else if (targetInfo)
            {
                Color c = targetInfo.color;
                targetInfo.color = new Color(c.r, c.g, c.b, 1f);
            }
        }

        /// <summary>
        /// 답변 입력 게이지 이미지를 초기화함.
        /// </summary>
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
            
            // 인지 복잡도 분해를 위해 서브 연출 단계 메서드로 전사 분리
            ApplyGaugeFillAmount(targetImages, stepCount);
            
            if (stepCount >= 5)
            {
                if (targetIcon) targetIcon.color = activeColor;
                if (_soundManager) _soundManager.PlaySFX("공통_22");
                return true; 
            }
            return false; 
        }

        private void ApplyGaugeFillAmount(Image[] targetImages, int stepCount)
        {
            float totalFillNeeded = stepCount * 1.0f;
            for (int i = targetImages.Length - 1; i >= 0; i--)
            {
                if (!targetImages[i]) continue;
                
                float amount = Mathf.Clamp01(totalFillNeeded);
                targetImages[i].fillAmount = amount;
                totalFillNeeded -= 1.0f;
            }
        }
        
        /// <summary>
        /// 화면 중앙에 성공 메시지를 잠시 노출함.
        /// </summary>
        public async UniTask ShowSuccessTextAsync(string message, float duration)
        {
            if (!centerText) return;

            centerText.text = message;
            centerText.gameObject.SetActive(true);
            await FadeTextAlphaAsync(centerText, 0f, 1f, 0.5f, _destroyCts.Token);
            await UniTask.Delay(TimeSpan.FromSeconds(duration), cancellationToken: _destroyCts.Token);
            await FadeTextAlphaAsync(centerText, 1f, 0f, 0.5f, _destroyCts.Token);
            
            centerText.gameObject.SetActive(false);
        }

        /// <summary>
        /// 특정 플레이어의 질문 팝업을 페이드아웃하여 숨김.
        /// </summary>
        public void HideQuestionPopup(int playerIdx, float duration)
        {
            _infoFadeBags[playerIdx].Dispose();

            Text targetInfo = (playerIdx == 0) ? textLeftInfo : textRightInfo;
            if (targetInfo)
            {
                Color c = targetInfo.color;
                targetInfo.color = new Color(c.r, c.g, c.b, 0f);
            }

            CanvasGroup targetPopup = (playerIdx == 0) ? popupQuestionLeft : popupQuestionRight;
            if (targetPopup && targetPopup.gameObject.activeInHierarchy)
            {
                targetPopup.blocksRaycasts = false;
                UIUtils.FadeCanvasGroupAsync(targetPopup, targetPopup.alpha, 0f, duration, _destroyCts.Token, false).Forget();
            }
        }

        /// <summary>
        /// Text 컴포넌트에 TextSetting 데이터를 적용함.
        /// </summary>
        private void ApplyTextSetting(Text targetText, TextSetting setting)
        {
            if (!targetText) return;

            if (setting != null)
            {
                if (_uiManager) _uiManager.SetText(targetText.gameObject, setting);
                else targetText.text = setting.text;
            }
        }

        /// <summary>
        /// 현재 밟고 있는 선택지 방향에 피드백(색상)을 적용함.
        /// </summary>
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

            _pageTransitionBag.Dispose();

            if (toPage2) 
                SequentialPageTransitionAsync(p1, p2, 0.5f, 0.5f).Forget();
            else 
                SequentialPageTransitionAsync(p2, p1, 0.5f, 0.5f).Forget();
        }
        
        /// <summary>
        /// 플레이어 입력 발생 시간을 기록하여 방치 상태를 체크함.
        /// </summary>
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
        private async UniTaskVoid SequentialPageTransitionAsync(CanvasGroup fromGroup, CanvasGroup toGroup, float fadeOutTime, float fadeInTime)
        {
            if (fromGroup.gameObject.activeSelf)
            {
                await UIUtils.FadeCanvasGroupAsync(fromGroup, fromGroup.alpha, 0f, fadeOutTime, _destroyCts.Token, false);
            }
            
            toGroup.gameObject.SetActive(true);
            toGroup.alpha = 0f;
            await UIUtils.FadeCanvasGroupAsync(toGroup, 0f, 1f, fadeInTime, _destroyCts.Token);
        }

        /// <summary>
        /// 텍스트 컴포넌트의 폰트 색상 알파값을 선형 보간하여 투명도를 조절함.
        /// </summary>
        private async UniTask FadeTextAlphaAsync(Text txt, float start, float end, float duration, CancellationToken ct)
        {
            if (!txt) return;

            float elapsed = 0f;
            Color c = txt.color;
            
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float a = Mathf.Lerp(start, end, elapsed / duration);
                txt.color = new Color(c.r, c.g, c.b, a);
                await UniTask.Yield(PlayerLoopTiming.Update, ct);
            }
            
            txt.color = new Color(c.r, c.g, c.b, end);
        }
    }
}