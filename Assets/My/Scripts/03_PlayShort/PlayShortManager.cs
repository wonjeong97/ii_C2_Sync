using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Text;
using Cysharp.Threading.Tasks;
using Microsoft.Extensions.Logging;
using My.Scripts._02_PlayTutorial.Components;
using My.Scripts._02_PlayTutorial.Data;
using My.Scripts.Core;
using My.Scripts.Global;
using My.Scripts.UI;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using VContainer;
using Wonjeong.Data;
using Wonjeong.UI;
using Wonjeong.Utils;
using ZLogger;

namespace My.Scripts._03_PlayShort
{
    [Serializable]
    public class QuestionSetting
    {
        public TextSetting page1;
        public TextSetting page2;
    }

    [Serializable]
    public class PlayShortData
    {
        public TextSetting playerAName;
        public TextSetting playerBName;

        public TextSetting startText;
        public TextSetting popupInfoText;
        public TextSetting waitingText;
        public TextSetting centerFinishText;

        public QuestionSetting[] questions;
    }

    [Serializable]
    public class PlayShortQuestionData
    {
        public QuestionSetting[] questions;
    }

    /// <summary>
    /// PlayShort 씬의 전반적인 게임 흐름과 플레이어 상태를 제어하는 매니저 클래스.
    /// </summary>
    public class PlayShortManager : MonoBehaviour, IPlayHitHandler
    {
        private const string BASE_PATH_FORMAT = "JSON/{0}/{1}";
        private const string PRIMARY_PATH_FORMAT = "JSON/{0}/Cartridge_{1}/PlayShort_{2}";
        private const string FALLBACK_A_PATH_FORMAT = "JSON/{0}/Cartridge_A/PlayShort_A{1}";
        private const string FALLBACK_A1_PATH_FORMAT = "JSON/{0}/Cartridge_A/PlayShort_A1";

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
        private CancellationTokenSource _managerCts;

        private ILogger<PlayShortManager> _logger;
        private GameManager _gameManager;
        private InputManager _inputManager;
        private SoundManager _soundManager;
        private UIManager _uiManager;

        public bool IsGameStarted => _gameStarted;

        [Inject]
        public void Construct(
            ILogger<PlayShortManager> logger,
            GameManager gameManager,
            InputManager inputManager,
            SoundManager soundManager,
            UIManager uiManager)
        {
            _logger = logger;
            _gameManager = gameManager;
            _inputManager = inputManager;
            _soundManager = soundManager;
            _uiManager = uiManager;
        }

        private void Awake()
        {
            if (!Instance)
            {
                Instance = this;
            }
            else if (Instance.GetInstanceID() != this.GetInstanceID())
            {
                Destroy(gameObject);
            }
        }

        private void Start()
        {
            _managerCts = new CancellationTokenSource();
            InitGame();
        }

        /// <summary>
        /// 게임 초기화 및 언어 설정이 반영된 질문 데이터를 로드함.
        /// </summary>
        private void InitGame()
        {
            string lang = "ko";
            if (_gameManager)
            {
                lang = _gameManager.CurrentLanguage;
            }

            LoadLocalizedQuestions(lang);

            if (!settings || players == null || players.Length < 2)
            {
                if (_logger != null) _logger.ZLogWarning($"필수 세팅 컴포넌트 데이터가 부족합니다.");
                return;
            }

            InitializeQuestionQueues();
            SetupInitialUIAndState();

            SetupPlayersPhysics();

            if (_inputManager) _inputManager.OnPadDown += HandlePadDown;

            SetAutoProgressing(true);
            CancellationToken token = _managerCts.Token;
            StartSequenceAsync(token).Forget();
        }

