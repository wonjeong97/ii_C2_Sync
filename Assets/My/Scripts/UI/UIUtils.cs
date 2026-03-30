using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Wonjeong.Data;

namespace My.Scripts.UI
{
    /// <summary>
    /// UI 연출 및 데이터 매핑을 위한 공용 정적 유틸리티 클래스.
    /// </summary>
    public static class UIUtils
    {
        /// <summary>
        /// CanvasGroup의 알파값을 일정 시간 동안 보간하여 페이드 효과를 적용함.
        /// </summary>
        public static IEnumerator FadeCanvasGroup(CanvasGroup cg, float start, float end, float duration)
        {
            // 이유: Unity 객체 생존 여부 확인을 위한 암시적 불리언 변환 활용.
            if (!cg) yield break;

            // 이유: 대기 시간 없이 즉시 결과값을 반영하여 불필요한 연산 오버헤드 방지.
            if (duration <= 0f)
            {
                cg.alpha = end;
                if (end <= 0f) cg.gameObject.SetActive(false);
                yield break;
            }

            float t = 0f;
            cg.alpha = start;

            // # TODO: 루프 내 빈번한 보간 연산을 가속하기 위해 보간 알고리즘 최적화 검토 필요.
            while (t < duration)
            {
                t += Time.deltaTime;
                // 예시 입력값: start(0), end(1), t(0.5), duration(1) -> 결과값 0.5.
                cg.alpha = Mathf.Lerp(start, end, t / duration);
                yield return null;
            }
            
            cg.alpha = end;
            
            // 이유: 완전히 투명해진 UI는 드로우콜을 방지하기 위해 비활성화함.
            if (end <= 0f) cg.gameObject.SetActive(false);
        }

        /// <summary>
        /// 여러 UI 화면에서 공통적으로 사용하는 플레이어 이름 텍스트를 치환하고 설정함
        /// </summary>
        public static void ApplyPlayerNames(Text p1Text, Text p2Text, string nameA, string nameB, TextSetting settingA, TextSetting settingB)
        {
            if (p1Text)
            {
                if (settingA != null)
                {
                    // 이유: 외부 시스템(UIManager)을 통해 텍스트의 시각적 속성을 우선 적용함.
                    if (Wonjeong.UI.UIManager.Instance) Wonjeong.UI.UIManager.Instance.SetText(p1Text.gameObject, settingA);
                    
                    // 이유: 데이터 시트에 정의된 예약어({nameA})를 유저의 실제 이름으로 매핑함.
                    p1Text.text = settingA.text.Replace("{nameA}", nameA);
                }
                else
                {
                    // 이유: 설정 데이터가 누락된 경우 유저가 위치를 인지할 수 있도록 기본 문구 노출.
                    p1Text.text = $"{nameA}님의 위치";
                }
            }
            else
            {
                Debug.LogWarning("P1 이름 텍스트 컴포넌트 누락됨.");
            }

            if (p2Text)
            {
                if (settingB != null)
                {
                    if (Wonjeong.UI.UIManager.Instance) Wonjeong.UI.UIManager.Instance.SetText(p2Text.gameObject, settingB);
                    p2Text.text = settingB.text.Replace("{nameB}", nameB);
                }
                else
                {
                    p2Text.text = $"{nameB}님의 위치";
                }
            }
            else
            {
                Debug.LogWarning("P2 이름 텍스트 컴포넌트 누락됨.");
            }
        }
    }
}