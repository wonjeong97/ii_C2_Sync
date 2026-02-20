using System.Collections.Generic;
using My.Scripts._02_PlayTutorial.Components;
using UnityEngine;

namespace My.Scripts._04_PlayLong
{
    public class PlayLongObstacleManager : MonoBehaviour
    {
        [Header("Obstacle Settings")]
        [SerializeField] private GameObject obstaclePrefab;

        [Header("Generation Settings")]
        [SerializeField] private float startSpawnDistance = 20f;
        [SerializeField] private float maxSpawnDistance = 500f;

        [Header("Lane Settings")]
        [SerializeField] private float laneWidth = 3f;

        [Header("Path Settings")]
        public Vector3 pathStart = new Vector3(0f, -1.5f, 1.534f);
        public Vector3 pathEnd = new Vector3(0f, -1.5f, 14.034f);
        public float virtualDistStartToEnd = 10f; 

        [Header("Fader Settings")]
        [SerializeField] private bool useDistanceFade = true;
        [SerializeField] private float fullyVisibleDist = 10f;
        [SerializeField] private float invisibleDist = 30f;

        private readonly List<GameObject> _spawnedObstacles = new List<GameObject>();
        private Vector3 _moveDirection; 
        private Vector3 _laneOffsetVector;
        private Vector3 _forwardDir; // Cached forward direction
        private float _worldPerVirtualMeter; 
        private Camera _targetCamera; 

        public void Init(Camera cam, bool spawnRandom = true)
        {
            _targetCamera = cam;

            if (InitializePathVectors())
            {
                if (spawnRandom) GenerateProgressiveObstacles();
            }
        }

        private bool InitializePathVectors()
        {
            if (virtualDistStartToEnd <= 0) return false;

            Vector3 segmentVector = pathEnd - pathStart;
            _forwardDir = segmentVector.normalized;
            _moveDirection = -_forwardDir; 
            _worldPerVirtualMeter = segmentVector.magnitude / virtualDistStartToEnd;

            Vector3 geomRight = Vector3.Cross(Vector3.up, _forwardDir).normalized;
            
            float correctionFactor = 1.0f;
            if (Mathf.Abs(geomRight.x) > 0.001f) correctionFactor = 1.0f / Mathf.Abs(geomRight.x);
            _laneOffsetVector = Vector3.right * (laneWidth * correctionFactor);

            return true;
        }

        public void GenerateProgressiveObstacles()
        {   
            ResetObstacles();
            
            float currentDist = startSpawnDistance;

            while (currentDist <= maxSpawnDistance)
            {
                float interval;
                int obstacleCount;

                if (currentDist < 150f)
                {
                    interval = Random.Range(15f, 20f);
                    obstacleCount = 1;                
                }
                else if (currentDist < 350f) 
                {
                    interval = Random.Range(10f, 15f); 
                    obstacleCount = (Random.value > 0.7f) ? 2 : 1; 
                }
                else 
                {
                    interval = Random.Range(7f, 10f);  
                    obstacleCount = (Random.value > 0.5f) ? 2 : 1; 
                }

                SpawnRandomLaneObstacles(currentDist, obstacleCount);
                currentDist += interval;
            }
        }
        
        public void ResetObstacles()
        {
            foreach (GameObject obj in _spawnedObstacles)
            {
                if (obj) Destroy(obj);
            }
            _spawnedObstacles.Clear();
        }

        private void SpawnRandomLaneObstacles(float dist, int count)
        {
            List<int> lanes = new List<int> { -1, 0, 1 };
            for (int i = 0; i < lanes.Count; i++)
            {
                int rnd = Random.Range(i, lanes.Count);
                (lanes[i], lanes[rnd]) = (lanes[rnd], lanes[i]);
            }

            for (int i = 0; i < count; i++) SpawnSingleObstacle(dist, lanes[i]);
        }

        public void SpawnSingleObstacle(float dist, int laneIdx)
        {
            if (!obstaclePrefab) return;

            Vector3 centerPos = pathStart + (_forwardDir * (dist * _worldPerVirtualMeter));
            Vector3 finalPos = centerPos + (_laneOffsetVector * laneIdx);

            GameObject obj = Instantiate(obstaclePrefab, transform);
            obj.transform.position = finalPos;
    
            ObstacleHitChecker hitChecker = obj.GetComponent<ObstacleHitChecker>();
            if (!hitChecker) hitChecker = obj.AddComponent<ObstacleHitChecker>();
            
            hitChecker.Setup(-1, laneIdx); 

            if (useDistanceFade)
            {
                FrameDistanceFader fader = obj.GetComponent<FrameDistanceFader>();
                if (!fader) fader = obj.AddComponent<FrameDistanceFader>();
                
                if (_targetCamera) fader.targetTransform = _targetCamera.transform;
                else if (Camera.main) fader.targetTransform = Camera.main.transform;
                
                fader.fullyVisibleDist = fullyVisibleDist;
                fader.invisibleDist = invisibleDist;
            }

            _spawnedObstacles.Add(obj);
        }

        public void MoveObstacles(float meters)
        {
            if (_spawnedObstacles.Count == 0) return;

            float moveDistance = meters * _worldPerVirtualMeter;
            Vector3 displacement = _moveDirection * moveDistance;

            for (int i = _spawnedObstacles.Count - 1; i >= 0; i--)
            {
                GameObject obj = _spawnedObstacles[i];
                if (!obj)
                {
                    _spawnedObstacles.RemoveAt(i);
                    continue;
                }

                ObstacleHitChecker hitChecker = obj.GetComponent<ObstacleHitChecker>();
                if (hitChecker && hitChecker.IsStopMove) 
                {
                    continue; 
                }

                obj.transform.position += displacement;

                float distFromStart = Vector3.Dot(obj.transform.position - pathStart, _forwardDir);

                if (distFromStart < -5f * _worldPerVirtualMeter)
                {
                    Destroy(obj);
                    _spawnedObstacles.RemoveAt(i);
                }
            }
        }
    }
}