        private void SetupInitialUIAndState()
        {
            if (ui)
            {
                ui.InitUI(targetDistance);
                if (_gameManager)
                {
                    string nameA = _gameManager.PlayerAName;
                    string nameB = _gameManager.PlayerBName;

                    TextSetting settingA = _data != null ? _data.playerAName : null;
                    TextSetting settingB = _data != null ? _data.playerBName : null;

                    ui.SetPlayerNames(nameA, nameB, settingA, settingB);
                    ui.SetPlayerBalls(_gameManager.GetColorSprite(_gameManager.PlayerAColor), _gameManager.GetColorSprite(_gameManager.PlayerBColor));
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
                countdownText.text = string.Empty;
            }
        }

        private void LoadLocalizedQuestions(string lang)
        {
            string configPath = ZString.Format(BASE_PATH_FORMAT, lang, GameConstants.Path.PlayShort);
            _data = JsonLoader.Load<PlayShortData>(configPath);
            if (_data == null || !_gameManager) return;

            string typeStr = _gameManager.currentUserType.ToString();
            char cartridgeChar = typeStr.Length > 0 ? typeStr[0] : 'A';
            string relationStr = typeStr.Length > 1 ? typeStr.Substring(1) : "1";

            string primaryPath = ZString.Format(PRIMARY_PATH_FORMAT, lang, cartridgeChar, typeStr);
            PlayShortQuestionData qData = JsonLoader.Load<PlayShortQuestionData>(primaryPath);

            if (qData != null && qData.questions != null)
            {
                _data.questions = qData.questions;
                return;
            }

            if (cartridgeChar != 'A')
            {
                string fallbackAPath = ZString.Format(FALLBACK_A_PATH_FORMAT, lang, relationStr);
                PlayShortQuestionData fallbackAData = JsonLoader.Load<PlayShortQuestionData>(fallbackAPath);
                if (fallbackAData != null && fallbackAData.questions != null)
                {
                    _data.questions = fallbackAData.questions;
                    return;
                }
            }

            string fallbackA1Path = ZString.Format(FALLBACK_A1_PATH_FORMAT, lang);
            PlayShortQuestionData fallbackA1Data = JsonLoader.Load<PlayShortQuestionData>(fallbackA1Path);
            if (fallbackA1Data != null && fallbackA1Data.questions != null)
            {
                _data.questions = fallbackA1Data.questions;
            }
        }

        private void SetupPlayersPhysics()
        {
            for (int i = 0; i < 2; i++)
            {
                if (players[i])
                {
                    ConfigurePlayerInstance(i);
                }
            }
        }

        private void ConfigurePlayerInstance(int index)
        {
            Vector2[] lanes = (index == 0) ? settings.p1LanePositions : settings.p2LanePositions;
            PlayerPhysicsConfig physicsConfig = settings.physicsConfig;
            physicsConfig.maxDistance = targetDistance;
            physicsConfig.useMetricDistance = true;
            physicsConfig.metricMultiplier = metricMultiplier;

            players[index].Setup(index, lanes, physicsConfig);
            players[index].OnDistanceChanged -= HandlePlayerDistanceChanged;
            players[index].OnDistanceChanged += HandlePlayerDistanceChanged;

            if (_gameManager)
            {
                ColorData colorData = (index == 0) ? _gameManager.PlayerAColor : _gameManager.PlayerBColor;
                Sprite targetSprite = _gameManager.GetColorSprite(colorData);

                if (targetSprite) players[index].SetCharacterSprite(targetSprite);
                else players[index].SetCharacterColor(_gameManager.GetColorFromData(colorData));
            }
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
                    int rnd = UnityEngine.Random.Range(i, indices.Count);
                    int temp = indices[i];
                    indices[i] = indices[rnd];
                    indices[rnd] = temp;
                }

                _questionQueues[p] = new Queue<int>(indices);
            }
        }

        private void OnDestroy()
        {
            if (_managerCts != null)
            {
                _managerCts.Cancel();
                _managerCts.Dispose();
            }

            if (Instance.GetInstanceID() == this.GetInstanceID()) Instance = null;
            if (_inputManager) _inputManager.OnPadDown -= HandlePadDown;
            
            if (players != null)
            {
                foreach (PlayerController player in players)
                {
                    if (player) player.OnDistanceChanged -= HandlePlayerDistanceChanged;
                }
            }

            if (_gameManager) _gameManager.IsAutoProgressing = false;
        }

