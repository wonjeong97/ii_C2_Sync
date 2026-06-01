using Microsoft.Extensions.Logging;
using My.Scripts._04_PlayLong;
using My.Scripts.Core;
using UnityEngine;
using VContainer;
using ZLogger;

namespace My.Scripts.Environment
{
    /// <summary>
    /// 월드 공간 상의 트랙 레인 위치와 충돌 영역을 시각화하는 디버그 도구.
    /// VContainer 의존성 주입 및 ZLogger 통합.
    /// </summary>
    public class DebugTrackVisualizer : MonoBehaviour
    {
        private readonly static int Color1 = Shader.PropertyToID("_Color");
        private readonly static int BaseColor = Shader.PropertyToID("_BaseColor");

        [Header("Debug Settings")]
        public float targetDistance = 1.0f;

        public Vector3 cubeScale = new Vector3(1.5f, 2.0f, 2.0f);
        public string targetLayer = "Default";

        [Header("Rotation & Path")]
        public float layoutRotationY = -0.684f;

        public float cubeRotationY = -0.684f;
        public float laneWidth = 1.5f;
        public Vector3 pathStart;
        public Vector3 pathEnd;
        public float virtualDistStartToEnd = 10f;

        [Header("References")]
        public bool isSpawnCube;

        [SerializeField] private PlayLongManager playLongManager; // 에디터 모드 Gizmos용 수동 할당

        private ILogger<DebugTrackVisualizer> _logger;

        [Inject]
        public void Construct(ILogger<DebugTrackVisualizer> logger)
        {
            _logger = logger;
        }

        private void Start()
        {
            SpawnDebugCubes();
        }

        private void SpawnDebugCubes()
        {
            if (virtualDistStartToEnd <= 0) return;

            Vector3 segmentVector = pathEnd - pathStart;
            Vector3 vectorPerMeter = segmentVector / virtualDistStartToEnd;
            Vector3 centerPos = pathStart + (vectorPerMeter * targetDistance);

            Vector3 forwardDir = segmentVector.normalized;
            Vector3 baseRightDir = Vector3.Cross(Vector3.up, forwardDir).normalized;

            Quaternion layoutRot = Quaternion.Euler(0, layoutRotationY, 0);
            Vector3 rotatedRightDir = layoutRot * baseRightDir;

            SpawnCube("Left_Red", centerPos - (rotatedRightDir * laneWidth), Color.red);
            SpawnCube("Center_Green", centerPos, Color.green);
            SpawnCube("Right_Blue", centerPos + (rotatedRightDir * laneWidth), Color.blue);
        }

        private void SpawnCube(string cubeName, Vector3 pos, Color color)
        {
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = cubeName;
            cube.transform.SetPositionAndRotation(pos, Quaternion.Euler(0, cubeRotationY, 0));
            cube.transform.localScale = cubeScale;

            if (cube.TryGetComponent<Collider>(out var col)) col.isTrigger = true;

            int layerIndex = LayerMask.NameToLayer(targetLayer);
            if (layerIndex != -1) cube.layer = layerIndex;

            if (cube.TryGetComponent<Renderer>(out var cubeRenderer))
            {
                cubeRenderer.enabled = isSpawnCube;
                if (isSpawnCube)
                {
                    cubeRenderer.material.color = color;
                    if (cubeRenderer.material.HasProperty(Color1)) cubeRenderer.material.SetColor(Color1, color);
                    else if (cubeRenderer.material.HasProperty(BaseColor))
                        cubeRenderer.material.SetColor(BaseColor, color);
                }
            }
        }

        private void OnDrawGizmos()
        {
            if (virtualDistStartToEnd <= 0) return;

            // 1. 기준 트랙 및 레인 위치 좌표 계산
            Vector3 centerPos = CalculateCenterPosition();
            Vector3 rotatedRightDir = CalculateRotatedRightDirection();

            Vector3 leftPos = centerPos - (rotatedRightDir * laneWidth);
            Vector3 rightPos = centerPos + (rotatedRightDir * laneWidth);
            Quaternion meshRot = Quaternion.Euler(0, cubeRotationY, 0);

            // 2. 플레이어 위치에 따른 충돌(활성화) 상태 판정
            EvaluateLaneOccupancy(out bool isHitLeft, out bool isHitCenter, out bool isHitRight);

            // 3. 각 레인별 디버그 큐브 드로우
            DrawGizmoCube(leftPos, meshRot, isHitLeft ? Color.black : Color.red);
            DrawGizmoCube(centerPos, meshRot, isHitCenter ? Color.black : Color.green);
            DrawGizmoCube(rightPos, meshRot, isHitRight ? Color.black : Color.blue);
        }

        /// <summary>
        /// 트랙의 중심 시작점과 가상 거리를 기반으로 센터 월드 좌표를 계산합니다.
        /// </summary>
        private Vector3 CalculateCenterPosition()
        {
            Vector3 segmentVector = pathEnd - pathStart;
            return pathStart + ((segmentVector / virtualDistStartToEnd) * targetDistance);
        }

        /// <summary>
        /// 레이아웃 회전값이 반영된 우측 방향 벡터를 계산합니다.
        /// </summary>
        private Vector3 CalculateRotatedRightDirection()
        {
            Vector3 segmentVector = pathEnd - pathStart;
            Vector3 baseRightDir = Vector3.Cross(Vector3.up, segmentVector.normalized).normalized;
            return Quaternion.Euler(0, layoutRotationY, 0) * baseRightDir;
        }

        /// <summary>
        /// 런타임 플레이어 데이터를 기반으로 각 레인의 기즈모 활성화 상태를 평가합니다.
        /// </summary>
        private void EvaluateLaneOccupancy(out bool isHitLeft, out bool isHitCenter, out bool isHitRight)
        {
            isHitLeft = false;
            isHitCenter = false;
            isHitRight = false;

            if (!Application.isPlaying || !playLongManager) return;

            PlayerController[] activePlayers = playLongManager.GetComponentsInChildren<PlayerController>();
            if (activePlayers == null || activePlayers.Length < 2) return;

            // 각 플레이어별 안전하게 레인 인덱스 추출 (Null 검사 포함)
            int p1Lane = activePlayers[0] ? activePlayers[0].currentLane : 1;
            int p2Lane = activePlayers[1] ? activePlayers[1].currentLane : 1;

            // 좌/우 및 센터 레인의 활성화 여부 판정 로직 분리
            isHitLeft = CheckSideLaneOccupancy(p1Lane, p2Lane, 0);
            isHitRight = CheckSideLaneOccupancy(p1Lane, p2Lane, 2);
            isHitCenter = CheckCenterLaneOccupancy(p1Lane, p2Lane);
        }

        private bool CheckSideLaneOccupancy(int p1Lane, int p2Lane, int targetLane)
        {
            return p1Lane == targetLane || p2Lane == targetLane;
        }

        private bool CheckCenterLaneOccupancy(int p1Lane, int p2Lane)
        {
            bool isCenterOccupied = (p1Lane == 1 || p2Lane == 1);
            bool isRedStringActive = (p1Lane == 0 && p2Lane == 2) || (p1Lane == 2 && p2Lane == 0);

            return isCenterOccupied || isRedStringActive;
        }

        private void DrawGizmoCube(Vector3 pos, Quaternion rot, Color color)
        {
            Gizmos.color = color;
            Gizmos.matrix = Matrix4x4.TRS(pos, rot, cubeScale);
            Gizmos.DrawWireCube(Vector3.zero, Vector3.one);
        }
    }
}