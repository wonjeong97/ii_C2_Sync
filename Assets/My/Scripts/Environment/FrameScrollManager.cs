using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using UnityEngine;
using VContainer;
using ZLogger;

namespace My.Scripts.Environment
{
    /// <summary>
    /// 달리기 트랙의 거리 표시 프레임 구조물들을 관리하고 무한 순환시키는 매니저 클래스.
    /// 계산 결과 캐싱을 통해 연산 부하를 최소화함.
    /// </summary>
    public class FrameScrollManager : MonoBehaviour
    {
        [Header("Prefab Settings")]
        [SerializeField] private GameObject framePrefab;
        [SerializeField] private int poolSize = 6;

        [Header("Path Settings")]
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

        public bool showDistanceLabel = true;

        private class FrameData
        {
            public Transform transform;
            public FrameDistanceLabel label;
            public float currentMeters;
        }

        private readonly List<FrameData> _frames = new();
        private Vector3 _segmentVector;
        private Vector3 _moveDirection;
        private Vector3 _totalTrackOffset;
        private float _worldDistPerUV;
        private float _worldPerVirtualMeter;
        private float _resetWorldDist;
        private float _totalVirtualDistance;

        private ILogger<FrameScrollManager> _logger;

        [Inject]
        public void Construct(ILogger<FrameScrollManager> logger)
        {
            _logger = logger;
        }

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
                _logger.ZLogWarning($"잘못된 Path 또는 Sync 설정값 감지. 매니저를 비활성화합니다.");
                enabled = false;
                return;
            }

            float worldDistPerLoop = _segmentVector.magnitude * (virtualMetersPerLoop / virtualDistStartToEnd);
            _worldDistPerUV = worldDistPerLoop / uvLoopSize;
            _worldPerVirtualMeter = _segmentVector.magnitude / virtualDistStartToEnd;
            _resetWorldDist = resetVirtualDistance * _worldPerVirtualMeter;
            _totalVirtualDistance = virtualDistStartToEnd * poolSize;
            _totalTrackOffset = _segmentVector * poolSize;
        }

        private void CreateAndPlaceFrames()
        {
            if (!framePrefab)
            {
                _logger.ZLogError($"framePrefab이 할당되지 않았습니다.");
                return;
            }

            for (int i = 0; i < poolSize; i++)
            {
                var obj = Instantiate(framePrefab, transform);
                obj.transform.SetPositionAndRotation(pathStart + (_segmentVector * i), Quaternion.Euler(fixedRotation));
                obj.transform.localScale = fixedScale;

                var data = new FrameData
                {
                    transform = obj.transform,
                    label = obj.GetComponent<FrameDistanceLabel>(),
                    currentMeters = (i + 1) * virtualDistStartToEnd
                };

                _frames.Add(data);
                UpdateFrameLabel(data);
            }
        }

        public void ScrollFrames(float uvSpeed)
        {
            if (!enabled || _frames.Count == 0) return;

            float moveDistance = uvSpeed * _worldDistPerUV * Time.deltaTime;
            Vector3 displacement = _moveDirection * moveDistance;
            Vector3 forwardDir = _segmentVector.normalized;

            foreach (var frameData in _frames)
            {
                frameData.transform.position += displacement;

                float distFromStart = Vector3.Dot(frameData.transform.position - pathStart, forwardDir);

                if (distFromStart < _resetWorldDist)
                {
                    float nextMeters = frameData.currentMeters + _totalVirtualDistance;
                    if (nextMeters <= finishDistance)
                    {
                        frameData.transform.position += _totalTrackOffset;
                        frameData.currentMeters = nextMeters;
                        UpdateFrameLabel(frameData);
                    }
                }
            }
        }

        private void UpdateFrameLabel(FrameData data)
        {
            if (data?.label == null) return;

            data.label.SetLabelActive(showDistanceLabel);
            if (!showDistanceLabel) return;

            if (data.currentMeters >= finishDistance - 0.1f)
            {
                data.label.SetText("FINISH");
            }
            else
            {
                data.label.SetDistance(data.currentMeters);
            }
        }

        public void ForceRecycleFrameClosestToCamera(Transform cameraTransform)
        {
            if (_frames.Count == 0 || !cameraTransform) return;

            Vector3 camPos = cameraTransform.position;
            FrameData closestFrame = null;
            float minSqrDist = float.MaxValue;

            foreach (var frame in _frames)
            {
                float sqrDist = (frame.transform.position - camPos).sqrMagnitude;
                if (sqrDist < minSqrDist)
                {
                    minSqrDist = sqrDist;
                    closestFrame = frame;
                }
            }

            if (closestFrame != null)
            {
                float nextMeters = closestFrame.currentMeters + _totalVirtualDistance;
                if (nextMeters <= finishDistance)
                {
                    closestFrame.transform.position += _totalTrackOffset;
                    closestFrame.currentMeters = nextMeters;
                    UpdateFrameLabel(closestFrame);
                }
            }
        }
    }
}