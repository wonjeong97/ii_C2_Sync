using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Wonjeong.Data;
using Wonjeong.UI;
using Wonjeong.Utils;
using My.Scripts.UI; 

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

        /// <summary>
        /// UI 컴포넌트들의 초기 상태를 설정함.
        /// </summary>
        /// <param name="maxDistance">게이지의 최대 목표 거리</param>
        public void InitUI(float maxDistance)
        {
            if (p1Gauge) p1Gauge.UpdateGauge(0, maxDistance);
            else Debug.LogWarning("p1Gauge 컴포넌트 누락됨.");

            if (p2Gauge) p2Gauge.UpdateGauge(0, maxDistance);
            else Debug.LogWarning("p2Gauge 컴포넌트 누락됨.");

            // 이유: 시작 시 성공 텍스트가 화면을 가리지 않도록 비활성화함.
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
        /// <param name="nameA">1P 이름</param>
        /// <param name="nameB">2P 이름</param>
        /// <param name="settingA">1P 텍스트 설정 데이터</param>
        /// <param name="settingB">2P 텍스트 설정 데이터</param>
        public void SetPlayerNames(string nameA, string nameB, TextSetting settingA, TextSetting settingB)
        {
            // 이유: 중복 로직 제거를 위해 공용 유틸리티 클래스의 메서드를 재사용함.
            UIUtils.ApplyPlayerNames(p1NameText, p2NameText, nameA, nameB, settingA, settingB);
        }

        /// <summary>
        /// 플레이어의 고유 색상에 맞춰 볼 스프라이트를 변경함.
        /// </summary>
        /// <param name="spriteA">1P 스프라이트</param>
        /// <param name="spriteB">2P 스프라이트</param>
        public void SetPlayerBalls(Sprite spriteA, Sprite spriteB)
        {
            if (ballImageA)
            {
                if (spriteA) ballImageA.sprite = spriteA;
                else Debug.LogWarning("Player A 컬러 스프라이트가 누락되어 기본 이미지를 유지함.");
            }
            else
            {
                Debug.LogWarning("ballImageA 컴포넌트 누락됨.");
            }

            if (ballImageB)
            {
                if (spriteB) ballImageB.sprite = spriteB;
                else Debug.LogWarning("Player B 컬러 스프라이트가 누락되어 기본 이미지를 유지함.");
            }
            else
            {
                Debug.LogWarning("ballImageB 컴포넌트 누락됨.");
            }
        }

        /// <summary>
        /// 개별 플레이어의 진행도 게이지를 업데이트함.
        /// </summary>
        /// <param name="playerIdx">플레이어 인덱스</param>
        /// <param name="current">현재 도달 거리</param>
        /// <param name="max">목표 거리</param>
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
        /// <param name="playerIdx">플레이어 인덱스</param>
        /// <param name="isRight">우측 방향 여부</param>
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
        /// <param name="playerIdx">플레이어 인덱스</param>
        /// <param name="isRight">우측 방향 여부</param>
        /// <param name="duration">페이드아웃 소요 시간</param>
        public void StopArrowFadeOut(int playerIdx, bool isRight, float duration)
        {
            UIArrowAnimator target;
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
        /// <param name="text">출력할 문자열</param>
        public void ShowPopupImmediately(string text)
        {   
            if (SoundManager.Instance) SoundManager.Instance.PlaySFX("공통_7");
            
            if (popupText) popupText.text = text;
            
            if (popup)
            {
                popup.alpha = 1;
                popup.blocksRaycasts = true;
            }
        }

        /// <summary>
        /// 팝업 노출 전 내용을 미리 세팅하고 투명 상태로 대기함.
        /// </summary>
        /// <param name="text">출력할 문자열</param>
        public void PreparePopup(string text)
        {
            if (popupText) popupText.text = text;
            
            if (popup)
            {
                popup.alpha = 0;
                popup.blocksRaycasts = true;
            }
        }

        /// <summary>
        /// 준비된 팝업을 부드럽게 페이드인함.
        /// </summary>
        /// <param name="duration">페이드 소요 시간</param>
        /// <returns>IEnumerator 루틴</returns>
        public IEnumerator FadeInPopup(float duration)
        {   
            if (!popup) yield break;

            if (!popup.gameObject.activeInHierarchy) popup.gameObject.SetActive(true);
            
            if (SoundManager.Instance) SoundManager.Instance.PlaySFX("공통_7");

            yield return StartCoroutine(FadeCanvasGroup(popup, 0f, 1f, duration));
        }

        /// <summary>
        /// 노출된 팝업을 부드럽게 페이드아웃함.
        /// </summary>
        /// <param name="duration">페이드 소요 시간</param>
        public void HidePopup(float duration)
        {
            if (!popup) return;
            
            StartCoroutine(FadeCanvasGroup(popup, popup.alpha, 0f, duration));
            popup.blocksRaycasts = false;
        }
        
        /// <summary>
        /// 기존 팝업 텍스트를 페이드아웃하고 내용을 변경한 뒤 다시 페이드인함.
        /// </summary>
        /// <param name="newText">변경할 문자열</param>
        /// <param name="fadeOutTime">아웃 소요 시간</param>
        /// <param name="fadeInTime">인 소요 시간</param>
        /// <returns>IEnumerator 루틴</returns>
        public IEnumerator FadeOutPopupTextAndChange(string newText, float fadeOutTime, float fadeInTime)
        {
            yield return StartCoroutine(FadeTextAlpha(popupText, 1f, 0f, fadeOutTime));
            
            if (popupText) popupText.text = newText;
            
            yield return StartCoroutine(FadeTextAlpha(popupText, 0f, 1f, fadeInTime));
        }

        /// <summary>
        /// 화면 중앙에 성공 메시지를 일정 시간 띄운 뒤 사라지게 함.
        /// </summary>
        /// <param name="message">출력할 메시지</param>
        /// <param name="duration">유지 시간</param>
        /// <returns>IEnumerator 루틴</returns>
        public IEnumerator ShowSuccessText(string message, float duration)
        {
            if (!centerText) yield break;

            centerText.text = message;
            centerText.gameObject.SetActive(true);
            
            if (SoundManager.Instance) SoundManager.Instance.PlaySFX("공통_20");
            
            yield return StartCoroutine(FadeTextAlpha(centerText, 0f, 1f, 0.25f));
            yield return CoroutineData.GetWaitForSeconds(duration);
            yield return StartCoroutine(FadeTextAlpha(centerText, 1f, 0f, 0.25f));

            centerText.gameObject.SetActive(false);
        }

        /// <summary>
        /// 튜토리얼 종료 전 최종 안내 문구들을 순차적으로 연출함.
        /// </summary>
        /// <param name="texts">출력할 텍스트 배열</param>
        /// <returns>IEnumerator 루틴</returns>
        public IEnumerator RunFinalPageSequence(TextSetting[] texts)
        {
            if (!finalPageCanvasGroup || !finalPageText)
            {
                Debug.LogWarning("RunFinalPageSequence 필수 컴포넌트 누락됨.");
                yield break;
            }

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

            yield return StartCoroutine(FadeCanvasGroup(finalPageCanvasGroup, 0f, 1f, 0.5f));

            for (int i = 0; i < texts.Length; i++)
            {
                TextSetting setting = texts[i];
                if (setting == null) continue;

                if (UIManager.Instance)
                {
                    UIManager.Instance.SetText(finalPageText.gameObject, setting);
                }
                else
                {
                    finalPageText.text = setting.text;
                }
                
                // 이유: 기획 의도에 맞춰 특정 텍스트 노출 시 사운드 효과를 삽입함.
                if (setting.name == "Text_Step1")
                {
                    if (SoundManager.Instance) SoundManager.Instance.PlaySFX("공통_13");
                }
                
                yield return StartCoroutine(FadeTextAlpha(finalPageText, 0f, 1f, 0.25f));
                yield return CoroutineData.GetWaitForSeconds(3.0f);
                yield return StartCoroutine(FadeTextAlpha(finalPageText, 1f, 0f, 0.25f));
            }
        }

        /// <summary>
        /// CanvasGroup의 알파값을 목표 수치까지 선형 보간함.
        /// </summary>
        /// <param name="cg">대상 CanvasGroup</param>
        /// <param name="start">시작 알파값</param>
        /// <param name="end">종료 알파값</param>
        /// <param name="duration">소요 시간</param>
        /// <returns>IEnumerator 루틴</returns>
        private IEnumerator FadeCanvasGroup(CanvasGroup cg, float start, float end, float duration)
        {
            if (!cg) yield break;
            float t = 0f;

            // # TODO: 빈번한 UI 업데이트 발생 시 Canvas 단위 배치 최적화 고려.
            while (t < duration)
            {
                t += Time.deltaTime;
                
                // 예시 입력: start(0f), end(1f), t(0.25f), duration(0.5f) -> 결과값 = 0.5f
                cg.alpha = Mathf.Lerp(start, end, t / duration);
                yield return null;
            }

            cg.alpha = end;
            
            // 이유: 완전 투명화 시 연산 비용을 줄이기 위해 게임 오브젝트를 비활성화함.
            if (end <= 0f) cg.gameObject.SetActive(false);
        }

        /// <summary>
        /// 텍스트 색상의 알파값을 목표 수치까지 선형 보간함.
        /// </summary>
        /// <param name="txt">대상 Text</param>
        /// <param name="start">시작 알파값</param>
        /// <param name="end">종료 알파값</param>
        /// <param name="duration">소요 시간</param>
        /// <returns>IEnumerator 루틴</returns>
        private IEnumerator FadeTextAlpha(Text txt, float start, float end, float duration)
        {
            if (!txt) yield break;
            float t = 0f;
            Color c = txt.color;

            // # TODO: Text 컴포넌트의 빈번한 color 변경은 버텍스 재생성을 유발하므로 텍스트 전용 CanvasGroup 사용 검토 필요.
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