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

        /// <summary>
        /// 하드웨어 연결 초기화 및 상태 체크 진입.
        /// </summary>
        private void Start()
        {
            // 통신 불량 대비 하드웨어 재연결 시도.
            if (ArduinoManager.Instance)
            {
                ArduinoManager.Instance.Reconnect();
            }
            else
            {
                Debug.LogWarning("[TitleManager] ArduinoManager가 존재하지 않습니다.");
            }

            // 사운드 중복 재생 방지.
            if (_soundCoroutine == null)
            {
                _soundCoroutine = StartCoroutine(StartMainBGM());
            }

            _pollCoroutine = StartCoroutine(PollRoomStateRoutine());
        }

        /// <summary>
        /// 주기적으로 서버에 룸 상태를 질의하여 진입 시점을 결정함.
        /// </summary>
        private IEnumerator PollRoomStateRoutine()
        {
#if UNITY_EDITOR
            // 에디터 테스트 속도 향상을 위해 API 호출 생략 후 즉시 이동.
            Debug.Log("[TitleManager] 에디터 모드: API 폴링을 생략하고 튜토리얼로 이동합니다.");
            yield return CoroutineData.GetWaitForSeconds(1.0f);
            GoToTutorial();
            yield break;
#endif
            // # TODO: 매 루프마다 발생하는 문자열 보간($) 가비지 생성 방지를 위해 URL 캐싱 필요.
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
                        
                        // 서버 응답으로 정상 활성화(USING) 상태 검증.
                        if (!string.IsNullOrEmpty(responseText) && responseText.IndexOf("USING", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            Debug.Log("[TitleManager] RoomState USING 감지. 튜토리얼로 이동.");
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
        /// 키보드 입력을 통한 강제 진행 감지.
        /// </summary>
        private void Update()
        {
            if (_isTransitioning) return; 

            // 하드웨어 오류 시 키보드로 우회 진행 허용.
            if (Input.GetKeyDown(KeyCode.Return))
            {
                GoToTutorial();
            }
        }

        /// <summary>
        /// 튜토리얼 씬으로 전환함.
        /// </summary>
        private void GoToTutorial()
        {
            // 중복 씬 로드 방지.
            if (_isTransitioning) return;
            _isTransitioning = true; 

            SceneManager.LoadScene(GameConstants.Scene.Tutorial);
        }
        
        /// <summary>
        /// 타이틀 화면 진입 시 메인 BGM을 재생함.
        /// </summary>
        private IEnumerator StartMainBGM()
        {
            if (!SoundManager.Instance) yield break;

            // 기존 BGM 중단 후 지연 재생으로 사운드 겹침 방지.
            SoundManager.Instance.StopBGM();
            yield return CoroutineData.GetWaitForSeconds(1.0f);
            SoundManager.Instance.PlayBGM("MainBGM");
        }

        /// <summary>
        /// 씬 전환 시 실행 중인 코루틴 메모리 정리.
        /// </summary>
        private void OnDestroy()
        {   
            StopAllCoroutines();
            _soundCoroutine = null;
            _pollCoroutine = null;
        }
    }
}