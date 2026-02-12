using System.Collections;
using UnityEngine;

namespace My.Scripts.Core.FlowSystem
{
    /// <summary>
    /// 여러 개의 GamePage를 순차적으로 전환하며 게임 흐름을 관리하는 최상위 추상 클래스.
    /// 페이지 간의 페이드 인/아웃 전환 연출과 실행 순서를 제어함.
    /// </summary>
    public abstract class BaseFlowManager : MonoBehaviour
    {
        [Header("Base Pages")]
        [SerializeField] protected GamePage[] pages; 

        protected int currentPageIndex = -1; 
        protected bool isTransitioning; 

        /// <summary>
        /// 매니저가 활성화되면 흐름 제어를 시작함.
        /// 데이터 로드 -> 페이지 초기화 -> 첫 페이지 진입 순으로 실행됨.
        /// </summary>
        protected virtual void Start()
        {
            // 자식 클래스에서 구체적인 설정(JSON 등)을 먼저 로드해야 페이지 초기화가 가능함
            LoadSettings(); 
            
            if (pages == null || pages.Length == 0)
            {
                Debug.LogWarning("[BaseFlowManager] pages 비어있음");
                return;
            }

            // 모든 페이지를 비활성화하고 이벤트를 연결하여 흐름 준비
            InitializePages(); 
            
            // 준비가 완료되면 첫 번째 페이지로 진입
            StartFlow(); 
        }

        /// <summary>
        /// 외부 설정 파일이나 데이터를 로드하는 추상 메서드.
        /// 자식 클래스에서 JSON 파싱 등의 구체적인 로직을 구현해야 함.
        /// </summary>
        protected abstract void LoadSettings();

        /// <summary>
        /// 마지막 페이지까지 모두 완료되었을 때 호출되는 추상 메서드.
        /// 씬 전환이나 게임 종료 등의 후처리를 자식 클래스에서 구현함.
        /// </summary>
        protected abstract void OnAllFinished();

        /// <summary>
        /// 등록된 페이지들을 초기화하고 완료 이벤트를 바인딩함.
        /// </summary>
        protected virtual void InitializePages()
        {
            if (pages == null) return;
            for (int i = 0; i < pages.Length; i++)
            {
                if (pages[i] == null) continue;
                
                // 시작 시 모든 페이지가 겹쳐 보이지 않도록 비활성화 및 투명 처리
                pages[i].gameObject.SetActive(false);
                pages[i].SetAlpha(0f);
                
                // 루프 변수 캡처(Closure) 문제 방지를 위해 로컬 변수에 복사
                int currentIndex = i;
                int nextIndex = i + 1;
                
                // 중복 구독을 방지하기 위해 기존 델리게이트를 초기화한 후 재연결
                // 페이지가 완료되면 자동으로 다음 인덱스로 넘어가도록 로직을 구성함
                pages[i].onStepComplete = null; 
                pages[i].onStepComplete += (info) => OnPageComplete(currentIndex, nextIndex, info);
            }
        }

        /// <summary>
        /// 흐름을 시작하여 첫 번째 페이지(Index 0)로 진입함.
        /// </summary>
        protected virtual void StartFlow()
        {
            if (pages != null && pages.Length > 0)
            {
                TransitionToPage(0);
            }
        }

        /// <summary>
        /// 특정 페이지가 완료되었을 때 호출되어 흐름을 분기함.
        /// </summary>
        /// <param name="currentIndex">완료된 페이지 인덱스</param>
        /// <param name="nextIndex">다음에 실행될 페이지 인덱스</param>
        /// <param name="info">이벤트 트리거 정보</param>
        protected virtual void OnPageComplete(int currentIndex, int nextIndex, int info)
        {
            // 다음 페이지가 존재하면 전환하고, 없으면 전체 플로우 종료 처리
            if (nextIndex < pages.Length)
            {
                TransitionToPage(nextIndex, info);
            }
            else
            {
                OnAllFinished();
            }
        }

        /// <summary>
        /// 목표 페이지로의 전환 연출을 시작함.
        /// </summary>
        /// <param name="targetIndex">전환할 목표 페이지 인덱스</param>
        /// <param name="info">전달할 추가 정보</param>
        protected virtual void TransitionToPage(int targetIndex, int info = 0)
        {
            // 이미 전환 연출이 진행 중이라면 중복 실행을 막아 흐름 꼬임을 방지함
            if (isTransitioning) return;
            
            // 유효하지 않은 인덱스 접근 방지
            if (pages == null || targetIndex < 0 || targetIndex >= pages.Length)
            {
                Debug.LogWarning($"[BaseFlowManager] 잘못된 인덱스: {targetIndex}");
                return;
            }

            isTransitioning = true;
            StartCoroutine(TransitionRoutine(targetIndex, info));
        }

        /// <summary>
        /// 실제 페이지 전환(퇴장 -> 등장)을 순차적으로 수행하는 코루틴.
        /// </summary>
        protected virtual IEnumerator TransitionRoutine(int targetIndex, int info)
        {
            try
            {
                // 1. 현재 실행 중인 페이지가 있다면 서서히 퇴장(Fade Out)시킴
                if (currentPageIndex >= 0 && currentPageIndex < pages.Length)
                {
                    var current = pages[currentPageIndex];
                    if (current != null)
                    {
                        yield return StartCoroutine(FadePage(current, 1f, 0f));
                        current.OnExit();
                    }
                }

                // 2. 다음 페이지로 인덱스를 갱신하고 진입 준비(초기화)
                currentPageIndex = targetIndex;
                var next = pages[targetIndex];
                if (next != null)
                {
                    next.OnEnter(); 
                    
                    // 3. 준비된 페이지를 서서히 등장(Fade In)시킴
                    yield return StartCoroutine(FadePage(next, 0f, 1f));
                }
            }
            finally
            {
                // 예외가 발생하더라도 전환 플래그는 반드시 해제하여 다음 진행이 가능하도록 함
                isTransitioning = false;
            }
        }

        /// <summary>
        /// CanvasGroup의 Alpha 값을 조절하여 페이드 효과를 주는 코루틴.
        /// </summary>
        protected IEnumerator FadePage(GamePage page, float start, float end, float duration = 0.5f)
        {
            if (!page) yield break;

            // 즉시 전환이 필요한 경우(duration <= 0) 바로 값을 세팅하고 종료
            if (duration <= 0f)
            {
                page.SetAlpha(end);
                yield break;
            }
            
            float t = 0f;
            page.SetAlpha(start);

            // 지정된 시간 동안 Alpha 값을 선형 보간하여 부드러운 전환 효과 구현
            while (t < duration)
            {
                t += Time.deltaTime;
                page.SetAlpha(Mathf.Lerp(start, end, t / duration));
                yield return null;
            }
            page.SetAlpha(end);
        }
    }
}