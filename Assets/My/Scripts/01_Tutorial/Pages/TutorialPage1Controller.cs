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
                    // 방 상태가 명시적으로 비어있음(Empty)으로 확인된 경우 1초 대기 후 즉시 타이틀로 복귀시킴
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
                    
                    // 방 상태는 Using 이지만, 실제로 할당된 유저 정보가 없는 상태가 15초간 지속되면 타이틀로 복귀함
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