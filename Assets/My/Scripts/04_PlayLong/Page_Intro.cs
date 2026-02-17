using System;
using System.Collections;
using My.Scripts.Core;
using UnityEngine;
using UnityEngine.UI;
using Wonjeong.Data;
using Wonjeong.UI;
using Wonjeong.Utils;

namespace My.Scripts._04_PlayLong
{   
    [Serializable]
    public class IntroPageData
    {
        public TextSetting introText1;
        public TextSetting introText2;
        public TextSetting introText3;
    }
    
    public class Page_Intro : GamePage<IntroPageData>
    {
        [Header("UI References")]
        [SerializeField] private Text textIntro;

        private IntroPageData _data;
        private readonly float fadeDuration = 1f;

        protected override void SetupData(IntroPageData data)
        {
            _data = data;
        }

        public override void OnEnter()
        {
            base.OnEnter();
            
            if (textIntro) textIntro.supportRichText = true;
            
            if (_data == null)
            {
                Debug.LogError("[Page_Intro] 데이터가 설정되지 않았습니다.");
                CompleteStep();
                return;
            }

            // 페이지 시작 시 CanvasGroup 알파를 1로 확실하게 설정
            SetAlpha(1f);
            StartCoroutine(IntroSequenceRoutine());
        }

        private IEnumerator IntroSequenceRoutine()
        {
            // --- 1. 첫 번째 텍스트 연출 ---
            if (_data.introText1 != null)
            {
                UIManager.Instance.SetText(textIntro.gameObject, _data.introText1);
            }
            SetTextAlpha(1f);
            yield return CoroutineData.GetWaitForSeconds(2.0f);
            yield return StartCoroutine(FadeTextAlpha(1f, 0f, fadeDuration));

            // --- 2. 두 번째 텍스트 연출 ---
            if (_data.introText2 != null)
            {
                string fullText = _data.introText2.text;
                string[] lines = fullText.Split('\n');

                if (lines.Length >= 2)
                {
                    textIntro.text = $"{lines[0]}\n<color=#00000000>{lines[1]}</color>";
                    yield return StartCoroutine(FadeTextAlpha(0f, 1f, fadeDuration));
                    yield return CoroutineData.GetWaitForSeconds(1.0f);
                    yield return StartCoroutine(FadeInSecondLine(lines[0], lines[1], fadeDuration));
                }
                else
                {
                    UIManager.Instance.SetText(textIntro.gameObject, _data.introText2);
                    yield return StartCoroutine(FadeTextAlpha(0f, 1f, fadeDuration));
                }
            }
            yield return CoroutineData.GetWaitForSeconds(2.0f);
            yield return StartCoroutine(FadeTextAlpha(1f, 0f, fadeDuration));

            // --- 3. 세 번째 텍스트 연출 ---
            if (_data.introText3 != null)
            {
                UIManager.Instance.SetText(textIntro.gameObject, _data.introText3);
            }
            yield return StartCoroutine(FadeTextAlpha(0f, 1f, fadeDuration));
            yield return CoroutineData.GetWaitForSeconds(2.0f);

            // --- 4. 페이지 종료 ---
            yield return StartCoroutine(FadePageAlpha(1f, 0f, 0.5f));
            
            CompleteStep();
        }

        /// <summary>
        /// GamePage의 CanvasGroup Alpha를 조절하는 코루틴
        /// </summary>
        private IEnumerator FadePageAlpha(float start, float end, float duration)
        {
            float elapsed = 0f;
            SetAlpha(start); // GamePage 부모 메서드 호출

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                // 부모 클래스(GamePage)의 SetAlpha를 사용하여 CanvasGroup 제어
                SetAlpha(Mathf.Lerp(start, end, elapsed / duration));
                yield return null;
            }
            SetAlpha(end);
        }

        private IEnumerator FadeInSecondLine(string line1, string line2, float duration)
        {
            float elapsed = 0f;
            Color baseColor = textIntro.color;
            string hexRGB = ColorUtility.ToHtmlStringRGB(baseColor); 

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float alpha = Mathf.Clamp01(elapsed / duration);
                int alphaByte = Mathf.RoundToInt(alpha * 255f);
                string alphaHex = alphaByte.ToString("X2");

                textIntro.text = $"{line1}\n<color=#{hexRGB}{alphaHex}>{line2}</color>";
                yield return null;
            }
            textIntro.text = $"{line1}\n{line2}";
        }

        private IEnumerator FadeTextAlpha(float start, float end, float duration)
        {
            float elapsed = 0f;
            SetTextAlpha(start);
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                SetTextAlpha(Mathf.Lerp(start, end, elapsed / duration));
                yield return null;
            }
            SetTextAlpha(end);
        }

        private void SetTextAlpha(float alpha)
        {
            if (textIntro)
            {
                Color c = textIntro.color;
                c.a = alpha;
                textIntro.color = c;
            }
        }
    }
}