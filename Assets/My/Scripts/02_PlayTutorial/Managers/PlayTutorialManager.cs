using System;
using System.Collections;
using My.Scripts._02_PlayTutorial.Controllers;
using My.Scripts._02_PlayTutorial.Data;
using My.Scripts.Core;
using My.Scripts.Global;
using UnityEngine;
using Wonjeong.Data;
using Wonjeong.Utils;

namespace My.Scripts._02_PlayTutorial.Managers
{
    /// <summary>
    /// 튜토리얼의 진행 단계(Phase)를 정의하는 열거형.
    /// 상태 머신에서 현재 게임의 상태를 구분하는 키로 사용됨.
    /// </summary>
    public enum TutorialPhase
    {
        Intro,          // 인트로 (팝업 연출)
        Phase1Center,  // 중앙 라인 달리기 (기본 조작)
        Phase2Right,   // 우측 라인 이동
        Phase3Left,    // 좌측 라인 이동
        FinalAutoRun,  // 자동 달리기 (피날레 연출)
        Complete        // 튜토리얼 종료
    }

    /// <summary>
    /// 튜토리얼 씬에서 사용하는 UI 텍스트 데이터 구조.
    /// JSON 파일에서 로드된 데이터를 매핑하여 관리함.
    /// </summary>
    [Serializable]
    public class PlayTutorialData
    {
        public TextSetting[] guideTexts;
        public TextSetting phase1SuccessMessage; 
        public TextSetting[] finalTexts;
    }

    /// <summary>
    /// 튜토리얼(Scene 02)의 전체 게임 흐름을 제어하는 메인 매니저.
    /// 플레이어의 입력, 이동 거리, 페이즈 전환, 환경 스크롤 등을 총괄함.
    /// </summary>
    public class PlayTutorialManager : MonoBehaviour
    {
        public static PlayTutorialManager Instance;

        [Header("Settings")]
        [SerializeField] private TutorialSettingsSO settings; // 기획 데이터(밸런스)와 로직을 분리하기 위해 SO 사용

        [Header("Sub Systems")]
        [SerializeField] private PlayTutorialUIManager ui;
        [SerializeField] private PlayTutorialEnvironment env;

        [Header("Players")]
        // 개별 변수(p1, p2) 대신 배열을 사용하여 루프 처리 및 인덱스 기반 접근을 용이하게 함
        [SerializeField] private TutorialPlayerController[] players = new TutorialPlayerController[2];

        private PlayTutorialData _data;
        private TutorialPhase _currentPhase = TutorialPhase.Intro;

        private bool _gameStarted;     
        private bool _isWaitingForRun; 
        private bool _popupFadedOut;
        
        private readonly float[] _phaseDistances = new float[2]; // 각 페이즈별 누적 거리
        private readonly bool[] _phaseCompleted = new bool[2];   // 각 페이즈 완료 여부
        
        private bool _phase1GlobalComplete; // Phase 1은 두 플레이어 모두 완료해야 넘어감
        private bool _routineStarted;       // 코루틴 중복 실행 방지 플래그
        private bool _waitingForFinalHit;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else if (Instance != this) Destroy(gameObject);
        }

        private void Start()
        {
            _data = JsonLoader.Load<PlayTutorialData>(GameConstants.Path.PlayTutorial);
            
            if (settings == null) { Debug.LogError("Settings Missing"); return; }

            // UI 및 환경 초기화에 SO의 설정값을 주입하여 일관된 물리 법칙 적용
            ui.InitUI(settings.physicsConfig.maxDistance);
            env.InitEnvironment();

            // 반복문을 통해 플레이어 초기화 로직을 단일화함 (중복 코드 제거)
            for (int i = 0; i < players.Length; i++)
            {
                if (players[i] != null)
                {
                    // P1, P2에 맞는 라인 좌표와 공통 물리 설정을 주입
                    Vector2[] lanes = (i == 0) ? settings.p1LanePositions : settings.p2LanePositions;
                    players[i].Setup(i, lanes, settings.physicsConfig);

                    // 혹시 모를 중복 구독을 방지하기 위해 해제 후 구독
                    players[i].OnDistanceChanged -= HandlePlayerDistanceChanged;
                    players[i].OnDistanceChanged += HandlePlayerDistanceChanged;
                }
            }

            if (players == null || players.Length < 2 || players[0] == null || players[1] == null)
            {
                Debug.LogError("Tutorial requires exactly two players assigned.");
                enabled = false;
                return;
            }

            if (InputManager.Instance != null) InputManager.Instance.OnPadDown += HandlePadDown;
            
            StartCoroutine(IntroScenario());
        }

