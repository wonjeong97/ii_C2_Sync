using UnityEngine;

namespace My.Scripts._03_Play150M
{
    public class Play150MEnvironment : MonoBehaviour
    {
        [Header("Floor Settings")] 
        [SerializeField] private TextureAdjuster p1Floor;
        [SerializeField] private TextureAdjuster p2Floor;

        [Header("Frame Settings")] 
        [SerializeField] private FrameScrollManager p1Frames;
        [SerializeField] private FrameScrollManager p2Frames;

        [Header("Obstacle Settings")]
        [SerializeField] private Play150MObstacleManager p1Obstacles; 
        [SerializeField] private Play150MObstacleManager p2Obstacles;

        [Header("Cameras")] 
        [SerializeField] private Camera leftCamera;
        [SerializeField] private Camera rightCamera;

        public void InitEnvironment()
        {
            if (p1Floor) { p1Floor.enableScroll = true; p1Floor.scrollSpeedY = 0f; }
            if (p2Floor) { p2Floor.enableScroll = true; p2Floor.scrollSpeedY = 0f; }

        }

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