using System;
using System.Threading;
using Cysharp.Text;
using Cysharp.Threading.Tasks;
using Microsoft.Extensions.Logging;
using My.Scripts._02_PlayTutorial.Components; // IPlayHitHandler 인터페이스 참조
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

namespace My.Scripts._04_PlayLong
{
    // IPlayHitHandler 구현 추가
    public class PlayLongManager : MonoBehaviour, IPlayHitHandler
    {
        [Serializable]
        public class PlayLongSetting
        {
            public TextSetting playerAName;
            public TextSetting playerBName;
            public IntroPageData introPage;
            public TextSetting[] popupTexts;
            public TextSetting startText;
            public TextSetting endText;
        }

        [Header("Game Settings")]
        [SerializeField] private float targetDistance = 500f;
        [SerializeField] private float timeLimit = 60f;
        [SerializeField] private float readyWaitTimeout = 30f;
        [SerializeField] private TutorialSettingsSO baseSettings;
        [SerializeField] private float stepDecayTime = 0.5f;

        [Header("References")]
        [SerializeField] private Vector2[] p1LongLanePositions;
        [SerializeField] private Vector2[] p2LongLanePositions;
        [SerializeField] private PlayLongUIManager ui;
        [SerializeField] private Page_Intro introPage;
        [SerializeField] private PadDotController padDotController;
        [SerializeField] private PlayerController[] players;
        [SerializeField] private PlayLongEnvironment env;
        [SerializeField] private PlayLongObstacleManager obstacleManager;

        private PlayLongSetting _setting;
        private bool _isGameActive;
        private float _currentTime;
        private CancellationTokenSource _cts;

        private bool _isIntroMissionActive, _isRightMissionActive, _isLeftMissionActive, _isInputBlocked, _isIntroDone;
        private bool _isP1Ready;
        private bool _isP2Ready;
        private int _p1StepCount, _p2StepCount, _syncedStepCount;
        private float _currentCoopDistance, _p1LastStepTime, _p2LastStepTime, _lastHitSoundTime = -1f;

        private const float RequiredIntroDistance = 6.0f;
        private const float RequiredRightDistance = 10.0f;
        private const float RequiredLeftDistance = 6.0f;

        private ILogger<PlayLongManager> _logger;
        private GameManager _gameManager;
        private InputManager _inputManager;
        private SoundManager _soundManager;
        private UIManager _uiManager;
        private IObjectResolver _resolver;

        public bool IsGameActive => _isGameActive;

        [Inject]
        public void Construct(ILogger<PlayLongManager> logger, GameManager gameManager,
            InputManager inputManager, SoundManager soundManager, UIManager uiManager, IObjectResolver resolver)
        {
            _logger = logger;
            _gameManager = gameManager;
            _inputManager = inputManager;
            _soundManager = soundManager;
            _uiManager = uiManager;
            _resolver = resolver;
        }

        private void Start()
        {
            _cts = new CancellationTokenSource();

            if (ui)
            {
                _resolver.Inject(ui);
            }

            LoadSettings();
            InitializeUI();
            InitializePlayers();

            _inputManager.OnPadDown += HandlePadDown;
            SetAutoProgressing(true);

            _isInputBlocked = true;
            RunInitialFlowAsync(_cts.Token).Forget();
        }

        private void Update()
        {
            CheckStepDecay();
            bool isPhysicsActive =
                (_isGameActive || _isIntroMissionActive || _isRightMissionActive || _isLeftMissionActive) &&
                !_isInputBlocked;
            if (isPhysicsActive)
                foreach (var p in players)
                    p?.OnUpdate(false, 0, 0);

            if (_isGameActive)
            {
                _currentTime -= Time.deltaTime;
                ui?.UpdateTimer(_currentTime);
                if (_currentTime <= 0) FinishGame();
            }
        }

        private void OnDestroy()
        {
            _inputManager.OnPadDown -= HandlePadDown;
            _cts?.Cancel();
            _cts?.Dispose();
            if (_gameManager) _gameManager.IsAutoProgressing = false;
        }

        /// <summary>
        /// 게임 설정 데이터를 로드하고 인트로 페이지에 의존성 및 데이터를 주입함
        /// </summary>
        private void LoadSettings()
        {
            string lang = _gameManager ? _gameManager.CurrentLanguage : "ko";
            string localizedPath = GameConstants.Path.GetLocalizedPath(GameConstants.Path.PlayLong, lang);
            _setting = JsonLoader.Load<PlayLongSetting>(localizedPath);

            if (introPage)
            {
                // OnEnter 호출 및 데이터 셋업 전에 의존성(UIManager, GameManager 등) 강제 주입
                _resolver.Inject(introPage);

                if (_setting != null)
                {
                    introPage.SetupData(_setting.introPage);
                }
            }
        }

