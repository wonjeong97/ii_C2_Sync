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
        [SerializeField] private Text descriptionText; // 단일 텍스트 컴포넌트를 재사용하여 여러 메시지를 출력함

        private TutorialPage4Data _data;
        
        // 연출 타이밍 상수 (초 단위)
        private const float FadeInTime = 1f;
        private const float DisplayTime = 3f;
        private const float FadeOutTime = 1f;

        /// <summary>
        /// 외부 데이터를 받아 컴포넌트를 초기화함.
        /// </summary>
        /// <param name="data">텍스트 배열이 포함된 데이터 객체</param>
        protected override void SetupData(TutorialPage4Data data)
        {
            _data = data;
            
            if (descriptionText != null)
            {
                // 연출 시작 전 텍스트가 화면에 보이는 것을 방지하기 위해 투명하게 초기화하고 비활성화함
                Color c = descriptionText.color;
                descriptionText.color = new Color(c.r, c.g, c.b, 0f);
                descriptionText.gameObject.SetActive(false);
            }
        }

        /// <summary>
        /// 페이지 진입 시 연출 시퀀스를 시작함.
        /// </summary>
        public override void OnEnter()
        {
            base.OnEnter();

            // 데이터가 유효한 경우에만 연출을 시작하고, 없으면 바로 다음 단계로 넘겨 진행 막힘을 방지함
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

        /// <summary>
        /// 정의된 텍스트 배열을 순회하며 페이드 연출을 수행하는 메인 코루틴.
        /// </summary>
        private IEnumerator ScenarioRoutine()
        {
            // 모든 텍스트 설정을 순서대로 처리 (설정 적용 -> 페이드 인 -> 대기 -> 페이드 아웃)
            for (int i = 0; i < _data.descriptionTexts.Length; i++)
            {
                var currentSetting = _data.descriptionTexts[i];
                if (descriptionText == null || currentSetting == null)
                {
                    Debug.LogWarning("[TutorialPage4] descriptionText 또는 currentSetting이 null입니다. 해당 항목을 건너뜁니다.");
                    continue;
                }
                if (UIManager.Instance == null)
                {
                    Debug.LogWarning("[TutorialPage4] UIManager.Instance가 null입니다.");
                    descriptionText.gameObject.SetActive(false);
                    SetTextAlpha(0f);
                    continue;
                }
                UIManager.Instance.SetText(descriptionText.gameObject, currentSetting);
                // 연출 시작을 위해 활성화하되, 페이드 인 효과를 위해 투명 상태로 시작
                descriptionText.gameObject.SetActive(true);
                SetTextAlpha(0f);
                // 1. 텍스트 서서히 등장
                yield return StartCoroutine(FadeText(0f, 1f, FadeInTime));
                // 2. 사용자가 읽을 수 있도록 일정 시간 대기
                yield return CoroutineData.GetWaitForSeconds(DisplayTime);
                // 3. 텍스트 서서히 퇴장
                yield return StartCoroutine(FadeText(1f, 0f, FadeOutTime));
            }
            // 모든 텍스트 시나리오가 끝나면 페이지 완료 처리
            CompleteStep();
        }

        /// <summary>
        /// 텍스트의 알파값을 즉시 변경하는 헬퍼 메서드.
        /// </summary>
        private void SetTextAlpha(float alpha)
        {
            if (descriptionText == null) return;
            Color c = descriptionText.color;
            descriptionText.color = new Color(c.r, c.g, c.b, alpha);
        }

        /// <summary>
        /// 텍스트의 투명도를 부드럽게 변경하는 코루틴.
        /// </summary>
        /// <param name="startAlpha">시작 투명도</param>
        /// <param name="endAlpha">목표 투명도</param>
        /// <param name="duration">변경에 걸리는 시간</param>
        private IEnumerator FadeText(float startAlpha, float endAlpha, float duration)
        {
            if (descriptionText == null) yield break;

            float elapsed = 0f;
            Color initialColor = descriptionText.color;

            // 시작 시점의 색상 설정
            descriptionText.color = new Color(initialColor.r, initialColor.g, initialColor.b, startAlpha);

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float alpha = Mathf.Lerp(startAlpha, endAlpha, elapsed / duration);
                descriptionText.color = new Color(initialColor.r, initialColor.g, initialColor.b, alpha);
                yield return null;
            }

            // 연산 오차 방지를 위해 목표값으로 확실하게 고정
            descriptionText.color = new Color(initialColor.r, initialColor.g, initialColor.b, endAlpha);
        }
    }
}