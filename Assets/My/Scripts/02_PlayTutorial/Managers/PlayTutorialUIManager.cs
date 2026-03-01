using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Wonjeong.Data;
using Wonjeong.UI;
using Wonjeong.Utils;

namespace My.Scripts._02_PlayTutorial.Managers
{
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

        /// <summary>
        /// API에서 받아온 유저 이름과 JSON 텍스트 세팅을 UI에 적용함.
        /// 외부 데이터(JSON)로 포맷과 스타일을 관리하여 기획 변경에 유연하게 대응하기 위함.
        /// </summary>
        public void SetPlayerNames(string nameA, string nameB, TextSetting settingA, TextSetting settingB)
        {
            if (p1NameText)
            {
                if (settingA != null)
                {
                    if (UIManager.Instance) UIManager.Instance.SetText(p1NameText.gameObject, settingA);
                    p1NameText.text = settingA.text.Replace("{nameA}", nameA);
                }
                else
                {
                    p1NameText.text = $"{nameA}님의 위치";
                }
            }
            else
            {
                Debug.LogWarning("[PlayTutorialUIManager] P1 이름 텍스트 컴포넌트가 할당되지 않았습니다.");
            }

            if (p2NameText)
            {
                if (settingB != null)
                {
                    if (UIManager.Instance) UIManager.Instance.SetText(p2NameText.gameObject, settingB);
                    p2NameText.text = settingB.text.Replace("{nameB}", nameB);
                }
                else
                {
                    p2NameText.text = $"{nameB}님의 위치";
                }
            }
            else
            {
                Debug.LogWarning("[PlayTutorialUIManager] P2 이름 텍스트 컴포넌트가 할당되지 않았습니다.");
            }
        }

        /// <summary>
        /// API에서 받아온 컬러 기반의 스프라이트를 플레이어 이름 옆 공 이미지에 적용함.
        /// 플레이어가 자신의 조작 영역과 색상을 직관적으로 매칭할 수 있게 돕기 위함.
        /// </summary>
        /// <param name="spriteA">GameManager에서 추출한 Player A의 스프라이트</param>
        /// <param name="spriteB">GameManager에서 추출한 Player B의 스프라이트</param>
        public void SetPlayerBalls(Sprite spriteA, Sprite spriteB)
        {
            if (ballImageA)
            {
                if (spriteA)
                {
                    ballImageA.sprite = spriteA;
                }
                else
                {
                    Debug.LogWarning("[PlayTutorialUIManager] Player A 컬러 스프라이트가 누락되어 기본 이미지를 유지합니다.");
                }
            }
            else
            {
                Debug.LogWarning("[PlayTutorialUIManager] ballImageA 컴포넌트가 연결되지 않았습니다.");
            }

            if (ballImageB)
            {
                if (spriteB)
                {
                    ballImageB.sprite = spriteB;
                }
                else
                {
                    Debug.LogWarning("[PlayTutorialUIManager] Player B 컬러 스프라이트가 누락되어 기본 이미지를 유지합니다.");
                }
            }
            else
            {
                Debug.LogWarning("[PlayTutorialUIManager] ballImageB 컴포넌트가 연결되지 않았습니다.");
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
            UIArrowAnimator target;
            if (playerIdx == 0) target = isRight ? p1RightArrow : p1LeftArrow;
            else target = isRight ? p2RightArrow : p2LeftArrow;

            if (target && target.gameObject.activeSelf)
                target.FadeOutAndStop(duration);
        }

        // --- Popup & Text Control (Tutorial Generic) ---

        public void ShowPopupImmediately(string text)
        {   
            SoundManager.Instance?.PlaySFX("공통_7");
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
            SoundManager.Instance?.PlaySFX("공통_7");
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
            SoundManager.Instance?.PlaySFX("공통_20");
            
            yield return StartCoroutine(FadeTextAlpha(centerText, 0f, 1f, 0.5f));
            yield return CoroutineData.GetWaitForSeconds(duration);
            yield return StartCoroutine(FadeTextAlpha(centerText, 1f, 0f, 0.5f));

            centerText.gameObject.SetActive(false);
        }

        public IEnumerator RunFinalPageSequence(TextSetting[] texts)
        {
            if (!finalPageCanvasGroup || !finalPageText) yield break;
            if (texts == null || texts.Length == 0) yield break;

            finalPageCanvasGroup.gameObject.SetActive(true);
            finalPageCanvasGroup.alpha = 0f;
            
            if (texts[0] != null)
            {
                if (UIManager.Instance) UIManager.Instance.SetText(finalPageText.gameObject, texts[0]);
                else finalPageText.text = texts[0].text;
            }

            Color c = finalPageText.color;
            finalPageText.color = new Color(c.r, c.g, c.b, 0f);

            yield return StartCoroutine(FadeCanvasGroup(finalPageCanvasGroup, 0f, 1f, 1f));

            foreach (TextSetting setting in texts)
            {
                if (setting == null) continue;

                if (UIManager.Instance)
                {
                    UIManager.Instance.SetText(finalPageText.gameObject, setting);
                }
                else
                {
                    finalPageText.text = setting.text;
                }
                
                if (setting.name == "Text_Step1")
                {
                    SoundManager.Instance?.PlaySFX("공통_13");
                }
                
                yield return StartCoroutine(FadeTextAlpha(finalPageText, 0f, 1f, 1f));
                yield return CoroutineData.GetWaitForSeconds(3.0f);
                yield return StartCoroutine(FadeTextAlpha(finalPageText, 1f, 0f, 1f));
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