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
        private bool _hasSentPieceUpdate;

        protected override void SetupData(EndingPage2Data data)
        {
            if (data == null) return;
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

            // 3. API 업데이트 및 총 획득량 계산
            int totalPieces = fragments;
            if (GameManager.Instance)
            {
                // PlayLong(현재 콘텐츠)에서 얻은 마음 조각을 로컬 변수에 캐싱함
                // 하단 텍스트의 TotalPieces 연산에 즉시 반영하기 위함임
                GameManager.Instance.PieceC2 = fragments;
                totalPieces = GameManager.Instance.TotalPieces;

                // 서버에 획득한 마음 조각 데이터를 실시간으로 동기화함
                if (GameManager.Instance && !_hasSentPieceUpdate)
                {
                    GameManager.Instance.SendPieceUpdateAPI(fragments);
                    _hasSentPieceUpdate = true;
                }
            }

            // 4. 텍스트 데이터 세팅
            if (_data != null)
            {
                if (topText && _data.topTextFormat != null)
                {
                    if (UIManager.Instance) UIManager.Instance.SetText(topText.gameObject, _data.topTextFormat);
                    topText.text = string.Format(_data.topTextFormat.text, fragments);
                }

                if (bottomText && _data.bottomTextFormat != null)
                {
                    if (UIManager.Instance) UIManager.Instance.SetText(bottomText.gameObject, _data.bottomTextFormat);
                    bottomText.text = string.Format(_data.bottomTextFormat.text, totalPieces);
                }
            }

            // 5. 조각 이미지 스프라이트 및 초기 투명도 설정
            if (heartImages != null)
            {
                for (int i = 0; i < heartImages.Length; i++)
                {
                    if (heartImages[i])
                    {
                        bool isGot = i < fragments;
                        heartImages[i].sprite = isGot ? heartGetSprite : heartDontGetSprite;
                        
                        // 획득한 조각은 순차적 연출을 위해 투명(0)으로 초기화하고, 
                        // 미획득 조각은 배경 그룹(heartsCg) 페이드인 시 함께 나타나도록 반투명(0.6)으로 설정함
                        Color c = heartImages[i].color;
                        c.a = isGot ? 0.0f : 0.6f;
                        heartImages[i].color = c;
                    }
                }
            }

            // 6. 시퀀스 시작
            StartCoroutine(EntranceSequence(fragments));
        }

        /// <summary>
        /// 요청된 기획에 맞춰 대기 -> 컨테이너 페이드인 -> 개별 조각 순차 페이드인 -> 텍스트 표시 순서로 진행함.
        /// </summary>
        private IEnumerator EntranceSequence(int fragments)
        {
            // 1. 페이지 입장 후 시각적 안정을 위한 0.5초 대기
            yield return CoroutineData.GetWaitForSeconds(0.5f);

            // 2. 전체 마음 조각 배경/틀 그룹을 0.5초 동안 페이드인
            if (heartsCg)
            {
                yield return StartCoroutine(UIUtils.FadeCanvasGroup(heartsCg, 0f, 1f, 0.5f));
            }

            // 3. 획득한 조각 개수만큼 1번부터 순차적으로 알파값 0 -> 1 연출 (각 0.5초)
            if (heartImages != null)
            {
                for (int i = 0; i < fragments; i++)
                {
                    if (i < heartImages.Length && heartImages[i])
                    {
                        if (SoundManager.Instance) SoundManager.Instance.PlaySFX("공통_6");
                        yield return StartCoroutine(FadeImageAlpha(heartImages[i], 0f, 1f, 0.5f));
                    }
                }
            }

            // (선택) 텍스트 등장 전 약간의 자연스러운 대기 시간
            yield return CoroutineData.GetWaitForSeconds(0.5f);

            // 4. 텍스트 캔버스 그룹 페이드인
            if (textsCg)
            {
                yield return StartCoroutine(UIUtils.FadeCanvasGroup(textsCg, 0f, 1f, 1.0f));
            }

            // 5. 유저가 결과를 인지할 수 있도록 대기 후 다음 페이지로 전환
            yield return CoroutineData.GetWaitForSeconds(2.0f);
            
            CompleteStep();
        }

        /// <summary> 개별 이미지의 알파값을 부드럽게 조절하기 위한 전용 코루틴 </summary>
        private IEnumerator FadeImageAlpha(Image img, float start, float end, float duration)
        {
            if (!img) yield break;
            
            float time = 0f;
            Color c = img.color;
            c.a = start;
            img.color = c;

            while (time < duration)
            {
                time += Time.deltaTime;
                c.a = Mathf.Lerp(start, end, time / duration);
                img.color = c;
                yield return null;
            }
            
            // 오차 보정
            c.a = end;
            img.color = c;
        }

        public override void OnExit()
        {
            base.OnExit();
            StopAllCoroutines();
        }
    }
}