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

        protected override void SetupData(EndingPage1Data data)
        {
            _data = data;
        }

        public override void OnEnter()
        {
            base.OnEnter();

            // 1. 초기화: UI 그룹 투명화
            if (picketAndCharsCg) picketAndCharsCg.alpha = 0f;
            if (particleCg)
            {
                particleCg.alpha = 0f;
                particleCg.gameObject.SetActive(false);
            }

            // 2. 캐릭터 색상 및 스프라이트 동기화
            ApplyPlayerColors();

            // 3. 거리 텍스트 데이터 세팅
            float dist = GameManager.Instance ? GameManager.Instance.lastPlayDistance : 0f;
            int finalDistance = Mathf.FloorToInt(dist);

            if (_data != null && _data.distanceFormatText != null && distanceTextUI)
            {
                if (UIManager.Instance) 
                    UIManager.Instance.SetText(distanceTextUI.gameObject, _data.distanceFormatText);
        
                distanceTextUI.text = string.Format(_data.distanceFormatText.text, finalDistance);
            }

            // 4. 데이터 세팅이 완전히 끝난 후, 텍스트 컴포넌트의 Alpha를 0으로 덮어씀
            if (distanceTextUI)
            {
                Color c = distanceTextUI.color;
                c.a = 0f;
                distanceTextUI.color = c;
            }

            // 5. 시퀀스 시작
            StartCoroutine(EntranceSequence(finalDistance));
        }

        /// <summary>
        /// GameManager에 저장된 API 컬러 데이터를 가져와 캐릭터(몸통, 손) 이미지에 적용함.
        /// 이유: API에 등록된 스프라이트가 존재하면 해당 이미지를 씌우고, 없다면 기존 틴트(Color) 방식을 예비책(Fallback)으로 적용하여 시각적 일관성을 유지하기 위함.
        /// </summary>
        private void ApplyPlayerColors()
        {
            if (!GameManager.Instance) return;

            Sprite spriteA = GameManager.Instance.GetColorSprite(GameManager.Instance.PlayerAColor);
            Sprite spriteB = GameManager.Instance.GetColorSprite(GameManager.Instance.PlayerBColor);

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
        private IEnumerator EntranceSequence(int finalDistance)
        {   
            // 1. 피켓과 캐릭터 페이드인 (1초)
            if (picketAndCharsCg)
            {
                yield return StartCoroutine(UIUtils.FadeCanvasGroup(picketAndCharsCg, 0f, 1f, 1.0f));
            }

            // 2. 결과 텍스트 페이드인 (1초)
            if (distanceTextUI)
            {
                yield return StartCoroutine(FadeTextAlpha(distanceTextUI, 0f, 1f, 1.0f));
            }

            // 3. 500M 만점 달성 시 파티클 점멸 시작
            if (finalDistance >= 500 && particleCg)
            {
                SoundManager.Instance?.PlaySFX("달리기_4");
                particleCg.gameObject.SetActive(true);
                _particleRoutine = StartCoroutine(BlinkRoutine(particleCg, 0.5f));
            }
            else
            {
                SoundManager.Instance?.PlaySFX("달리기_5");
            }

            // 4. 두 캐릭터 점프 애니메이션 트리거
            if (p1Animator) p1Animator.SetTrigger(Jump);
            if (p2Animator) p2Animator.SetTrigger(Jump);

            // 5. 점프 애니메이션 재생 시간 대기 (2초)
            yield return CoroutineData.GetWaitForSeconds(2.0f);
            if (p1Animator) p1Animator.SetTrigger(Idle);
            if (p2Animator) p2Animator.SetTrigger(Idle);    
            
            // 6. 대기가 끝나면 다음 페이지로 넘어감
            CompleteStep();
        }

        public override void OnExit()
        {
            base.OnExit();
            StopAllCoroutines();
            _particleRoutine = null;
        }

        // --- Utility Coroutines ---

        private IEnumerator FadeTextAlpha(Text txt, float start, float end, float duration)
        {
            float elapsed = 0f;
            Color c = txt.color;
            txt.color = new Color(c.r, c.g, c.b, start);
            
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float a = Mathf.Lerp(start, end, elapsed / duration);
                txt.color = new Color(c.r, c.g, c.b, a);
                yield return null;
            }
            txt.color = new Color(c.r, c.g, c.b, end);
        }

        private IEnumerator BlinkRoutine(CanvasGroup cg, float interval)
        {
            bool isVisible = true;
            
            while (cg)
            {
                cg.alpha = isVisible ? 1f : 0f;
                yield return CoroutineData.GetWaitForSeconds(interval);
                isVisible = !isVisible;
            }
        }
    }
}