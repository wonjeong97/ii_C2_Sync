using Microsoft.Extensions.Logging;
using UnityEngine;
using UnityEngine.UI;
using VContainer;
using ZLogger;

namespace My.Scripts._04_PlayLong
{
    /// <summary>
    /// PlayLong 전용 게이지 바 컨트롤러.
    /// VContainer 의존성 주입과 ZLogger를 통한 구조화된 로깅을 지원합니다.
    /// </summary>
    public class PlayLongGaugeController : MonoBehaviour
    {
        [Header("UI Components")]
        [SerializeField] private Image barFill;

        private ILogger<PlayLongGaugeController> _logger;

        [Inject]
        public void Construct(ILogger<PlayLongGaugeController> logger)
        {
            _logger = logger;
        }

        public void UpdateGauge(float current, float max)
        {
            if (!barFill)
            {
                _logger?.ZLogWarning($"barFill 컴포넌트가 할당되지 않았습니다.");
                return;
            }

            if (max <= 0)
            {
                _logger?.ZLogWarning($"목표 거리가 0 이하입니다. 게이지 업데이트를 건너뜁니다.");
                return;
            }

            barFill.fillAmount = Mathf.Clamp01(current / max);
        }

        public void ResetGauge()
        {
            if (barFill)
            {
                barFill.fillAmount = 0f;
            }
            else
            {
                _logger?.ZLogWarning($"ResetGauge 호출 시 barFill 컴포넌트가 누락되었습니다.");
            }
        }
    }
}