using UnityEngine;
using System.Collections.Generic;
using My.Scripts.Environment;

namespace My.Scripts._04_PlayLong
{
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

        private List<FrameData> _frames = new List<FrameData>();
        private Vector3 _moveDirection;
        private float _worldPerVirtualMeter;
        private Vector3 _segmentVector;

        public void Init()
        {
            _segmentVector = pathEnd - pathStart;
            Vector3 dir = -_segmentVector.normalized;
            _moveDirection = new Vector3(0f, 0f, dir.z);
            _worldPerVirtualMeter = _segmentVector.magnitude / virtualDistStartToEnd;

            CreateAndPlaceFrames();
        }

        private void CreateAndPlaceFrames()
        {
            for (int i = 0; i < poolSize; i++)
            {
                if (!framePrefab) continue;

                GameObject obj = Instantiate(framePrefab, transform);
                float targetVirtualMeters = (i + 1) * frameIntervalMeters;

                Vector3 spawnPos = pathStart + (_segmentVector.normalized * (targetVirtualMeters * _worldPerVirtualMeter));
                spawnPos.x = pathStart.x;
                spawnPos.y = pathStart.y;
                obj.transform.position = spawnPos;

                FrameData data = new FrameData();
                data.gameObject = obj;
                data.transform = obj.transform;
                data.label = obj.GetComponent<FrameDistanceLabel>();
                data.currentMeters = targetVirtualMeters;

                _frames.Add(data);
                UpdateFrameLabel(data);
            }
        }

        public void MoveFrames(float movedMeters)
        {
            if (_frames.Count == 0) return;

            Vector3 displacement = _moveDirection * (movedMeters * _worldPerVirtualMeter);
            Vector3 forwardDir = _segmentVector.normalized;

            float totalVirtualDistance = frameIntervalMeters * _frames.Count;
            Vector3 totalTrackOffset = _segmentVector.normalized * (totalVirtualDistance * _worldPerVirtualMeter);

            for (int i = _frames.Count - 1; i >= 0; i--)
            {
                FrameData frame = _frames[i];
                frame.transform.position += displacement;
                frame.currentMeters += movedMeters;

                if (movedMeters > 0)
                {
                    float distFromStart = Vector3.Dot(frame.transform.position - pathStart, forwardDir);
                    
                    // 이유: 카메라 뒤로 넘어간 프레임을 파괴(Destroy)하지 않고 트랙 맨 앞으로 위치시켜 무한 재사용(Recycling)함
                    if (distFromStart < -frameIntervalMeters * _worldPerVirtualMeter)
                    {
                        float nextMeters = frame.currentMeters + totalVirtualDistance;
                        
                        if (nextMeters <= finishDistance)
                        {
                            frame.transform.position += totalTrackOffset;
                            frame.currentMeters = nextMeters;
                            UpdateFrameLabel(frame);
                        }
                    }
                }
            }
        }

        private void UpdateFrameLabel(FrameData data)
        {
            if (!data.label) return;

            float m = Mathf.Round(data.currentMeters / 10f) * 10f; 

            if (m >= finishDistance) data.label.SetText("FINISH");
            else data.label.SetDistance(m);

            bool isMilestone = (m > 0 && Mathf.Abs(m % 100f) < 0.1f);
            data.label.SetLabelActive(isMilestone); 
        }

        public void ResetFrames()
        {
            for (int i = _frames.Count - 1; i >= 0; i--)
            {
                if (_frames[i] != null) UpdateFrameLabel(_frames[i]);
            }
        }

        public void RebuildFramesFromZero()
        {
            foreach (FrameData frame in _frames)
            {
                if (frame != null && frame.gameObject) Destroy(frame.gameObject);
            }
            _frames.Clear();
            CreateAndPlaceFrames();
        }
    }
}