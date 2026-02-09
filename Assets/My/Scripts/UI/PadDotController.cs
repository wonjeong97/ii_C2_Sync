using My.Scripts.Core;
using UnityEngine;
using UnityEngine.UI;
using My.Scripts.Global;

public class PadDotController : MonoBehaviour
{
    [Header("Settings")]
    public Sprite activeSprite; 

    [Header("UI Images Reference")]
    // 순서: [P1 L(2) -> C(2) -> R(2)] -> [P2 L(2) -> C(2) -> R(2)]
    public Image[] padImages; 

    private Sprite[] _defaultSprites;

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

        if (IsValidIndex(index))
        {
            if (isPressed)
            {
                if (activeSprite != null) padImages[index].sprite = activeSprite;
            }
            else
            {
                if (_defaultSprites != null && _defaultSprites[index] != null)
                    padImages[index].sprite = _defaultSprites[index];
            }
        }
    }

    // ★ [추가] 특정 플레이어의 중앙 라인(Lane 1) 닷 투명도 설정
    public void SetCenterDotsAlpha(int playerIdx, float alpha)
    {
        // 중앙 라인(Lane 1)의 발판 인덱스는 0번과 1번 두 개임
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

    private bool IsValidIndex(int index)
    {
        return padImages != null && index >= 0 && index < padImages.Length && padImages[index] != null;
    }
}