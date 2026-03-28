using My.Scripts.Core;
using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using Wonjeong.Utils;

/// <summary>
/// 하드웨어 발판 입력 상태를 UI 도트 이미지로 시각화하는 컨트롤러.
/// </summary>
public class PadDotController : MonoBehaviour
{
    [Header("Settings")]
    public Sprite activeSprite; 

    [Header("UI Images Reference")]
    // 순서: [P1 L(2) -> C(2) -> R(2)] -> [P2 L(2) -> C(2) -> R(2)]
    public Image[] padImages; 

    private Sprite[] _defaultSprites;

    private readonly HashSet<int> _blinkingIndices = new HashSet<int>();
    private Coroutine _blinkCoroutine;

    private const int DOTS_PER_PLAYER = 6;

    /// <summary>
    /// 컴포넌트 활성화 시 초기 리소스 백업 및 이벤트 구독을 수행함.
    /// </summary>
    private void Start()
    {
        if (padImages != null)
        {
            // 이유: 입력 해제 시 원래 상태로 복구하기 위해 초기 스프라이트를 저장함.
            _defaultSprites = new Sprite[padImages.Length];
            for (int i = 0; i < padImages.Length; i++)
            {
                if (padImages[i])
                    _defaultSprites[i] = padImages[i].sprite;
            }
        }

        ApplyPlayerColors();
        
        // 이유: 게임 중 유저 데이터가 갱신될 때 실시간으로 색상을 동기화함.
        if (GameManager.Instance)
        {
            GameManager.Instance.OnUserDataUpdated += ApplyPlayerColors;
        }

        if (InputManager.Instance)
        {
            InputManager.Instance.OnPadDown += OnKeyDown;
            InputManager.Instance.OnPadUp += OnKeyUp;
        }
    }

    /// <summary>
    /// 현재 플레이어 설정에 맞춰 도트 이미지의 색상을 일괄 업데이트함.
    /// </summary>
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
                    // 이유: 배열 인덱스를 기준으로 각 도트가 속한 플레이어 영역을 판별함.
                    // 예시: 인덱스 7 입력 시 7 / 6 = 1 -> 플레이어 B 색상 적용.
                    int playerIdx = Mathf.Clamp(i / DOTS_PER_PLAYER, 0, 1);
                    Color targetColor = (playerIdx == 0) ? colorA : colorB;
                    
                    float currentAlpha = padImages[i].color.a;
                    padImages[i].color = new Color(targetColor.r, targetColor.g, targetColor.b, currentAlpha);
                }
            }
        }
    }

    /// <summary>
    /// 객체 파괴 시 등록된 모든 이벤트 구독을 해제함.
    /// </summary>
    private void OnDestroy()
    {
        if (InputManager.Instance)
        {
            InputManager.Instance.OnPadDown -= OnKeyDown;
            InputManager.Instance.OnPadUp -= OnKeyUp;
        }
        
        if (GameManager.Instance)
        {
            GameManager.Instance.OnUserDataUpdated -= ApplyPlayerColors;
        }
    }

    private void OnKeyDown(int playerIdx, int laneIdx, int padIdx) 
        => ChangeImageState(playerIdx, laneIdx, padIdx, true);

    private void OnKeyUp(int playerIdx, int laneIdx, int padIdx) 
        => ChangeImageState(playerIdx, laneIdx, padIdx, false);

    /// <summary>
    /// 개별 도트의 활성 상태에 따라 스프라이트를 교체함.
    /// </summary>
    private void ChangeImageState(int pIdx, int lIdx, int padIdx, bool isPressed)
    {
        // 이유: 3차원 입력 데이터를 선형 배열 인덱스로 변환하여 관리함.
        // 예시: Player 1(0), Lane Center(1), Pad 0 -> (0 * 6) + (1 * 2) + 0 = 인덱스 2.
        int index = (pIdx * 6) + (lIdx * 2) + padIdx;

        if (IsValidIndex(index))
        {
            if (isPressed)
            {
                if (activeSprite) padImages[index].sprite = activeSprite;
            }
            else
            {
                // 이유: 가이드 점멸 중인 도트는 입력 해제 시에도 점멸 상태를 유지해야 함.
                if (!_blinkingIndices.Contains(index))
                {
                    if (_defaultSprites != null && _defaultSprites[index])
                        padImages[index].sprite = _defaultSprites[index];
                }
            }
        }
    }

    /// <summary>
    /// 특정 플레이어의 중앙 레인 가이드 라인 투명도를 설정함.
    /// </summary>
    public void SetCenterDotsAlpha(int playerIdx, float alpha)
    {
        SetDotAlpha(playerIdx, 1, 0, alpha);
        SetDotAlpha(playerIdx, 1, 1, alpha);
    }

    /// <summary>
    /// 개별 도트 이미지의 알파값을 변경함.
    /// </summary>
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

    /// <summary>
    /// 지정된 발판 인덱스 리스트에 대해 점멸 가이드 연출을 시작함.
    /// </summary>
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

    /// <summary>
    /// 지정된 발판들의 점멸 가이드를 중단하고 원래 상태로 복구함.
    /// </summary>
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

    /// <summary>
    /// 가이드 대상 도트들을 주기적으로 깜빡이게 하는 루틴.
    /// </summary>
    private IEnumerator BlinkRoutine()
    {
        bool isActive = true;
        WaitForSeconds wait = CoroutineData.GetWaitForSeconds(0.5f); 

        // # TODO: 대규모 도트 점멸 시 렌더링 부하 발생 가능성 있으므로 최적화 검토 필요.
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

    /// <summary>
    /// 배열 범위 및 이미지 객체 유효성을 검사함.
    /// </summary>
    private bool IsValidIndex(int index)
    {
        return padImages != null && index >= 0 && index < padImages.Length && padImages[index];
    }
}