using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using My.Scripts.UI;
using UnityEngine;
using UnityEngine.UI;
using VContainer;
using Wonjeong.Data;
using Wonjeong.UI;

namespace My.Scripts._02_PlayTutorial.Managers
{
    /// <summary>
    /// PlayTutorial 씬의 전반적인 UI 상태와 연출을 관리하는 클래스.
    /// </summary>
    public class PlayTutorialUIManager : MonoBehaviour
    {
        [Header("Player Name UI")]
        [SerializeField] private Text p1NameText;
        [SerializeField] private Text p2NameText;

        [Header("Player Color Balls")]
        [SerializeField] private Image ballImageA;
        [SerializeField] private Image ballImageB;

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

        private CancellationTokenSource _destroyCts;
        private SoundManager _soundManager;
        private UIManager _uiManager;

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
            if (_destroyCts != null)
            {
                _destroyCts.Cancel();
                _destroyCts.Dispose();
            }
        }

        /// <summary>
        /// UI 컴포넌트들의 초기 상태를 설정함.
        /// </summary>
        public void InitUI(float maxDistance)
        {
            if (p1Gauge) p1Gauge.UpdateGauge(0, maxDistance);
            if (p2Gauge) p2Gauge.UpdateGauge(0, maxDistance);

            if (centerText) centerText.gameObject.SetActive(false);

            StopAllArrows();

            if (finalPageCanvasGroup)
            {
                finalPageCanvasGroup.alpha = 0f;
                finalPageCanvasGroup.gameObject.SetActive(false);
                finalPageCanvasGroup.blocksRaycasts = false;
            }
        }

        /// <summary>
        /// 플레이어 이름 UI를 동적으로 치환하여 설정함.
        /// </summary>
        public void SetPlayerNames(string nameA, string nameB, TextSetting settingA, TextSetting settingB)
        {
            UIUtils.ApplyPlayerNames(_uiManager, p1NameText, p2NameText, nameA, nameB, settingA, settingB);
        }

        /// <summary>
        /// 플레이어의 고유 색상에 맞춰 볼 스프라이트를 변경함.
        /// </summary>
        public void SetPlayerBalls(Sprite spriteA, Sprite spriteB)
        {
            if (ballImageA && spriteA) ballImageA.sprite = spriteA;
            if (ballImageB && spriteB) ballImageB.sprite = spriteB;
        }

        /// <summary>
        /// 개별 플레이어의 진행도 게이지를 업데이트함.
        /// </summary>
        public void UpdateGauge(int playerIdx, float current, float max)
        {
            if (playerIdx == 0 && p1Gauge) p1Gauge.UpdateGauge(current, max);
            else if (playerIdx == 1 && p2Gauge) p2Gauge.UpdateGauge(current, max);
        }

        /// <summary>
        /// 모든 방향 지시 화살표 애니메이션을 강제 중단함.
        /// </summary>
        private void StopAllArrows()
        {
            if (p1RightArrow) p1RightArrow.Stop();
            if (p2RightArrow) p2RightArrow.Stop();
            if (p1LeftArrow) p1LeftArrow.Stop();
            if (p2LeftArrow) p2LeftArrow.Stop();
        }

        /// <summary>
        /// 특정 플레이어의 방향 지시 화살표를 활성화하고 애니메이션을 재생함.
        /// </summary>
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

        /// <summary>
        /// 재생 중인 방향 지시 화살표를 부드럽게 페이드아웃하며 정지시킴.
        /// </summary>
        public void StopArrowFadeOut(int playerIdx, bool isRight, float duration)
        {
            UIArrowAnimator target = null;
            if (playerIdx == 0) target = isRight ? p1RightArrow : p1LeftArrow;
            else target = isRight ? p2RightArrow : p2LeftArrow;

            if (target && target.gameObject.activeSelf)
            {
                target.FadeOutAndStop(duration);
            }
        }

        /// <summary>
        /// 페이드인 연출 없이 팝업을 즉시 화면에 노출함.
        /// </summary>
        public void ShowPopupImmediately(string text)
        {   
            if (_soundManager) _soundManager.PlaySFX("공통_7");
            
            if (popupText) popupText.text = text;
            
            if (popup)
            {
                popup.alpha = 1f;
                popup.blocksRaycasts = true;
            }
        }

        /// <summary>
        /// 팝업 노출 전 내용을 미리 세팅하고 투명 상태로 대기함.
        /// </summary>
        public void PreparePopup(string text)
        {
            if (popupText) popupText.text = text;
            
            if (popup)
            {
                popup.alpha = 0f;
                popup.blocksRaycasts = true;
            }
        }

        /// <summary>
        /// 준비된 팝업을 부드럽게 페이드인함.
        /// </summary>
        public async UniTask FadeInPopupAsync(float duration)
        {   
            if (!popup) return;

            if (!popup.gameObject.activeInHierarchy) popup.gameObject.SetActive(true);
            if (_soundManager) _soundManager.PlaySFX("공통_7");

            CancellationToken token = _destroyCts.Token;
            await FadeCanvasGroupAsync(popup, 0f, 1f, duration, token);
        }

