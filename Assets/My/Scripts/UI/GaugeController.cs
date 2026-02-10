using UnityEngine;
using UnityEngine.UI;

public class GaugeController : MonoBehaviour
{
    [Header("UI Components")]
    [SerializeField] private Image fillImage;       
    [SerializeField] private RectTransform pictogram; 
    
    [Header("Settings")]
    [SerializeField] private RectTransform gaugeArea; 
    [SerializeField] private float xOffset = 0f;
    [SerializeField] private float maxPictogramX = 670f; 

    // ★ [추가] 원본 스프라이트 저장용 변수
    private Sprite _originSprite;

    private void Awake()
    {
        // 시작 시 할당된 이미지를 원본으로 저장
        if (fillImage != null) _originSprite = fillImage.sprite;
    }

    public void UpdateGauge(float currentDistance, float maxDistance)
    {
        if (maxDistance <= 0) return;

        float ratio = Mathf.Clamp01(currentDistance / maxDistance);

        if (fillImage != null)
        {
            fillImage.fillAmount = ratio;
        }

        if (pictogram != null && gaugeArea != null)
        {
            float totalWidth = gaugeArea.rect.width;
            float targetX = (totalWidth * ratio) + xOffset;
            targetX = Mathf.Min(targetX, maxPictogramX);
            pictogram.anchoredPosition = new Vector2(targetX, pictogram.anchoredPosition.y);
        }
    }

    // 게이지 이미지 변경 메서드
    public void SetFillSprite(Sprite newSprite)
    {
        if (fillImage != null && newSprite != null)
        {
            fillImage.sprite = newSprite;
        }
    }

    // 게이지 이미지 초기화 메서드
    public void ResetSprite()
    {
        if (fillImage != null && _originSprite != null)
        {
            fillImage.sprite = _originSprite;
        }
    }
}