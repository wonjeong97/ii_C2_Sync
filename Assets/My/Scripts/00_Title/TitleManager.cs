using My.Scripts.Global;
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
            Debug.Log("[TitleManager] 튜토리얼 진입 요청");

            // GameManager를 경유하여 페이드 효과 등 공통 전환 로직을 적용받기 위함
            if (GameManager.Instance != null)
            {
                GameManager.Instance.ChangeScene(GameConstants.Scene.Tutorial);
            }
            else
            {
                // GameManager가 없는 단독 테스트 환경에서도 씬 이동이 가능하도록 예외 처리
                Debug.LogWarning("GameManager가 없습니다. 즉시 로드합니다.");
                UnityEngine.SceneManagement.SceneManager.LoadScene(GameConstants.Scene.Tutorial);
            }
        }
    }
}