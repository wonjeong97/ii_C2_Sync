using Microsoft.Extensions.Logging;
using My.Scripts._04_PlayLong;
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
                    if (cubeRenderer.material.HasProperty("_Color")) cubeRenderer.material.SetColor("_Color", color);
                    else if (cubeRenderer.material.HasProperty("_BaseColor")) cubeRenderer.material.SetColor("_BaseColor", color);
                }
            }
        }

        private void OnDrawGizmos()
        {
            if (virtualDistStartToEnd <= 0) return;

            Vector3 segmentVector = pathEnd - pathStart;
            Vector3 centerPos = pathStart + ((segmentVector / virtualDistStartToEnd) * targetDistance);

            Vector3 forwardDir = segmentVector.normalized;
            Vector3 baseRightDir = Vector3.Cross(Vector3.up, forwardDir).normalized;
            
            Quaternion layoutRot = Quaternion.Euler(0, layoutRotationY, 0);
            Vector3 rotatedRightDir = layoutRot * baseRightDir;
            
            Vector3 leftPos = centerPos - (rotatedRightDir * laneWidth);
            Vector3 rightPos = centerPos + (rotatedRightDir * laneWidth);
            Quaternion meshRot = Quaternion.Euler(0, cubeRotationY, 0);

            bool isHitLeft = false;
            bool isHitCenter = false;
            bool isHitRight = false;

            if (Application.isPlaying && playLongManager != null)
            {
                int p1Lane = playLongManager.GetCurrentLane(0);
                int p2Lane = playLongManager.GetCurrentLane(1);

                if (p1Lane == 0 || p2Lane == 0) isHitLeft = true;
                if (p1Lane == 2 || p2Lane == 2) isHitRight = true;

                bool isCenterOccupied = (p1Lane == 1 || p2Lane == 1);
                bool isRedStringActive = (p1Lane == 0 && p2Lane == 2) || (p1Lane == 2 && p2Lane == 0);
                
                if (isCenterOccupied || isRedStringActive) isHitCenter = true;
            }

            DrawGizmoCube(leftPos, meshRot, isHitLeft ? Color.black : Color.red);
            DrawGizmoCube(centerPos, meshRot, isHitCenter ? Color.black : Color.green);
            DrawGizmoCube(rightPos, meshRot, isHitRight ? Color.black : Color.blue);
        }

        private void DrawGizmoCube(Vector3 pos, Quaternion rot, Color color)
        {
            Gizmos.color = color;
            Gizmos.matrix = Matrix4x4.TRS(pos, rot, cubeScale);
            Gizmos.DrawWireCube(Vector3.zero, Vector3.one);
        }
    }
}