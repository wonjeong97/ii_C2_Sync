using System;
using System.Collections;
using UnityEngine;
using My.Scripts.Core;
using My.Scripts._01_Tutorial.Pages;
using My.Scripts.Core.FlowSystem;
using Wonjeong.Utils;     

namespace My.Scripts._01_Tutorial
{
    [Serializable]
    public class TutorialSetting
    {
        public TutorialPage1Data page1;
        public TutorialPage2Data page2;
        public TutorialPage3Data page3;
        public TutorialPage4Data page4;
    }

    public class TutorialManager : BaseFlowManager
    {
        protected override void LoadSettings()
        {
            TutorialSetting setting = JsonLoader.Load<TutorialSetting>(GameConstants.Path.Tutorial);

            if (setting == null)
            {
                Debug.LogError("[TutorialManager] JSON 데이터 로드 실패");
                return;
            }

            if (pages != null && pages.Length > 0)
            {
                if (pages.Length > 0 && pages[0] is TutorialPage1Controller page1) page1.SetupData(setting.page1);
                if (pages.Length > 1 && pages[1] is TutorialPage2Controller page2) page2.SetupData(setting.page2);
                if (pages.Length > 2 && pages[2] is TutorialPage3Controller page3) page3.SetupData(setting.page3);
                if (pages.Length > 3 && pages[3] is TutorialPage4Controller page4) page4.SetupData(setting.page4);
            }
        }

        protected override void OnAllFinished()
        {
            if (GameManager.Instance)
            {
                GameManager.Instance.ChangeScene(GameConstants.Scene.PlayTutorial);
            }
        }

        /// <summary>
        /// 튜토리얼 씬 진입 시 첫 페이지의 페이드 연출을 생략하고 즉시 노출합니다.
        /// 이유: 기획 상 캔버스 그룹은 즉시(Alpha 1) 보여주고 내부 텍스트만 별도로 0.5초간 페이드인하기 위함입니다.
        /// </summary>
        protected override IEnumerator TransitionRoutine(int targetIndex, int info)
        {
            if (currentPageIndex == -1 && targetIndex == 0)
            {
                currentPageIndex = 0;
                GamePage next = pages[0];
                
                if (next)
                {
                    next.OnEnter();
                    next.SetAlpha(1f); // 부모의 FadePage 코루틴을 거치지 않고 즉시 투명도 1 적용
                }
                
                isTransitioning = false;
                yield break;
            }

            // 첫 페이지가 아닌 나머지 페이지들은 기존의 정상적인 페이드 전환 사용
            yield return base.TransitionRoutine(targetIndex, info);
        }
    }
}