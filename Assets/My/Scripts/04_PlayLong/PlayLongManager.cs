using System;
using System.Collections;
using My.Scripts._02_PlayTutorial.Data;
using My.Scripts.Core;
using UnityEngine;
using UnityEngine.SceneManagement;
using Wonjeong.Data;
using Wonjeong.Utils;

namespace My.Scripts._04_PlayLong
{
    public class PlayLongManager : MonoBehaviour
    {
        [Serializable]
        public class Play500MSetting
        {
            public IntroPageData introPage;
            public TextSetting[] popupTexts;
            public TextSetting startText;
            public TextSetting endText;
        }

        public static PlayLongManager Instance { get; private set; }

        [Header("Game Settings")]
        [SerializeField] private float targetDistance = 500f;
        [SerializeField] private float timeLimit = 60f;
        [SerializeField] private float readyWaitTimeout = 30f;
        [SerializeField] private TutorialSettingsSO baseSettings;
        [SerializeField] private float stepDecayTime = 0.5f;   

        [Header("Long Mode Lane Positions")]
        [SerializeField] private Vector2[] p1LongLanePositions;
        [SerializeField] private Vector2[] p2LongLanePositions;

        [Header("Manager References")]
        [SerializeField] private PlayLongUIManager ui;
        [SerializeField] private Page_Intro introPage;
        [SerializeField] private PadDotController padDotController;

        [Header("Players")]
        [SerializeField] private PlayerController[] players;

        [Header("Environment")]
        [SerializeField] private PlayLongEnvironment env;
        [SerializeField] private PlayLongObstacleManager obstacleManager;

        private Play500MSetting _setting;
        private bool _isGameActive;
        private float _currentTime;

        private bool _isIntroMissionActive;
        private bool _isRightMissionActive;
        private bool _isLeftMissionActive;
        private bool _isInputBlocked;

        private int _p1StepCount;
        private int _p2StepCount;
        private int _syncedStepCount;
        private float _currentCoopDistance;

        private float _p1LastStepTime;
        private float _p2LastStepTime;

        private const float RequiredIntroDistance = 6.0f;
        private const float RequiredRightDistance = 10.0f;
        private const float RequiredLeftDistance = 6.0f;

        private void Awake()
        {
            if (!Instance) Instance = this;
            else if (Instance != this) Destroy(gameObject);
        }

        private void Start()
        {
            LoadSettings();
            if (ui) ui.InitUI(targetDistance);
            InitializePlayers();
            if (InputManager.Instance) InputManager.Instance.OnPadDown += HandlePadDown;

            StartIntroMission();
        }

        private bool IsAnyPlayerStunned()
        {
            if (players == null || players.Length < 2) return true;

            foreach (PlayerController p in players)
            {
                if (p && p.IsStunned) return true;
            }

            return false;
        }

        private IEnumerator RedStringIntroSequence()
        {
            if (_setting == null || _setting.popupTexts == null)
            {
                Debug.LogError("[PlayLongManager] 설정 데이터가 없어 연출을 스킵합니다.");
                StartInGame();
                yield break;
            }

            _isInputBlocked = true;
            foreach (PlayerController p in players)
                if (p)
                    p.ForceStop();

            if (ui)
            {
                yield return StartCoroutine(ui.ShowRedStringStep1(_setting.popupTexts[1]));
                yield return StartCoroutine(ui.BlinkRedString(2, 2.0f));
                yield return StartCoroutine(ui.FadeInSecondLine(_setting.popupTexts[1], 2.0f));
            }

            if (padDotController) padDotController.StartBlinking(new[] { 4, 5, 10, 11 });
            _isInputBlocked = false;
            _isRightMissionActive = true;
            _currentCoopDistance = 0f;

            while (_currentCoopDistance < RequiredRightDistance) yield return null;

            _isRightMissionActive = false;
            foreach (PlayerController p in players)
                if (p)
                    p.ForceStop();
            if (padDotController) padDotController.StopBlinking(new[] { 4, 5, 10, 11 });

            yield return CoroutineData.GetWaitForSeconds(0.5f);
            
            if (ui) yield return StartCoroutine(ui.ShowPopupSequence(new[] { _setting.popupTexts[2] }, 2.0f, false));

            if (padDotController) padDotController.StartBlinking(new[] { 0, 1, 10, 11 });
            _isLeftMissionActive = true;
            _p1StepCount = _p2StepCount = _syncedStepCount = 0;
            _currentCoopDistance = 0f;

            while (_currentCoopDistance < RequiredLeftDistance) yield return null;

            _isLeftMissionActive = false;
            _isInputBlocked = true;
            foreach (PlayerController p in players)
                if (p)
                    p.ForceStop();
            if (padDotController) padDotController.StopBlinking(new[] { 0, 1, 10, 11 });

            yield return StartCoroutine(SpawnCenterObstacleEvent());
        }

