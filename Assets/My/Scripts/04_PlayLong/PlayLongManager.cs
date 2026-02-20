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
    /// <summary>
    /// PlayLong 씬의 전체 게임 흐름과 튜토리얼 미션 시퀀스를 관리하는 클래스.
    /// </summary>
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
        [SerializeField] private float stepDecayTime = 0.5f;    // 설정한 시간 동안 상대방이 밟지 않으면 혼자 쌓은 스탭 횟수가 사라짐.

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
            if (Instance == null) Instance = this;
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

            foreach (var p in players)
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
            foreach (var p in players)
                if (p)
                    p.ForceStop();

            yield return StartCoroutine(ui.ShowRedStringStep1(_setting.popupTexts[1]));
            yield return StartCoroutine(ui.BlinkRedString(2, 2.0f));
            yield return StartCoroutine(ui.FadeInSecondLine(_setting.popupTexts[1], 2.0f));

            if (padDotController) padDotController.StartBlinking(new[] { 4, 5, 10, 11 });
            _isInputBlocked = false;
            _isRightMissionActive = true;
            _currentCoopDistance = 0f;

            while (_currentCoopDistance < RequiredRightDistance) yield return null;

            _isRightMissionActive = false;
            foreach (var p in players)
                if (p)
                    p.ForceStop();
            if (padDotController) padDotController.StopBlinking(new[] { 4, 5, 10, 11 });

            yield return CoroutineData.GetWaitForSeconds(0.5f);
            yield return StartCoroutine(ui.ShowPopupSequence(new[] { _setting.popupTexts[2] }, 2.0f, false));

            if (padDotController) padDotController.StartBlinking(new[] { 0, 1, 10, 11 });
            _isLeftMissionActive = true;
            _p1StepCount = _p2StepCount = _syncedStepCount = 0;
            _currentCoopDistance = 0f;

            while (_currentCoopDistance < RequiredLeftDistance) yield return null;

            _isLeftMissionActive = false;
            _isInputBlocked = true;
            foreach (var p in players)
                if (p)
                    p.ForceStop();
            if (padDotController) padDotController.StopBlinking(new[] { 0, 1, 10, 11 });

            yield return StartCoroutine(SpawnCenterObstacleEvent());
        }

        /// <summary>
        /// 중앙 장애물 스폰 이벤트 및 플레이어 대기 로직.
        /// </summary>
        private IEnumerator SpawnCenterObstacleEvent()
        {
            if (!obstacleManager) yield break;

            // 1. 장애물 등장 안내 문구 표시
            if (_setting.popupTexts.Length > 3)
            {
                yield return StartCoroutine(ui.ShowPopupSequence(new[] { _setting.popupTexts[3] }, 2.0f, false));
            }

            // 2. 중앙 장애물 스폰
            obstacleManager.SpawnSingleObstacle(2.0f, 0);
            yield return CoroutineData.GetWaitForSeconds(1.0f);

            // 3. while 루프를 이용한 직접 스크롤 제어
            float totalDistanceToMove = 2.0f;
            float duration = 2.0f;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                float deltaTime = Time.deltaTime;
                elapsed += deltaTime;

                // 매 프레임 이동량을 계산하여 환경 매니저에 전달
                float stepMove = (totalDistanceToMove / duration) * deltaTime;
                if (env) env.ScrollByMeter(stepMove);

                // 플레이어가 스턴 상태(충돌)가 되면 루프 탈출
                if (IsAnyPlayerStunned()) break;

                yield return null;
            }

            yield return CoroutineData.GetWaitForSeconds(2.0f);

            // 4. 환경 리셋 및 준비 단계 진행
            if (env) yield return StartCoroutine(env.SmoothResetEnvironment(1.0f));

            if (_setting.popupTexts.Length > 4)
            {
                yield return StartCoroutine(ui.FadeTransitionTutorialReady(1.0f));
                yield return StartCoroutine(ui.ShowPopupSequence(new[] { _setting.popupTexts[4] }, 1.0f, false));
            }

            ui.StartPopupTextBlinking(0.5f);

            _isInputBlocked = false;
            bool p1Ready = false;
            bool p2Ready = false;
            float readyStartTime = Time.time;

            while (!p1Ready || !p2Ready)
            {
                if (Time.time - readyStartTime > readyWaitTimeout) break;

                if (players != null && players.Length >= 2)
                {
                    bool p1CurrentInput = Input.GetKey(KeyCode.Alpha3) && Input.GetKey(KeyCode.Alpha4);
                    bool p2CurrentInput = Input.GetKey(KeyCode.Alpha9) && Input.GetKey(KeyCode.Alpha0);

                    if (p1CurrentInput && !p1Ready)
                    {
                        p1Ready = true;
                        players[0].MoveToLane(1);
                    }

                    if (p2CurrentInput && !p2Ready)
                    {
                        p2Ready = true;
                        players[1].MoveToLane(1);
                    }
                }

                if (p1Ready && p2Ready) break;

                yield return null;
            }

            ui.StopPopupTextBlinking();
            ui.HideQuestionPopup(0.5f);

            yield return StartCoroutine(StartCountdownSequence());

            StartInGame();
        }

        private IEnumerator StartCountdownSequence()
        {
            for (int i = 3; i > 0; i--)
            {
                ui.SetCenterText(i.ToString(), true);
                yield return CoroutineData.GetWaitForSeconds(1.0f);
            }

            if (_setting.startText != null)
            {
                ui.SetCenterText(_setting.startText);
            }
            else
            {
                Debug.LogError("[PlayLongManager] StartCountdownSequence: _setting.startText is null");
            }

            yield return CoroutineData.GetWaitForSeconds(1.0f);

            ui.SetCenterText("", false);
        }

        private void StartIntroMission()
        {
            if (_setting != null && _setting.popupTexts != null && _setting.popupTexts.Length > 0)
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
            if (IsAnyPlayerStunned()) return; // 스턴 중 입력 무시

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

                    // 2. 동기화 스텝에 맞춰 정확히 1M 단위로 거리 반영
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

                float addMeters = delta * 1.0f; // 정수 단위 이동
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
                    foreach (var p in players)
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
            foreach (var p in players)
            {
                if (p != null)
                {
                    p.OnHit(2.0f);
                }
            }
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
            if (_setting != null && introPage != null) introPage.SetupData(_setting.introPage);
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
            // 중복 실행 방지 및 종료 시퀀스 시작
            if (!_isGameActive) return;
            _isGameActive = false;

            StartCoroutine(FinishGameSequence());
        }

        private IEnumerator FinishGameSequence()
        {
            // 1. 모든 플레이어 정지 처리
            foreach (var p in players)
            {
                if (p) p.ForceStop();
            }

            // 2. 결과에 따른 중앙 팝업 출력
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
                        Debug.LogError("[PlayLongManager] FinishGameSequence _setting or _setting.endText is null");
                    }
                }
                else
                {
                    ui.ShowCenterResultPopup("TIME OVER");
                }
            }

            // 3. 3초간 대기 (결과 확인 시간)
            yield return CoroutineData.GetWaitForSeconds(3.0f);

            // 4. 페이드아웃 및 엔딩 씬 로드
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
            if (playerIdx >= 0 && playerIdx < players.Length && players[playerIdx] != null)
                return players[playerIdx].currentLane;

            return 1;
        }

        private void InitializePlayers()
        {
            if (baseSettings == null) return;

            var config = baseSettings.physicsConfig;
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