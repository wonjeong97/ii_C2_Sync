using System;
using System.Collections;
using My.Scripts.Core;
using My.Scripts.UI;
using UnityEngine;
using UnityEngine.UI;
using Wonjeong.Data;
using Wonjeong.UI;
using Wonjeong.Utils;

namespace My.Scripts._05_Ending.Pages
{
    [Serializable]
    public class EndingPage1Data
    {
        public TextSetting distanceFormatText; 
    }

    public class EndingPage1Controller : GamePage<EndingPage1Data>
    {
        private readonly static int Jump = Animator.StringToHash("Jump");
        private readonly static int Idle = Animator.StringToHash("Idle");

        [Header("UI Groups")]
        [Tooltip("피켓 이미지와 두 캐릭터(P1, P2)를 묶어둔 최상위 CanvasGroup")]
        [SerializeField] private CanvasGroup picketAndCharsCg;
        
        [Tooltip("500M 완주 성공 시 띄울 파티클 이펙트 CanvasGroup")]
        [SerializeField] private CanvasGroup particleCg;
        
        [SerializeField] private Text distanceTextUI;

        [Header("Animators")]
        [SerializeField] private Animator p1Animator;
        [SerializeField] private Animator p2Animator;

        [Header("Character Parts (For Color)")]
        [SerializeField] private Image p1Body;
        [SerializeField] private Image p1LeftHand;
        [SerializeField] private Image p1RightHand;
        
        [SerializeField] private Image p2Body;
        [SerializeField] private Image p2LeftHand;
        [SerializeField] private Image p2RightHand;

        private EndingPage1Data _data;
        private Coroutine _particleRoutine;

        /// <summary>
        /// 외부 데이터를 받아 내부 데이터 변수에 할당함.
        /// </summary>
        /// <param name="data">적용할 엔딩 데이터</param>
        protected override void SetupData(EndingPage1Data data)
        {
            if (data == null)
            {
                Debug.LogWarning("EndingPage1Data 누락됨.");
                return;
            }
            _data = data;
        }

        /// <summary>
        /// 페이지 진입 시 호출됨.
        /// UI 상태를 초기화하고 캐릭터 색상 동기화 및 등장 연출을 시작함.
        /// </summary>
        public override void OnEnter()
        {
            base.OnEnter();

            // 이유: 등장 연출(페이드인) 전 이전 상태가 잔존하지 않도록 완벽히 투명화 및 비활성화함.
            if (picketAndCharsCg) picketAndCharsCg.alpha = 0f;
            if (particleCg)
            {
                particleCg.alpha = 0f;
                particleCg.gameObject.SetActive(false);
            }

            ApplyPlayerColors();

            float dist = GameManager.Instance ? GameManager.Instance.lastPlayDistance : 0f;
            int finalDistance = Mathf.FloorToInt(dist);

            if (_data != null && _data.distanceFormatText != null && distanceTextUI)
            {
                if (UIManager.Instance) 
                {
                    UIManager.Instance.SetText(distanceTextUI.gameObject, _data.distanceFormatText);
                }
        
                // 이유: 데이터에 정의된 포맷 문자열({0})에 최종 도달 거리를 동적으로 삽입함.
                distanceTextUI.text = string.Format(_data.distanceFormatText.text, finalDistance);
            }
            else
            {
                Debug.LogWarning("거리 텍스트 데이터 혹은 UI 컴포넌트가 누락됨.");
            }

            // 이유: 데이터 세팅 중 텍스트가 깜빡이는 것을 방지하기 위해 알파값을 다시 0으로 덮어씌움.
            if (distanceTextUI)
            {
                Color c = distanceTextUI.color;
                c.a = 0f;
                distanceTextUI.color = c;
            }

            StartCoroutine(EntranceSequence(finalDistance));
        }

        /// <summary>
        /// GameManager에 저장된 API 컬러 데이터를 가져와 캐릭터(몸통, 손) 이미지에 적용함.
        /// </summary>
        private void ApplyPlayerColors()
        {
            if (!GameManager.Instance) return;

            Sprite spriteA = GameManager.Instance.GetColorSprite(GameManager.Instance.PlayerAColor);
            Sprite spriteB = GameManager.Instance.GetColorSprite(GameManager.Instance.PlayerBColor);

            // 이유: API에 등록된 스프라이트가 존재하면 해당 이미지를 씌우고, 없다면 기존 틴트(Color) 방식을 예비책(Fallback)으로 적용하여 시각적 일관성을 유지함.
            if (spriteA)
            {
                if (p1Body) p1Body.sprite = spriteA;
                if (p1LeftHand) p1LeftHand.sprite = spriteA;
                if (p1RightHand) p1RightHand.sprite = spriteA;
            }
            else
            {
                Color colorA = GameManager.Instance.GetColorFromData(GameManager.Instance.PlayerAColor);
                if (p1Body) p1Body.color = new Color(colorA.r, colorA.g, colorA.b, p1Body.color.a);
                if (p1LeftHand) p1LeftHand.color = new Color(colorA.r, colorA.g, colorA.b, p1LeftHand.color.a);
                if (p1RightHand) p1RightHand.color = new Color(colorA.r, colorA.g, colorA.b, p1RightHand.color.a);
            }

            if (spriteB)
            {
                if (p2Body) p2Body.sprite = spriteB;
                if (p2LeftHand) p2LeftHand.sprite = spriteB;
                if (p2RightHand) p2RightHand.sprite = spriteB;
            }
            else
            {
                Color colorB = GameManager.Instance.GetColorFromData(GameManager.Instance.PlayerBColor);
                if (p2Body) p2Body.color = new Color(colorB.r, colorB.g, colorB.b, p2Body.color.a);
                if (p2LeftHand) p2LeftHand.color = new Color(colorB.r, colorB.g, colorB.b, p2LeftHand.color.a);
                if (p2RightHand) p2RightHand.color = new Color(colorB.r, colorB.g, colorB.b, p2RightHand.color.a);
            }
        }