        private IEnumerator SpawnCenterObstacleEvent()
        {
            if (!obstacleManager) yield break;

            if (ui && _setting != null && _setting.popupTexts != null && _setting.popupTexts.Length > 3)
            {
                yield return StartCoroutine(ui.ShowPopupSequence(new[] { _setting.popupTexts[3] }, 2.0f, false));
            }

            obstacleManager.SpawnSingleObstacle(2.0f, 0);
            yield return CoroutineData.GetWaitForSeconds(1.0f);

            float totalDistanceToMove = 2.0f;
            float duration = 2.0f;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                float deltaTime = Time.deltaTime;
                elapsed += deltaTime;

                float stepMove = (totalDistanceToMove / duration) * deltaTime;
                if (env) env.ScrollByMeter(stepMove);

                if (IsAnyPlayerStunned()) break;

                yield return null;
            }

            yield return CoroutineData.GetWaitForSeconds(2.0f);

            if (env) yield return StartCoroutine(env.SmoothResetEnvironment(1.0f));

            if (ui && _setting != null && _setting.popupTexts != null && _setting.popupTexts.Length > 4)
            {
                yield return StartCoroutine(ui.FadeTransitionTutorialReady(1.0f));
                yield return StartCoroutine(ui.ShowPopupSequence(new[] { _setting.popupTexts[4] }, 1.0f, false));
            }

            if (ui) ui.StartPopupTextBlinking(0.5f);

            _isInputBlocked = false;
            bool p1Ready = false;
            bool p2Ready = false;
            float readyStartTime = Time.time;

            // 로컬 이벤트 핸들러를 통한 대기 로직 처리
            Action<int, int, int> onReadyPadDown = (pIdx, lIdx, padIdx) =>
            {
                if (lIdx == 1) // Center lane
                {
                    if (pIdx == 0 && !p1Ready)
                    {
                        p1Ready = true;
                        if (players != null && players.Length > 0 && players[0]) players[0].MoveToLane(1);
                    }
                    else if (pIdx == 1 && !p2Ready)
                    {
                        p2Ready = true;
                        if (players != null && players.Length > 1 && players[1]) players[1].MoveToLane(1);
                    }
                }
            };

            if (InputManager.Instance) InputManager.Instance.OnPadDown += onReadyPadDown;

            while (!p1Ready || !p2Ready)
            {
                if (Time.time - readyStartTime > readyWaitTimeout) break;
                yield return null;
            }

            if (InputManager.Instance) InputManager.Instance.OnPadDown -= onReadyPadDown;

            if (ui)
            {
                ui.StopPopupTextBlinking();
                ui.HideQuestionPopup(0.5f);
            }

            yield return StartCoroutine(StartCountdownSequence());

            StartInGame();
        }

        private IEnumerator StartCountdownSequence()
        {
            for (int i = 3; i > 0; i--)
            {
                if (ui) ui.SetCenterText(i.ToString(), true);
                yield return CoroutineData.GetWaitForSeconds(1.0f);
            }

            if (_setting != null && _setting.startText != null)
            {
                if (ui) ui.SetCenterText(_setting.startText);
            }
            else
            {
                Debug.LogWarning("[PlayLongManager] StartCountdownSequence: _setting or startText is null");
            }

            yield return CoroutineData.GetWaitForSeconds(1.0f);

            if (ui) ui.SetCenterText("", false);
        }

        private void StartIntroMission()
        {
            if (ui && _setting != null && _setting.popupTexts != null && _setting.popupTexts.Length > 0)
            {
                StartCoroutine(ui.ShowPopupSequence(new[] { _setting.popupTexts[0] }, 3.0f, false));
            }

            _isIntroMissionActive = true;
            _currentCoopDistance = 0f;
            _p1LastStepTime = _p2LastStepTime = Time.time;
        }

        private void HandlePadDown(int pIdx, int lIdx, int padIdx)
        {
            if (_isInputBlocked) return;
            if (IsAnyPlayerStunned()) return; 

            bool isAnyActionActive =
                _isGameActive || _isIntroMissionActive || _isRightMissionActive || _isLeftMissionActive;
            if (!isAnyActionActive) return;

            if (pIdx >= 0 && pIdx < players.Length && players[pIdx])
            {
                if (players[pIdx].HandleInput(lIdx, padIdx))
                {
                    bool isAnyTutorialMission = _isIntroMissionActive || _isRightMissionActive || _isLeftMissionActive;
                    if (isAnyTutorialMission)
                    {
                        if (pIdx == 0 && _p1StepCount > _p2StepCount) return;
                        if (pIdx == 1 && _p2StepCount > _p1StepCount) return;
                    }

                    if (_isIntroMissionActive && lIdx != 1) return;
                    if (_isRightMissionActive && lIdx != 2) return;

                    if (_isLeftMissionActive)
                    {
                        if (pIdx == 0 && lIdx != 0) return;
                        if (pIdx == 1 && lIdx != 2) return;

                        int baseIdx = pIdx * 6 + (lIdx * 2);
                        if (padDotController) padDotController.StopBlinking(new[] { baseIdx, baseIdx + 1 });
                    }

                    players[pIdx].MoveAndAccelerate(lIdx);

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

                    ProcessCoopStepSync();
                }
            }
        }

