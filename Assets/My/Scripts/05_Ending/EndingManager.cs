using System;
using UnityEngine;
using My.Scripts.Core;
using My.Scripts.Core.FlowSystem;
using My.Scripts._05_Ending.Pages;
using Wonjeong.Utils;

namespace My.Scripts._05_Ending
{
    /// <summary>
    /// 엔딩 씬 전체 설정 데이터를 담는 루트 클래스.
    /// JSON 파일(Ending.json)의 최상위 구조와 1:1로 매핑됨.
    /// </summary>
    [Serializable]
    public class EndingSetting
    {
        public EndingPage1Data page1;
        public EndingPage2Data page2;
        public EndingPage3Data page3;
    }

    /// <summary>
    /// 엔딩 씬의 전체 흐름(페이지 전환)을 관리하는 매니저.
    /// BaseFlowManager를 상속받아 JSON 데이터를 로드하고 각 페이지에 주입함.
    /// </summary>
    public class EndingManager : BaseFlowManager
    {
        /// <summary>
        /// 초기화 시 JSON 데이터를 로드하여 각 페이지에 분배.
        /// 외부 설정 파일을 통해 빌드 후에도 텍스트를 수정할 수 있도록 지원.
        /// </summary>
        protected override void LoadSettings()
        {
            EndingSetting setting = JsonLoader.Load<EndingSetting>(GameConstants.Path.Ending);

            if (setting == null)
            {
                Debug.LogError("[EndingManager] JSON 데이터 로드 실패");
                return;
            }

            // 인스펙터에 등록된 페이지 배열에 순차적으로 데이터 주입
            if (pages != null && pages.Length > 0)
            {
                if (pages[0] is EndingPage1Controller page1) 
                    page1.SetupData(setting.page1);
                
                if (pages.Length > 1 && pages[1] is EndingPage2Controller page2) 
                    page2.SetupData(setting.page2);
                
                if (pages.Length > 2 && pages[2] is EndingPage3Controller page3) 
                    page3.SetupData(setting.page3);
            }
        }

        /// <summary>
        /// 등록된 3개의 엔딩 페이지가 모두 끝났을 때 호출.
        /// 게임의 완전한 종료를 의미하므로 타이틀 씬으로 복귀함.
        /// </summary>
        protected override void OnAllFinished()
        {
            if (GameManager.Instance)
            {
                // 모든 상태(inactivity 타이머 등)를 초기화하고 첫 화면으로 돌아감
                GameManager.Instance.ReturnToTitle(); 
            }
            else
            {
                UnityEngine.SceneManagement.SceneManager.LoadScene(GameConstants.Scene.Title);
            }
        }
    }
}