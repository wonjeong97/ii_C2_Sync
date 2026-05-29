using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using My.Scripts.Environment;
using UnityEngine;
using VContainer;
using ZLogger;

namespace My.Scripts._04_PlayLong
{
    /// <summary>
    /// PlayLong 씬에서 거리 표시 프레임의 생성, 이동 및 풀링을 관리하는 클래스.
    /// 객체 풀링과 DI를 통해 최적화된 상태입니다.
    /// </summary>
    public class PlayLongFrameManager : MonoBehaviour
    {
        [Header("Prefab Settings")]
        [SerializeField] private GameObject framePrefab;
        [Tooltip("최적화를 위해 소수의 프레임만 무한 순환시킵니다.")]
        [SerializeField] private int poolSize = 5;

        [Header("Path Settings (Fixed Height)")]
        public Vector3 pathStart = new Vector3(0f, 0.6f, 1.531f);
        public Vector3 pathEnd = new Vector3(0f, 0.6f, 14.03f);
        public float virtualDistStartToEnd = 10f;

        [Header("Sync Settings")]
        [SerializeField] private float finishDistance = 500f;
        [SerializeField] private float frameIntervalMeters = 20f;

        private class FrameData
        {
            public GameObject gameObject;
            public Transform transform;
            public FrameDistanceLabel label;
            public float currentMeters;
        }

        private readonly List<FrameData> _frames = new List<FrameData>();
        private Vector3 _moveDirection;
        private float _worldPerVirtualMeter;
        private Vector3 _segmentVector;
        
        private float _totalVirtualDistance;
        private Vector3 _totalTrackOffset;
        private float _recycleThreshold;

        private ILogger<PlayLongFrameManager> _logger;

        [Inject]
        public void Construct(ILogger<PlayLongFrameManager> logger)
        {
            _logger = logger;
        }

        public void Init()
        {
            _segmentVector = pathEnd - pathStart;
            Vector3 dir = -_segmentVector.normalized;
            _moveDirection = new Vector3(0f, 0f, dir.z);
            _worldPerVirtualMeter = _segmentVector.magnitude / virtualDistStartToEnd;

            // 이동 및 순환에 필요한 상수 사전 계산
            _totalVirtualDistance = frameIntervalMeters * poolSize;
            _totalTrackOffset = _segmentVector.normalized * (_totalVirtualDistance * _worldPerVirtualMeter);
            _recycleThreshold = -frameIntervalMeters * _worldPerVirtualMeter;

            CreateAndPlaceFrames();
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
                GameObject obj = Instantiate(framePrefab, transform);
                
                float targetVirtualMeters = (i + 1) * frameIntervalMeters;
                Vector3 spawnPos = pathStart + (_segmentVector.normalized * (targetVirtualMeters * _worldPerVirtualMeter));
                spawnPos.x = pathStart.x;
                spawnPos.y = pathStart.y;
                obj.transform.position = spawnPos;

                var data = new FrameData
                {
                    gameObject = obj,
                    transform = obj.transform,
                    label = obj.GetComponent<FrameDistanceLabel>(),
                    currentMeters = targetVirtualMeters
                };

                _frames.Add(data);
                UpdateFrameLabel(data);
            }
        }

        public void MoveFrames(float movedMeters)
        {
            if (_frames.Count == 0 || movedMeters <= 0) return;

            Vector3 displacement = _moveDirection * (movedMeters * _worldPerVirtualMeter);
            Vector3 forwardDir = _segmentVector.normalized;

            foreach (FrameData frame in _frames)
            {
                // 1. 이동
                frame.transform.position += displacement;

                // 2. 범위를 벗어난 프레임 순환 (Recycle)
                if (Vector3.Dot(frame.transform.position - pathStart, forwardDir) < _recycleThreshold)
                {
                    RecycleFrame(frame);
                }
            }
        }
        
        private void RecycleFrame(FrameData frame)
        {
            float nextMeters = frame.currentMeters + _totalVirtualDistance;
        
            if (nextMeters <= finishDistance)
            {
                frame.transform.position += _totalTrackOffset;
                frame.currentMeters = nextMeters;
                UpdateFrameLabel(frame);
            }
        }

        private void UpdateFrameLabel(FrameData data)
        {
            if (!data?.label) return;

            float m = Mathf.Round(data.currentMeters / 10f) * 10f; 

            if (m >= finishDistance) data.label.SetText("FINISH");
            else data.label.SetDistance(m);

            bool isMilestone = (m > 0 && Mathf.Abs(m % 100f) < 0.1f);
            data.label.SetLabelActive(isMilestone); 
        }

        public void ResetFrames()
        {
            foreach (var frame in _frames)
            {
                if (frame != null) UpdateFrameLabel(frame);
            }
        }

        /// <summary>
        /// 객체 파괴 없이 재배치하여 GC 발생을 방지합니다.
        /// </summary>
        public void RebuildFramesFromZero()
        {
            for (int i = 0; i < _frames.Count; i++)
            {
                FrameData frame = _frames[i];
                if (!frame?.gameObject) continue;

                frame.currentMeters = (i + 1) * frameIntervalMeters;
                Vector3 spawnPos = pathStart + (_segmentVector.normalized * (frame.currentMeters * _worldPerVirtualMeter));
                spawnPos.x = pathStart.x;
                spawnPos.y = pathStart.y;
                
                frame.transform.position = spawnPos;
                frame.gameObject.SetActive(true);
                UpdateFrameLabel(frame);
            }
        }
    }
}