        private void ProcessCoopStepSync()
        {
            int currentSynced = Mathf.Min(_p1StepCount, _p2StepCount);
            if (currentSynced > _syncedStepCount)
            {
                int delta = currentSynced - _syncedStepCount;
                _syncedStepCount = currentSynced;

                float addMeters = delta * 1.0f; 
                _currentCoopDistance += addMeters;

                if (_isGameActive && ui)
                {
                    ui.UpdateLongCoopGauge(_currentCoopDistance, targetDistance);
                    ui.UpdateDistanceMarkers(_currentCoopDistance);
                }

                if (env) env.ScrollByMeter(addMeters);

                if (_isIntroMissionActive && _currentCoopDistance >= RequiredIntroDistance)
                {
                    _isIntroMissionActive = false;
                    foreach (PlayerController p in players)
                        if (p)
                            p.ForceStop();
                    _p1StepCount = _p2StepCount = _syncedStepCount = 0;
                    StartCoroutine(RedStringIntroSequence());
                }
                else if (_isGameActive && _currentCoopDistance >= targetDistance)
                {
                    FinishGame();
                }
            }
        }

        public void OnBothPlayersHit()
        {
            foreach (PlayerController p in players)
            {
                if (p)
                {
                    p.OnHit(2.0f);
                }
            }
        }

        /// <summary>
        /// 다른 컴포넌트 호환성을 위한 단일 플레이어 충돌 위임 메서드.
        /// </summary>
        public void OnPlayerHit(int playerIdx)
        {
            OnBothPlayersHit();
        }

        private void Update()
        {
            CheckStepDecay();
            bool isPhysicsActive =
                (_isGameActive || _isIntroMissionActive || _isRightMissionActive || _isLeftMissionActive) &&
                !_isInputBlocked;
            if (isPhysicsActive)
            {
                foreach (PlayerController p in players)
                    if (p)
                        p.OnUpdate(false, 0, 0);
            }

            if (!_isGameActive) return;

            _currentTime -= Time.deltaTime;
            if (ui) ui.UpdateTimer(_currentTime);
            if (_currentTime <= 0) FinishGame();
        }

        private void CheckStepDecay()
        {
            float now = Time.time;
            if (now - _p1LastStepTime > stepDecayTime && _p1StepCount > _syncedStepCount)
                _p1StepCount = _syncedStepCount;
            if (now - _p2LastStepTime > stepDecayTime && _p2StepCount > _syncedStepCount)
                _p2StepCount = _syncedStepCount;
        }

        private void LoadSettings()
        {
            _setting = JsonLoader.Load<Play500MSetting>(GameConstants.Path.PlayLong);
            if (_setting != null && introPage) introPage.SetupData(_setting.introPage);
        }

        private void StartInGame()
        {
            _currentTime = timeLimit;
            _isGameActive = true;
            _p1StepCount = _p2StepCount = _syncedStepCount = 0;
            _currentCoopDistance = 0f;
            _p1LastStepTime = _p2LastStepTime = Time.time;
            if (obstacleManager) obstacleManager.GenerateProgressiveObstacles();
        }

        private void FinishGame()
        {
            if (!_isGameActive) return;
            _isGameActive = false;

            StartCoroutine(FinishGameSequence());
        }

        private IEnumerator FinishGameSequence()
        {
            foreach (PlayerController p in players)
            {
                if (p) p.ForceStop();
            }

            if (ui)
            {
                bool isSuccess = _currentCoopDistance >= targetDistance;
                if (isSuccess)
                {
                    if (_setting != null && _setting.endText != null)
                    {
                        ui.ShowCenterResultPopup(_setting.endText);
                    }
                    else
                    {
                        Debug.LogWarning("[PlayLongManager] FinishGameSequence _setting or _setting.endText is null. Using fallback text.");
                        ui.ShowCenterResultPopup("SUCCESS");
                    }
                }
                else
                {
                    ui.ShowCenterResultPopup("TIME OVER");
                }
            }

            yield return CoroutineData.GetWaitForSeconds(3.0f);

            if (GameManager.Instance)
            {
                GameManager.Instance.ChangeScene(GameConstants.Scene.Ending); 
            }
            else
            {
                SceneManager.LoadScene(GameConstants.Scene.Ending);
            }
        }
        
        public int GetCurrentLane(int playerIdx)
        {
            if (playerIdx >= 0 && playerIdx < players.Length && players[playerIdx])
                return players[playerIdx].currentLane;

            return 1;
        }

        private void InitializePlayers()
        {
            if (baseSettings == null) return;

            PlayerPhysicsConfig config = baseSettings.physicsConfig;
            config.maxDistance = targetDistance;
            if (players.Length > 0 && players[0]) players[0].Setup(0, p1LongLanePositions, config);
            if (players.Length > 1 && players[1]) players[1].Setup(1, p2LongLanePositions, config);
        }

        private void OnDestroy()
        {
            if (InputManager.Instance) InputManager.Instance.OnPadDown -= HandlePadDown;
            if (Instance == this) Instance = null;
        }
    }
}