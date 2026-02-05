using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Wonjeong.Data;
using Wonjeong.UI;
using Wonjeong.Utils;

namespace My.Scripts._02_PlayTutorial.Managers
{
    /// <summary>
    /// 튜토리얼 씬(Scene 02)의 UI 요소들을 총괄하는 매니저 클래스.
    /// 팝업 메시지, 방향 지시 화살표, 진행도 게이지, 성공 메시지 등의 연출을 담당함.
    /// </summary>
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

        /// <summary>
        /// 씬 진입 시 UI 상태를 초기화함.
        /// </summary>
        /// <param name="maxDistance">게이지 표시에 사용될 전체 목표 거리</param>
        public void InitUI(float maxDistance)
        {
            // 게이지를 0으로 리셋하여 진행도 초기화
            if (p1Gauge) p1Gauge.UpdateGauge(0, maxDistance);
            if (p2Gauge) p2Gauge.UpdateGauge(0, maxDistance);
            
            // 성공 메시지 텍스트는 숨김 상태로 시작
            if (centerText != null) centerText.gameObject.SetActive(false);
            
            // 튜토리얼 시작 전에는 화살표 가이드가 보이지 않도록 모두 끔
            StopAllArrows();
            
            // 시작 시 Final Page가 안보이도록 설정
            if (finalPageCanvasGroup != null)
            {
                finalPageCanvasGroup.alpha = 0f;
                finalPageCanvasGroup.gameObject.SetActive(false);
                finalPageCanvasGroup.blocksRaycasts = false; // 입력 차단
            }
        }

        /// <summary>
        /// 특정 플레이어의 진행 게이지를 업데이트함.
        /// </summary>
        public void UpdateGauge(int playerIdx, float current, float max)
        {
            if (playerIdx == 0 && p1Gauge) p1Gauge.UpdateGauge(current, max);
            else if (playerIdx == 1 && p2Gauge) p2Gauge.UpdateGauge(current, max);
        }

        // --- Arrow Control ---

        /// <summary>
        /// 화면에 떠있는 모든 화살표 UI를 즉시 비활성화함.
        /// 페이즈 전환 시 이전 가이드가 남아있는 것을 방지하기 위함.
        /// </summary>
        private void StopAllArrows()
        {
            if (p1RightArrow) p1RightArrow.Stop();
            if (p2RightArrow) p2RightArrow.Stop();
            if (p1LeftArrow) p1LeftArrow.Stop();
            if (p2LeftArrow) p2LeftArrow.Stop();
        }

        /// <summary>
        /// 지정된 플레이어에게 특정 방향의 이동 가이드 화살표를 표시함.
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
        /// 화살표를 부드럽게 페이드 아웃시키며 정지함.
        /// 목표 달성 후 가이드가 갑자기 사라지는 위화감을 줄이기 위함.
        /// </summary>
        public void StopArrowFadeOut(int playerIdx, bool isRight, float duration)
        {
            UIArrowAnimator target = null;
            if (playerIdx == 0) target = isRight ? p1RightArrow : p1LeftArrow;
            else target = isRight ? p2RightArrow : p2LeftArrow;

            if (target != null && target.gameObject.activeSelf) 
                target.FadeOutAndStop(duration);
        }

        // --- Popup & Text Control ---

        /// <summary>
        /// 팝업을 즉시 화면에 표시함 (페이드 효과 없음).
        /// 인트로 시작 시 "Start" 메시지처럼 대기 시간 없이 바로 보여줘야 할 때 사용.
        /// </summary>
        public void ShowPopupImmediately(string text)
        {
            if (popupText) popupText.text = text;
            if (popup) { popup.alpha = 1; popup.blocksRaycasts = true; }
        }

        /// <summary>
        /// 팝업 텍스트를 설정하되 화면에는 보이지 않게 준비함.
        /// 이후 FadeInPopup 코루틴을 통해 부드럽게 등장시키기 위함.
        /// </summary>
        public void PreparePopup(string text)
        {
            if (popupText) popupText.text = text;
            if (popup) { popup.alpha = 0; popup.blocksRaycasts = true; }
        }

        /// <summary>
        /// 준비된 팝업을 서서히 나타나게 함.
        /// </summary>
        public IEnumerator FadeInPopup(float duration)
        {
            yield return StartCoroutine(FadeCanvasGroup(popup, 0f, 1f, duration));
        }

        /// <summary>
        /// 팝업을 서서히 사라지게 하고 입력을 차단함.
        /// </summary>
        public void HidePopup(float duration)
        {
            StartCoroutine(FadeCanvasGroup(popup, popup.alpha, 0f, duration));
            if (popup) popup.blocksRaycasts = false;
        }

