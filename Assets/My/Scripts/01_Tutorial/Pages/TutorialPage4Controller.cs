using System;
using System.Collections;
using My.Scripts.Core;
using UnityEngine;
using UnityEngine.UI;
using Wonjeong.Data;
using Wonjeong.UI;
using Wonjeong.Utils;

namespace My.Scripts._01_Tutorial.Pages
{
    [Serializable]
    public class TutorialPage4Data
    {
        public TextSetting[] descriptionTexts;
    }

    public class TutorialPage4Controller : GamePage<TutorialPage4Data>
    {
        [Header("UI Components")]
        [SerializeField] private Text descriptionText; // 하나만 사용하여 계속 재활용

        private TutorialPage4Data _data;
        
        // 타이밍 상수
        private const float FADE_IN_TIME = 1.0f;
        private const float DISPLAY_TIME = 3.0f;
        private const float FADE_OUT_TIME = 1.0f;

        protected override void SetupData(TutorialPage4Data data)
        {
            _data = data;
            
            if (descriptionText != null)
            {
                // 시작 시 텍스트 숨기기 (알파 0)
                Color c = descriptionText.color;
                descriptionText.color = new Color(c.r, c.g, c.b, 0f);
                descriptionText.gameObject.SetActive(false);
            }
        }

        public override void OnEnter()
        {
            base.OnEnter();

            if (_data != null && _data.descriptionTexts != null && _data.descriptionTexts.Length > 0)
            {
                StartCoroutine(ScenarioRoutine());
            }
            else
            {
                Debug.LogWarning("[Page4] 텍스트 데이터가 없습니다.");
                CompleteStep();
            }
        }

        private IEnumerator ScenarioRoutine()
        {
            // 배열에 있는 모든 설정을 순서대로 실행
            for (int i = 0; i < _data.descriptionTexts.Length; i++)
            {
                var currentSetting = _data.descriptionTexts[i];

                // 1. 텍스트 설정 적용 (내용, 위치, 폰트, 사이즈 등 변경)
                if (descriptionText != null && currentSetting != null)
                {
                    // UIManager가 텍스트 컴포넌트의 속성을 데이터대로 변경해줌
                    UIManager.Instance.SetText(descriptionText.gameObject, currentSetting);
                    
                    // 활성화 및 투명 상태로 시작
                    descriptionText.gameObject.SetActive(true);
                    SetTextAlpha(0f);
                }

                // 2. 페이드 인 (1초)
                yield return StartCoroutine(FadeText(0f, 1f, FADE_IN_TIME));

                // 3. 대기 (3초)
                yield return CoroutineData.GetWaitForSeconds(DISPLAY_TIME);

                // 4. 페이드 아웃 (1초)
                yield return StartCoroutine(FadeText(1f, 0f, FADE_OUT_TIME));
            }

            // 모든 텍스트 시나리오 종료 -> 다음 단계
            CompleteStep();
        }

        // 알파값 즉시 설정 헬퍼
        private void SetTextAlpha(float alpha)
        {
            if (descriptionText == null) return;
            Color c = descriptionText.color;
            descriptionText.color = new Color(c.r, c.g, c.b, alpha);
        }

        // 페이드 코루틴
        private IEnumerator FadeText(float startAlpha, float endAlpha, float duration)
        {
            if (descriptionText == null) yield break;

            float elapsed = 0f;
            Color initialColor = descriptionText.color;

            // 시작값 보정
            descriptionText.color = new Color(initialColor.r, initialColor.g, initialColor.b, startAlpha);

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float alpha = Mathf.Lerp(startAlpha, endAlpha, elapsed / duration);
                descriptionText.color = new Color(initialColor.r, initialColor.g, initialColor.b, alpha);
                yield return null;
            }

            // 끝값 고정
            descriptionText.color = new Color(initialColor.r, initialColor.g, initialColor.b, endAlpha);
        }
    }
}