        private void OnDestroy()
        {
            if (InputManager.Instance != null) InputManager.Instance.OnPadDown -= HandlePadDown;
            if (Instance == this) Instance = null;

            // 플레이어 이벤트 구독 해제
            if (players != null)
            {
                foreach (var player in players)
                {
                    if (player != null)
                    {
                        player.OnDistanceChanged -= HandlePlayerDistanceChanged;
                    }
                }
            }
        }

        private void Update()
        {
            if (!_gameStarted) return;

            // 마지막 페이즈(자동 달리기) 여부에 따라 목표 속도 결정
            bool isAutoRun = (_currentPhase == TutorialPhase.FinalAutoRun);
            float autoTarget = isAutoRun ? settings.physicsConfig.maxScrollSpeed * settings.autoRunSpeedRatio : 0f;
            
            // 모든 플레이어의 물리 업데이트 처리
            for (int i = 0; i < players.Length; i++)
            {
                if (players[i]) players[i].OnUpdate(isAutoRun, autoTarget, settings.autoRunSmoothTime);
            }

            // 플레이어 속도에 맞춰 배경 환경 스크롤
            float s1 = players[0] ? players[0].currentSpeed : 0f;
            float s2 = players[1] ? players[1].currentSpeed : 0f;
            env.ScrollEnvironment(s1, s2);
        }
        
        private void HandlePlayerDistanceChanged(int playerIdx, float currentDist, float maxDist)
        {
            // UI 게이지 업데이트
            // Phase 1 목표 거리까지만 게이지가 차오르도록 제한(Min)을 둠
            if (ui)
            {
                ui.UpdateGauge(playerIdx, Mathf.Min(currentDist, settings.targetDistancePhase1), maxDist);
            }
        }

        /// <summary>
        /// 입력 매니저로부터 전달받은 패드 입력 처리.
        /// </summary>
        private void HandlePadDown(int playerIdx, int laneIdx, int padIdx)
        {
            // 자동 달리기 중이거나 게임 시작 전에는 입력 무시
            if (!_gameStarted || _currentPhase == TutorialPhase.FinalAutoRun) return; 
            
            // 인덱스 범위 안전성 체크
            if (playerIdx < 0 || playerIdx >= players.Length) return;
            if (laneIdx < 0 || laneIdx > 2) return; 

            var player = players[playerIdx];
            if (player == null) return;

            // 플레이어 컨트롤러가 입력을 처리했다면(양발 입력 성공 등) 게임 로직 수행
            if (player.HandleInput(laneIdx, padIdx))
            {
                ProcessMoveLogic(player, laneIdx);
            }
        }

        /// <summary>
        /// 현재 페이즈에 따라 플레이어의 이동 및 게임 진행 로직을 분기함.
        /// </summary>
        private void ProcessMoveLogic(TutorialPlayerController player, int laneIdx)
        {
            switch (_currentPhase)
            {
                case TutorialPhase.Phase1Center:
                    HandlePhase1(player, laneIdx);
                    break;

                case TutorialPhase.Phase2Right:
                    // 우측 이동 페이즈: 목표 라인 2, 완료 시 Phase2CompletionRoutine 실행
                    // 공통 함수(HandleRunningPhase)를 사용하여 로직 중복을 제거함
                    HandleRunningPhase(player, laneIdx, 2, settings.targetDistancePhase2, Phase2CompletionRoutine);
                    break;

                case TutorialPhase.Phase3Left:
                    // 좌측 이동 페이즈: 목표 라인 0, 완료 시 Phase3CompletionRoutine 실행
                    HandleRunningPhase(player, laneIdx, 0, settings.targetDistancePhase3, Phase3CompletionRoutine);
                    break;
            }
        }

        // --- Phase Handlers ---

