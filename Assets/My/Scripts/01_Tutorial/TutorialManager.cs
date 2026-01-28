using System;
using UnityEngine;
using My.Scripts.Core;
using My.Scripts.Global;     // GameConstants
using My.Scripts._01_Tutorial.Pages;
using Wonjeong.Utils;        // JsonLoader

namespace My.Scripts._01_Tutorial
{
    // [데이터] 튜토리얼 전체 설정 구조
    [Serializable]
    public class TutorialSetting
    {
        public TutorialPage1Data page1;
        public TutorialPage2Data page2;
        public TutorialPage3Data page3;
    }

    // [매니저] 튜토리얼 흐름 관리
    public class TutorialManager : BaseFlowManager
    {
        // 1. 설정 로드 및 데이터 주입
        protected override void LoadSettings()
        {
            // "Settings/JSON/Tutorial.json" 로드 (경로는 GameConstants 사용 권장)
            var setting = JsonLoader.Load<TutorialSetting>(GameConstants.Path.Tutorial);

            if (setting == null)
            {
                Debug.LogError("[TutorialManager] JSON 데이터 로드 실패");
                return;
            }

            // Pages 배열(Inspector에 할당됨) 중 첫 번째 페이지에 데이터 전달
            if (pages != null && pages.Length > 0)
            {
                // 타입 확인 후 안전하게 주입
                if (pages[0] is TutorialPage1Controller page1) page1.SetupData(setting.page1);
                if (pages[1] is TutorialPage2Controller page2) page2.SetupData(setting.page2);
                if (pages[2] is TutorialPage3Controller page3) page3.SetupData(setting.page3);
            }
        }

        // 2. 모든 페이지 종료 시 처리
        protected override void OnAllFinished()
        {
            Debug.Log("튜토리얼 종료 -> 다음 씬(실전 플레이)으로 이동");
            
            // GameManager가 있다면 씬 전환 요청
            if (GameManager.Instance != null)
            {
                //GameManager.Instance.ChangeScene(GameConstants.Scene.PlayTutorial);
            }
        }
    }
}