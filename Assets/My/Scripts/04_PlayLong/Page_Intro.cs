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

        /// <summary>
        /// 외부 데이터를 받아 페이지 초기 설정 데이터를 세팅함.
        /// </summary>
        /// <param name="data">적용할 인트로 텍스트 데이터</param>
        protected override void SetupData(IntroPageData data)
        {
            if (data == null)
            {
                Debug.LogWarning("IntroPageData 누락됨.");
                return;
            }
            _data = data;
        }

        /// <summary>
        /// 페이지 진입 시 호출됨.
        /// UI 상태를 초기화하고 텍스트 연출 코루틴을 시작함.
        /// </summary>
        public override void OnEnter()
        {
            base.OnEnter();
            
            if (textIntro) 
            {
                textIntro.supportRichText = true;
            }
            else 
            {
                Debug.LogWarning("textIntro 컴포넌트 누락됨.");
            }
            
            if (_data == null)
            {
                Debug.LogError("데이터가 설정되지 않음.");
                CompleteStep();
                return;
            }

            // 이유: 페이지 노출 전 이전 투명도 상태를 강제 초기화함.
            SetAlpha(1f);
            StartCoroutine(IntroSequenceRoutine());
        }

        /// <summary>
        /// 3단계의 인트로 텍스트를 순차적으로 페이드 인/아웃 연출함.
        /// </summary>
        /// <returns>IEnumerator 루틴</returns>
        private IEnumerator IntroSequenceRoutine()
        {
            // 이유: 기획된 타이밍에 맞춰 첫 번째 안내 문구 노출 및 효과음 재생.
            if (_data.introText1 != null)
            {   
                if (SoundManager.Instance) SoundManager.Instance.PlaySFX("공통_13");
                if (UIManager.Instance) UIManager.Instance.SetText(textIntro.gameObject, _data.introText1);
            }
            SetTextAlpha(1f);
            yield return CoroutineData.GetWaitForSeconds(2.0f);
            yield return StartCoroutine(FadeTextAlpha(1f, 0f, fadeDuration));

            // 이유: 두 번째 문구 중 두 줄로 구성된 경우, 두 번째 줄만 지연하여 페이드인 연출함.
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
                    if (UIManager.Instance) UIManager.Instance.SetText(textIntro.gameObject, _data.introText2);
                    yield return StartCoroutine(FadeTextAlpha(0f, 1f, fadeDuration));
                }
            }
            yield return CoroutineData.GetWaitForSeconds(2.0f);
            yield return StartCoroutine(FadeTextAlpha(1f, 0f, fadeDuration));

            // 이유: 마지막 안내 문구 노출.
            if (_data.introText3 != null)
            {
                if (UIManager.Instance) UIManager.Instance.SetText(textIntro.gameObject, _data.introText3);
            }
            yield return StartCoroutine(FadeTextAlpha(0f, 1f, fadeDuration));
            yield return CoroutineData.GetWaitForSeconds(2.0f);

            // 이유: 인트로 연출 종료 후 페이지 전체 페이드아웃 및 다음 단계 진행.
            yield return StartCoroutine(FadePageAlpha(1f, 0f, 0.5f));
            
            CompleteStep();
        }

        /// <summary>
        /// GamePage의 CanvasGroup 알파값을 선형 보간하여 부드럽게 전환함.
        /// </summary>
        /// <param name="start">시작 알파값</param>
        /// <param name="end">종료 알파값</param>
        /// <param name="duration">소요 시간</param>
        /// <returns>IEnumerator 루틴</returns>
        private IEnumerator FadePageAlpha(float start, float end, float duration)
        {
            float elapsed = 0f;
            SetAlpha(start); 

            // # TODO: 빈번한 UI 업데이트 발생 시 Canvas 단위 배치 최적화 고려 필요.
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                // 예시 입력: start(1f), end(0f), elapsed(0.25f), duration(0.5f) -> 결과값 = 0.5f
                SetAlpha(Mathf.Lerp(start, end, elapsed / duration));
                yield return null;
            }
            SetAlpha(end);
        }

        /// <summary>
        /// 텍스트 두 번째 줄의 색상 태그를 동적으로 조작하여 지연 페이드인 연출을 만듦.
        /// </summary>
        /// <param name="line1">첫 번째 줄 문자열</param>
        /// <param name="line2">두 번째 줄 문자열</param>
        /// <param name="duration">소요 시간</param>
        /// <returns>IEnumerator 루틴</returns>
        private IEnumerator FadeInSecondLine(string line1, string line2, float duration)
        {
            float elapsed = 0f;
            Color baseColor = textIntro.color;
            string hexRGB = ColorUtility.ToHtmlStringRGB(baseColor); 

            // # TODO: 매 프레임 문자열 할당 가비지 생성을 피하기 위해 TextMeshPro 기능 전환 검토 필요.
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float alpha = Mathf.Clamp01(elapsed / duration);
                
                // 예시 입력: alpha(0.5f) * 255f -> 결과값 = 128 (16진수 '80')
                int alphaByte = Mathf.RoundToInt(alpha * 255f);
                string alphaHex = alphaByte.ToString("X2");

                textIntro.text = $"{line1}\n<color=#{hexRGB}{alphaHex}>{line2}</color>";
                yield return null;
            }
            textIntro.text = $"{line1}\n{line2}";
        }

        /// <summary>
        /// 텍스트 컴포넌트 전체 색상의 알파값을 선형 보간하여 투명도를 조절함.
        /// </summary>
        /// <param name="start">시작 알파값</param>
        /// <param name="end">종료 알파값</param>
        /// <param name="duration">소요 시간</param>
        /// <returns>IEnumerator 루틴</returns>
        private IEnumerator FadeTextAlpha(float start, float end, float duration)
        {
            float elapsed = 0f;
            SetTextAlpha(start);
            
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                // 예시 입력: start(0f), end(1f), elapsed(0.5f), duration(1f) -> 결과값 = 0.5f
                SetTextAlpha(Mathf.Lerp(start, end, elapsed / duration));
                yield return null;
            }
            SetTextAlpha(end);
        }

        /// <summary>
        /// 텍스트 컴포넌트의 알파값을 갱신함.
        /// </summary>
        /// <param name="alpha">적용할 알파값</param>
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