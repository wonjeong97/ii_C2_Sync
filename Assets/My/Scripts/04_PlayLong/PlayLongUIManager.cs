using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Wonjeong.Data;
using Wonjeong.UI;
using Wonjeong.Utils;

namespace My.Scripts._04_PlayLong
{
    /// <summary>
    /// PlayLong 씬의 팝업 메시지 연출, HUD(게이지, 타이머) 갱신, 붉은 실 애니메이션을 관리하는 UI 매니저.
    /// </summary>
    public class PlayLongUIManager : MonoBehaviour
    {
        [Header("Popup")]
        [SerializeField] private CanvasGroup popup; 
        [SerializeField] private Text popupText;    

        [Header("HUD")]
        [SerializeField] private Text centerText;
        [SerializeField] private Text timerText;
        [SerializeField] private GaugeController p1Gauge; 
        [SerializeField] private GaugeController p2Gauge; 
        
        [Header("Red String Animation")]
        [SerializeField] private CanvasGroup redStringCanvasGroup;

        /// <summary>
        /// 씬 시작 시 UI 요소들의 초기 투명도와 활성화 상태를 설정.
        /// </summary>
        /// <param name="maxDistance">게이지 최대치 설정을 위한 목표 거리.</param>
        public void InitUI(float maxDistance)
        {
            if (popup)
            {
                popup.alpha = 0f;
                popup.gameObject.SetActive(false);
            }
            
            if (redStringCanvasGroup != null)
            {
                redStringCanvasGroup.alpha = 0f;
            }

            if (p1Gauge) p1Gauge.UpdateGauge(0, maxDistance);
            if (p2Gauge) p2Gauge.UpdateGauge(0, maxDistance);
            
            if (centerText) centerText.gameObject.SetActive(false);
            if (timerText) timerText.text = "";
        }
        
        /// <summary>
        /// 중앙 텍스트에 카운트다운 숫자나 "시작" 문구를 표시합니다.
        /// </summary>
        public void SetCenterText(string message, bool isActive)
        {
            if (centerText)
            {
                centerText.text = message;
                centerText.gameObject.SetActive(isActive);
            }
        }

        /// <summary>
        /// JSON의 TextSetting 데이터를 중앙 텍스트에 적용합니다.
        /// </summary>
        public void SetCenterText(TextSetting setting)
        {
            if (centerText && setting != null)
            {
                centerText.gameObject.SetActive(true);
                if (UIManager.Instance != null) 
                    UIManager.Instance.SetText(centerText.gameObject, setting);
                else 
                    centerText.text = setting.text;
            }
        }

        /// <summary>
        /// 여러 텍스트를 순차적으로 표시하며, 텍스트 전환 시 페이드 인/아웃 효과를 적용.
        /// </summary>
        /// <param name="textDatas">표시할 텍스트 설정 배열.</param>
        /// <param name="durationPerText">텍스트 하나가 유지되는 시간.</param>
        /// <param name="hideAtEnd">시퀀스 종료 후 팝업을 완전히 닫을지 여부.</param>
        public IEnumerator ShowPopupSequence(TextSetting[] textDatas, float durationPerText, bool hideAtEnd = true)
        {
            if (!popup || !popupText) yield break;
            if (textDatas == null || textDatas.Length == 0) yield break;

            popup.gameObject.SetActive(true);
            popupText.color = new Color(popupText.color.r, popupText.color.g, popupText.color.b, 0f);
    
            // 팝업 창이 부드럽게 나타나도록 전체 알파 제어
            yield return StartCoroutine(FadeCanvasGroup(popup, popup.alpha, 1f, 0.5f));

            for (int i = 0; i < textDatas.Length; i++)
            {
                var textData = textDatas[i];
                if (textData == null) continue;

                if (UIManager.Instance != null) 
                    UIManager.Instance.SetText(popupText.gameObject, textData);
                else 
                    popupText.text = textData.text;

                // 새 텍스트가 서서히 등장
                yield return StartCoroutine(FadeTextAlpha(popupText, 0f, 1f, 0.5f));
                yield return CoroutineData.GetWaitForSeconds(durationPerText);
        
                // 마지막 텍스트가 아니거나, 다음 연출을 위해 팝업을 유지해야 하는 경우가 아닐 때만 퇴장 연출
                if (i < textDatas.Length - 1 || hideAtEnd)
                {
                    yield return StartCoroutine(FadeTextAlpha(popupText, 1f, 0f, 0.5f));
                }
            }

            if (hideAtEnd)
            {
                yield return StartCoroutine(FadeCanvasGroup(popup, 1f, 0f, 0.5f));
                popup.gameObject.SetActive(false);
            }
        }

