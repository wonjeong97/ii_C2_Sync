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

        private TutorialPage3Data _data; // [추가] 런타임 이름 치환을 위해 원본 데이터 캐싱

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

            _data = data; // 원본 데이터를 기억해둡니다.

            // SetupData는 씬 시작 시(API 통신 전) 호출되므로, 여기서는 폰트/색상 등 UI 스타일만 덮어씌웁니다.
            if (playerAText && data.playerAName != null)
            {
                if (UIManager.Instance) UIManager.Instance.SetText(playerAText.gameObject, data.playerAName);
            }

            if (playerBText && data.playerBName != null)
            {
                if (UIManager.Instance) UIManager.Instance.SetText(playerBText.gameObject, data.playerBName);
            }

            if (descriptionText && data.descriptionText != null)
            {
                descriptionText.supportRichText = true;
                if (UIManager.Instance) UIManager.Instance.SetText(descriptionText.gameObject, data.descriptionText);
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

            // --- [수정] OnEnter 시점(API 통신 완료 후)에 동적 이름 치환 수행 ---
            ApplyDynamicNames();
            // ------------------------------------------------------------------

            ApplyPlayerColors();
            SyncInitialInputState();

            if (InputManager.Instance)
            {
                InputManager.Instance.OnPadDown += HandlePadDown;
                InputManager.Instance.OnPadUp += HandlePadUp;
            }
        }

        /// <summary>
        /// API 연동이 완료된 최신 이름을 가져와 텍스트에 적용합니다.
        /// 이유: SetupData는 통신 전에 실행되므로 "NoName"이 들어가는 문제를 해결하기 위함.
        /// </summary>
        private void ApplyDynamicNames()
        {
            if (_data == null) return;

            string nameA = "Player A";
            string nameB = "Player B";

            if (GameManager.Instance)
            {
                nameA = GameManager.Instance.PlayerAName;
                nameB = GameManager.Instance.PlayerBName;

                // 통신 실패나 빈 값에 대한 방어 로직
                if (string.IsNullOrEmpty(nameA) || nameA == "NoNameA") nameA = "Player A";
                if (string.IsNullOrEmpty(nameB) || nameB == "NoNameB") nameB = "Player B";
            }

            if (playerAText && _data.playerAName != null)
            {
                playerAText.text = _data.playerAName.text.Replace("{nameA}", nameA);
            }

            if (playerBText && _data.playerBName != null)
            {
                playerBText.text = _data.playerBName.text.Replace("{nameB}", nameB);
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