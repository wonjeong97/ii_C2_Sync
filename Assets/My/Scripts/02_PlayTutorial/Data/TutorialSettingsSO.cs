using My.Scripts._02_PlayTutorial.Controllers;
using UnityEngine;

namespace My.Scripts._02_PlayTutorial.Data
{
    /// <summary>
    /// 튜토리얼 씬(PlayTutorial)에서 사용되는 모든 정적 설정값들을 중앙 관리하는 스크립터블 오브젝트.
    /// 기획자가 코드를 수정하지 않고 물리, UI 좌표, 게임 룰(목표 거리 등)을 인스펙터에서 조정할 수 있도록 함.
    /// </summary>
    [CreateAssetMenu(fileName = "TutorialSettings", menuName = "Tutorial/Settings")]
    public class TutorialSettingsSO : ScriptableObject
    {
        [Header("Physics Settings")]
        [Tooltip("플레이어 이동, 가속, 감속 등 물리 연산에 필요한 상수 모음.")]
        public PlayerPhysicsConfig physicsConfig;

        [Header("Lane Positions")]
        [Tooltip("P1(왼쪽 플레이어)이 각 라인(좌, 중, 우)에 위치할 때의 UI 앵커 좌표.")]
        public Vector2[] p1LanePositions;

        [Tooltip("P2(오른쪽 플레이어)이 각 라인(좌, 중, 우)에 위치할 때의 UI 앵커 좌표.")]
        public Vector2[] p2LanePositions;

        [Header("Phase Settings")]
        [Tooltip("Phase 1(중앙 달리기)의 목표 달성 거리 (미터).")]
        public float targetDistancePhase1 = 10f;

        [Tooltip("Phase 2(우측 달리기)의 목표 달성 거리 (미터).")]
        public float targetDistancePhase2 = 6f;

        [Tooltip("Phase 3(좌측 달리기)의 목표 달성 거리 (미터).")]
        public float targetDistancePhase3 = 8f;

        [Header("Auto Run Settings")]
        [Tooltip("자동 달리기 시 적용할 최대 속도 비율. (MaxScrollSpeed 대비 % 단위)")]
        public float autoRunSpeedRatio = 0.05f;

        [Tooltip("자동 달리기 시작/종료 시 속도가 부드럽게 변하는 시간 (초). 급격한 속도 변화로 인한 위화감을 줄이기 위함.")]
        public float autoRunSmoothTime = 2.0f;

        [Tooltip("자동 달리기가 지속되는 시간 (초). 연출 길이를 제어함.")]
        public float autoRunDuration = 1.5f;
    }
}