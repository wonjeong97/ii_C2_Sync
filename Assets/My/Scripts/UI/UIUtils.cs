using System;
using System.Threading;
using Cysharp.Text;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using Wonjeong.Data;
using Wonjeong.UI;

namespace My.Scripts.UI
{
    /// <summary>
    /// UI 연출 및 데이터 매핑을 위한 공용 정적 유틸리티 클래스.
    /// ZString을 통한 메모리 최적화 및 UniTask 지원.
    /// </summary>
    public static class UIUtils
    {
        /// <summary>
        /// CanvasGroup의 알파값을 일정 시간 동안 보간하여 페이드 효과를 적용함.
        /// </summary>
        public static async UniTask FadeCanvasGroupAsync(CanvasGroup cg, float start, float end, float duration, CancellationToken ct = default, bool isFadeIn = true)
        {
            if (!cg) return;

            // FadeIn 시작 전 활성화, FadeOut 시 나중에 비활성화
            if (isFadeIn) cg.gameObject.SetActive(true);

            if (duration <= 0f)
            {
                cg.alpha = end;
                if (!isFadeIn && end <= 0f) cg.gameObject.SetActive(false);
                return;
            }

            cg.alpha = start;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                cg.alpha = Mathf.Lerp(start, end, elapsed / duration);
                await UniTask.Yield(PlayerLoopTiming.Update, ct);
            }
            
            cg.alpha = end;
            if (!isFadeIn && end <= 0f) cg.gameObject.SetActive(false);
        }

        /// <summary>
        /// 플레이어 이름 UI를 동적으로 치환하고 설정함.
        /// </summary>
        public static void ApplyPlayerNames(UIManager uiManager, Text p1Text, Text p2Text, string nameA, string nameB, TextSetting settingA, TextSetting settingB)
        {
            UpdatePlayerName(uiManager, p1Text, nameA, settingA, "{nameA}", "P1");
            UpdatePlayerName(uiManager, p2Text, nameB, settingB, "{nameB}", "P2");
        }

        private static void UpdatePlayerName(UIManager uiManager, Text textComponent, string playerName, TextSetting setting, string placeholder, string label)
        {
            if (!textComponent)
            {
                Debug.LogWarning($"{label} 이름 텍스트 컴포넌트 누락됨.");
                return;
            }

            if (setting != null)
            {
                uiManager?.SetText(textComponent.gameObject, setting);
                textComponent.text = setting.text.ReplaceZ(placeholder, playerName);
            }
            else
            {
                textComponent.text = playerName;
            }
        }

        /// <summary>
        /// ZString을 사용하여 메모리 할당을 최소화한 Replace 메서드.
        /// </summary>
        public static string ReplaceZ(this string str, string oldValue, string newValue)
        {
            if (string.IsNullOrEmpty(str) || string.IsNullOrEmpty(oldValue)) return str;
            if (newValue == null) newValue = string.Empty;

            // ZString을 통해 가비지 발생을 억제하며 문자열 생성
            using (var sb = ZString.CreateStringBuilder())
            {
                int lastIndex = 0;
                int index = str.IndexOf(oldValue, StringComparison.Ordinal);

                while (index != -1)
                {
                    sb.Append(str, lastIndex, index - lastIndex);
                    sb.Append(newValue);
                    lastIndex = index + oldValue.Length;
                    index = str.IndexOf(oldValue, lastIndex, StringComparison.Ordinal);
                }

                sb.Append(str, lastIndex, str.Length - lastIndex);
                return sb.ToString();
            }
        }
    }
}