        /// <summary>
        /// 페이즈 1(중앙 달리기) 전용 로직.
        /// 두 플레이어가 모두 목표 거리에 도달해야 다음으로 넘어감.
        /// </summary>
        private void HandlePhase1(TutorialPlayerController player, int laneIdx)
        {
            if (laneIdx != 1) return; // 중앙 라인 입력만 허용

            // 목표 거리를 초과하여 이동하지 않도록 제한
            if (player.currentDistance >= settings.targetDistancePhase1) return;

            player.MoveAndAccelerate(1);
            CheckPopupClose();
            
            // 두 플레이어 모두 목표를 달성했는지 체크 (Global Complete)
            if (!_phase1GlobalComplete &&
                players[0].currentDistance >= settings.targetDistancePhase1 && 
                players[1].currentDistance >= settings.targetDistancePhase1)
            {
                _phase1GlobalComplete = true;
                string msg = _data?.phase1SuccessMessage?.text ?? "잘하셨어요.";
                StartCoroutine(SuccessSequenceRoutine(msg));
            }
        }

        /// <summary>
        /// 페이즈 2(우측)와 페이즈 3(좌측)의 공통 로직을 처리하는 헬퍼 함수.
        /// 목표 라인 이동, 거리 누적, 완료 체크 및 다음 연출 실행을 담당함.
        /// </summary>
        /// <param name="player">입력을 처리할 플레이어 컨트롤러 객체</param>
        /// <param name="laneIdx">사용자가 입력한 라인 인덱스</param>
        /// <param name="targetLane">현재 페이즈에서 이동해야 할 목표 라인 인덱스</param>
        /// <param name="targetDist">현재 페이즈의 목표 이동 거리</param>
        /// <param name="nextRoutine">두 플레이어 모두 완료 시 실행할 연출 코루틴 대리자</param>
        private void HandleRunningPhase(TutorialPlayerController player, int laneIdx, int targetLane, float targetDist, Func<IEnumerator> nextRoutine)
        {
            // TutorialPlayerController.cs에 정의된 프로퍼티는 PascalCase(PlayerIndex)임
            int pIdx = player.playerIndex;

            // 이미 해당 플레이어가 페이즈를 완료했거나, 목표하지 않은 엉뚱한 라인을 입력한 경우 무시
            if (_phaseCompleted[pIdx] || laneIdx != targetLane) return;

            // 페이즈 진입 후 첫 유효 입력 시, 화면에 남아있는 안내 팝업을 닫음
            if (!_popupFadedOut)
            {
                _popupFadedOut = true;
                ui.HidePopup(1.0f);
            }
            
            // 해당 플레이어 쪽의 화살표 UI를 끔 (우측 이동(2)이면 true, 좌측(0)이면 false)
            ui.StopArrowFadeOut(pIdx, laneIdx == 2, 1.0f); 

            // 실제 물리 이동 및 가속 처리
            player.MoveAndAccelerate(targetLane);

            // 해당 페이즈에서의 진행 거리 누적 (배열 사용으로 ref 제거됨)
            _phaseDistances[pIdx] += 1f;

            // 개인별 목표 달성 체크
            if (_phaseDistances[pIdx] >= targetDist)
            {
                _phaseCompleted[pIdx] = true;

                // 두 플레이어 모두 완료(Global Complete)했고, 다음 루틴이 아직 실행되지 않았다면 진행
                if (_phaseCompleted[0] && _phaseCompleted[1] && !_routineStarted)
                {
                    _routineStarted = true;
                    StartCoroutine(nextRoutine());
                }
            }
        }

        // --- Helper & Coroutines ---

        /// <summary>
        /// 플레이어가 달리기 시작하면 대기 중이던 안내 팝업을 닫음.
        /// </summary>
        private void CheckPopupClose()
        {
            if (_isWaitingForRun) { _isWaitingForRun = false; ui.HidePopup(1.0f); }
        }

        /// <summary>
        /// 새로운 페이즈를 시작하기 위해 상태 변수들을 초기화함.
        /// </summary>
        private void ResetPhaseState()
        {
            _phaseDistances[0] = 0f;
            _phaseDistances[1] = 0f;
            _phaseCompleted[0] = false;
            _phaseCompleted[1] = false;
            _routineStarted = false;
        }

        private IEnumerator IntroScenario()
        {
            string t1 = (_data?.guideTexts != null && _data.guideTexts.Length > 0) ? _data.guideTexts[0].text : "Start";
            string t2 = (_data?.guideTexts != null && _data.guideTexts.Length > 1) ? _data.guideTexts[1].text : "Next";

            ui.ShowPopupImmediately(t1);
            yield return CoroutineData.GetWaitForSeconds(3.0f);
            yield return StartCoroutine(ui.FadeOutPopupTextAndChange(t2, 1f, 1f));
            
            _isWaitingForRun = true; 
            _gameStarted = true;
            _currentPhase = TutorialPhase.Phase1Center;
        }

