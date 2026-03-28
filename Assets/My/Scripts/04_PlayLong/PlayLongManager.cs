using System;
using System.Collections;
using My.Scripts._02_PlayTutorial.Data;
using My.Scripts.Core;
using UnityEngine;
using UnityEngine.SceneManagement;
using Wonjeong.Data;
using Wonjeong.UI;
using Wonjeong.Utils;

namespace My.Scripts._04_PlayLong
{
    /// <summary>
    /// PlayLong(500m 달리기) 씬의 전반적인 게임 흐름, 플레이어 동기화, 물리 및 미션을 관리하는 클래스.
    /// </summary>
    public class PlayLongManager : MonoBehaviour
    {
        [Serializable]
        public class Play500MSetting
        {
            public TextSetting playerAName;
            public TextSetting playerBName;
            
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
        private float _lastHitSoundTime = -1f;

        private const float RequiredIntroDistance = 6.0f;
        private const float RequiredRightDistance = 10.0f;
        private const float RequiredLeftDistance = 6.0f;

        public bool IsGameActive => _isGameActive;

        /// <summary>
        /// 싱글톤 인스턴스 초기화.
        /// </summary>
        private void Awake()
        {
            if (!Instance) Instance = this;
            else if (Instance && Instance.GetInstanceID() != this.GetInstanceID()) Destroy(gameObject);
        }

        /// <summary>
        /// 데이터 로드 및 게임 초기화.
        /// </summary>
        private void Start()
        {
            LoadSettings();
            
            if (ui) 
            {
                ui.InitUI(targetDistance);

                if (GameManager.Instance)
                {
                    string nameA = string.IsNullOrEmpty(GameManager.Instance.PlayerAName) ? "Player A" : GameManager.Instance.PlayerAName;
                    string nameB = string.IsNullOrEmpty(GameManager.Instance.PlayerBName) ? "Player B" : GameManager.Instance.PlayerBName;
                    
                    TextSetting settingA = _setting != null ? _setting.playerAName : null;
                    TextSetting settingB = _setting != null ? _setting.playerBName : null;

                    ui.SetPlayerNames(nameA, nameB, settingA, settingB);

                    // 이유: 팝업 텍스트에 포함된 플레이어 이름 토큰을 실제 이름으로 치환하여 출력함.
                    if (_setting != null && _setting.popupTexts != null)
                    {
                        foreach (TextSetting popupText in _setting.popupTexts)
                        {
                            if (popupText != null && !string.IsNullOrEmpty(popupText.text))
                            {
                                popupText.text = popupText.text.Replace("{nameA}", nameA).Replace("{nameB}", nameB);
                            }
                        }
                    }

                    Sprite spriteA = GameManager.Instance.GetColorSprite(GameManager.Instance.PlayerAColor);
                    if (!spriteA) Debug.LogWarning("Player A 컬러 스프라이트 누락됨.");
                    
                    Sprite spriteB = GameManager.Instance.GetColorSprite(GameManager.Instance.PlayerBColor);
                    if (!spriteB) Debug.LogWarning("Player B 컬러 스프라이트 누락됨.");
                    
                    ui.SetPlayerBalls(spriteA, spriteB);
                }
                else
                {
                    Debug.LogWarning("GameManager 인스턴스 누락됨.");
                }
            }
            else
            {
                Debug.LogWarning("PlayLongUIManager 컴포넌트 누락됨.");
            }
            
            InitializePlayers();
            
            if (InputManager.Instance) InputManager.Instance.OnPadDown += HandlePadDown;
            else Debug.LogWarning("InputManager 인스턴스 누락됨.");

            SetAutoProgressing(true);
            StartCoroutine(InitialFlowRoutine());
        }
        
        /// <summary>
        /// 객체 파괴 시 이벤트 구독 해제.
        /// </summary>
        private void OnDestroy()
        {
            if (InputManager.Instance) InputManager.Instance.OnPadDown -= HandlePadDown;
            if (Instance && Instance.GetInstanceID() == this.GetInstanceID()) Instance = null;

            if (GameManager.Instance)
            {
                GameManager.Instance.IsAutoProgressing = false;
            }
        }

