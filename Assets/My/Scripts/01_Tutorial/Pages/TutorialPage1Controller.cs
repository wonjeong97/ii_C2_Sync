using System;
using My.Scripts.Core;
using UnityEngine;
using UnityEngine.UI;
using Wonjeong.Data;
using Wonjeong.UI;

namespace My.Scripts._01_Tutorial.Pages
{
    /// <summary>
    /// 튜토리얼 1페이지의 데이터 구조를 정의하는 클래스.
    /// JSON 파일의 "page1" 섹션 데이터와 매핑됨.
    /// </summary>
    [Serializable]
    public class TutorialPage1Data
    {
        // 1페이지는 별도의 타이틀 없이 설명 텍스트만으로 구성되므로 해당 설정만 포함
        public TextSetting descriptionText;
    }

    /// <summary>
    /// 튜토리얼의 첫 번째 페이지 동작을 제어하는 컨트롤러.
    /// 텍스트 정보를 표시하고 사용자의 입력(넘기기)을 대기함.
    /// </summary>
    public class TutorialPage1Controller : GamePage<TutorialPage1Data>
    {
        [Header("UI Components")]
        [SerializeField] private Text descriptionText;

        /// <summary>
        /// 외부에서 로드된 데이터를 받아 UI를 초기화함.
        /// </summary>
        /// <param name="data">JSON 파싱을 통해 생성된 페이지 데이터 객체</param>
        protected override void SetupData(TutorialPage1Data data)
        {
            if (data == null) return;

            // UIManager를 사용하여 텍스트의 스타일(폰트, 크기 등)과 내용을 일괄 적용
            // 이를 통해 데이터와 뷰의 표시 로직을 분리하고 일관된 스타일을 유지함
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
        
        private void Update()
        {
            // 사용자가 내용을 확인하고 다음 단계로 넘어가려는 의도(엔터 또는 클릭)를 확인
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetMouseButtonDown(0))
            {
                // 부모 클래스의 메서드를 호출하여 현재 스텝 완료 이벤트를 전파
                CompleteStep(); 
            }
        }
    }
}