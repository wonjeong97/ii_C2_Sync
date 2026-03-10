using System;
using System.Collections;
using My.Scripts.Core;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using Wonjeong.Data;
using Wonjeong.UI;
using Wonjeong.Utils;

namespace My.Scripts._01_Tutorial.Pages
{
    [Serializable]
    public class TutorialPage1Data
    {
        public TextSetting descriptionText;
    }

    /// <summary>
    /// 튜토리얼 첫 번째 페이지 컨트롤러.
    /// 방 상태 유지(USING) 및 입장한 유저의 UID를 1초마다 폴링하며, 
    /// 15초 동안 UID가 들어오지 않으면 노쇼로 간주하여 초기화 후 타이틀로 복귀함.
    /// </summary>
    public class TutorialPage1Controller : GamePage<TutorialPage1Data>
    {
        [Header("Page 1 UI")]
        [SerializeField] private Text descriptionText;

        [Header("Polling Settings")]
        [SerializeField] private float basePollInterval = 1.0f; 
        [SerializeField] private float maxPollInterval = 10.0f; 

        private float _currentPollInterval; 
        private readonly float fadeTime = 1f;
        private Coroutine _pollCoroutine; 
        private bool _isCompleted;

        protected override void Awake()
        {
            base.Awake();
            
            // 연출 시작 전 텍스트가 깜빡이는 것을 막기 위해 투명도를 0으로 초기화
            if (descriptionText)
            {
                Color c = descriptionText.color;
                c.a = 0f;
                descriptionText.color = c;
            }
        }

        protected override void SetupData(TutorialPage1Data data)
        {
            if (data == null) return;
            
            if (descriptionText && data.descriptionText != null)
            {
                if (UIManager.Instance)
                {
                    UIManager.Instance.SetText(descriptionText.gameObject, data.descriptionText);
                }
            }
        }

        public override void OnEnter()
        {
            base.OnEnter(); 
            _isCompleted = false;

            // 방치 타이머를 비활성화하고, 태그 대기 상태임을 알리기 위해 전용 텍스트 타입 설정
            if (GameManager.Instance)
            {
                GameManager.Instance.IsAutoProgressing = false;
                GameManager.Instance.CurrentInactivityTextType = InactivityTextType.Tag;
            }

            if (descriptionText) StartCoroutine(FadeInTextRoutine());
            
            if (_pollCoroutine != null) StopCoroutine(_pollCoroutine);
            _pollCoroutine = StartCoroutine(PollRoomStateRoutine());
        }

        public override void OnExit()
        {
            if (_pollCoroutine != null)
            {
                StopCoroutine(_pollCoroutine);
                _pollCoroutine = null;
            }
            
            if (GameManager.Instance)
            {
                GameManager.Instance.CurrentInactivityTextType = InactivityTextType.Warning;
            }
            
            base.OnExit();
        }

        private void Update()
        {
#if UNITY_EDITOR
            // 에디터 테스트 시, 통신을 기다리지 않고 엔터키나 클릭으로 강제 진행하기 위한 가드 로직
            if (!_isCompleted && (Input.GetKeyDown(KeyCode.Return) || Input.GetMouseButtonDown(0)))
            {
                ProceedToNext();
            }
#endif
        }

