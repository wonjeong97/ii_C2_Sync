using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

public class ApiRequester : MonoBehaviour
{
    private const string BaseUrl = "http://192.168.0.252:8500/api/resetStart.cfm";

    private void Start()
    {
        StartCoroutine(SendResetRequests());
    }

    /// <summary>
    /// 1부터 100까지 idx_user를 변경하며 API 요청을 순차적으로 전송함.
    /// </summary>
    /// <returns>Coroutine enumeration.</returns>
    private IEnumerator SendResetRequests()
    {
        for (int i = 1; i <= 400; i++)
        {
            string requestUrl = $"{BaseUrl}?idx_user={i}&code=c2";
            yield return StartCoroutine(ExecuteRequest(requestUrl, i));
        }
    }

    /// <summary>
    /// 특정 URL로 GET 요청을 보내고 결과를 처리함.
    /// </summary>
    /// <param name="url">요청을 보낼 전체 URL.</param>
    /// <param name="userIdx">현재 처리 중인 사용자 인덱스.</param>
    /// <returns>Coroutine enumeration.</returns>
    private IEnumerator ExecuteRequest(string url, int userIdx)
    {
        using (UnityWebRequest webRequest = UnityWebRequest.Get(url))
        {
            yield return webRequest.SendWebRequest();

            if (webRequest.result != UnityWebRequest.Result.Success)
            {
                // 네트워크 오류 또는 서버 에러 발생 시 로그 기록
                Debug.LogError($"User {userIdx} 요청 실패: {webRequest.error}");
            }
            else
            {
                // 성공적인 응답 수신 시 로그 기록
                Debug.Log($"User {userIdx} 요청 성공: {webRequest.downloadHandler.text}");
            }
        }
    }
}