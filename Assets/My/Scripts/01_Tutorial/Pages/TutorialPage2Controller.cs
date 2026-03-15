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

        protected override void SetupData(TutorialPage2Data data)
        {
            if (data == null) return;

            if (descriptionText != null && data.descriptionText != null)
            {
                if (UIManager.Instance != null)
                {
                    UIManager.Instance.SetText(descriptionText.gameObject, data.descriptionText);
                }
            }
        }

        public override void OnEnter()
        {
            base.OnEnter();
            
            // 이유: 유저 입력 없이 자동으로 넘어가는 연출 구간이므로 글로벌 무입력 타이머를 정지시킴
            if (GameManager.Instance) GameManager.Instance.IsAutoProgressing = true;

            _isCompleted = false;

            StartCoroutine(AutoPassRoutine());
        }

        private IEnumerator AutoPassRoutine()
        {
            if (SoundManager.Instance) SoundManager.Instance.PlaySFX("공통_6");
            yield return CoroutineData.GetWaitForSeconds(3.0f);

            if (!_isCompleted)
            {
                _isCompleted = true;
                CompleteStep(); 
            }
        }
        
        public override void OnExit()
        {
            base.OnExit();
            StopAllCoroutines(); 
        }
    }
}