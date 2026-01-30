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

    private MeshFilter _mf;
    private Mesh _mesh; // 복제된 메쉬 (변형용)
    private Vector2[] _originalUVs;

    void OnEnable()
    {
        _mf = GetComponent<MeshFilter>();
        if (_mf != null)
        {
            // 1. 원본 메쉬가 지정되지 않았다면, 현재 메쉬를 원본으로 저장
            // (이미 복제된 메쉬라면 이름으로 구분하거나, 최초 1회만 저장)
            if (originalMesh == null && _mf.sharedMesh != null)
            {
                // 이름에 Instance가 포함되어 있지 않다면 원본으로 간주
                if (!_mf.sharedMesh.name.Contains("Instance"))
                {
                    originalMesh = _mf.sharedMesh;
                }
            }

            // 2. 원본 메쉬를 기반으로 복제본(Instance) 생성
            if (originalMesh != null)
            {
                // .mesh 프로퍼티 접근 경고 해결 -> Instantiate 사용
                _mesh = Instantiate(originalMesh);
                _mesh.name = originalMesh.name + " (Instance)";
                _mesh.hideFlags = HideFlags.DontSave; // 씬 저장 시 메쉬 데이터 제외

                _mf.sharedMesh = _mesh;

                // 원본 UV 가져오기
                _originalUVs = originalMesh.uv;
            }
        }
        UpdateUVs();
    }

    void Update()
    {
        if (!Application.isPlaying || transform.hasChanged)
        {
            UpdateUVs();
            transform.hasChanged = false;
        }
    }

    void OnValidate()
    {
        UpdateUVs();
    }

    void UpdateUVs()
    {
        if (_mesh == null || _originalUVs == null || _originalUVs.Length == 0) return;

        Vector2[] newUVs = new Vector2[_originalUVs.Length];

        float rad = rotation * Mathf.Deg2Rad;
        float cos = Mathf.Cos(rad);
        float sin = Mathf.Sin(rad);

        for (int i = 0; i < _originalUVs.Length; i++)
        {
            Vector2 uv = _originalUVs[i];

            // 1. Pivot 이동
            uv -= pivot;

            // 2. Scale
            uv.x *= scale.x;
            uv.y *= scale.y;

            // 3. Rotation
            float xNew = uv.x * cos - uv.y * sin;
            float yNew = uv.x * sin + uv.y * cos;
            uv.x = xNew;
            uv.y = yNew;

            // 4. 복귀 및 Offset
            uv += pivot;
            uv += offset;

            newUVs[i] = uv;
        }

        _mesh.uv = newUVs;
    }
}