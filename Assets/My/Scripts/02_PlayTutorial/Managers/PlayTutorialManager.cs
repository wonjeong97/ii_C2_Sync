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

    /// <summary>
    /// 플레이 튜토리얼(실제 조작 연습)의 전체 흐름을 제어하는 매니저 클래스.
    /// 페이즈별 연출 및 방치 타이머 상태를 관리함.
    /// </summary>
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

        private void Awake()
        {
            if (!Instance) Instance = this;
            else if (Instance != this) Destroy(gameObject);
        }

        private void Start()
        {
            _data = JsonLoader.Load<PlayTutorialData>(GameConstants.Path.PlayTutorial);

            if (!settings)
            {
                Debug.LogError("[PlayTutorialManager] Settings(TutorialSettingsSO)가 연결되지 않았습니다.");
                return;
            }

            if (ui) 
            {
                ui.InitUI(settings.physicsConfig.maxDistance);
                
                // API 연동 데이터(이름, 컬러)와 JSON 스타일 데이터를 UI에 전달함.
                if (GameManager.Instance)
                {
                    string nameA = string.IsNullOrEmpty(GameManager.Instance.PlayerALastName) ? "Player A" : GameManager.Instance.PlayerALastName;
                    string nameB = string.IsNullOrEmpty(GameManager.Instance.PlayerBLastName) ? "Player B" : GameManager.Instance.PlayerBLastName;
                    
                    TextSetting settingA = _data != null ? _data.playerAName : null;
                    TextSetting settingB = _data != null ? _data.playerBName : null;

                    ui.SetPlayerNames(nameA, nameB, settingA, settingB);

                    // 컬러 데이터를 기반으로 UI 공 이미지의 스프라이트를 변경함.
                    Sprite spriteA = GameManager.Instance.GetColorSprite(GameManager.Instance.PlayerAColor);
                    Sprite spriteB = GameManager.Instance.GetColorSprite(GameManager.Instance.PlayerBColor);
                    ui.SetPlayerBalls(spriteA, spriteB);
                }
            }
            
            if (env) env.InitEnvironment();

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
                            Color targetColor = GameManager.Instance.GetColorFromData(colorData);
                            players[i].SetCharacterColor(targetColor);
                        }
                    }
                }
            }

            if (players == null || players.Length < 2 || !players[0] || !players[1])
            {
                Debug.LogError("[PlayTutorialManager] 플레이어가 2명 할당되지 않았습니다.");
                enabled = false;
                return;
            }

            if (InputManager.Instance) InputManager.Instance.OnPadDown += HandlePadDown;

            SetAutoProgressing(true);
            StartCoroutine(IntroScenario());
        }

        private void OnDestroy()
        {
            if (InputManager.Instance) InputManager.Instance.OnPadDown -= HandlePadDown;
            if (Instance == this) Instance = null;

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
            if (GameManager.Instance)
            {
                GameManager.Instance.IsAutoProgressing = isAuto;
                
                if (!isAuto)
                {
                    GameManager.Instance.ResetInactivityTimer();
                }
            }
            else
            {
                Debug.LogWarning("[PlayTutorialManager] GameManager.Instance를 찾을 수 없습니다.");
            }
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

        private void HandlePhase1(PlayerController player, int laneIdx)
        {
            if (laneIdx != 1) return;

            if (player.currentDistance >= settings.targetDistancePhase1) return;

            player.MoveAndAccelerate(1);
            CheckPopupClose();

            if (!_phase1GlobalComplete && players != null && players.Length > 1 && players[0] && players[1])
            {
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

        private void HandleRunningPhase(PlayerController player, int laneIdx, int targetLane, float targetDist,
            Func<IEnumerator> nextRoutine)
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

        private void CheckPopupClose()
        {
            if (_isWaitingForRun)
            {
                _isWaitingForRun = false;
                if (ui) ui.HidePopup(0.5f);
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

        private IEnumerator SuccessSequenceRoutine(string message)
        {
            _currentPhase = TutorialPhase.Intro; 
            
            if (ui) yield return StartCoroutine(ui.ShowSuccessText(message, 2.0f));

            if (_data != null && _data.guideTexts != null && _data.guideTexts.Length > 3)
            {
                if (ui)
                {
                    ui.PreparePopup(_data.guideTexts[2].text);
                    StartCoroutine(ui.FadeInPopup(1f)); 
                }
                
                if (env) env.FadeInAllObstacles(0, 3, 1f);
                
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
                    StartCoroutine(ui.FadeInPopup(1f));
                }
                
                if (env) env.FadeInAllObstacles(3, 1, 1.0f);
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
            if (players != null && playerIdx >= 0 && playerIdx < players.Length && players[playerIdx])
                return players[playerIdx].currentLane;
            return 1;
        }

        public void OnPlayerHit(int playerIdx)
        {
            if (players != null && playerIdx >= 0 && playerIdx < players.Length && players[playerIdx])
            {
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