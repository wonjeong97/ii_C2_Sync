using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Microsoft.Extensions.Logging;
using My.Scripts.Core;
using UnityEngine;
using UnityEngine.UI;
using VContainer;
using ZLogger;

namespace My.Scripts.UI
{
    /// <summary>
    /// 하드웨어 발판 입력 상태를 UI 도트 이미지로 시각화하는 컨트롤러.
    /// VContainer 의존성 주입 및 UniTask 기반의 비동기 연출 적용.
    /// </summary>
    public class PadDotController : MonoBehaviour
    {
        [Header("Settings")]
        public Sprite activeSprite; 

        [Header("UI Images Reference")]
        public Image[] padImages; 

        private Sprite[] _defaultSprites;
        private readonly HashSet<int> _blinkingIndices = new();
        private CancellationTokenSource _blinkCts;
        
        private const int DOTS_PER_PLAYER = 6;

        private ILogger<PadDotController> _logger;
        private GameManager _gameManager;
        private InputManager _inputManager;

        [Inject]
        public void Construct(ILogger<PadDotController> logger, GameManager gameManager, InputManager inputManager)
        {
            _logger = logger;
            _gameManager = gameManager;
            _inputManager = inputManager;
        }

        private void Start()
        {
            InitializeSprites();
            ApplyPlayerColors();
            
            _gameManager.OnUserDataUpdated += ApplyPlayerColors;
            _inputManager.OnPadDown += OnKeyDown;
            _inputManager.OnPadUp += OnKeyUp;
        }

        private void OnDestroy()
        {
            _gameManager.OnUserDataUpdated -= ApplyPlayerColors;
            _inputManager.OnPadDown -= OnKeyDown;
            _inputManager.OnPadUp -= OnKeyUp;
            
            CancelBlink();
        }

        private void InitializeSprites()
        {
            if (padImages == null) return;

            _defaultSprites = new Sprite[padImages.Length];
            for (int i = 0; i < padImages.Length; i++)
            {
                if (padImages[i]) _defaultSprites[i] = padImages[i].sprite;
            }
        }

        private void ApplyPlayerColors()
        {
            Color colorA = _gameManager.GetColorFromData(_gameManager.PlayerAColor);
            Color colorB = _gameManager.GetColorFromData(_gameManager.PlayerBColor);

            if (padImages == null) return;

            for (int i = 0; i < padImages.Length; i++)
            {
                if (!padImages[i]) continue;

                int playerIdx = Mathf.Clamp(i / DOTS_PER_PLAYER, 0, 1);
                Color targetColor = (playerIdx == 0) ? colorA : colorB;
                
                float currentAlpha = padImages[i].color.a;
                padImages[i].color = new Color(targetColor.r, targetColor.g, targetColor.b, currentAlpha);
            }
        }

        private void OnKeyDown(int pIdx, int lIdx, int padIdx) => ChangeImageState(pIdx, lIdx, padIdx, true);
        private void OnKeyUp(int pIdx, int lIdx, int padIdx) => ChangeImageState(pIdx, lIdx, padIdx, false);

        private void ChangeImageState(int pIdx, int lIdx, int padIdx, bool isPressed)
        {
            int index = CalculateIndex(pIdx, lIdx, padIdx);

            if (IsValidIndex(index))
            {
                if (isPressed)
                {
                    if (activeSprite) padImages[index].sprite = activeSprite;
                }
                else
                {
                    if (!_blinkingIndices.Contains(index))
                    {
                        if (_defaultSprites != null && _defaultSprites[index])
                            padImages[index].sprite = _defaultSprites[index];
                    }
                }
            }
        }

        public void SetCenterDotsAlpha(int playerIdx, float alpha)
        {
            SetDotAlpha(playerIdx, 1, 0, alpha);
            SetDotAlpha(playerIdx, 1, 1, alpha);
        }

        private void SetDotAlpha(int pIdx, int lIdx, int padIdx, float alpha)
        {
            int index = CalculateIndex(pIdx, lIdx, padIdx);
            if (IsValidIndex(index))
            {
                Color c = padImages[index].color;
                c.a = alpha;
                padImages[index].color = c;
            }
        }

        public void StartBlinking(int[] indices)
        {
            if (indices == null) return;

            foreach (int idx in indices)
            {
                if (IsValidIndex(idx)) _blinkingIndices.Add(idx);
            }

            if (_blinkingIndices.Count > 0 && _blinkCts == null)
            {
                _blinkCts = new CancellationTokenSource();
                BlinkLoopAsync(_blinkCts.Token).Forget();
            }
        }

        public void StopBlinking(int[] indices)
        {
            if (indices == null) return;

            foreach (int idx in indices)
            {
                if (_blinkingIndices.Remove(idx) && IsValidIndex(idx))
                {
                    if (_defaultSprites != null && _defaultSprites[idx])
                        padImages[idx].sprite = _defaultSprites[idx];
                }
            }
            
            if (_blinkingIndices.Count == 0) CancelBlink();
        }

        private async UniTaskVoid BlinkLoopAsync(CancellationToken ct)
        {
            bool isActive = true;
            try
            {
                while (!ct.IsCancellationRequested && _blinkingIndices.Count > 0)
                {
                    foreach (int idx in _blinkingIndices)
                    {
                        if (!IsValidIndex(idx)) continue;
                        padImages[idx].sprite = isActive ? activeSprite : _defaultSprites[idx];
                    }

                    await UniTask.Delay(TimeSpan.FromSeconds(0.5f), cancellationToken: ct);
                    isActive = !isActive;
                }
            }
            catch (OperationCanceledException) { }
            finally { _blinkCts = null; }
        }

        private void CancelBlink()
        {
            if (_blinkCts != null)
            {
                _blinkCts.Cancel();
                _blinkCts.Dispose();
                _blinkCts = null;
            }
        }

        private int CalculateIndex(int pIdx, int lIdx, int padIdx) => (pIdx * 6) + (lIdx * 2) + padIdx;
        
        private bool IsValidIndex(int index) => 
            padImages != null && index >= 0 && index < padImages.Length && padImages[index];
    }
}