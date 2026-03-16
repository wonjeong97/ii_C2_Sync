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
    /// <summary>
    /// 타이틀 화면 진입 대기 및 씬 전환을 전담하는 매니저입니다.
    /// 서버 방 상태를 주기적으로 확인(Polling)하여 유저 입장이 감지되면 튜토리얼 씬으로 자동 전환합니다.
    /// </summary>
    public class TitleManager : MonoBehaviour
    {
        [Header("Polling Settings")]
        [SerializeField] private float pollInterval = 3.0f; 

        private bool _isTransitioning; 
        
        private Coroutine _soundCoroutine;
        private Coroutine _pollCoroutine;

        /// <summary> 
        /// 씬 진입 시 초기 상태를 구성하고 API 폴링을 시작합니다. 
        /// </summary>
        private void Start()
        {
            // 타이틀로 돌아왔을 때 누적된 하드웨어 통신 오류를 털어내고 아두이노를 새롭게 재부팅시켜 통신 안정성을 확보
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

        /// <summary> 
        /// 방 상태를 지속적으로 조회하여 새 유저의 진입 여부를 감지합니다.
        /// </summary>
        private IEnumerator PollRoomStateRoutine()
        {
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

        /// <summary> 
        /// 관리자 테스트 및 비상용 수동 전환 단축키 입력 대기 
        /// </summary>
        private void Update()
        {
            if (_isTransitioning) return; 

            if (Input.GetKeyDown(KeyCode.Return))
            {
                GoToTutorial();
            }
        }

        /// <summary> 
        /// 튜토리얼 씬으로 전환하며 중복 호출을 차단합니다. 
        /// </summary>
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

        /// <summary> 
        /// 오브젝트 파괴 시 백그라운드 코루틴을 정리하여 메모리 누수를 방지합니다. 
        /// </summary>
        private void OnDestroy()
        {   
            StopAllCoroutines();
            _soundCoroutine = null;
            _pollCoroutine = null;
        }
    }
}