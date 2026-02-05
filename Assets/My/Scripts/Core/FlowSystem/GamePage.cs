using System;
using UnityEngine;

namespace My.Scripts.Core
{
    /// <summary>
    /// 모든 UI 페이지 컨트롤러의 최상위 추상 클래스.
    /// 페이지의 활성화/비활성화, 투명도 제어(페이드 효과), 완료 이벤트 전달 등의 공통 기능을 제공함.
    /// </summary>
    public abstract class GamePage : MonoBehaviour
    {
        // 페이지가 완료되었을 때 상위 매니저(BaseFlowManager)에게 알리기 위한 이벤트
        // int 파라미터는 분기 처리가 필요한 경우 트리거 정보로 사용됨
        public Action<int> onStepComplete; 
        
        protected CanvasGroup canvasGroup; 

        protected virtual void Awake()
        {
            canvasGroup = GetComponent<CanvasGroup>();
            // 페이드 인/아웃 연출을 위해 CanvasGroup이 필수적이므로, 누락되었을 경우 자동으로 추가하여 에러를 방지함
            if (canvasGroup == null) canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }

        /// <summary>
        /// 외부 데이터를 받아 페이지를 초기화하는 추상 메서드.
        /// 구체적인 데이터 타입은 제네릭 자식 클래스에서 정의함.
        /// </summary>
        public abstract void SetupData(object data);

        /// <summary>
        /// 페이지 진입 시 호출되어 오브젝트를 활성화하고 화면에 표시함.
        /// </summary>
        public virtual void OnEnter() 
        { 
            gameObject.SetActive(true);
            SetAlpha(1f); // 이전 상태(투명)가 남아있을 수 있으므로 확실하게 초기화
        }

        /// <summary>
        /// 페이지 퇴장 시 호출되어 오브젝트를 비활성화함.
        /// </summary>
        public virtual void OnExit() 
        { 
            gameObject.SetActive(false); 
        }

        /// <summary>
        /// CanvasGroup의 Alpha 값을 조절하여 투명도를 설정함.
        /// 주로 페이드 연출 코루틴에서 매 프레임 호출됨.
        /// </summary>
        public void SetAlpha(float alpha)
        {
            if (canvasGroup) canvasGroup.alpha = alpha;
        }

        /// <summary>
        /// 현재 페이지의 로직이 완료되었음을 매니저에게 알림.
        /// </summary>
        /// <param name="triggerInfo">다음 단계 분기를 위한 추가 정보 (기본값 0)</param>
        protected void CompleteStep(int triggerInfo = 0)
        {
            onStepComplete?.Invoke(triggerInfo);
        }
    }

    /// <summary>
    /// 특정 데이터 타입(T)을 사용하는 페이지 컨트롤러의 제네릭 부모 클래스.
    /// object 타입으로 들어오는 데이터를 구체적인 타입(T)으로 안전하게 변환하여 자식 클래스에 전달하는 역할을 함.
    /// </summary>
    /// <typeparam name="T">이 페이지에서 사용할 데이터 클래스 타입 (예: TutorialPage1Data)</typeparam>
    public abstract class GamePage<T> : GamePage where T : class
    {
        /// <summary>
        /// 매니저가 호출하는 비제네릭 SetupData를 오버라이드하여 타입 캐스팅을 수행.
        /// 자식 클래스에서는 이 메서드가 아닌, 타입이 명시된 SetupData(T)를 구현하도록 강제하기 위해 sealed로 막음.
        /// </summary>
        public sealed override void SetupData(object data)
        {
            // 데이터 타입이 일치할 때만 자식 클래스의 설정 메서드를 호출하여 타입 안전성 보장
            if (data is T typedData)
            {
                SetupData(typedData);
            }
        }

        /// <summary>
        /// 실제 데이터 타입(T)을 받아 초기화를 수행하는 추상 메서드.
        /// 자식 클래스에서 UI 갱신 등의 구체적인 로직을 구현해야 함.
        /// </summary>
        protected abstract void SetupData(T data);
    }
}