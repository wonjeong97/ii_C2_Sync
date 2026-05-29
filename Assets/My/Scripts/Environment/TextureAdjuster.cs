using Microsoft.Extensions.Logging;
using UnityEngine;
using VContainer;

namespace My.Scripts.Environment
{
    /// <summary>
    /// 메쉬의 UV 좌표를 실시간으로 수정하여 텍스처 스크롤, 회전, 스케일링을 처리하는 클래스.
    /// 메모리 할당 최적화(GC Zero) 및 VContainer 의존성 주입을 지원함.
    /// </summary>
    [ExecuteInEditMode]
    [RequireComponent(typeof(MeshFilter))]
    public class TextureAdjuster : MonoBehaviour
    {
        [Header("Texture Settings")]
        [Range(-360, 360), Tooltip("UV 회전 각도")] 
        public float rotation = -0.684f; 
        
        [Tooltip("UV 스케일")]
        public Vector2 scale = new Vector2(1, 1);
        
        [Tooltip("UV 오프셋 (스크롤 위치)")]
        public Vector2 offset = new Vector2(0, 0.0022f);    

        [Header("Loop Settings")]
        public bool useCustomLoop = true;    
        public float loopMinY = 0.0022f;     
        public float loopMaxY = 0.0272f;     

        [Header("Pivot")]
        public Vector2 pivot = new Vector2(0.5f, 0.5f);

        [SerializeField] private Mesh originalMesh;

        [Header("Runtime Debug")]
        public bool enableScroll = false;
        public float scrollSpeedY = 0.0f;
        public KeyCode debugKey = KeyCode.T;

        private MeshFilter _mf;
        private Mesh _mesh; 
        private Vector2[] _originalUVs;
        private Vector2[] _newUVs; // 할당 방지를 위한 캐시 버퍼

        private ILogger<TextureAdjuster> _logger;

        [Inject]
        public void Construct(ILogger<TextureAdjuster> logger)
        {
            _logger = logger;
        }

        private void OnEnable()
        {
            _mf = GetComponent<MeshFilter>();
            if (!_mf) return;

            // 메쉬 인스턴스 초기화
            if (!originalMesh && _mf.sharedMesh && !_mf.sharedMesh.name.Contains("Instance"))
            {
                originalMesh = _mf.sharedMesh;
            }

            if (originalMesh)
            {
                _mesh = Instantiate(originalMesh);
                _mesh.name = $"{originalMesh.name} (Instance)";
                _mesh.hideFlags = HideFlags.DontSave; 
                _mf.sharedMesh = _mesh;
                
                _originalUVs = originalMesh.uv;
                _newUVs = new Vector2[_originalUVs.Length]; // 버퍼 사전 할당
            }
            UpdateUVs();
        }

        private void OnDestroy()
        {
            if (_mesh) DestroyImmediate(_mesh);
        }

        private void Update()
        {
            if (!Application.isPlaying)
            {
                if (transform.hasChanged)
                {
                    UpdateUVs();
                    transform.hasChanged = false;
                }
                return;
            }

            if (enableScroll)
            {
                if (Input.GetKeyDown(debugKey)) enableScroll = !enableScroll;

                offset.y += scrollSpeedY * Time.deltaTime;
                
                if (useCustomLoop)
                {
                    if (offset.y < loopMinY) offset.y = loopMaxY - (loopMinY - offset.y);
                    else if (offset.y > loopMaxY) offset.y = loopMinY + (offset.y - loopMaxY);
                }
                UpdateUVs();
            }
        }

        private void OnValidate()
        {
            if (loopMaxY < loopMinY) (loopMinY, loopMaxY) = (loopMaxY, loopMinY);
            UpdateUVs();
        }

        /// <summary>
        /// 캐싱된 버퍼를 활용하여 GC 할당 없이 UV를 계산하고 적용합니다.
        /// </summary>
        public void UpdateUVs()
        {
            if (!_mesh || _originalUVs == null) return;

            float rad = rotation * Mathf.Deg2Rad;
            float cos = Mathf.Cos(rad);
            float sin = Mathf.Sin(rad);

            for (int i = 0; i < _originalUVs.Length; i++)
            {
                // Pivot 기준으로 좌표 이동
                Vector2 uv = _originalUVs[i] - pivot;
                
                // 스케일 및 회전 연산
                float x = (uv.x * scale.x);
                float y = (uv.y * scale.y);
                
                // 회전 적용
                float xNew = (x * cos - y * sin);
                float yNew = (x * sin + y * cos);

                // 결과 적용 (Pivot 복구 + 오프셋)
                _newUVs[i] = new Vector2(xNew, yNew) + pivot + offset;
            }
            
            _mesh.uv = _newUVs;
        }
    }
}