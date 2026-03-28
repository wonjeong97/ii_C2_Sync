using System;
using System.Collections;
using Cysharp.Threading.Tasks; 
using My.Scripts.Core;
using My.Scripts.Global;
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
    
    public class TutorialPage1Controller : GamePage<TutorialPage1Data>
    {
        [Header("Page 1 UI")]
        [SerializeField] private Text descriptionText;
        
        [Header("API Manager")]
        [SerializeField] private APIManager apiManager;
        
        [Header("Polling Settings")]
        [SerializeField] private float pollInterval = 3.0f; 

        private readonly float fadeTime = 0.5f; 
        private Coroutine _pollCoroutine; 

        /// <summary>
        /// 객체 초기화 시 호출됨.
        /// 텍스트 알파값을 0으로 설정하여 초기 투명 상태를 만듦.
        /// </summary>
        protected override void Awake()
        {
            base.Awake();
            if (descriptionText)
            {
                Color c = descriptionText.color;
                c.a = 0f;
                descriptionText.color = c;
            }
            else
            {
                Debug.LogWarning("[TutorialPage1] descriptionText 컴포넌트가 누락됨.");
            }
        }

        /// <summary>
        /// 외부 데이터를 받아 UI 텍스트를 세팅함.
        /// </summary>
        /// <param name="data">적용할 텍스트 설정 데이터</param>
        protected override void SetupData(TutorialPage1Data data)
        {
            if (descriptionText) 
            {
                if (UIManager.Instance)
                {
                    UIManager.Instance.SetText(descriptionText.gameObject, data.descriptionText);
                }
                else
                {
                    Debug.LogWarning("[TutorialPage1] UIManager 인스턴스가 없음.");
                }
            }
            else
            {
                Debug.LogWarning("[TutorialPage1] descriptionText 컴포넌트가 누락됨.");
            }
        }

        /// <summary>
        /// 페이지 진입 시 호출됨.
        /// 텍스트 페이드인 연출을 시작함.
        /// </summary>
        public override void OnEnter()
        {
            base.OnEnter(); 

            // 튜토리얼 매칭 대기 화면이므로 유저가 조작하지 않아도 방치 타이머가 동작하지 않도록 강제함.
            if (GameManager.Instance) GameManager.Instance.IsAutoProgressing = true;

            if (descriptionText) 
            {
                StartCoroutine(FadeInTextRoutine());
            }
        }

        /// <summary>
        /// 페이지 퇴장 시 호출됨.
        /// 실행 중인 코루틴을 정리하고 메인 BGM으로 전환함.
        /// </summary>
        public override void OnExit()
        {
            if (SoundManager.Instance)
            {
                // 대기 화면 종료 후 본격적인 게임 진입이므로 BGM을 교체함.
                SoundManager.Instance.StopBGM();
                SoundManager.Instance.PlayBGM("MainBGM");
            }

            if (_pollCoroutine != null)
            {
                StopCoroutine(_pollCoroutine);
                _pollCoroutine = null;
            }
            base.OnExit();
        }

        /// <summary>
        /// 매 프레임 호출됨.
        /// 단축키 입력을 통한 스킵 처리.
        /// </summary>
        private void Update()
        {
            // 개발 및 디버그 환경에서 빠른 테스트를 위해 엔터키로 강제 진행을 허용함.
            if (Input.GetKeyDown(KeyCode.Return)) CompleteStep(); 
        }

        /// <summary>
        /// 주기적으로 서버에 룸 상태 및 유저 할당 여부를 확인하여 튜토리얼을 진행할지 판단함.
        /// </summary>
        /// <returns>IEnumerator 루틴</returns>
        private IEnumerator PollRoomStateRoutine()
        {
#if UNITY_EDITOR
            // 이유: 에디터에서는 실제 유저의 매칭을 기다리는 API 폴링을 생략하고, 즉시 가상 데이터 패치를 호출하여 진행함.
            Debug.Log("[TutorialPage1] 에디터 모드: 유저 할당 대기를 생략합니다.");
            yield return CoroutineData.GetWaitForSeconds(1.0f);
            
            if (apiManager)
            {
                bool fetchSuccess = false;
                bool fetchFaulted = false;
                yield return apiManager.FetchDataAsync("EDITOR_TEST").ToCoroutine(r => fetchSuccess = r, ex => { fetchFaulted = true; });
            }
            else
            {
                Debug.LogWarning("[TutorialPage1] apiManager 컴포넌트가 누락됨.");
            }
            
            CompleteStep();
            yield break;
#endif
            float emptyUserStartTime = -1f; 

            // # TODO: 루프 내부에서의 반복적인 문자열 생성으로 인한 가비지 발생을 막기 위해 URL 캐싱 최적화 필요.
            while (true)
            {
                if (!GameManager.Instance || GameManager.Instance.ApiConfig == null)
                {
                    yield return CoroutineData.GetWaitForSeconds(pollInterval);
                    continue;
                }

                string checkUrl = $"{GameManager.Instance.ApiConfig.CheckRoomStateUrl}?code={GameConstants.Module.Code.ToLower()}";
                string userUrl = $"{GameManager.Instance.ApiConfig.GetCurrentRoomUserUrl}?code={GameConstants.Module.Code.ToLower()}";

                bool isRoomEmpty = false;

                using (UnityWebRequest stateReq = UnityWebRequest.Get(checkUrl))
                {
                    stateReq.timeout = 10; 
                    yield return stateReq.SendWebRequest();

                    if (stateReq.result == UnityWebRequest.Result.Success)
                    {
                        if (stateReq.downloadHandler.text.IndexOf(GameConstants.Api.StatusEmpty, StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            isRoomEmpty = true;
                        }
                    }
                }

                if (isRoomEmpty)
                {
                    // 이유: 방이 비어있으면 튜토리얼을 중단하고 즉시 타이틀 화면으로 복귀함.
                    yield return CoroutineData.GetWaitForSeconds(1.0f);
                    if (GameManager.Instance) GameManager.Instance.ReturnToTitle();
                    yield break;
                }

                bool isUserEmpty = false;

                using (UnityWebRequest userReq = UnityWebRequest.Get(userUrl))
                {
                    userReq.timeout = 10; 
                    yield return userReq.SendWebRequest();

                    if (userReq.result == UnityWebRequest.Result.Success)
                    {
                        string rawText = userReq.downloadHandler.text;
                        if (rawText.IndexOf(GameConstants.Api.StatusEmpty, StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            isUserEmpty = true;
                        }
                        else if (rawText.Contains(","))
                        {
                            emptyUserStartTime = -1f;

                            string[] parts = rawText.Split(new[] { '\r', '\n', ',' }, StringSplitOptions.RemoveEmptyEntries);
                            if (parts.Length >= 1)
                            {
                                string uidLeft = parts[0].Trim();
                                if (parts.Length >= 2 && SessionManager.Instance)
                                {
                                    SessionManager.Instance.PlayerAUid = uidLeft;
                                    SessionManager.Instance.PlayerBUid = parts[1].Trim();
                                }

                                if (apiManager)
                                {   
                                    bool fetchSuccess = false;
                                    bool fetchFaulted = false;

                                    yield return apiManager.FetchDataAsync(uidLeft)
                                                           .Timeout(TimeSpan.FromSeconds(25))
                                                           .ToCoroutine(
                                                                r => fetchSuccess = r, 
                                                                ex => { fetchFaulted = true; }
                                                            );

                                    // 이유: 데이터 패치에 실패하거나 유저 인덱스가 유효하지 않을 경우 재시도함.
                                    if (fetchFaulted || !fetchSuccess || !SessionManager.Instance || SessionManager.Instance.CurrentUserId == 0)
                                    {
                                        yield return CoroutineData.GetWaitForSeconds(pollInterval);
                                        continue;
                                    }
                                }
                                else
                                {
                                    Debug.LogWarning("[TutorialPage1] apiManager 컴포넌트가 누락됨.");
                                }
                                CompleteStep(); 
                                yield break; 
                            }
                        }
                    }
                }

                if (isUserEmpty)
                {
                    // 이유: 방은 할당되었으나 유저 데이터가 계속 들어오지 않을 경우 15초 타임아웃 처리.
                    if (emptyUserStartTime < 0f) emptyUserStartTime = Time.time;
                    
                    if (Time.time - emptyUserStartTime >= 15f)
                    {
                        if (GameManager.Instance) GameManager.Instance.ReturnToTitle();
                        yield break;
                    }
                }
                else
                {
                    emptyUserStartTime = -1f;
                }

                yield return CoroutineData.GetWaitForSeconds(pollInterval);
            }
        }

        /// <summary>
        /// 설정된 시간 동안 텍스트의 투명도를 조절하여 페이드인 효과를 줌.
        /// </summary>
        /// <returns>IEnumerator 루틴</returns>
        private IEnumerator FadeInTextRoutine()
        {
            float timer = 0f;
            while (timer < fadeTime)
            {
                timer += Time.deltaTime;
                if (descriptionText)
                {
                    Color c = descriptionText.color;
                    // 예시 입력: timer = 0.25, fadeTime = 0.5 -> 결과값 = 0.5 (알파값 50%)
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
            
            if (_pollCoroutine != null) StopCoroutine(_pollCoroutine);
            _pollCoroutine = StartCoroutine(PollRoomStateRoutine());
        }
    }
}