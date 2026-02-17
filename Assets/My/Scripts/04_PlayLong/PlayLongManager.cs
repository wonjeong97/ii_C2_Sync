using System;
using System.Collections;
using My.Scripts.Core;
using My.Scripts.Global;
using UnityEngine;
using Wonjeong.Data;
using Wonjeong.Utils;

namespace My.Scripts._04_PlayLong
{   
    [Serializable]
    public class Play500MSetting
    {
        public IntroPageData introPage;
        public TextSetting[] popupTexts; 
    }
    
    public class PlayLongManager : MonoBehaviour
    {
        public static PlayLongManager Instance { get; private set; }

        [Header("Game Settings")]
        [SerializeField] private float targetDistance = 500f; 
        [SerializeField] private float timeLimit = 60f;

        [Header("Manager References")]
        [SerializeField] private PlayLongUIManager ui;
        [SerializeField] private Page_Intro introPage;
        
        [Header("Players")]
        [SerializeField] private PlayerController[] players;

        [Header("Environment")]
        [SerializeField] private PlayLongEnvironment env;

        private Play500MSetting _setting;
        private bool _isGameActive = false;
        private float _currentTime;

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

            StartIntro();
        }

        private void InitializePlayers()
        {
            for (int i = 0; i < players.Length; i++)
            {
                if (players[i]) 
                {
                    players[i].OnDistanceChanged += HandlePlayerDistanceChanged;
                }
            }
        }

        private void OnDestroy()
        {
            if (InputManager.Instance) InputManager.Instance.OnPadDown -= HandlePadDown;
            for (int i = 0; i < players.Length; i++)
            {
                if (players[i]) players[i].OnDistanceChanged -= HandlePlayerDistanceChanged;
            }
        }

        private void Update()
        {
            if (!_isGameActive) return;

            _currentTime -= Time.deltaTime;
            if (ui) ui.UpdateTimer(_currentTime);

            if (_currentTime <= 0)
            {
                FinishGame();
                return;
            }

            for (int i = 0; i < players.Length; i++)
            {
                if (players[i]) players[i].OnUpdate(false, 0, 0);
            }
    
            // // 환경 스크롤 호출 (Environment 쪽에서 아무 일도 안 함)
            // float s1 = players[0] ? players[0].currentSpeed : 0;
            // float s2 = players[1] ? players[1].currentSpeed : 0;
            // if (env) env.ScrollEnvironment(s1, s2);
        }

        private void LoadSettings()
        {
            _setting = JsonLoader.Load<Play500MSetting>(GameConstants.Path.PlayLong);

            if (_setting == null)
            {
                Debug.LogError("[Play500MManager] JSON 로드 실패");
                return;
            }

            if (introPage != null)
            {
                introPage.SetupData(_setting.introPage);
            }
        }

        private void StartIntro()
        {
            if (introPage)
            {
                introPage.onStepComplete += OnIntroComplete;
                introPage.OnEnter();
            }
            else
            {
                OnIntroComplete(0);
            }
        }

        private void OnIntroComplete(int info)
        {
            if (introPage)
            {
                introPage.onStepComplete -= OnIntroComplete;
                introPage.OnExit();
            }
            StartCoroutine(StartTutorialMode());
        }

        private IEnumerator StartTutorialMode()
        {
            Debug.Log("[Play500M] 튜토리얼 팝업 시퀀스 시작");

            // [변경] popupTexts 배열을 사용하여 시퀀스 실행
            if (ui != null && _setting != null && 
                _setting.popupTexts != null && _setting.popupTexts.Length > 0)
            {
                yield return StartCoroutine(ui.ShowPopupSequence(_setting.popupTexts, 3.0f));
            }
            else
            {
                yield return CoroutineData.GetWaitForSeconds(1.0f);
            }

            StartInGame();
        }

        private void StartInGame()
        {
            Debug.Log("[Play500M] 본 게임 시작");
            _currentTime = timeLimit; 
            _isGameActive = true;
        }

        private void HandlePlayerDistanceChanged(int playerIdx, float currentDist, float maxDist)
        {
            if (_isGameActive && ui != null)
            {
                ui.UpdateGauge(playerIdx, currentDist, targetDistance);
            }
        }

        private void HandlePadDown(int pIdx, int lIdx, int padIdx)
        {
            if (!_isGameActive) return;

            if (pIdx >= 0 && pIdx < players.Length && players[pIdx])
            {
                if (players[pIdx].HandleInput(lIdx, padIdx))
                {
                    players[pIdx].MoveAndAccelerate(lIdx);
                }
            }
        }
        
        private void FinishGame()
        {
            _isGameActive = false;
            Debug.Log("Game Finished!");
        }
    }
}