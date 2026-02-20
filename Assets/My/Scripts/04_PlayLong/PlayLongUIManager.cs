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
        [Header("Formatting Settings")]
        [Tooltip("두 번째 줄의 텍스트 크기를 작게(size=40) 변경할 TextSetting의 name 지정 (예: PopupText_4)")]
        [SerializeField] private string[] formattedTextNames = new string[] { "PopupText_4" };

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

        private readonly Vector2 OriginalMarkerSize = new Vector2(85f, 35f);
        private readonly Vector2 HeartFragmentSize = new Vector2(144f, 138f);
        
        private string _originalFullText;
        private Coroutine _textBlinkCoroutine;

        /// <summary>
        /// 씬 시작 시 UI 요소들의 초기 상태를 설정.
        /// 이전 상태 잔재에 의한 시각적 오류 방지.
        /// </summary>
        public void InitUI(float maxDistance)
        {
            if (popup != null)
            {
                popup.alpha = 0f;
                popup.gameObject.SetActive(false);
                popup.blocksRaycasts = true;
            }
            
            if (redStringCanvasGroup != null)
            {
                redStringCanvasGroup.alpha = 0f;
            }
            
            if (centerText != null) centerText.gameObject.SetActive(false);
            if (timerText != null) timerText.text = "";
            
            if (p1LongGauge != null) p1LongGauge.ResetGauge();
            if (p2LongGauge != null) p2LongGauge.ResetGauge();
            
            if (p1SideDistCG != null) p1SideDistCG.alpha = 0f;
            if (p2SideDistCG != null) p2SideDistCG.alpha = 0f;
            if (padImagesCG != null) padImagesCG.alpha = 1f;

            ResetDistMarkers();
        }

        /// <summary>
        /// 거리 마커를 초기 원본 숫자 이미지로 복구.
        /// 재시작 시 이전 달성 기록 초기화.
        /// </summary>
        private void ResetDistMarkers()
        {
            if (p1DistMarkers == null || p2DistMarkers == null) return;

            int originLen = originalMarkerSprites != null ? originalMarkerSprites.Length : 0;
            int maxLen = Mathf.Min(p1DistMarkers.Length, p2DistMarkers.Length, originLen);

            for (int i = 0; i < maxLen; i++)
            {
                if (originalMarkerSprites != null)
                {
                    Sprite origin = originalMarkerSprites[i];
                    if (origin != null)
                    {
                        if (p1DistMarkers[i] != null) UpdateMarkerAppearance(p1DistMarkers[i], origin, OriginalMarkerSize);
                        if (p2DistMarkers[i] != null) UpdateMarkerAppearance(p2DistMarkers[i], origin, OriginalMarkerSize);
                    }
                    else
                    {
                        Debug.LogWarning($"[PlayLongUIManager] originalMarkerSprites[{i}]가 null입니다.");
                    }
                }
            }
        }
        
        /// <summary>
        /// 현재 달성 거리에 맞춰 마커 이미지를 하트 조각으로 교체.
        /// 진행도에 따른 시각적 피드백 제공.
        /// </summary>
        public void UpdateDistanceMarkers(float currentDist)
        {
            if (p1DistMarkers == null || p2DistMarkers == null) return;
            
            // 예: 250M 달성 시 2개 교체
            int activeCount = Mathf.FloorToInt(currentDist / 100f);
            int len = Mathf.Min(p1DistMarkers.Length, p2DistMarkers.Length);
            
            for (int i = 0; i < len; i++)
            {
                if (i < activeCount)
                {
                    if (p1DistMarkers[i] && p1DistMarkers[i].sprite != heartFragmentSprite)
                        UpdateMarkerAppearance(p1DistMarkers[i], heartFragmentSprite, HeartFragmentSize);
                    
                    if (p2DistMarkers[i] && p2DistMarkers[i].sprite != heartFragmentSprite)
                        UpdateMarkerAppearance(p2DistMarkers[i], heartFragmentSprite, HeartFragmentSize);
                }
            }
        }

        /// <summary>
        /// 마커의 스프라이트와 크기를 동시에 변경.
        /// 이미지 비율 왜곡 방지.
        /// </summary>
        private void UpdateMarkerAppearance(Image targetImg, Sprite sprite, Vector2 size)
        {
            if (!targetImg || !sprite) return;

            targetImg.sprite = sprite;
            targetImg.rectTransform.sizeDelta = size;
        }
        
        /// <summary>
        /// 중앙 텍스트 활성화/비활성화 및 내용 설정.
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
        /// 중앙 텍스트 활성화/비활성화 및 내용 설정 (TextSetting 활용).
        /// </summary>
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
        /// 배열된 텍스트들을 순차적으로 표시.
        /// 외부 모델 구조(TextSetting) 변경 없이 특정 텍스트 이름 기반으로 서식을 동적 적용.
        /// </summary>
        public IEnumerator ShowPopupSequence(TextSetting[] textDatas, float durationPerText, bool hideAtEnd = true)
        {
            if (!popup || !popupText || textDatas == null || textDatas.Length == 0) yield break;

            popup.gameObject.SetActive(true);
            popupText.supportRichText = true;

            yield return StartCoroutine(FadeCanvasGroup(popup, popup.alpha, 1f, 0.5f));

            for (int i = 0; i < textDatas.Length; i++)
            {
                TextSetting textData = textDatas[i];
                if (textData == null) continue;

                bool applySpecialFormat = false;
                
                // 지정된 formattedTextNames 배열에 현재 텍스트 이름이 포함되어 있는지 확인
                if (formattedTextNames != null && !string.IsNullOrEmpty(textData.name))
                {
                    foreach (string targetName in formattedTextNames)
                    {
                        if (textData.name == targetName)
                        {
                            applySpecialFormat = true;
                            break;
                        }
                    }
                }

                if (applySpecialFormat)
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
        /// 팝업 텍스트의 두 번째 줄 부분만 점멸.
        /// 특정 지시 사항 강조 연출.
        /// </summary>
        public void StartPopupTextBlinking(float interval = 0.5f)
        {
            if (!popupText) return;
            _originalFullText = popupText.text; 
    
            if (_textBlinkCoroutine != null) StopCoroutine(_textBlinkCoroutine);
            _textBlinkCoroutine = StartCoroutine(BlinkSecondLineRoutine(interval));
        }

        /// <summary>
        /// 점멸을 중지하고 텍스트를 원래 상태로 복구.
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

        /// <summary>
        /// 투명 색상 태그를 활용해 특정 줄만 보이지 않게 점멸 처리.
        /// </summary>
        private IEnumerator BlinkSecondLineRoutine(float interval)
        {
            if (!popupText) yield break;

            string[] lines = _originalFullText.Split('\n');
            if (lines.Length < 2) yield break;

            // 진입 시 바로 글자가 사라지는 깜빡임 어색함을 방지하기 위해 false로 시작
            bool isVisible = false; 
            while (true)
            {
                isVisible = !isVisible;
                if (isVisible)
                {
                    popupText.text = _originalFullText;
                }
                else
                {
                    popupText.text = $"{lines[0]}\n<color=#00000000>{lines[1]}</color>";
                }
                yield return CoroutineData.GetWaitForSeconds(interval);
            }
        }

        /// <summary>
        /// 붉은 실 연출 1단계 표시.
        /// </summary>
        public IEnumerator ShowRedStringStep1(TextSetting textData)
        {
            if (textData == null || popup == null || popupText == null) yield break;
            
            popupText.supportRichText = true;
            string fullText = textData.text; 
            string[] lines = fullText.Split('\n');

            if (lines.Length >= 2) popupText.text = $"{lines[0]}\n<color=#00000000>{lines[1]}</color>";
            else popupText.text = fullText;

            yield return StartCoroutine(FadeTextAlpha(popupText, 0f, 1f, 0.5f));
            if (redStringCanvasGroup) yield return StartCoroutine(FadeCanvasGroup(redStringCanvasGroup, 0f, 1f, 2.0f));
        }

        /// <summary>
        /// 텍스트 두 번째 줄만 페이드 인.
        /// </summary>
        public IEnumerator FadeInSecondLine(TextSetting textData, float duration)
        {
            if (textData == null || popupText == null) yield break;
            
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
                string alphaHex = Mathf.RoundToInt(alpha * 255f).ToString("X2"); // 2자리 16진수 포맷
                popupText.text = $"{lines[0]}\n<size=40><color=#{hexColor}{alphaHex}>{lines[1]}</color></size>";
                yield return null;
            }
            popupText.text = $"{lines[0]}\n<size=40>{lines[1]}</size>";
        }

        /// <summary>
        /// 붉은 실 CanvasGroup 깜빡임 연출.
        /// </summary>
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

        /// <summary>
        /// 남은 시간 갱신.
        /// </summary>
        public void UpdateTimer(float time)
        {
            if (timerText) timerText.text = Mathf.CeilToInt(Mathf.Max(0f, time)).ToString();
        }

        /// <summary>
        /// CanvasGroup 부드러운 전환 처리.
        /// </summary>
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

        /// <summary>
        /// 텍스트 컴포넌트 부드러운 전환 처리.
        /// </summary>
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
        
        /// <summary>
        /// 질문 팝업 숨김 연출 코루틴 실행.
        /// </summary>
        public void HideQuestionPopup(float duration)
        {
            if (popup && popup.gameObject.activeInHierarchy)
            {
                popup.blocksRaycasts = false; 
                StartCoroutine(HideQuestionPopupRoutine(duration));
            }
        }

        /// <summary>
        /// 질문 팝업 페이드 아웃 후 비활성화.
        /// </summary>
        private IEnumerator HideQuestionPopupRoutine(float duration)
        {
            yield return StartCoroutine(FadeCanvasGroup(popup, popup.alpha, 0f, duration));
            popup.gameObject.SetActive(false);
            popup.blocksRaycasts = true; 
        }
        
        /// <summary>
        /// 사이드 UI 표시 및 발판 이미지 숨김 동시 연출.
        /// 게임 시작 준비 단계 전환용.
        /// </summary>
        public IEnumerator FadeTransitionTutorialReady(float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float progress = Mathf.Clamp01(elapsed / duration);

                if (p1SideDistCG) p1SideDistCG.alpha = progress;
                if (p2SideDistCG) p2SideDistCG.alpha = progress;

                if (padImagesCG) padImagesCG.alpha = 1f - progress; 
        
                yield return null;
            }
    
            if (p1SideDistCG) p1SideDistCG.alpha = 1f;
            if (p2SideDistCG) p2SideDistCG.alpha = 1f;
            if (padImagesCG) padImagesCG.alpha = 0f;
        }
        
        /// <summary>
        /// 롱 모드 협동 게이지 업데이트.
        /// </summary>
        public void UpdateLongCoopGauge(float current, float max)
        {
            if (p1LongGauge) p1LongGauge.UpdateGauge(current, max);
            if (p2LongGauge) p2LongGauge.UpdateGauge(current, max);
        }
        
        /// <summary>
        /// 완료/시간 종료 시 팝업 텍스트 세팅(TextSetting 파라미터).
        /// </summary>
        public void ShowCenterResultPopup(TextSetting textData)
        {
            if (!popup || !popupText || textData == null) return;

            popup.gameObject.SetActive(true);
            popup.alpha = 1f;
            popup.blocksRaycasts = true;

            Color c = popupText.color;
            c.a = 1f;
            popupText.color = c;

            if (UIManager.Instance)
                UIManager.Instance.SetText(popupText.gameObject, textData);
            else
                popupText.text = textData.text;
        }
        
        /// <summary>
        /// 완료/시간 종료 시 팝업 텍스트 세팅(String 파라미터).
        /// </summary>
        public void ShowCenterResultPopup(string message)
        {
            if (!popup || !popupText) return;

            popup.gameObject.SetActive(true);
            popup.alpha = 1f;
            popup.blocksRaycasts = true;
    
            Color c = popupText.color;
            c.a = 1f;
            popupText.color = c;

            popupText.text = message;
        }
    }
}