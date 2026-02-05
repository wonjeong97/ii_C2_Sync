using System.Collections;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(CanvasGroup))]
public class UIArrowAnimator : MonoBehaviour
{
    [Header("Animation Settings")]
    [Tooltip("이동할 거리 (X축)")]
    public float moveX = 55f;
    public float duration = 1.2f;
    public float startDelay = 0f;

    private RectTransform _rect;
    private CanvasGroup _cg;
    private Vector2 _originPos;
    private Coroutine _animRoutine;

    private void Awake()
    {
        _rect = GetComponent<RectTransform>();
        _cg = GetComponent<CanvasGroup>();
        _originPos = _rect.anchoredPosition;
    }

    private void OnEnable() => ResetState();

    public void Play()
    {
        gameObject.SetActive(true);
        if (_animRoutine != null) StopCoroutine(_animRoutine);
        _animRoutine = StartCoroutine(LoopRoutine());
    }

    public void Stop()
    {
        if (_animRoutine != null) StopCoroutine(_animRoutine);
        ResetState();
        gameObject.SetActive(false);
    }

    // ★ [추가] 1초 동안 페이드 아웃 후 정지
    public void FadeOutAndStop(float fadeDuration = 1.0f)
    {
        if (!gameObject.activeInHierarchy) return;
        if (_animRoutine != null) StopCoroutine(_animRoutine);
        StartCoroutine(FadeOutRoutine(fadeDuration));
    }

    private void ResetState()
    {
        _cg.alpha = 0f;
        _rect.anchoredPosition = _originPos;
    }

    private IEnumerator LoopRoutine()
    {
        if (startDelay > 0) yield return new WaitForSeconds(startDelay);

        while (true)
        {
            float timer = 0f;
            _rect.anchoredPosition = _originPos;
            _cg.alpha = 0f;

            while (timer < duration)
            {
                timer += Time.deltaTime;
                float t = timer / duration;

                float moveT = Mathf.Sin(t * Mathf.PI * 0.5f); 
                float currentX = Mathf.Lerp(0, moveX, moveT);
                _rect.anchoredPosition = _originPos + new Vector2(currentX, 0);

                float alpha = 1f;
                if (t < 0.2f) alpha = t / 0.2f;
                else if (t > 0.7f) alpha = 1f - ((t - 0.7f) / 0.3f);
                
                _cg.alpha = alpha;
                yield return null;
            }
            yield return null;
        }
    }

    private IEnumerator FadeOutRoutine(float duration)
    {
        float startAlpha = _cg.alpha;
        float timer = 0f;

        while (timer < duration)
        {
            timer += Time.deltaTime;
            _cg.alpha = Mathf.Lerp(startAlpha, 0f, timer / duration);
            yield return null;
        }

        _cg.alpha = 0f;
        gameObject.SetActive(false);
    }
}