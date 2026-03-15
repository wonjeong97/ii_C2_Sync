using System;
using System.Collections;
using My.Scripts.Core;
using My.Scripts.Global;
using UnityEngine;
using UnityEngine.UI;
using Wonjeong.Data;
using Wonjeong.UI;
using Wonjeong.Utils;

namespace My.Scripts._05_Ending.Pages
{
    [Serializable]
    public class EndingPage3Data
    {
        public TextSetting resultText;
        public TextSetting allFinishedText;
    }

    /// <summary> 
    /// 엔딩 씬의 마지막 페이지 컨트롤러.
    /// 타 카트리지 콘텐츠 클리어 여부에 따라 일반 엔딩 또는 진엔딩(붉은 실 연출)을 분기하여 표시함.
    /// </summary>
    public class EndingPage3Controller : GamePage<EndingPage3Data>
    {
        [Header("UI References")] 
        [SerializeField] private Text result;
        [SerializeField] private Image redLineImage;

        private EndingPage3Data _data;
        private bool _isAllFinished; 
        private bool _hasSentEndTime;

        /// <summary> JSON 파싱 시 생성된 텍스트 설정 데이터를 페이지 진입 시 활용하기 위해 내부 변수에 캐싱함. </summary>
        protected override void SetupData(EndingPage3Data data)
        {
            _data = data;
        }

        /// <summary> 타 카트리지 클리어 여부에 따라 진엔딩 조건을 평가하고, 결과에 맞는 텍스트와 UI(붉은 실)를 동적으로 구성함. </summary>
        public override void OnEnter()
        {
            base.OnEnter(); 

            _isAllFinished = false;

            if (SessionManager.Instance)
            {
                _isAllFinished = SessionManager.Instance.IsOtherCartridgeContentsCleared;
            }

            if (_data != null)
            {
                TextSetting textToUse = _isAllFinished && _data.allFinishedText != null 
                    ? _data.allFinishedText 
                    : _data.resultText;

                if (result && UIManager.Instance) 
                {
                    UIManager.Instance.SetText(result.gameObject, textToUse);
                }
            }

            if (redLineImage)
            {
                redLineImage.type = Image.Type.Filled;
                redLineImage.fillAmount = 0f;
                redLineImage.gameObject.SetActive(_isAllFinished); 
            }
            
            // 정상 종료 시 End 시간 기록 및 방 점유 초기화(exitRoom)를 동시 진행하여 세션을 안전하게 닫음.
            if (!_hasSentEndTime && GameManager.Instance)
            {
                _hasSentEndTime = true;
                GameManager.Instance.SendTimeUpdateAPI(); 
                GameManager.Instance.SendExitRoomAPI();   
            }

            StartCoroutine(SequenceRoutine());
        }

        /// <summary> 분기된 엔딩 타입(일반/특별)에 맞춰 BGM 페이드아웃 및 붉은 실 생성 시각 효과의 진행 시간을 동기화함. </summary>
        private IEnumerator SequenceRoutine()
        {   
            yield return CoroutineData.GetWaitForSeconds(1.5f);
            
            if (_isAllFinished && redLineImage)
            {
                yield return StartCoroutine(FillImageRoutine(redLineImage, 0f, 1f, 2.0f));
                if (SoundManager.Instance) SoundManager.Instance.FadeOutBGM(5.0f);
                yield return CoroutineData.GetWaitForSeconds(5.0f);
            }
            else
            {   
                yield return CoroutineData.GetWaitForSeconds(2.0f);
                if (SoundManager.Instance) SoundManager.Instance.FadeOutBGM(5.0f);
                yield return CoroutineData.GetWaitForSeconds(5.0f);
            }
            
            CompleteStep();
        }

        /// <summary> UI 이미지의 Fill Amount 속성을 조절하여 선이 점진적으로 이어지는 애니메이션을 표현함. </summary>
        private IEnumerator FillImageRoutine(Image image, float start, float end, float duration)
        {
            if (!image) yield break;
            float time = 0f;
            image.fillAmount = start;
            
            while (time < duration)
            {
                time += Time.deltaTime;
                image.fillAmount = Mathf.Lerp(start, end, time / duration);
                yield return null;
            }

            image.fillAmount = end;
        }
        
        /// <summary> 페이지 퇴장 시 코루틴을 정리하고 UI 상태를 초기화함. </summary>
        public override void OnExit()
        {
            base.OnExit();
            StopAllCoroutines();
            if (redLineImage)
            {
                redLineImage.fillAmount = 0f;
                redLineImage.gameObject.SetActive(false);
            }
        }
    }
}