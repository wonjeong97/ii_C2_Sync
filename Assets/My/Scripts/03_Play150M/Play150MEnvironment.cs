using UnityEngine;
using My.Scripts.Environment; // FrameScrollManager, TextureAdjuster
using My.Scripts._02_PlayTutorial.Controllers; // TutorialObstacleManager 재사용 (기능이 같다면)

namespace My.Scripts._03_Play150M.Managers
{
    /// <summary>
    /// 150M 달리기 모드의 배경 및 환경 요소를 제어하는 매니저입니다.
    /// 튜토리얼과 달리 연출 로직을 배제하고 스크롤 동기화에 집중합니다.
    /// </summary>
    public class Play150MEnvironment : MonoBehaviour
    {
        [Header("Floor Settings")] 
        [SerializeField] private TextureAdjuster p1Floor;
        [SerializeField] private TextureAdjuster p2Floor;

        [Header("Frame Settings")] 
        [SerializeField] private FrameScrollManager p1Frames;
        [SerializeField] private FrameScrollManager p2Frames;

        [Header("Obstacle Settings")]
        [SerializeField] private TutorialObstacleManager p1Obstacles; // 기존 장애물 매니저 재사용
        [SerializeField] private TutorialObstacleManager p2Obstacles;

        [Header("Cameras")] 
        [SerializeField] private Camera leftCamera;
        [SerializeField] private Camera rightCamera;

        /// <summary>
        /// 환경 요소 초기화 (스크롤 활성화 준비)
        /// </summary>
        public void InitEnvironment()
        {
            if (p1Floor) { p1Floor.enableScroll = true; p1Floor.scrollSpeedY = 0f; }
            if (p2Floor) { p2Floor.enableScroll = true; p2Floor.scrollSpeedY = 0f; }
        }

        /// <summary>
        /// 플레이어 속도에 맞춰 바닥, 프레임, 장애물을 스크롤합니다.
        /// </summary>
        public void ScrollEnvironment(float p1Speed, float p2Speed)
        {
            // Player 1
            if (p1Floor) p1Floor.scrollSpeedY = p1Speed;
            if (p1Frames) p1Frames.ScrollFrames(p1Speed);
            if (p1Obstacles) p1Obstacles.ScrollObstacles(p1Speed);

            // Player 2
            if (p2Floor) p2Floor.scrollSpeedY = p2Speed;
            if (p2Frames) p2Frames.ScrollFrames(p2Speed);
            if (p2Obstacles) p2Obstacles.ScrollObstacles(p2Speed);
        }

        /// <summary>
        /// 팝업 등장 등으로 인해 시야를 가리는 프레임을 강제로 뒤로 보냅니다.
        /// </summary>
        public void RecycleFrameClosestToCamera(int playerIdx)
        {
            if (playerIdx == 0 && p1Frames && leftCamera)
            {
                p1Frames.ForceRecycleFrameClosestToCamera(leftCamera.transform);
            }
            else if (playerIdx == 1 && p2Frames && rightCamera)
            {
                p2Frames.ForceRecycleFrameClosestToCamera(rightCamera.transform);
            }
        }
    }
}