        private void InitializeUI()
        {
            if (!ui) return;

            ui.InitUI(targetDistance);
            string nameA = string.IsNullOrEmpty(_gameManager.PlayerAName) ? "Player A" : _gameManager.PlayerAName;
            string nameB = string.IsNullOrEmpty(_gameManager.PlayerBName) ? "Player B" : _gameManager.PlayerBName;

            ui.SetPlayerNames(nameA, nameB, _setting?.playerAName, _setting?.playerBName);
            if (_setting?.popupTexts != null)
            {
                foreach (TextSetting pt in _setting.popupTexts)
                    if (pt != null)
                        pt.text = pt.text.Replace("{nameA}", nameA).Replace("{nameB}", nameB);
            }

            ui.SetPlayerBalls(_gameManager.GetColorSprite(_gameManager.PlayerAColor),
                _gameManager.GetColorSprite(_gameManager.PlayerBColor));
        }

        private void InitializePlayers()
        {
            var config = baseSettings.physicsConfig;
            config.maxDistance = targetDistance;
            for (int i = 0; i < players.Length; i++)
            {
                if (players[i])
                {
                    players[i].Setup(i, (i == 0) ? p1LongLanePositions : p2LongLanePositions, config);
                    var colorData = (i == 0) ? _gameManager.PlayerAColor : _gameManager.PlayerBColor;
                    var sprite = _gameManager.GetColorSprite(colorData);
                    if (sprite) players[i].SetCharacterSprite(sprite);
                    else players[i].SetCharacterColor(_gameManager.GetColorFromData(colorData));
                }
            }
        }

        private async UniTaskVoid RunInitialFlowAsync(CancellationToken ct)
        {
            if (introPage)
            {
                _isIntroDone = false;
                introPage.onStepComplete = OnIntroStepComplete;
                introPage.OnEnter();
                await UniTask.WaitUntil(IsIntroDone, cancellationToken: ct)
                    .Timeout(TimeSpan.FromSeconds(readyWaitTimeout)).SuppressCancellationThrow();
                introPage.OnExit();
            }

            await StartIntroMissionAsync(ct);
        }

        private bool IsIntroDone() => _isIntroDone;

        private void OnIntroStepComplete(int _) => _isIntroDone = true;

        /// <summary>
        /// 인트로 미션을 시작하고 첫 번째 안내 팝업 출력이 완료될 때까지 입력을 제한함
        /// </summary>
        private async UniTask StartIntroMissionAsync(CancellationToken ct)
        {
            _isInputBlocked = true;

            if (ui && _setting?.popupTexts != null && _setting.popupTexts.Length > 0)
            {
                _soundManager?.PlaySFX("공통_7");
                await ui.ShowPopupSequenceAsync(new[] { _setting.popupTexts[0] }, 1f, false, ct);
            }

            _isIntroMissionActive = true;
            _currentCoopDistance = 0f;
            _p1LastStepTime = Time.time;
            _p2LastStepTime = Time.time;
            SetAutoProgressing(false);
            _isInputBlocked = false;
        }

        private void SetAutoProgressing(bool isAuto)
        {
            if (_gameManager)
            {
                _gameManager.IsAutoProgressing = isAuto;
                if (!isAuto) _gameManager.ResetInactivityTimer();
            }
        }

        private void HandlePadDown(int pIdx, int lIdx, int padIdx)
        {
            if (_isInputBlocked || IsAnyPlayerStunned()) return;
            if (!IsGameModeActive()) return;

            if (players != null && pIdx >= 0 && pIdx < players.Length && players[pIdx].HandleInput(lIdx, padIdx))
            {
                if (IsTutorialMode() && !IsValidTutorialInput(pIdx, lIdx)) return;

                HandleSpecialLaneInput(pIdx, lIdx);

                players[pIdx].MoveAndAccelerate(lIdx);
                UpdatePlayerStepTime(pIdx);
                ProcessCoopStepSync();
            }
        }

        private void UpdatePlayerStepTime(int pIdx)
        {
            if (pIdx == 0)
            {
                _p1LastStepTime = Time.time;
                _p1StepCount++;
            }
            else
            {
                _p2LastStepTime = Time.time;
                _p2StepCount++;
            }
        }

