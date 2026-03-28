using System.Collections;
using My.Scripts.UI;
using UnityEngine;
using UnityEngine.UI;
using Wonjeong.Data;
using Wonjeong.UI;
using Wonjeong.Utils;

namespace My.Scripts._04_PlayLong
{
    /// <summary>
    /// PlayLong(500m 달리기) 씬의 전반적인 UI 갱신, 팝업 연출, 게이지 업데이트 등을 관리하는 클래스.
    /// </summary>
    public class PlayLongUIManager : MonoBehaviour
    {
        [Header("Player Name UI")]
        [SerializeField] private Text p1NameText;
        [SerializeField] private Text p2NameText;

        [Header("Player Color Balls")]
        [SerializeField] private Image ballImageA;
        [SerializeField] private Image ballImageB;

        [Header("Formatting Settings")]
        [SerializeField] private string[] formattedTextNames = new string[] { "PopupText_4" };

        [Header("Popup")]
        [SerializeField] private CanvasGroup popup;
        [SerializeField] private Text popupText;

        [Header("HUD")]
        [SerializeField] private Text centerText;
        [SerializeField] private Text timerText;
        [SerializeField] private Image timerIconImage;
        [SerializeField] private CanvasGroup padImagesCg;

        [Header("Red String Animation")]
        [SerializeField] private CanvasGroup redStringCanvasGroup;

        [Header("Side HUD")]
        [SerializeField] private PlayLongGaugeController p1LongGauge;
        [SerializeField] private PlayLongGaugeController p2LongGauge;
        [SerializeField] private CanvasGroup p1SideDistCg;
        [SerializeField] private CanvasGroup p2SideDistCg;

        [Header("Side HUD - Distance Markers")]
        [SerializeField] private Image[] p1DistMarkers;
        [SerializeField] private Image[] p2DistMarkers;

        [Header("Marker Assets")]
        [SerializeField] private Sprite[] originalMarkerSprites;
        [SerializeField] private Sprite heartFragmentSprite;

        private static readonly Vector2 OriginalMarkerSize = new Vector2(85f, 35f);
        private static readonly Vector2 HeartFragmentSize = new Vector2(144f, 138f);

        private string _originalFullText;
        private Coroutine _textBlinkCoroutine;
        
        private int _lastActiveMarkerCount;
        
        private Color _defaultTimerColor = Color.white;
        private bool _isTimerColorSaved;
        
        private Color _defaultTimerIconColor = Color.white;
        private bool _isTimerIconColorSaved;

        /// <summary>
        /// UI 컴포넌트들의 초기 상태를 설정함.
        /// </summary>
        /// <param name="maxDistance">목표 최대 거리</param>
        public void InitUI(float maxDistance)
        {
            if (popup)
            {
                popup.alpha = 0f;
                popup.gameObject.SetActive(false);
                popup.blocksRaycasts = true;
            }
            else
            {
                Debug.LogWarning("popup 캔버스 그룹 컴포넌트 누락됨.");
            }

            if (redStringCanvasGroup)
            {
                redStringCanvasGroup.alpha = 0f;
            }

            if (centerText) centerText.gameObject.SetActive(false);
            
            if (timerText) 
            {
                timerText.text = "60";
                if (_isTimerColorSaved)
                {
                    timerText.color = _defaultTimerColor;
                }
            }
            else
            {
                Debug.LogWarning("timerText 컴포넌트 누락됨.");
            }

            if (timerIconImage)
            {
                if (_isTimerIconColorSaved)
                {
                    timerIconImage.color = _defaultTimerIconColor;
                }
            }
            else
            {
                Debug.LogWarning("timerIconImage 컴포넌트 누락됨.");
            }

            if (p1LongGauge) p1LongGauge.ResetGauge();
            else Debug.LogWarning("p1LongGauge 누락됨.");

            if (p2LongGauge) p2LongGauge.ResetGauge();
            else Debug.LogWarning("p2LongGauge 누락됨.");

            if (p1SideDistCg) p1SideDistCg.alpha = 0f;
            if (p2SideDistCg) p2SideDistCg.alpha = 0f;
            if (padImagesCg) padImagesCg.alpha = 1f;

            _lastActiveMarkerCount = 0;
            ResetDistMarkers();
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
                else Debug.LogWarning("Player A 컬러 스프라이트 누락됨. 기본 이미지 유지.");
            }
            else
            {
                Debug.LogWarning("ballImageA 컴포넌트 누락됨.");
            }