        private void Update()
        {
            if (!_gameStarted) return;

            ProcessActivePlayersUpdate();

            float stopLimit = targetDistance + 1.0f;
            float s1 = 0f;
            float s2 = 0f;

            if (Time.deltaTime > 0f)
            {
                s1 = CalculateScrollSpeed(0, stopLimit);
                s2 = CalculateScrollSpeed(1, stopLimit);
            }

            if (env) env.ScrollEnvironment(s1, s2);
        }

        private void ProcessActivePlayersUpdate()
        {
            for (int i = 0; i < 2; i++)
            {
                if (_isPlayerPaused[i])
                {
                    if (players[i]) players[i].ForceStop();
                    continue;
                }

                if (players[i]) players[i].OnUpdate(false, 0f, 0f);
            }
        }

        private float CalculateScrollSpeed(int pIdx, float stopLimit)
        {
            if (!players[pIdx]) return 0f;
            float currentDist = players[pIdx].currentDistance;
            if (currentDist >= stopLimit) return 0f;

            float delta = currentDist - _prevDistances[pIdx];
            _prevDistances[pIdx] = currentDist;
            return (delta / metricMultiplier) / Time.deltaTime;
        }

        private void SetAutoProgressing(bool isAuto)
        {
            if (_gameManager)
            {
                _gameManager.IsAutoProgressing = isAuto;
                if (!isAuto) _gameManager.ResetInactivityTimer();
            }
        }

        private void HandlePadDown(int playerIdx, int laneIdx, int padIdx)
        {
            if (!_gameStarted || _isGameFinished) return;
            if (playerIdx < 0 || playerIdx >= 2 || _isInputBlocked[playerIdx] || _playerFinished[playerIdx]) return;

            PlayerController player = players[playerIdx];
            if (!player) return;

            ExecutePlayerInput(player, playerIdx, laneIdx, padIdx);
        }

        private void ExecutePlayerInput(PlayerController player, int playerIdx, int laneIdx, int padIdx)
        {
            if (_isPlayerPaused[playerIdx])
            {
                if (ui) ui.NotifyInput(playerIdx);
                if (player.HandleInput(laneIdx, padIdx))
                {
                    player.MoveToLane(laneIdx);
                    ProcessAnswerInput(playerIdx, laneIdx);
                }
                return;
            }

            if (player.HandleInput(laneIdx, padIdx))
            {
                player.MoveAndAccelerate(laneIdx);
            }
        }

        private void ProcessAnswerInput(int pIdx, int laneIdx)
        {
            if (laneIdx == 1)
            {
                if (ui) ui.ResetAnswerFeedback(pIdx);
                return;
            }

            bool isYes = (laneIdx == 0);
            ProcessAnswerLogic(pIdx, laneIdx, isYes);

            if (ui && ui.UpdateStepGauge(pIdx, isYes, _playerStepCounts[pIdx]))
            {
                ApplyAnswerStepProcess(pIdx, isYes);
            }
        }

        private void ApplyAnswerStepProcess(int pIdx, bool isYes)
        {
            if (_gameManager)
            {
                string side = (pIdx == 0) ? "left" : "right";
                int qNo = _currentQuestionNumbers[pIdx];
        
                // 문항 내용 가져오기 (로그 한 줄 출력을 위해 줄바꿈 제거)
                string qText = string.Empty;
                if (_data != null && _data.questions != null && qNo > 0 && qNo <= _data.questions.Length)
                {
                    qText = _data.questions[qNo - 1]?.page1?.text ?? string.Empty;
                    qText = qText.Replace("\n", " ").Replace("\r", "");
                }
        
                // 현재 거리 가져오기
                float dist = players[pIdx] ? players[pIdx].currentDistance : 0f;

                _gameManager.SendValueUpdateAPI(qNo, qText, side, isYes ? 1 : 0, dist);
            }

            CancellationToken token = _managerCts.Token;
            AnswerCompleteTaskAsync(pIdx, token).Forget();
        }

