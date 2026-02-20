using UnityEngine;
using System.Collections.Generic;
using My.Scripts.Environment;

namespace My.Scripts._04_PlayLong
{
    /// <summary>
    /// PlayLong 모드 전용 프레임 매니저.
    /// 생성된 프레임이 카메라 뒤로 넘어가면 리사이클링 대신 파괴 처리함.
    /// </summary>
    public class PlayLongFrameManager : MonoBehaviour
    {
        [Header("Prefab Settings")]
        [SerializeField] private GameObject framePrefab;
        [SerializeField] private int poolSize = 25;

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
                if (framePrefab == null) continue;

                GameObject obj = Instantiate(framePrefab, transform);
                float targetVirtualMeters = (i + 1) * frameIntervalMeters;

                Vector3 spawnPos = pathStart + (_segmentVector.normalized * (targetVirtualMeters * _worldPerVirtualMeter));
                spawnPos.x = pathStart.x;
                spawnPos.y = pathStart.y;
                obj.transform.position = spawnPos;

                FrameData data = new FrameData
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

        /// <summary>
        /// 프레임을 이동시키고 기준점보다 뒤로 가면 리스트에서 제거 및 파괴함.
        /// </summary>
        public void MoveFrames(float movedMeters)
        {
            if (_frames.Count == 0) return;

            Vector3 displacement = _moveDirection * (movedMeters * _worldPerVirtualMeter);
            Vector3 forwardDir = _segmentVector.normalized;

            // 리스트를 역순으로 순회하여 파괴 시 인덱스 꼬임 방지
            for (int i = _frames.Count - 1; i >= 0; i--)
            {
                var frame = _frames[i];
                frame.transform.position += displacement;
                frame.currentMeters += movedMeters;

                // 리사이클링 로직 대신 파괴 로직 적용
                if (movedMeters > 0)
                {
                    float distFromStart = Vector3.Dot(frame.transform.position - pathStart, forwardDir);
                    
                    // 기준점(0M)보다 프레임 간격만큼 뒤로 가면 삭제
                    if (distFromStart < -frameIntervalMeters * _worldPerVirtualMeter)
                    {
                        Destroy(frame.gameObject);
                        _frames.RemoveAt(i);
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
            // 리스트에 남은 프레임들만 갱신
            for (int i = _frames.Count - 1; i >= 0; i--)
            {
                if (_frames[i] != null) UpdateFrameLabel(_frames[i]);
            }
        }

        public void RebuildFramesFromZero()
        {
            foreach (var frame in _frames)
            {
                if (frame != null && frame.gameObject != null)
                    Destroy(frame.gameObject);
            }
            _frames.Clear();
            CreateAndPlaceFrames();
        }
    }
}