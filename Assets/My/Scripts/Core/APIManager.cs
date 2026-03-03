using System;
using System.Collections;
using System.Collections.Generic;
using My.Scripts.Core.Data;
using UnityEngine;
using UnityEngine.Networking;
using Newtonsoft.Json; 
using Wonjeong.Utils;

namespace My.Scripts.Core
{
    public struct UserData
    {
        public int IDX_USER; 
        public string UID_LEFT;
        public string UID_RIGHT;
        public string LANG;
        public int RELATION;
        
        public ColorData COLOR_LEFT; 
        public ColorData COLOR_RIGHT;

        public string RESERVATION_FIRST_NAME_LEFT;
        public string RESERVATION_LAST_NAME_LEFT;
        public string RESERVATION_FIRST_NAME_RIGHT;
        public string RESERVATION_LAST_NAME_RIGHT;
        
        public int PIECE_A1;
        public int PIECE_A2;  
        public int PIECE_A3;
        public int PIECE_B1;
        public int PIECE_B2;  
        public int PIECE_B3;
        public int PIECE_C1;
        public int PIECE_C2;
        public int PIECE_C3;
        public int PIECE_D1;
        public int PIECE_D2;
        public int PIECE_D3;
    }

    public class ApiTableResponse
    {
        public List<string> COLUMNS { get; set; }
        public List<List<object>> DATA { get; set; } 
    }

    public class APIManager : MonoBehaviour
    {
        [Header("API Settings")]
        [Tooltip("조회할 유저의 UID를 입력하세요.")]
#if UNITY_EDITOR
        [SerializeField] private string userUid = "2270AE4A-ABFC-E349-1A0A5A69999CC1A8";
#else
        [SerializeField] private string userUid = "";
#endif

        private void Start()
        {
            if (!string.IsNullOrEmpty(userUid))
            {
                FetchData();
            }
        }
        
        [ContextMenu("Fetch API Data")]
        public void FetchData()
        {
            ApiSettings config = null;
            if (GameManager.Instance) config = GameManager.Instance.ApiConfig;
            if (config == null) config = JsonLoader.Load<ApiSettings>(GameConstants.Path.ApiSetting);

            if (config == null)
            {
                Debug.LogError("[APIManager] API 설정을 찾을 수 없습니다.");
                return;
            }

            string requestUrl = $"{config.GetUserUrl}?uid={userUid}";
            StartCoroutine(GetApiDataRoutine(requestUrl));
        }

        private IEnumerator GetApiDataRoutine(string url)
        {
            string maskedUrl = string.IsNullOrEmpty(userUid) ? url : url.Replace(userUid, "<masked_uid>");
            Debug.Log($"[APIManager] API 요청 시작: {maskedUrl}");

            using (UnityWebRequest webRequest = UnityWebRequest.Get(url))
            { 
                webRequest.timeout = 10; 
                
                yield return webRequest.SendWebRequest();

                if (webRequest.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogError($"[APIManager] 통신 실패: {webRequest.error}");
                }
                else
                {
                    string jsonResult = webRequest.downloadHandler.text;
                    Debug.Log("[APIManager] 데이터 수신 성공! 파싱을 시작합니다.");
                    ParseAndProcessData(jsonResult);
                }
            }
        }

