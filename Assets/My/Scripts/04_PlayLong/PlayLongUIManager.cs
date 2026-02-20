using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Wonjeong.Data;
using Wonjeong.UI;
using Wonjeong.Utils;

namespace My.Scripts._04_PlayLong
{
    /// <summary>
    /// PlayLong 씬의 팝업 메시지 연출, HUD(게이지, 타이머) 갱신, 붉은 실 애니메이션 및 텍스트 점멸을 관리하는 UI 매니저.
    /// </summary>
    public class PlayLongUIManager : MonoBehaviour
    {
        [Header("Popup")]
        [SerializeField] private CanvasGroup popup; 
        [SerializeField] private Text popupText;    

        [Header("HUD")]
        [SerializeField] private Text centerText;
        [SerializeField] private Text timerText;
        [SerializeField] private CanvasGroup padImagesCG;
        
        [Header("Red String Animation")]
        [SerializeField] private CanvasGroup redStringCanvasGroup;
        
        [Header("Side HUD")]
        [SerializeField] private PlayLongGaugeController p1LongGauge; 
        [SerializeField] private PlayLongGaugeController p2LongGauge;
        [SerializeField] private CanvasGroup p1SideDistCG; 
        [SerializeField] private CanvasGroup p2SideDistCG; 

        [Header("Side HUD - Distance Markers")]
        [Tooltip("순서대로 100M, 200M, 300M, 400M, 500M 이미지 컴포넌트들")]
        [SerializeField] private Image[] p1DistMarkers; 
        [SerializeField] private Image[] p2DistMarkers; 

        [Header("Marker Assets")]
        [Tooltip("100M, 200M, 300M, 400M, 500M 원본 숫자 스프라이트들을 순서대로 할당")]
        [SerializeField] private Sprite[] originalMarkerSprites; 
        [SerializeField] private Sprite heartFragmentSprite; 

        // 이미지 사이즈 상수 정의
        private readonly Vector2 OriginalMarkerSize = new Vector2(85f, 35f);
        private readonly Vector2 HeartFragmentSize = new Vector2(144f, 138f);
        
        private string _originalFullText;
        private Coroutine _textBlinkCoroutine;

        /// <summary>
        /// 씬 시작 시 UI 요소들의 초기 상태를 설정함.
        /// </summary>
        public void InitUI(float maxDistance)
        {
            if (popup)
            {
                popup.alpha = 0f;
                popup.gameObject.SetActive(false);
                popup.blocksRaycasts = true;
            }
            
            if (redStringCanvasGroup != null)
            {
                redStringCanvasGroup.alpha = 0f;
            }
            
            if (centerText) centerText.gameObject.SetActive(false);
            if (timerText) timerText.text = "";
            
            // 게이지 및 사이드 UI 초기화
            if (p1LongGauge) p1LongGauge.ResetGauge();
            if (p2LongGauge) p2LongGauge.ResetGauge();
            
            if (p1SideDistCG) p1SideDistCG.alpha = 0f;
            if (p2SideDistCG) p2SideDistCG.alpha = 0f;
            if (padImagesCG) padImagesCG.alpha = 1f;

            // 거리 마커 이미지 및 크기 초기화
            ResetDistMarkers();
        }

        /// <summary>
        /// 모든 거리 마커를 각 인덱스에 맞는 원본 숫자 이미지와 기본 크기(85x35)로 초기화함.
        /// </summary>
        private void ResetDistMarkers()
        {
            if (p1DistMarkers == null || p2DistMarkers == null || originalMarkerSprites == null) return;
            for (int i = 0; i < p1DistMarkers.Length; i++)
            {
                if (i >= p2DistMarkers.Length) break;
                // 인덱스에 매칭되는 원본 스프라이트가 있는지 확인
                Sprite origin = (i < originalMarkerSprites.Length) ? originalMarkerSprites[i] : null;
                
                if (origin != null)
                {
                    UpdateMarkerAppearance(p1DistMarkers[i], origin, OriginalMarkerSize);
                    UpdateMarkerAppearance(p2DistMarkers[i], origin, OriginalMarkerSize);
                }
            }
        }
        /// <summary>
        /// 현재 달성 거리에 따라 마커 이미지를 마음조각(144x138)으로 교체함.
        /// </summary>
        public void UpdateDistanceMarkers(float currentDist)
        {
            if (p1DistMarkers == null || p2DistMarkers == null) return;
            // 100M 단위로 달성 개수 계산 (예: 250M 달성 시 activeCount는 2)
            int activeCount = Mathf.FloorToInt(currentDist / 100f);
            int len = Mathf.Min(p1DistMarkers.Length, p2DistMarkers.Length);
            for (int i = 0; i < len; i++)
            {
                // 1. 달성한 거리(activeCount) 안에 포함되는 인덱스인지 확인
                if (i < activeCount)
                {
                    // 2. [중요] 현재 이미지가 이미 마음조각인지 확인하여 중복 업데이트 방지
                    // 만약 이미지가 바뀌지 않는다면 이 조건문을 제거하고 테스트해보세요.
                    if (p1DistMarkers[i].sprite != heartFragmentSprite)
                    {
                        UpdateMarkerAppearance(p1DistMarkers[i], heartFragmentSprite, HeartFragmentSize);
                        UpdateMarkerAppearance(p2DistMarkers[i], heartFragmentSprite, HeartFragmentSize);
                
                        // 디버그 로그를 추가하여 실제로 이 블록에 진입하는지 확인
                        // Debug.Log($"[MarkerUpdate] { (i+1)*100 }M 지점 마음조각으로 교체 완료");
                    }
                }
            }
        }

