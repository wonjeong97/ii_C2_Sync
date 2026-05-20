using Cysharp.Text;
using Microsoft.Extensions.Logging;
using UnityEngine;
using UnityEngine.UI;
using VContainer;
using ZLogger;

namespace My.Scripts.Environment
{
    /// <summary>
    /// 월드 공간 상의 프레임 오브젝트에 거리 수치나 텍스트 정보를 시각화하는 클래스.
    /// VContainer 인젝션 및 ZString 최적화 적용.
    /// </summary>
    public class FrameDistanceLabel : MonoBehaviour
    {
        [Tooltip("거리를 표시할 UI 텍스트 (World Space Canvas 자식)")]
        [SerializeField] private Text distanceText;

        private ILogger<FrameDistanceLabel> _logger;

        [Inject]
        public void Construct(ILogger<FrameDistanceLabel> logger) 
        { 
            _logger = logger; 
        }

        public void SetDistance(float meters)
        {
            if (distanceText)
            {
                // ZString을 통해 메모리 할당 없이 포맷팅
                distanceText.text = ZString.Format("{0:F0}M", meters);
            }
            else
            {
                _logger.ZLogWarning($"distanceText 컴포넌트 누락됨.");
            }
        }

        public void SetText(string text)
        {
            if (distanceText)
            {
                distanceText.text = text;
            }
            else
            {
                _logger.ZLogWarning($"distanceText 컴포넌트 누락됨.");
            }
        }

        public void SetLabelActive(bool isActive)
        {
            if (distanceText)
            {
                distanceText.gameObject.SetActive(isActive);
            }
            else
            {
                _logger.ZLogWarning($"distanceText 컴포넌트 누락됨.");
            }
        }
    }
}