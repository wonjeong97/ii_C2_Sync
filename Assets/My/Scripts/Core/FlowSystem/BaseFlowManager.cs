using System.Threading;
using Cysharp.Threading.Tasks;
using Microsoft.Extensions.Logging;
using UnityEngine;
using VContainer;
using ZLogger;

namespace My.Scripts.Core.FlowSystem
{
    /// <summary>
    /// 여러 개의 GamePage를 순차적으로 전환하며 게임 흐름을 관리하는 최상위 추상 클래스.
    /// UniTask 기반의 비동기 전환 연출을 지원함.
    /// </summary>
    public abstract class BaseFlowManager : MonoBehaviour
    {
        [Header("Base Pages")]
        [SerializeField] protected GamePage[] pages;

        protected int currentPageIndex = -1;
        protected bool isTransitioning;
        protected CancellationTokenSource cts;

        protected ILogger<BaseFlowManager> _logger;

        [Inject]
        public void Construct(ILogger<BaseFlowManager> logger)
        {
            _logger = logger;
        }

        protected virtual void Start()
        {
            cts = new CancellationTokenSource();
            InitializeFlowAsync(cts.Token).Forget();
        }

        protected virtual void OnDestroy()
        {
            cts?.Cancel();
            cts?.Dispose();
            cts = null;
        }

        private async UniTaskVoid InitializeFlowAsync(CancellationToken ct)
        {
            LoadSettings();

            if (pages == null || pages.Length == 0)
            {
                _logger.ZLogWarning($"BaseFlowManager: pages가 할당되지 않았습니다.");
                return;
            }

            InitializePages();
            await StartFlowAsync(ct);
        }

        protected abstract void LoadSettings();
        protected abstract void OnAllFinished();

        protected virtual void InitializePages()
        {
            if (pages == null) return;
            for (int i = 0; i < pages.Length; i++)
            {
                if (pages[i] == null) continue;

                pages[i].gameObject.SetActive(false);
                pages[i].SetAlpha(0f);

                int currentIndex = i;
                int nextIndex = i + 1;

                pages[i].onStepComplete = null;
                pages[i].onStepComplete += (info) => OnPageComplete(currentIndex, nextIndex, info);
            }
        }

        protected virtual async UniTask StartFlowAsync(CancellationToken ct)
        {
            if (pages != null && pages.Length > 0)
            {
                await TransitionAsync(0, 0, ct);
            }
        }

        protected virtual void OnPageComplete(int currentIndex, int nextIndex, int info)
        {
            if (cts == null) return;

            if (nextIndex < pages.Length)
            {
                TransitionAsync(nextIndex, info, cts.Token).Forget();
            }
            else
            {
                OnAllFinished();
            }
        }

        protected virtual async UniTask TransitionAsync(int targetIndex, int info, CancellationToken ct)
        {
            if (isTransitioning) return;
            
            if (pages == null || targetIndex < 0 || targetIndex >= pages.Length)
            {
                _logger.ZLogWarning($"BaseFlowManager: 잘못된 인덱스 접근 시도: {targetIndex}");
                return;
            }

            isTransitioning = true;
            try
            {
                // 1. 현재 페이지 퇴장
                if (currentPageIndex >= 0 && currentPageIndex < pages.Length)
                {
                    var current = pages[currentPageIndex];
                    if (current != null)
                    {
                        await FadePageAsync(current, 1f, 0f, 0.5f, ct);
                        current.OnExit();
                    }
                }

                // 2. 다음 페이지 진입
                currentPageIndex = targetIndex;
                var next = pages[targetIndex];
                if (next)
                {
                    next.OnEnter();
                    await FadePageAsync(next, 0f, 1f, 0.5f, ct);
                }
            }
            finally
            {
                isTransitioning = false;
            }
        }

        protected virtual async UniTask FadePageAsync(GamePage page, float start, float end, float duration, CancellationToken ct)
        {
            if (!page) return;

            if (duration <= 0f)
            {
                page.SetAlpha(end);
                return;
            }

            page.SetAlpha(start);
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                page.SetAlpha(Mathf.Lerp(start, end, elapsed / duration));
                await UniTask.Yield(PlayerLoopTiming.Update, ct);
            }
            page.SetAlpha(end);
        }
    }
}