        /// <summary>
        /// 방 상태와 유저 UID를 순차적으로 조회하는 메인 통신 루틴.
        /// 통신 실패 시 지수 백오프를 적용하며, 15초 이상 유저 정보가 없을 경우 방을 해제(노쇼 처리)함.
        /// </summary>
        private IEnumerator PollRoomStateRoutine()
        {
            float emptyUserStartTime = -1f; 
            _currentPollInterval = basePollInterval;

            while (!_isCompleted)
            {
                if (!GameManager.Instance || GameManager.Instance.ApiConfig == null)
                {
                    yield return CoroutineData.GetWaitForSeconds(_currentPollInterval);
                    continue;
                }

                // C2 콘텐츠 파라미터 적용
                string checkUrl = $"{GameManager.Instance.ApiConfig.CheckRoomStateUrl}?code=c2";
                string userUrl = $"{GameManager.Instance.ApiConfig.GetCurrentRoomUserUrl}?code=c2";

                bool isRoomEmpty = false;
                bool isNetworkError = false;

                // 1. 방 상태 확인 (EMPTY / USING)
                using (UnityWebRequest stateReq = UnityWebRequest.Get(checkUrl))
                {
                    stateReq.timeout = 10; 
                    yield return stateReq.SendWebRequest();

                    if (stateReq.result == UnityWebRequest.Result.Success)
                    {
                        if (stateReq.downloadHandler.text.IndexOf("EMPTY", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            isRoomEmpty = true;
                        }
                    }
                    else 
                    {
                        isNetworkError = true;
                    }
                }

                // 방이 USING 상태이고 네트워크 오류가 없을 때만 유저 정보 확인 진행
                if (!isNetworkError && !isRoomEmpty)
                {
                    bool isUserEmpty = false;
                    
                    // 2. 유저 UID 할당 여부 확인
                    using (UnityWebRequest userReq = UnityWebRequest.Get(userUrl))
                    {
                        userReq.timeout = 10; 
                        yield return userReq.SendWebRequest();

                        if (userReq.result == UnityWebRequest.Result.Success)
                        {
                            string rawText = userReq.downloadHandler.text.Trim();
                            
                            if (rawText.IndexOf("EMPTY", StringComparison.OrdinalIgnoreCase) >= 0)
                            {
                                isUserEmpty = true;
                            }
                            else if (!string.IsNullOrEmpty(rawText))
                            {
                                // 통신 성공 및 UID 확보 시 폴링 주기 원상 복구
                                _currentPollInterval = basePollInterval;
                                emptyUserStartTime = -1f;

                                string uidLeft = rawText;
                                
                                // 다중 유저 입장 시 (쉼표 구분) 첫 번째 유저의 UID를 추출함
                                if (rawText.Contains(","))
                                {
                                    string[] parts = rawText.Split(new[] { '\r', '\n', ',' }, StringSplitOptions.RemoveEmptyEntries);
                                    if (parts.Length > 0)
                                    {
                                        uidLeft = parts[0].Trim();
                                    }
                                }

                                if (APIManager.Instance)
                                {   
                                    bool isApiDone = false;
                                    bool fetchSuccess = false;

                                    APIManager.Instance.FetchData(uidLeft, (success) => {
                                        isApiDone = true;
                                        fetchSuccess = success;
                                    });

                                    // API 파싱이 완료될 때까지 대기 (최대 25초 타임아웃)
                                    float fetchTimer = 0f;
                                    while (!isApiDone && fetchTimer < 25f)
                                    {
                                        fetchTimer += Time.deltaTime;
                                        yield return null;
                                    }

                                    // 타임아웃 발생이거나 데이터 조회 실패 시 다음 루프에서 재시도
                                    if (!isApiDone || !fetchSuccess || GameManager.Instance.CurrentUserIdx == 0)
                                    {
                                        Debug.LogWarning("[TutorialPage1] 유저 데이터 로드 실패 또는 타임아웃 발생.");
                                        yield return CoroutineData.GetWaitForSeconds(_currentPollInterval);
                                        continue;
                                    }
                                }
                                
                                ProceedToNext(); 
                                yield break; 
                            }
                        }
                        else 
                        {
                            isNetworkError = true;
                        }
                    }

                    // 빈 방(EMPTY)이 15초 동안 유지될 경우 노쇼(No-Show)로 판정
                    if (isUserEmpty)
                    {
                        if (emptyUserStartTime < 0f) emptyUserStartTime = Time.time;
                        if (Time.time - emptyUserStartTime >= 15f) isRoomEmpty = true;
                    }
                }

                // 네트워크 에러 시 지수 백오프 적용 (서버 과부하 방지)
                if (isNetworkError)
                {
                    _currentPollInterval = Mathf.Min(_currentPollInterval * 2f, maxPollInterval);
                }

                // 방 상태가 EMPTY가 되거나 15초 노쇼가 확정된 경우 타이틀로 회귀
                if (isRoomEmpty)
                {
                    Debug.Log("[TutorialPage1] 방이 비어있거나 15초 타임아웃(노쇼) 발생. 타이틀로 돌아갑니다.");
                    if (GameManager.Instance)
                    {
                        GameManager.Instance.SendResetStartAPI();
                        GameManager.Instance.SendExitRoomAPI();
                        GameManager.Instance.ReturnToTitle();
                    }
                    yield break;
                }

                yield return CoroutineData.GetWaitForSeconds(_currentPollInterval);
            }
        }

        private IEnumerator FadeInTextRoutine()
        {
            float timer = 0f;
            while (timer < fadeTime)
            {
                timer += Time.deltaTime;
                if (descriptionText)
                {
                    Color c = descriptionText.color;
                    c.a = Mathf.Clamp01(timer / fadeTime);
                    descriptionText.color = c;
                }
                yield return null;
            }
            
            if (descriptionText)
            {
                Color c = descriptionText.color;
                c.a = 1f;
                descriptionText.color = c;
            }
        }

        private void ProceedToNext()
        {
            if (_isCompleted) return;
            _isCompleted = true;

            if (SoundManager.Instance)
            {
                SoundManager.Instance.StopBGM();
                SoundManager.Instance.PlayBGM("MainBGM");
            }
            
            CompleteStep();
        }
    }
}