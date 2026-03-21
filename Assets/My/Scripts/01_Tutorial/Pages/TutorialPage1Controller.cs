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

        protected override void Awake()
        {
            base.Awake();
            if (descriptionText)
            {
                Color c = descriptionText.color;
                c.a = 0f;
                descriptionText.color = c;
            }
        }

        protected override void SetupData(TutorialPage1Data data)
        {
            if (descriptionText) UIManager.Instance.SetText(descriptionText.gameObject, data.descriptionText);
        }

        public override void OnEnter()
        {
            base.OnEnter(); 

            if (GameManager.Instance) GameManager.Instance.IsAutoProgressing = true;

            if (descriptionText) StartCoroutine(FadeInTextRoutine());
        }

        public override void OnExit()
        {
            if (SoundManager.Instance)
            {
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

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Return)) CompleteStep(); 
        }

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
            
            CompleteStep();
            yield break;
#endif
            float emptyUserStartTime = -1f; 

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

                                    if (fetchFaulted || !fetchSuccess || !SessionManager.Instance || SessionManager.Instance.CurrentUserId == 0)
                                    {
                                        yield return CoroutineData.GetWaitForSeconds(pollInterval);
                                        continue;
                                    }
                                }
                                CompleteStep(); 
                                yield break; 
                            }
                        }
                    }
                }

                if (isUserEmpty)
                {
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
            
            if (_pollCoroutine != null) StopCoroutine(_pollCoroutine);
            _pollCoroutine = StartCoroutine(PollRoomStateRoutine());
        }
    }
}