        public void ParseAndProcessData(string jsonString)
        {
            try
            {
                ApiTableResponse response = JsonConvert.DeserializeObject<ApiTableResponse>(jsonString);

                if (response != null && response.DATA != null && response.DATA.Count > 0)
                {
                    List<object> firstRow = response.DATA[0];
                    UserData userData = new UserData();

                    userData.IDX_USER = ParseIntSafe(response, firstRow, "IDX_USER");
                    userData.UID_LEFT = ParseStringSafe(response, firstRow, "UID_LEFT");
                    userData.UID_RIGHT = ParseStringSafe(response, firstRow, "UID_RIGHT");
                    
                    userData.LANG = ParseStringSafe(response, firstRow, "LANG");
                    userData.RELATION = ParseIntSafe(response, firstRow, "RELATION");

                    userData.RESERVATION_FIRST_NAME_LEFT = ParseStringSafe(response, firstRow, "RESERVATION_FIRST_NAME_LEFT");
                    userData.RESERVATION_LAST_NAME_LEFT = ParseStringSafe(response, firstRow, "RESERVATION_LAST_NAME_LEFT");
                    userData.RESERVATION_FIRST_NAME_RIGHT = ParseStringSafe(response, firstRow, "RESERVATION_FIRST_NAME_RIGHT");
                    userData.RESERVATION_LAST_NAME_RIGHT = ParseStringSafe(response, firstRow, "RESERVATION_LAST_NAME_RIGHT");
                    
                    userData.COLOR_LEFT = ParseColorSafe(response, firstRow, "COLOR_LEFT");
                    userData.COLOR_RIGHT = ParseColorSafe(response, firstRow, "COLOR_RIGHT");

                    userData.PIECE_A1 = ParseIntSafe(response, firstRow, "PIECE_A1");
                    userData.PIECE_A2 = ParseIntSafe(response, firstRow, "PIECE_A2");
                    userData.PIECE_A3 = ParseIntSafe(response, firstRow, "PIECE_A3");
                    userData.PIECE_B1 = ParseIntSafe(response, firstRow, "PIECE_B1");
                    userData.PIECE_B2 = ParseIntSafe(response, firstRow, "PIECE_B2");
                    userData.PIECE_B3 = ParseIntSafe(response, firstRow, "PIECE_B3");
                    userData.PIECE_C1 = ParseIntSafe(response, firstRow, "PIECE_C1");
                    userData.PIECE_C2 = ParseIntSafe(response, firstRow, "PIECE_C2");
                    userData.PIECE_C3 = ParseIntSafe(response, firstRow, "PIECE_C3");
                    userData.PIECE_D1 = ParseIntSafe(response, firstRow, "PIECE_D1");
                    userData.PIECE_D2 = ParseIntSafe(response, firstRow, "PIECE_D2");
                    userData.PIECE_D3 = ParseIntSafe(response, firstRow, "PIECE_D3");

                    if (GameManager.Instance)
                    {   
                        GameManager.Instance.CurrentUserId = userData.IDX_USER;
                        GameManager.Instance.CurrentLanguage = userData.LANG;

                        switch (userData.RELATION)
                        {
                            case 1: GameManager.Instance.currentUserType = UserType.A; break;
                            case 2: GameManager.Instance.currentUserType = UserType.B; break;
                            case 3: GameManager.Instance.currentUserType = UserType.C; break;
                            case 4: GameManager.Instance.currentUserType = UserType.D; break;
                            case 5: GameManager.Instance.currentUserType = UserType.E; break;
                            case 6: GameManager.Instance.currentUserType = UserType.F; break;
                            default: 
                                GameManager.Instance.currentUserType = UserType.A; 
                                Debug.LogWarning($"[APIManager] 알 수 없는 RELATION 값({userData.RELATION})입니다. UserType.A로 기본 설정됩니다.");
                                break;
                        }

                        Debug.Log($"[APIManager] 언어: {userData.LANG}, 관계: {userData.RELATION} -> 설정된 UserType: {GameManager.Instance.currentUserType}");

                        if (!string.IsNullOrEmpty(userData.RESERVATION_LAST_NAME_LEFT))
                            GameManager.Instance.PlayerALastName = userData.RESERVATION_LAST_NAME_LEFT;
                            
                        if (!string.IsNullOrEmpty(userData.RESERVATION_LAST_NAME_RIGHT))
                            GameManager.Instance.PlayerBLastName = userData.RESERVATION_LAST_NAME_RIGHT;
                        
                        GameManager.Instance.PlayerAColor = userData.COLOR_LEFT;
                        GameManager.Instance.PlayerBColor = userData.COLOR_RIGHT;

                        GameManager.Instance.PieceA1 = userData.PIECE_A1;
                        GameManager.Instance.PieceA2 = userData.PIECE_A2;
                        GameManager.Instance.PieceA3 = userData.PIECE_A3;
                        GameManager.Instance.PieceB1 = userData.PIECE_B1;
                        GameManager.Instance.PieceB2 = userData.PIECE_B2;
                        GameManager.Instance.PieceB3 = userData.PIECE_B3;
                        GameManager.Instance.PieceC1 = userData.PIECE_C1;
                        GameManager.Instance.PieceC2 = userData.PIECE_C2;
                        GameManager.Instance.PieceC3 = userData.PIECE_C3;
                        GameManager.Instance.PieceD1 = userData.PIECE_D1;
                        GameManager.Instance.PieceD2 = userData.PIECE_D2;
                        GameManager.Instance.PieceD3 = userData.PIECE_D3;

                        // 이벤트 호출을 통해 색상 등이 필요한 모든 컨트롤러가 실시간으로 데이터를 반영하도록 알림
                        GameManager.Instance.NotifyUserDataUpdated();
                    }
                }
                else
                {
                    Debug.LogWarning("[APIManager] JSON 응답에 데이터(DATA 배열)가 없습니다.");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[APIManager] 파싱 중 에러 발생: {e.Message}\n수신된 JSON: {jsonString}");
            }
        }

        #region 데이터 추출 헬퍼 메서드

        private int ParseIntSafe(ApiTableResponse response, List<object> row, string colName)
        {
            if (response?.COLUMNS == null || row == null) return 0;
            int index = response.COLUMNS.IndexOf(colName);
            if (index != -1 && row.Count > index && row[index] != null)
            {
                string valStr = row[index].ToString().Trim();
                if (string.IsNullOrEmpty(valStr)) return 0;
                
                if (int.TryParse(valStr, out int val)) return val;
                
                if (float.TryParse(valStr, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out float fVal)) 
                    return (int)fVal;
            }
            return 0; 
        }

        private string ParseStringSafe(ApiTableResponse response, List<object> row, string colName)
        {
            if (response?.COLUMNS == null || row == null) return string.Empty;
            int index = response.COLUMNS.IndexOf(colName);
            if (index != -1 && row.Count > index && row[index] != null)
            {
                return row[index].ToString();
            }
            return string.Empty; 
        }

        private ColorData ParseColorSafe(ApiTableResponse response, List<object> row, string colName)
        {
            if (response?.COLUMNS == null || row == null) return ColorData.NotSet;
            int index = response.COLUMNS.IndexOf(colName);
            if (index != -1 && row.Count > index && row[index] != null)
            {
                if (int.TryParse(row[index].ToString(), out int val))
                {
                    if (val >= (int)ColorData.NotSet && val <= (int)ColorData.Yellow)
                    {
                        return (ColorData)val;   
                    }
                }
            }
            return ColorData.NotSet; 
        }

        #endregion
    }
}