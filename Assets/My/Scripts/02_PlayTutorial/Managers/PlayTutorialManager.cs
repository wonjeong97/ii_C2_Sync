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

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else if (Instance != this) Destroy(gameObject);
        }

        private void Start()
        {
            _data = JsonLoader.Load<PlayTutorialData>(GameConstants.Path.PlayTutorial);

            if (settings == null)
            {
                Debug.LogError("Settings Missing");
                return;
            }

            ui.InitUI(settings.physicsConfig.maxDistance);
            env.InitEnvironment();

            for (int i = 0; i < players.Length; i++)
            {
                if (players[i] != null)
                {
                    Vector2[] lanes = (i == 0) ? settings.p1LanePositions : settings.p2LanePositions;
                    players[i].Setup(i, lanes, settings.physicsConfig);

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

            bool isAutoRun = (_currentPhase == TutorialPhase.FinalAutoRun);
            float autoTarget = isAutoRun ? settings.physicsConfig.maxScrollSpeed * settings.autoRunSpeedRatio : 0f;

            for (int i = 0; i < players.Length; i++)
            {
                if (players[i]) players[i].OnUpdate(isAutoRun, autoTarget, settings.autoRunSmoothTime);
            }

            float s1 = players[0] ? players[0].currentSpeed : 0f;
            float s2 = players[1] ? players[1].currentSpeed : 0f;
            env.ScrollEnvironment(s1, s2);
        }

        private void HandlePlayerDistanceChanged(int playerIdx, float currentDist, float maxDist)
        {
            if (ui)
            {
                ui.UpdateGauge(playerIdx, Mathf.Min(currentDist, settings.targetDistancePhase1), maxDist);
            }
        }

        private void HandlePadDown(int playerIdx, int laneIdx, int padIdx)
        {
            if (!_gameStarted || _currentPhase == TutorialPhase.FinalAutoRun) return;
            if (playerIdx < 0 || playerIdx >= players.Length) return;
            if (laneIdx < 0 || laneIdx > 2) return;

            var player = players[playerIdx];
            if (!player) return;

            if (player.HandleInput(laneIdx, padIdx))
            {
                ProcessMoveLogic(player, laneIdx);
            }
        }

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
                    // ★ [수정] 우측에서 왼쪽 화살표를 보고 이동하므로, 목표 라인을 좌측(0)이 아닌 중앙(1)으로 변경
                    HandleRunningPhase(player, laneIdx, 1, settings.targetDistancePhase3, Phase3CompletionRoutine);
                    break;
            }
        }

        private void HandlePhase1(PlayerController player, int laneIdx)
        {
            if (laneIdx != 1) return;

            if (player.currentDistance >= settings.targetDistancePhase1) return;

            player.MoveAndAccelerate(1);
            CheckPopupClose();

            if (!_phase1GlobalComplete &&
                players[0].currentDistance >= settings.targetDistancePhase1 &&
                players[1].currentDistance >= settings.targetDistancePhase1)
            {
                _phase1GlobalComplete = true;
                string msg = _data?.phase1SuccessMessage?.text ?? "잘하셨어요.";
                StartCoroutine(SuccessSequenceRoutine(msg));
            }
        }

        private void HandleRunningPhase(PlayerController player, int laneIdx, int targetLane, float targetDist,
            Func<IEnumerator> nextRoutine)
        {
            int pIdx = player.playerIndex;

            if (_phaseCompleted[pIdx] || laneIdx != targetLane) return;

            // 플레이어가 해당 라인을 밟았으므로 점멸 중지
            if (padDotController != null)
            {
                int baseIdx = pIdx * 6 + targetLane * 2;
                padDotController.StopBlinking(new int[] { baseIdx, baseIdx + 1 });
            }

            if (!_popupFadedOut)
            {
                _popupFadedOut = true;
                ui.HidePopup(1f);
            }

            // Phase3Left(목표:중앙,1)인 경우: laneIdx(1) == 2는 false이므로 ui.StopArrowFadeOut(..., false, ...) 호출 -> 왼쪽 화살표 꺼짐 (정상)
            ui.StopArrowFadeOut(pIdx, laneIdx == 2, 1.0f);

            player.MoveAndAccelerate(targetLane);
            _phaseDistances[pIdx] += 1f;

            if (_phaseDistances[pIdx] >= targetDist)
            {
                _phaseCompleted[pIdx] = true;

                if (_phaseCompleted[0] && _phaseCompleted[1] && !_routineStarted)
                {
                    _routineStarted = true;
                    StartCoroutine(nextRoutine());
                }
            }
        }

        private void CheckPopupClose()
        {
            if (_isWaitingForRun)
            {
                _isWaitingForRun = false;
                ui.HidePopup(0.5f);
            }
        }

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
            yield return StartCoroutine(ui.FadeOutPopupTextAndChange(t2, 0.5f, 0.5f));

            _isWaitingForRun = true;
            _gameStarted = true;
            _currentPhase = TutorialPhase.Phase1Center;
        }

        private IEnumerator SuccessSequenceRoutine(string message)
        {
            _currentPhase = TutorialPhase.Intro; 
            
            yield return StartCoroutine(ui.ShowSuccessText(message, 2.0f));

            if (_data?.guideTexts != null && _data.guideTexts.Length > 3)
            {
                ui.PreparePopup(_data.guideTexts[2].text);
                
                float fadeDuration = 1f; 
                StartCoroutine(ui.FadeInPopup(fadeDuration)); 
                env.FadeInAllObstacles(0, 3, fadeDuration);
                
                yield return CoroutineData.GetWaitForSeconds(3.0f);
                
                yield return StartCoroutine(ui.FadeOutPopupTextAndChange(_data.guideTexts[3].text, 0.5f, 0.5f));
                yield return CoroutineData.GetWaitForSeconds(1f);

                ResetPhaseState();
                ui.PlayArrow(0, true);
                ui.PlayArrow(1, true);

                // 우측 이동 단계 시작 시 우측 발판 점멸 시작
                // P1 Right(4,5), P2 Right(10,11)
                if (padDotController != null)
                {
                    padDotController.StartBlinking(new int[] { 4, 5, 10, 11 });
                }

                _currentPhase = TutorialPhase.Phase2Right;
            }
        }

        private IEnumerator Phase2CompletionRoutine()
        {
            yield return CoroutineData.GetWaitForSeconds(1.0f);
            
            // ★ [수정] 플레이어를 중앙으로 강제 이동시키지 않음
            // players[0].MoveToLane(1); 
            // players[1].MoveToLane(1);

            // 왼쪽 화살표 재생 (우측에 있으므로 왼쪽으로 유도)
            ui.PlayArrow(0, false);
            ui.PlayArrow(1, false);

            // ★ [추가] 중앙 발판 점멸 시작 (이동 목표 표시)
            // P1 Center(2,3), P2 Center(8,9)
            if (padDotController != null)
            {
                padDotController.StartBlinking(new int[] { 2, 3, 8, 9 });
            }

            ResetPhaseState();
            _currentPhase = TutorialPhase.Phase3Left;
        }

        private IEnumerator Phase3CompletionRoutine()
        {
            yield return CoroutineData.GetWaitForSeconds(1.0f);
            players[0].MoveToLane(1);
            players[1].MoveToLane(1);

            string msg = (_data != null && _data.phase1SuccessMessage != null) ? _data.phase1SuccessMessage.text : "Complete";
            yield return StartCoroutine(ui.ShowSuccessText(msg, 2.0f));

            if (_data != null && _data.guideTexts != null && _data.guideTexts.Length > 4)
            {
                ui.PreparePopup(_data.guideTexts[4].text);
                StartCoroutine(ui.FadeInPopup(1f));
                env.FadeInAllObstacles(3, 1, 1.0f);
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

        public int GetCurrentLane(int playerIdx)
        {
            if (playerIdx >= 0 && playerIdx < players.Length && players[playerIdx] != null)
                return players[playerIdx].currentLane;
            return 1;
        }

        public void OnPlayerHit(int playerIdx)
        {
            if (playerIdx >= 0 && playerIdx < players.Length && players[playerIdx] != null)
            {
                players[playerIdx].OnHit(2.0f);

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
            if (_data != null && _data.guideTexts != null && _data.guideTexts.Length > 5)
            {
                yield return StartCoroutine(ui.FadeOutPopupTextAndChange(_data.guideTexts[5].text, 0.5f, 0.5f));
            }

            yield return CoroutineData.GetWaitForSeconds(2.0f);

            if (ui&& _data != null && _data.finalTexts != null && _data.finalTexts.Length > 0)
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