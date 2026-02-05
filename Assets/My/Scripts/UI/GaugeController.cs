using UnityEngine;
using UnityEngine.UI;

public class GaugeController : MonoBehaviour
{
    [Header("UI Components")]
    [SerializeField] private Image fillImage;       // 게이지 바 (Filled Image)
    [SerializeField] private RectTransform pictogram; // 🏃‍♂️ 달리는 사람 아이콘
    
    [Header("Settings")]
    [SerializeField] private RectTransform gaugeArea; // 게이지 전체 영역 (너비 기준용)
    [SerializeField] private float xOffset = 0f;      // 아이콘 미세 위치 조정용

    /// <summary>
    /// 현재 거리와 최대 거리를 받아 UI 및 픽토그램 위치를 갱신합니다.
    /// </summary>
    public void UpdateGauge(float currentDistance, float maxDistance)
    {
        if (maxDistance <= 0) return;

        // 진행률 계산 (0.0 ~ 1.0)
        float ratio = Mathf.Clamp01(currentDistance / maxDistance);

        // 게이지 바 채우기
        if (fillImage != null)
        {
            fillImage.fillAmount = ratio;
        }

        // 픽토그램 위치 이동
        if (pictogram != null && gaugeArea != null)
        {
            // 게이지의 전체 너비 구하기
            float totalWidth = gaugeArea.rect.width;

            // 비율에 따른 이동 거리 계산
            // (Pivot이 (0, 0.5)인 경우: 0에서 width까지 이동)
            // (Pivot이 (0.5, 0.5)인 경우: -width/2에서 width/2까지 이동)
            
            // 가장 일반적인 방식: Pivot X가 0(왼쪽)이라고 가정했을 때
            float targetX = (totalWidth * ratio) + xOffset;

            // 만약 게이지의 Pivot이 중앙(0.5)이라면 아래 주석을 사용하세요:
            // float targetX = (totalWidth * (ratio - 0.5f)) + xOffset;

            // 위치 적용 (Y값은 유지)
            pictogram.anchoredPosition = new Vector2(targetX, pictogram.anchoredPosition.y);
        }
    }
}