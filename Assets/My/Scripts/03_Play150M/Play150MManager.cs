using System.Collections;
using System.Collections.Generic;
using My.Scripts._02_PlayTutorial.Controllers;
using My.Scripts._02_PlayTutorial.Data;
using My.Scripts._03_Play150M.Managers;
using My.Scripts.Core;
using My.Scripts.Global;
using UnityEngine;
using UnityEngine.UI;
using Wonjeong.Data;
using Wonjeong.UI;
using Wonjeong.Utils;

namespace My.Scripts._03_Play150M
{
    [System.Serializable]
    public class Play150MData
    {
        public TextSetting startText;
        public TextSetting[] questions;
    }

    public class Play150MManager : MonoBehaviour
    {
        // [수정] Instance 필드를 프로퍼티로 변경 (캡슐화 경고 해결)
        public static Play150MManager Instance { get; private set; }

        [Header("Settings")]
        [SerializeField] private TutorialSettingsSO settings;
        [SerializeField] private float targetDistance = 150f;

        [Header("Distance Sync")]
        [SerializeField] private float metricMultiplier = 210f; 

        [Header("Sub Systems")]
        [SerializeField] private Play150MUIManager ui; 
        [SerializeField] private Play150MEnvironment env;
        
        [SerializeField] private Text countdownText;
        
        [Header("Dot Controller")]
        [SerializeField] private PadDotController padDotController;

        [Header("Players")]
        [SerializeField] private TutorialPlayerController[] players = new TutorialPlayerController[2];

        private Play150MData _data;
        private bool _gameStarted;
        private bool _isGameFinished;

        // [수정] 배열 참조가 변하지 않으므로 readonly 적용
        private readonly bool[] _playerFinished = new bool[2];
        private readonly bool[] _isPlayerPaused = new bool[2];
        
        // [수정] 명시적 배열 크기 지정 제거 및 readonly 적용
        private readonly int[] _nextMilestones = { 10, 10 };
        private readonly Queue<int>[] _questionQueues = new Queue<int>[2];

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else if (Instance != this) Destroy(gameObject);
        }

        private void Start()
        {
            _data = JsonLoader.Load<Play150MData>(GameConstants.Path.Play150M);
            
            // 패턴 매칭: null 체크 간소화
            if (settings is null) { Debug.LogError("Settings Missing"); return; }

            InitializeQuestionQueues();
            
            if (ui) ui.InitUI(targetDistance);
            if (env) env.InitEnvironment();

            if (padDotController != null)
            {
                padDotController.SetCenterDotsAlpha(0, 1f);
                padDotController.SetCenterDotsAlpha(1, 1f);
            }

            // 마일스톤 초기화 (배열은 readonly지만 내부 값은 변경 가능)
            _nextMilestones[0] = 10;
            _nextMilestones[1] = 10;

            if (countdownText != null)
            {
                countdownText.gameObject.SetActive(false);
                countdownText.text = "";
            }

            for (int i = 0; i < players.Length; i++)
            {
                if (players[i] != null)
                {
                    Vector2[] lanes = (i == 0) ? settings.p1LanePositions : settings.p2LanePositions;
                    var physicsConfig = settings.physicsConfig;
                    physicsConfig.maxDistance = targetDistance;
                    physicsConfig.useMetricDistance = true;
                    physicsConfig.metricMultiplier = metricMultiplier; 
                    
                    players[i].Setup(i, lanes, physicsConfig);
                    players[i].OnDistanceChanged -= HandlePlayerDistanceChanged;
                    players[i].OnDistanceChanged += HandlePlayerDistanceChanged;
                }
            }

            if (InputManager.Instance != null) InputManager.Instance.OnPadDown += HandlePadDown;

            StartCoroutine(StartSequence());
        }

        // 누락되었던 메서드 구현
        private void InitializeQuestionQueues()
        {
            int questionCount = (_data?.questions != null) ? _data.questions.Length : 0;
            
            for (int p = 0; p < 2; p++)
            {
                List<int> indices = new List<int>();
                for (int i = 0; i < questionCount; i++) indices.Add(i);
                
                // Fisher-Yates Shuffle
                for (int i = 0; i < indices.Count; i++)
                {
                    int rnd = Random.Range(i, indices.Count);
                    (indices[i], indices[rnd]) = (indices[rnd], indices[i]);
                }
                _questionQueues[p] = new Queue<int>(indices);
            }
        }

        private void OnDestroy()
        {
            if (InputManager.Instance != null) InputManager.Instance.OnPadDown -= HandlePadDown;
            if (players != null)
            {
                foreach (var player in players)
                    if (player != null) player.OnDistanceChanged -= HandlePlayerDistanceChanged;
            }
        }

        private void Update()
        {
            if (!_gameStarted) return;

            for (int i = 0; i < players.Length; i++)
            {
                if (_isPlayerPaused[i])
                {
                    if (players[i]) players[i].ForceStop();
                    continue; 
                }

                if (players[i]) players[i].OnUpdate(false, 0f, 0f);
            }

            float stopLimit = targetDistance + 1.0f; 
            
            // 삼항 연산자 패턴 정리
            float s1 = (players[0] && !_isPlayerPaused[0] && players[0].currentDistance < stopLimit) ? players[0].currentSpeed : 0f;
            float s2 = (players[1] && !_isPlayerPaused[1] && players[1].currentDistance < stopLimit) ? players[1].currentSpeed : 0f;

            if (env) env.ScrollEnvironment(s1, s2);
        }

