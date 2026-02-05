using System.Collections;
using System.Collections.Generic;
using My.Scripts._02_PlayTutorial.Components;
using UnityEngine;

namespace My.Scripts._02_PlayTutorial.Controllers
{
    /// <summary>
    /// 장애물 생성 위치와 라인 정보를 정의하는 구조체.
    /// </summary>
    [System.Serializable]
    public struct ObstacleSpawnData
    {
        public float distance; // 가상 10M로부터의 거리 (미터)
        public int laneIndex;  // 배치할 라인 (-1: 좌, 0: 중, 1: 우)
    }

    /// <summary>
    /// 튜토리얼 씬에서 장애물을 생성하고, 바닥 스크롤 속도에 맞춰 이동시키는 관리자 클래스.
    /// 바닥 텍스처의 UV 스크롤과 실제 오브젝트의 월드 좌표 이동을 동기화하여 플레이어가 달리는 듯한 착시를 줌.
    /// </summary>
    public class TutorialObstacleManager : MonoBehaviour
    {
        [Header("Obstacle Settings")]
        [SerializeField] private GameObject obstaclePrefab;
    
        // 튜토리얼 단계별로 등장할 장애물 데이터 배열 (예: 0~3번 인덱스)
        [SerializeField] private ObstacleSpawnData[] spawnData; 

        [Tooltip("라인 간격 (폭)")]
        [SerializeField] private float laneWidth = 1.5f;

        [Header("Fix Distortion")]
        [Tooltip("경로가 기울어져 있을 때, 라인 간격이 왜곡되는 현상을 보정할지 여부")]
        [SerializeField] private bool useZCompensatedLanes = true;

        [Header("Path Settings")]
        // 가상의 트랙 경로 (직선이지만 월드 좌표상에서는 대각선일 수 있음)
        public Vector3 pathStart = new Vector3(3.286f, -3.5f, 2.52f);
        public Vector3 pathEnd = new Vector3(23.278f, -3.5f, 17.57f);
        public float virtualDistStartToEnd = 10f;

        [Header("Sync Settings")]
        [SerializeField] private float uvLoopSize = 0.025f;
        [SerializeField] private float virtualMetersPerLoop = 5f;
    
        [Header("Player Settings")]
        [Tooltip("이 매니저가 담당하는 플레이어 (0: P1, 1: P2)")]
        public int playerIndex;

        // 생성된 장애물 객체 관리 리스트
        private readonly List<GameObject> _spawnedObstacles = new List<GameObject>();
        // 페이드 효과 제어를 위한 모든 렌더러 캐싱 리스트
        private readonly List<Renderer> _allRenderers = new List<Renderer>(); 
    
        private Vector3 _moveDirection; 
        private Vector3 _laneOffsetVector;
        private float _worldDistPerUV;

        private void Start()
        {
            InitializeManager();
        }

        /// <summary>
        /// 경로 벡터를 계산하고 장애물을 미리 생성하여 배치함.
        /// </summary>
        private void InitializeManager()
        {
            if (virtualDistStartToEnd <= 0) return;

            // 장애물은 플레이어가 다가오는 방향(End -> Start) 반대로 이동해야 하므로 방향 벡터를 반전시킴
            Vector3 segmentVector = pathEnd - pathStart;
            _moveDirection = -segmentVector.normalized;

            Vector3 forwardDir = segmentVector.normalized;

            // 경로가 대각선으로 배치된 경우, 단순 X축 이동은 라인 간격을 좁아 보이게 만듦.
            // 이를 방지하기 위해 경로에 수직인 벡터를 계산하여 라인 오프셋으로 사용함.
            if (useZCompensatedLanes)
            {
                Vector3 geomRight = Vector3.Cross(Vector3.up, forwardDir).normalized;
                float correctionFactor = 1.0f;
            
                // X축 투영 성분이 작을수록(경사가 급할수록) 간격을 더 벌려서 보정
                if (Mathf.Abs(geomRight.x) > 0.001f) correctionFactor = 1.0f / Mathf.Abs(geomRight.x);
            
                _laneOffsetVector = Vector3.right * (laneWidth * correctionFactor);
            }
            else
            {
                // 보정 없이 물리적인 수직 벡터 사용
                Vector3 geomRight = Vector3.Cross(Vector3.up, forwardDir).normalized;
                _laneOffsetVector = geomRight * laneWidth;
            }

            // UV 1 변화당 실제 월드에서 이동해야 할 거리를 계산 (스크롤 속도 동기화용)
            float worldDistPerLoop = segmentVector.magnitude * (virtualMetersPerLoop / virtualDistStartToEnd);
            if (uvLoopSize > 0) _worldDistPerUV = worldDistPerLoop / uvLoopSize;

            SpawnObstacles(segmentVector);
        }

