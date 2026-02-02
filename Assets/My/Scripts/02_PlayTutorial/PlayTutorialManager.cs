using System;
using System.Collections;
using My.Scripts.Global; // InputManager 사용
using UnityEngine;
using UnityEngine.UI;
using Wonjeong.Data;
using Wonjeong.Utils;

[Serializable]
public class PlayTutorialData
{
    public TextSetting[] guideTexts;
}

public class PlayTutorialManager : MonoBehaviour
{
    public static PlayTutorialManager Instance;

    [Header("Popup UI")] 
    [SerializeField] private CanvasGroup popup; 
    [SerializeField] private Text popupText;    

    [Header("Gauge UI")]
    [SerializeField] private GaugeController p1Gauge; // P1 게이지 연결
    [SerializeField] private GaugeController p2Gauge; // P2 게이지 연결
    [SerializeField] private float maxDistance = 150f; // 목표 거리 (150m)

    [Header("Floor Settings")]
    [SerializeField] private TextureAdjuster p1Floor; // P1 바닥 연결
    [SerializeField] private TextureAdjuster p2Floor; // P2 바닥 연결
    [SerializeField] private float runSpeedBoost = 2.0f;   // 발 구를 때 추가될 속도
    [SerializeField] private float speedDecay = 3.0f;      // 초당 줄어드는 속도 (마찰력)
    [SerializeField] private float maxScrollSpeed = 10.0f; // 최대 스크롤 속도 제한

    private PlayTutorialData _data;
    private const string JsonPath = GameConstants.Path.PlayTutorial;

    // === 상태 변수 ===
    private bool _isWaitingForRun = false; // 팝업 닫기용 첫 달리기 감지 대기
    private bool _gameStarted = false;     // 게임 시작 여부 (게이지 증가 허용)

    // 거리 변수
    private float _p1Distance = 0f;
    private float _p2Distance = 0f;

    // 속도 변수 (바닥 스크롤용)
    private float _p1CurrentSpeed = 0f;
    private float _p2CurrentSpeed = 0f;
    
    // 발판 입력 체크 (왼발/오른발)
    private bool _p1_LeftStep, _p1_RightStep;
    private bool _p2_LeftStep, _p2_RightStep;
    
    private void Awake()
    {
        if (Instance == null) Instance = this;
        else if (Instance != this) Destroy(gameObject);
    }

    private void Start()
    {
        // 1. 데이터 로드
        _data = JsonLoader.Load<PlayTutorialData>(JsonPath);
        if (_data == null) Debug.LogWarning($"[PlayTutorialManager] '{JsonPath}' 로드 실패");
        // 2. 게이지 초기화
        if (p1Gauge) p1Gauge.UpdateGauge(0, maxDistance);
        if (p2Gauge) p2Gauge.UpdateGauge(0, maxDistance);
        // 3. 바닥 초기화
        InitFloor(p1Floor);
        InitFloor(p2Floor);
        // 4. 입력 이벤트 연결
        if (InputManager.Instance != null)
        {
            InputManager.Instance.OnPadDown += HandlePadDown;
        }
        else
        {
            Debug.LogError("[PlayTutorialManager] InputManager가 없습니다.");
        }
        
        // 5. 인트로 시나리오 시작
        StartCoroutine(IntroScenario());
    }

    private void OnDestroy()
    {
        if (InputManager.Instance != null)
        {
            InputManager.Instance.OnPadDown -= HandlePadDown;
        }
    }

    private void Update()
    {
        // 게임 시작 전에는 스크롤 로직 돌리지 않음
        if (!_gameStarted) return;

        // 속도 감쇠 및 바닥 스크롤 적용
        UpdateFloorSpeed(ref _p1CurrentSpeed, p1Floor);
        UpdateFloorSpeed(ref _p2CurrentSpeed, p2Floor);
    }

    // --- 바닥 제어 로직 ---

    private void InitFloor(TextureAdjuster floor)
    {
        if (floor != null)
        {
            floor.enableScroll = true; // 스크롤 활성화
            floor.scrollSpeedY = 0f;   // 정지 상태로 시작
        }
    }

    private void UpdateFloorSpeed(ref float currentSpeed, TextureAdjuster floor)
    {
        // 자연스러운 감속 (0 이하로는 내려가지 않음)
        if (currentSpeed > 0)
        {
            currentSpeed -= speedDecay * Time.deltaTime;
            if (currentSpeed < 0) currentSpeed = 0;
        }

        // 바닥 컴포넌트에 속도 반영
        if (floor != null)
        {
            floor.scrollSpeedY = currentSpeed;
        }
    }

    // --- 시나리오 로직 ---

