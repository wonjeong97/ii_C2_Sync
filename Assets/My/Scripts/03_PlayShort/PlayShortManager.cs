using System;
using System.Collections;
using System.Collections.Generic;
using My.Scripts._02_PlayTutorial.Data;
using My.Scripts.Core;
using My.Scripts.Global;
using UnityEngine;
using UnityEngine.UI;
using Wonjeong.Data;
using Wonjeong.UI;
using Wonjeong.Utils;
using Random = UnityEngine.Random;

namespace My.Scripts._03_PlayShort
{
    [Serializable]
    public class PlayShortData
    {
        public TextSetting startText;
        public TextSetting popupInfoText;
        public TextSetting waitingText;
        public TextSetting centerFinishText;
        public TextSetting[] questions;
    }

    public class PlayShortManager : MonoBehaviour
    {
        public static PlayShortManager Instance { get; private set; }

        [Header("Settings")]
        [SerializeField] private TutorialSettingsSO settings;
        
        [Header("Distance Sync")]
        [SerializeField] private float metricMultiplier = 200f; 

        [Header("Sub Systems")]
        [SerializeField] private PlayShortUIManager ui; 
        [SerializeField] private PlayShortEnvironment env;
        
        [SerializeField] private Text countdownText;
        
        [Header("Dot Controller")]
        [SerializeField] private PadDotController padDotController;

        [Header("Players")]
        [SerializeField] private PlayerController[] players = new PlayerController[2];

        private readonly float targetDistance = 200f;
        private PlayShortData _data;
        private bool _gameStarted;
        private bool _isGameFinished;

        private readonly bool[] _playerFinished = new bool[2];
        private readonly bool[] _isPlayerPaused = new bool[2];
        private readonly bool[] _isInputBlocked = new bool[2];
        private readonly int[] _nextMilestones = { 10, 10 }; 
        private readonly Queue<int>[] _questionQueues = new Queue<int>[2];

        // 게이지 및 답변 상태 관리 변수
        private readonly int[] _playerStepCounts = new int[2]; 
        private readonly int[] _lastActiveLane = new int[2] { -1, -1 }; 

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else if (Instance != this) Destroy(gameObject);
        }

