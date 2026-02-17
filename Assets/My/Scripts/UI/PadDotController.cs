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

    // 점멸 중인 인덱스를 관리하기 위한 HashSet
    private HashSet<int> _blinkingIndices = new HashSet<int>();
    private Coroutine _blinkCoroutine;

    void Start()
    {
        if (padImages != null)
        {
            _defaultSprites = new Sprite[padImages.Length];
            for (int i = 0; i < padImages.Length; i++)
            {
                if (padImages[i] != null)
                    _defaultSprites[i] = padImages[i].sprite;
            }
        }

        if (InputManager.Instance != null)
        {
            InputManager.Instance.OnPadDown += OnKeyDown;
            InputManager.Instance.OnPadUp += OnKeyUp;
        }
    }

    void OnDestroy()
    {
        if (InputManager.Instance != null)
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

        // ★ 점멸 중인 발판을 눌렀을 때는 점멸 로직보다 입력 상태를 우선시하기 위해 예외 처리가 필요할 수 있으나,
        // 현재 로직상 입력 이벤트가 발생하면 즉시 스프라이트를 덮어씌우므로 자연스럽게 동작함.
        if (IsValidIndex(index))
        {
            if (isPressed)
            {
                if (activeSprite != null) padImages[index].sprite = activeSprite;
            }
            else
            {
                // 점멸 중이 아닐 때만 기본 이미지로 복구 (점멸 중이면 코루틴이 알아서 제어)
                if (!_blinkingIndices.Contains(index))
                {
                    if (_defaultSprites != null && _defaultSprites[index] != null)
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

    // ★ [추가] 특정 인덱스들의 점멸 시작
    public void StartBlinking(int[] indices)
    {
        foreach (var idx in indices)
        {
            if (IsValidIndex(idx)) _blinkingIndices.Add(idx);
        }

        if (_blinkingIndices.Count > 0 && _blinkCoroutine == null)
        {
            _blinkCoroutine = StartCoroutine(BlinkRoutine());
        }
    }

    // ★ [추가] 특정 인덱스들의 점멸 중지
    public void StopBlinking(int[] indices)
    {
        foreach (var idx in indices)
        {
            if (_blinkingIndices.Contains(idx))
            {
                _blinkingIndices.Remove(idx);
                
                // 점멸을 멈출 때 기본 이미지로 즉시 복구
                if (IsValidIndex(idx))
                {
                    if (_defaultSprites != null && _defaultSprites[idx] != null)
                        padImages[idx].sprite = _defaultSprites[idx];
                }
            }
        }
    }

    private IEnumerator BlinkRoutine()
    {
        bool isActive = true;
        WaitForSeconds wait = CoroutineData.GetWaitForSeconds(0.5f); // 0.5초 간격 점멸

        while (_blinkingIndices.Count > 0)
        {
            foreach (var idx in _blinkingIndices)
            {
                if (!IsValidIndex(idx)) continue;

                if (isActive)
                {
                    if (activeSprite != null) padImages[idx].sprite = activeSprite;
                }
                else
                {
                    if (_defaultSprites != null && _defaultSprites[idx] != null)
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
        return padImages != null && index >= 0 && index < padImages.Length && padImages[index] != null;
    }
}