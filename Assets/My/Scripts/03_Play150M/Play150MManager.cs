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
        public TextSetting popupInfoText;
        public TextSetting waitingText;
        public TextSetting centerFinishText;
        public TextSetting[] questions;
    }

    public class Play150MManager : MonoBehaviour
    {
        public static Play150MManager Instance { get; private set; }

        [Header("Settings")]
        [SerializeField] private TutorialSettingsSO settings;
        [SerializeField] private float targetDistance = 150f;

        [Header("Distance Sync")]
        [SerializeField] private float metricMultiplier = 200f; 

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

        private readonly bool[] _playerFinished = new bool[2];
        private readonly bool[] _isPlayerPaused = new bool[2];
        private readonly bool[] _isInputBlocked = new bool[2];
        private readonly int[] _nextMilestones = { 10, 10 }; 
        private readonly Queue<int>[] _questionQueues = new Queue<int>[2];

        // 게이지 및 답변 상태 관리 변수
        private readonly int[] _playerStepCounts = new int[2]; 
        private readonly int[] _lastActiveLane = new int[2] { -1, -1 }; // (0:Yes, 2:No, -1:None)

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else if (Instance != this) Destroy(gameObject);
        }

        private void Start()
        {
            _data = JsonLoader.Load<Play150MData>(GameConstants.Path.Play150M);
            
            if (settings == null) { Debug.LogError("Settings Missing"); return; }
            if (players == null || players.Length < 2)
            {
                Debug.LogError("[Play150MManager] Players array must have size 2.");
                return;
            }

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

            // 라인 기억 초기화
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
            
            // [수정] 이미 완주한 플레이어라면 입력 무시 (마지막 팝업 상태여도 조작 불가)
            if (_playerFinished[playerIdx]) return;

            var player = players[playerIdx];
            if (player == null) return;

            // 팝업이 떠 있는 정지 상태일 때 (답변 입력 처리)
            if (_isPlayerPaused[playerIdx])
            {
                if (player.HandleInput(laneIdx, padIdx))
                {
                    player.MoveToLane(laneIdx);

                    // [수정] 게이지 처리 (답변 변경 시 초기화 로직 포함)
                    if (laneIdx == 0) // Yes
                    {
                        // 이전에 No(2)를 밟고 있었다면 리셋
                        if (_lastActiveLane[playerIdx] == 2)
                        {
                            _playerStepCounts[playerIdx] = 0;
                            ui.UpdateStepGauge(playerIdx, false, 0); // No 게이지 0으로 비움
                        }
                        
                        _lastActiveLane[playerIdx] = 0; // 현재 라인 갱신
                        ui.SetAnswerFeedback(playerIdx, true);
                        _playerStepCounts[playerIdx]++;
                        
                        // 완료 체크
                        if (ui.UpdateStepGauge(playerIdx, true, _playerStepCounts[playerIdx]))
                        {
                            StartCoroutine(AnswerCompleteRoutine(playerIdx));
                        }
                    }
                    else if (laneIdx == 2) // No
                    {
                        // 이전에 Yes(0)를 밟고 있었다면 리셋
                        if (_lastActiveLane[playerIdx] == 0)
                        {
                            _playerStepCounts[playerIdx] = 0;
                            ui.UpdateStepGauge(playerIdx, true, 0); // Yes 게이지 0으로 비움
                        }

                        _lastActiveLane[playerIdx] = 2; // 현재 라인 갱신
                        ui.SetAnswerFeedback(playerIdx, false);
                        _playerStepCounts[playerIdx]++;
                        
                        // 완료 체크
                        if (ui.UpdateStepGauge(playerIdx, false, _playerStepCounts[playerIdx]))
                        {
                            StartCoroutine(AnswerCompleteRoutine(playerIdx));
                        }
                    }
                    else // Center
                    {
                        ui.ResetAnswerFeedback(playerIdx);
                        // 중앙은 카운트를 올리지 않음
                    }
                }
                return; 
            }
            
            // 일반 달리기 입력 처리
            if (player.HandleInput(laneIdx, padIdx))
            {
                player.MoveAndAccelerate(laneIdx);
            }
        }

        private IEnumerator AnswerCompleteRoutine(int playerIdx)
        {
            _isInputBlocked[playerIdx] = true;
            
            // 1초간 정답(노란색) 상태 유지
            yield return CoroutineData.GetWaitForSeconds(1.0f);
            
            // 완주 판정 (목표 거리 초과)
            if (_nextMilestones[playerIdx] > targetDistance)
            {
                _playerFinished[playerIdx] = true;
                _isInputBlocked[playerIdx] = false; 
                
                if (ui) 
                {
                    ui.HideQuestionPopup(playerIdx, 0.1f);
                    ui.SetGaugeFinish(playerIdx); // 게이지 이미지 변경
                }

                // ★ [추가] 완주 후 대기 팝업 로직
                // 상대방 인덱스 구하기 (0이면 1, 1이면 0)
                int otherPlayerIdx = (playerIdx == 0) ? 1 : 0;

                // 상대방이 아직 안 끝났으면 대기 팝업 표시
                if (!_playerFinished[otherPlayerIdx])
                {
                    TextSetting waitData = _data != null ? _data.waitingText : null;
                    if (ui) ui.ShowWaitingPopup(playerIdx, waitData);
                }

                // 두 플레이어 모두 끝났는지 체크
                if (_playerFinished[0] && _playerFinished[1])
                {
                    StartCoroutine(FinishSequence());
                }
            }
            else
            {
                // 아직 목표에 도달하지 않았다면 게임 재개
                ResumePlayer(playerIdx);
            }
        }

        private void HandlePlayerDistanceChanged(int playerIdx, float currentDist, float maxDist)
        {
            if (_isGameFinished) return;
            if (playerIdx < 0 || playerIdx >= 2) return;

            if (ui) ui.UpdateGauge(playerIdx, currentDist, targetDistance);
            
            // [수정] 목표 거리(150m)를 포함하도록 조건 변경 (<= targetDistance)
            if (currentDist >= _nextMilestones[playerIdx] && _nextMilestones[playerIdx] <= targetDistance)
            {
                int milestone = _nextMilestones[playerIdx];
                _nextMilestones[playerIdx] += 10; 

                _isPlayerPaused[playerIdx] = true;
                
                if (players[playerIdx]) 
                {
                    players[playerIdx].ForceStop();
                    players[playerIdx].MoveToLane(1); 
                }

                StartCoroutine(BlockInputRoutine(playerIdx, 1.0f));
                if (padDotController != null) padDotController.SetCenterDotsAlpha(playerIdx, 0f);

                // 팝업 등장 시 상태 초기화
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
                if (ui) ui.ShowQuestionPopup(playerIdx, milestone, questionData, infoData);

                if (env) env.RecycleFrameClosestToCamera(playerIdx); 
            }
        }

        private IEnumerator BlockInputRoutine(int playerIdx, float duration)
        {
            _isInputBlocked[playerIdx] = true;
            yield return CoroutineData.GetWaitForSeconds(duration);
            _isInputBlocked[playerIdx] = false;
        }

        public void ResumePlayer(int playerIdx)
        {
            if (playerIdx < 0 || playerIdx >= 2) return;
            
            _isPlayerPaused[playerIdx] = false;
            _isInputBlocked[playerIdx] = false;
            
            if (ui) ui.HideQuestionPopup(playerIdx, 0.1f);
            
            if (padDotController != null) padDotController.SetCenterDotsAlpha(playerIdx, 1f);
        }

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
    
            // 1. 기존 질문 팝업 닫기
            if (ui)
            {
                ui.HideQuestionPopup(0, 0.1f);
                ui.HideQuestionPopup(1, 0.1f);
        
                // 열려있던 대기(Waiting) 팝업 닫기
                ui.HideWaitingPopups();
            }

            if (ui)
            {
                TextSetting centerData = _data != null ? _data.centerFinishText : null;
                ui.ShowCenterFinishPopup(centerData);
            }

            // 팝업을 읽을 시간(예: 3초) 대기 후 타이틀로 이동
            yield return CoroutineData.GetWaitForSeconds(3.0f);
    
            if (GameManager.Instance) GameManager.Instance.ReturnToTitle();
        }
    }
}