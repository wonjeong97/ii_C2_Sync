using UnityEngine;
using System.Collections.Generic;
using My.Scripts.Environment;

namespace My.Scripts._04_PlayLong
{
    /// <summary>
    /// PlayLong 씬에서 거리 표시 프레임의 생성, 이동 및 풀링을 관리하는 클래스.
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

        private List<FrameData> _frames = new List<FrameData>();
        private Vector3 _moveDirection;
        private float _worldPerVirtualMeter;
        private Vector3 _segmentVector;

        /// <summary>
        /// 프레임 스크롤 연산에 필요한 초기 벡터 및 비율을 계산하고 초기 프레임을 배치함.
        /// </summary>
        public void Init()
        {
            _segmentVector = pathEnd - pathStart;
            Vector3 dir = -_segmentVector.normalized;
            _moveDirection = new Vector3(0f, 0f, dir.z);
            
            // 예시 입력: segmentVector.magnitude(12.5) / virtualDistStartToEnd(10) -> 결과값 = 1.25 (가상 1m당 실제 월드 거리)
            _worldPerVirtualMeter = _segmentVector.magnitude / virtualDistStartToEnd;

            CreateAndPlaceFrames();
        }

        /// <summary>
        /// 설정된 풀 사이즈만큼 프레임을 인스턴스화하고 트랙 경로상에 순차적으로 배치함.
        /// </summary>
        private void CreateAndPlaceFrames()
        {
            for (int i = 0; i < poolSize; i++)
            {
                if (!framePrefab)
                {
                    Debug.LogWarning("framePrefab 누락됨.");
                    continue;
                }

                GameObject obj = Instantiate(framePrefab, transform);
                
                // 예시 입력: (i=0 + 1) * 20f -> 결과값 = 20f
                float targetVirtualMeters = (i + 1) * frameIntervalMeters;

                // 이유: 시작 위치 기준으로 목표 가상 거리에 해당하는 월드 좌표 오프셋을 더해 배치 위치를 결정함.
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

        /// <summary>
        /// 입력받은 이동 거리만큼 프레임들을 이동시키고 범위를 벗어난 프레임을 재배치함.
        /// </summary>
        /// <param name="movedMeters">이동할 가상 거리(미터)</param>
        public void MoveFrames(float movedMeters)
        {
            if (_frames.Count == 0) return;

            // 예시 입력: movedMeters(2) * _worldPerVirtualMeter(1.25) -> 결과값 = 2.5 (실제 이동해야 할 월드 거리)
            Vector3 displacement = _moveDirection * (movedMeters * _worldPerVirtualMeter);
            Vector3 forwardDir = _segmentVector.normalized;

            float totalVirtualDistance = frameIntervalMeters * _frames.Count;
            Vector3 totalTrackOffset = _segmentVector.normalized * (totalVirtualDistance * _worldPerVirtualMeter);

            // # TODO: 리스트 역순 순회 성능 향상을 위해 배열 접근 방식 또는 인덱스 캐싱 최적화 검토 필요.
            for (int i = _frames.Count - 1; i >= 0; i--)
            {
                FrameData frame = _frames[i];
                frame.transform.position += displacement;
                frame.currentMeters += movedMeters;

                if (movedMeters > 0)
                {
                    float distFromStart = Vector3.Dot(frame.transform.position - pathStart, forwardDir);
                    
                    // 이유: 카메라 뒤로 넘어간 프레임을 파괴하지 않고 트랙 맨 앞으로 위치시켜 무한 재사용함.
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

        /// <summary>
        /// 지정된 프레임 데이터의 거리 텍스트 라벨을 갱신함.
        /// </summary>
        /// <param name="data">갱신할 프레임 데이터 객체</param>
        private void UpdateFrameLabel(FrameData data)
        {
            if (data == null)
            {
                Debug.LogWarning("FrameData 누락됨.");
                return;
            }
            if (!data.label)
            {
                Debug.LogWarning("FrameDistanceLabel 누락됨.");
                return;
            }

            // 예시 입력: currentMeters(24) / 10f -> 2.4 -> Round(2.4) = 2 -> 2 * 10f = 20f
            float m = Mathf.Round(data.currentMeters / 10f) * 10f; 

            if (m >= finishDistance) data.label.SetText("FINISH");
            else data.label.SetDistance(m);

            // 이유: 100m 단위 마일스톤인 경우에만 라벨을 활성화하여 화면 내 과도한 UI 노출을 방지함.
            bool isMilestone = (m > 0 && Mathf.Abs(m % 100f) < 0.1f);
            data.label.SetLabelActive(isMilestone); 
        }

        /// <summary>
        /// 모든 프레임의 라벨을 현재 상태에 맞춰 다시 갱신함.
        /// </summary>
        public void ResetFrames()
        {
            for (int i = _frames.Count - 1; i >= 0; i--)
            {
                if (_frames[i] != null) UpdateFrameLabel(_frames[i]);
            }
        }

        /// <summary>
        /// 기존 프레임을 모두 파괴하고 초기 상태로 재배치함.
        /// </summary>
        public void RebuildFramesFromZero()
        {
            // # TODO: 잦은 Destroy 호출은 가비지 컬렉션 오버헤드를 유발하므로 비활성화(SetActive) 후 재사용하는 풀링 방식으로 전환 검토 필요.
            foreach (FrameData frame in _frames)
            {
                if (frame != null && frame.gameObject) Destroy(frame.gameObject);
            }
            _frames.Clear();
            CreateAndPlaceFrames();
        }
    }
}