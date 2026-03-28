using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Wonjeong.Utils;

/// <summary>
/// UI 화살표의 이동 및 투명도 루프 애니메이션을 제어하는 컴포넌트.
/// </summary>
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

    /// <summary>
    /// 컴포넌트 초기화 및 초기 위치 저장.
    /// </summary>
    private void Awake()
    {
        _rect = GetComponent<RectTransform>();
        _cg = GetComponent<CanvasGroup>();

        if (!_rect) Debug.LogError("RectTransform 컴포넌트 누락");
        if (!_cg) Debug.LogError("CanvasGroup 컴포넌트 누락");

        _originPos = _rect.anchoredPosition;
    }

    /// <summary>
    /// 오브젝트 활성화 시 상태 초기화.
    /// </summary>
    private void OnEnable() => ResetState();

    /// <summary>
    /// 애니메이션 루틴을 시작함.
    /// </summary>
    public void Play()
    {
        gameObject.SetActive(true);
        if (_animRoutine != null) StopCoroutine(_animRoutine);
        _animRoutine = StartCoroutine(LoopRoutine());
    }

    /// <summary>
    /// 애니메이션을 즉시 중단하고 오브젝트를 비활성화함.
    /// </summary>
    public void Stop()
    {
        if (_animRoutine != null) StopCoroutine(_animRoutine);
        ResetState();
        gameObject.SetActive(false);
    }

    /// <summary>
    /// 실행 중인 애니메이션을 멈추고 지정된 시간 동안 페이드 아웃 후 비활성화함.
    /// </summary>
    /// <param name="fadeDuration">페이드 아웃 소요 시간</param>
    public void FadeOutAndStop(float fadeDuration = 1.0f)
    {
        if (!gameObject.activeInHierarchy) return;
        if (_animRoutine != null) StopCoroutine(_animRoutine);
        StartCoroutine(FadeOutRoutine(fadeDuration));
    }

    /// <summary>
    /// UI 요소를 투명하게 만들고 시작 위치로 복구함.
    /// </summary>
    private void ResetState()
    {
        if (_cg) _cg.alpha = 0f;
        if (_rect) _rect.anchoredPosition = _originPos;
    }

    /// <summary>
    /// 화살표의 이동과 알파값 변화를 무한 반복하는 루틴.
    /// </summary>
    /// <returns>IEnumerator</returns>
    private IEnumerator LoopRoutine()
    {
        if (startDelay > 0) yield return CoroutineData.GetWaitForSeconds(startDelay);

        while (true)
        {
            float timer = 0f;
            _rect.anchoredPosition = _originPos;
            _cg.alpha = 0f;

            while (timer < duration)
            {
                timer += Time.deltaTime;
                float t = timer / duration;

                // 이유: 이동 끝부분에서 자연스럽게 감속되는 연출을 위해 Sin 곡선 사용
                // 예시: t=0.5일 때 Sin(0.25PI) 0.707, t=1.0일 때 Sin(0.5PI) 1.0
                float moveT = Mathf.Sin(t * Mathf.PI * 0.5f); 
                float currentX = Mathf.Lerp(0, moveX, moveT);
                _rect.anchoredPosition = _originPos + new Vector2(currentX, 0);

                // 이유: 등장 시 20퍼센트 구간 페이드인, 퇴장 시 30퍼센트 구간 페이드아웃 적용
                float alpha = 1f;
                if (t < 0.2f) alpha = t / 0.2f;
                else if (t > 0.7f) alpha = 1f - ((t - 0.7f) / 0.3f);
                
                _cg.alpha = alpha;
                yield return null;
            }
            yield return null;
        }
    }

    /// <summary>
    /// 현재 알파값에서 0까지 선형 보간으로 투명화하는 루틴.
    /// </summary>
    /// <param name="duration">페이드 시간</param>
    /// <returns>IEnumerator</returns>
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