        /// <summary>
        /// 플레이어의 질문 답변 선택 방향을 처리하고 전환 시 기존 진행도를 초기화함
        /// </summary>
        private void ProcessAnswerLogic(int pIdx, int laneIdx, bool isYes)
        {
            int oppositeLane = isYes ? 2 : 0;

            if (_lastActiveLane[pIdx] == oppositeLane)
            {
                _playerStepCounts[pIdx] = 0;
                if (ui) ui.UpdateStepGauge(pIdx, !isYes, 0);
            }

            _lastActiveLane[pIdx] = laneIdx;
            if (ui) ui.SetAnswerFeedback(pIdx, isYes);
            _playerStepCounts[pIdx]++;
        }

        private async UniTaskVoid AnswerCompleteTaskAsync(int playerIdx, CancellationToken ct)
        {
            _isInputBlocked[playerIdx] = true;
            await UniTask.Delay(TimeSpan.FromSeconds(1.0f), cancellationToken: ct);

            if (_nextMilestones[playerIdx] > targetDistance)
            {
                TriggerFinishSequence(playerIdx, ct);
            }
            else
            {
                ResumePlayer(playerIdx);
            }
        }

        private void TriggerFinishSequence(int playerIdx, CancellationToken ct)
        {
            _playerFinished[playerIdx] = true;
            _isInputBlocked[playerIdx] = false;

            if (env) env.ClearObstaclesForPlayer(playerIdx, 0.5f);

            if (players[playerIdx])
            {
                players[playerIdx].MoveToLane(1);
                players[playerIdx].SetFinishTaskAsync(ct).Forget();
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
                FinishSequenceAsync(ct).Forget();
            }
        }

        private void HandlePlayerDistanceChanged(int playerIdx, float currentDist, float maxDist)
        {
            if (_isGameFinished) return;
            if (playerIdx < 0 || playerIdx >= 2) return;

            if (ui) ui.UpdateGauge(playerIdx, currentDist, targetDistance);

            if (currentDist >= _nextMilestones[playerIdx] && _nextMilestones[playerIdx] <= targetDistance)
            {
                TriggerQuestionMilestone(playerIdx);
            }
        }

        private void TriggerQuestionMilestone(int playerIdx)
        {
            int milestone = _nextMilestones[playerIdx];
            _nextMilestones[playerIdx] += 10;

            _isPlayerPaused[playerIdx] = true;
            if (players[playerIdx]) players[playerIdx].ForceStop();

            _isInputBlocked[playerIdx] = true;
            if (padDotController) padDotController.SetCenterDotsAlpha(playerIdx, 0f);

            _playerStepCounts[playerIdx] = 0;
            _lastActiveLane[playerIdx] = -1;

            QuestionSetting questionData = DequeueQuestion(playerIdx);

            TextSetting infoData = _data != null ? _data.popupInfoText : null;
            if (_soundManager) _soundManager.PlaySFX("달리기_3");

            CancellationToken token = _managerCts.Token;
            QuestionSequenceTaskAsync(playerIdx, milestone, questionData, infoData, token).Forget();

            if (env) env.RecycleFrameClosestToCamera(playerIdx);
        }

        private QuestionSetting DequeueQuestion(int playerIdx)
        {
            if (_questionQueues[playerIdx] == null || _questionQueues[playerIdx].Count <= 0) return null;
            
            int qIdx = _questionQueues[playerIdx].Dequeue();
            _currentQuestionNumbers[playerIdx] = qIdx + 1;

            if (_data?.questions != null && qIdx < _data.questions.Length)
            {
                return _data.questions[qIdx];
            }
            return null;
        }