        /// <summary>
        /// 튜토리얼 Step 2 진입 시 첫 번째 줄 텍스트와 붉은 실 이미지를 페이드 인 시킴.
        /// </summary>
        /// <param name="textData">표시할 전체 텍스트 설정.</param>
        public IEnumerator ShowRedStringStep1(TextSetting textData)
        {
            if (popup == null || popupText == null) yield break;

            popupText.supportRichText = true;
            string fullText = textData.text; 
            string[] lines = fullText.Split('\n');

            // Rich Text 태그를 사용하여 첫 줄은 그대로, 두 번째 줄은 투명하게 설정하여 시각적 분리 유도
            if (lines.Length >= 2)
            {
                popupText.text = $"{lines[0]}\n<color=#00000000>{lines[1]}</color>";
            }
            else
            {
                popupText.text = fullText;
            }
    
            // 이전 텍스트와의 시각적 간섭을 막기 위해 투명도 초기화 후 페이드 인
            Color c = popupText.color;
            popupText.color = new Color(c.r, c.g, c.b, 0f);

            yield return StartCoroutine(FadeTextAlpha(popupText, 0f, 1f, 0.5f));

            if (redStringCanvasGroup != null)
            {
                // 두 사람 사이의 연결을 강조하기 위해 붉은 실 이미지를 2초간 서서히 노출
                yield return StartCoroutine(FadeCanvasGroup(redStringCanvasGroup, 0f, 1f, 2.0f));
            }
        }

        /// <summary>
        /// 이미 표시 중인 텍스트의 두 번째 줄(미션 안내)만 서서히 나타나게 함.
        /// </summary>
        /// <param name="textData">기준이 되는 텍스트 설정.</param>
        /// <param name="duration">페이드 인 소요 시간.</param>
        public IEnumerator FadeInSecondLine(TextSetting textData, float duration)
        {
            if (popupText == null) yield break;

            string fullText = textData.text;
            string[] lines = fullText.Split('\n');
            if (lines.Length < 2) yield break;

            int reducedSize = 40; // 가독성을 위해 미션 안내 문구의 크기를 조절
            float elapsed = 0f;
            Color originColor = popupText.color;
            string hexColor = ColorUtility.ToHtmlStringRGB(originColor);

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float alpha = Mathf.Clamp01(elapsed / duration);
                int alphaByte = Mathf.RoundToInt(alpha * 255);
                string alphaHex = alphaByte.ToString("X2");

                // 첫 줄은 유지한 채로 두 번째 줄만 실시간으로 계산된 알파값을 Rich Text로 적용
                popupText.text = $"{lines[0]}\n<size={reducedSize}><color=#{hexColor}{alphaHex}>{lines[1]}</color></size>";
                yield return null;
            }

            popupText.text = $"{lines[0]}\n<size={reducedSize}>{lines[1]}</size>";
        }

        /// <summary>
        /// 붉은 실 이미지를 지정된 횟수만큼 점멸시켜 사용자 주의를 환기함.
        /// </summary>
        public IEnumerator BlinkRedString(int count, float duration)
        {
            if (redStringCanvasGroup == null) yield break;

            float waitTime = duration / (count * 2);
            for (int i = 0; i < count; i++)
            {
                redStringCanvasGroup.alpha = 0f;
                yield return CoroutineData.GetWaitForSeconds(waitTime);
                redStringCanvasGroup.alpha = 1f;
                yield return CoroutineData.GetWaitForSeconds(waitTime);
            }
        }

        /// <summary>
        /// 플레이어별 협동 거리 게이지를 업데이트.
        /// </summary>
        public void UpdateGauge(int playerIdx, float current, float max)
        {
            if (playerIdx == 0 && p1Gauge) p1Gauge.UpdateGauge(current, max);
            else if (playerIdx == 1 && p2Gauge) p2Gauge.UpdateGauge(current, max);
        }

        /// <summary>
        /// HUD에 현재 남은 시간을 정수 형태의 문자열로 표시.
        /// </summary>
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
        
        /// <summary>
        /// 진행 중인 미션 팝업을 페이드 아웃시키고 레이캐스트를 차단하여 조작 간섭을 방지.
        /// </summary>
        public void HideQuestionPopup(int playerIdx, float duration)
        {
            if (popup != null && popup.gameObject.activeInHierarchy)
            {
                popup.blocksRaycasts = false; 
                StartCoroutine(FadeCanvasGroup(popup, popup.alpha, 0f, duration));
            }
        }
    }
}