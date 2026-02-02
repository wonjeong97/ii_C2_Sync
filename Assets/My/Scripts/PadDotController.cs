using UnityEngine;
using UnityEngine.UI;
using My.Scripts.Global; // InputManager 사용

public class PadDotController : MonoBehaviour
{
    [Header("Settings")]
    public Sprite activeSprite; // 눌렀을 때 바뀔 이미지

    [Header("UI Images Reference")]
    // 순서: [P1 왼쪽(2개) -> 중앙(2개) -> 오른쪽(2개)] -> [P2 왼쪽 -> 중앙 -> 오른쪽]
    public Image[] padImages; 

    // 원래 이미지(DotVoid)를 기억할 배열
    private Sprite[] _defaultSprites;

    void Start()
    {
        // 1. 배열 크기 체크 및 원본 이미지 캐싱(기억)
        if (padImages != null)
        {
            _defaultSprites = new Sprite[padImages.Length];
            for (int i = 0; i < padImages.Length; i++)
            {
                if (padImages[i] != null)
                {
                    _defaultSprites[i] = padImages[i].sprite;
                }
            }
        }

        // 2. InputManager 이벤트 연결
        if (InputManager.Instance != null)
        {
            InputManager.Instance.OnPadDown += OnKeyDown;
            InputManager.Instance.OnPadUp += OnKeyUp;
        }
    }

    void OnDestroy()
    {
        // 3. 이벤트 연결 해제
        if (InputManager.Instance != null)
        {
            InputManager.Instance.OnPadDown -= OnKeyDown;
            InputManager.Instance.OnPadUp -= OnKeyUp;
        }
    }

    // --- 이벤트 핸들러 ---

    private void OnKeyDown(int playerIdx, int laneIdx, int padIdx)
    {
        ChangeImageState(playerIdx, laneIdx, padIdx, true);
    }

    private void OnKeyUp(int playerIdx, int laneIdx, int padIdx)
    {
        ChangeImageState(playerIdx, laneIdx, padIdx, false);
    }

    // --- 로직 ---

    private void ChangeImageState(int pIdx, int lIdx, int padIdx, bool isPressed)
    {
        // 인덱스 계산 (0 ~ 11)
        // 플레이어(0,1) * 6 + 라인(0,1,2) * 2 + 발판(0,1)
        int index = (pIdx * 6) + (lIdx * 2) + padIdx;

        if (IsValidIndex(index))
        {
            if (isPressed)
            {
                // 누르면 설정한 Active 이미지로 변경
                if (activeSprite != null) 
                    padImages[index].sprite = activeSprite;
            }
            else
            {
                // 떼면 원래 이미지(DotVoid)로 복구
                padImages[index].sprite = _defaultSprites[index];
            }
        }
    }

    private bool IsValidIndex(int index)
    {
        return padImages != null && index >= 0 && index < padImages.Length && padImages[index] != null;
    }
}