        private async UniTaskVoid QuestionSequenceTaskAsync(int playerIdx, int milestone, QuestionSetting qData, TextSetting infoData, CancellationToken ct)
        {
            if (ui) ui.ShowQuestionPopup(playerIdx, milestone, qData?.page1, qData?.page2, infoData);

            await UniTask.Delay(TimeSpan.FromSeconds(2.0f), cancellationToken: ct);

            if (ui) await ui.ShowQuestionPhase2RoutineAsync(playerIdx, 0.5f, milestone);

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
                    if (_soundManager) _soundManager.PlaySFX("달리기_2");
                    _lastHitSoundTime = Time.time;
                }
                players[playerIdx].OnHit(2.0f);
            }
        }

        public bool IsPlayerPaused(int playerIdx) 
        {
            return (playerIdx >= 0 && playerIdx < 2) && (_isPlayerPaused[playerIdx] || IsPlayerStunned(playerIdx));
        }

        public bool IsPlayerStunned(int playerIdx) => (playerIdx >= 0 && playerIdx < 2 && players[playerIdx]) && players[playerIdx].IsStunned;

        private async UniTaskVoid StartSequenceAsync(CancellationToken ct)
        {
            if (ui)
            {
                ui.HideQuestionPopup(0, 0f);
                ui.HideQuestionPopup(1, 0f);
            }

            if (countdownText)
            {
                await DisplayCountdownLoopAsync(ct);
                await DisplayStartTextAsync(ct);
            }

            _gameStarted = true;
            SetAutoProgressing(false);

            await UniTask.Delay(TimeSpan.FromSeconds(1.0f), cancellationToken: ct);
            if (countdownText) countdownText.gameObject.SetActive(false);
        }

        private async UniTask DisplayCountdownLoopAsync(CancellationToken ct)
        {
            if (_soundManager) _soundManager.PlaySFX("공통_10_3초");
            countdownText.gameObject.SetActive(true);
            for (int i = 3; i > 0; i--)
            {
                countdownText.text = i.ToString();
                await UniTask.Delay(TimeSpan.FromSeconds(1.0f), cancellationToken: ct);
            }
        }

        private async UniTask DisplayStartTextAsync(CancellationToken ct)
        {
            if (_data?.startText != null)
            {
                await UniTask.Delay(TimeSpan.FromMilliseconds(300), cancellationToken: ct);
                if (_soundManager) _soundManager.PlaySFX("공통_14");
                if (_uiManager) _uiManager.SetText(countdownText.gameObject, _data.startText);
                else countdownText.text = _data.startText.text;
            }
            else
            {
                countdownText.text = "Start!";
            }
        }

        private async UniTaskVoid FinishSequenceAsync(CancellationToken ct)
        {
            if (_isGameFinished) return;

            _isGameFinished = true;
            SetAutoProgressing(true);

            if (ui)
            {
                ui.HideQuestionPopup(0, 0.5f);
                ui.HideQuestionPopup(1, 0.5f);
                ui.HideWaitingPopups();
        
                TextSetting centerData = _data != null ? _data.centerFinishText : null;
                if (_soundManager) _soundManager.PlaySFX("달리기_4");
                ui.ShowCenterFinishPopup(centerData);
            }

            // 마지막 플레이어의 개별 도착 점프가 묻히지 않도록 1초 대기
            await UniTask.Delay(TimeSpan.FromSeconds(1.0f), cancellationToken: ct);

            // 양쪽 플레이어 모두 전체 완료 세리머니 (점프 후 Idle 복귀)
            foreach (PlayerController player in players)
            {
                if (player) player.SetFinishTaskAsync(ct).Forget();
            }

            // 세리머니를 충분히 볼 수 있도록 기존 5초 대기를 4초로 조정
            await UniTask.Delay(TimeSpan.FromSeconds(4.0f), cancellationToken: ct);

            if (_gameManager) _gameManager.ChangeScene(GameConstants.Scene.PlayLong);
            else SceneManager.LoadScene(GameConstants.Scene.PlayLong);
        }
    }
}