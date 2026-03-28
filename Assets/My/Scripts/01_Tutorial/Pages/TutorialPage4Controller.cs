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

        /// <summary>
        /// 외부 데이터를 받아 UI 초기 상태를 세팅함.
        /// </summary>
        /// <param name="data">적용할 텍스트 배열 설정 데이터</param>
        protected override void SetupData(TutorialPage4Data data)
        {
            if (data == null)
            {
                Debug.LogWarning("TutorialPage4Data 데이터가 누락됨.");
                return;
            }

            _data = data;
            
            if (descriptionText)
            {
                // 이유: 연출 시작 전 텍스트가 노출되지 않도록 투명 및 비활성화 처리함.
                Color c = descriptionText.color;
                descriptionText.color = new Color(c.r, c.g, c.b, 0f);
                descriptionText.gameObject.SetActive(false);
            }
            else
            {
                Debug.LogWarning("descriptionText 컴포넌트가 누락됨.");
            }
        }

        /// <summary>
        /// 페이지 진입 시 호출됨.
        /// 시네마틱 텍스트 연출을 시작함.
        /// </summary>
        public override void OnEnter()
        {
            base.OnEnter();

            // 이유: 시네마틱 텍스트 연출 구간이므로 유저가 가만히 있어도 타이머가 울리지 않게 정지시킴.
            if (GameManager.Instance) GameManager.Instance.IsAutoProgressing = true;

            if (_data != null && _data.descriptionTexts != null && _data.descriptionTexts.Length > 0)
            {
                StartCoroutine(ScenarioRoutine());
            }
            else
            {
                Debug.LogWarning("descriptionTexts 배열 데이터가 부족하여 연출을 스킵함.");
                CompleteStep();
            }
        }

        /// <summary>
        /// 설정된 텍스트 배열을 순차적으로 화면에 페이드 인/아웃하여 보여줌.
        /// </summary>
        /// <returns>IEnumerator 루틴</returns>
        private IEnumerator ScenarioRoutine()
        {
            for (int i = 0; i < _data.descriptionTexts.Length; i++)
            {
                TextSetting currentSetting = _data.descriptionTexts[i];
                
                if (!descriptionText)
                {
                    Debug.LogWarning("descriptionText 컴포넌트가 누락되어 연출을 진행할 수 없음.");
                    continue;
                }
                
                if (currentSetting == null)
                {
                    Debug.LogWarning($"Index {i}의 TextSetting 데이터가 누락됨.");
                    continue;
                }
                
                if (!UIManager.Instance)
                {
                    Debug.LogWarning("UIManager.Instance가 누락됨.");
                    descriptionText.gameObject.SetActive(false);
                    SetTextAlpha(0f);
                    continue;
                }
                
                UIManager.Instance.SetText(descriptionText.gameObject, currentSetting);
                descriptionText.gameObject.SetActive(true);
                SetTextAlpha(0f);
                
                yield return StartCoroutine(FadeText(0f, 1f, FadeInTime));
                yield return CoroutineData.GetWaitForSeconds(DisplayTime);
                
                // 다음 씬으로 전환될 때 화면이 텅 비어 보이는 것을 방지하기 위해, 마지막 텍스트는 지우지 않고 화면에 남겨둠.
                if (i < _data.descriptionTexts.Length - 1)
                {
                    yield return StartCoroutine(FadeText(1f, 0f, FadeOutTime));
                }
            }
            
            SetAlpha(1f);
            
            CompleteStep();
        }

        /// <summary>
        /// 텍스트 컴포넌트의 알파값을 강제로 설정함.
        /// </summary>
        /// <param name="alpha">적용할 알파값</param>
        private void SetTextAlpha(float alpha)
        {
            if (!descriptionText) return;

            // # TODO: 텍스트 알파 제어가 빈번하므로 CanvasGroup을 활용한 최적화 고려 필요.
            Color c = descriptionText.color;
            descriptionText.color = new Color(c.r, c.g, c.b, alpha);
        }

        /// <summary>
        /// 지정된 시간 동안 텍스트의 투명도를 부드럽게 조절함.
        /// </summary>
        /// <param name="startAlpha">시작 알파값</param>
        /// <param name="endAlpha">목표 알파값</param>
        /// <param name="duration">진행 시간</param>
        /// <returns>IEnumerator 루틴</returns>
        private IEnumerator FadeText(float startAlpha, float endAlpha, float duration)
        {
            if (!descriptionText) yield break;

            float elapsed = 0f;
            Color initialColor = descriptionText.color;

            descriptionText.color = new Color(initialColor.r, initialColor.g, initialColor.b, startAlpha);

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                
                // 예시 입력: startAlpha = 0f, endAlpha = 1f, elapsed = 0.25f, duration = 0.5f -> 결과값 = 0.5f (알파 50%)
                float alpha = Mathf.Lerp(startAlpha, endAlpha, elapsed / duration);
                descriptionText.color = new Color(initialColor.r, initialColor.g, initialColor.b, alpha);
                
                yield return null;
            }

            descriptionText.color = new Color(initialColor.r, initialColor.g, initialColor.b, endAlpha);
        }
    }
}