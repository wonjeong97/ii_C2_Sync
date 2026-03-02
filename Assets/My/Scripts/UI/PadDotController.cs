using My.Scripts.Core;
using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using Wonjeong.Utils;

public class PadDotController : MonoBehaviour
{
    [Header("Settings")]
    public Sprite activeSprite; 

    [Header("UI Images Reference")]
    // 순서: [P1 L(2) -> C(2) -> R(2)] -> [P2 L(2) -> C(2) -> R(2)]
    public Image[] padImages; 

    private Sprite[] _defaultSprites;

    private HashSet<int> _blinkingIndices = new HashSet<int>();
    private Coroutine _blinkCoroutine;

    private const int DOTS_PER_PLAYER = 6;

    private void Start()
    {
        if (padImages != null)
        {
            _defaultSprites = new Sprite[padImages.Length];
            for (int i = 0; i < padImages.Length; i++)
            {
                if (padImages[i])
                    _defaultSprites[i] = padImages[i].sprite;
            }
        }

        ApplyPlayerColors();

        if (InputManager.Instance)
        {
            InputManager.Instance.OnPadDown += OnKeyDown;
            InputManager.Instance.OnPadUp += OnKeyUp;
        }
    }

    private void ApplyPlayerColors()
    {
        if (!GameManager.Instance) return;

        Color colorA = GameManager.Instance.GetColorFromData(GameManager.Instance.PlayerAColor);
        Color colorB = GameManager.Instance.GetColorFromData(GameManager.Instance.PlayerBColor);

        if (padImages != null)
        {
            for (int i = 0; i < padImages.Length; i++)
            {
                if (padImages[i])
                {
                    int playerIdx = Mathf.Clamp(i / DOTS_PER_PLAYER, 0, 1);
                    Color targetColor = (playerIdx == 0) ? colorA : colorB;
                    
                    float currentAlpha = padImages[i].color.a;
                    padImages[i].color = new Color(targetColor.r, targetColor.g, targetColor.b, currentAlpha);
                }
            }
        }
    }

    private void OnDestroy()
    {
        if (InputManager.Instance)
        {
            InputManager.Instance.OnPadDown -= OnKeyDown;
            InputManager.Instance.OnPadUp -= OnKeyUp;
        }
    }

    private void OnKeyDown(int playerIdx, int laneIdx, int padIdx) 
        => ChangeImageState(playerIdx, laneIdx, padIdx, true);

    private void OnKeyUp(int playerIdx, int laneIdx, int padIdx) 
        => ChangeImageState(playerIdx, laneIdx, padIdx, false);

    private void ChangeImageState(int pIdx, int lIdx, int padIdx, bool isPressed)
    {
        int index = (pIdx * 6) + (lIdx * 2) + padIdx;

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
        int index = (pIdx * 6) + (lIdx * 2) + padIdx;
        if (IsValidIndex(index))
        {
            Color c = padImages[index].color;
            c.a = alpha;
            padImages[index].color = c;
        }
    }

    public void StartBlinking(int[] indices)
    {
        if (indices == null || indices.Length == 0) return;

        foreach (int idx in indices)
        {
            if (IsValidIndex(idx)) _blinkingIndices.Add(idx);
        }

        if (_blinkingIndices.Count > 0 && _blinkCoroutine == null)
        {
            _blinkCoroutine = StartCoroutine(BlinkRoutine());
        }
    }

    public void StopBlinking(int[] indices)
    {
        if (indices == null || indices.Length == 0) return;

        foreach (int idx in indices)
        {
            if (_blinkingIndices.Contains(idx))
            {
                _blinkingIndices.Remove(idx);
                
                if (IsValidIndex(idx))
                {
                    if (_defaultSprites != null && _defaultSprites[idx])
                        padImages[idx].sprite = _defaultSprites[idx];
                }
            }
        }
    }

    private IEnumerator BlinkRoutine()
    {
        bool isActive = true;
        WaitForSeconds wait = CoroutineData.GetWaitForSeconds(0.5f); 

        while (_blinkingIndices.Count > 0)
        {
            foreach (int idx in _blinkingIndices)
            {
                if (!IsValidIndex(idx)) continue;

                if (isActive)
                {
                    if (activeSprite) padImages[idx].sprite = activeSprite;
                }
                else
                {
                    if (_defaultSprites != null && _defaultSprites[idx])
                        padImages[idx].sprite = _defaultSprites[idx];
                }
            }

            yield return wait;
            isActive = !isActive;
        }

        _blinkCoroutine = null;
    }

    private bool IsValidIndex(int index)
    {
        return padImages != null && index >= 0 && index < padImages.Length && padImages[index];
    }
}