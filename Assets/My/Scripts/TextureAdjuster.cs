using UnityEngine;

[ExecuteInEditMode]
[RequireComponent(typeof(MeshFilter))]
public class TextureAdjuster : MonoBehaviour
{
    [Header("Texture Settings")]
    [Range(-360, 360)] public float rotation = -0.684f; // 기본 회전값
    public Vector2 scale = new Vector2(1, 1);
    public Vector2 offset = new Vector2(0, 0.0022f);    // 시작 오프셋

    [Header("Loop Settings (Custom)")]
    public bool useCustomLoop = true;    // 커스텀 루프 활성화
    public float loopMinY = 0.0022f;     // 루프 시작점 (최소값)
    public float loopMaxY = 0.0272f;     // 루프 끝점 (최대값, 계산된 0.0272 적용)

    [Header("Pivot")]
    public Vector2 pivot = new Vector2(0.5f, 0.5f);

    [Header("Original Reference")]
    [SerializeField] private Mesh originalMesh;

    [Header("Runtime Debug")]
    public bool enableScroll = false;
    public float scrollSpeedY = 0.0f;
    public KeyCode debugKey = KeyCode.T;

    private MeshFilter _mf;
    private Mesh _mesh; 
    private Vector2[] _originalUVs;

    void OnEnable()
    {
        _mf = GetComponent<MeshFilter>();
        if (_mf != null)
        {
            // 원본 메쉬 확보 (Instance가 아닌 원본)
            if (originalMesh == null && _mf.sharedMesh != null && !_mf.sharedMesh.name.Contains("Instance"))
            {
                originalMesh = _mf.sharedMesh;
            }

            // 런타임용 복제본 생성
            if (originalMesh != null)
            {
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
        // [에디터] 변경사항 즉시 반영
        if (!Application.isPlaying)
        {
            if (transform.hasChanged)
            {
                UpdateUVs();
                transform.hasChanged = false;
            }
            return;
        }

        // [런타임] 스크롤 로직
        if (Application.isPlaying)
        {
            if (Input.GetKeyDown(debugKey)) enableScroll = !enableScroll;

            if (enableScroll)
            {
                // 오프셋 이동
                offset.y += scrollSpeedY * Time.deltaTime;
                
                // ★ 커스텀 루프 (자연스러운 연결)
                if (useCustomLoop)
                {
                    // [상황 1] 값이 줄어들며 달릴 때 (Y 감소)
                    // 최소값보다 작아지면 -> 최대값 근처로 보냄
                    if (offset.y < loopMinY)
                    {
                        float diff = loopMinY - offset.y;
                        offset.y = loopMaxY - diff;
                    }
                    // [상황 2] 값이 늘어나며 달릴 때 (혹시 모를 대비)
                    // 최대값보다 커지면 -> 최소값 근처로 보냄
                    else if (offset.y > loopMaxY)
                    {
                        float diff = offset.y - loopMaxY;
                        offset.y = loopMinY + diff;
                    }
                }

                UpdateUVs();
            }
        }
    }

    void OnValidate() => UpdateUVs();

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
            
            // 1. Pivot 기준 이동
            uv -= pivot;
            
            // 2. Scale
            uv.x *= scale.x;
            uv.y *= scale.y;
            
            // 3. Rotation
            float xNew = uv.x * cos - uv.y * sin;
            float yNew = uv.x * sin + uv.y * cos;
            uv.x = xNew;
            uv.y = yNew;

            // 4. Pivot 복구 및 Offset 적용
            uv += pivot;
            uv += offset;
            
            newUVs[i] = uv;
        }
        _mesh.uv = newUVs;
    }
}