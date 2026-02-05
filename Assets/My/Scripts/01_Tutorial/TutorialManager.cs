using System;
using UnityEngine;
using My.Scripts.Core;
using My.Scripts.Global;   
using My.Scripts._01_Tutorial.Pages;
using My.Scripts.Core.FlowSystem;
using Wonjeong.Utils;     

namespace My.Scripts._01_Tutorial
{
    /// <summary>
    /// 튜토리얼 씬 전체의 설정 데이터를 담는 루트 클래스.
    /// JSON 파일(Tutorial.json)의 최상위 구조와 1:1로 매핑되어 직렬화됨.
    /// </summary>
    [Serializable]
    public class TutorialSetting
    {
        public TutorialPage1Data page1;
        public TutorialPage2Data page2;
        public TutorialPage3Data page3;
        public TutorialPage4Data page4;
    }

    /// <summary>
    /// 텍스트 기반 튜토리얼(Scene 01)의 전체 흐름을 관리하는 매니저.
    /// JSON 데이터를 로드하여 각 페이지 컨트롤러에 분배하고, 모든 과정이 끝나면 다음 씬으로 이동시킴.
    /// </summary>
    public class TutorialManager : BaseFlowManager
    {
        /// <summary>
        /// 초기화 시 호출되어 외부 설정 파일(JSON)을 로드하고 각 페이지에 필요한 데이터를 주입함.
        /// </summary>
        protected override void LoadSettings()
        {
            var setting = JsonLoader.Load<TutorialSetting>(GameConstants.Path.Tutorial);

            if (setting == null)
            {
                Debug.LogError("[TutorialManager] JSON 데이터 로드 실패");
                return;
            }

            // 부모 클래스(BaseFlowManager)에 할당된 페이지 배열을 순회하며 데이터 주입
            if (pages != null && pages.Length > 0)
            {
                // 각 인덱스의 컨트롤러 타입을 확인(is 패턴 매칭)하여 안전하게 형변환 후 데이터 전달
                // 순서가 고정된 튜토리얼 특성상 인덱스로 직접 접근하여 매핑함
                if (pages.Length > 0 && pages[0] is TutorialPage1Controller page1) page1.SetupData(setting.page1);
                if (pages.Length > 1 && pages[1] is TutorialPage2Controller page2) page2.SetupData(setting.page2);
                if (pages.Length > 2 && pages[2] is TutorialPage3Controller page3) page3.SetupData(setting.page3);
                if (pages.Length > 3 && pages[3] is TutorialPage4Controller page4) page4.SetupData(setting.page4);
            }
        }

        /// <summary>
        /// 모든 페이지의 진행이 완료되었을 때 호출되는 콜백.
        /// </summary>
        protected override void OnAllFinished()
        {
            Debug.Log("튜토리얼 종료 -> 다음 씬(실전 플레이)으로 이동");
            
            // 씬 전환 로직은 GameManager에 위임하여 페이드 효과 등 전역적인 전환 처리를 따르도록 함
            if (GameManager.Instance != null)
            {
                GameManager.Instance.ChangeScene(GameConstants.Scene.PlayTutorial);
            }
        }
    }
}