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

        private bool _isAllFinished; 
        private bool _hasSentEndTime;

        protected override void SetupData(EndingPage3Data data)
        {
            if (data == null) return;

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
            base.OnEnter(); 

            if (redLineImage)
            {
                redLineImage.type = Image.Type.Filled;
                redLineImage.fillAmount = 0f;
                redLineImage.gameObject.SetActive(_isAllFinished); 
            }
            
            // --- [수정] 정상 종료 시 End 시간 기록 및 방 점유 초기화(exitRoom) 동시 진행 ---
            if (!_hasSentEndTime && GameManager.Instance)
            {
                _hasSentEndTime = true;
                GameManager.Instance.SendTimeUpdateAPI(); // 4번: 유저의 End값을 할당
                GameManager.Instance.SendExitRoomAPI();   // 4번: 방 점유 초기화 추가
            }
            // -----------------------------------------------------------------------

            StartCoroutine(SequenceRoutine());
        }

        private IEnumerator SequenceRoutine()
        {   
            yield return CoroutineData.GetWaitForSeconds(0.5f);
            yield return CoroutineData.GetWaitForSeconds(1.0f);
            
            if (_isAllFinished && redLineImage)
            {
                yield return StartCoroutine(FillImageRoutine(redLineImage, 0f, 1f, 2.0f));
                SoundManager.Instance?.FadeOutBGM(5.0f);
                yield return CoroutineData.GetWaitForSeconds(5.0f);
            }
            else
            {   
                yield return CoroutineData.GetWaitForSeconds(2.0f);
                SoundManager.Instance?.FadeOutBGM(5.0f);
                yield return CoroutineData.GetWaitForSeconds(5.0f);
            }
            
            CompleteStep();
        }

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