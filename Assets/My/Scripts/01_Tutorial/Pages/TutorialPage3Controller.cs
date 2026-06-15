using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Microsoft.Extensions.Logging;
using My.Scripts.Core;
using UnityEngine;
using UnityEngine.UI;
using VContainer;
using Wonjeong.Data;
using Wonjeong.UI;
using ZLogger;

namespace My.Scripts._01_Tutorial.Pages
{
    [Serializable]
    public class TutorialPage3Data
    {
        public TextSetting playerAName;
        public TextSetting playerBName;
        public TextSetting descriptionText;
    }

    public class TutorialPage3Controller : GamePage<TutorialPage3Data>
    {
        [Header("UI Components")]
        [SerializeField] private Text descriptionText;
        [SerializeField] private Image checkImageA;
        [SerializeField] private Image checkImageB;
        [SerializeField] private Text playerAText; 
        [SerializeField] private Text playerBText; 
        [SerializeField] private Image ballImageA; 
        [SerializeField] private Image ballImageB; 

        [Header("Settings")]
        [SerializeField] private float jumpLandingTolerance = 0.25f; 

        private TutorialPage3Data _data; 
        private CancellationTokenSource _cts;

        private bool _isAFinished, _isBFinished, _isStepCompleted, _finishSoundPlayed;
        private bool _pAPad0, _pAPad1, _pBPad0, _pBPad1;
        private bool _pAIsReady, _pAHasJumped, _pBIsReady, _pBHasJumped;
        private float _pAFirstFootTime, _pBFirstFootTime; 

        private ILogger<TutorialPage3Controller> _logger;
        private GameManager _gameManager;
        private SoundManager _soundManager;
        private UIManager _uiManager;
        private InputManager _inputManager;

        [Inject]
        public void Construct(ILogger<TutorialPage3Controller> logger, GameManager gameManager, 
                              SoundManager soundManager, UIManager uiManager, InputManager inputManager)
        {
            _logger = logger;
            _gameManager = gameManager;
            _soundManager = soundManager;
            _uiManager = uiManager;
            _inputManager = inputManager;
        }

        protected override void SetupData(TutorialPage3Data data)
        {
            _data = data ?? throw new ArgumentNullException(nameof(data));

            if (_uiManager)
            {
                if (playerAText && data.playerAName != null) _uiManager.SetText(playerAText.gameObject, data.playerAName);
                if (playerBText && data.playerBName != null) _uiManager.SetText(playerBText.gameObject, data.playerBName);
                if (descriptionText && data.descriptionText != null)
                {
                    descriptionText.supportRichText = true;
                    _uiManager.SetText(descriptionText.gameObject, data.descriptionText);
                }
            }
        }

        public override void OnEnter()
        {
            base.OnEnter();
            _cts = new CancellationTokenSource();
            
            if (_gameManager) _gameManager.IsAutoProgressing = false;

            _isAFinished = _isBFinished = _isStepCompleted = _finishSoundPlayed = false;
            _pAIsReady = _pAHasJumped = _pBIsReady = _pBHasJumped = false;
            _pAFirstFootTime = _pBFirstFootTime = 0f;

            InitCheckImage(checkImageA);
            InitCheckImage(checkImageB);

            ApplyDynamicNames();
            ApplyPlayerColors();
            SyncInitialInputState();

            if (_inputManager)
            {
                _inputManager.OnPadDown += HandlePadDown;
                _inputManager.OnPadUp += HandlePadUp;
            }
        }

        public override void OnExit()
        {
            if (_gameManager) _gameManager.IsAutoProgressing = true;
            
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;

            if (_inputManager)
            {
                _inputManager.OnPadDown -= HandlePadDown;
                _inputManager.OnPadUp -= HandlePadUp;
            }
            base.OnExit();
        }

        private void ApplyDynamicNames()
        {
            if (_data == null || !_gameManager) return;

            string nameA = _gameManager.PlayerAName;
            string nameB = _gameManager.PlayerBName;

            if (playerAText && _data.playerAName != null)
                playerAText.text = _data.playerAName.text.Replace("{nameA}", nameA);
            if (playerBText && _data.playerBName != null)
                playerBText.text = _data.playerBName.text.Replace("{nameB}", nameB);
        }

