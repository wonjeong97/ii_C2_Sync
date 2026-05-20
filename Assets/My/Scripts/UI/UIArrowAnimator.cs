using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Microsoft.Extensions.Logging;
using UnityEngine;
using VContainer;
using ZLogger;

namespace My.Scripts.UI
{
    /// <summary>
    /// UI 화살표의 이동 및 투명도 루프 애니메이션을 제어하는 컴포넌트.
    /// 코루틴 대신 UniTask를 사용하여 메모리 할당을 최적화함.
    /// </summary>
    [RequireComponent(typeof(CanvasGroup), typeof(RectTransform))]
    public class UIArrowAnimator : MonoBehaviour
    {
        [Header("Animation Settings")]
        public float moveX = 55f;
        public float duration = 1.2f;
        public float startDelay = 0f;

        private RectTransform _rect;
        private CanvasGroup _cg;
        private Vector2 _originPos;
        private CancellationTokenSource _cts;

        private ILogger<UIArrowAnimator> _logger;

        [Inject]
        public void Construct(ILogger<UIArrowAnimator> logger)
        {
            _logger = logger;
        }

        private void Awake()
        {
            _rect = GetComponent<RectTransform>();
            _cg = GetComponent<CanvasGroup>();
            _originPos = _rect.anchoredPosition;
        }

        private void OnEnable() => ResetState();

        private void OnDisable() => CancelAnimation();

        /// <summary>
        /// 애니메이션 루프를 비동기로 시작함.
        /// </summary>
        public void Play()
        {
            CancelAnimation();
            gameObject.SetActive(true);
            _cts = new CancellationTokenSource();
            PlayLoopAsync(_cts.Token).Forget();
        }

        /// <summary>
        /// 애니메이션을 즉시 중단하고 오브젝트를 비활성화함.
        /// </summary>
        public void Stop()
        {
            CancelAnimation();
            ResetState();
            gameObject.SetActive(false);
        }

        /// <summary>
        /// 지정된 시간 동안 페이드 아웃 후 비활성화.
        /// </summary>
        public void FadeOutAndStop(float fadeDuration = 1.0f)
        {
            if (!gameObject.activeInHierarchy) return;
            
            CancelAnimation();
            FadeOutAsync(fadeDuration, this.GetCancellationTokenOnDestroy()).Forget();
        }

        private void ResetState()
        {
            if (_cg) _cg.alpha = 0f;
            if (_rect) _rect.anchoredPosition = _originPos;
        }

        private void CancelAnimation()
        {
            if (_cts != null)
            {
                _cts.Cancel();
                _cts.Dispose();
                _cts = null;
            }
        }

        private async UniTaskVoid PlayLoopAsync(CancellationToken ct)
        {
            try
            {
                if (startDelay > 0) await UniTask.Delay(TimeSpan.FromSeconds(startDelay), cancellationToken: ct);

                while (!ct.IsCancellationRequested)
                {
                    float timer = 0f;
                    _rect.anchoredPosition = _originPos;
                    _cg.alpha = 0f;

                    while (timer < duration)
                    {
                        timer += Time.deltaTime;
                        float t = timer / duration;

                        // 이동 애니메이션 (Sin 곡선으로 자연스러운 감속)
                        float moveT = Mathf.Sin(t * Mathf.PI * 0.5f); 
                        _rect.anchoredPosition = _originPos + new Vector2(moveX * moveT, 0);

                        // 페이드 연출
                        _cg.alpha = t < 0.2f ? t / 0.2f : (t > 0.7f ? 1f - ((t - 0.7f) / 0.3f) : 1f);
                        
                        await UniTask.Yield(PlayerLoopTiming.Update, ct);
                    }
                    await UniTask.Yield(PlayerLoopTiming.Update, ct);
                }
            }
            catch (OperationCanceledException) { /* 정상 취소 */ }
        }

        private async UniTaskVoid FadeOutAsync(float fadeDuration, CancellationToken ct)
        {
            float startAlpha = _cg.alpha;
            float timer = 0f;

            while (timer < fadeDuration)
            {
                timer += Time.deltaTime;
                _cg.alpha = Mathf.Lerp(startAlpha, 0f, timer / fadeDuration);
                await UniTask.Yield(PlayerLoopTiming.Update, ct);
            }

            _cg.alpha = 0f;
            gameObject.SetActive(false);
        }
    }
}