        /// <summary>
        /// 정의된 순서대로 페이드 인 및 애니메이션을 실행하는 코루틴.
        /// </summary>
        /// <param name="finalDistance">최종 도달 거리</param>
        /// <returns>IEnumerator 루틴</returns>
        private IEnumerator EntranceSequence(int finalDistance)
        {   
            if (picketAndCharsCg)
            {
                yield return StartCoroutine(UIUtils.FadeCanvasGroup(picketAndCharsCg, 0f, 1f, 1.0f));
            }

            if (distanceTextUI)
            {
                yield return StartCoroutine(FadeTextAlpha(distanceTextUI, 0f, 1f, 1.0f));
            }

            // 이유: 500m 만점 달성 여부에 따라 성공/실패 효과음을 분기 처리함.
            if (finalDistance >= 500 && particleCg)
            {
                if (SoundManager.Instance) SoundManager.Instance.PlaySFX("달리기_4");
                particleCg.gameObject.SetActive(true);
                _particleRoutine = StartCoroutine(BlinkRoutine(particleCg, 0.5f));
            }
            else
            {
                if (SoundManager.Instance) SoundManager.Instance.PlaySFX("달리기_5");
            }

            if (p1Animator) p1Animator.SetTrigger(Jump);
            if (p2Animator) p2Animator.SetTrigger(Jump);

            yield return CoroutineData.GetWaitForSeconds(2.0f);
            
            if (p1Animator) p1Animator.SetTrigger(Idle);
            if (p2Animator) p2Animator.SetTrigger(Idle);    
            
            CompleteStep();
        }

        /// <summary>
        /// 페이지 퇴장 시 호출됨.
        /// 동작 중인 모든 코루틴을 정리함.
        /// </summary>
        public override void OnExit()
        {
            base.OnExit();
            
            // 이유: 페이지 전환 시 파티클 점멸 및 텍스트 페이드 코루틴이 백그라운드에 남지 않게 강제 종료함.
            StopAllCoroutines();
            _particleRoutine = null;
        }

        /// <summary>
        /// 텍스트 컴포넌트 폰트 컬러의 알파값을 선형 보간하여 조절함.
        /// </summary>
        /// <param name="txt">대상 Text 컴포넌트</param>
        /// <param name="start">시작 알파값</param>
        /// <param name="end">종료 알파값</param>
        /// <param name="duration">진행 소요 시간</param>
        /// <returns>IEnumerator 루틴</returns>
        private IEnumerator FadeTextAlpha(Text txt, float start, float end, float duration)
        {
            if (!txt) yield break;

            float elapsed = 0f;
            Color c = txt.color;
            txt.color = new Color(c.r, c.g, c.b, start);
            
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                // 예시 입력: start(0f), end(1f), elapsed(0.5f), duration(1f) -> 결과값 = 0.5f
                float a = Mathf.Lerp(start, end, elapsed / duration);
                txt.color = new Color(c.r, c.g, c.b, a);
                yield return null;
            }
            txt.color = new Color(c.r, c.g, c.b, end);
        }

        /// <summary>
        /// 대상 캔버스 그룹의 알파값을 0과 1로 반복 전환하여 점멸 효과를 줌.
        /// </summary>
        /// <param name="cg">대상 CanvasGroup</param>
        /// <param name="interval">상태 전환 간격 시간</param>
        /// <returns>IEnumerator 루틴</returns>
        private IEnumerator BlinkRoutine(CanvasGroup cg, float interval)
        {
            bool isVisible = true;
            
            // # TODO: 조건 검사 방식 최적화를 위해 CanvasGroup의 null 여부를 외부에서 제어 고려 필요.
            while (cg)
            {
                cg.alpha = isVisible ? 1f : 0f;
                yield return CoroutineData.GetWaitForSeconds(interval);
                isVisible = !isVisible;
            }
        }
    }
}