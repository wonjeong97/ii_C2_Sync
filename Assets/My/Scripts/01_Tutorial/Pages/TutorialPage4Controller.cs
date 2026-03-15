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
        [SerializeField] private Text descriptionText; 

        private TutorialPage4Data _data;
        
        private const float FadeInTime = 0.5f;
        private const float DisplayTime = 3f;
        private const float FadeOutTime = 0.5f;

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

        public override void OnEnter()
        {
            base.OnEnter();

            // 이유: 시네마틱 텍스트 연출 구간이므로 유저가 가만히 있어도 타이머가 울리지 않게 정지시킴
            if (GameManager.Instance) GameManager.Instance.IsAutoProgressing = true;

            if (_data != null && _data.descriptionTexts != null && _data.descriptionTexts.Length > 0)
            {
                StartCoroutine(ScenarioRoutine());
            }
            else
            {
                CompleteStep();
            }
        }

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
            
            SetAlpha(1f);
            
            CompleteStep();
        }

        private void SetTextAlpha(float alpha)
        {
            if (!descriptionText) return;
            Color c = descriptionText.color;
            descriptionText.color = new Color(c.r, c.g, c.b, alpha);
        }

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