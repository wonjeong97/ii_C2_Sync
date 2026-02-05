using UnityEngine;

/// <summary>
/// 메쉬의 UV 좌표를 실시간으로 수정하여 텍스처 스크롤, 회전, 스케일링을 처리하는 클래스입니다.
/// 특정 UV 구간 내에서만 반복되는 커스텀 루프 기능을 지원합니다.
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

    [Header("Loop Settings (Custom)")]
    [Tooltip("설정된 Min/Max 구간 내에서만 UV를 반복할지 여부")]
    public bool useCustomLoop = true;    
    
    [Tooltip("루프 시작 지점 (최소 Y값)")]
    public float loopMinY = 0.0022f;     
    
    [Tooltip("루프 종료 지점 (최대 Y값)")]
    public float loopMaxY = 0.0272f;     

    [Header("Pivot")]
    [Tooltip("회전 및 스케일링의 기준점")]
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
            // 인스턴스가 아닌 원본 메쉬를 참조로 저장
            if (originalMesh == null && _mf.sharedMesh != null && !_mf.sharedMesh.name.Contains("Instance"))
            {
                originalMesh = _mf.sharedMesh;
            }

            // 런타임 수정을 위한 메쉬 복제 (원본 보존)
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
        // [에디터 모드] 트랜스폼 등 변경 사항이 있을 때만 UV 갱신
        if (!Application.isPlaying)
        {
            if (transform.hasChanged)
            {
                UpdateUVs();
                transform.hasChanged = false;
            }
            return;
        }

        // [플레이 모드] 실시간 스크롤 로직
        if (Application.isPlaying)
        {
            if (Input.GetKeyDown(debugKey)) enableScroll = !enableScroll;

            if (enableScroll)
            {
                // Y축 오프셋 이동
                offset.y += scrollSpeedY * Time.deltaTime;
                
                // 커스텀 루프 처리: UV가 설정된 범위를 벗어나면 순환시킴
                if (useCustomLoop)
                {
                    // 값이 감소하여 Min보다 작아지면 Max 위치로 이동
                    if (offset.y < loopMinY)
                    {
                        float diff = loopMinY - offset.y;
                        offset.y = loopMaxY - diff;
                    }
                    // 값이 증가하여 Max보다 커지면 Min 위치로 이동
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

    // 인스펙터 값 변경 시 즉시 반영
    void OnValidate()
    {
        // Min이 Max보다 클 경우 스왑하여 오류 방지
        if (loopMaxY < loopMinY)
        {
            (loopMinY, loopMaxY) = (loopMaxY, loopMinY);
        }
        UpdateUVs();
    }

    /// <summary>
    /// 설정된 Pivot, Scale, Rotation, Offset 값을 기반으로 메쉬의 UV를 다시 계산합니다.
    /// </summary>
    public void UpdateUVs()
    {
        if (_mesh == null || _originalUVs == null || _originalUVs.Length == 0) return;

        Vector2[] newUVs = new Vector2[_originalUVs.Length];
        
        // 회전 계산을 위한 삼각함수 값 미리 계산
        float rad = rotation * Mathf.Deg2Rad;
        float cos = Mathf.Cos(rad);
        float sin = Mathf.Sin(rad);

        for (int i = 0; i < _originalUVs.Length; i++)
        {
            Vector2 uv = _originalUVs[i];
            
            // 1. Pivot 기준으로 좌표 이동
            uv -= pivot;
            
            // 2. 스케일 적용
            uv.x *= scale.x;
            uv.y *= scale.y;
            
            // 3. 회전 적용 (행렬 연산)
            float xNew = uv.x * cos - uv.y * sin;
            float yNew = uv.x * sin + uv.y * cos;
            uv.x = xNew;
            uv.y = yNew;

            // 4. Pivot 복구 및 오프셋(스크롤) 적용
            uv += pivot;
            uv += offset;
            
            newUVs[i] = uv;
        }
        
        // 변경된 UV를 메쉬에 할당
        _mesh.uv = newUVs;
    }
}