        private bool IsGameModeActive() =>
            _isGameActive || _isIntroMissionActive || _isRightMissionActive || _isLeftMissionActive;

        private bool IsTutorialMode() => _isIntroMissionActive || _isRightMissionActive || _isLeftMissionActive;

        private bool IsValidTutorialInput(int pIdx, int lIdx)
        {
            if ((pIdx == 0 && _p1StepCount > _p2StepCount) || (pIdx == 1 && _p2StepCount > _p1StepCount)) return false;
            if (_isIntroMissionActive && lIdx != 1) return false;
            if (_isRightMissionActive && lIdx != 2) return false;

            return true;
        }

        private void HandleSpecialLaneInput(int pIdx, int lIdx)
        {
            if (_isLeftMissionActive)
            {
                if ((pIdx == 0 && lIdx != 0) || (pIdx == 1 && lIdx != 2)) return;

                padDotController?.StopBlinking(new[] { pIdx * 6 + (lIdx * 2), pIdx * 6 + (lIdx * 2) + 1 });
            }
        }

        private void ProcessCoopStepSync()
        {
            int currentSynced = Mathf.Min(_p1StepCount, _p2StepCount);
            if (currentSynced <= _syncedStepCount) return;

            int delta = currentSynced - _syncedStepCount;
            _syncedStepCount = currentSynced;
            float addMeters = delta * 2.0f;
            _currentCoopDistance += addMeters;

            UpdateTrackingUI();
            env?.ScrollByMeter(addMeters);
            CheckMissionCompletion();
        }

        private void UpdateTrackingUI()
        {
            if (_isGameActive && ui)
            {
                ui.UpdateLongCoopGauge(_currentCoopDistance, targetDistance);
                ui.UpdateDistanceMarkers(_currentCoopDistance);
            }
        }

        private void CheckMissionCompletion()
        {
            if (_isIntroMissionActive && _currentCoopDistance >= RequiredIntroDistance)
            {
                TransitionFromIntroToRedString();
            }
            else if (_isGameActive && _currentCoopDistance >= targetDistance)
            {
                FinishGame();
            }
        }

        private void TransitionFromIntroToRedString()
        {
            _isIntroMissionActive = false;
            foreach (var p in players) p?.ForceStop();
            _p1StepCount = _p2StepCount = _syncedStepCount = 0;
            RunRedStringSequenceAsync(_cts.Token).Forget();
        }

        private async UniTaskVoid RunRedStringSequenceAsync(CancellationToken ct)
        {
            _isInputBlocked = true;
            SetAutoProgressing(true);
            foreach (var p in players)
            {
                if (p) p.ForceStop();
            }

            if (ui)
            {
                await ui.ShowRedStringStep1Async(_setting.popupTexts[1], ct);
                await ui.BlinkRedStringAsync(2, 2.0f, ct);
                await ui.FadeInSecondLineAsync(_setting.popupTexts[1], 2.0f, ct);
            }

            await RunRightMissionAsync(ct);
            await RunLeftMissionAsync(ct);
            await RunCenterObstacleEventAsync(ct);
        }

        private async UniTask RunRightMissionAsync(CancellationToken ct)
        {
            padDotController?.StartBlinking(new[] { 4, 5, 10, 11 });
            _isInputBlocked = false;
            _isRightMissionActive = true;
            _currentCoopDistance = 0f;
            SetAutoProgressing(false);

            await UniTask.WaitUntil(IsRightMissionComplete, cancellationToken: ct);

            _isRightMissionActive = false;
            SetAutoProgressing(true);
            foreach (var p in players)
                if (p)
                    p.ForceStop();
            padDotController?.StopBlinking(new[] { 4, 5, 10, 11 });

            await UniTask.Delay(TimeSpan.FromSeconds(0.5f), cancellationToken: ct);
            if (ui) await ui.ShowPopupSequenceAsync(new[] { _setting.popupTexts[2] }, 2.0f, false, ct);
        }

        private async UniTask RunLeftMissionAsync(CancellationToken ct)
        {
            padDotController?.StartBlinking(new[] { 0, 1, 10, 11 });
            _isLeftMissionActive = true;
            _p1StepCount = _p2StepCount = _syncedStepCount = 0;
            _currentCoopDistance = 0f;
            SetAutoProgressing(false);

            await UniTask.WaitUntil(IsLeftMissionComplete, cancellationToken: ct);

            _isLeftMissionActive = false;
            _isInputBlocked = true;
            SetAutoProgressing(true);
            foreach (var p in players)
                if (p)
                    p.ForceStop();
            padDotController?.StopBlinking(new[] { 0, 1, 10, 11 });
        }

