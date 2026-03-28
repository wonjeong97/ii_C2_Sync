using UnityEngine;
using My.Scripts._04_PlayLong;

namespace My.Scripts.Environment
{
    /// <summary>
    /// 개발 단계에서 트랙의 레인 위치와 충돌 판정 영역을 시각적으로 확인하기 위한 디버그 도구 클래스.
    /// </summary>
    public class DebugTrackVisualizer : MonoBehaviour
    {
        [Header("Debug Settings")]
        [Tooltip("표시할 거리 (미터)")]
        public float targetDistance = 1.0f;

        [Tooltip("큐브 크기")]
        public Vector3 cubeScale = new Vector3(1.5f, 2.0f, 2.0f);
    
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

        /// <summary>
        /// 게임 시작 시 설정된 위치에 디버그용 프리미티브 큐브들을 생성함.
        /// </summary>
        private void Start()
        {
            SpawnDebugCubes();
        }

        /// <summary>
        /// 트랙 경로와 레인 간격을 계산하여 좌, 중, 우 위치에 큐브를 배치함.
        /// </summary>
        private void SpawnDebugCubes()
        {
            if (virtualDistStartToEnd <= 0) return;

            Vector3 segmentVector = pathEnd - pathStart;
            // 예시 입력: segmentVector(10,0,0) / virtualDistStartToEnd(10) -> 결과값 = (1,0,0) (1미터당 월드 벡터)
            Vector3 vectorPerMeter = segmentVector / virtualDistStartToEnd;
            Vector3 centerPos = pathStart + (vectorPerMeter * targetDistance);

            Vector3 forwardDir = segmentVector.normalized;
            Vector3 baseRightDir = Vector3.Cross(Vector3.up, forwardDir).normalized;

            Quaternion layoutRot = Quaternion.Euler(0, layoutRotationY, 0);
            Vector3 rotatedRightDir = layoutRot * baseRightDir;

            // 이유: 각 레인의 정확한 물리 판정 위치를 시각화하여 장애물 배치 정밀도를 검증함.
            SpawnCube("Left_Red", centerPos - (rotatedRightDir * laneWidth), Color.red);
            SpawnCube("Center_Green", centerPos, Color.green);
            SpawnCube("Right_Blue", centerPos + (rotatedRightDir * laneWidth), Color.blue);    
        }

        /// <summary>
        /// 단일 디버그 큐브를 생성하고 속성(이름, 위치, 색상, 레이어)을 설정함.
        /// </summary>
        /// <param name="cubeName">오브젝트 이름</param>
        /// <param name="pos">배치 좌표</param>
        /// <param name="color">표시 색상</param>
        private void SpawnCube(string cubeName, Vector3 pos, Color color)
        {
            // # TODO: 빈번한 생성/삭제가 필요한 도구라면 런타임 성능을 위해 풀링 또는 가상 시각화(Gizmos) 위주로 변경 검토 필요.
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = cubeName; 
            cube.transform.position = pos;
            cube.transform.rotation = Quaternion.Euler(0, cubeRotationY, 0);
            cube.transform.localScale = cubeScale;

            Collider col = cube.GetComponent<Collider>();
            if (col)
            {
                // 이유: 물리 연산에는 영향을 주지 않으면서 레이캐스트 등의 트리거 체크만 가능하게 함.
                col.isTrigger = true; 
            }

            int layerIndex = LayerMask.NameToLayer(targetLayer);
            if (layerIndex != -1) cube.layer = layerIndex;
            
            Renderer cubeRenderer = cube.GetComponent<Renderer>();
            if (cubeRenderer)
            {
                cubeRenderer.enabled = isSpawnCube; 
                
                if (isSpawnCube)
                {
                    cubeRenderer.material.color = color;
                    // 이유: 셰이더 프로퍼티 이름이 환경에 따라 다를 수 있으므로 순차적 확인 후 적용.
                    if (cubeRenderer.material.HasProperty("_Color"))
                        cubeRenderer.material.SetColor("_Color", color);
                    else if (cubeRenderer.material.HasProperty("_BaseColor"))
                        cubeRenderer.material.SetColor("_BaseColor", color);
                }
            }
        }

        /// <summary>
        /// 에디터 뷰에서 트랙 경로와 실시간 플레이어 위치 상태를 기즈모로 표시함.
        /// </summary>
        private void OnDrawGizmos()
        {
            if (virtualDistStartToEnd <= 0) return;

            Vector3 segmentVector = pathEnd - pathStart;
            Vector3 vectorPerMeter = segmentVector / virtualDistStartToEnd;
            Vector3 centerPos = pathStart + (vectorPerMeter * targetDistance);

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

            // 이유: 플레이 중일 때 실제 플레이어의 레인 점유 상태를 색상 변화(검정색)로 피드백함.
            if (Application.isPlaying && PlayLongManager.Instance)
            {
                int p1Lane = PlayLongManager.Instance.GetCurrentLane(0);
                int p2Lane = PlayLongManager.Instance.GetCurrentLane(1);

                if (p1Lane == 0 || p2Lane == 0) isHitLeft = true;
                if (p1Lane == 2 || p2Lane == 2) isHitRight = true;

                bool isCenterOccupied = (p1Lane == 1 || p2Lane == 1);
                // 이유: 양 끝 레인 사이를 가로지르는 붉은 실의 판정 영역 포함 여부 체크.
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