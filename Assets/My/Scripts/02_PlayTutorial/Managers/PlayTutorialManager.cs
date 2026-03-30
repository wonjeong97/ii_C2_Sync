using System;
using System.Collections;
using My.Scripts._02_PlayTutorial.Data;
using My.Scripts.Core;
using UnityEngine;
using Wonjeong.Data;
using Wonjeong.UI;
using Wonjeong.Utils;

namespace My.Scripts._02_PlayTutorial.Managers
{
    public enum TutorialPhase
    {
        Intro,
        Phase1Center,
        Phase2Right,
        Phase3Left,
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

    public class PlayTutorialManager : MonoBehaviour
    {
        public static PlayTutorialManager Instance;

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

        /// <summary>
        /// 인스턴스 초기화.
        /// </summary>
        private void Awake()
        {
            if (!Instance) Instance = this;
            else if (Instance && Instance.GetInstanceID() != this.GetInstanceID()) Destroy(gameObject);
        }

        /// <summary>
        /// 게임 시작 시 필수 데이터 및 컴포넌트 세팅.
        /// </summary>
        private void Start()
        {
            _data = JsonLoader.Load<PlayTutorialData>(GameConstants.Path.PlayTutorial);

            if (!settings)
            {
                Debug.LogError("[PlayTutorialManager] TutorialSettingsSO 누락됨.");
                return;
            }

            if (ui) 
            {
                ui.InitUI(settings.physicsConfig.maxDistance);
                
                if (GameManager.Instance)
                {
                    string nameA = string.IsNullOrEmpty(GameManager.Instance.PlayerAName) ? "Player A" : GameManager.Instance.PlayerAName;
                    string nameB = string.IsNullOrEmpty(GameManager.Instance.PlayerBName) ? "Player B" : GameManager.Instance.PlayerBName;
                    
                    TextSetting settingA = _data != null ? _data.playerAName : null;
                    TextSetting settingB = _data != null ? _data.playerBName : null;

                    ui.SetPlayerNames(nameA, nameB, settingA, settingB);

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
                Debug.LogWarning("PlayTutorialUIManager 누락됨.");
            }
            
            if (env) env.InitEnvironment();
            else Debug.LogWarning("PlayTutorialEnvironment 누락됨.");

            if (players != null)
            {
                for (int i = 0; i < players.Length; i++)
                {
                    if (players[i])
                    {
                        Vector2[] lanes = (i == 0) ? settings.p1LanePositions : settings.p2LanePositions;
                        players[i].Setup(i, lanes, settings.physicsConfig);

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
                                Debug.LogWarning($"Player {i} 스프라이트 누락. 틴트 색상 적용.");
                                Color targetColor = GameManager.Instance.GetColorFromData(colorData);
                                players[i].SetCharacterColor(targetColor);
                            }
                        }
                    }
                }
            }

            if (players == null || players.Length < 2 || !players[0] || !players[1])
            {
                Debug.LogError("[PlayTutorialManager] 플레이어가 2명 할당되지 않음.");
                enabled = false;
                return;
            }

            if (InputManager.Instance) InputManager.Instance.OnPadDown += HandlePadDown;
            else Debug.LogWarning("InputManager 인스턴스 누락됨.");

            SetAutoProgressing(true);
            StartCoroutine(IntroScenario());
        }

        /// <summary>
        /// 객체 파괴 시 이벤트 구독 해제.
        /// </summary>
        private void OnDestroy()
        {
            if (InputManager.Instance) InputManager.Instance.OnPadDown -= HandlePadDown;
            if (Instance && Instance.GetInstanceID() == this.GetInstanceID()) Instance = null;

            if (players != null)
            {
                foreach (PlayerController player in players)
                {
                    if (player)
                    {
                        player.OnDistanceChanged -= HandlePlayerDistanceChanged;
                    }
                }
            }

            if (GameManager.Instance)
            {
                GameManager.Instance.IsAutoProgressing = false;
            }
        }