        /// <summary>
        /// 노출된 팝업을 부드럽게 페이드아웃함.
        /// </summary>
        public void HidePopup(float duration)
        {
            if (!popup) return;
            
            CancellationToken token = _destroyCts.Token;
            FadeCanvasGroupAsync(popup, popup.alpha, 0f, duration, token).Forget();
            popup.blocksRaycasts = false;
        }
        
        /// <summary>
        /// 기존 팝업 텍스트를 페이드아웃하고 내용을 변경한 뒤 다시 페이드인함.
        /// </summary>
        public async UniTask FadeOutPopupTextAndChangeAsync(string newText, float fadeOutTime, float fadeInTime)
        {
            CancellationToken token = _destroyCts.Token;
            await FadeTextAlphaAsync(popupText, 1f, 0f, fadeOutTime, token);
            
            if (popupText) popupText.text = newText;
            
            await FadeTextAlphaAsync(popupText, 0f, 1f, fadeInTime, token);
        }

        /// <summary>
        /// 화면 중앙에 성공 메시지를 일정 시간 띄운 뒤 사라지게 함.
        /// </summary>
        public async UniTask ShowSuccessTextAsync(string message, float duration)
        {
            if (!centerText) return;

            centerText.text = message;
            centerText.gameObject.SetActive(true);
            
            if (_soundManager) _soundManager.PlaySFX("공통_20");
            
            CancellationToken token = _destroyCts.Token;
            await FadeTextAlphaAsync(centerText, 0f, 1f, 0.25f, token);
            await UniTask.Delay(TimeSpan.FromSeconds(duration), cancellationToken: token);
            await FadeTextAlphaAsync(centerText, 1f, 0f, 0.25f, token);

            centerText.gameObject.SetActive(false);
        }

        /// <summary>
        /// 튜토리얼 종료 전 최종 안내 문구들을 순차적으로 연출함.
        /// </summary>
        public async UniTask RunFinalPageSequenceAsync(TextSetting[] texts)
        {
            if (!finalPageCanvasGroup || !finalPageText || texts == null || texts.Length == 0) return;

            finalPageCanvasGroup.gameObject.SetActive(true);
            finalPageCanvasGroup.alpha = 0f;
            
            ApplyFinalPageText(texts[0]);

            Color c = finalPageText.color;
            finalPageText.color = new Color(c.r, c.g, c.b, 0f);

            CancellationToken token = _destroyCts.Token;
            await FadeCanvasGroupAsync(finalPageCanvasGroup, 0f, 1f, 0.5f, token);

            for (int i = 0; i < texts.Length; i++)
            {
                TextSetting setting = texts[i];
                if (setting == null) continue;

                // 인지 복잡도 격리를 위해 단일 연출 태스크로 추상화 분리
                await RenderFinalPageStepAsync(setting, token);
            }
        }

        private async UniTask RenderFinalPageStepAsync(TextSetting setting, CancellationToken token)
        {
            ApplyFinalPageText(setting);
            
            if (setting.name == "Text_Step1" && _soundManager)
            {
                _soundManager.PlaySFX("공통_13");
            }
            
            await FadeTextAlphaAsync(finalPageText, 0f, 1f, 0.25f, token);
            await UniTask.Delay(TimeSpan.FromSeconds(3.0f), cancellationToken: token);
            await FadeTextAlphaAsync(finalPageText, 1f, 0f, 0.25f, token);
        }

        private void ApplyFinalPageText(TextSetting setting)
        {
            if (_uiManager)
            {
                _uiManager.SetText(finalPageText.gameObject, setting);
            }
            else
            {
                finalPageText.text = setting.text;
            }
        }

        /// <summary>
        /// CanvasGroup의 알파값을 목표 수치까지 선형 보간함.
        /// </summary>
        private async UniTask FadeCanvasGroupAsync(CanvasGroup cg, float start, float end, float duration, CancellationToken ct)
        {
            if (!cg) return;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                cg.alpha = Mathf.Lerp(start, end, elapsed / duration);
                await UniTask.Yield(PlayerLoopTiming.Update, ct);
            }

            cg.alpha = end;
            if (end <= 0f) cg.gameObject.SetActive(false);
        }

        /// <summary>
        /// 텍스트 색상의 알파값을 목표 수치까지 선형 보간함.
        /// </summary>
        private async UniTask FadeTextAlphaAsync(Text txt, float start, float end, float duration, CancellationToken ct)
        {
            if (!txt) return;
            float elapsed = 0f;
            Color c = txt.color;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float alpha = Mathf.Lerp(start, end, elapsed / duration);
                txt.color = new Color(c.r, c.g, c.b, alpha);
                await UniTask.Yield(PlayerLoopTiming.Update, ct);
            }

            txt.color = new Color(c.r, c.g, c.b, end);
        }
    }
}