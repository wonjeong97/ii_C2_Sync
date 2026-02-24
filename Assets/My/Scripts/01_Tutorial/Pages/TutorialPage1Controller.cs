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
        public TextSetting descriptionText;
    }

    /// <summary>
    /// 튜토리얼의 첫 번째 페이지 동작을 제어하는 컨트롤러.
    /// 텍스트 정보를 표시하고 사용자의 입력(넘기기)을 대기함.
    /// 글로벌 방치 타이머에 영향을 주어 방치 시 특정 텍스트(tagText)가 뜨도록 유도함.
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
            
            if (descriptionText && data.descriptionText != null)
            {
                if (UIManager.Instance)
                {
                    UIManager.Instance.SetText(descriptionText.gameObject, data.descriptionText);
                }
                else
                {
                    Debug.LogWarning("[TutorialPage1Controller] UIManager.Instance가 null입니다. 텍스트 설정을 건너뜁니다.");
                }
            }
        }
        
        /// <summary>
        /// 페이지 진입 시 글로벌 방치 타이머가 작동하도록 설정하고, 출력 텍스트 타입을 변경함.
        /// 이유: 튜토리얼 1페이지에서는 사용자 입력을 기다리며, 방치 시 전용 안내(tagText)를 띄워야 하기 때문임.
        /// </summary>
        public override void OnEnter()
        {
            base.OnEnter();
            if (GameManager.Instance)
            {
                GameManager.Instance.IsAutoProgressing = false;
                GameManager.Instance.CurrentInactivityTextType = InactivityTextType.Tag;
            }
            else
            {
                Debug.LogWarning("[TutorialPage1Controller] GameManager.Instance를 찾을 수 없습니다.");
            }
        }

        /// <summary>
        /// 페이지 퇴장 시 글로벌 방치 텍스트 타입을 기본값으로 원복함.
        /// 이유: 다른 씬이나 구간에서 잘못된 방치 텍스트가 뜨는 것을 방지하기 위함.
        /// </summary>
        public override void OnExit()
        {
            base.OnExit();
            if (GameManager.Instance)
            {
                GameManager.Instance.CurrentInactivityTextType = InactivityTextType.Warning;
            }
        }

        private void Update()
        {
            // 사용자가 내용을 확인하고 다음 단계로 넘어가려는 의도(엔터 또는 클릭)를 확인
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetMouseButtonDown(0))
            {
                if (SoundManager.Instance)
                {
                    SoundManager.Instance.StopBGM();
                    SoundManager.Instance.PlayBGM("MainBGM");
                }
                CompleteStep(); 
            }
        }
    }
}