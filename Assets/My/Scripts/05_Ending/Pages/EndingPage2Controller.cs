using System;
using System.Collections;
using My.Scripts.Core;
using UnityEngine;
using UnityEngine.UI;
using Wonjeong.Data;
using Wonjeong.UI;
using Wonjeong.Utils; // CoroutineData 사용

namespace My.Scripts._05_Ending.Pages
{
    [Serializable]
    public class EndingPage2Data
    {
        public TextSetting topTextFormat;
        public TextSetting bottomTextFormat;
    }

    public class EndingPage2Controller : GamePage<EndingPage2Data>
    {
        [Header("UI Groups")]
        [Tooltip("중간 마음 조각 이미지 5개를 묶어둔 CanvasGroup")]
        [SerializeField] private CanvasGroup heartsCg;
        [Tooltip("상단 및 하단 텍스트를 묶어둔 CanvasGroup")]
        [SerializeField] private CanvasGroup textsCg;

        [Header("Texts")]
        [SerializeField] private Text topText;
        [SerializeField] private Text bottomText;

        [Header("Heart Images")]
        [Tooltip("왼쪽부터 순서대로 5개의 별(마음조각) 이미지를 할당하세요.")]
        [SerializeField] private Image[] heartImages;
        [SerializeField] private Sprite heartGetSprite;
        [SerializeField] private Sprite heartDontGetSprite;

        private EndingPage2Data _data;

        protected override void SetupData(EndingPage2Data data)
        {
            _data = data;
        }

        public override void OnEnter()
        {
            base.OnEnter();

            // 1. 초기화: 캔버스 그룹 투명화 (연출 전 숨김 처리)
            if (heartsCg) heartsCg.alpha = 0f;
            if (textsCg) textsCg.alpha = 0f;

            // 2. 획득한 조각 개수 계산 (100M당 1개, 최대 5개로 제한)
            float dist = GameManager.Instance ? GameManager.Instance.lastPlayDistance : 0f;
            int fragments = Mathf.Clamp(Mathf.FloorToInt(dist / 100f), 0, 5);

            // 3. 텍스트 설정
            if (_data != null)
            {
                if (topText && _data.topTextFormat != null)
                {
                    if (UIManager.Instance) UIManager.Instance.SetText(topText.gameObject, _data.topTextFormat);
                    // JSON의 {0} 자리에 획득한 조각 개수를 넣음
                    topText.text = string.Format(_data.topTextFormat.text, fragments);
                }

                if (bottomText && _data.bottomTextFormat != null)
                {
                    if (UIManager.Instance) UIManager.Instance.SetText(bottomText.gameObject, _data.bottomTextFormat);
                    // API 연동 전 임시로 현재 획득한 개수를 {0} 자리에 넣음
                    bottomText.text = string.Format(_data.bottomTextFormat.text, fragments);
                }
            }

            // ★ 4. 조각 이미지 스프라이트 및 투명도(Alpha) 갱신
            if (heartImages != null)
            {
                for (int i = 0; i < heartImages.Length; i++)
                {
                    if (heartImages[i] != null)
                    {
                        bool isGot = i < fragments;
                        
                        // 스프라이트 교체
                        heartImages[i].sprite = isGot ? heartGetSprite : heartDontGetSprite;
                        
                        // 획득 시 알파 1.0(100%), 미획득 시 알파 0.6(60%)
                        Color c = heartImages[i].color;
                        c.a = isGot ? 1.0f : 0.6f;
                        heartImages[i].color = c;
                    }
                }
            }

            // 5. 시퀀스 시작
            StartCoroutine(EntranceSequence());
        }

        /// <summary>
        /// 중간 이미지 페이드인 -> 대기 -> 텍스트 페이드인 -> 대기 순서로 연출을 진행함.
        /// </summary>
        private IEnumerator EntranceSequence()
        {
            // 1. 중간 마음조각 이미지들 페이드인 (1초)
            if (heartsCg)
            {
                yield return StartCoroutine(FadeCanvasGroup(heartsCg, 0f, 1f, 1.0f));
            }

            // 2. 1초 대기
            yield return CoroutineData.GetWaitForSeconds(1.0f);

            // 3. 상단, 하단 텍스트 페이드인 (1초)
            if (textsCg)
            {
                yield return StartCoroutine(FadeCanvasGroup(textsCg, 0f, 1f, 1.0f));
            }

            // 4. 2초 대기 후 완료
            yield return CoroutineData.GetWaitForSeconds(2.0f);
            
            CompleteStep();
        }

        public override void OnExit()
        {
            base.OnExit();
            StopAllCoroutines();
        }

        // --- Utility Coroutine ---
        private IEnumerator FadeCanvasGroup(CanvasGroup cg, float start, float end, float duration)
        {
            float elapsed = 0f;
            cg.alpha = start;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                cg.alpha = Mathf.Lerp(start, end, elapsed / duration);
                yield return null;
            }
            cg.alpha = end;
        }
    }
}