        private void HandlePadDown(int playerIdx, int laneIdx, int padIdx)
        {
            if (!_gameStarted || _isGameFinished) return;
            if (playerIdx < 0 || playerIdx >= players.Length) return;
            
            var player = players[playerIdx];
            if (player == null) return;

            if (_isPlayerPaused[playerIdx])
            {
                if (player.HandleInput(laneIdx, padIdx))
                {
                    player.MoveToLane(laneIdx);

                    if (ui)
                    {
                        if (laneIdx == 0) ui.SetAnswerFeedback(playerIdx, true);
                        else if (laneIdx == 2) ui.SetAnswerFeedback(playerIdx, false);
                        else ui.ResetAnswerFeedback(playerIdx);
                    }
                }
                return; 
            }
            
            if (_playerFinished[playerIdx]) return;

            if (player.HandleInput(laneIdx, padIdx))
            {
                player.MoveAndAccelerate(laneIdx);
            }
        }

        private void HandlePlayerDistanceChanged(int playerIdx, float currentDist, float maxDist)
        {
            if (_isGameFinished) return;

            if (ui) ui.UpdateGauge(playerIdx, currentDist, targetDistance);

            // 10m 단위 마일스톤 체크
            if (!_isPlayerPaused[playerIdx] && currentDist >= _nextMilestones[playerIdx] && _nextMilestones[playerIdx] < targetDistance)
            {
                int milestone = _nextMilestones[playerIdx];
                _nextMilestones[playerIdx] += 10; 

                _isPlayerPaused[playerIdx] = true;
                if (players[playerIdx]) 
                {
                    players[playerIdx].ForceStop();
                    players[playerIdx].MoveToLane(1); 
                }

                if (padDotController != null) padDotController.SetCenterDotsAlpha(playerIdx, 0f);

                TextSetting questionData = null;
                if (_questionQueues[playerIdx] != null && _questionQueues[playerIdx].Count > 0)
                {
                    int qIdx = _questionQueues[playerIdx].Dequeue();
                    if (_data?.questions != null && qIdx < _data.questions.Length)
                    {
                        questionData = _data.questions[qIdx];
                    }
                }

                if (ui) ui.ShowQuestionPopup(playerIdx, milestone, questionData);
                if (env) env.RecycleFrameClosestToCamera(playerIdx); 
            }

            if (!_playerFinished[playerIdx] && currentDist >= targetDistance)
            {
                _playerFinished[playerIdx] = true;
                if (_playerFinished[0] && _playerFinished[1])
                {
                    StartCoroutine(FinishSequence());
                }
            }
        }

        public void ResumePlayer(int playerIdx)
        {
            if (playerIdx < 0 || playerIdx >= 2) return;
            
            _isPlayerPaused[playerIdx] = false;
            
            if (ui) ui.HideQuestionPopup(playerIdx, 0.5f);
            
            if (padDotController != null) padDotController.SetCenterDotsAlpha(playerIdx, 1f);
        }

        public int GetCurrentLane(int playerIdx)
        {
            if (playerIdx >= 0 && playerIdx < players.Length && players[playerIdx] != null)
                return players[playerIdx].currentLane;
            return 1;
        }

        public void OnPlayerHit(int playerIdx)
        {
            if (playerIdx >= 0 && playerIdx < players.Length && players[playerIdx] != null)
            {
                players[playerIdx].OnHit(2.0f);
            }
        }

        // [복원] 누락되었던 시퀀스 로직 구현 (150M UI에 맞춰 수정됨)
        private IEnumerator StartSequence()
        {
            // 150M UI에는 HidePopup이 없고 QuestionPopup 제어만 있음
            if (ui)
            {
                ui.HideQuestionPopup(0, 0f);
                ui.HideQuestionPopup(1, 0f);
            }

            if (countdownText != null)
            {
                countdownText.gameObject.SetActive(true);
                for (int i = 3; i > 0; i--)
                {
                    countdownText.text = i.ToString();
                    yield return CoroutineData.GetWaitForSeconds(1.0f);
                }

                if (_data?.startText != null)
                {
                    if (UIManager.Instance != null)
                        UIManager.Instance.SetText(countdownText.gameObject, _data.startText);
                    else
                        countdownText.text = _data.startText.text;
                }
                else
                {
                    countdownText.text = "Start!";
                }
            }
            
            _gameStarted = true;
            yield return CoroutineData.GetWaitForSeconds(1.0f);
            if (countdownText != null) countdownText.gameObject.SetActive(false);
        }

        private IEnumerator FinishSequence()
        {
            _isGameFinished = true;
            if (ui)
            {
                ui.HideQuestionPopup(0, 0.1f);
                ui.HideQuestionPopup(1, 0.1f);
                yield return StartCoroutine(ui.ShowSuccessText("FINISH!", 3.0f));
            }
            yield return CoroutineData.GetWaitForSeconds(3.0f);
            if (GameManager.Instance != null) GameManager.Instance.ReturnToTitle();
        }
    }
}