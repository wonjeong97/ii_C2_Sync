using UnityEngine;

namespace My.Scripts.Core
{
    [ExecuteInEditMode]
    public class DisplaySystem : MonoBehaviour
    {
        [Header("Cameras")]
        public Camera camP1; // 1P (왼쪽)
        public Camera camP2; // 2P (오른쪽)

        [Header("Resolution Settings (Pixels)")]
        public float fullScreenWidth = 3840f;   // 전체 해상도 너비 (예: 3840)
        public float targetActiveWidth = 2000f; // 실제 사용할 중앙 너비 (예: 2000)

        void Update()
        {
            UpdateViewports();
        }

        void UpdateViewports()
        {
            if (camP1 == null || camP2 == null || fullScreenWidth <= 0 || targetActiveWidth <= 0) return;
            // 1. 전체 너비 대비 사용 비율 계산 (2000 / 3840 = 0.52...)
            float activeRatio = Mathf.Clamp01(targetActiveWidth / fullScreenWidth);
        
            // 2. 플레이어 1명이 차지할 비율 (절반)
            float singlePlayerRatio = activeRatio / 2f;
            // 3. 뷰포트 설정
            // [P1] 중앙(0.5)에서 왼쪽으로 자신의 너비만큼 이동
            float p1X = 0.5f - singlePlayerRatio;
            camP1.rect = new Rect(p1X, 0f, singlePlayerRatio, 1f);
            // [P2] 중앙(0.5)에서 시작
            float p2X = 0.5f;
            camP2.rect = new Rect(p2X, 0f, singlePlayerRatio, 1f);
        }
    }
}