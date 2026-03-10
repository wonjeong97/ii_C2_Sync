using System;
using System.Collections;
using My.Scripts.Core;
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
        [SerializeField] private float basePollInterval = 1.0f; // 기본 API 폴링 간격
        [SerializeField] private float maxPollInterval = 10.0f; // 통신 실패 시 지수 백오프 최대 한도

        private float _currentPollInterval; 
        private bool _isTransitioning; // 중복 씬 로드 방지 가드 플래그
        
        private Coroutine _soundCoroutine;
        private Coroutine _pollCoroutine;

        /// <summary> 씬 진입 시 초기 상태를 구성하고 API 폴링을 시작합니다. </summary>
        private void Start()
        {
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
            _currentPollInterval = basePollInterval;

            while (!_isTransitioning)
            {
                if (!GameManager.Instance || GameManager.Instance.ApiConfig == null)
                {
                    yield return CoroutineData.GetWaitForSeconds(_currentPollInterval);
                    continue;
                }

                string requestUrl = $"{GameManager.Instance.ApiConfig.CheckRoomStateUrl}?code=c2";

                using (UnityWebRequest webRequest = UnityWebRequest.Get(requestUrl))
                {
                    webRequest.timeout = 10;
                    
                    yield return webRequest.SendWebRequest();

                    // 네트워크 오류 시 즉각적인 재시도로 인한 서버 과부하를 막기 위해 지수 백오프(Exponential Backoff) 적용
                    if (webRequest.result == UnityWebRequest.Result.ConnectionError || 
                        webRequest.result == UnityWebRequest.Result.ProtocolError)
                    {
                        _currentPollInterval = Mathf.Min(_currentPollInterval * 2f, maxPollInterval);
                        Debug.LogWarning($"[TitleManager] 상태 체크 통신 실패: {webRequest.error}. 백오프 적용: {_currentPollInterval}초 후 재시도");
                    }
                    else
                    {
                        // 통신 성공 시 폴링 주기를 기본값으로 즉시 복구
                        _currentPollInterval = basePollInterval;

                        string responseText = webRequest.downloadHandler.text;
                        
                        // 서버에서 'USING' 응답 반환 시 출입문 태그 완료(유저 입장)로 간주하고 자동 진행
                        if (!string.IsNullOrEmpty(responseText) && responseText.IndexOf("USING", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            Debug.Log($"[TitleManager] RoomState 'USING' 감지. 튜토리얼로 이동.");
                            GoToTutorial();
                            yield break;
                        }
                    }
                }

                yield return CoroutineData.GetWaitForSeconds(_currentPollInterval);
            }
        }

        /// <summary> 관리자 테스트 및 비상용 수동 전환 단축키 입력 대기 </summary>
        private void Update()
        {
            if (_isTransitioning) return; 

            // 엔터 키 입력 시 통신 결과와 무관하게 즉시 튜토리얼로 강제 진입
            if (Input.GetKeyDown(KeyCode.Return))
            {
                GoToTutorial();
            }
        }

        /// <summary> 튜토리얼 씬으로 전환하며 중복 호출을 차단합니다. </summary>
        private void GoToTutorial()
        {
            if (_isTransitioning) return;
            _isTransitioning = true; 

            SceneManager.LoadScene(GameConstants.Scene.Tutorial);
        }

        /// <summary> 
        /// 이전 씬(엔딩)의 잔여 사운드와 겹치거나 갑작스러운 소음으로 인한 불쾌감을 막기 위해 일정 시간 대기 후 BGM을 재생합니다. 
        /// </summary>
        private IEnumerator StartMainBGM()
        {
            if (!SoundManager.Instance) yield break;

            SoundManager.Instance.StopBGM();
            yield return CoroutineData.GetWaitForSeconds(1.0f);
            SoundManager.Instance.PlayBGM("MainBGM");
        }

        /// <summary> 오브젝트 파괴 시 백그라운드 코루틴을 정리하여 메모리 누수를 방지합니다. </summary>
        private void OnDestroy()
        {   
            StopAllCoroutines();
            _soundCoroutine = null;
            _pollCoroutine = null;
        }
    }
}