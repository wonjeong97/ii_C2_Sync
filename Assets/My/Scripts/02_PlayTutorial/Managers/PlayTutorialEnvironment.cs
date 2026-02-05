using My.Scripts._02_PlayTutorial.Controllers;
using UnityEngine;

namespace My.Scripts._02_PlayTutorial.Managers
{
    /// <summary>
    /// 튜토리얼 씬의 배경 및 환경 요소(바닥, 프레임, 장애물)를 제어하는 매니저 클래스.
    /// 플레이어의 이동 속도에 맞춰 환경을 역방향으로 스크롤하여 달리는 연출을 구현함.
    /// </summary>
    public class PlayTutorialEnvironment : MonoBehaviour
    {
        [Header("Floor Settings")]
        [SerializeField] private TextureAdjuster p1Floor; 
        [SerializeField] private TextureAdjuster p2Floor; 
    
        [Header("Frame Settings")]
        [SerializeField] private FrameScrollManager p1Frames; 
        [SerializeField] private FrameScrollManager p2Frames;

        [Header("Obstacle Settings")]
        [SerializeField] private TutorialObstacleManager p1Obstacles;
        [SerializeField] private TutorialObstacleManager p2Obstacles;

        /// <summary>
        /// 환경 요소들을 초기 상태로 설정함.
        /// </summary>
        public void InitEnvironment()
        {
            // 스크롤 기능을 활성화하되, 게임 시작 전까지는 움직이지 않도록 초기 속도를 0으로 고정함
            if (p1Floor) { p1Floor.enableScroll = true; p1Floor.scrollSpeedY = 0f; }
            if (p2Floor) { p2Floor.enableScroll = true; p2Floor.scrollSpeedY = 0f; }
        }

        /// <summary>
        /// 매 프레임 플레이어의 속도 정보를 받아 환경 스크롤에 반영함.
        /// </summary>
        /// <param name="p1Speed">플레이어 1의 현재 이동 속도</param>
        /// <param name="p2Speed">플레이어 2의 현재 이동 속도</param>
        public void ScrollEnvironment(float p1Speed, float p2Speed)
        {
            // P1과 P2가 서로 다른 속도(예: 한 명이 스턴 상태)로 움직일 수 있으므로 개별적으로 업데이트 수행
        
            // Player 1 환경 업데이트
            if (p1Floor) p1Floor.scrollSpeedY = p1Speed;
            if (p1Frames) p1Frames.ScrollFrames(p1Speed);
            if (p1Obstacles) p1Obstacles.ScrollObstacles(p1Speed);

            // Player 2 환경 업데이트
            if (p2Floor) p2Floor.scrollSpeedY = p2Speed;
            if (p2Frames) p2Frames.ScrollFrames(p2Speed);
            if (p2Obstacles) p2Obstacles.ScrollObstacles(p2Speed);
        }

        /// <summary>
        /// 특정 플레이어의 장애물을 페이드 인 효과와 함께 등장시킴.
        /// </summary>
        /// <param name="playerIdx">대상 플레이어 인덱스 (0: P1, 1: P2)</param>
        /// <param name="startIndex">등장시킬 장애물의 시작 인덱스</param>
        /// <param name="count">등장시킬 개수</param>
        /// <param name="duration">페이드 인 지속 시간</param>
        public void FadeInObstacles(int playerIdx, int startIndex, int count, float duration)
        {
            // 상위 매니저(PlayTutorialManager)가 하위 시스템(ObstacleManager)을 직접 참조하지 않도록 
            // 환경 매니저가 중계(Facade) 역할을 수행함
            var target = (playerIdx == 0) ? p1Obstacles : p2Obstacles;
            if (target != null) target.FadeInSpecificObstacles(duration, startIndex, count);
        }

        /// <summary>
        /// 양쪽 플레이어의 장애물을 동시에 등장시킴.
        /// </summary>
        /// <param name="startIndex">등장시킬 장애물의 시작 인덱스</param>
        /// <param name="count">등장시킬 개수</param>
        /// <param name="duration">페이드 인 지속 시간</param>
        public void FadeInAllObstacles(int startIndex, int count, float duration)
        {
            // 튜토리얼 페이즈 전환 시 양쪽 라인의 장애물을 동기화하여 보여주기 위함
            if (p1Obstacles) p1Obstacles.FadeInSpecificObstacles(duration, startIndex, count);
            if (p2Obstacles) p2Obstacles.FadeInSpecificObstacles(duration, startIndex, count);
        }
    }
}