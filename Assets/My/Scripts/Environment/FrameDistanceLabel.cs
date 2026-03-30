using UnityEngine;
using UnityEngine.UI;

namespace My.Scripts.Environment
{
    /// <summary>
    /// 월드 공간 상의 프레임 오브젝트에 거리 수치나 텍스트 정보를 시각화하는 클래스.
    /// </summary>
    public class FrameDistanceLabel : MonoBehaviour
    {
        [Tooltip("거리를 표시할 UI 텍스트 (World Space Canvas 자식)")]
        [SerializeField] private Text distanceText;

        /// <summary>
        /// 입력된 미터 수치를 포맷팅하여 텍스트에 출력함.
        /// </summary>
        /// <param name="meters">표시할 가상 미터 거리</param>
        public void SetDistance(float meters)
        {
            if (distanceText)
            {
                // 예시 입력: 123.456 -> 결과값: "123M"
                distanceText.text = $"{meters:F0}M";
            }
            else
            {
                Debug.LogWarning("distanceText 컴포넌트 누락됨.");
            }
        }

        /// <summary>
        /// 수치가 아닌 일반 문자열을 텍스트에 직접 출력함.
        /// </summary>
        /// <param name="text">출력할 문자열</param>
        public void SetText(string text)
        {
            if (distanceText)
            {
                distanceText.text = text;
            }
            else
            {
                Debug.LogWarning("distanceText 컴포넌트 누락됨.");
            }
        }

        /// <summary>
        /// 텍스트 게임 오브젝트의 활성화 상태를 제어함.
        /// </summary>
        /// <param name="isActive">활성화 여부</param>
        public void SetLabelActive(bool isActive)
        {
            if (distanceText)
            {
                // 이유: 특정 거리(마일스톤)가 아닌 경우 UI 리소스를 아끼기 위해 텍스트를 숨김.
                distanceText.gameObject.SetActive(isActive);
            }
            else
            {
                Debug.LogWarning("distanceText 컴포넌트 누락됨.");
            }
        }
    }
}