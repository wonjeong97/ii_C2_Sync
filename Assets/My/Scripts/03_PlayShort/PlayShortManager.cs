using System;
using System.Collections;
using System.Collections.Generic;
using My.Scripts._02_PlayTutorial.Data;
using My.Scripts.Core;
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
        public TextSetting playerAName; 
        public TextSetting playerBName; 

        public TextSetting startText;
        public TextSetting popupInfoText;
        public TextSetting waitingText;
        public TextSetting centerFinishText;
        
        public TextSetting[] questions;
    }

    [Serializable]
    public class PlayShortQuestionData
    {
        public TextSetting[] questions;
    }

    /// <summary>
    /// Play Short 모드의 흐름과 각 플레이어의 거리, 질문 팝업 등을 관리하는 클래스.
    /// 연출 시퀀스와 실제 사용자 입력 구간을 구분하여 글로벌 방치 타이머를 제어함.
    /// </summary>
    public class PlayShortManager : MonoBehaviour
    {
        private static readonly int Idle = Animator.StringToHash("Idle");
        private static readonly int FinishJump = Animator.StringToHash("FinishJump");
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

        private readonly int[] _playerStepCounts = new int[2]; 
        private readonly int[] _lastActiveLane = new int[2] { -1, -1 }; 
        
        // [추가] 각 플레이어의 현재 진행 중인 질문 번호(1~20)를 저장하는 배열
        private readonly int[] _currentQuestionNumbers = new int[2]; 

        private float _lastHitSoundTime = -1f;

        private void Awake()
        {
            if (!Instance) Instance = this;
            else if (Instance != this) Destroy(gameObject);
        }

        private void Start()
        {
            _data = JsonLoader.Load<PlayShortData>(GameConstants.Path.PlayShort);
            
            if (GameManager.Instance)
            {
                string typeStr = GameManager.Instance.currentUserType.ToString(); 
                string questionJsonPath = $"JSON/PlayShort_{typeStr}";
                
                PlayShortQuestionData qData = JsonLoader.Load<PlayShortQuestionData>(questionJsonPath);
                
                if (qData != null && qData.questions != null)
                {
                    _data.questions = qData.questions;
                    Debug.Log($"[PlayShortManager] {questionJsonPath} 파일에서 관계 맞춤형 질문 데이터를 로드했습니다.");
                }
                else
                {
                    Debug.LogWarning($"[PlayShortManager] {questionJsonPath} 파일을 찾을 수 없어 기본(PlayShort.json) 질문을 사용합니다.");
                }
            }

            if (!settings) { Debug.LogError("[PlayShortManager] Settings Missing"); return; }
            if (players == null || players.Length < 2) return;

            InitializeQuestionQueues();
            
            if (ui) 
            {
                ui.InitUI(targetDistance);
                
                if (GameManager.Instance)
                {
                    string nameA = string.IsNullOrEmpty(GameManager.Instance.PlayerAName) ? "Player A" : GameManager.Instance.PlayerAName;
                    string nameB = string.IsNullOrEmpty(GameManager.Instance.PlayerBName) ? "Player B" : GameManager.Instance.PlayerBName;
                    
                    TextSetting settingA = _data != null ? _data.playerAName : null;
                    TextSetting settingB = _data != null ? _data.playerBName : null;

                    ui.SetPlayerNames(nameA, nameB, settingA, settingB);

                    Sprite spriteA = GameManager.Instance.GetColorSprite(GameManager.Instance.PlayerAColor);
                    Sprite spriteB = GameManager.Instance.GetColorSprite(GameManager.Instance.PlayerBColor);
                    ui.SetPlayerBalls(spriteA, spriteB);
                }
            }

            if (env) env.InitEnvironment();

            if (padDotController)
            {
                padDotController.SetCenterDotsAlpha(0, 1f);
                padDotController.SetCenterDotsAlpha(1, 1f);
            }

            _nextMilestones[0] = 10;
            _nextMilestones[1] = 10;
            _lastActiveLane[0] = -1;
            _lastActiveLane[1] = -1;

            if (countdownText)
            {
                countdownText.gameObject.SetActive(false);
                countdownText.text = "";
            }

            for (int i = 0; i < 2; i++)
            {
                if (players[i])
                {
                    Vector2[] lanes = (i == 0) ? settings.p1LanePositions : settings.p2LanePositions;
                    PlayerPhysicsConfig physicsConfig = settings.physicsConfig;
                    physicsConfig.maxDistance = targetDistance;
                    physicsConfig.useMetricDistance = true;
                    physicsConfig.metricMultiplier = metricMultiplier; 
                    
                    players[i].Setup(i, lanes, physicsConfig);
                    players[i].OnDistanceChanged -= HandlePlayerDistanceChanged;
                    players[i].OnDistanceChanged += HandlePlayerDistanceChanged;

                    if (GameManager.Instance)
                    {
                        ColorData colorData = (i == 0) ? GameManager.Instance.PlayerAColor : GameManager.Instance.PlayerBColor;
                        Color targetColor = GameManager.Instance.GetColorFromData(colorData);
                        players[i].SetCharacterColor(targetColor);
                    }
                }
            }

            if (InputManager.Instance) InputManager.Instance.OnPadDown += HandlePadDown;

            SetAutoProgressing(true);
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
            if (InputManager.Instance) InputManager.Instance.OnPadDown -= HandlePadDown;
            if (players != null)
            {
                foreach (PlayerController player in players)
                    if (player) player.OnDistanceChanged -= HandlePlayerDistanceChanged;
            }

            if (GameManager.Instance)
            {
                GameManager.Instance.IsAutoProgressing = false;
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

        private void SetAutoProgressing(bool isAuto)
        {
            if (GameManager.Instance)
            {
                GameManager.Instance.IsAutoProgressing = isAuto;
                
                if (!isAuto)
                {
                    GameManager.Instance.ResetInactivityTimer();
                }
            }
            else
            {
                Debug.LogWarning("[PlayShortManager] GameManager.Instance를 찾을 수 없습니다.");
            }
        }

        private void HandlePadDown(int playerIdx, int laneIdx, int padIdx)
        {
            if (!_gameStarted || _isGameFinished) return;
            if (playerIdx < 0 || playerIdx >= 2) return;
            if (_isInputBlocked[playerIdx]) return;
            
            if (_playerFinished[playerIdx]) return;

            PlayerController player = players[playerIdx];
            if (!player) return;

            if (_isPlayerPaused[playerIdx])
            {
                if (ui) ui.NotifyInput(playerIdx);
                if (player.HandleInput(laneIdx, padIdx))
                {
                    player.MoveToLane(laneIdx);

                    if (laneIdx == 0) // Yes
                    {
                        if (_lastActiveLane[playerIdx] == 2)
                        {
                            _playerStepCounts[playerIdx] = 0;
                            if (ui) ui.UpdateStepGauge(playerIdx, false, 0); 
                        }
                        
                        _lastActiveLane[playerIdx] = 0; 
                        if (ui) ui.SetAnswerFeedback(playerIdx, true);
                        _playerStepCounts[playerIdx]++;
                        
                        if (ui && ui.UpdateStepGauge(playerIdx, true, _playerStepCounts[playerIdx]))
                        {
                            // [추가] 게이지가 다 차서 답변이 확정되었을 때 API 전송 (YES는 1로 전송)
                            if (GameManager.Instance)
                            {
                                string side = (playerIdx == 0) ? "left" : "right";
                                GameManager.Instance.SendValueUpdateAPI(_currentQuestionNumbers[playerIdx], side, 1);
                            }
                            StartCoroutine(AnswerCompleteRoutine(playerIdx));
                        }
                    }
                    else if (laneIdx == 2) // No
                    {
                        if (_lastActiveLane[playerIdx] == 0)
                        {
                            _playerStepCounts[playerIdx] = 0;
                            if (ui) ui.UpdateStepGauge(playerIdx, true, 0); 
                        }

                        _lastActiveLane[playerIdx] = 2; 
                        if (ui) ui.SetAnswerFeedback(playerIdx, false);
                        _playerStepCounts[playerIdx]++;
                        
                        if (ui && ui.UpdateStepGauge(playerIdx, false, _playerStepCounts[playerIdx]))
                        {
                            // 게이지가 다 차서 답변이 확정되었을 때 API 전송
                            if (GameManager.Instance)
                            {
                                string side = (playerIdx == 0) ? "left" : "right";
                                GameManager.Instance.SendValueUpdateAPI(_currentQuestionNumbers[playerIdx], side, 0);
                            }
                            StartCoroutine(AnswerCompleteRoutine(playerIdx));
                        }
                    }
                    else // Center
                    {
                        if (ui) ui.ResetAnswerFeedback(playerIdx);
                    }
                }
                return; 
            }
            
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
                    StartCoroutine(players[playerIdx].SetFinishRoutine());
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
                }

                _isInputBlocked[playerIdx] = true;
                if (padDotController) padDotController.SetCenterDotsAlpha(playerIdx, 0f);

                _playerStepCounts[playerIdx] = 0;
                _lastActiveLane[playerIdx] = -1;

                TextSetting questionData = null;
                if (_questionQueues[playerIdx] != null && _questionQueues[playerIdx].Count > 0)
                {
                    int qIdx = _questionQueues[playerIdx].Dequeue();
                    
                    // [추가] 큐에서 뽑은 인덱스(0~19)에 1을 더해 실제 질문 번호(1~20)로 기억함
                    // 이유: 나중에 답변을 확정했을 때 API로 어느 질문에 대답했는지 식별하여 전송하기 위함.
                    _currentQuestionNumbers[playerIdx] = qIdx + 1;

                    if (_data?.questions != null && qIdx < _data.questions.Length)
                    {
                        questionData = _data.questions[qIdx];
                    }
                }

                TextSetting infoData = _data != null ? _data.popupInfoText : null;
                if (SoundManager.Instance) SoundManager.Instance.PlaySFX("달리기_3");
                
                StartCoroutine(QuestionSequenceRoutine(playerIdx, milestone, questionData, infoData));

                if (env) env.RecycleFrameClosestToCamera(playerIdx); 
            }
        }

        private IEnumerator QuestionSequenceRoutine(int playerIdx, int milestone, TextSetting qData, TextSetting infoData)
        {
            if (ui) ui.ShowQuestionPopup(playerIdx, milestone, qData, infoData);

            yield return CoroutineData.GetWaitForSeconds(2.0f);

            if (ui) yield return StartCoroutine(ui.ShowQuestionPhase2Routine(playerIdx, 0.5f, milestone));

            _isInputBlocked[playerIdx] = false;
        }

        private void ResumePlayer(int playerIdx)
        {
            if (playerIdx < 0 || playerIdx >= 2) return;
            
            _isPlayerPaused[playerIdx] = false;
            _isInputBlocked[playerIdx] = false;
            
            if (ui) ui.HideQuestionPopup(playerIdx, 0.5f);
            
            if (padDotController) padDotController.SetCenterDotsAlpha(playerIdx, 1f);
        }

        public int GetCurrentLane(int playerIdx)
        {
            if (playerIdx >= 0 && playerIdx < 2 && players[playerIdx])
                return players[playerIdx].currentLane;
            return 1;
        }

        public void OnPlayerHit(int playerIdx)
        {
            if (playerIdx >= 0 && playerIdx < 2 && players[playerIdx])
            {
                if (Time.time - _lastHitSoundTime > 0.1f)
                {
                    if (SoundManager.Instance) SoundManager.Instance.PlaySFX("달리기_2");
                    _lastHitSoundTime = Time.time;
                }
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

            if (countdownText)
            {   
                if (SoundManager.Instance) SoundManager.Instance.PlaySFX("공통_10_3초");
                countdownText.gameObject.SetActive(true);
                for (int i = 3; i > 0; i--)
                {
                    countdownText.text = i.ToString();
                    yield return CoroutineData.GetWaitForSeconds(1.0f);
                }

                if (_data != null && _data.startText != null)
                {
                    if (UIManager.Instance)
                    {   
                        yield return CoroutineData.GetWaitForSeconds(0.3f);
                        if (SoundManager.Instance) SoundManager.Instance.PlaySFX("공통_14");
                        UIManager.Instance.SetText(countdownText.gameObject, _data.startText);
                    }
                    else
                    {
                        countdownText.text = _data.startText.text;
                    }
                }
                else
                {
                    countdownText.text = "Start!";
                }
            }
            
            _gameStarted = true;
            SetAutoProgressing(false);
            
            yield return CoroutineData.GetWaitForSeconds(1.0f);
            if (countdownText) countdownText.gameObject.SetActive(false);
        }

        private IEnumerator FinishSequence()
        {
            if (_isGameFinished) yield break;
    
            _isGameFinished = true;
            SetAutoProgressing(true);
    
            if (ui)
            {
                ui.HideQuestionPopup(0, 0.5f);
                ui.HideQuestionPopup(1, 0.5f);
                ui.HideWaitingPopups();
            }

            if (ui)
            {
                TextSetting centerData = _data != null ? _data.centerFinishText : null;
                
                if (SoundManager.Instance) SoundManager.Instance.PlaySFX("달리기_4");
                ui.ShowCenterFinishPopup(centerData);
            }
            
            foreach (PlayerController player in players)
            {
                if (player) player.CharacterAnimator.SetTrigger(Idle);
            }
            yield return CoroutineData.GetWaitForSeconds(0.5f);
            foreach (PlayerController player in players)
            {
                if (player) player.CharacterAnimator.SetTrigger(FinishJump);
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