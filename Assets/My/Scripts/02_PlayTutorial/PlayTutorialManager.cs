using System;
using System.Collections;
using My.Scripts.Global;
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
    [SerializeField] private GaugeController p1Gauge; 
    [SerializeField] private GaugeController p2Gauge; 
    [SerializeField] private float maxDistance = 150f;

    [Header("Floor Settings")]
    [SerializeField] private TextureAdjuster p1Floor; 
    [SerializeField] private TextureAdjuster p2Floor; 
    
    // ★ [추가됨] 프레임 매니저 연결 변수
    [Header("Frame Settings")]
    [SerializeField] private FrameScrollManager p1Frames; 
    [SerializeField] private FrameScrollManager p2Frames;

    [Space(10)]
    [Header("Scroll Physics")]
    [SerializeField] private float runSpeedBoost = 0.011f;    
    [SerializeField] private float maxScrollSpeed = 0.1f;  
    [SerializeField] private float speedDecay = 2.0f;       
    [SerializeField] private float stopThreshold = 0.001f;    

    private PlayTutorialData _data;
    private const string JsonPath = GameConstants.Path.PlayTutorial;

    private bool _gameStarted = false;     
    private bool _isWaitingForRun = false; 

    private float _p1Distance = 0f;
    private float _p2Distance = 0f;

    private float _p1CurrentSpeed = 0f;
    private float _p2CurrentSpeed = 0f;
    
    private bool _p1_LeftStep, _p1_RightStep;
    private bool _p2_LeftStep, _p2_RightStep;
    
    private void Awake()
    {
        if (Instance == null) Instance = this;
        else if (Instance != this) Destroy(gameObject);
    }

    private void Start()
    {
        _data = JsonLoader.Load<PlayTutorialData>(JsonPath);
        
        if (p1Gauge) p1Gauge.UpdateGauge(0, maxDistance);
        if (p2Gauge) p2Gauge.UpdateGauge(0, maxDistance);

        InitFloor(p1Floor);
        InitFloor(p2Floor);

        if (InputManager.Instance != null) InputManager.Instance.OnPadDown += HandlePadDown;
        
        StartCoroutine(IntroScenario());
    }

    private void OnDestroy()
    {
        if (InputManager.Instance != null) InputManager.Instance.OnPadDown -= HandlePadDown;
    }

    private void Update()
    {
        if (!_gameStarted) return;

        // === P1 업데이트 ===
        UpdateFloorSpeed(ref _p1CurrentSpeed, p1Floor);
        // ★ [추가됨] 프레임 이동 호출
        if (p1Frames != null) p1Frames.ScrollFrames(_p1CurrentSpeed);

        // === P2 업데이트 ===
        UpdateFloorSpeed(ref _p2CurrentSpeed, p2Floor);
        // ★ [추가됨] 프레임 이동 호출
        if (p2Frames != null) p2Frames.ScrollFrames(_p2CurrentSpeed);
    }

    private void InitFloor(TextureAdjuster floor)
    {
        if (floor != null)
        {
            floor.enableScroll = true; 
            floor.scrollSpeedY = 0f;   
        }
    }

    private void UpdateFloorSpeed(ref float currentSpeed, TextureAdjuster floor)
    {
        // 1. Lerp 감속
        currentSpeed = Mathf.Lerp(currentSpeed, 0f, speedDecay * Time.deltaTime);

        // 2. Cutoff (급정지)
        if (currentSpeed < stopThreshold)
        {
            currentSpeed = 0f;
        }

        // 3. 바닥 적용 (양수)
        if (floor != null)
        {
            floor.scrollSpeedY = currentSpeed; 
        }
    }

    private void HandlePadDown(int playerIdx, int laneIdx, int padIdx)
    {
        if (!_gameStarted || laneIdx != 1) return;

        // P1
        if (playerIdx == 0)
        {
            if (padIdx == 0) _p1_LeftStep = true;
            else if (padIdx == 1) _p1_RightStep = true;

            if (_p1_LeftStep && _p1_RightStep)
            {
                _p1Distance += 1f; 
                if (p1Gauge) p1Gauge.UpdateGauge(Mathf.Min(_p1Distance, maxDistance), maxDistance);

                BoostSpeed(ref _p1CurrentSpeed);
                
                _p1_LeftStep = false;
                _p1_RightStep = false;
                CheckPopupClose();
            }
        }
        // P2
        else if (playerIdx == 1)
        {
            if (padIdx == 0) _p2_LeftStep = true;
            else if (padIdx == 1) _p2_RightStep = true;

            if (_p2_LeftStep && _p2_RightStep)
            {
                _p2Distance += 1f; 
                if (p2Gauge) p2Gauge.UpdateGauge(Mathf.Min(_p2Distance, maxDistance), maxDistance);

                BoostSpeed(ref _p2CurrentSpeed);

                _p2_LeftStep = false;
                _p2_RightStep = false;
                CheckPopupClose();
            }
        }
    }

    private void BoostSpeed(ref float currentSpeed)
    {
        currentSpeed += runSpeedBoost;
        if (currentSpeed > maxScrollSpeed) currentSpeed = maxScrollSpeed;
    }

    // --- 시나리오 (기존 동일) ---
    private IEnumerator IntroScenario()
    {   
        ShowPopup(0);
        yield return CoroutineData.GetWaitForSeconds(3.0f);
        yield return StartCoroutine(FadeTextAlpha(1f, 0f, 1.0f)); 
        if (_data != null && _data.guideTexts != null && _data.guideTexts.Length > 1) 
            if (popupText != null) popupText.text = _data.guideTexts[1].text;
        yield return StartCoroutine(FadeTextAlpha(0f, 1f, 1.0f)); 
        ResetRunFlags();
        _isWaitingForRun = true; 
        _gameStarted = true;     
    }
    
    private void CheckPopupClose() { if (_isWaitingForRun) { _isWaitingForRun = false; StartCoroutine(FadeOutPopupRoutine(1.0f)); } }
    private void ResetRunFlags() { _p1_LeftStep = _p1_RightStep = false; _p2_LeftStep = _p2_RightStep = false; }
    private IEnumerator FadeOutPopupRoutine(float d) { if (!popup) yield break; float t=0, s=popup.alpha; while(t<d){ t+=Time.deltaTime; popup.alpha=Mathf.Lerp(s,0,t/d); yield return null; } popup.alpha=0; ClosePopup(); }
    private IEnumerator FadeTextAlpha(float s, float e, float d) { if (!popupText) yield break; float t=0; Color c=popupText.color; while(t<d){ t+=Time.deltaTime; float a=Mathf.Lerp(s,e,t/d); popupText.color=new Color(c.r,c.g,c.b,a); yield return null; } popupText.color=new Color(c.r,c.g,c.b,e); }
    public void ShowPopup(int i) { if(_data==null||_data.guideTexts==null||i>=_data.guideTexts.Length)return; if(popupText){popupText.text=_data.guideTexts[i].text;Color c=popupText.color;popupText.color=new Color(c.r,c.g,c.b,1);} if(popup){popup.alpha=1;popup.blocksRaycasts=true;} }
    public void ClosePopup() { if(popup){popup.alpha=0;popup.blocksRaycasts=false;} }
}