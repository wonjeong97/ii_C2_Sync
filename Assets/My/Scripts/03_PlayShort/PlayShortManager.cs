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
    /// PlayShort 씬의 전반적인 게임 흐름과 플레이어 상태를 제어하는 매니저 클래스.
    /// </summary>
    public class PlayShortManager : MonoBehaviour
    {
        private readonly static int Idle = Animator.StringToHash("Idle");
        private readonly static int FinishJump = Animator.StringToHash("FinishJump");
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
        
        private readonly int[] _currentQuestionNumbers = new int[2]; 
        private readonly float[] _prevDistances = new float[2];

        private float _lastHitSoundTime = -1f;

        public bool IsGameStarted => _gameStarted;

        /// <summary>
        /// 싱글톤 인스턴스 초기화.
        /// </summary>
        private void Awake()
        {
            if (!Instance) Instance = this;
            else if (Instance && Instance.GetInstanceID() != this.GetInstanceID()) Destroy(gameObject);
        }

        /// <summary>
        /// 데이터 로드 및 초기 컴포넌트 세팅.
        /// </summary>
        private void Start()
        {
            _data = JsonLoader.Load<PlayShortData>(GameConstants.Path.PlayShort);
            
            if (GameManager.Instance)
            {
                string typeStr = GameManager.Instance.currentUserType.ToString(); 
                char cartridgeChar = typeStr.Length > 0 ? typeStr[0] : 'A';
                string relationStr = typeStr.Length > 1 ? typeStr.Substring(1) : "1";
                
                bool isLoaded = false;
                
                string primaryPath = $"JSON/Cartridge_{cartridgeChar}/PlayShort_{typeStr}";
                PlayShortQuestionData qData = JsonLoader.Load<PlayShortQuestionData>(primaryPath);
                
                // 일반 C# 객체는 명시적 null 검사 허용
                if (qData != null && qData.questions != null)
                {
                    _data.questions = qData.questions;
                    isLoaded = true;
                }
                
                // 이유: B, C, D 카트리지에서 파일 누락 시 1차적으로 동일한 관계의 A 카트리지 질문으로 대응함.
                if (!isLoaded && cartridgeChar != 'A')
                {
                    string fallbackAPath = $"JSON/Cartridge_A/PlayShort_A{relationStr}";
                    PlayShortQuestionData fallbackAData = JsonLoader.Load<PlayShortQuestionData>(fallbackAPath);
                    
                    if (fallbackAData != null && fallbackAData.questions != null)
                    {
                        _data.questions = fallbackAData.questions;
                        Debug.LogWarning($"맞춤형 질문 로드 실패. 폴백 데이터 사용. 경로: {fallbackAPath}");
                        isLoaded = true;
                    }
                }
                
                // 이유: 모든 폴백 실패 시 최종적으로 가장 기본 형태인 A1 데이터를 강제 적용함.
                if (!isLoaded && typeStr != "A1")
                {
                    string fallbackA1Path = "JSON/Cartridge_A/PlayShort_A1";
                    PlayShortQuestionData fallbackA1Data = JsonLoader.Load<PlayShortQuestionData>(fallbackA1Path);
                    
                    if (fallbackA1Data != null && fallbackA1Data.questions != null)
                    {
                        _data.questions = fallbackA1Data.questions;
                        Debug.LogWarning($"최종 카트리지 기본값 사용. 경로: {fallbackA1Path}");
                        isLoaded = true;
                    }
                }
                
                if (!isLoaded)
                {
                    Debug.LogWarning("모든 카트리지 폴백 실패. 기본 질문 유지됨.");
                }
            }
            else
            {
                Debug.LogWarning("GameManager 인스턴스 누락됨.");
            }

            if (!settings) 
            { 
                Debug.LogError("TutorialSettingsSO 누락됨."); 
                return; 
            }
            
            if (players == null || players.Length < 2) 
            {
                Debug.LogWarning("플레이어 배열 데이터 누락됨.");
                return;
            }

            InitializeQuestionQueues();
            
            if (ui) 
            {
                ui.InitUI(targetDistance);
                
                if (GameManager.Instance)
                {
                    string nameA = GameManager.Instance.PlayerAName;
                    string nameB = GameManager.Instance.PlayerBName;
                    
                    // 이유: 기획 의도와 달리 빈 문자열이 표시되는 것을 막기 위해 경고 로그 출력 후 진행.
                    if (string.IsNullOrEmpty(nameA)) Debug.LogWarning("Player A 이름 데이터 누락됨.");
                    if (string.IsNullOrEmpty(nameB)) Debug.LogWarning("Player B 이름 데이터 누락됨.");
                    
                    TextSetting settingA = _data != null ? _data.playerAName : null;
                    TextSetting settingB = _data != null ? _data.playerBName : null;

                    ui.SetPlayerNames(nameA, nameB, settingA, settingB);

                    Sprite spriteA = GameManager.Instance.GetColorSprite(GameManager.Instance.PlayerAColor);
                    Sprite spriteB = GameManager.Instance.GetColorSprite(GameManager.Instance.PlayerBColor);
                    
                    if (!spriteA) Debug.LogWarning("Player A 컬러 스프라이트 누락됨.");
                    if (!spriteB) Debug.LogWarning("Player B 컬러 스프라이트 누락됨.");
                    
                    ui.SetPlayerBalls(spriteA, spriteB);
                }
            }
            else
            {
                Debug.LogWarning("PlayShortUIManager 누락됨.");
            }

            if (env) env.InitEnvironment();
            else Debug.LogWarning("PlayShortEnvironment 누락됨.");

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
            else
            {
                Debug.LogWarning("countdownText 누락됨.");
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
                        Sprite targetSprite = GameManager.Instance.GetColorSprite(colorData);

                        if (targetSprite)
                        {
                            players[i].SetCharacterSprite(targetSprite);
                        }
                        else
                        {
                            Debug.LogWarning($"Player {i} 대상 스프라이트 누락됨. 틴트 적용.");
                            Color targetColor = GameManager.Instance.GetColorFromData(colorData);
                            players[i].SetCharacterColor(targetColor);
                        }
                    }
                }
            }

            if (InputManager.Instance) InputManager.Instance.OnPadDown += HandlePadDown;
            else Debug.LogWarning("InputManager 누락됨.");

            SetAutoProgressing(true);
            StartCoroutine(StartSequence());
        }

        /// <summary>
        /// 플레이어마다 띄워줄 질문 리스트의 인덱스를 셔플하여 큐에 할당함.
        /// </summary>
        private void InitializeQuestionQueues()
        {
            // 이유: 플레이어가 마주하는 질문의 순서를 무작위로 섞어 단조로움을 방지함.
            int questionCount = (_data != null && _data.questions != null) ? _data.questions.Length : 0;
            
            for (int p = 0; p < 2; p++)
            {
                List<int> indices = new List<int>();
                for (int i = 0; i < questionCount; i++) indices.Add(i);
                
                for (int i = 0; i < indices.Count; i++)
                {
                    int rnd = Random.Range(i, indices.Count);
                    int temp = indices[i];
                    indices[i] = indices[rnd];
                    indices[rnd] = temp;
                }
                _questionQueues[p] = new Queue<int>(indices);
            }
        }

        /// <summary>
        /// 이벤트 구독 해제 및 상태 초기화.
        /// </summary>
        private void OnDestroy()
        {   
            if (Instance && Instance.GetInstanceID() == this.GetInstanceID()) Instance = null;
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

        /// <summary>
        /// 매 프레임 플레이어 속도 계산 및 배경 스크롤 반영.
        /// </summary>
        private void Update()
        {
            if (!_gameStarted) return;

            // # TODO: 매 프레임 배열을 순회하여 물리 연산을 진행하므로, 이벤트 구동 방식으로 최적화 고려.
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
            
            float s1 = 0f;
            float s2 = 0f;
            
            if (Time.deltaTime > 0f)
            {
                if (players[0])
                {
                    float currentDist = players[0].currentDistance;
                    if (currentDist < stopLimit)
                    {
                        float delta = currentDist - _prevDistances[0];
                        // 예시 입력값: delta(10) / metricMultiplier(200) / Time.deltaTime(0.016) -> 결과값 = 3.125 (배경 스크롤 속도)
                        s1 = (delta / metricMultiplier) / Time.deltaTime;
                    }
                    _prevDistances[0] = currentDist;
                }

                if (players[1])
                {
                    float currentDist = players[1].currentDistance;
                    if (currentDist < stopLimit)
                    {
                        float delta = currentDist - _prevDistances[1];
                        s2 = (delta / metricMultiplier) / Time.deltaTime;
                    }
                    _prevDistances[1] = currentDist;
                }
            }

            if (env) env.ScrollEnvironment(s1, s2);
        }

        /// <summary>
        /// 자동 진행 여부에 따른 방치 타이머 활성 상태 갱신.
        /// </summary>
        /// <param name="isAuto">자동 진행 활성화 여부</param>
        private void SetAutoProgressing(bool isAuto)
        {
            if (GameManager.Instance)
            {
                GameManager.Instance.IsAutoProgressing = isAuto;
                
                // 이유: 유저 조작 구간으로 변경될 때 방치 타이머를 리셋하여 원치 않는 팝업을 방지함.
                if (!isAuto)
                {
                    GameManager.Instance.ResetInactivityTimer();
                }
            }
            else
            {
                Debug.LogWarning("GameManager 인스턴스 누락됨.");
            }
        }

        /// <summary>
        /// 발판 조작에 따른 플레이어 위치 이동 및 선택지 판정 로직.
        /// </summary>
        /// <param name="playerIdx">플레이어 인덱스</param>
        /// <param name="laneIdx">레인 인덱스</param>
        /// <param name="padIdx">패드 인덱스</param>
        private void HandlePadDown(int playerIdx, int laneIdx, int padIdx)
        {
            if (!_gameStarted || _isGameFinished) return;
            if (playerIdx < 0 || playerIdx >= 2) return;
            if (_isInputBlocked[playerIdx]) return;
            if (_playerFinished[playerIdx]) return;

            PlayerController player = players[playerIdx];
            if (!player) return;

            // 이유: 질문이 떠서 멈춰있는 상태에서는 답변 선택지 조작만 활성화함.
            if (_isPlayerPaused[playerIdx])
            {
                if (ui) ui.NotifyInput(playerIdx);
                if (player.HandleInput(laneIdx, padIdx))
                {
                    player.MoveToLane(laneIdx);

                    if (laneIdx == 0)
                    {
                        // 이유: 0번 레인은 긍정 선택지로 지정됨.
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
                            if (GameManager.Instance)
                            {
                                string side = (playerIdx == 0) ? "left" : "right";
                                GameManager.Instance.SendValueUpdateAPI(_currentQuestionNumbers[playerIdx], side, 1);
                            }
                            StartCoroutine(AnswerCompleteRoutine(playerIdx));
                        }
                    }
                    else if (laneIdx == 2)
                    {
                        // 이유: 2번 레인은 부정 선택지로 지정됨.
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
                            if (GameManager.Instance)
                            {
                                string side = (playerIdx == 0) ? "left" : "right";
                                GameManager.Instance.SendValueUpdateAPI(_currentQuestionNumbers[playerIdx], side, 0);
                            }
                            StartCoroutine(AnswerCompleteRoutine(playerIdx));
                        }
                    }
                    else
                    {
                        if (ui) ui.ResetAnswerFeedback(playerIdx);
                    }
                }
                return; 
            }
            
            // 이유: 질문 상태가 아닌 일반 달리기 상태일 경우 전진 가속 처리.
            if (player.HandleInput(laneIdx, padIdx))
            {
                player.MoveAndAccelerate(laneIdx);
            }
        }

        /// <summary>
        /// 질문 답변 선택 후 조작을 제한하고 다음 단계 진입을 처리함.
        /// </summary>
        /// <param name="playerIdx">응답 완료한 플레이어 인덱스</param>
        /// <returns>IEnumerator 루틴</returns>
        private IEnumerator AnswerCompleteRoutine(int playerIdx)
        {
            _isInputBlocked[playerIdx] = true;
            
            // 이유: 답변 완료 연출을 1초간 보여주기 위함.
            yield return CoroutineData.GetWaitForSeconds(1.0f);
            
            if (_nextMilestones[playerIdx] > targetDistance)
            {
                _playerFinished[playerIdx] = true;
                _isInputBlocked[playerIdx] = false;

                if (env) env.ClearObstaclesForPlayer(playerIdx, 0.5f);

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

        /// <summary>
        /// 목표 거리 도달 감지 및 질문 팝업 이벤트 실행.
        /// </summary>
        /// <param name="playerIdx">플레이어 인덱스</param>
        /// <param name="currentDist">현재 이동 거리</param>
        /// <param name="maxDist">목표 이동 거리</param>
        private void HandlePlayerDistanceChanged(int playerIdx, float currentDist, float maxDist)
        {
            if (_isGameFinished) return;
            if (playerIdx < 0 || playerIdx >= 2) return;

            if (ui) ui.UpdateGauge(playerIdx, currentDist, targetDistance);
            
            // 이유: 10M 단위의 마일스톤에 도달할 때마다 질문을 노출함.
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

        /// <summary>
        /// 질문 팝업 노출 및 페이즈 전환 연출 진행.
        /// </summary>
        /// <param name="playerIdx">플레이어 인덱스</param>
        /// <param name="milestone">도달 거리</param>
        /// <param name="qData">질문 텍스트 데이터</param>
        /// <param name="infoData">추가 안내 텍스트 데이터</param>
        /// <returns>IEnumerator 루틴</returns>
        private IEnumerator QuestionSequenceRoutine(int playerIdx, int milestone, TextSetting qData, TextSetting infoData)
        {
            if (ui) ui.ShowQuestionPopup(playerIdx, milestone, qData, infoData);

            yield return CoroutineData.GetWaitForSeconds(2.0f);

            if (ui) yield return StartCoroutine(ui.ShowQuestionPhase2Routine(playerIdx, 0.5f, milestone));

            _isInputBlocked[playerIdx] = false;
        }

        /// <summary>
        /// 답변 완료 후 플레이어 이동 상태를 복구함.
        /// </summary>
        /// <param name="playerIdx">대상 플레이어 인덱스</param>
        private void ResumePlayer(int playerIdx)
        {
            if (playerIdx < 0 || playerIdx >= 2) return;
            
            _isPlayerPaused[playerIdx] = false;
            _isInputBlocked[playerIdx] = false;
            
            if (ui) ui.HideQuestionPopup(playerIdx, 0.5f);
            
            if (padDotController) padDotController.SetCenterDotsAlpha(playerIdx, 1f);
        }

        /// <summary>
        /// 특정 플레이어의 현재 위치(레인 번호)를 반환.
        /// </summary>
        /// <param name="playerIdx">대상 플레이어 인덱스</param>
        /// <returns>레인 인덱스</returns>
        public int GetCurrentLane(int playerIdx)
        {
            if (playerIdx >= 0 && playerIdx < 2 && players[playerIdx])
                return players[playerIdx].currentLane;
            return 1;
        }

        /// <summary>
        /// 장애물 피격 시 스턴 상태 돌입 처리.
        /// </summary>
        /// <param name="playerIdx">대상 플레이어 인덱스</param>
        public void OnPlayerHit(int playerIdx)
        {
            if (playerIdx >= 0 && playerIdx < 2 && players[playerIdx])
            {
                // 이유: 짧은 시간 안에 피격음이 다수 재생되어 볼륨이 커지는 것을 막음.
                if (Time.time - _lastHitSoundTime > 0.1f)
                {
                    if (SoundManager.Instance) SoundManager.Instance.PlaySFX("달리기_2");
                    _lastHitSoundTime = Time.time;
                }
                players[playerIdx].OnHit(2.0f);
            }
        }

        /// <summary>
        /// 특정 플레이어가 질문에 답하기 위해 멈춰있는 상태인지 확인.
        /// </summary>
        /// <param name="playerIdx">플레이어 인덱스</param>
        /// <returns>정지 여부</returns>
        public bool IsPlayerPaused(int playerIdx)
        {
            if (playerIdx >= 0 && playerIdx < 2)
            {
                return _isPlayerPaused[playerIdx];
            }
            return false;
        }

        /// <summary>
        /// 특정 플레이어가 장애물 피격으로 기절한 상태인지 확인.
        /// </summary>
        /// <param name="playerIdx">플레이어 인덱스</param>
        /// <returns>기절 여부</returns>
        public bool IsPlayerStunned(int playerIdx)
        {
            if (playerIdx >= 0 && playerIdx < 2 && players[playerIdx])
            {
                return players[playerIdx].IsStunned;
            }
            return false;
        }

        /// <summary>
        /// 3, 2, 1 카운트다운 후 달리기 시작.
        /// </summary>
        /// <returns>IEnumerator 루틴</returns>
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

        /// <summary>
        /// 두 명 모두 결승선 도달 시 마무리 연출 후 씬 전환.
        /// </summary>
        /// <returns>IEnumerator 루틴</returns>
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