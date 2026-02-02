using My.Scripts.Global;
using UnityEngine;

// GameManager, GameConstants 사용

namespace My.Scripts._00_Title
{
    public class TitleManager : MonoBehaviour
    {
        private bool _isTransitioning = false; // 중복 입력 방지

        void Update()
        {
            // 엔터 키 입력 감지
            if (!_isTransitioning && Input.GetKeyDown(KeyCode.Return))
            {
                GoToTutorial();
            }
        }

        private void GoToTutorial()
        {
            _isTransitioning = true;
            Debug.Log("[TitleManager] 튜토리얼 진입 요청");

            if (GameManager.Instance != null)
            {
                // GameManager의 씬 전환 기능 사용 (페이드 효과 포함)
                // GameConstants.Scene.Tutorial 상수를 사용하여 안전하게 이동
                GameManager.Instance.ChangeScene(GameConstants.Scene.Tutorial);
            }
            else
            {
                // GameManager가 없을 경우 (테스트용 비상 코드)
                Debug.LogWarning("GameManager가 없습니다. 즉시 로드합니다.");
                UnityEngine.SceneManagement.SceneManager.LoadScene(GameConstants.Scene.Tutorial);
            }
        }
    }
}