        /// <summary>
        /// 현재 표시된 텍스트를 페이드 아웃한 뒤, 내용을 변경하고 다시 페이드 인함.
        /// 팝업창을 닫지 않고 내용만 자연스럽게 교체할 때 사용.
        /// </summary>
        public IEnumerator FadeOutPopupTextAndChange(string newText, float fadeOutTime, float fadeInTime)
        {
            yield return StartCoroutine(FadeTextAlpha(popupText, 1f, 0f, fadeOutTime));
            if (popupText) popupText.text = newText;
            yield return StartCoroutine(FadeTextAlpha(popupText, 0f, 1f, fadeInTime));
        }

        /// <summary>
        /// 화면 중앙에 성공 메시지를 잠시 띄웠다 사라지게 함.
        /// 페이즈 완료 시 긍정적 피드백을 주기 위함.
        /// </summary>
        public IEnumerator ShowSuccessText(string message, float duration)
        {
            if (!centerText) yield break;
            
            centerText.text = message;
            centerText.gameObject.SetActive(true);
            
            // 등장 -> 대기 -> 퇴장
            yield return StartCoroutine(FadeTextAlpha(centerText, 0f, 1f, 1.0f));
            yield return new WaitForSeconds(duration);
            yield return StartCoroutine(FadeTextAlpha(centerText, 1f, 0f, 1.0f));
            
            centerText.gameObject.SetActive(false);
        }
        
        /// <summary>
        /// 마지막 페이지를 부드럽게 페이드 인시키는 코루틴.
        /// </summary>
        /// <param name="duration">페이드 시간</param>
        public IEnumerator FadeInFinalPageRoutine(float duration)
        {
            if (finalPageCanvasGroup == null)
            {
                Debug.LogWarning("[UI] Final Page CanvasGroup is missing.");
                yield break;
            }

            // 활성화 후 투명도 0부터 시작
            finalPageCanvasGroup.gameObject.SetActive(true);
            finalPageCanvasGroup.alpha = 0f;

            yield return StartCoroutine(FadeCanvasGroup(finalPageCanvasGroup, 0f, 1f, duration));
            
            // 페이드 완료 후 입력 허용
            finalPageCanvasGroup.blocksRaycasts = true; 
        }
        
        /// <summary>
        /// 마지막 페이지를 띄우고 텍스트 4개를 순차적으로 보여주는 코루틴
        /// 흐름: (등장 1초) -> 2초 유지 -> (퇴장 1초) -> 다음 텍스트 -> (등장 1초) ...
        /// </summary>
        public IEnumerator RunFinalPageSequence(TextSetting[] texts)
        {
            if (finalPageCanvasGroup == null || finalPageText == null || texts == null) yield break;

            // 페이지 활성화 (투명 상태)
            finalPageCanvasGroup.gameObject.SetActive(true);
            finalPageCanvasGroup.alpha = 0f;
            finalPageCanvasGroup.blocksRaycasts = true;

            for (int i = 0; i < texts.Length; i++)
            {
                // 1. 텍스트 데이터 적용 (폰트, 사이즈, 내용 등)
                if (UIManager.Instance != null)
                {
                    UIManager.Instance.SetText(finalPageText.gameObject, texts[i]);
                }
                else
                {
                    finalPageText.text = texts[i].text;
                }

                // 2. 등장 (Fade In 1초)
                if (i == 0)
                {
                    // 첫 번째는 페이지 전체(CanvasGroup)를 페이드 인
                    SetTextAlpha(finalPageText, 1f); // 텍스트는 불투명하게 설정해둠
                    yield return StartCoroutine(FadeCanvasGroup(finalPageCanvasGroup, 0f, 1f, 1.0f));
                }
                else
                {
                    // 두 번째부터는 텍스트만 페이드 인
                    // (텍스트 교체 전 Fade Out으로 안보이는 상태에서 시작)
                    yield return StartCoroutine(FadeTextAlpha(finalPageText, 0f, 1f, 1.0f));
                }

                // 3. 유지 (2초)
                yield return CoroutineData.GetWaitForSeconds(2.0f);

                // 4. 퇴장 (Fade Out 1초) - 다음 텍스트가 있을 때만
                if (i < texts.Length - 1)
                {
                    yield return StartCoroutine(FadeTextAlpha(finalPageText, 1f, 0f, 1.0f));
                }
            }
            
            // 4개의 텍스트가 모두 끝난 후의 처리가 필요하다면 여기에 추가 (예: 페이지 닫기)
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
        
        private void SetTextAlpha(Text txt, float alpha)
        {
            if (!txt) return;
            Color c = txt.color;
            txt.color = new Color(c.r, c.g, c.b, alpha);
        }
    }
}