        /// <summary>
        /// 마커의 스프라이트와 RectTransform의 sizeDelta를 일괄 변경하는 헬퍼 메서드.
        /// </summary>
        private void UpdateMarkerAppearance(Image targetImg, Sprite sprite, Vector2 size)
        {
            if (!targetImg || !sprite) return;

            targetImg.sprite = sprite;
            targetImg.rectTransform.sizeDelta = size;
        }
        
        public void SetCenterText(string message, bool isActive)
        {
            if (centerText)
            {
                centerText.text = message;
                centerText.gameObject.SetActive(isActive);
            }
        }

        public void SetCenterText(TextSetting setting)
        {
            if (centerText && setting != null)
            {
                centerText.gameObject.SetActive(true);
                if (UIManager.Instance) 
                    UIManager.Instance.SetText(centerText.gameObject, setting);
                else 
                    centerText.text = setting.text;
            }
        }

        /// <summary>
        /// 텍스트를 순차적으로 표시하며, PopupText_4의 경우 두 번째 줄 크기를 작게 조절함.
        /// </summary>
        public IEnumerator ShowPopupSequence(TextSetting[] textDatas, float durationPerText, bool hideAtEnd = true)
        {
            if (!popup || !popupText) yield break;
            if (textDatas == null || textDatas.Length == 0) yield break;

            popup.gameObject.SetActive(true);
            popupText.supportRichText = true;

            yield return StartCoroutine(FadeCanvasGroup(popup, popup.alpha, 1f, 0.5f));

            for (int i = 0; i < textDatas.Length; i++)
            {
                var textData = textDatas[i];
                if (textData == null) continue;

                // 인덱스 4번 안내 문구에 대해 서식 적용
                if (textData.name == "PopupText_4")
                {
                    string[] lines = textData.text.Split('\n');
                    if (lines.Length >= 2)
                    {
                        popupText.text = $"{lines[0]}\n<size=40>{lines[1]}</size>";
                    }
                    else
                    {
                        popupText.text = textData.text;
                    }
                }
                else
                {
                    if (UIManager.Instance) UIManager.Instance.SetText(popupText.gameObject, textData);
                    else popupText.text = textData.text;
                }

                yield return StartCoroutine(FadeTextAlpha(popupText, 0f, 1f, 0.5f));
                yield return CoroutineData.GetWaitForSeconds(durationPerText);
        
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
        /// 팝업 텍스트의 두 번째 줄 부분만 투명 컬러 태그를 이용해 점멸시킴.
        /// </summary>
        public void StartPopupTextBlinking(float interval = 0.5f)
        {
            if (!popupText) return;
            _originalFullText = popupText.text; 
    
            if (_textBlinkCoroutine != null) StopCoroutine(_textBlinkCoroutine);
            _textBlinkCoroutine = StartCoroutine(BlinkSecondLineRoutine(interval));
        }

        /// <summary>
        /// 점멸을 중지하고 텍스트를 원래 상태로 복구함.
        /// </summary>
        public void StopPopupTextBlinking()
        {
            if (_textBlinkCoroutine != null)
            {
                StopCoroutine(_textBlinkCoroutine);
                _textBlinkCoroutine = null;
            }
            if (popupText && !string.IsNullOrEmpty(_originalFullText))
            {
                popupText.text = _originalFullText;
            }
        }

        private IEnumerator BlinkSecondLineRoutine(float interval)
        {
            if (!popupText) yield break;

            string[] lines = _originalFullText.Split('\n');
            if (lines.Length < 2) yield break;

            bool isVisible = true;
            while (true)
            {
                isVisible = !isVisible;
                if (isVisible)
                {
                    popupText.text = _originalFullText;
                }
                else
                {
                    // 두 번째 줄만 투명색(#00000000)으로 변경하여 숨김 효과 연출
                    popupText.text = $"{lines[0]}\n<color=#00000000>{lines[1]}</color>";
                }
                yield return CoroutineData.GetWaitForSeconds(interval);
            }
        }

        public IEnumerator ShowRedStringStep1(TextSetting textData)
        {
            if (textData == null || !popup || !popupText) yield break;
            popupText.supportRichText = true;
            string fullText = textData.text; 
            string[] lines = fullText.Split('\n');

            if (lines.Length >= 2) popupText.text = $"{lines[0]}\n<color=#00000000>{lines[1]}</color>";
            else popupText.text = fullText;

            yield return StartCoroutine(FadeTextAlpha(popupText, 0f, 1f, 0.5f));
            if (redStringCanvasGroup) yield return StartCoroutine(FadeCanvasGroup(redStringCanvasGroup, 0f, 1f, 2.0f));
        }

        public IEnumerator FadeInSecondLine(TextSetting textData, float duration)
        {
            if (textData == null || !popupText) yield break;
            string fullText = textData.text;
            string[] lines = fullText.Split('\n');
            if (lines.Length < 2) yield break;

            float elapsed = 0f;
            Color originColor = popupText.color;
            string hexColor = ColorUtility.ToHtmlStringRGB(originColor);

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float alpha = Mathf.Clamp01(elapsed / duration);
                string alphaHex = Mathf.RoundToInt(alpha * 255).ToString("X2");
                popupText.text = $"{lines[0]}\n<size=40><color=#{hexColor}{alphaHex}>{lines[1]}</color></size>";
                yield return null;
            }
            popupText.text = $"{lines[0]}\n<size=40>{lines[1]}</size>";
        }

        public IEnumerator BlinkRedString(int count, float duration)
        {
            if (!redStringCanvasGroup) yield break;
            float waitTime = duration / (count * 2);
            for (int i = 0; i < count; i++)
            {
                redStringCanvasGroup.alpha = 0f;
                yield return CoroutineData.GetWaitForSeconds(waitTime);
                redStringCanvasGroup.alpha = 1f;
                yield return CoroutineData.GetWaitForSeconds(waitTime);
            }
        }

        public void UpdateTimer(float time)
        {
            if (timerText) timerText.text = Mathf.CeilToInt(Mathf.Max(0, time)).ToString();
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
            while (t < duration)
            {
                t += Time.deltaTime;
                txt.color = new Color(c.r, c.g, c.b, Mathf.Lerp(start, end, t / duration));
                yield return null;
            }
            txt.color = new Color(c.r, c.g, c.b, end);
        }
        
        public void HideQuestionPopup(float duration)
        {
            if (popup && popup.gameObject.activeInHierarchy)
            {
                popup.blocksRaycasts = false; 
                StartCoroutine(HideQuestionPopupRoutine(duration));
            }
        }

        private IEnumerator HideQuestionPopupRoutine(float duration)
        {
            yield return StartCoroutine(FadeCanvasGroup(popup, popup.alpha, 0f, duration));
            popup.gameObject.SetActive(false);
            popup.blocksRaycasts = true; 
        }
        
        /// <summary>
        /// 사이드 UI는 페이드 인 시키고, 발판 이미지는 1초 동안 페이드 아웃 시킵니다.
        /// </summary>
        public IEnumerator FadeTransitionTutorialReady(float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float progress = Mathf.Clamp01(elapsed / duration);

                // 사이드 거리 UI는 페이드 인 (0 -> 1)
                if (p1SideDistCG) p1SideDistCG.alpha = progress;
                if (p2SideDistCG) p2SideDistCG.alpha = progress;

                // 발판 이미지는 페이드 아웃 (1 -> 0)
                if (padImagesCG) padImagesCG.alpha = 1f - progress; 
        
                yield return null;
            }
    
            // 최종 상태 확정
            if (p1SideDistCG) p1SideDistCG.alpha = 1f;
            if (p2SideDistCG) p2SideDistCG.alpha = 1f;
            if (padImagesCG) padImagesCG.alpha = 0f;
        }
        
