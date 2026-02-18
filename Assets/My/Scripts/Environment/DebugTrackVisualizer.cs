using UnityEngine;
using My.Scripts._04_PlayLong;

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
        public string targetLayer = "Default";

        [Header("Rotation Settings")]
        public float layoutRotationY = -0.684f; 
        public float cubeRotationY = -0.684f; 

        [Header("Track Settings")]
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

            SpawnCube("Left_Red", centerPos - (rotatedRightDir * laneWidth), Color.red);
            SpawnCube("Center_Green", centerPos, Color.green);
            SpawnCube("Right_Blue", centerPos + (rotatedRightDir * laneWidth), Color.blue);    
        }

        private void SpawnCube(string cubeName, Vector3 pos, Color color)
        {
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = cubeName; // 이름에 Left, Center, Right가 포함되어 있어야 ObstacleHitChecker가 인식함
            cube.transform.position = pos;
            cube.transform.rotation = Quaternion.Euler(0, cubeRotationY, 0);
            cube.transform.localScale = cubeScale;

            // ★ [수정] 충돌 감지를 위해 Collider를 유지하되, Trigger로 설정
            Collider col = cube.GetComponent<Collider>();
            if (col != null)
            {
                // 물리적 충돌(밀림)은 방지하고, Trigger 이벤트만 발생시킴
                col.isTrigger = true; 
            }

            // 레이어 설정
            int layerIndex = LayerMask.NameToLayer(targetLayer);
            if (layerIndex != -1) cube.layer = layerIndex;
            
            var cubeRenderer = cube.GetComponent<Renderer>();
            if (cubeRenderer != null)
            {
                // isSpawnCube가 false여도 투명한 상태로 충돌 판정은 유지됨
                cubeRenderer.enabled = isSpawnCube; 
                
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
            Vector3 centerPos = pathStart + (segmentVector / virtualDistStartToEnd * targetDistance);
            Vector3 forwardDir = segmentVector.normalized;
            Vector3 baseRightDir = Vector3.Cross(Vector3.up, forwardDir).normalized;
            Quaternion layoutRot = Quaternion.Euler(0, layoutRotationY, 0);
            Vector3 rotatedRightDir = layoutRot * baseRightDir;
            
            Vector3 leftPos = centerPos - (rotatedRightDir * laneWidth);
            Vector3 rightPos = centerPos + (rotatedRightDir * laneWidth);
            Quaternion meshRot = Quaternion.Euler(0, cubeRotationY, 0);

            // 히트 여부 체크 (시각적 확인용)
            bool isHitLeft = false;
            bool isHitCenter = false;
            bool isHitRight = false;

            if (Application.isPlaying && PlayLongManager.Instance != null)
            {
                int p1Lane = PlayLongManager.Instance.GetCurrentLane(0);
                int p2Lane = PlayLongManager.Instance.GetCurrentLane(1);

                if (p1Lane == 0 || p2Lane == 0) isHitLeft = true;
                if (p1Lane == 2 || p2Lane == 2) isHitRight = true;

                bool isCenterOccupied = (p1Lane == 1 || p2Lane == 1);
                bool isRedStringActive = (p1Lane == 0 && p2Lane == 2) || (p1Lane == 2 && p2Lane == 0);
                
                if (isCenterOccupied || isRedStringActive) isHitCenter = true;
            }

            Matrix4x4 oldMatrix = Gizmos.matrix;
            Gizmos.DrawLine(pathStart, pathEnd);

            Gizmos.color = isHitLeft ? Color.black : Color.red;
            Gizmos.matrix = Matrix4x4.TRS(leftPos, meshRot, cubeScale);
            Gizmos.DrawWireCube(Vector3.zero, Vector3.one);

            Gizmos.color = isHitCenter ? Color.black : Color.green;
            Gizmos.matrix = Matrix4x4.TRS(centerPos, meshRot, cubeScale);
            Gizmos.DrawWireCube(Vector3.zero, Vector3.one);

            Gizmos.color = isHitRight ? Color.black : Color.blue;
            Gizmos.matrix = Matrix4x4.TRS(rightPos, meshRot, cubeScale);
            Gizmos.DrawWireCube(Vector3.zero, Vector3.one);

            Gizmos.matrix = oldMatrix;
        }
    }
}