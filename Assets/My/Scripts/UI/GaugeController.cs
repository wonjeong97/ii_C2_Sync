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
    
    // 픽토그램이 이동할 수 있는 최대 X 좌표 (이 값을 넘어서면 멈춤)
    [SerializeField] private float maxPictogramX = 670f; 

    /// <summary>
    /// 현재 거리와 최대 거리를 받아 UI 게이지와 픽토그램 위치를 갱신합니다.
    /// </summary>
    /// <param name="currentDistance">현재 이동한 거리</param>
    /// <param name="maxDistance">목표 최대 거리</param>
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
            
            // 진행률(ratio)에 비례하여 목표 X 위치 계산
            float targetX = (totalWidth * ratio) + xOffset;

            // 픽토그램이 설정된 최대 위치(670)를 넘어가지 않도록 제한
            targetX = Mathf.Min(targetX, maxPictogramX);

            pictogram.anchoredPosition = new Vector2(targetX, pictogram.anchoredPosition.y);
        }
    }
}