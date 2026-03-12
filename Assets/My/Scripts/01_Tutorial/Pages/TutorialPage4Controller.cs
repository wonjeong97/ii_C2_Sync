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
    /// <summary>
    /// 튜토리얼 4페이지의 데이터 구조.
    /// JSON의 "page4" 섹션과 매핑되며, 순차적으로 보여줄 여러 개의 텍스트 설정을 배열로 담고 있음.
    /// </summary>
    [Serializable]
    public class TutorialPage4Data
    {
        public TextSetting[] descriptionTexts;
    }

    /// <summary>
    /// 튜토리얼의 네 번째 페이지를 제어하는 컨트롤러.
    /// 여러 텍스트를 순서대로 페이드 인/아웃하며 시네마틱하게 연출하는 역할을 담당함.
    /// </summary>
    public class TutorialPage4Controller : GamePage<TutorialPage4Data>
    {
        [Header("UI Components")]
        [SerializeField] private Text descriptionText; 

        private TutorialPage4Data _data;
        
        private const float FadeInTime = 0.5f;
        private const float DisplayTime = 3f;
        private const float FadeOutTime = 0.5f;

        /// <summary> 외부 데이터를 받아 컴포넌트를 초기화함. </summary>
        protected override void SetupData(TutorialPage4Data data)
        {
            _data = data;
            
            if (descriptionText)
            {
                Color c = descriptionText.color;
                descriptionText.color = new Color(c.r, c.g, c.b, 0f);
                descriptionText.gameObject.SetActive(false);
            }
        }

        /// <summary> 페이지 진입 시 연출 시퀀스를 시작함. </summary>
        public override void OnEnter()
        {
            base.OnEnter();

            if (_data != null && _data.descriptionTexts != null && _data.descriptionTexts.Length > 0)
            {
                StartCoroutine(ScenarioRoutine());
            }
            else
            {
                CompleteStep();
            }
        }

        /// <summary> 정의된 텍스트 배열을 순회하며 페이드 연출을 수행하는 메인 코루틴. </summary>
        private IEnumerator ScenarioRoutine()
        {
            for (int i = 0; i < _data.descriptionTexts.Length; i++)
            {
                TextSetting currentSetting = _data.descriptionTexts[i];
                if (!descriptionText || currentSetting == null)
                {
                    continue;
                }
                
                if (!UIManager.Instance)
                {
                    descriptionText.gameObject.SetActive(false);
                    SetTextAlpha(0f);
                    continue;
                }
                
                UIManager.Instance.SetText(descriptionText.gameObject, currentSetting);
                descriptionText.gameObject.SetActive(true);
                SetTextAlpha(0f);
                
                yield return StartCoroutine(FadeText(0f, 1f, FadeInTime));
                yield return CoroutineData.GetWaitForSeconds(DisplayTime);
                
                // 이유: 다음 씬으로 전환될 때 화면이 텅 비어 보이는 것을 방지하기 위해, 마지막 텍스트는 지우지 않고 화면에 남겨둠
                if (i < _data.descriptionTexts.Length - 1)
                {
                    yield return StartCoroutine(FadeText(1f, 0f, FadeOutTime));
                }
            }
            
            // 페이지 완료 시점에 전체 캔버스 그룹의 알파값이 1로 유지되도록 명시적으로 고정함
            SetAlpha(1f);
            
            CompleteStep();
        }

        /// <summary> 텍스트의 알파값을 즉시 변경하는 헬퍼 메서드. </summary>
        private void SetTextAlpha(float alpha)
        {
            if (!descriptionText) return;
            Color c = descriptionText.color;
            descriptionText.color = new Color(c.r, c.g, c.b, alpha);
        }

        /// <summary> 텍스트의 투명도를 부드럽게 변경하는 코루틴. </summary>
        private IEnumerator FadeText(float startAlpha, float endAlpha, float duration)
        {
            if (!descriptionText) yield break;

            float elapsed = 0f;
            Color initialColor = descriptionText.color;

            descriptionText.color = new Color(initialColor.r, initialColor.g, initialColor.b, startAlpha);

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float alpha = Mathf.Lerp(startAlpha, endAlpha, elapsed / duration);
                descriptionText.color = new Color(initialColor.r, initialColor.g, initialColor.b, alpha);
                yield return null;
            }

            descriptionText.color = new Color(initialColor.r, initialColor.g, initialColor.b, endAlpha);
        }
    }
}