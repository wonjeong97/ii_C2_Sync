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
    /// <summary>
    /// 튜토리얼 2페이지의 데이터 구조.
    /// JSON 파일의 "page2" 섹션과 매핑되며, 화면에 표시할 설명 텍스트 설정을 담고 있음.
    /// </summary>
    [Serializable]
    public class TutorialPage2Data
    {
        public TextSetting descriptionText;
    }

    /// <summary>
    /// 튜토리얼의 두 번째 페이지를 제어하는 컨트롤러.
    /// 일정 시간(3초) 후 자동으로 다음 단계로 넘어가거나, 사용자 입력으로 즉시 스킵할 수 있는 기능을 제공함.
    /// </summary>
    public class TutorialPage2Controller : GamePage<TutorialPage2Data>
    {
        [Header("UI Components")]
        [SerializeField] private Text descriptionText;

        private bool _isCompleted;

        /// <summary>
        /// 로드된 데이터를 UI 컴포넌트에 적용함.
        /// </summary>
        /// <param name="data">페이지 설정 데이터</param>
        protected override void SetupData(TutorialPage2Data data)
        {
            if (data == null) return;

            // UIManager를 통해 텍스트의 스타일(폰트, 색상 등)과 내용을 일괄 적용하여
            // 데이터 기반의 UI 구성을 유지함.
            if (descriptionText != null && data.descriptionText != null)
            {
                if (UIManager.Instance != null)
                {
                    UIManager.Instance.SetText(descriptionText.gameObject, data.descriptionText);
                }
            }
        }

        /// <summary>
        /// 페이지가 활성화될 때 호출되어 초기화 및 자동 진행 로직을 시작함.
        /// </summary>
        public override void OnEnter()
        {
            base.OnEnter();
            
            // 페이지 재진입 시 상태가 꼬이지 않도록 플래그를 초기화함
            _isCompleted = false;

            // 사용자가 가만히 있어도 튜토리얼이 진행되도록 자동 넘김 타이머를 시작함
            StartCoroutine(AutoPassRoutine());
        }

        /// <summary>
        /// 일정 시간 대기 후 다음 단계로 진행하는 코루틴.
        /// </summary>
        private IEnumerator AutoPassRoutine()
        {
            // 사용자가 텍스트를 읽을 시간을 부여하기 위해 3초간 대기
            yield return CoroutineData.GetWaitForSeconds(3.0f);

            // 대기 시간 동안 사용자가 이미 스킵하지 않았을 경우에만 완료 처리
            if (!_isCompleted)
            {
                _isCompleted = true;
                CompleteStep(); 
            }
        }
        
        private void Update()
        {
            // 이미 완료된 상태라면 중복 처리를 방지하기 위해 로직을 수행하지 않음
            if (_isCompleted) return;

            // 성격 급한 사용자나 이미 내용을 아는 사용자를 위해 클릭/엔터로 대기 시간을 건너뛰게 함
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetMouseButtonDown(0))
            {
                _isCompleted = true;
                
                // 자동 넘김 코루틴이 나중에 중복으로 실행되지 않도록 강제 종료함
                StopAllCoroutines(); 
                CompleteStep();
            }
        }
        
        /// <summary>
        /// 페이지가 비활성화될 때 호출되어 정리 작업을 수행함.
        /// </summary>
        public override void OnExit()
        {
            base.OnExit();
            // 페이지를 나간 후에는 더 이상 자동 넘김 로직이 돌지 않도록 확실하게 정리함
            StopAllCoroutines(); 
        }
    }
}