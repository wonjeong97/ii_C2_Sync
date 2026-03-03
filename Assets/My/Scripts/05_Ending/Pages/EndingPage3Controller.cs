using System;
using System.Collections;
using My.Scripts.Core;
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
    /// 엔딩 씬의 마지막 페이지 컨트롤러입니다.
    /// 플레이어에게 최종 메시지를 전달하며, 50% 확률로 '붉은 실(Red Line)' 연출을 포함한 특별 엔딩을 보여줍니다.
    /// </summary>
    public class EndingPage3Controller : GamePage<EndingPage3Data>
    {
        [Header("UI References")] 
        [SerializeField] private Text result;
        [SerializeField] private Image redLineImage;

        private bool _isAllFinished; // 특별 엔딩(붉은 실) 활성화 여부
        private bool _hasSentEndTime;

        protected override void SetupData(EndingPage3Data data)
        {
            if (data == null) return;

            // 엔딩의 다양성을 위해 50% 확률로 일반 엔딩과 특별 엔딩(Red Line)을 분기합니다.
            // # TODO: 현재는 랜덤이지만, 추후 API에 따라 변경 필요
            int randomValue = UnityEngine.Random.Range(0, 2);
            TextSetting textToUse;

            if (randomValue == 1 && data.allFinishedText != null)
            {
                textToUse = data.allFinishedText;
                _isAllFinished = true;
            }
            else
            {
                textToUse = data.resultText;
                _isAllFinished = false;
            }

            if (result && UIManager.Instance) 
            {
                UIManager.Instance.SetText(result.gameObject, textToUse);
            }
        }

        public override void OnEnter()
        {
            base.OnEnter(); // BaseFlowManager에서 자동으로 알파값을 0 -> 1로 페이드인 시켜줍니다.

            if (redLineImage)
            {
                redLineImage.type = Image.Type.Filled;
                redLineImage.fillAmount = 0f;
                // 특별 엔딩이 아닐 경우 이미지 오브젝트 자체를 비활성화
                redLineImage.gameObject.SetActive(_isAllFinished); 
            }
            
            if (!_hasSentEndTime && GameManager.Instance)
            {
                _hasSentEndTime = true;
                GameManager.Instance.SendTimeUpdateAPI();
            }

            StartCoroutine(SequenceRoutine());
        }

        /// <summary>
        /// 엔딩 시퀀스 루틴입니다. 분기된 엔딩 타입에 따라 다른 연출과 대기 시간을 가집니다.
        /// </summary>
        private IEnumerator SequenceRoutine()
        {   
            // 1. BaseFlowManager가 페이지를 페이드인 하는 시간(0.5초) 동안 대기
            yield return CoroutineData.GetWaitForSeconds(0.5f);
            
            // 2. 글자가 다 나타난 후 약간의 딜레이(1초)를 주어 자연스러운 템포 형성
            yield return CoroutineData.GetWaitForSeconds(1.0f);
            
            // 3. 분기별 연출
            if (_isAllFinished && redLineImage)
            {
                // 특별 엔딩: 붉은 실이 차오르는 연출
                yield return StartCoroutine(FillImageRoutine(redLineImage, 0f, 1f, 2.0f));
                SoundManager.Instance?.FadeOutBGM(5.0f);
                yield return CoroutineData.GetWaitForSeconds(5.0f);
            }
            else
            {   
                // 일반 엔딩: 텍스트만 보여준 채 대기
                yield return CoroutineData.GetWaitForSeconds(2.0f);
                SoundManager.Instance?.FadeOutBGM(5.0f);
                yield return CoroutineData.GetWaitForSeconds(5.0f);
            }
            
            CompleteStep();
        }

        // --- Utility Coroutines ---

        /// <summary>
        /// 이미지의 FillAmount를 조절하여 게이지가 차오르는 듯한 연출을 수행합니다. (붉은 실 연출용)
        /// </summary>
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