        private async UniTask RunCenterObstacleEventAsync(CancellationToken ct)
        {
            await ui.ShowMissionPopupKeepAsync(_setting.popupTexts[3], ct);

            obstacleManager.SpawnSingleObstacle(2.0f, 0);
            await UniTask.Delay(TimeSpan.FromSeconds(1.0f), cancellationToken: ct);
            await ExecuteObstacleMovementAsync(2.0f, 1.0f, ct);

            await UniTask.Delay(TimeSpan.FromSeconds(2.0f), cancellationToken: ct);
            ui.HideMissionPopupAsync(0.5f, ct).Forget();

            if (env) await env.SmoothResetEnvironmentAsync(1.0f, ct);

            await ui.ShowMissionPopupKeepAsync(_setting.popupTexts[4], ct);
            ui?.StartPopupTextBlinkingAsync(0.5f, ct).Forget();

            if (ui) ui.FadeTransitionTutorialReadyAsync(0.5f, ct).Forget();

            await AwaitPlayerReadyAsync(ct);

            ui?.StopPopupTextBlinking();
            ui?.HideMissionPopupAsync(0.5f, ct).Forget();

            await StartCountdownSequenceAsync(ct);
            StartInGame();
        }

        private async UniTask ExecuteObstacleMovementAsync(float dist, float dur, CancellationToken ct)
        {
            float elapsed = 0f;
            while (elapsed < dur)
            {
                elapsed += Time.deltaTime;
                float stepMove = (dist / dur) * Time.deltaTime;
                env?.ScrollByMeter(stepMove);
                obstacleManager.ForceMoveActiveObstacles(stepMove);
                if (IsAnyPlayerStunned()) break;

                await UniTask.Yield(PlayerLoopTiming.Update, ct);
            }
        }

        private async UniTask AwaitPlayerReadyAsync(CancellationToken ct)
        {
            _isInputBlocked = false;
            _isP1Ready = false;
            _isP2Ready = false;
            SetAutoProgressing(false);

            _inputManager.OnPadDown += OnReadyPadDown;

            await UniTask.WaitUntil(IsPlayersReady, cancellationToken: ct)
                .Timeout(TimeSpan.FromSeconds(readyWaitTimeout)).SuppressCancellationThrow();

            _inputManager.OnPadDown -= OnReadyPadDown;
            SetAutoProgressing(true);
        }

        private void OnReadyPadDown(int pIdx, int lIdx, int padIdx)
        {
            if (lIdx != 1) return;

            if (pIdx == 0) _isP1Ready = true;
            else if (pIdx == 1) _isP2Ready = true;

            players[pIdx]?.MoveToLane(1);
        }

        private bool IsPlayersReady() => _isP1Ready && _isP2Ready;

        private bool IsRightMissionComplete() => _currentCoopDistance >= RequiredRightDistance;

        private bool IsLeftMissionComplete() => _currentCoopDistance >= RequiredLeftDistance;

        private async UniTask StartCountdownSequenceAsync(CancellationToken ct)
        {
            _soundManager?.PlaySFX("공통_10_3초");
            for (int i = 3; i > 0; i--)
            {
                ui?.SetCenterText(i.ToString(), true);
                await UniTask.Delay(TimeSpan.FromSeconds(1.0f), cancellationToken: ct);
            }

            await UniTask.Delay(TimeSpan.FromSeconds(0.3f), cancellationToken: ct);
            _soundManager?.PlaySFX("공통_14");
            if (_setting?.startText != null) ui?.SetCenterText(_setting.startText);
            await UniTask.Delay(TimeSpan.FromSeconds(1.0f), cancellationToken: ct);
            ui?.SetCenterText("", false);
        }

        private void StartInGame()
        {
            _soundManager?.PlaySFX("공통_15_60초");
            _currentTime = timeLimit;
            _isGameActive = true;
            _p1StepCount = 0;
            _p2StepCount = 0;
            _syncedStepCount = 0;
            _currentCoopDistance = 0f;
            _p1LastStepTime = Time.time;
            _p2LastStepTime = Time.time;
            obstacleManager?.GenerateProgressiveObstacles();
            SetAutoProgressing(false);
        }

        private void FinishGame()
        {
            if (!_isGameActive) return;

            _isGameActive = false;
            FinishGameAsync(_cts.Token).Forget();
        }

