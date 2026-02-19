using UnityEngine;
using UnityEngine.UI;

namespace My.Scripts._04_PlayLong
{
    /// <summary>
    /// PlayLong 전용 게이지 바 컨트롤러. 픽토그램 없이 Bar_Fill의 fillAmount만 제어함.
    /// </summary>
    public class PlayLongGaugeController : MonoBehaviour
    {
        [Header("UI Components")]
        [SerializeField] private Image barFill; // Bar_Fill 오브젝트 할당

        /// <summary>
        /// 게이지의 충전 상태를 업데이트합니다.
        /// </summary>
        /// <param name="current">현재 거리</param>
        /// <param name="max">목표 거리</param>
        public void UpdateGauge(float current, float max)
        {
            if (!barFill || max <= 0) return;

            // 진행 비율 계산 (0~1)
            float ratio = Mathf.Clamp01(current / max);
            barFill.fillAmount = ratio;
        }

        /// <summary>
        /// 게이지를 0으로 초기화합니다.
        /// </summary>
        public void ResetGauge()
        {
            if (barFill) barFill.fillAmount = 0f;
        }
    }
}