        /// <summary>
        /// 설정된 데이터를 기반으로 장애물들을 일괄 생성하고 초기 위치를 잡음.
        /// </summary>
        /// <param name="segmentVector">전체 경로 벡터</param>
        private void SpawnObstacles(Vector3 segmentVector)
        {
            if (obstaclePrefab == null) return;

            // 거리 1m당 이동해야 할 벡터
            Vector3 vectorPerMeter = segmentVector / virtualDistStartToEnd;

            foreach (var data in spawnData)
            {
                // 중심 경로상 위치 계산 후, 라인 오프셋을 더해 최종 위치 결정
                Vector3 centerPos = pathStart + (vectorPerMeter * data.distance);
                Vector3 finalPos = centerPos + (_laneOffsetVector * data.laneIndex);

                GameObject obj = Instantiate(obstaclePrefab, transform);
                obj.transform.position = finalPos;
            
                _spawnedObstacles.Add(obj);
            
                // 충돌 감지 컴포넌트에 플레이어 정보 주입 (누가 부딪혔는지 판별용)
                var hitChecker = obj.GetComponent<ObstacleHitChecker>();
                if (hitChecker == null) hitChecker = obj.AddComponent<ObstacleHitChecker>();
            
                hitChecker.Setup(playerIndex, data.laneIndex); 
            
                // 초기 생성 시 보이지 않게 투명 처리 (나중에 페이드인으로 등장)
                var renderers = obj.GetComponentsInChildren<Renderer>();
                foreach (var r in renderers)
                {
                    _allRenderers.Add(r);
                    SetAlpha(r, 0f); 
                }
            }
        }

        /// <summary>
        /// 매 프레임 외부(Manager)에서 호출되어 장애물을 이동시킴.
        /// </summary>
        /// <param name="uvSpeed">현재 바닥 텍스처의 스크롤 속도</param>
        public void ScrollObstacles(float uvSpeed)
        {
            if (_spawnedObstacles.Count == 0) return;

            // 바닥 텍스처 속도(UV)를 월드 이동 거리로 변환하여 동기화 유지
            float moveDistance = uvSpeed * _worldDistPerUV * Time.deltaTime;
            Vector3 displacement = _moveDirection * moveDistance;

            foreach (var obj in _spawnedObstacles)
            {
                if (obj != null)
                    obj.transform.position += displacement;
            }
        }

        /// <summary>
        /// 모든 장애물을 서서히 나타나게 함.
        /// </summary>
        public void FadeInObstacles(float duration)
        {
            StartCoroutine(FadeRoutine(_allRenderers, 0f, 1f, duration));
        }

        /// <summary>
        /// 튜토리얼 단계에 맞춰 특정 구간의 장애물만 선택적으로 나타나게 함.
        /// </summary>
        /// <param name="duration">페이드 시간</param>
        /// <param name="startIndex">리스트 내 시작 인덱스</param>
        /// <param name="count">나타날 개수</param>
        public void FadeInSpecificObstacles(float duration, int startIndex, int count)
        {
            List<Renderer> targetRenderers = new List<Renderer>();

            // 요청된 범위 내의 장애물 렌더러만 수집
            for (int i = startIndex; i < startIndex + count; i++)
            {
                if (i >= 0 && i < _spawnedObstacles.Count && _spawnedObstacles[i] != null)
                {
                    var renderers = _spawnedObstacles[i].GetComponentsInChildren<Renderer>();
                    targetRenderers.AddRange(renderers);
                }
            }

            if (targetRenderers.Count > 0)
            {
                StartCoroutine(FadeRoutine(targetRenderers, 0f, 1f, duration));
            }
        }

        /// <summary>
        /// 렌더러 리스트의 투명도를 부드럽게 조절하는 코루틴.
        /// </summary>
        private IEnumerator FadeRoutine(List<Renderer> targets, float start, float end, float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float alpha = Mathf.Lerp(start, end, elapsed / duration);
                foreach (var r in targets) SetAlpha(r, alpha);
                yield return null;
            }
            // 오차 방지를 위해 최종값으로 고정
            foreach (var r in targets) SetAlpha(r, end);
        }

        /// <summary>
        /// 재질(Material)의 투명도를 설정하는 헬퍼 메서드.
        /// </summary>
        private void SetAlpha(Renderer r, float alpha)
        {
            if (r is SpriteRenderer sr)
            {
                sr.color = new Color(sr.color.r, sr.color.g, sr.color.b, alpha);
            }
            else if (r is MeshRenderer)
            {
                foreach (Material m in r.materials)
                {
                    if (m.HasProperty("_Color"))
                    {
                        m.color = new Color(m.color.r, m.color.g, m.color.b, alpha);
                    }
                    else if (m.HasProperty("_BaseColor"))
                    {
                        Color baseColor = m.GetColor("_BaseColor");
                        m.SetColor("_BaseColor", new Color(baseColor.r, baseColor.g, baseColor.b, alpha));
                    }
                }
            }
        }
    }
}