        /// <summary>
        /// 게임 종료 시퀀스를 실행함
        /// </summary>
        private async UniTaskVoid FinishGameAsync(CancellationToken ct)
        {   
            string nameA = _gameManager ? _gameManager.PlayerAName : "Player A";
            string nameB = _gameManager ? _gameManager.PlayerBName : "Player B";
            
            _logger?.ZLogInformation($"{nameA}(이)와 {nameB}은(는) {(int)_currentCoopDistance}M 만큼 진행함.");
            
            SetAutoProgressing(true);
            if (env) env.ClearObstacles(0.5f);

            StopAllPlayers();
            await ShowResultSequenceAsync(ct);
            TransitionToEndingScene();
        }

        /// <summary>
        /// 모든 플레이어의 이동 및 애니메이션을 강제 정지함
        /// </summary>
        private void StopAllPlayers()
        {
            if (players == null) return;

            // 반복문 내 플레이어 누락 예외 방지
            foreach (PlayerController p in players)
            {
                if (p) p.ForceStop();
            }
        }

        /// <summary>
        /// 게임 성공 여부에 따른 결과 UI 및 사운드 연출을 대기함
        /// </summary>
        private async UniTask ShowResultSequenceAsync(CancellationToken ct)
        {
            if (!ui)
            {
                // UI 객체 누락 시 연출 시간 동기화를 위한 대기
                await UniTask.Delay(TimeSpan.FromSeconds(3.0f), cancellationToken: ct);
                return;
            }

            if (_soundManager) _soundManager.StopSFX();

            bool isSuccess = _currentCoopDistance >= targetDistance;

            if (isSuccess)
            {
                if (_soundManager) _soundManager.PlaySFX("달리기_4");

                if (_setting != null && _setting.endText != null)
                {
                    await ui.ShowCenterResultPopupAsync(_setting.endText, 3.0f, ct);
                }
                else
                {
                    await ui.ShowCenterResultPopupAsync("SUCCESS", 3.0f, ct);
                }
            }
            else
            {
                if (_soundManager) _soundManager.PlaySFX("공통_18");
                await ui.ShowCenterResultPopupAsync("TIME OVER", 3.0f, ct);
            }
        }

        /// <summary>
        /// 플레이 기록을 저장하고 엔딩 씬으로 전환함
        /// </summary>
        private void TransitionToEndingScene()
        {
            if (_gameManager)
            {
                _gameManager.lastPlayDistance = _currentCoopDistance;
                _gameManager.ChangeScene(GameConstants.Scene.Ending);
            }
            else
            {
                SceneManager.LoadScene(GameConstants.Scene.Ending);
            }
        }

        private void CheckStepDecay()
        {
            float now = Time.time;
            if (now - _p1LastStepTime > stepDecayTime && _p1StepCount > _syncedStepCount)
                _p1StepCount = _syncedStepCount;
            if (now - _p2LastStepTime > stepDecayTime && _p2StepCount > _syncedStepCount)
                _p2StepCount = _syncedStepCount;
        }

        public bool IsAnyPlayerStunned()
        {
            if (players == null) return false;

            bool p1Stunned = players[0]?.IsStunned ?? false;
            bool p2Stunned = players[1]?.IsStunned ?? false;
            return p1Stunned || p2Stunned;
        }

        // ==========================================
        // IPlayHitHandler 인터페이스 구현부 추가
        // ==========================================
        public void OnPlayerHit(int playerIdx)
        {
            if (players != null)
            {
                if (Time.time - _lastHitSoundTime > 0.1f)
                {
                    if (_soundManager) _soundManager.PlaySFX("달리기_2");
                    _lastHitSoundTime = Time.time;

                    // 동시 스폰된 바로 옆 장애물 페이드아웃
                    if (obstacleManager) obstacleManager.FadeOutAdjacentObstacles(1.0f);
                }

                if (players.Length > 0 && players[0]) players[0].OnHit(2.0f);
                if (players.Length > 1 && players[1]) players[1].OnHit(2.0f);
            }
        }

        public bool IsPlayerPaused(int playerIdx)
        {
            return IsAnyPlayerStunned();
        }

        public int GetCurrentLane(int playerIdx)
        {
            // 임시로 현재 플레이어의 레인을 반환합니다.
            // (ObstacleHitChecker의 공용 판정 로직과 맞물려 동작합니다)
            if (playerIdx >= 0 && playerIdx < players.Length && players[playerIdx])
            {
                return players[playerIdx].currentLane;
            }

            return 1; // 기본 중앙값
        }
    }
}