    private IEnumerator IntroScenario()
    {   
        // [Step 1] 0번 텍스트 (3초 대기)
        ShowPopup(0);
        yield return CoroutineData.GetWaitForSeconds(3.0f);

        // [Step 2] 텍스트 교체 (0 -> 1)
        yield return StartCoroutine(FadeTextAlpha(1f, 0f, 1.0f)); // 페이드 아웃
        
        if (_data != null && _data.guideTexts != null && _data.guideTexts.Length > 1)
        {
            if (popupText != null) popupText.text = _data.guideTexts[1].text;
        }

        yield return StartCoroutine(FadeTextAlpha(0f, 1f, 1.0f)); // 페이드 인

        // [Step 3] 게임 시작 및 입력 대기
        Debug.Log("[PlayTutorial] 달리기 입력 대기 시작...");
        ResetRunFlags();
        _isWaitingForRun = true; // 팝업 닫기 조건 활성화
        _gameStarted = true;     // 게이지 증가 활성화
    }

    // --- 입력 감지 및 게이지 처리 로직 ---

    private void HandlePadDown(int playerIdx, int laneIdx, int padIdx)
    {
        // 게임이 시작되지 않았거나, 중앙 라인(1)이 아니면 무시
        if (!_gameStarted || laneIdx != 1) return;

        // === Player 1 ===
        if (playerIdx == 0)
        {
            if (padIdx == 0) _p1_LeftStep = true;
            else if (padIdx == 1) _p1_RightStep = true;

            // 양발 모두 밟았으면 1m 전진
            if (_p1_LeftStep && _p1_RightStep)
            {
                // 거리 증가
                _p1Distance += 1f;
                if (_p1Distance > maxDistance) _p1Distance = maxDistance;

                // UI 갱신 (게이지 + 픽토그램)
                if (p1Gauge) p1Gauge.UpdateGauge(_p1Distance, maxDistance);

                // ★ 바닥 스크롤 속도 증가
                _p1CurrentSpeed += runSpeedBoost;
                if (_p1CurrentSpeed > maxScrollSpeed) _p1CurrentSpeed = maxScrollSpeed;
                
                // 플래그 초기화 (다시 밟아야 함)
                _p1_LeftStep = false;
                _p1_RightStep = false;

                // 팝업 닫기 체크 (첫 달리기 성공 시)
                CheckPopupClose();
            }
        }
        // === Player 2 ===
        else if (playerIdx == 1)
        {
            if (padIdx == 0) _p2_LeftStep = true;
            else if (padIdx == 1) _p2_RightStep = true;

            if (_p2_LeftStep && _p2_RightStep)
            {
                _p2Distance += 1f;
                if (_p2Distance > maxDistance) _p2Distance = maxDistance;

                if (p2Gauge) p2Gauge.UpdateGauge(_p2Distance, maxDistance);

                // ★ 바닥 스크롤 속도 증가
                _p2CurrentSpeed += runSpeedBoost;
                if (_p2CurrentSpeed > maxScrollSpeed) _p2CurrentSpeed = maxScrollSpeed;

                _p2_LeftStep = false;
                _p2_RightStep = false;

                CheckPopupClose();
            }
        }
    }

    private void CheckPopupClose()
    {
        // 팝업이 떠있고 대기 중이라면 닫기
        if (_isWaitingForRun)
        {
            Debug.Log("[PlayTutorial] 첫 달리기 감지! 팝업 종료.");
            _isWaitingForRun = false; 
            StartCoroutine(FadeOutPopupRoutine(1.0f));
        }
    }

    private void ResetRunFlags()
    {
        _p1_LeftStep = _p1_RightStep = false;
        _p2_LeftStep = _p2_RightStep = false;
    }

    // --- 연출 코루틴 ---

    private IEnumerator FadeOutPopupRoutine(float duration)
    {
        if (popup == null) yield break;

        float elapsed = 0f;
        float startAlpha = popup.alpha;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            popup.alpha = Mathf.Lerp(startAlpha, 0f, elapsed / duration);
            yield return null;
        }

        popup.alpha = 0f;
        ClosePopup();
    }

    private IEnumerator FadeTextAlpha(float start, float end, float duration)
    {
        if (popupText == null) yield break;

        float elapsed = 0f;
        Color initialColor = popupText.color;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float alpha = Mathf.Lerp(start, end, elapsed / duration);
            popupText.color = new Color(initialColor.r, initialColor.g, initialColor.b, alpha);
            yield return null;
        }
        
        popupText.color = new Color(initialColor.r, initialColor.g, initialColor.b, end);
    }

    // --- 기본 기능 ---

    public void ShowPopup(int index)
    {   
        if (_data == null || _data.guideTexts == null || index < 0 || index >= _data.guideTexts.Length) return;
        
        if (popupText != null)
        {
            popupText.text = _data.guideTexts[index].text;
            Color c = popupText.color;
            popupText.color = new Color(c.r, c.g, c.b, 1f); 
        }

        if (popup != null)
        {
            popup.alpha = 1f;
            popup.blocksRaycasts = true;
        }
    }

    public void ClosePopup()
    {
        if (popup != null)
        {
            popup.alpha = 0f;
            popup.blocksRaycasts = false;
        }
    }
}