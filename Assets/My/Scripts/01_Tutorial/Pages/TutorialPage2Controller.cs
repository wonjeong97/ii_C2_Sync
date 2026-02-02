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

        private bool _isCompleted = false;

        // 1. 데이터 주입
        protected override void SetupData(TutorialPage2Data data)
        {
            if (data == null) return;

            // UIManager를 이용해 JSON 설정값(내용, 폰트, 위치 등)을 UI에 적용
            if (descriptionText != null && data.descriptionText != null)
            {
                if (UIManager.Instance != null)
                {
                    UIManager.Instance.SetText(descriptionText.gameObject, data.descriptionText);
                }
            }
        }

        // 2. 페이지 진입
        public override void OnEnter()
        {
            base.OnEnter();
            
            _isCompleted = false; // 상태 초기화

            // 3초 후 자동으로 넘어가는 코루틴 실행
            StartCoroutine(AutoPassRoutine());
        }

        // 자동 넘김 코루틴
        private IEnumerator AutoPassRoutine()
        {
            yield return CoroutineData.GetWaitForSeconds(3.0f);

            // 아직 완료되지 않았다면 다음 단계로 진행
            if (!_isCompleted)
            {
                _isCompleted = true;
                CompleteStep(); 
            }
        }

        // 3. 업데이트 (입력 감지 - 스킵 기능)
        private void Update()
        {
            if (_isCompleted) return;

            // 엔터 키 또는 마우스 클릭 시 3초 기다리지 않고 즉시 다음 단계로 진행
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetMouseButtonDown(0))
            {
                _isCompleted = true;
                StopAllCoroutines(); // 대기 중이던 자동 넘김 타이머 취소
                CompleteStep();
            }
        }
        
        public override void OnExit()
        {
            base.OnExit();
            StopAllCoroutines(); // 페이지를 나갈 때 코루틴 확실히 정리
        }
    }
}