        /// <summary>
        /// 자동 진행 여부에 따른 글로벌 방치 타이머 상태 제어.
        /// </summary>
        /// <param name="isAuto">자동 진행 활성화 여부</param>
        private void SetAutoProgressing(bool isAuto)
        {
            if (GameManager.Instance)
            {
                GameManager.Instance.IsAutoProgressing = isAuto;
                
                // 이유: 수동 조작 구간 진입 시 방치 타이머를 리셋하여 팝업 오작동을 방지함.
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
        /// 게임 진입 시 인트로 페이지 연출을 대기하고 완료 후 미션을 시작함.
        /// </summary>
        /// <returns>IEnumerator 루틴</returns>
        private IEnumerator InitialFlowRoutine()
        {
            if (introPage)
            {
                bool isIntroDone = false;
        
                Action<int> onComplete = (info) => isIntroDone = true;
                introPage.onStepComplete += onComplete;
        
                introPage.OnEnter();
        
                float waitStartTime = Time.time;
                while (!isIntroDone)
                {
                    if (Time.time - waitStartTime > readyWaitTimeout)
                    {
                        Debug.LogWarning("인트로 페이지 타임아웃 발생.");
                        break;
                    }
                    yield return null;
                }
        
                introPage.onStepComplete -= onComplete;
                introPage.OnExit();
            }

            StartIntroMission();
        }

        /// <summary>
        /// 두 플레이어 중 한 명이라도 스턴 상태인지 확인.
        /// </summary>
        /// <returns>스턴 여부</returns>
        public bool IsAnyPlayerStunned()
        {
            if (players == null || players.Length < 2) return true;

            foreach (PlayerController p in players)
            {
                if (p && p.IsStunned) return true;
            }

            return false;
        }

        /// <summary>
        /// 붉은 실 연출 및 방향 전환(우->좌) 미션 시퀀스.
        /// </summary>
        /// <returns>IEnumerator 루틴</returns>
        private IEnumerator RedStringIntroSequence()
        {
            if (_setting == null || _setting.popupTexts == null)
            {
                Debug.LogError("설정 데이터 누락됨.");
                yield break;
            }

            _isInputBlocked = true;
            SetAutoProgressing(true);
            
            foreach (PlayerController p in players)
            {
                if (p) p.ForceStop();
            }

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

            SetAutoProgressing(false);

            while (_currentCoopDistance < RequiredRightDistance) yield return null;

            _isRightMissionActive = false;
            SetAutoProgressing(true);
            
            foreach (PlayerController p in players)
            {
                if (p) p.ForceStop();
            }
                
            if (padDotController) padDotController.StopBlinking(new[] { 4, 5, 10, 11 });

            yield return CoroutineData.GetWaitForSeconds(0.5f);
            
            if (ui) yield return StartCoroutine(ui.ShowPopupSequence(new[] { _setting.popupTexts[2] }, 2.0f, false));

            if (padDotController) padDotController.StartBlinking(new[] { 0, 1, 10, 11 });
            
            _isLeftMissionActive = true;
            _p1StepCount = 0;
            _p2StepCount = 0;
            _syncedStepCount = 0;
            _currentCoopDistance = 0f;

            SetAutoProgressing(false);

            while (_currentCoopDistance < RequiredLeftDistance) yield return null;

            _isLeftMissionActive = false;
            _isInputBlocked = true;
            
            SetAutoProgressing(true);
            
            foreach (PlayerController p in players)
            {
                if (p) p.ForceStop();
            }
                
            if (padDotController) padDotController.StopBlinking(new[] { 0, 1, 10, 11 });

            yield return StartCoroutine(SpawnCenterObstacleEvent());
        }

        /// <summary>
        /// 중앙 장애물 생성 연출 및 준비 상태 확인 시퀀스.
        /// </summary>
        /// <returns>IEnumerator 루틴</returns>
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
            float duration = 1.0f;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                float deltaTime = Time.deltaTime;
                elapsed += deltaTime;

                // 예시 입력: totalDistance(2.0) / duration(1.0) * deltaTime(0.016) -> 결과값 = 0.032 (프레임당 이동 거리)
                float stepMove = (totalDistanceToMove / duration) * deltaTime;
                
                if (env) env.ScrollByMeter(stepMove);
                if (obstacleManager) obstacleManager.ForceMoveActiveObstacles(stepMove);

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

            SetAutoProgressing(false);

            // 로컬 함수 형태로 패드 입력 이벤트 콜백 정의
            Action<int, int, int> onReadyPadDown = (pIdx, lIdx, padIdx) =>
            {
                if (lIdx == 1) 
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

            SetAutoProgressing(true);

            if (ui)
            {
                ui.StopPopupTextBlinking();
                ui.HideQuestionPopup(0.5f);
            }

            yield return StartCoroutine(StartCountdownSequence());
            
            yield return CoroutineData.GetWaitForSeconds(0.1f);
            StartInGame();
        }

        /// <summary>
        /// 3, 2, 1 카운트다운 연출.
        /// </summary>
        /// <returns>IEnumerator 루틴</returns>
        private IEnumerator StartCountdownSequence()
        {
            if (SoundManager.Instance) SoundManager.Instance.PlaySFX("공통_10_3초");
            
            for (int i = 3; i > 0; i--)
            {
                if (ui) ui.SetCenterText(i.ToString(), true);
                yield return CoroutineData.GetWaitForSeconds(1.0f);
            }
            
            yield return CoroutineData.GetWaitForSeconds(0.1f);
            if (SoundManager.Instance) SoundManager.Instance.PlaySFX("공통_14");
            
            if (_setting != null && _setting.startText != null)
            {
                if (ui) ui.SetCenterText(_setting.startText);
            }

            yield return CoroutineData.GetWaitForSeconds(1.0f);

            if (ui) ui.SetCenterText("", false);
        }

        /// <summary>
        /// 발맞춰 달리기 튜토리얼 구간 시작.
        /// </summary>
        private void StartIntroMission()
        {
            if (ui && _setting != null && _setting.popupTexts != null && _setting.popupTexts.Length > 0)
            {   
                if (SoundManager.Instance) SoundManager.Instance.PlaySFX("공통_7");
                StartCoroutine(ui.ShowPopupSequence(new[] { _setting.popupTexts[0] }, 3.0f, false));
            }

            _isIntroMissionActive = true;
            _currentCoopDistance = 0f;
            _p1LastStepTime = Time.time;
            _p2LastStepTime = Time.time;
            
            SetAutoProgressing(false);
        }

        /// <summary>
        /// 플레이어 발판 입력 이벤트 핸들러.
        /// 입력 제한, 미션별 허용 레인, 동기화 로직을 처리함.
        /// </summary>
        private void HandlePadDown(int pIdx, int lIdx, int padIdx)
        {
            if (_isInputBlocked) return;
            if (IsAnyPlayerStunned()) return; 

            bool isAnyActionActive = _isGameActive || _isIntroMissionActive || _isRightMissionActive || _isLeftMissionActive;
            if (!isAnyActionActive) return;

            if (players != null && pIdx >= 0 && pIdx < players.Length && players[pIdx])
            {
                if (players[pIdx].HandleInput(lIdx, padIdx))
                {
                    bool isAnyTutorialMission = _isIntroMissionActive || _isRightMissionActive || _isLeftMissionActive;
                    
                    if (isAnyTutorialMission)
                    {
                        // 이유: 미션 구간에서는 한 플레이어가 과도하게 앞서나가는 것을 방지함.
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

        /// <summary>
        /// 두 플레이어의 발걸음 동기화 상태를 확인하고 전진 처리함.
        /// </summary>
        private void ProcessCoopStepSync()
        {
            // 예시 입력: _p1StepCount(5), _p2StepCount(3) -> currentSynced = 3
            int currentSynced = Mathf.Min(_p1StepCount, _p2StepCount);
            
            if (currentSynced > _syncedStepCount)
            {
                int delta = currentSynced - _syncedStepCount;
                _syncedStepCount = currentSynced;

                // 이유: 한 쌍의 스텝(동기화)당 2.0m 전진함.
                float addMeters = delta * 2.0f; 
                _currentCoopDistance += addMeters;

                if (_isGameActive && ui)
                {
                    ui.UpdateLongCoopGauge(_currentCoopDistance, targetDistance);
                    ui.UpdateDistanceMarkers(_currentCoopDistance);
                }

                if (env) env.ScrollByMeter(addMeters);

                // 미션 완료 조건 검사
                if (_isIntroMissionActive && _currentCoopDistance >= RequiredIntroDistance)
                {
                    _isIntroMissionActive = false;
                    
                    foreach (PlayerController p in players)
                    {
                        if (p) p.ForceStop();
                    }
                    
                    _p1StepCount = 0;
                    _p2StepCount = 0;
                    _syncedStepCount = 0;
                    
                    StartCoroutine(RedStringIntroSequence());
                }
                else if (_isGameActive && _currentCoopDistance >= targetDistance)
                {
                    FinishGame();
                }
            }
        }

        /// <summary>
        /// 중앙 장애물에 두 플레이어가 묶인 상태로 충돌했을 때 일괄 피격 처리.
        /// </summary>
        public void OnBothPlayersHit()
        {   
            // 이유: 짧은 시간 안에 피격음이 겹쳐 볼륨이 과도해지는 것을 방지함.
            if (Time.time - _lastHitSoundTime > 0.1f)
            {
                if (SoundManager.Instance) SoundManager.Instance.PlaySFX("달리기_2");
                _lastHitSoundTime = Time.time;
            }
            
            if (players == null) return;
            
            foreach (PlayerController p in players)
            {
                if (p) p.OnHit(2.0f);
            }
        }

        /// <summary>
        /// 매 프레임 상태 업데이트.
        /// </summary>
        private void Update()
        {
            CheckStepDecay();
            
            bool isPhysicsActive = (_isGameActive || _isIntroMissionActive || _isRightMissionActive || _isLeftMissionActive) && !_isInputBlocked;
                
            if (isPhysicsActive)
            {
                // # TODO: 루프 내 반복되는 객체 배열 접근 최적화 고려.
                foreach (PlayerController p in players)
                {
                    if (p) p.OnUpdate(false, 0, 0);
                }
            }

            if (!_isGameActive) return;

            _currentTime -= Time.deltaTime;
            
            if (ui) ui.UpdateTimer(_currentTime);
            if (_currentTime <= 0) FinishGame();
        }

        /// <summary>
        /// 특정 시간 동안 추가 입력이 없으면 초과 스텝을 동기화 수치로 롤백함.
        /// </summary>
        private void CheckStepDecay()
        {
            float now = Time.time;
            
            // 이유: 한 플레이어가 먼저 발판을 여러 번 밟고 멈췄을 때, 스텝 차이가 유지되어 추후 불합리하게 전진하는 현상 방지.
            if (now - _p1LastStepTime > stepDecayTime && _p1StepCount > _syncedStepCount)
            {
                _p1StepCount = _syncedStepCount;
            }
            if (now - _p2LastStepTime > stepDecayTime && _p2StepCount > _syncedStepCount)
            {
                _p2StepCount = _syncedStepCount;
            }
        }

        /// <summary>
        /// JSON 설정 데이터 로드.
        /// </summary>
        private void LoadSettings()
        {
            _setting = JsonLoader.Load<Play500MSetting>(GameConstants.Path.PlayLong);
            if (_setting != null && introPage) introPage.SetupData(_setting.introPage);
        }

        /// <summary>
        /// 본격적인 500m 달리기 모드 시작.
        /// </summary>
        private void StartInGame()
        {   
            if (SoundManager.Instance) SoundManager.Instance.PlaySFX("공통_15_60초");   
            
            _currentTime = timeLimit;
            _isGameActive = true;
            _p1StepCount = 0;
            _p2StepCount = 0;
            _syncedStepCount = 0;
            _currentCoopDistance = 0f;
            _p1LastStepTime = Time.time;
            _p2LastStepTime = Time.time;
            
            if (obstacleManager) obstacleManager.GenerateProgressiveObstacles();
            else Debug.LogWarning("obstacleManager 누락됨.");
            
            SetAutoProgressing(false);
        }

        /// <summary>
        /// 거리 달성 또는 시간 초과로 인한 게임 종료 처리 진입.
        /// </summary>
        private void FinishGame()
        {
            if (!_isGameActive) return;
            _isGameActive = false;

            StartCoroutine(FinishGameSequence());
        }

        /// <summary>
        /// 게임 종료 연출 및 엔딩 씬 전환.
        /// </summary>
        /// <returns>IEnumerator 루틴</returns>
        private IEnumerator FinishGameSequence()
        {
            SetAutoProgressing(true);

            // 이유: 게임이 종료(성공/실패)되었으므로 시야를 가리지 않도록 남은 장애물들을 모두 지워줌.
            if (env) env.ClearObstacles(0.5f);

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
                        if (SoundManager.Instance) SoundManager.Instance.PlaySFX("달리기_4");
                        ui.ShowCenterResultPopup(_setting.endText);
                    }
                    else
                    {
                        ui.ShowCenterResultPopup("SUCCESS");
                        Debug.LogWarning("엔딩 텍스트 설정 데이터가 없음.");
                    }
                }
                else
                {   
                    if (SoundManager.Instance) SoundManager.Instance.PlaySFX("공통_18");
                    ui.ShowCenterResultPopup("TIME OVER");
                }
            }

            yield return CoroutineData.GetWaitForSeconds(3.0f);

            if (GameManager.Instance)
            {   
                // 이유: 엔딩 씬에서 도달 거리에 따른 분기 처리를 위해 데이터를 저장함.
                GameManager.Instance.lastPlayDistance = _currentCoopDistance;
                GameManager.Instance.ChangeScene(GameConstants.Scene.Ending); 
            }
            else
            {
                SceneManager.LoadScene(GameConstants.Scene.Ending);
            }
        }
        
        /// <summary>
        /// 특정 플레이어의 현재 레인 인덱스 조회.
        /// </summary>
        /// <param name="playerIdx">플레이어 인덱스</param>
        /// <returns>레인 인덱스</returns>
        public int GetCurrentLane(int playerIdx)
        {
            if (players != null && playerIdx >= 0 && playerIdx < players.Length && players[playerIdx])
                return players[playerIdx].currentLane;

            return 1;
        }

        /// <summary>
        /// 플레이어 캐릭터 객체의 설정(색상, 레인 위치 등)을 초기화함.
        /// </summary>
        private void InitializePlayers()
        {
            if (!baseSettings)
            {
                Debug.LogWarning("baseSettings 누락됨.");
                return;
            }

            PlayerPhysicsConfig config = baseSettings.physicsConfig;
            config.maxDistance = targetDistance;
            
            if (players != null)
            {
                for (int i = 0; i < players.Length; i++)
                {
                    if (players[i])
                    {
                        Vector2[] lanes = (i == 0) ? p1LongLanePositions : p2LongLanePositions;
                        players[i].Setup(i, lanes, config);

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
                                Debug.LogWarning($"Player {i} 대상 스프라이트 누락됨. 색상 틴트로 대체.");
                                Color targetColor = GameManager.Instance.GetColorFromData(colorData);
                                players[i].SetCharacterColor(targetColor);
                            }
                        }
                    }
                }
            }
        }
    }
}