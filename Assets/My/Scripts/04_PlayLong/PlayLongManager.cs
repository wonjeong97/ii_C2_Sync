using System;
using System.Collections;
using My.Scripts.Core;
using My.Scripts.Global;
using My.Scripts._02_PlayTutorial.Data;
using UnityEngine;
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
        }

        public static PlayLongManager Instance { get; private set; }

        [Header("Game Settings")] 
        [SerializeField] private float targetDistance = 500f;
        [SerializeField] private float timeLimit = 60f;
        [SerializeField] private float readyWaitTimeout = 30f;
        [SerializeField] private TutorialSettingsSO baseSettings;

        [Tooltip("이 시간 동안 상대방이 밟지 않으면 혼자 쌓은 스텝 횟수가 사라집니다.")] 
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
        private bool _isGameActive = false;
        private float _currentTime;

        private bool _isIntroMissionActive = false;
        private bool _isRightMissionActive = false;
        private bool _isLeftMissionActive = false;
        private bool _isInputBlocked = false;

        private int _p1StepCount = 0;
        private int _p2StepCount = 0;
        private int _syncedStepCount = 0;
        private float _currentCoopDistance = 0f;

        private float _p1LastStepTime;
        private float _p2LastStepTime;

        private const float RequiredIntroDistance = 6.0f;
        private const float RequiredRightDistance = 10.0f;
        private const float RequiredLeftDistance = 6.0f;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);
        }

        private void Start()
        {
            LoadSettings();
            if (ui) ui.InitUI(targetDistance);
            InitializePlayers();
            if (InputManager.Instance) InputManager.Instance.OnPadDown += HandlePadDown;

            StartIntroMission();
        }

        /// <summary>
        /// 모든 플레이어의 유효성을 검사하고 한 명이라도 스턴 상태인지 확인합니다.
        /// </summary>
        private bool IsAnyPlayerStunned()
        {
            if (players == null || players.Length < 2) return true; //

            foreach (var p in players)
            {
                if (p == null || p.IsStunned) return true; //
            }
            return false;
        }

        /// <summary>
        /// 붉은 실 등장 연출 및 우측/좌측 이동 미션을 순차적으로 실행하는 코루틴.
        /// </summary>
        private IEnumerator RedStringIntroSequence()
        {   
            if (_setting == null || _setting.popupTexts == null)
            {
                Debug.LogError("[PlayLongManager] 설정 데이터가 없어 연출을 스킵합니다.");
                StartInGame();
                yield break;
            }
            
            _isInputBlocked = true; 
            foreach (var p in players) if (p) p.ForceStop();

            yield return StartCoroutine(ui.ShowRedStringStep1(_setting.popupTexts[1]));
            yield return StartCoroutine(ui.BlinkRedString(2, 2.0f));
            yield return StartCoroutine(ui.FadeInSecondLine(_setting.popupTexts[1], 2.0f));

            if (padDotController) padDotController.StartBlinking(new[] { 4, 5, 10, 11 });
            _isInputBlocked = false;
            _isRightMissionActive = true;
            _currentCoopDistance = 0f;

            while (_currentCoopDistance < RequiredRightDistance) yield return null;

            _isRightMissionActive = false;
            foreach (var p in players) if (p) p.ForceStop(); 
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
            foreach (var p in players) if (p) p.ForceStop();
            if (padDotController) padDotController.StopBlinking(new[] { 0, 1, 10, 11 });

            yield return StartCoroutine(SpawnCenterObstacleEvent());
        }

        /// <summary>
        /// 중앙 장애물 스폰 및 충돌 후 최종 카운트다운을 진행하는 이벤트.
        /// </summary>
        private IEnumerator SpawnCenterObstacleEvent()
        {
            if (obstacleManager == null) yield break;

            if (_setting.popupTexts.Length > 3)
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

                if (IsAnyPlayerStunned()) break; //
                yield return null;
            }

            yield return CoroutineData.GetWaitForSeconds(2.0f);

            if (_setting.popupTexts.Length > 4)
            {
                yield return StartCoroutine(ui.ShowPopupSequence(new[] { _setting.popupTexts[4] }, 4.0f, false));
            }

            int[] centerPads = { 2, 3, 8, 9 };
            if (padDotController) padDotController.StartBlinking(centerPads);

            _isInputBlocked = false; 
            bool p1Ready = false;
            bool p2Ready = false;
            
            // 무한 루프 방지를 위해 타임아웃 추적 추가
            float readyStartTime = Time.time;

            while (!p1Ready || !p2Ready)
            {
                // 1. 타임아웃 체크: 지정된 시간(readyWaitTimeout) 초과 시 루프 강제 탈출
                if (Time.time - readyStartTime > readyWaitTimeout)
                {
                    Debug.LogWarning("[PlayLongManager] 준비 시간 초과로 인해 자동으로 게임을 시작합니다.");
                    break;
                }

                // 2. 입력 체크
                if (players != null && players.Length >= 2)
                {
                    p1Ready = Input.GetKey(KeyCode.Alpha3) && Input.GetKey(KeyCode.Alpha4);
                    p2Ready = Input.GetKey(KeyCode.Alpha9) && Input.GetKey(KeyCode.Alpha0);
                }

                if (p1Ready && p2Ready) break;
                yield return null;
            }
            if (padDotController) padDotController.StopBlinking(centerPads);
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
                ui.SetCenterText("시작!", true);
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

            bool isAnyActionActive = _isGameActive || _isIntroMissionActive || _isRightMissionActive || _isLeftMissionActive;
            if (!isAnyActionActive) return; //

            if (IsAnyPlayerStunned()) return; //

            if (pIdx >= 0 && pIdx < players.Length && players[pIdx])
            {
                if (players[pIdx].HandleInput(lIdx, padIdx))
                {
                    // --- 협동 균형 체크 (이동 명령 전 수행) ---
                    bool isAnyTutorialMission = _isIntroMissionActive || _isRightMissionActive || _isLeftMissionActive;
                    if (isAnyTutorialMission)
                    {
                        if (pIdx == 0 && _p1StepCount > _p2StepCount) return; //
                        if (pIdx == 1 && _p2StepCount > _p1StepCount) return; //
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

                    if (pIdx == 0) _p1LastStepTime = Time.time;
                    else _p2LastStepTime = Time.time;

                    if (isAnyTutorialMission)
                    {
                        if (pIdx == 0) _p1StepCount++;
                        else _p2StepCount++;

                        int currentSynced = Mathf.Min(_p1StepCount, _p2StepCount);
                        if (currentSynced > _syncedStepCount)
                        {
                            int delta = currentSynced - _syncedStepCount;
                            _syncedStepCount = currentSynced;
                            float addMeters = delta * 1.0f;
                            _currentCoopDistance += addMeters;

                            if (env) env.ScrollByMeter(addMeters);

                            if (_isIntroMissionActive && _currentCoopDistance >= RequiredIntroDistance)
                            {
                                _isIntroMissionActive = false;
                                foreach (var p in players) if (p) p.ForceStop();
                                _p1StepCount = _p2StepCount = _syncedStepCount = 0;
                                StartCoroutine(RedStringIntroSequence());
                            }
                        }
                        return;
                    }

                    if (_isGameActive)
                    {
                        if (pIdx == 0) _p1StepCount++;
                        else _p2StepCount++;
                        ProcessCoopStepSync();
                    }
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

                if (ui)
                {
                    ui.UpdateGauge(0, _currentCoopDistance, targetDistance);
                    ui.UpdateGauge(1, _currentCoopDistance, targetDistance);
                }

                if (env) env.ScrollByMeter(addMeters);
                if (_currentCoopDistance >= targetDistance) FinishGame();
            }
        }

        private void InitializePlayers()
        {
            if (baseSettings == null) return;
            var config = baseSettings.physicsConfig;
            config.maxDistance = targetDistance;

            if (players.Length > 0 && players[0]) players[0].Setup(0, p1LongLanePositions, config);
            if (players.Length > 1 && players[1]) players[1].Setup(1, p2LongLanePositions, config);
        }

        private void Update()
        {
            CheckStepDecay();
            bool isPhysicsActive = (_isGameActive || _isIntroMissionActive || _isRightMissionActive || _isLeftMissionActive) && !_isInputBlocked;

            if (isPhysicsActive)
            {
                foreach (var p in players) if (p) p.OnUpdate(false, 0, 0);
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
        }

        private void FinishGame()
        {
            if (!_isGameActive) return;
            _isGameActive = false;
            Debug.Log($"Game Finished! Total Distance: {_currentCoopDistance}M");
        }

        public int GetCurrentLane(int playerIdx)
        {
            if (playerIdx >= 0 && playerIdx < players.Length && players[playerIdx] != null)
                return players[playerIdx].currentLane;
            return 1;
        }
        
        /// <summary>
        /// 두 플레이어 모두 장애물이나 붉은 실에 걸렸을 때 호출되는 통합 피격 처리 메서드.
        /// </summary>
        public void OnBothPlayersHit()
        {
            // 각 플레이어에게 스턴 적용
            foreach (var p in players)
            {
                if (p != null) p.OnHit(2.0f);
            }

            // 환경 스크롤 즉시 중단 (중복 호출 방지)
            if (env) env.StopScroll();

            // 협동 스텝 카운트 초기화
            _p1StepCount = _p2StepCount = _syncedStepCount; 
        }

        public void OnPlayerHit(int playerIdx)
        {
            if (playerIdx >= 0 && playerIdx < players.Length && players[playerIdx] != null)
            {
                players[playerIdx].OnHit(2.0f);
                if (env) env.StopScroll(); 
                _p1StepCount = _p2StepCount = _syncedStepCount;
            }
        }
    }
}