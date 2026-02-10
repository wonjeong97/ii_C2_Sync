using UnityEngine;

namespace My.Scripts.Environment
{
    public class DebugTrackVisualizer : MonoBehaviour
    {
        [Header("Debug Settings")]
        [Tooltip("표시할 거리 (미터)")]
        public float targetDistance = 1.0f;

        [Tooltip("큐브 크기")]
        public Vector3 cubeScale = new Vector3(0.5f, 0.5f, 0.5f);
    
        [Header("Layer Settings")]
        [Tooltip("큐브에 적용할 레이어 이름 (Unity 에디터에서 추가한 레이어 이름 입력, 예: Left, Right)")]
        public string targetLayer = "Default";

        [Header("Rotation Settings")]
        [Tooltip("배치 라인 회전 (중앙 큐브 기준 좌/우 큐브 위치 회전)")]
        public float layoutRotationY = -0.684f; 

        [Tooltip("큐브 자체 회전 (큐브가 바라보는 방향)")]
        public float cubeRotationY = -0.684f; 

        [Header("Track Settings")]
        [Tooltip("라인 간격 (폭)")]
        public float laneWidth = 1.5f;

        [Header("Path Settings")]
        public Vector3 pathStart;
        public Vector3 pathEnd;
        public float virtualDistStartToEnd = 10f;

        [Header("Spawn Cube")] 
        public bool isSpawnCube;

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

            // 큐브 생성
            SpawnCube("Left_Red", centerPos - (rotatedRightDir * laneWidth), Color.red);
            SpawnCube("Center_Green", centerPos, Color.green);
            SpawnCube("Right_Blue", centerPos + (rotatedRightDir * laneWidth), Color.blue);    
        }

        private void SpawnCube(string cubeName, Vector3 pos, Color color)
        {
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = cubeName;
            cube.transform.position = pos;
            cube.transform.rotation = Quaternion.Euler(0, cubeRotationY, 0);
            cube.transform.localScale = cubeScale;

            // ★ 레이어 적용 로직
            int layerIndex = LayerMask.NameToLayer(targetLayer);
            if (layerIndex != -1)
            {
                cube.layer = layerIndex;
            }
            else
            {
                Debug.LogWarning($"[DebugTrackVisualizer] '{targetLayer}' 레이어를 찾을 수 없습니다. Default로 생성됩니다.");
            }

            var cubeRenderer = cube.GetComponent<Renderer>();
            if (cubeRenderer != null)
            {
                // isSpawnCube가 true면 켜고, false면 끔
                cubeRenderer.enabled = isSpawnCube; 

                // 색상 설정 (켜져 있을 때만 의미 있음)
                if (isSpawnCube)
                {
                    cubeRenderer.material.color = color;
                    if (cubeRenderer.material.HasProperty("_Color"))
                        cubeRenderer.material.SetColor("_Color", color);
                    else if (cubeRenderer.material.HasProperty("_BaseColor"))
                        cubeRenderer.material.SetColor("_BaseColor", color);
                }
            }
        }

        private void OnDrawGizmos()
        {
            if (virtualDistStartToEnd <= 0) return;
            Vector3 segmentVector = pathEnd - pathStart;
            Vector3 forwardDir = segmentVector.normalized;
            Vector3 baseRightDir = Vector3.Cross(Vector3.up, forwardDir).normalized;
            Vector3 centerPos = pathStart + (segmentVector / virtualDistStartToEnd * targetDistance);
            Quaternion layoutRot = Quaternion.Euler(0, layoutRotationY, 0);
            Vector3 rotatedRightDir = layoutRot * baseRightDir;
            Vector3 leftPos = centerPos - (rotatedRightDir * laneWidth);
            Vector3 rightPos = centerPos + (rotatedRightDir * laneWidth);
            Quaternion meshRot = Quaternion.Euler(0, cubeRotationY, 0);

            Gizmos.color = Color.white;
            Gizmos.DrawLine(pathStart, pathEnd);
            Matrix4x4 oldMatrix = Gizmos.matrix;
            Gizmos.color = Color.red;
            Gizmos.matrix = Matrix4x4.TRS(leftPos, meshRot, cubeScale);
            Gizmos.DrawWireCube(Vector3.zero, Vector3.one);
            Gizmos.color = Color.green;
            Gizmos.matrix = Matrix4x4.TRS(centerPos, meshRot, cubeScale);
            Gizmos.DrawWireCube(Vector3.zero, Vector3.one);
            Gizmos.color = Color.blue;
            Gizmos.matrix = Matrix4x4.TRS(rightPos, meshRot, cubeScale);
            Gizmos.DrawWireCube(Vector3.zero, Vector3.one);
            Gizmos.matrix = oldMatrix;
        }
    }
}