        private void Start()
        {
            _data = JsonLoader.Load<PlayShortData>(GameConstants.Path.PlayShort);
            
            if (settings == null) { Debug.LogError("Settings Missing"); return; }
            if (players == null || players.Length < 2) return;

            InitializeQuestionQueues();
            
            if (ui) ui.InitUI(targetDistance);
            if (env) env.InitEnvironment();

            if (padDotController != null)
            {
                padDotController.SetCenterDotsAlpha(0, 1f);
                padDotController.SetCenterDotsAlpha(1, 1f);
            }

            _nextMilestones[0] = 10;
            _nextMilestones[1] = 10;
            _lastActiveLane[0] = -1;
            _lastActiveLane[1] = -1;

            if (countdownText != null)
            {
                countdownText.gameObject.SetActive(false);
                countdownText.text = "";
            }

            for (int i = 0; i < 2; i++)
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

        private void InitializeQuestionQueues()
        {
            int questionCount = (_data != null && _data.questions != null) ? _data.questions.Length : 0;
            
            for (int p = 0; p < 2; p++)
            {
                List<int> indices = new List<int>();
                for (int i = 0; i < questionCount; i++) indices.Add(i);
                
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
            if (Instance == this) Instance = null;
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

            for (int i = 0; i < 2; i++)
            {
                if (_isPlayerPaused[i])
                {
                    if (players[i]) players[i].ForceStop();
                    continue; 
                }

                if (players[i]) players[i].OnUpdate(false, 0f, 0f);
            }

            float stopLimit = targetDistance + 1.0f; 
            
            float s1 = (players[0] && !_isPlayerPaused[0] && players[0].currentDistance < stopLimit) ? players[0].currentSpeed : 0f;
            float s2 = (players[1] && !_isPlayerPaused[1] && players[1].currentDistance < stopLimit) ? players[1].currentSpeed : 0f;

            if (env) env.ScrollEnvironment(s1, s2);
        }

        private void HandlePadDown(int playerIdx, int laneIdx, int padIdx)
        {
            if (!_gameStarted || _isGameFinished) return;
            if (playerIdx < 0 || playerIdx >= 2) return;
            if (_isInputBlocked[playerIdx]) return;
            
            if (_playerFinished[playerIdx]) return;

            var player = players[playerIdx];
            if (player == null) return;

            // 팝업 상태 (답변 입력)
            if (_isPlayerPaused[playerIdx])
            {
                if (player.HandleInput(laneIdx, padIdx))
                {
                    player.MoveToLane(laneIdx);

                    if (laneIdx == 0) // Yes
                    {
                        if (_lastActiveLane[playerIdx] == 2)
                        {
                            _playerStepCounts[playerIdx] = 0;
                            ui.UpdateStepGauge(playerIdx, false, 0); 
                        }
                        
                        _lastActiveLane[playerIdx] = 0; 
                        ui.SetAnswerFeedback(playerIdx, true);
                        _playerStepCounts[playerIdx]++;
                        
                        if (ui.UpdateStepGauge(playerIdx, true, _playerStepCounts[playerIdx]))
                        {
                            StartCoroutine(AnswerCompleteRoutine(playerIdx));
                        }
                    }
                    else if (laneIdx == 2) // No
                    {
                        if (_lastActiveLane[playerIdx] == 0)
                        {
                            _playerStepCounts[playerIdx] = 0;
                            ui.UpdateStepGauge(playerIdx, true, 0); 
                        }

                        _lastActiveLane[playerIdx] = 2; 
                        ui.SetAnswerFeedback(playerIdx, false);
                        _playerStepCounts[playerIdx]++;
                        
                        if (ui.UpdateStepGauge(playerIdx, false, _playerStepCounts[playerIdx]))
                        {
                            StartCoroutine(AnswerCompleteRoutine(playerIdx));
                        }
                    }
                    else // Center
                    {
                        ui.ResetAnswerFeedback(playerIdx);
                    }
                }
                return; 
            }
            
            // 일반 달리기 입력
            if (player.HandleInput(laneIdx, padIdx))
            {
                player.MoveAndAccelerate(laneIdx);
            }
        }

        private IEnumerator AnswerCompleteRoutine(int playerIdx)
        {
            _isInputBlocked[playerIdx] = true;
            
            yield return CoroutineData.GetWaitForSeconds(1.0f);
            
            if (_nextMilestones[playerIdx] > targetDistance)
            {
                _playerFinished[playerIdx] = true;
                _isInputBlocked[playerIdx] = false;

                if (players[playerIdx])
                {
                    players[playerIdx].MoveToLane(1);           
                    players[playerIdx].SetFinishAnimation();    
                }
                if (ui) 
                {
                    ui.HideQuestionPopup(playerIdx, 0.5f);
                    ui.SetGaugeFinish(playerIdx); 
                }
                
                int otherPlayerIdx = (playerIdx == 0) ? 1 : 0;

                if (!_playerFinished[otherPlayerIdx])
                {
                    TextSetting waitData = _data != null ? _data.waitingText : null;
                    if (ui) ui.ShowWaitingPopup(playerIdx, waitData);
                }

                if (_playerFinished[0] && _playerFinished[1])
                {
                    StartCoroutine(FinishSequence());
                }
            }
            else
            {
                ResumePlayer(playerIdx);
            }
        }

        private void HandlePlayerDistanceChanged(int playerIdx, float currentDist, float maxDist)
        {
            if (_isGameFinished) return;
            if (playerIdx < 0 || playerIdx >= 2) return;

            if (ui) ui.UpdateGauge(playerIdx, currentDist, targetDistance);
            
            if (currentDist >= _nextMilestones[playerIdx] && _nextMilestones[playerIdx] <= targetDistance)
            {
                int milestone = _nextMilestones[playerIdx];
                _nextMilestones[playerIdx] += 10; 

                _isPlayerPaused[playerIdx] = true;
                
                if (players[playerIdx]) 
                {
                    players[playerIdx].ForceStop();
                    // [수정] 중앙 이동(MoveToLane(1)) 제거: 현재 라인 유지
                }

                // [수정] 즉시 입력 차단 후 시퀀스 시작
                _isInputBlocked[playerIdx] = true;
                if (padDotController != null) padDotController.SetCenterDotsAlpha(playerIdx, 0f);

                _playerStepCounts[playerIdx] = 0;
                _lastActiveLane[playerIdx] = -1;

                TextSetting questionData = null;
                if (_questionQueues[playerIdx] != null && _questionQueues[playerIdx].Count > 0)
                {
                    int qIdx = _questionQueues[playerIdx].Dequeue();
                    if (_data?.questions != null && qIdx < _data.questions.Length)
                    {
                        questionData = _data.questions[qIdx];
                    }
                }

                TextSetting infoData = _data != null ? _data.popupInfoText : null;
                
                // [수정] 팝업 시퀀스 코루틴 실행 (2초 대기 -> 페이드 -> 입력 허용)
                StartCoroutine(QuestionSequenceRoutine(playerIdx, milestone, questionData, infoData));

                if (env) env.RecycleFrameClosestToCamera(playerIdx); 
            }
        }

        // 팝업 등장 시퀀스 제어
        private IEnumerator QuestionSequenceRoutine(int playerIdx, int milestone, TextSetting qData, TextSetting infoData)
        {
            // 1. Page1(질문) 표시 (YesNo 그룹은 숨김 상태)
            if (ui) ui.ShowQuestionPopup(playerIdx, milestone, qData, infoData);

            // 2. 2초 대기 (입력은 여전히 차단됨)
            yield return CoroutineData.GetWaitForSeconds(2.0f);

            // 3. YesNo 페이드인 + Page2로 전환 (0.5초)
            if (ui) yield return StartCoroutine(ui.ShowQuestionPhase2Routine(playerIdx, 0.5f));

            // 4. 입력 허용
            _isInputBlocked[playerIdx] = false;
        }

        public void ResumePlayer(int playerIdx)
        {
            if (playerIdx < 0 || playerIdx >= 2) return;
            
            _isPlayerPaused[playerIdx] = false;
            _isInputBlocked[playerIdx] = false;
            
            if (ui) ui.HideQuestionPopup(playerIdx, 0.5f);
            
            if (padDotController != null) padDotController.SetCenterDotsAlpha(playerIdx, 1f);
        }

        // ... (나머지 메서드는 기존과 동일) ...
        
        public int GetCurrentLane(int playerIdx)
        {
            if (playerIdx >= 0 && playerIdx < 2 && players[playerIdx] != null)
                return players[playerIdx].currentLane;
            return 1;
        }

        public void OnPlayerHit(int playerIdx)
        {
            if (playerIdx >= 0 && playerIdx < 2 && players[playerIdx] != null)
            {
                players[playerIdx].OnHit(2.0f);
            }
        }

        private IEnumerator StartSequence()
        {
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

                if (_data != null && _data.startText != null)
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
            if (_isGameFinished) yield break;
    
            _isGameFinished = true;
    
            if (ui)
            {
                ui.HideQuestionPopup(0, 0.5f);
                ui.HideQuestionPopup(1, 0.5f);
                ui.HideWaitingPopups();
            }

            if (ui)
            {
                TextSetting centerData = _data != null ? _data.centerFinishText : null;
                ui.ShowCenterFinishPopup(centerData);
            }

            yield return CoroutineData.GetWaitForSeconds(5.0f);
            
            if (GameManager.Instance) 
            {
                GameManager.Instance.ChangeScene(GameConstants.Scene.PlayLong);
            }
            else 
            {
                UnityEngine.SceneManagement.SceneManager.LoadScene(GameConstants.Scene.PlayLong);
            }
        }
    }
}