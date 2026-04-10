using System;
using System.Collections;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace My.Scripts.Utils
{
    /// <summary>
    /// 특정 범위의 유저 인덱스에 대해 방 퇴장 API를 일괄적으로 호출하는 유틸리티 클래스.
    /// </summary>
    public class ApiBatchExecutor : MonoBehaviour
    {
        private const string RequestUrlFormat = "http://192.168.0.252:8500/api/exitRoom.cfm?code=c2&idx_user={0}";

        /// <summary>
        /// 1부터 500까지의 유저 인덱스를 순회하며 비동기로 API를 호출함.
        /// </summary>
        [ContextMenu("Fire Exit API 1-500")]
        public void RunBatchRequest()
        {
            ExecuteBatchAsync().Forget();
        }

        /// <summary>
        /// 실제 루프를 돌며 UnityWebRequest를 송신하는 코어 로직.
        /// </summary>
        /// <returns>비동기 작업</returns>
        private async UniTaskVoid ExecuteBatchAsync()
        {
            for (int i = 501; i <= 1000; i++)
            {
                // 이유: 인덱스 번호를 포맷에 맞춰 완성된 URL로 생성함.
                string fullUrl = string.Format(RequestUrlFormat, i);

                using (UnityWebRequest webRequest = UnityWebRequest.Get(fullUrl))
                {
                    // 이유: 개별 요청이 전체 루프를 지연시키지 않도록 타임아웃을 짧게 설정함.
                    webRequest.timeout = 5;

                    try
                    {
                        await webRequest.SendWebRequest().ToUniTask();

                        if (webRequest.result == UnityWebRequest.Result.Success)
                        {
                            Debug.Log($"[BatchAPI] 성공 - User: {i}");
                        }
                        else
                        {
                            Debug.LogWarning($"[BatchAPI] 실패 - User: {i} | {webRequest.error}");
                        }
                    }
                    catch (Exception e)
                    {
                        Debug.LogError($"[BatchAPI] 예외 발생 - User: {i} | {e.Message}");
                    }
                }

                // 이유: 500개의 요청이 찰나의 순간에 몰려 서버가 차단(DDOS 오인)되는 것을 방지하기 위해 미세한 간격을 둠.
                await UniTask.Delay(TimeSpan.FromMilliseconds(20));
            }

            Debug.Log("[BatchAPI] 모든 요청 완료.");
        }
    }
}