        /// <summary>
        /// 매 프레임 플레이어 및 배경 스크롤 상태 업데이트.
        /// </summary>
        private void Update()
        {
            if (!_gameStarted) return;

            // # TODO: Update 내부의 반복 연산을 최적화하기 위해 캐싱 구조 고려 필요.
            bool isAutoRun = (_currentPhase == TutorialPhase.FinalAutoRun);
            // 예: maxScrollSpeed(10) * autoRunSpeedRatio(0.5) = 5
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

        /// <summary>
        /// 자동 진행 상태 토글.
        /// </summary>
        /// <param name="isAuto">자동 진행 여부</param>
        private void SetAutoProgressing(bool isAuto)
        {
            if (GameManager.Instance)
            {
                GameManager.Instance.IsAutoProgressing = isAuto;
                
                // 이유: 수동 조작으로 전환 시 방치 타이머를 리셋하여 팝업 오작동 방지.
                if (!isAuto)
                {
                    GameManager.Instance.ResetInactivityTimer();
                }
            }
            else
            {
                Debug.LogWarning("[PlayTutorialManager] GameManager 인스턴스 누락됨.");
            }
        }

        /// <summary>
        /// 플레이어 이동 거리 갱신 시 UI 업데이트.
        /// </summary>
        /// <param name="playerIdx">플레이어 인덱스</param>
        /// <param name="currentDist">현재 거리</param>
        /// <param name="maxDist">목표 거리</param>
        private void HandlePlayerDistanceChanged(int playerIdx, float currentDist, float maxDist)
        {
            if (ui)
            {
                ui.UpdateGauge(playerIdx, Mathf.Min(currentDist, settings.targetDistancePhase1), maxDist);
            }
        }

        /// <summary>
        /// 발판 입력 감지 및 페이즈별 이동 분기 처리.
        /// </summary>
        /// <param name="playerIdx">플레이어 인덱스</param>
        /// <param name="laneIdx">입력된 레인 인덱스</param>
        /// <param name="padIdx">패드 인덱스</param>
        private void HandlePadDown(int playerIdx, int laneIdx, int padIdx)
        {
            // 이유: 튜토리얼 종료 또는 연출 중 조작 무시.
            if (!_gameStarted || _currentPhase == TutorialPhase.FinalAutoRun || _currentPhase == TutorialPhase.Complete) return;
            if (players == null || playerIdx < 0 || playerIdx >= players.Length) return;
            if (laneIdx < 0 || laneIdx > 2) return;

            PlayerController player = players[playerIdx];
            if (!player) return;

            if (player.HandleInput(laneIdx, padIdx))
            {
                ProcessMoveLogic(player, laneIdx);
            }
        }

        /// <summary>
        /// 현재 페이즈에 맞는 이동 로직 분배.
        /// </summary>
        /// <param name="player">조작 중인 플레이어 객체</param>
        /// <param name="laneIdx">타겟 레인 인덱스</param>
        private void ProcessMoveLogic(PlayerController player, int laneIdx)
        {
            switch (_currentPhase)
            {
                case TutorialPhase.Phase1Center:
                    HandlePhase1(player, laneIdx);
                    break;

                case TutorialPhase.Phase2Right:
                    HandleRunningPhase(player, laneIdx, 2, settings.targetDistancePhase2, Phase2CompletionRoutine);
                    break;

                case TutorialPhase.Phase3Left:
                    HandleRunningPhase(player, laneIdx, 1, settings.targetDistancePhase3, Phase3CompletionRoutine);
                    break;
            }
        }

        /// <summary>
        /// 중앙 달리기 페이즈 처리.
        /// </summary>
        /// <param name="player">조작 중인 플레이어 객체</param>
        /// <param name="laneIdx">타겟 레인 인덱스</param>
        private void HandlePhase1(PlayerController player, int laneIdx)
        {
            // 이유: 페이즈 1은 중앙 이동만 허용함.
            if (laneIdx != 1) return;

            if (player.currentDistance >= settings.targetDistancePhase1) return;

            player.MoveAndAccelerate(1);
            CheckPopupClose();

            if (!_phase1GlobalComplete && players != null && players.Length > 1 && players[0] && players[1])
            {
                // 이유: 두 플레이어 모두 목표 거리에 도달해야 페이즈 통과.
                if (players[0].currentDistance >= settings.targetDistancePhase1 &&
                    players[1].currentDistance >= settings.targetDistancePhase1)
                {
                    _phase1GlobalComplete = true;
                    
                    SetAutoProgressing(true);
                    
                    string msg = _data != null && _data.phase1SuccessMessage != null ? _data.phase1SuccessMessage.text : "잘하셨어요.";
                    StartCoroutine(SuccessSequenceRoutine(msg));
                }
            }
        }

        /// <summary>
        /// 방향 전환 페이즈 처리.
        /// </summary>
        /// <param name="player">조작 중인 플레이어 객체</param>
        /// <param name="laneIdx">입력 레인 인덱스</param>
        /// <param name="targetLane">목표 레인 인덱스</param>
        /// <param name="targetDist">목표 이동 횟수</param>
        /// <param name="nextRoutine">성공 시 실행할 다음 코루틴</param>
        private void HandleRunningPhase(PlayerController player, int laneIdx, int targetLane, float targetDist, Func<IEnumerator> nextRoutine)
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
                    StartCoroutine(nextRoutine());
                }
            }
        }

        /// <summary>
        /// 대기 상태 팝업 강제 닫기.
        /// </summary>
        private void CheckPopupClose()
        {
            if (_isWaitingForRun)
            {
                _isWaitingForRun = false;
                if (ui) ui.HidePopup(0.5f);
            }
        }

        /// <summary>
        /// 페이즈 전환 전 진행 상태 초기화.
        /// </summary>
        private void ResetPhaseState()
        {
            _phaseDistances[0] = 0f;
            _phaseDistances[1] = 0f;
            _phaseCompleted[0] = false;
            _phaseCompleted[1] = false;
            _routineStarted = false;
        }

        /// <summary>
        /// 시작 튜토리얼 팝업 연출.
        /// </summary>
        /// <returns>IEnumerator 루틴</returns>
        private IEnumerator IntroScenario()
        {
            string t1 = (_data != null && _data.guideTexts != null && _data.guideTexts.Length > 0) ? _data.guideTexts[0].text : "Start";
            string t2 = (_data != null && _data.guideTexts != null && _data.guideTexts.Length > 1) ? _data.guideTexts[1].text : "Next";

            if (ui)
            {
                ui.ShowPopupImmediately(t1);
                yield return CoroutineData.GetWaitForSeconds(3.0f);
                yield return StartCoroutine(ui.FadeOutPopupTextAndChange(t2, 0.5f, 0.5f));
            }

            _isWaitingForRun = true;
            _gameStarted = true;
            _currentPhase = TutorialPhase.Phase1Center;
            
            SetAutoProgressing(false);
        }

        /// <summary>
        /// 특정 페이즈 성공 메시지 연출 및 다음 단계 진입 준비.
        /// </summary>
        /// <param name="message">출력할 메시지</param>
        /// <returns>IEnumerator 루틴</returns>
        private IEnumerator SuccessSequenceRoutine(string message)
        {
            _currentPhase = TutorialPhase.Intro; 
            
            if (ui) yield return StartCoroutine(ui.ShowSuccessText(message, 2.0f));

            if (_data != null && _data.guideTexts != null && _data.guideTexts.Length > 3)
            {
                if (ui)
                {
                    ui.PreparePopup(_data.guideTexts[2].text);
                    StartCoroutine(ui.FadeInPopup(0.5f)); 
                }
                
                if (env) env.FadeInAllObstacles(0, 3, 0.5f);
                
                yield return CoroutineData.GetWaitForSeconds(3.0f);
                
                if (ui) yield return StartCoroutine(ui.FadeOutPopupTextAndChange(_data.guideTexts[3].text, 0.5f, 0.5f));
                
                yield return CoroutineData.GetWaitForSeconds(1f);

                ResetPhaseState();
                
                if (ui)
                {
                    ui.PlayArrow(0, true);
                    ui.PlayArrow(1, true);
                }

                if (padDotController)
                {
                    padDotController.StartBlinking(new int[] { 4, 5, 10, 11 });
                }

                _currentPhase = TutorialPhase.Phase2Right;
                
                SetAutoProgressing(false);
            }
        }

        /// <summary>
        /// 우측 이동 미션 완료 처리.
        /// </summary>
        /// <returns>IEnumerator 루틴</returns>
        private IEnumerator Phase2CompletionRoutine()
        {   
            yield return CoroutineData.GetWaitForSeconds(1.0f);

            if (ui)
            {
                ui.PlayArrow(0, false);
                ui.PlayArrow(1, false);
            }

            if (padDotController)
            {
                padDotController.StartBlinking(new int[] { 2, 3, 8, 9 });
            }

            ResetPhaseState();
            _currentPhase = TutorialPhase.Phase3Left;
            
            SetAutoProgressing(false);
        }

        /// <summary>
        /// 좌측 이동 미션 완료 후 최종 자동 달리기 연출 돌입.
        /// </summary>
        /// <returns>IEnumerator 루틴</returns>
        private IEnumerator Phase3CompletionRoutine()
        {
            yield return CoroutineData.GetWaitForSeconds(1.0f);
            
            if (players != null && players.Length > 1)
            {
                if (players[0]) players[0].MoveToLane(1);
                if (players[1]) players[1].MoveToLane(1);
            }

            string msg = (_data != null && _data.phase1SuccessMessage != null) ? _data.phase1SuccessMessage.text : "Complete";
            
            if (ui) yield return StartCoroutine(ui.ShowSuccessText(msg, 2.0f));

            if (_data != null && _data.guideTexts != null && _data.guideTexts.Length > 4)
            {
                if (ui)
                {
                    ui.PreparePopup(_data.guideTexts[4].text);
                    StartCoroutine(ui.FadeInPopup(0.5f));
                }
                
                if (env) env.FadeInAllObstacles(3, 1, 0.5f);
                yield return CoroutineData.GetWaitForSeconds(1.0f);
                
                _currentPhase = TutorialPhase.FinalAutoRun;
                _waitingForFinalHit = true;
                
                yield return CoroutineData.GetWaitForSeconds(settings.autoRunDuration);
                
                _currentPhase = TutorialPhase.Complete;
                
                if (_waitingForFinalHit)
                {
                    _waitingForFinalHit = false;
                    StartCoroutine(FinalTextChangeSequence());
                }
            }
        }

        /// <summary>
        /// 특정 플레이어의 현재 레인 인덱스 반환.
        /// </summary>
        /// <param name="playerIdx">조회할 플레이어 인덱스</param>
        /// <returns>현재 레인 인덱스</returns>
        public int GetCurrentLane(int playerIdx)
        {
            if (players != null && playerIdx >= 0 && playerIdx < players.Length && players[playerIdx])
                return players[playerIdx].currentLane;
            return 1;
        }

        /// <summary>
        /// 장애물 피격 처리.
        /// </summary>
        /// <param name="playerIdx">피격 대상 플레이어 인덱스</param>
        public void OnPlayerHit(int playerIdx)
        {
            if (players != null && playerIdx >= 0 && playerIdx < players.Length && players[playerIdx])
            {
                // 이유: 단기간 내 연속 피격음 재생 방지.
                if (Time.time - _lastHitSoundTime > 0.1f)
                {
                    if (SoundManager.Instance) SoundManager.Instance.PlaySFX("달리기_2");
                    _lastHitSoundTime = Time.time;
                }
                
                players[playerIdx].OnHit(2.0f);

                if (_waitingForFinalHit)
                {
                    _waitingForFinalHit = false;
                    StartCoroutine(FinalTextChangeSequence());
                }
            }
        }

        /// <summary>
        /// 튜토리얼 완료 후 다음 씬 전환 연출.
        /// </summary>
        /// <returns>IEnumerator 루틴</returns>
        private IEnumerator FinalTextChangeSequence()
        {
            yield return CoroutineData.GetWaitForSeconds(2.0f);
            
            if (ui && _data != null && _data.guideTexts != null && _data.guideTexts.Length > 5)
            {
                yield return StartCoroutine(ui.FadeOutPopupTextAndChange(_data.guideTexts[5].text, 0.5f, 0.5f));
            }

            yield return CoroutineData.GetWaitForSeconds(2.0f);

            if (ui && _data != null && _data.finalTexts != null && _data.finalTexts.Length > 0)
            {
                yield return StartCoroutine(ui.RunFinalPageSequence(_data.finalTexts));
            }

            _currentPhase = TutorialPhase.Complete;

            if (GameManager.Instance)
            {
                GameManager.Instance.ChangeScene(GameConstants.Scene.PlayShort);
            }
            else
            {
                UnityEngine.SceneManagement.SceneManager.LoadScene(GameConstants.Scene.PlayShort);
            }
        }
    }
}