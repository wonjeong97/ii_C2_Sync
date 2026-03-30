using UnityEngine;
using System.Collections.Generic;
using My.Scripts.Environment;

/// <summary>
/// 달리기 트랙의 거리 표시 프레임 구조물들을 생성하고, 플레이어 속도에 맞춰 무한 순환(Recycling)시키는 매니저 클래스.
/// </summary>
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
    [SerializeField] private float finishDistance = 200f;

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

    /// <summary>
    /// 객체 시작 시 경로 계산 및 초기 프레임 배치를 수행함.
    /// </summary>
    private void Start()
    {
        InitializePathAndRatio();
        CreateAndPlaceFrames();
    }

    /// <summary>
    /// 트랙의 방향 벡터와 UV 속도 대비 실제 이동 거리 비율을 계산함.
    /// </summary>
    private void InitializePathAndRatio()
    {
        _segmentVector = pathEnd - pathStart;
        _moveDirection = -_segmentVector.normalized;

        if (virtualDistStartToEnd <= 0f || uvLoopSize <= 0f)
        {
            Debug.LogWarning("잘못된 Path 또는 Sync 설정값이 입력됨.");
            enabled = false;
            return;
        }

        // 예시 입력: segmentVector.magnitude(12.5) * (5 / 10) -> 결과값 = 6.25 (루프당 실제 월드 거리)
        float worldDistPerLoop = _segmentVector.magnitude * (virtualMetersPerLoop / virtualDistStartToEnd);
        
        // 예시 입력: 6.25 / 0.025 -> 결과값 = 250 (UV 1단위당 실제 월드 거리)
        _worldDistPerUV = worldDistPerLoop / uvLoopSize;
    }

    /// <summary>
    /// 설정된 풀 사이즈만큼 프레임을 생성하고 트랙 경로를 따라 초기 위치에 배치함.
    /// </summary>
    private void CreateAndPlaceFrames()
    {
        for (int i = 0; i < poolSize; i++)
        {
            if (framePrefab)
            {
                GameObject obj = Instantiate(framePrefab, transform);
                
                // 이유: 시작 위치로부터 트랙 방향 벡터만큼 순차적으로 간격을 벌려 배치함.
                obj.transform.position = pathStart + (_segmentVector * i);
                obj.transform.eulerAngles = fixedRotation;
                obj.transform.localScale = fixedScale;

                FrameData data = new FrameData();
                data.transform = obj.transform;
                data.label = obj.GetComponent<FrameDistanceLabel>();
                
                // 예시 입력: (0+1) * 10 -> 결과값 = 10m (첫 번째 프레임의 가상 거리)
                data.currentMeters = (i + 1) * virtualDistStartToEnd;
                
                _frames.Add(data);
                UpdateFrameLabel(data);
            }
            else
            {
                Debug.LogWarning("framePrefab이 할당되지 않음.");
            }
        }
    }

    /// <summary>
    /// 입력된 UV 스크롤 속도에 맞춰 모든 프레임을 이동시키고, 화면 뒤로 사라진 프레임을 앞으로 재배치함.
    /// </summary>
    /// <param name="uvSpeed">바닥 텍스처 스크롤 속도</param>
    public void ScrollFrames(float uvSpeed)
    {
        if (!enabled || _frames.Count == 0) return;

        // 예시 입력: uvSpeed(0.1) * _worldDistPerUV(250) * deltaTime(0.016) -> 결과값 = 0.4 (프레임당 이동 거리)
        float moveDistance = uvSpeed * _worldDistPerUV * Time.deltaTime;
        Vector3 displacement = _moveDirection * moveDistance;

        Vector3 forwardDir = _segmentVector.normalized; 
        float worldPerVirtualMeter = _segmentVector.magnitude / virtualDistStartToEnd;
        float resetWorldDist = resetVirtualDistance * worldPerVirtualMeter; 

        Vector3 totalTrackOffset = _segmentVector * _frames.Count;
        float totalVirtualDistance = virtualDistStartToEnd * _frames.Count;

        // # TODO: 리스트 역순 순회 또는 인덱스 캐싱을 통해 대량의 프레임 처리 시 연산 최적화 필요.
        for (int i = 0; i < _frames.Count; i++)
        {
            FrameData frameData = _frames[i];

            frameData.transform.position += displacement;

            // 이유: 시작점 기준 내적(Dot) 값을 통해 프레임이 플레이어 뒤쪽(임계점)으로 넘어갔는지 판별함.
            float distFromStart = Vector3.Dot(frameData.transform.position - pathStart, forwardDir);

            if (distFromStart < resetWorldDist)
            {
                float nextMeters = frameData.currentMeters + totalVirtualDistance;

                // 이유: 결승선 도달 전까지만 프레임을 앞으로 순환시켜 무한 트랙 효과를 유지함.
                if (nextMeters <= finishDistance)
                {
                    frameData.transform.position += totalTrackOffset;
                    frameData.currentMeters = nextMeters;
                    UpdateFrameLabel(frameData);
                }
            }
        }
    }

    /// <summary>
    /// 프레임에 부착된 거리 라벨 UI의 텍스트와 활성화 상태를 갱신함.
    /// </summary>
    /// <param name="data">갱신할 프레임 데이터</param>
    private void UpdateFrameLabel(FrameData data)
    {
        if (data == null) return;
        
        if (data.label)
        {
            data.label.SetLabelActive(showDistanceLabel);

            if (!showDistanceLabel) return;

            // 이유: 목표 거리에 도달한 프레임은 수치 대신 FINISH 문구를 출력함.
            if (data.currentMeters >= finishDistance - 0.1f)
            {
                data.label.SetText("FINISH");
            }
            else
            {
                data.label.SetDistance(data.currentMeters);
            }
        }
        else
        {
            Debug.LogWarning("프레임 프리팹에 FrameDistanceLabel 컴포넌트가 누락됨.");
        }
    }

    /// <summary>
    /// 카메라와 가장 가까운 프레임을 찾아 강제로 트랙 맨 뒤로 이동시킴.
    /// </summary>
    /// <param name="cameraTransform">기준이 될 카메라의 Transform</param>
    public void ForceRecycleFrameClosestToCamera(Transform cameraTransform)
    {
        if (_frames.Count == 0) return;
        
        if (!cameraTransform)
        {
            Debug.LogWarning("cameraTransform이 유효하지 않음.");
            return;
        }

        Vector3 camPos = cameraTransform.position;
        FrameData closestFrame = null;
        float minSqrDist = float.MaxValue;

        foreach (FrameData frame in _frames)
        {
            // 이유: 거리 비교 시 루트 연산을 생략하여 CPU 연산 비용을 절감하기 위해 sqrMagnitude를 사용함.
            float sqrDist = (frame.transform.position - camPos).sqrMagnitude;

            if (sqrDist < minSqrDist)
            {
                minSqrDist = sqrDist;
                closestFrame = frame;
            }
        }

        if (closestFrame != null)
        {
            Vector3 totalTrackOffset = _segmentVector * _frames.Count;
            float totalVirtualDistance = virtualDistStartToEnd * _frames.Count;

            float nextMeters = closestFrame.currentMeters + totalVirtualDistance;

            // 이유: 플레이어가 정지했을 때 카메라 바로 앞을 가로막는 프레임을 치워 시야 확보를 도움.
            if (nextMeters <= finishDistance)
            {
                closestFrame.transform.position += totalTrackOffset;
                closestFrame.currentMeters = nextMeters;
            
                UpdateFrameLabel(closestFrame);
            }
        }
    }
}