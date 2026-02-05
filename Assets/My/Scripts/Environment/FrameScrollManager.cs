using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 가상의 경로(Start -> End)를 정의하고, 텍스처 스크롤 속도에 맞춰
/// 프레임(오브젝트)들을 이동 및 순환(Pooling)시키는 클래스입니다.
/// </summary>
public class FrameScrollManager : MonoBehaviour
{
    [Header("Prefab Settings")]
    [SerializeField, Tooltip("생성할 프레임 프리팹")] 
    private GameObject framePrefab; 
    
    [SerializeField, Tooltip("화면에 끊김 없이 보일 수 있도록 생성할 프레임 개수")] 
    private int poolSize = 6; 

    [Header("Path Settings (Virtual 10m Segment)")]
    [Tooltip("가상 경로의 시작점 (0m 지점)")]
    public Vector3 pathStart = new Vector3(23.278f, 0.28f, 17.57f);

    [Tooltip("가상 경로의 끝점 (10m 지점, 이 벡터가 배치 간격이 됨)")]
    public Vector3 pathEnd = new Vector3(43.27f, 0.28f, 32.62f);

    [Tooltip("생성될 프레임의 고정 회전값")]
    public Vector3 fixedRotation = new Vector3(0f, -0.042f, 0f);

    [Tooltip("생성될 프레임의 고정 크기")]
    public Vector3 fixedScale = new Vector3(3.27f, 3.27f, 3.27f);

    [Header("Sync Settings")]
    [SerializeField, Tooltip("텍스처 UV가 1회 반복되는 단위 크기")] 
    private float uvLoopSize = 0.025f;

    [SerializeField, Tooltip("텍스처 1회 반복 시 실제 이동하는 것으로 간주할 가상 거리")] 
    private float virtualMetersPerLoop = 5f;

    [SerializeField, Tooltip("PathStart와 PathEnd 사이의 가상 거리 정의")] 
    private float virtualDistStartToEnd = 10f;

    [Header("Reset Settings")]
    [SerializeField, Tooltip("시작점보다 얼마나 더 뒤로 이동하면 리셋할지 설정 (가상 미터 단위)")] 
    private float resetVirtualDistance = -5f; 

    private List<Transform> _frames = new List<Transform>();
    private Vector3 _segmentVector; // Start에서 End로 향하는 벡터 (배치 간격)
    private Vector3 _moveDirection; // 실제 프레임이 이동할 방향 (End -> Start)
    private float _worldDistPerUV;  // UV 1당 이동해야 할 월드 거리 비율

    private void Start()
    {
        InitializePathAndRatio();
        CreateAndPlaceFrames();
    }

    /// <summary>
    /// 경로 벡터와 UV 대 월드 이동 비율을 초기화합니다.
    /// </summary>
    private void InitializePathAndRatio()
    {
        // 1. 기준 벡터 계산 (Start -> End)
        _segmentVector = pathEnd - pathStart;
        
        // 2. 이동 방향 설정 (플레이어 진행 방향의 반대인 End -> Start로 설정)
        _moveDirection = -_segmentVector.normalized;

        if (virtualDistStartToEnd <= 0f || uvLoopSize <= 0f)
        {
            Debug.LogError("[FrameScrollManager] 거리 설정값은 0보다 커야 합니다.");
            enabled = false;
            return;
        }

        // 3. 동기화 비율 계산
        // 가상 10m 구간(_segmentVector 길이) 내에서, 가상 5m가 차지하는 실제 월드 길이 계산
        float worldDistPerLoop = _segmentVector.magnitude * (virtualMetersPerLoop / virtualDistStartToEnd);
        
        // UV 1 변화량 당 이동해야 할 월드 거리 산출
        _worldDistPerUV = worldDistPerLoop / uvLoopSize;
    }

    /// <summary>
    /// 설정된 개수만큼 프레임을 생성하고 초기 위치에 배치합니다.
    /// </summary>
    private void CreateAndPlaceFrames()
    {
        for (int i = 0; i < poolSize; i++)
        {
            if (framePrefab != null)
            {
                GameObject obj = Instantiate(framePrefab, transform);
                
                // 시작점에서 벡터 방향으로 i번째 간격만큼 떨어진 위치에 배치
                obj.transform.position = pathStart + (_segmentVector * i);
                
                // 회전 및 크기 적용
                obj.transform.eulerAngles = fixedRotation;
                obj.transform.localScale = fixedScale;
                
                _frames.Add(obj.transform);
            }
        }
    }

    /// <summary>
    /// 외부 매니저(PlayTutorialManager)에서 호출하여 프레임을 이동시킵니다.
    /// </summary>
    /// <param name="uvSpeed">현재 바닥 텍스처의 UV 스크롤 속도</param>
    public void ScrollFrames(float uvSpeed)
    {
        if (!enabled || _frames.Count == 0) return;

        // 1. 이동 거리 계산 (UV 속도 -> 월드 거리 변환)
        float moveDistance = uvSpeed * _worldDistPerUV * Time.deltaTime;
        
        // 2. 경로 방향으로 이동 벡터 산출
        Vector3 displacement = _moveDirection * moveDistance;

        // 3. 리셋 처리를 위한 기준값 계산
        Vector3 forwardDir = _segmentVector.normalized; 
        float worldPerVirtualMeter = _segmentVector.magnitude / virtualDistStartToEnd;
        float resetWorldDist = resetVirtualDistance * worldPerVirtualMeter; 

        // 트랙 전체 길이 (리셋 시 맨 뒤로 보낼 거리)
        Vector3 totalTrackOffset = _segmentVector * _frames.Count;

        for (int i = 0; i < _frames.Count; i++)
        {
            Transform frame = _frames[i];

            // 프레임 이동 적용
            frame.position += displacement;

            // 리셋 조건 확인: Start 지점을 기준으로 투영(Projection)하여 거리 측정
            // 결과값이 음수이면 Start 지점보다 뒤(카메라 쪽)에 있다는 의미
            float distFromStart = Vector3.Dot(frame.position - pathStart, forwardDir);

            // 설정한 리셋 거리보다 뒤로 이동했다면 순환 처리
            if (distFromStart < resetWorldDist)
            {
                // 현재 위치에서 전체 트랙 길이만큼 앞으로 이동시켜 맨 뒤에 붙임
                frame.position += totalTrackOffset;
            }
        }
    }
}