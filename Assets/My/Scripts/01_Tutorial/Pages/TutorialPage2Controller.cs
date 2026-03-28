using System;
using System.Collections;
using My.Scripts.Core;
using UnityEngine;
using UnityEngine.UI;
using Wonjeong.Data;
using Wonjeong.UI;
using Wonjeong.Utils;

namespace My.Scripts._01_Tutorial.Pages
{
    [Serializable]
    public class TutorialPage2Data
    {
        public TextSetting descriptionText;
    }

    public class TutorialPage2Controller : GamePage<TutorialPage2Data>
    {
        [Header("UI Components")]
        [SerializeField] private Text descriptionText;

        private bool _isCompleted;

        /// <summary>
        /// 외부 데이터를 받아 UI 텍스트를 세팅함.
        /// </summary>
        /// <param name="data">적용할 텍스트 설정 데이터</param>
        protected override void SetupData(TutorialPage2Data data)
        {
            if (data == null)
            {
                Debug.LogWarning("TutorialPage2Data is null.");
                return;
            }

            if (descriptionText)
            {
                if (data.descriptionText != null)
                {
                    if (UIManager.Instance)
                    {
                        UIManager.Instance.SetText(descriptionText.gameObject, data.descriptionText);
                    }
                    else
                    {
                        Debug.LogWarning("UIManager.Instance is null.");
                    }
                }
            }
            else
            {
                Debug.LogWarning("descriptionText is null.");
            }
        }

        /// <summary>
        /// 페이지 진입 시 호출됨.
        /// 자동 넘김 연출을 시작함.
        /// </summary>
        public override void OnEnter()
        {
            base.OnEnter();
            
            // 이유: 유저 입력 없이 자동으로 넘어가는 연출 구간이므로 글로벌 무입력 방치 타이머를 정지시킴.
            if (GameManager.Instance) GameManager.Instance.IsAutoProgressing = true;

            _isCompleted = false;

            StartCoroutine(AutoPassRoutine());
        }

        /// <summary>
        /// 지정된 시간 대기 후 다음 페이지로 자동 전환함.
        /// </summary>
        /// <returns>IEnumerator 루틴</returns>
        private IEnumerator AutoPassRoutine()
        {
            // # TODO: 대기 시간 하드코딩(3.0f)을 외부 설정 데이터로 분리하여 기획자 제어 편의성 향상 필요.
            if (SoundManager.Instance) SoundManager.Instance.PlaySFX("공통_6");
            else Debug.LogWarning("SoundManager.Instance is null.");

            yield return CoroutineData.GetWaitForSeconds(3.0f);

            // 이유: 중복 호출 방지를 위해 완료 상태를 체크함.
            if (!_isCompleted)
            {
                _isCompleted = true;
                CompleteStep(); 
            }
        }
        
        /// <summary>
        /// 페이지 퇴장 시 호출됨.
        /// 실행 중인 코루틴을 강제 종료함.
        /// </summary>
        public override void OnExit()
        {
            base.OnExit();
            
            // 이유: 페이지가 전환될 때 대기 루틴이 백그라운드에서 계속 실행되는 것을 막음.
            StopAllCoroutines(); 
        }
    }
}