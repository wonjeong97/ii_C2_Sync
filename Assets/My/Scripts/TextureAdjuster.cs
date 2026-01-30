using UnityEngine;

[ExecuteInEditMode]
[RequireComponent(typeof(MeshFilter))]
public class TextureAdjuster : MonoBehaviour
{
    [Header("Texture Settings")]
    [Range(-360, 360)] public float rotation = 0f; // 회전
    public Vector2 scale = new Vector2(1, 1);      // 크기/반복 (Tiling)
    public Vector2 offset = new Vector2(0, 0);     // 위치 (Offset)

    [Header("Pivot")]
    public Vector2 pivot = new Vector2(0.5f, 0.5f); // 회전 중심

    [Header("Original Reference")]
    [SerializeField] private Mesh originalMesh; // 원본 메쉬 저장용

    [Header("Runtime Debug (Scroll)")]
    public bool enableScroll = false;       // 스크롤 활성화 여부
    public float scrollSpeedY = 0.5f;       // 스크롤 속도
    public KeyCode debugKey = KeyCode.T;    // 켜고 끌 키 (기본: T)

    private MeshFilter _mf;
    private Mesh _mesh; 
    private Vector2[] _originalUVs;

    void OnEnable()
    {
        _mf = GetComponent<MeshFilter>();
        if (_mf != null)
        {
            // 1. 원본 메쉬가 지정되지 않았다면, 현재 메쉬를 원본으로 저장
            if (originalMesh == null && _mf.sharedMesh != null)
            {
                if (!_mf.sharedMesh.name.Contains("Instance"))
                {
                    originalMesh = _mf.sharedMesh;
                }
            }

            // 2. 원본 메쉬를 기반으로 복제본(Instance) 생성
            if (originalMesh != null)
            {
                // 이미 인스턴스인지 확인 후 생성
                if (_mesh == null)
                {
                    _mesh = Instantiate(originalMesh);
                    _mesh.name = originalMesh.name + " (Instance)";
                    _mesh.hideFlags = HideFlags.DontSave; 
                }
                
                _mf.sharedMesh = _mesh;
                _originalUVs = originalMesh.uv;
            }
        }
        UpdateUVs();
    }

    void Update()
    {
        // [에디터 모드] 오브젝트를 움직이거나 값이 바뀔 때만 갱신
        if (!Application.isPlaying)
        {
            if (transform.hasChanged)
            {
                UpdateUVs();
                transform.hasChanged = false;
            }
            return;
        }

        // [런타임 모드] 디버그 키 입력 및 스크롤 처리
        if (Application.isPlaying)
        {
            // 키 입력 시 토글
            if (Input.GetKeyDown(debugKey))
            {
                enableScroll = !enableScroll;
                Debug.Log($"[TextureAdjuster] Auto Scroll: {enableScroll}");
            }

            // 스크롤 활성화 시 Y 오프셋 계속 증가
            if (enableScroll)
            {
                offset.y += scrollSpeedY * Time.deltaTime;
                
                // 오프셋이 너무 커지지 않게 0~1 사이(또는 반복)로 관리하고 싶다면 아래 주석 해제
                // offset.y %= 1.0f; 

                UpdateUVs(); // 매 프레임 UV 갱신
            }
            else
            {
                // 스크롤은 안 하지만 인스펙터에서 값을 바꿨을 때를 위해 체크
                // (매 프레임 호출이 부담스럽다면 이 부분은 제거 가능)
                UpdateUVs(); 
            }
        }
    }

    // 인스펙터 값 변경 시 즉시 반영
    void OnValidate()
    {
        UpdateUVs();
    }

    public void UpdateUVs()
    {
        if (_mesh == null || _originalUVs == null || _originalUVs.Length == 0) return;

        Vector2[] newUVs = new Vector2[_originalUVs.Length];

        float rad = rotation * Mathf.Deg2Rad;
        float cos = Mathf.Cos(rad);
        float sin = Mathf.Sin(rad);

        for (int i = 0; i < _originalUVs.Length; i++)
        {
            Vector2 uv = _originalUVs[i];

            // 1. Pivot
            uv -= pivot;

            // 2. Scale
            uv.x *= scale.x;
            uv.y *= scale.y;

            // 3. Rotation
            float xNew = uv.x * cos - uv.y * sin;
            float yNew = uv.x * sin + uv.y * cos;
            uv.x = xNew;
            uv.y = yNew;

            // 4. Offset
            uv += pivot;
            uv += offset;

            newUVs[i] = uv;
        }

        _mesh.uv = newUVs;
    }
}