            if (ballImageB)
            {
                if (spriteB) ballImageB.sprite = spriteB;
                else Debug.LogWarning("Player B 컬러 스프라이트 누락됨. 기본 이미지 유지.");
            }
            else
            {
                Debug.LogWarning("ballImageB 컴포넌트 누락됨.");
            }
        }

        /// <summary>
        /// 거리 진행도 마커(점) 이미지를 원본 형태로 초기화함.
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
                    if (origin)
                    {
                        if (p1DistMarkers[i]) UpdateMarkerAppearance(p1DistMarkers[i], origin, OriginalMarkerSize);
                        if (p2DistMarkers[i]) UpdateMarkerAppearance(p2DistMarkers[i], origin, OriginalMarkerSize);
                    }
                }
            }
        }

        /// <summary>
        /// 현재 이동 거리에 따라 마커 이미지를 달성(하트 조각) 상태로 순차적으로 갱신함.
        /// </summary>
        /// <param name="currentDist">현재 공동 도달 거리</param>
        public void UpdateDistanceMarkers(float currentDist)
        {
            if (p1DistMarkers == null || p2DistMarkers == null) return;

            // 예시 입력: currentDist(120f) / 100f -> 1.2 -> 결과값 = 1 (1개의 마커 활성화)
            int activeCount = Mathf.FloorToInt(currentDist / 100f);
            
            if (activeCount > _lastActiveMarkerCount)
            {
                // 이유: 새로운 마커 달성 시 효과음을 재생하여 유저에게 성취감을 제공함.
                if (SoundManager.Instance) SoundManager.Instance.PlaySFX("달리기_3");
                _lastActiveMarkerCount = activeCount;
            }
            
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
        /// 대상 이미지의 스프라이트와 크기를 변경함.
        /// </summary>
        /// <param name="targetImg">대상 이미지 컴포넌트</param>
        /// <param name="sprite">적용할 스프라이트</param>
        /// <param name="size">적용할 해상도 크기</param>
        private void UpdateMarkerAppearance(Image targetImg, Sprite sprite, Vector2 size)
        {
            if (!targetImg || !sprite) return;

            targetImg.sprite = sprite;
            targetImg.rectTransform.sizeDelta = size;
        }

        /// <summary>
        /// 화면 중앙 텍스트의 내용을 변경하고 활성화 상태를 조절함.
        /// </summary>
        /// <param name="message">출력할 문자열</param>
        /// <param name="isActive">활성화 여부</param>
        public void SetCenterText(string message, bool isActive)
        {
            if (centerText)
            {
                centerText.text = message;
                centerText.gameObject.SetActive(isActive);
            }
        }

        /// <summary>
        /// 화면 중앙 텍스트에 TextSetting 데이터를 적용하고 활성화함.
        /// </summary>
        /// <param name="setting">적용할 데이터</param>
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
        /// 팝업 텍스트 배열을 순차적으로 페이드 인/아웃하여 보여줌.
        /// </summary>
        /// <param name="textDatas">출력할 텍스트 설정 배열</param>
        /// <param name="durationPerText">텍스트당 유지 시간</param>
        /// <param name="hideAtEnd">종료 시 팝업 닫기 여부</param>
        /// <returns>IEnumerator 루틴</returns>
        public IEnumerator ShowPopupSequence(TextSetting[] textDatas, float durationPerText, bool hideAtEnd = true)
        {
            if (!popup || !popupText || textDatas == null || textDatas.Length == 0) yield break;

            Color c = popupText.color;
            c.a = 0f;
            popupText.color = c;

            popup.gameObject.SetActive(true);
            popupText.supportRichText = true;
            
            yield return StartCoroutine(UIUtils.FadeCanvasGroup(popup, popup.alpha, 1f, 0.5f));

            for (int i = 0; i < textDatas.Length; i++)
            {
                TextSetting textData = textDatas[i];
                if (textData == null) continue;

                bool applySpecialFormat = false;

                // 특정 이름(PopupText_4 등)의 텍스트인 경우, 기획 의도에 맞춰 줄바꿈 크기 차이를 두는 포맷팅을 적용함.
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

                yield return StartCoroutine(FadeTextAlpha(popupText, 0f, 1f, 0.25f));
                yield return CoroutineData.GetWaitForSeconds(durationPerText);

                if (i < textDatas.Length - 1 || hideAtEnd)
                {
                    yield return StartCoroutine(FadeTextAlpha(popupText, 1f, 0f, 0.25f));
                }
            }

            if (hideAtEnd)
            {
                yield return StartCoroutine(UIUtils.FadeCanvasGroup(popup, 1f, 0f, 0.5f));
                popup.gameObject.SetActive(false);
            }
        }

        /// <summary>
        /// 팝업 텍스트의 두 번째 줄이 깜빡이는 연출을 시작함.
        /// </summary>
        /// <param name="interval">깜빡임 주기</param>
        public void StartPopupTextBlinking(float interval = 0.5f)
        {
            if (!popupText) return;

            _originalFullText = popupText.text;

            if (_textBlinkCoroutine != null) StopCoroutine(_textBlinkCoroutine);
            _textBlinkCoroutine = StartCoroutine(BlinkSecondLineRoutine(interval));
        }

        /// <summary>
        /// 팝업 텍스트 깜빡임 연출을 중단하고 원본 텍스트로 복구함.
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
        /// 텍스트 색상 태그를 조작하여 두 번째 줄을 숨기거나 나타내는 방식으로 깜빡임을 구현함.
        /// </summary>
        /// <param name="interval">상태 전환 주기</param>
        /// <returns>IEnumerator 루틴</returns>
        private IEnumerator BlinkSecondLineRoutine(float interval)
        {
            if (!popupText) yield break;

            string[] lines = _originalFullText.Split('\n');
            if (lines.Length < 2) yield break;

            bool isVisible = true;
            
            // # TODO: 문자열 생성 루프 가비지를 줄이기 위해 CanvasGroup 분리 또는 TextMeshPro 기능 전환 고려 필요.
            while (true)
            {
                if (isVisible)
                {
                    popupText.text = _originalFullText;
                }
                else
                {
                    popupText.text = $"{lines[0]}\n<color=#00000000>{lines[1]}</color>";
                }

                yield return CoroutineData.GetWaitForSeconds(interval);

                isVisible = !isVisible;
            }
        }

        /// <summary>
        /// 붉은 실 연출 1단계로 팝업과 실 이미지를 페이드인함.
        /// </summary>
        /// <param name="textData">출력할 텍스트 설정 데이터</param>
        /// <returns>IEnumerator 루틴</returns>
        public IEnumerator ShowRedStringStep1(TextSetting textData)
        {
            if (textData == null || !popup || !popupText) yield break;

            popupText.supportRichText = true;
            string fullText = textData.text;
            string[] lines = fullText.Split('\n');

            if (lines.Length >= 2) popupText.text = $"{lines[0]}\n<color=#00000000>{lines[1]}</color>";
            else popupText.text = fullText;

            yield return StartCoroutine(FadeTextAlpha(popupText, 0f, 1f, 0.25f));

            if (redStringCanvasGroup) yield return StartCoroutine(UIUtils.FadeCanvasGroup(redStringCanvasGroup, 0f, 1f, 2.0f));
        }

        /// <summary>
        /// 텍스트 두 번째 줄의 알파값을 점진적으로 올려 서서히 나타나게 함.
        /// </summary>
        /// <param name="textData">출력할 전체 텍스트 데이터</param>
        /// <param name="duration">진행 소요 시간</param>
        /// <returns>IEnumerator 루틴</returns>
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
                string alphaHex = Mathf.RoundToInt(alpha * 255f).ToString("X2");
                popupText.text = $"{lines[0]}\n<size=40><color=#{hexColor}{alphaHex}>{lines[1]}</color></size>";
                yield return null;
            }

            popupText.text = $"{lines[0]}\n<size=40>{lines[1]}</size>";
        }

        /// <summary>
        /// 붉은 실 이미지를 지정된 횟수만큼 깜빡임 연출함.
        /// </summary>
        /// <param name="count">깜빡임 횟수</param>
        /// <param name="duration">총 소요 시간</param>
        /// <returns>IEnumerator 루틴</returns>
        public IEnumerator BlinkRedString(int count, float duration)
        {
            if (!redStringCanvasGroup) yield break;
            if (SoundManager.Instance) SoundManager.Instance.PlaySFX("달리기_6");

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
        /// 타이머 남은 시간을 갱신하고, 임박 시 붉은색 경고 표시를 함.
        /// </summary>
        /// <param name="time">남은 시간</param>
        public void UpdateTimer(float time)
        {
            if (timerText)
            {
                if (!_isTimerColorSaved)
                {
                    _defaultTimerColor = timerText.color;
                    _isTimerColorSaved = true;
                }

                timerText.text = Mathf.CeilToInt(Mathf.Max(0f, time)).ToString();

                // 이유: 남은 시간이 5초 이하일 때 유저에게 직관적인 경고 효과를 주기 위해 텍스트 색상을 빨간색으로 변경함.
                if (time <= 5f)
                {
                    timerText.color = Color.red;
                }
                else
                {
                    timerText.color = _defaultTimerColor;
                }
            }

            if (timerIconImage)
            {
                if (!_isTimerIconColorSaved)
                {
                    _defaultTimerIconColor = timerIconImage.color;
                    _isTimerIconColorSaved = true;
                }

                // 이유: 시계 아이콘도 텍스트와 함께 빨간색으로 변경하여 경고 시인성을 높임.
                if (time <= 5f)
                {
                    timerIconImage.color = Color.red;
                }
                else
                {
                    timerIconImage.color = _defaultTimerIconColor;
                }
            }
        }

        /// <summary>
        /// 단일 Text 컴포넌트의 폰트 알파값을 선형 보간하여 투명도를 조절함.
        /// </summary>
        /// <param name="txt">대상 텍스트 컴포넌트</param>
        /// <param name="start">시작 알파값</param>
        /// <param name="end">종료 알파값</param>
        /// <param name="duration">진행 소요 시간</param>
        /// <returns>IEnumerator 루틴</returns>
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
        /// 팝업 창을 부드럽게 페이드아웃하여 숨김.
        /// </summary>
        /// <param name="duration">소요 시간</param>
        public void HideQuestionPopup(float duration)
        {
            if (popup && popup.gameObject.activeInHierarchy)
            {
                popup.blocksRaycasts = false;
                StartCoroutine(HideQuestionPopupRoutine(duration));
            }
        }

        /// <summary>
        /// 팝업 창 페이드아웃 후 오브젝트를 비활성화함.
        /// </summary>
        /// <param name="duration">소요 시간</param>
        /// <returns>IEnumerator 루틴</returns>
        private IEnumerator HideQuestionPopupRoutine(float duration)
        {
            yield return StartCoroutine(UIUtils.FadeCanvasGroup(popup, popup.alpha, 0f, duration));

            popup.gameObject.SetActive(false);
            popup.blocksRaycasts = true;
        }

        /// <summary>
        /// 튜토리얼 페이즈 종료 후 본격적인 달리기 준비 UI(사이드 게이지)로 크로스페이드 전환함.
        /// </summary>
        /// <param name="duration">전환 소요 시간</param>
        /// <returns>IEnumerator 루틴</returns>
        public IEnumerator FadeTransitionTutorialReady(float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float progress = Mathf.Clamp01(elapsed / duration);

                if (p1SideDistCg) p1SideDistCg.alpha = progress;
                if (p2SideDistCg) p2SideDistCg.alpha = progress;

                if (padImagesCg) padImagesCg.alpha = 1f - progress;

                yield return null;
            }

            if (p1SideDistCg) p1SideDistCg.alpha = 1f;
            if (p2SideDistCg) p2SideDistCg.alpha = 1f;
            if (padImagesCg) padImagesCg.alpha = 0f;
        }

        /// <summary>
        /// 두 플레이어의 사이드 진행도 게이지 수치를 갱신함.
        /// </summary>
        /// <param name="current">현재 누적 수치</param>
        /// <param name="max">목표 최대 수치</param>
        public void UpdateLongCoopGauge(float current, float max)
        {
            if (p1LongGauge) p1LongGauge.UpdateGauge(current, max);
            if (p2LongGauge) p2LongGauge.UpdateGauge(current, max);
        }

        /// <summary>
        /// 게임 종료 시 중앙에 텍스트 설정 데이터를 적용하여 결과 팝업을 띄움.
        /// </summary>
        /// <param name="textData">결과 텍스트 설정 데이터</param>
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
        /// 게임 종료 시 중앙에 일반 문자열을 사용하여 결과 팝업을 띄움.
        /// </summary>
        /// <param name="message">출력할 문자열</param>
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