        /// <summary>
        /// 협동 거리에 따른 Bar_Fill 게이지를 갱신함.
        /// </summary>
        public void UpdateLongCoopGauge(float current, float max)
        {
            if (p1LongGauge) p1LongGauge.UpdateGauge(current, max);
            if (p2LongGauge) p2LongGauge.UpdateGauge(current, max);
        }
        
        /// <summary>
        /// 게임 종료 시 중앙 팝업을 활성화하고 결과 메시지를 표시합니다.
        /// </summary>
        public void ShowCenterResultPopup(TextSetting textData)
        {
            if (!popup || !popupText) return;

            // 팝업 오브젝트 활성화
            popup.gameObject.SetActive(true);
            popup.alpha = 1f;
            popup.blocksRaycasts = true;

            // 텍스트 설정 적용
            if (UIManager.Instance && textData != null)
            {
                UIManager.Instance.SetText(popupText.gameObject, textData);
            }
            else if (textData != null)
            {
                popupText.text = textData.text;
            }
        }
        
        /// <summary>
        /// 게임 종료 시 중앙 팝업을 활성화하고 결과 메시지(String)를 표시합니다.
        /// </summary>
        public void ShowCenterResultPopup(string message)
        {
            if (!popup || !popupText) return;

            popup.gameObject.SetActive(true);
            popup.alpha = 1f;
            popup.blocksRaycasts = true;
    
            // 서식 없이 일반 텍스트로 할당
            popupText.text = message;
        }
    }
}