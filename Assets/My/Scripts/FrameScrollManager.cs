using UnityEngine;
using System.Collections.Generic;

public class FrameScrollManager : MonoBehaviour
{
    [Header("Prefab Settings")]
    [SerializeField] private GameObject framePrefab; 
    [SerializeField] private int poolSize = 6; // 화면에 꽉 차게 넉넉히

    [Header("Path Settings (Virtual 10m Segment)")]
    [Tooltip("가상 0m 지점 (시작점)")]
    public Vector3 pathStart = new Vector3(23.278f, 0.28f, 17.57f);

    [Tooltip("가상 10m 지점 (이 벡터가 10m 간격이 됨)")]
    public Vector3 pathEnd = new Vector3(43.27f, 0.28f, 32.62f);

    [Tooltip("프레임 고정 회전값")]
    public Vector3 fixedRotation = new Vector3(0f, -0.042f, 0f);

    [Tooltip("프레임 고정 크기")]
    public Vector3 fixedScale = new Vector3(3.27f, 3.27f, 3.27f);

    [Header("Sync Settings")]
    [Tooltip("텍스처 UV 1회 반복 단위 (0.025)")]
    [SerializeField] private float uvLoopSize = 0.025f;

    [Tooltip("텍스처 1회 반복이 차지하는 가상 거리 (5m)")]
    [SerializeField] private float virtualMetersPerLoop = 5f;

    [Tooltip("Start-End 구간의 가상 거리 정의 (10m)")]
    [SerializeField] private float virtualDistStartToEnd = 10f;

    [Header("Reset Settings")]
    [Tooltip("시작점보다 얼마나 더 뒤로 가면 리셋할지 (가상 미터 단위)")]
    [SerializeField] private float resetVirtualDistance = -5f; 

    private List<Transform> _frames = new List<Transform>();
    private Vector3 _segmentVector; // Start -> End 벡터 (World)
    private Vector3 _moveDirection; // 이동 방향 (Normalized, End -> Start)
    private float _worldDistPerUV;  // UV 1당 이동할 World 거리

    private void Start()
    {
        // 1. 기준 벡터 계산 (Start -> End)
        // 이 벡터의 길이가 "가상 10m"의 월드 길이입니다.
        _segmentVector = pathEnd - pathStart;
        
        // 2. 이동 방향 (End -> Start 로 다가옴)
        // 플레이어가 앞으로 달리면 바닥(오브젝트)은 뒤로 옵니다.
        _moveDirection = -_segmentVector.normalized;

        // 3. 비율 계산 (Sync)
        // 텍스처 1 Loop (0.025 UV) = 가상 5m
        // Start->End 벡터 = 가상 10m
        // 즉, "가상 5m"의 월드 길이 = _segmentVector 길이의 절반 (5/10)
        float worldDistPerLoop = _segmentVector.magnitude * (virtualMetersPerLoop / virtualDistStartToEnd);
        
        if (uvLoopSize > 0)
        {
            // UV 1 당 World 이동 거리
            _worldDistPerUV = worldDistPerLoop / uvLoopSize;
        }

        // 4. 프레임 생성 및 배치
        // "10m 마다 생성" -> Start-End 벡터가 가상 10m이므로, 배치 간격은 벡터 그 자체입니다.
        for (int i = 0; i < poolSize; i++)
        {
            if (framePrefab != null)
            {
                GameObject obj = Instantiate(framePrefab, transform);
                
                // 위치: Start에서 벡터만큼 i번 더한 위치 (0m, 10m, 20m...)
                obj.transform.position = pathStart + (_segmentVector * i);
                
                // 회전 & 크기
                obj.transform.eulerAngles = fixedRotation;
                obj.transform.localScale = fixedScale;
                
                _frames.Add(obj.transform);
            }
        }
    }

    /// <summary>
    /// PlayTutorialManager에서 UV 속도를 받아 프레임 이동
    /// </summary>
    public void ScrollFrames(float uvSpeed)
    {
        if (_frames.Count == 0) return;

        // 1. 이번 프레임 이동 거리 (World)
        // uvSpeed가 양수일 때 뒤로(카메라 쪽으로) 와야 함
        float moveDistance = uvSpeed * _worldDistPerUV * Time.deltaTime;
        
        // 경로를 따라 이동하는 벡터 (단순 Z이동 아님)
        Vector3 displacement = _moveDirection * moveDistance;

        // 2. 리셋 기준 계산
        Vector3 forwardDir = _segmentVector.normalized; 
        float segmentLength = _segmentVector.magnitude; 
        float worldPerVirtualMeter = segmentLength / virtualDistStartToEnd;
        float resetWorldDist = resetVirtualDistance * worldPerVirtualMeter; 

        // 전체 트랙 길이 (리셋 시 보낼 거리: 풀 크기 * 10m벡터)
        Vector3 totalTrackOffset = _segmentVector * _frames.Count;

        for (int i = 0; i < _frames.Count; i++)
        {
            Transform frame = _frames[i];

            // 이동 적용
            frame.position += displacement;

            // 리셋 체크: Start 지점을 기준으로 투영 (Project)
            // 결과가 음수이면 Start보다 뒤(카메라 뒤쪽)에 있는 것
            float distFromStart = Vector3.Dot(frame.position - pathStart, forwardDir);

            // 리셋 지점보다 더 뒤로 갔으면
            if (distFromStart < resetWorldDist)
            {
                // 맨 뒤로 이동 (현재 위치 + 전체 길이)
                // 이렇게 하면 경로 선상에서 정확히 맨 뒤로 이동합니다.
                frame.position += totalTrackOffset;
            }
        }
    }
}