using System;
using System.Collections;
using My.Scripts.Core;
using My.Scripts.Hardware;
using UnityEngine;
using UnityEngine.Networking; 
using UnityEngine.SceneManagement;
using Wonjeong.UI;
using Wonjeong.Utils;

namespace My.Scripts._00_Title
{
    public class TitleManager : MonoBehaviour
    {
        [Header("Polling Settings")]
        [SerializeField] private float pollInterval = 3.0f; 

        private bool _isTransitioning; 
        
        private Coroutine _soundCoroutine;
        private Coroutine _pollCoroutine;

        private void Start()
        {
            if (ArduinoManager.Instance)
            {
                ArduinoManager.Instance.Reconnect();
            }

            if (_soundCoroutine == null)
            {
                _soundCoroutine = StartCoroutine(StartMainBGM());
            }

            _pollCoroutine = StartCoroutine(PollRoomStateRoutine());
        }

        private IEnumerator PollRoomStateRoutine()
        {
#if UNITY_EDITOR
            // 이유: 에디터 모드에서는 방 상태를 기다리지 않고 바로 튜토리얼 씬으로 진입시켜 빠른 테스트를 돕기 위함.
            Debug.Log("[TitleManager] 에디터 모드: API 폴링을 생략하고 튜토리얼로 이동합니다.");
            yield return CoroutineData.GetWaitForSeconds(1.0f);
            GoToTutorial();
            yield break;
#endif
            while (!_isTransitioning)
            {
                if (!GameManager.Instance || GameManager.Instance.ApiConfig == null)
                {
                    yield return CoroutineData.GetWaitForSeconds(pollInterval);
                    continue;
                }

                string requestUrl = $"{GameManager.Instance.ApiConfig.CheckRoomStateUrl}?code=c2";

                using (UnityWebRequest webRequest = UnityWebRequest.Get(requestUrl))
                {
                    webRequest.timeout = 10;
                    
                    yield return webRequest.SendWebRequest();
                    
                    if (webRequest.result == UnityWebRequest.Result.Success)
                    {
                        string responseText = webRequest.downloadHandler.text;
                        
                        if (!string.IsNullOrEmpty(responseText) && responseText.IndexOf("USING", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            Debug.Log($"[TitleManager] RoomState 'USING' 감지. 튜토리얼로 이동.");
                            GoToTutorial();
                            yield break;
                        }
                    }
                    else
                    {
                        Debug.LogWarning($"[TitleManager] 상태 체크 통신 실패: {webRequest.error}. 3초 후 재시도합니다.");
                    }
                }

                yield return CoroutineData.GetWaitForSeconds(pollInterval);
            }
        }

        private void Update()
        {
            if (_isTransitioning) return; 

            if (Input.GetKeyDown(KeyCode.Return))
            {
                GoToTutorial();
            }
        }

        private void GoToTutorial()
        {
            if (_isTransitioning) return;
            _isTransitioning = true; 

            SceneManager.LoadScene(GameConstants.Scene.Tutorial);
        }
        
        private IEnumerator StartMainBGM()
        {
            if (!SoundManager.Instance) yield break;

            SoundManager.Instance.StopBGM();
            yield return CoroutineData.GetWaitForSeconds(1.0f);
            SoundManager.Instance.PlayBGM("MainBGM");
        }

        private void OnDestroy()
        {   
            StopAllCoroutines();
            _soundCoroutine = null;
            _pollCoroutine = null;
        }
    }
}