using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using My.Scripts.Core;
using Wonjeong.Data;
using Wonjeong.UI;
using Wonjeong.Utils; 

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

        private bool _isAFinished;
        private bool _isBFinished;
        private bool _isStepCompleted; 
        private float _lastInputTime; 

        private bool _pAPad0, _pAPad1;
        private bool _pBPad0, _pBPad1;

        private bool _pAIsReady;
        private bool _pAHasJumped;
        private float _pAFirstFootTime; 

        private bool _pBIsReady;
        private bool _pBHasJumped;
        private float _pBFirstFootTime; 

        protected override void SetupData(TutorialPage3Data data)
        {
            if (data == null)
            {
                Debug.LogWarning("[TutorialPage3Controller] 전달된 데이터가 없습니다.");
                return;
            }

            string nameA = GameManager.Instance ? GameManager.Instance.PlayerALastName : "Player A";
            string nameB = GameManager.Instance ? GameManager.Instance.PlayerBLastName : "Player B";
            
            if (playerAText && data.playerAName != null)
            {
                string replacedTextA = data.playerAName.text.Replace("{nameA}", nameA);
                if (UIManager.Instance) UIManager.Instance.SetText(playerAText.gameObject, data.playerAName);
                playerAText.text = replacedTextA;
            }
            else
            {
                Debug.LogWarning("[TutorialPage3Controller] Player A 이름 텍스트 설정에 필요한 컴포넌트나 데이터가 누락되었습니다.");
            }

            if (playerBText && data.playerBName != null)
            {
                string replacedTextB = data.playerBName.text.Replace("{nameB}", nameB);
                if (UIManager.Instance) UIManager.Instance.SetText(playerBText.gameObject, data.playerBName);
                playerBText.text = replacedTextB;
            }
            else
            {
                Debug.LogWarning("[TutorialPage3Controller] Player B 이름 텍스트 설정에 필요한 컴포넌트나 데이터가 누락되었습니다.");
            }

            if (descriptionText && data.descriptionText != null)
            {
                descriptionText.supportRichText = true;
                if (UIManager.Instance)
                {
                    UIManager.Instance.SetText(descriptionText.gameObject, data.descriptionText);
                }
                else
                {
                    Debug.LogWarning("[TutorialPage3Controller] UIManager.Instance를 찾을 수 없습니다.");
                }
            }
        }

        public override void OnEnter()
        {
            base.OnEnter();
            
            _isAFinished = false;
            _isBFinished = false;
            _isStepCompleted = false; 
            _lastInputTime = Time.time;

            _pAIsReady = _pAHasJumped = false;
            _pBIsReady = _pBHasJumped = false;
            _pAFirstFootTime = _pBFirstFootTime = 0f;

            InitCheckImage(checkImageA);
            InitCheckImage(checkImageB);

            ApplyPlayerColors();
            SyncInitialInputState();

            if (InputManager.Instance)
            {
                InputManager.Instance.OnPadDown += HandlePadDown;
                InputManager.Instance.OnPadUp += HandlePadUp;
            }
            else
            {
                Debug.LogWarning("[TutorialPage3Controller] InputManager.Instance를 찾을 수 없습니다.");
            }
        }

        public override void OnExit()
        {
            base.OnExit();
            
            if (InputManager.Instance)
            {
                InputManager.Instance.OnPadDown -= HandlePadDown;
                InputManager.Instance.OnPadUp -= HandlePadUp;
            }
        }

        private void Update()
        {
            if (_isStepCompleted) return;

            if (Time.time - _lastInputTime >= 20f)
            {
                if (GameManager.Instance)
                {
                    GameManager.Instance.ForceInactivitySequence();
                }
                else
                {
                    Debug.LogWarning("[TutorialPage3Controller] GameManager.Instance를 찾을 수 없어 방치 팝업을 띄울 수 없습니다.");
                }
                _lastInputTime = float.MaxValue; 
            }
        }

        private void ApplyPlayerColors()
        {
            if (!GameManager.Instance) return;

            if (ballImageA)
            {
                Sprite spriteA = GameManager.Instance.GetColorSprite(GameManager.Instance.PlayerAColor);
                if (spriteA) ballImageA.sprite = spriteA;
            }

            if (ballImageB)
            {
                Sprite spriteB = GameManager.Instance.GetColorSprite(GameManager.Instance.PlayerBColor);
                if (spriteB) ballImageB.sprite = spriteB;
            }
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
            if (img)
            {
                Color c = img.color;
                c.a = 0f;
                img.color = c;
                img.gameObject.SetActive(false);
            }
        }

        private void HandlePadDown(int playerIdx, int laneIdx, int padIdx) => UpdateLogic(playerIdx, laneIdx, padIdx, true);
        private void HandlePadUp(int playerIdx, int laneIdx, int padIdx) => UpdateLogic(playerIdx, laneIdx, padIdx, false);

        private void UpdateLogic(int playerIdx, int laneIdx, int padIdx, bool isDown)
        {
            _lastInputTime = Time.time;

            if (laneIdx != 1) return; 

            if (playerIdx == 0)
            {
                if (padIdx == 0) _pAPad0 = isDown;
                else if (padIdx == 1) _pAPad1 = isDown;

                CheckSequence(0, _pAPad0, _pAPad1, ref _pAIsReady, ref _pAHasJumped, ref _pAFirstFootTime, ref _isAFinished, checkImageA);
            }
            else if (playerIdx == 1)
            {
                if (padIdx == 0) _pBPad0 = isDown;
                else if (padIdx == 1) _pBPad1 = isDown;

                CheckSequence(1, _pBPad0, _pBPad1, ref _pBIsReady, ref _pBHasJumped, ref _pBFirstFootTime, ref _isBFinished, checkImageB);
            }
            
            if (_isAFinished && _isBFinished && !_isStepCompleted)
            {
                _isStepCompleted = true;
                StartCoroutine(WaitAndCompleteRoutine());
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
                    if (SoundManager.Instance) SoundManager.Instance.PlaySFX("공통_5");
                    
                    isFinished = true;
                    StartCoroutine(FadeInRoutine(checkImg, 1f));
                }
                else
                {
                    isReady = true;
                    hasJumped = false;
                    firstFootTime = 0f;
                }
            }
        }

        private IEnumerator FadeInRoutine(Image targetImg, float duration)
        {
            if (!targetImg) yield break;
            
            targetImg.gameObject.SetActive(true);
            Color initialColor = targetImg.color;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float alpha = Mathf.Clamp01(elapsed / duration);
                targetImg.color = new Color(initialColor.r, initialColor.g, initialColor.b, alpha);
                yield return null;
            }
            targetImg.color = new Color(initialColor.r, initialColor.g, initialColor.b, 1f);
        }

        private IEnumerator WaitAndCompleteRoutine()
        {
            yield return CoroutineData.GetWaitForSeconds(1.0f);
            CompleteStep();
        }
    }
}