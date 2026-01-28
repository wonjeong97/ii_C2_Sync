using System;
using My.Scripts.Core;
using UnityEngine;
using UnityEngine.UI;
using Wonjeong.Data;
using Wonjeong.UI;

namespace My.Scripts._01_Tutorial.Pages
{
    // [데이터] JSON의 "page1" 섹션과 1:1 매핑
    [Serializable]
    public class TutorialPage1Data
    {
        public TextSetting descriptionText; // 타이틀 없이 설명 텍스트만 존재
    }

    // [컨트롤러] 페이지 로직 담당
    public class TutorialPage1Controller : GamePage<TutorialPage1Data>
    {
        [Header("UI Components")]
        [SerializeField] private Text descriptionText; // Inspector에서 연결할 UI 텍스트

        // 1. 데이터 주입 (매니저가 호출)
        protected override void SetupData(TutorialPage1Data data)
        {
            if (data == null) return;

            // UIManager를 이용해 JSON 설정값(내용, 폰트, 위치 등)을 UI에 적용
            if (descriptionText != null && data.descriptionText != null)
            {
                if (UIManager.Instance != null)
                {
                    UIManager.Instance.SetText(descriptionText.gameObject, data.descriptionText);
                }
                else
                {
                    Debug.LogWarning("[TutorialPage1Controller] UIManager.Instance가 null입니다.");
                }
            }
        }

        // 2. 페이지 진입
        public override void OnEnter()
        {
            base.OnEnter();
            // 필요 시 추가 연출 (예: 텍스트 페이드 인) 구현 가능
        }

        // 3. 업데이트 (입력 감지)
        private void Update()
        {
            // 엔터 키 또는 마우스 클릭 시 다음 단계로 진행
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetMouseButtonDown(0))
            {
                CompleteStep(); // 상위(TutorialManager)에 완료 신호 전송
            }
        }
    }
}