using My.Scripts.Core;
using UnityEngine;

namespace My.Scripts._00_Title
{
    /// <summary>
    /// 타이틀 화면의 사용자 입력 감지 및 씬 전환을 관리하는 클래스.
    /// </summary>
    public class TitleManager : MonoBehaviour
    {
        private bool _isTransitioning; 

        void Update()
        {
            // 씬 전환 중 중복 입력을 방지하기 위해 플래그를 먼저 확인
            if (!_isTransitioning && Input.GetKeyDown(KeyCode.Return))
            {
                GoToTutorial();
            }
        }

        /// <summary>
        /// 튜토리얼 씬으로의 전환 프로세스를 실행.
        /// </summary>
        private void GoToTutorial()
        {
            _isTransitioning = true;
            
            // 페이드 효과 없이 씬을 넘기기 위해 게임매니저 호출 X
            UnityEngine.SceneManagement.SceneManager.LoadScene(GameConstants.Scene.Tutorial);
        }
    }
}