        private IEnumerator SuccessSequenceRoutine(string message)
        {
            _currentPhase = TutorialPhase.Intro; // 연출 중 입력 방지를 위해 상태 변경
            yield return StartCoroutine(ui.ShowSuccessText(message, 2.0f));

            if (_data?.guideTexts != null && _data.guideTexts.Length > 3)
            {
                ui.PreparePopup(_data.guideTexts[2].text);
                yield return StartCoroutine(ui.FadeInPopup(1.0f));
                env.FadeInAllObstacles(0, 3, 1.0f);
                yield return CoroutineData.GetWaitForSeconds(3.0f);
                yield return StartCoroutine(ui.FadeOutPopupTextAndChange(_data.guideTexts[3].text, 1f, 1f));
                yield return CoroutineData.GetWaitForSeconds(0.5f);

                // 다음 페이즈(2) 준비
                ResetPhaseState();
                ui.PlayArrow(0, true); ui.PlayArrow(1, true);
                _currentPhase = TutorialPhase.Phase2Right;
            }
        }

        private IEnumerator Phase2CompletionRoutine()
        {
            yield return CoroutineData.GetWaitForSeconds(1.0f);
            players[0].MoveToLane(1); players[1].MoveToLane(1);
            ui.PlayArrow(0, false); ui.PlayArrow(1, false);

            // 다음 페이즈(3) 준비
            ResetPhaseState();
            _currentPhase = TutorialPhase.Phase3Left;
        }

        private IEnumerator Phase3CompletionRoutine()
        {
            yield return CoroutineData.GetWaitForSeconds(1.0f);
            players[0].MoveToLane(1); players[1].MoveToLane(1);

            string msg = (_data != null) ? _data.phase1SuccessMessage.text : "Complete";
            yield return StartCoroutine(ui.ShowSuccessText(msg, 2.0f));

            if (_data != null && _data.guideTexts.Length > 4)
            {
                ui.PreparePopup(_data.guideTexts[4].text);
                StartCoroutine(ui.FadeInPopup(1.0f));
                env.FadeInAllObstacles(3, 1, 1.0f);

                // 피날레 자동 달리기 시작
                _currentPhase = TutorialPhase.FinalAutoRun;
                _waitingForFinalHit = true; 
                yield return CoroutineData.GetWaitForSeconds(settings.autoRunDuration);
                _currentPhase = TutorialPhase.Complete;
            }
        }

        // --- Public Methods ---

        /// <summary>
        /// 외부(장애물 등)에서 특정 플레이어의 현재 라인 정보를 조회할 때 사용.
        /// </summary>
        public int GetCurrentLane(int playerIdx)
        {
            if (playerIdx >= 0 && playerIdx < players.Length && players[playerIdx] != null)
                return players[playerIdx].currentLane;
            return 1;
        }

        /// <summary>
        /// 장애물 충돌 시 호출되어 피격 처리를 수행함.
        /// </summary>
        public void OnPlayerHit(int playerIdx)
        {
            if (playerIdx >= 0 && playerIdx < players.Length && players[playerIdx] != null)
            {
                players[playerIdx].OnHit(2.0f);
                
                // 마지막 자동 달리기 중 충돌 시 추가 연출 트리거
                if (_waitingForFinalHit)
                {
                    _waitingForFinalHit = false;
                    StartCoroutine(FinalTextChangeSequence());
                }
            }
        }

        private IEnumerator FinalTextChangeSequence()
        {
            yield return CoroutineData.GetWaitForSeconds(2.0f);
            if (_data != null && _data.guideTexts.Length > 5)
            {   
                // 팝업 텍스트 변경 (guideTexts[5])
                yield return StartCoroutine(ui.FadeOutPopupTextAndChange(_data.guideTexts[5].text, 1f, 1f));
            }
            
            yield return CoroutineData.GetWaitForSeconds(2.0f);
            
            if (ui != null && _data != null && _data.finalTexts != null && _data.finalTexts.Length > 0)
            {
                yield return StartCoroutine(ui.RunFinalPageSequence(_data.finalTexts));
            }
            
            _currentPhase = TutorialPhase.Complete;
        }
    }
}