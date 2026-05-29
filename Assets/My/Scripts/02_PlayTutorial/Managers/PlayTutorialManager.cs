using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Microsoft.Extensions.Logging;
using My.Scripts._02_PlayTutorial.Data;
using My.Scripts.Core;
using My.Scripts.UI;
using UnityEngine;
using UnityEngine.SceneManagement;
using VContainer;
using Wonjeong.Data;
using Wonjeong.UI;
using Wonjeong.Utils;
using ZLogger;

namespace My.Scripts._02_PlayTutorial.Managers
{
    public enum TutorialPhase
    {
        Intro,
        Phase1Center,
        Phase2Right,
        Phase3Center,
        FinalAutoRun,
        Complete
    }

    [Serializable]
    public class PlayTutorialData
    {
        public TextSetting playerAName;
        public TextSetting playerBName;

        public TextSetting[] guideTexts;
        public TextSetting phase1SuccessMessage;
        public TextSetting[] finalTexts;
    }

    /// <summary>
    /// 튜토리얼 씬의 전반적인 게임 흐름과 플레이어 상태를 제어하는 매니저 클래스.
    /// </summary>
    public class PlayTutorialManager : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] private TutorialSettingsSO settings;

        [Header("Sub Systems")]
        [SerializeField] private PlayTutorialUIManager ui;
        [SerializeField] private PlayTutorialEnvironment env;

        [Header("Dot Controller")]
        [SerializeField] private PadDotController padDotController;

        [Header("Players")]
        [SerializeField] private PlayerController[] players = new PlayerController[2];

        private PlayTutorialData _data;
        private TutorialPhase _currentPhase = TutorialPhase.Intro;

        private bool _gameStarted;
        private bool _isWaitingForRun;
        private bool _popupFadedOut;

        private readonly float[] _phaseDistances = new float[2];
        private readonly bool[] _phaseCompleted = new bool[2];

        private bool _phase1GlobalComplete;
        private bool _routineStarted;
        private bool _waitingForFinalHit;
        private float _lastHitSoundTime = -1f;
        private CancellationTokenSource _managerCts;

        private ILogger<PlayTutorialManager> _logger;
        private GameManager _gameManager;
        private InputManager _inputManager;
        private SoundManager _soundManager;
        private UIManager _uiManager;

        [Inject]
        public void Construct(
            ILogger<PlayTutorialManager> logger,
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

        private void Start()
        {
            _managerCts = new CancellationTokenSource();
            string localizedPath = GameConstants.Path.GetLocalizedPath(GameConstants.Path.PlayTutorial);
            _data = JsonLoader.Load<PlayTutorialData>(localizedPath);

            if (!settings)
            {
                _logger?.ZLogError($"[PlayTutorialManager] TutorialSettingsSO 누락됨.");
                return;
            }

            SetupGameSystem();
        }

        private void SetupGameSystem()
        {
            if (ui)
            {
                ui.InitUI(settings.physicsConfig.maxDistance);
                SetupPlayerUI();
            }

            if (env) env.InitEnvironment();
            SetupPlayersPhysics();

            if (_inputManager) _inputManager.OnPadDown += HandlePadDown;

            SetAutoProgressing(true);
            IntroScenarioAsync(_managerCts.Token).Forget();
        }

        private void SetupPlayerUI()
        {
            if (_gameManager)
            {
                string nameA = string.IsNullOrEmpty(_gameManager.PlayerAName) ? "Player A" : _gameManager.PlayerAName;
                string nameB = string.IsNullOrEmpty(_gameManager.PlayerBName) ? "Player B" : _gameManager.PlayerBName;

                ui.SetPlayerNames(nameA, nameB, _data?.playerAName, _data?.playerBName);
                ui.SetPlayerBalls(_gameManager.GetColorSprite(_gameManager.PlayerAColor),
                    _gameManager.GetColorSprite(_gameManager.PlayerBColor));
            }
        }

        private void SetupPlayersPhysics()
        {
            if (players == null) return;

            for (int i = 0; i < players.Length; i++)
            {
                if (!players[i]) continue;

                Vector2[] lanes = (i == 0) ? settings.p1LanePositions : settings.p2LanePositions;
                players[i].Setup(i, lanes, settings.physicsConfig);
                players[i].OnDistanceChanged -= HandlePlayerDistanceChanged;
                players[i].OnDistanceChanged += HandlePlayerDistanceChanged;

                if (_gameManager)
                {
                    ColorData colorData = (i == 0) ? _gameManager.PlayerAColor : _gameManager.PlayerBColor;
                    Sprite targetSprite = _gameManager.GetColorSprite(colorData);
                    if (targetSprite) players[i].SetCharacterSprite(targetSprite);
                    else players[i].SetCharacterColor(_gameManager.GetColorFromData(colorData));
                }
            }
        }

        private void OnDestroy()
        {
            if (_inputManager) _inputManager.OnPadDown -= HandlePadDown;

            if (players != null)
            {
                foreach (PlayerController player in players)
                {
                    if (player) player.OnDistanceChanged -= HandlePlayerDistanceChanged;
                }
            }

            _managerCts?.Cancel();
            _managerCts?.Dispose();
            if (_gameManager) _gameManager.IsAutoProgressing = false;
        }

        private void Update()
        {
            if (!_gameStarted) return;

            bool isAutoRun = (_currentPhase == TutorialPhase.FinalAutoRun);
            float autoTarget = isAutoRun ? settings.physicsConfig.maxScrollSpeed * settings.autoRunSpeedRatio : 0f;

            if (players != null)
            {
                foreach (PlayerController pc in players)
                {
                    if (pc) pc.OnUpdate(isAutoRun, autoTarget, settings.autoRunSmoothTime);
                }
            }

            float s1 = players != null && players.Length > 0 && players[0] ? players[0].currentSpeed : 0f;
            float s2 = players != null && players.Length > 1 && players[1] ? players[1].currentSpeed : 0f;
            if (env) env.ScrollEnvironment(s1, s2);
        }

        private void SetAutoProgressing(bool isAuto)
        {
            if (_gameManager)
            {
                _gameManager.IsAutoProgressing = isAuto;
                if (!isAuto) _gameManager.ResetInactivityTimer();
            }
        }

        private void HandlePlayerDistanceChanged(int playerIdx, float currentDist, float maxDist)
        {
            if (ui) ui.UpdateGauge(playerIdx, Mathf.Min(currentDist, settings.targetDistancePhase1), maxDist);
        }

        private void HandlePadDown(int playerIdx, int laneIdx, int padIdx)
        {
            if (!_gameStarted || _currentPhase == TutorialPhase.FinalAutoRun ||
                _currentPhase == TutorialPhase.Complete) return;
            if (players == null || playerIdx < 0 || playerIdx >= players.Length || !players[playerIdx]) return;

            // 허용되지 않은 레인의 입력이 PlayerController의 발판 교차 상태(_lastPadIdx)를 초기화하는 현상 차단
            if (_currentPhase == TutorialPhase.Phase1Center && laneIdx != 1) return;
            if (_currentPhase == TutorialPhase.Phase2Right && laneIdx != 2) return;
            if (_currentPhase == TutorialPhase.Phase3Center && laneIdx != 1) return;

            PlayerController player = players[playerIdx];
            if (player.HandleInput(laneIdx, padIdx))
            {
                ProcessMoveLogic(player, laneIdx);
            }
        }
        
        private void ProcessMoveLogic(PlayerController player, int laneIdx)
        {
            CancellationToken token = _managerCts.Token;
            switch (_currentPhase)
            {
                case TutorialPhase.Phase1Center:
                    HandlePhase1(player, laneIdx, token);
                    break;
                case TutorialPhase.Phase2Right:
                    HandleRunningPhase(player, laneIdx, 2, settings.targetDistancePhase2, token,
                        Phase2CompletionTaskAsync);
                    break;
                case TutorialPhase.Phase3Center:
                    HandleRunningPhase(player, laneIdx, 1, settings.targetDistancePhase3, token,
                        Phase3CompletionTaskAsync);
                    break;
            }
        }

        private void HandlePhase1(PlayerController player, int laneIdx, CancellationToken token)
        {
            if (laneIdx != 1 || player.currentDistance >= settings.targetDistancePhase1) return;

            player.MoveAndAccelerate(1);
            if (_isWaitingForRun)
            {
                _isWaitingForRun = false;
                if (ui) ui.HidePopup(0.5f);
            }

            if (!_phase1GlobalComplete && players != null && players.Length > 1 && players[0] && players[1])
            {
                if (players[0].currentDistance >= settings.targetDistancePhase1 &&
                    players[1].currentDistance >= settings.targetDistancePhase1)
                {
                    _phase1GlobalComplete = true;
                    SetAutoProgressing(true);
                    string msg = (_data != null && _data.phase1SuccessMessage != null)
                        ? _data.phase1SuccessMessage.text
                        : "잘하셨어요.";
                    SuccessSequenceTaskAsync(msg, token).Forget();
                }
            }
        }

        private void HandleRunningPhase(PlayerController player, int laneIdx, int targetLane, float targetDist,
            CancellationToken token, Func<CancellationToken, UniTask> nextTask)
        {
            int pIdx = player.playerIndex;
            if (_phaseCompleted[pIdx] || laneIdx != targetLane) return;

            if (padDotController)
            {
                int baseIdx = pIdx * 6 + targetLane * 2;
                padDotController.StopBlinking(new int[] { baseIdx, baseIdx + 1 });
            }

            if (!_popupFadedOut)
            {
                _popupFadedOut = true;
                if (ui) ui.HidePopup(1f);
            }

            if (ui) ui.StopArrowFadeOut(pIdx, laneIdx == 2, 1.0f);

            player.MoveAndAccelerate(targetLane);
            _phaseDistances[pIdx] += 1f;

            if (_phaseDistances[pIdx] >= targetDist)
            {
                _phaseCompleted[pIdx] = true;
                if (_phaseCompleted[0] && _phaseCompleted[1] && !_routineStarted)
                {
                    _routineStarted = true;
                    SetAutoProgressing(true);
                    nextTask(token).Forget();
                }
            }
        }

        private async UniTaskVoid IntroScenarioAsync(CancellationToken ct)
        {
            string t1 = (_data?.guideTexts != null && _data.guideTexts.Length > 0) ? _data.guideTexts[0].text : "Start";
            string t2 = (_data?.guideTexts != null && _data.guideTexts.Length > 1) ? _data.guideTexts[1].text : "Next";

            if (ui)
            {
                ui.ShowPopupImmediately(t1);
                await UniTask.Delay(TimeSpan.FromSeconds(3.0f), cancellationToken: ct);
                await ui.FadeOutPopupTextAndChangeAsync(t2, 0.5f, 0.5f);
            }

            _isWaitingForRun = true;
            _gameStarted = true;
            _currentPhase = TutorialPhase.Phase1Center;
            SetAutoProgressing(false);
        }

        private async UniTaskVoid SuccessSequenceTaskAsync(string message, CancellationToken ct)
        {
            _currentPhase = TutorialPhase.Intro;
            if (ui) await ui.ShowSuccessTextAsync(message, 2.0f);

            if (_data?.guideTexts != null && _data.guideTexts.Length > 3)
            {
                if (ui)
                {
                    ui.PreparePopup(_data.guideTexts[2].text);
                    ui.FadeInPopupAsync(0.5f).Forget();
                }

                if (env) env.FadeInAllObstacles(0, 3, 0.5f);
                await UniTask.Delay(TimeSpan.FromSeconds(3.0f), cancellationToken: ct);

                await ui.FadeOutPopupTextAndChangeAsync(_data.guideTexts[3].text, 0.5f, 0.5f);
                await UniTask.Delay(TimeSpan.FromSeconds(1.0f), cancellationToken: ct);

                _phaseDistances[0] = _phaseDistances[1] = 0f;
                _phaseCompleted[0] = _phaseCompleted[1] = false;
                _routineStarted = false;

                if (ui)
                {
                    ui.PlayArrow(0, true);
                    ui.PlayArrow(1, true);
                }

                if (padDotController) padDotController.StartBlinking(new int[] { 4, 5, 10, 11 });

                _currentPhase = TutorialPhase.Phase2Right;
                SetAutoProgressing(false);
            }
        }

        private async UniTask Phase2CompletionTaskAsync(CancellationToken ct)
        {
            await UniTask.Delay(TimeSpan.FromSeconds(1.0f), cancellationToken: ct);
            if (ui)
            {
                ui.PlayArrow(0, false);
                ui.PlayArrow(1, false);
            }

            if (padDotController) padDotController.StartBlinking(new int[] { 2, 3, 8, 9 });

            _phaseDistances[0] = _phaseDistances[1] = 0f;
            _phaseCompleted[0] = _phaseCompleted[1] = false;
            _routineStarted = false;
            _currentPhase = TutorialPhase.Phase3Center;
            SetAutoProgressing(false);
        }

        private async UniTask Phase3CompletionTaskAsync(CancellationToken ct)
        {
            await UniTask.Delay(TimeSpan.FromSeconds(1.0f), cancellationToken: ct);
            if (players != null)
            {
                if (players[0]) players[0].MoveToLane(1);
                if (players[1]) players[1].MoveToLane(1);
            }

            string msg = (_data?.phase1SuccessMessage != null) ? _data.phase1SuccessMessage.text : "Complete";
            if (ui) await ui.ShowSuccessTextAsync(msg, 2.0f);

            if (_data?.guideTexts != null && _data.guideTexts.Length > 4)
            {
                if (ui)
                {
                    ui.PreparePopup(_data.guideTexts[4].text);
                    ui.FadeInPopupAsync(0.5f).Forget();
                }

                if (env) env.FadeInAllObstacles(3, 1, 0.5f);
                await UniTask.Delay(TimeSpan.FromSeconds(1.0f), cancellationToken: ct);

                _currentPhase = TutorialPhase.FinalAutoRun;
                _waitingForFinalHit = true;
                await UniTask.Delay(TimeSpan.FromSeconds(settings.autoRunDuration), cancellationToken: ct);
                _currentPhase = TutorialPhase.Complete;

                if (_waitingForFinalHit)
                {
                    _waitingForFinalHit = false;
                    FinalSequenceAsync(ct).Forget();
                }
            }
        }

        private async UniTaskVoid FinalSequenceAsync(CancellationToken ct)
        {
            await UniTask.Delay(TimeSpan.FromSeconds(2.0f), cancellationToken: ct);
            if (ui && _data?.guideTexts != null && _data.guideTexts.Length > 5)
                await ui.FadeOutPopupTextAndChangeAsync(_data.guideTexts[5].text, 0.5f, 0.5f);
            await UniTask.Delay(TimeSpan.FromSeconds(2.0f), cancellationToken: ct);

            if (ui && _data?.finalTexts != null && _data.finalTexts.Length > 0)
                await ui.RunFinalPageSequenceAsync(_data.finalTexts);

            _currentPhase = TutorialPhase.Complete;
            if (_gameManager) _gameManager.ChangeScene(GameConstants.Scene.PlayShort);
            else SceneManager.LoadScene(GameConstants.Scene.PlayShort);
        }
        
        public void OnPlayerHit(int playerIdx)
        {
            if (players != null && playerIdx >= 0 && playerIdx < players.Length && players[playerIdx])
            {
                if (Time.time - _lastHitSoundTime > 0.1f)
                {
                    if (_soundManager) _soundManager.PlaySFX("달리기_2");
                    _lastHitSoundTime = Time.time;
                }
                players[playerIdx].OnHit(2.0f); // 2초간 스턴
            }
        }

        public bool IsPlayerStunned(int playerIdx)
        {
            if (players != null && playerIdx >= 0 && playerIdx < players.Length && players[playerIdx])
            {
                return players[playerIdx].IsStunned;
            }
            return false;
        }

        public int GetCurrentLane(int playerIdx)
        {
            if (players != null && playerIdx >= 0 && playerIdx < players.Length && players[playerIdx])
            {
                return players[playerIdx].currentLane;
            }
            return 1;
        }
    }
}