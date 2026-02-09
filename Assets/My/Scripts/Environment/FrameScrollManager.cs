using UnityEngine;
using System.Collections.Generic;
using My.Scripts.Environment;

public class FrameScrollManager : MonoBehaviour
{   
    [Header("Prefab Settings")]
    [SerializeField] private GameObject framePrefab; 
    [SerializeField] private int poolSize = 6; 

    [Header("Path Settings (Virtual 10m Segment)")]
    public Vector3 pathStart = new Vector3(23.278f, 0.28f, 17.57f);
    public Vector3 pathEnd = new Vector3(43.27f, 0.28f, 32.62f);
    public Vector3 fixedRotation = new Vector3(0f, -0.042f, 0f);
    public Vector3 fixedScale = new Vector3(3.27f, 3.27f, 3.27f);

    [Header("Sync Settings")]
    [SerializeField] private float uvLoopSize = 0.025f;
    [SerializeField] private float virtualMetersPerLoop = 5f;
    [SerializeField] private float virtualDistStartToEnd = 10f;

    [Header("Reset Settings")]
    [SerializeField] private float resetVirtualDistance = -5f; 
    [SerializeField] private float finishDistance = 150f;

    [Header("Options")]
    public bool showDistanceLabel = true;

    private class FrameData
    {
        public Transform transform;
        public FrameDistanceLabel label;
        public float currentMeters;
    }

    private List<FrameData> _frames = new List<FrameData>();
    private Vector3 _segmentVector; 
    private Vector3 _moveDirection; 
    private float _worldDistPerUV;  

    private void Start()
    {
        InitializePathAndRatio();
        CreateAndPlaceFrames();
    }

    private void InitializePathAndRatio()
    {
        _segmentVector = pathEnd - pathStart;
        _moveDirection = -_segmentVector.normalized;

        if (virtualDistStartToEnd <= 0f || uvLoopSize <= 0f)
        {
            enabled = false;
            return;
        }

        float worldDistPerLoop = _segmentVector.magnitude * (virtualMetersPerLoop / virtualDistStartToEnd);
        _worldDistPerUV = worldDistPerLoop / uvLoopSize;
    }

    private void CreateAndPlaceFrames()
    {
        for (int i = 0; i < poolSize; i++)
        {
            if (framePrefab != null)
            {
                GameObject obj = Instantiate(framePrefab, transform);
                
                obj.transform.position = pathStart + (_segmentVector * i);
                obj.transform.eulerAngles = fixedRotation;
                obj.transform.localScale = fixedScale;

                FrameData data = new FrameData();
                data.transform = obj.transform;
                data.label = obj.GetComponent<FrameDistanceLabel>();
                
                data.currentMeters = (i + 1) * virtualDistStartToEnd;
                _frames.Add(data);
                UpdateFrameLabel(data);
            }
        }
    }

    public void ScrollFrames(float uvSpeed)
    {
        if (!enabled || _frames.Count == 0) return;

        float moveDistance = uvSpeed * _worldDistPerUV * Time.deltaTime;
        Vector3 displacement = _moveDirection * moveDistance;

        Vector3 forwardDir = _segmentVector.normalized; 
        float worldPerVirtualMeter = _segmentVector.magnitude / virtualDistStartToEnd;
        float resetWorldDist = resetVirtualDistance * worldPerVirtualMeter; 

        Vector3 totalTrackOffset = _segmentVector * _frames.Count;
        float totalVirtualDistance = virtualDistStartToEnd * _frames.Count;

        for (int i = 0; i < _frames.Count; i++)
        {
            FrameData frameData = _frames[i];

            frameData.transform.position += displacement;

            float distFromStart = Vector3.Dot(frameData.transform.position - pathStart, forwardDir);

            if (distFromStart < resetWorldDist)
            {
                float nextMeters = frameData.currentMeters + totalVirtualDistance;

                if (nextMeters <= finishDistance)
                {
                    frameData.transform.position += totalTrackOffset;
                    frameData.currentMeters = nextMeters;
                    UpdateFrameLabel(frameData);
                }
            }
        }
    }

    private void UpdateFrameLabel(FrameData data)
    {
        // 데이터나 라벨이 없으면 리턴
        if (data == null || data.label == null) return;

        // 프레임 전체(gameObject)를 끄지 않고, 텍스트만 제어하는 메서드 호출
        data.label.SetLabelActive(showDistanceLabel);

        // 라벨을 안 보여줄 거면 텍스트 갱신 로직도 스킵
        if (!showDistanceLabel) return;

        // 텍스트 내용 갱신
        if (data.currentMeters >= finishDistance - 0.1f)
        {
            data.label.SetText("FINISH");
        }
        else
        {
            data.label.SetDistance(data.currentMeters);
        }
    }

    /// <summary>
    /// 카메라와 가장 가까운 프레임을 찾아 강제로 트랙 맨 뒤로 이동시킵니다.
    /// (Why: 플레이어 정지 시 카메라 바로 앞의 구조물이 시야를 가리는 것을 방지하기 위함)
    /// </summary>
    /// <param name="cameraTransform">기준이 될 카메라의 Transform (보통 Camera.main.transform)</param>
    public void ForceRecycleFrameClosestToCamera(Transform cameraTransform)
    {
        if (_frames.Count == 0 || cameraTransform == null) return;

        Vector3 camPos = cameraTransform.position;
        FrameData closestFrame = null;
        float minSqrDist = float.MaxValue;

        // 1. 카메라 위치와 가장 가까운 프레임 탐색
        foreach (var frame in _frames)
        {
            // 최적화: 거리 비교 시 sqrt 연산을 피하기 위해 sqrMagnitude 사용
            float sqrDist = (frame.transform.position - camPos).sqrMagnitude;

            if (sqrDist < minSqrDist)
            {
                minSqrDist = sqrDist;
                closestFrame = frame;
            }
        }

        // 2. 해당 프레임을 트랙의 맨 뒤로 이동
        // (FrameData는 일반 클래스이므로 명시적 null 체크 필요)
        if (closestFrame != null)
        {
            Vector3 totalTrackOffset = _segmentVector * _frames.Count;
            float totalVirtualDistance = virtualDistStartToEnd * _frames.Count;

            // 다음 거리(미터) 계산
            float nextMeters = closestFrame.currentMeters + totalVirtualDistance;

            // 위치 이동 및 데이터 갱신
            closestFrame.transform.position += totalTrackOffset;
            closestFrame.currentMeters = nextMeters;
        
            // UI 라벨 갱신 (피니시 라인 통과 여부 등 내부 로직 처리)
            UpdateFrameLabel(closestFrame);
        }
    }
}