        private void ApplyPlayerColors()
        {
            if (!_gameManager) return;
            if (ballImageA) ballImageA.sprite = _gameManager.GetColorSprite(_gameManager.PlayerAColor);
            if (ballImageB) ballImageB.sprite = _gameManager.GetColorSprite(_gameManager.PlayerBColor);
        }

        private void SyncInitialInputState()
        {
            _pAPad0 = Input.GetKey(KeyCode.Alpha3);
            _pAPad1 = Input.GetKey(KeyCode.Alpha4);
            if (_pAPad0 && _pAPad1) _pAIsReady = true;

            _pBPad0 = Input.GetKey(KeyCode.Alpha9);
            _pBPad1 = Input.GetKey(KeyCode.Alpha0);
            if (_pBPad0 && _pBPad1) _pBIsReady = true;
        }

        private void InitCheckImage(Image img)
        {
            if (!img) return;
            Color c = img.color;
            c.a = 0f;
            img.color = c;
            img.gameObject.SetActive(false);
        }

        private void HandlePadDown(int pIdx, int lIdx, int pId) => UpdateLogic(pIdx, lIdx, pId, true);
        private void HandlePadUp(int pIdx, int lIdx, int pId) => UpdateLogic(pIdx, lIdx, pId, false);

        private void UpdateLogic(int pIdx, int lIdx, int pId, bool isDown)
        {
            if (lIdx != 1) return; 

            if (pIdx == 0)
            {
                if (pId == 0) _pAPad0 = isDown;
                else if (pId == 1) _pAPad1 = isDown;
                CheckSequence(0, _pAPad0, _pAPad1, ref _pAIsReady, ref _pAHasJumped, ref _pAFirstFootTime, ref _isAFinished, checkImageA);
            }
            else if (pIdx == 1)
            {
                if (pId == 0) _pBPad0 = isDown;
                else if (pId == 1) _pBPad1 = isDown;
                CheckSequence(1, _pBPad0, _pBPad1, ref _pBIsReady, ref _pBHasJumped, ref _pBFirstFootTime, ref _isBFinished, checkImageB);
            }
            
            if (_isAFinished && _isBFinished && !_isStepCompleted)
            {
                _isStepCompleted = true;
                WaitAndCompleteAsync(_cts.Token).Forget();
            }
        }

        private void CheckSequence(int pIdx, bool p0, bool p1, ref bool isReady, ref bool hasJumped, ref float firstFootTime, ref bool isFinished, Image checkImg)
        {
            if (isFinished) return;

            int padCount = (p0 ? 1 : 0) + (p1 ? 1 : 0);
            if (padCount == 0)
            {
                if (isReady) hasJumped = true; 
                firstFootTime = 0f; 
            }
            else if (padCount == 1)
            {
                if (firstFootTime <= 0f) firstFootTime = Time.time;
            }
            else if (padCount == 2)
            {
                bool isSimultaneous = (Time.time - firstFootTime) <= jumpLandingTolerance;
                if (hasJumped && isSimultaneous)
                {
                    if (!_finishSoundPlayed)
                    {
                        _finishSoundPlayed = true;
                        _soundManager?.PlaySFX("공통_5");
                    }
                    isFinished = true;
                    FadeInImageAsync(checkImg, 1.0f, _cts.Token).Forget();
                }
                else
                {
                    isReady = true;
                    hasJumped = false;
                    firstFootTime = 0f;
                }
            }
        }

        private async UniTask FadeInImageAsync(Image targetImg, float duration, CancellationToken ct)
        {
            if (!targetImg) return;
            targetImg.gameObject.SetActive(true);
            Color initialColor = targetImg.color;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                targetImg.color = new Color(initialColor.r, initialColor.g, initialColor.b, Mathf.Clamp01(elapsed / duration));
                await UniTask.Yield(PlayerLoopTiming.Update, ct);
            }
            targetImg.color = new Color(initialColor.r, initialColor.g, initialColor.b, 1f);
        }

        private async UniTaskVoid WaitAndCompleteAsync(CancellationToken ct)
        {
            await UniTask.Delay(TimeSpan.FromSeconds(1.0f), cancellationToken: ct);
            CompleteStep();
        }
    }
}