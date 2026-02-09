using UnityEngine;
using UnityEngine.UI;

namespace My.Scripts.Environment
{
    public class FrameDistanceLabel : MonoBehaviour
    {
        [Tooltip("거리를 표시할 UI 텍스트 (World Space Canvas 자식)")]
        [SerializeField] private Text distanceText;

        public void SetDistance(float meters)
        {
            if (distanceText != null)
            {
                distanceText.text = $"{meters:F0}M";
            }
        }

        public void SetText(string text)
        {
            if (distanceText != null)
            {
                distanceText.text = text;
            }
        }

        public void SetLabelActive(bool isActive)
        {
            if (distanceText != null)
            {
                distanceText.gameObject.SetActive(isActive);
            }
        }
    }
}