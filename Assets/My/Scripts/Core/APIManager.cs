using System;
using System.Collections;
using System.Collections.Generic;
using Cysharp.Threading.Tasks; 
using My.Scripts.Core.Data;
using UnityEngine;
using UnityEngine.Networking;
using Newtonsoft.Json; 
using My.Scripts.Global;
using Wonjeong.Utils;

namespace My.Scripts.Core
{
    /// <summary> 서버에서 전달되는 플레이어 고유 색상 코드 매핑용 열거형 </summary>
    public enum ColorData
    {   
        NotSet = -1,
        Cyan = 0, Pink = 1, Orange = 2, Green = 3, Red = 4, Yellow = 5
    }
    
    /// <summary> API 응답 데이터 중 세션 관리에 필요한 유저 정보 구조체 </summary>
    public struct UserData
    {
        public string CARTRIDGE;
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
        
        public int PIECE_A1; public int PIECE_A2; public int PIECE_A3;
        public int PIECE_B1; public int PIECE_B2; public int PIECE_B3;
        public int PIECE_C1; public int PIECE_C2; public int PIECE_C3;
        public int PIECE_D1; public int PIECE_D2; public int PIECE_D3;
    }

    /// <summary> 서버 JSON 테이블 구조(COLUMNS/DATA) 역직렬화용 클래스 </summary>
    public class ApiTableResponse
    {
        public List<string> COLUMNS { get; set; }
        public List<List<object>> DATA { get; set; } 
    }

    /// <summary> 
    /// 서버 API 통신 및 유저 데이터 파싱 매니저.
    /// 비동기 처리를 통해 통신 중 프레임 드랍을 방지하고 작업 완료 시점을 명시적으로 보장합니다.
    /// </summary>
    public class APIManager : MonoBehaviour
    {
        private string userUid;

        /// <summary> 
        /// 레거시 동기 코드 호환용 래퍼 
        /// </summary>
        /// <param name="uid">유저 고유 식별자</param>
        public void FetchData(string uid)
        {
            FetchDataAsync(uid).Forget();
        }
        
        /// <summary> 
        /// UID 기반 유저 데이터 비동기 요청 및 전역 설정 동기화.
        /// </summary>
        /// <param name="uid">유저 고유 식별자</param>
        /// <returns>통신 및 데이터 설정 성공 여부</returns>
        [ContextMenu("Fetch API Data")]
        public async UniTask<bool> FetchDataAsync(string uid)
        {
            userUid = uid;
            ApiSettings config = GameManager.Instance ? GameManager.Instance.ApiConfig : null;

            if (config == null)
            {
                config = JsonLoader.Load<ApiSettings>(GameConstants.Path.ApiSetting);
                if (GameManager.Instance && config != null) 
                {
                    GameManager.Instance.ApiConfig = config;
                }
            }

            if (config == null)
            {
                Debug.LogError("[APIManager] API 설정을 찾을 수 없습니다.");
                return false;
            }

            string requestUrl = $"{config.GetUserUrl}?uid={userUid}";
            int maxRetries = 10;
            
            // 이유: 일시적 네트워크 불안정 시 데이터 누락 방지. 1초 대기 후 최대 10회 재시도함.
            // TODO: 추후 네트워크 단절 UI 팝업과 연동하여 백그라운드 재시도를 사용자에게 알리는 기능 추가 고려
            for (int i = 0; i < maxRetries; i++)
            {
                using (UnityWebRequest webRequest = UnityWebRequest.Get(requestUrl))
                {
                    webRequest.timeout = 10; 
                    
                    try
                    {
                        await webRequest.SendWebRequest().ToUniTask();

                        if (webRequest.result == UnityWebRequest.Result.Success)
                        {
                            return await ParseAndProcessDataAsync(webRequest.downloadHandler.text);
                        }
                    }
                    catch (Exception e)
                    {
                        Debug.LogWarning($"[APIManager] FetchDataAsync 통신 예외 발생 ({i + 1}/{maxRetries}): {e.Message}");
                    }
                    
                    Debug.LogWarning($"[APIManager] 통신 실패 ({i + 1}/{maxRetries}). 1초 후 재시도: {webRequest.error}");
                    
                    if (i < maxRetries - 1)
                    {
                        await UniTask.Delay(TimeSpan.FromSeconds(1f));
                    }
                }
            }
            
            Debug.LogError("[APIManager] FetchDataAsync 최대 재시도 횟수 초과");
            return false;
        }

        /// <summary> 
        /// JSON 파싱 및 세션 할당. O(n*m) 탐색 비용을 줄이기 위해 컬럼 맵을 사전 빌드합니다.
        /// </summary>
        /// <param name="jsonString">서버 응답 JSON</param>
        /// <returns>파싱 및 유효성 검사 성공 여부</returns>
        public async UniTask<bool> ParseAndProcessDataAsync(string jsonString)
        {
            try
            {
                ApiTableResponse response = await UniTask.RunOnThreadPool(() => JsonConvert.DeserializeObject<ApiTableResponse>(jsonString));

                if (response != null && response.DATA != null && response.DATA.Count > 0)
                {
                    List<object> firstRow = response.DATA[0];

                    Dictionary<string, int> colMap = new Dictionary<string, int>();
                    for (int i = 0; i < response.COLUMNS.Count; i++)
                    {
                        colMap[response.COLUMNS[i]] = i;
                    }

                    UserData userData = new UserData();
                    userData.IDX_USER = ParseIntSafe(colMap, firstRow, "IDX_USER");
                    userData.CARTRIDGE = ParseStringSafe(colMap, firstRow, "CARTRIDGE"); 
                    userData.UID_LEFT = ParseStringSafe(colMap, firstRow, "UID_LEFT");
                    userData.UID_RIGHT = ParseStringSafe(colMap, firstRow, "UID_RIGHT");
                    userData.LANG = ParseStringSafe(colMap, firstRow, "LANG");
                    userData.RELATION = ParseIntSafe(colMap, firstRow, "RELATION");
                    userData.RESERVATION_FIRST_NAME_LEFT = ParseStringSafe(colMap, firstRow, "RESERVATION_FIRST_NAME_LEFT");
                    userData.RESERVATION_FIRST_NAME_RIGHT = ParseStringSafe(colMap, firstRow, "RESERVATION_FIRST_NAME_RIGHT");
                    userData.COLOR_LEFT = ParseColorSafe(colMap, firstRow, "COLOR_LEFT");
                    userData.COLOR_RIGHT = ParseColorSafe(colMap, firstRow, "COLOR_RIGHT");
                    
                    Debug.Log($"[APIManager] 유저 데이터 로드 완료!\n" +
                              $"- 유저 인덱스(IDX_USER): {userData.IDX_USER}\n" +
                              $"- 이름 (L/R): {userData.RESERVATION_FIRST_NAME_LEFT} / {userData.RESERVATION_FIRST_NAME_RIGHT}\n" +
                              $"- UID (L/R): {userData.UID_LEFT} / {userData.UID_RIGHT}\n" +
                              $"- 컬러 (L/R): {userData.COLOR_LEFT} / {userData.COLOR_RIGHT}\n" +
                              $"- 언어/관계: {userData.LANG} / {userData.RELATION}\n" +
                              $"- 카트리지: {userData.CARTRIDGE}");

                    if (SessionManager.Instance)
                    {   
                        SessionManager.Instance.CurrentUserId = userData.IDX_USER;
                        SessionManager.Instance.Cartridge = userData.CARTRIDGE; 
                        SessionManager.Instance.PlayerAUid = userData.UID_LEFT;
                        SessionManager.Instance.PlayerBUid = userData.UID_RIGHT;

                        if (!string.IsNullOrWhiteSpace(userData.LANG)) 
                            SessionManager.Instance.CurrentLanguage = userData.LANG.Trim();

                        switch (userData.RELATION)
                        {
                            case 1: SessionManager.Instance.CurrentUserType = UserType.A; break;
                            case 2: SessionManager.Instance.CurrentUserType = UserType.B; break;
                            case 3: SessionManager.Instance.CurrentUserType = UserType.C; break;
                            case 4: SessionManager.Instance.CurrentUserType = UserType.D; break;
                            case 5: SessionManager.Instance.CurrentUserType = UserType.E; break;
                            case 6: SessionManager.Instance.CurrentUserType = UserType.F; break;
                            default: SessionManager.Instance.CurrentUserType = UserType.A; break;
                        }

                        if (!string.IsNullOrEmpty(userData.RESERVATION_FIRST_NAME_LEFT))
                            SessionManager.Instance.PlayerAFirstName = userData.RESERVATION_FIRST_NAME_LEFT;
                        if (!string.IsNullOrEmpty(userData.RESERVATION_FIRST_NAME_RIGHT))
                            SessionManager.Instance.PlayerBFirstName = userData.RESERVATION_FIRST_NAME_RIGHT;
                        
                        SessionManager.Instance.PlayerAColor = userData.COLOR_LEFT;
                        SessionManager.Instance.PlayerBColor = userData.COLOR_RIGHT;
                        
                        userData.PIECE_A1 = ParseIntSafe(colMap, firstRow, "PIECE_A1");
                        userData.PIECE_A2 = ParseIntSafe(colMap, firstRow, "PIECE_A2");
                        userData.PIECE_A3 = ParseIntSafe(colMap, firstRow, "PIECE_A3");
                        userData.PIECE_B1 = ParseIntSafe(colMap, firstRow, "PIECE_B1");
                        userData.PIECE_B2 = ParseIntSafe(colMap, firstRow, "PIECE_B2");
                        userData.PIECE_B3 = ParseIntSafe(colMap, firstRow, "PIECE_B3");
                        userData.PIECE_C1 = ParseIntSafe(colMap, firstRow, "PIECE_C1");
                        userData.PIECE_C2 = ParseIntSafe(colMap, firstRow, "PIECE_C2");
                        userData.PIECE_C3 = ParseIntSafe(colMap, firstRow, "PIECE_C3");
                        userData.PIECE_D1 = ParseIntSafe(colMap, firstRow, "PIECE_D1");
                        userData.PIECE_D2 = ParseIntSafe(colMap, firstRow, "PIECE_D2");
                        userData.PIECE_D3 = ParseIntSafe(colMap, firstRow, "PIECE_D3");
                        
                        SessionManager.Instance.PieceA1 = Mathf.Max(0, userData.PIECE_A1);
                        SessionManager.Instance.PieceA2 = Mathf.Max(0, userData.PIECE_A2);
                        SessionManager.Instance.PieceA3 = Mathf.Max(0, userData.PIECE_A3);
                        SessionManager.Instance.PieceB1 = Mathf.Max(0, userData.PIECE_B1);
                        SessionManager.Instance.PieceB2 = Mathf.Max(0, userData.PIECE_B2);
                        SessionManager.Instance.PieceB3 = Mathf.Max(0, userData.PIECE_B3);
                        SessionManager.Instance.PieceC1 = Mathf.Max(0, userData.PIECE_C1);
                        SessionManager.Instance.PieceC2 = Mathf.Max(0, userData.PIECE_C2);
                        SessionManager.Instance.PieceC3 = Mathf.Max(0, userData.PIECE_C3);
                        SessionManager.Instance.PieceD1 = Mathf.Max(0, userData.PIECE_D1);
                        SessionManager.Instance.PieceD2 = Mathf.Max(0, userData.PIECE_D2);
                        SessionManager.Instance.PieceD3 = Mathf.Max(0, userData.PIECE_D3);

                        SessionManager.Instance.IsOtherCartridgeContentsCleared = false;
                        if (!string.IsNullOrWhiteSpace(userData.CARTRIDGE))
                        {
                            await CheckOtherCartridgeContentsAsync(userData.CARTRIDGE, response, firstRow);
                        }
                        return true; 
                    }
                }
                return false;
            }
            catch (Exception e)
            {
                Debug.LogError($"[APIManager] JSON 파싱 중 에러 발생: {e.Message}");
                return false;
            }
        }

        /// <summary> 
        /// 카트리지 묶음 콘텐츠의 클리어 상태 추가 조회 및 전역 설정 역주입 확인 
        /// </summary>
        /// <param name="cartridgeStr">카트리지 식별자</param>
        /// <param name="firstApiResponse">이전 응답 테이블</param>
        /// <param name="firstApiRow">이전 응답 행 데이터</param>
        private async UniTask CheckOtherCartridgeContentsAsync(string cartridgeStr, ApiTableResponse firstApiResponse, List<object> firstApiRow)
        {
            ApiSettings config = GameManager.Instance ? GameManager.Instance.ApiConfig : null;
            if (config == null)
            {
                config = JsonLoader.Load<ApiSettings>(GameConstants.Path.ApiSetting);
                if (GameManager.Instance && config != null)
                {
                   GameManager.Instance.ApiConfig = config;
                }
            }
            if (config == null) return;

            string url = $"{config.GetCartridgeContentUrl}?cartridge={UnityWebRequest.EscapeURL(cartridgeStr)}";
            int maxRetries = 10;
            
            // 이유: 카트리지 검증 통신 실패 시에도 1초 간격으로 최대 10회 재시도함.
            for (int i = 0; i < maxRetries; i++)
            {
                using (UnityWebRequest req = UnityWebRequest.Get(url))
                {
                    req.timeout = 10;
                    
                    try
                    {
                        await req.SendWebRequest().ToUniTask();
                        
                        if (req.result == UnityWebRequest.Result.Success)
                        {   
                            string targetListStr = req.downloadHandler.text;
                            if (SessionManager.Instance)
                            {   
                                SessionManager.Instance.IsOtherCartridgeContentsCleared = ParseOtherCartridgeClearState(targetListStr, firstApiResponse, firstApiRow);
                            }
                            return; 
                        }
                    }
                    catch (Exception e)
                    {
                        Debug.LogWarning($"[APIManager] CheckOtherCartridge 통신 예외 ({i + 1}/{maxRetries}): {e.Message}");
                    }

                    if (i < maxRetries - 1)
                    {
                        await UniTask.Delay(TimeSpan.FromSeconds(1f));
                    }
                }
            }
            
            Debug.LogError("[APIManager] CheckOtherCartridge 최대 재시도 횟수 초과");
        }

        /// <summary> 
        /// 사전 빌드된 맵을 사용하여 빠른 컬럼 데이터 추출 (정수) 
        /// </summary>
        /// <param name="map">컬럼 맵</param>
        /// <param name="row">행 데이터</param>
        /// <param name="col">추출할 컬럼명</param>
        /// <returns>추출된 정수값 (실패 시 0)</returns>
        private int ParseIntSafe(Dictionary<string, int> map, List<object> row, string col)
        {
            if (map.TryGetValue(col, out int idx) && row.Count > idx && row[idx] != null)
            {
                string valStr = row[idx].ToString().Trim();
                if (int.TryParse(valStr, out int val)) return val;
            }
            return 0; 
        }

        /// <summary> 
        /// 사전 빌드된 맵을 사용하여 빠른 컬럼 데이터 추출 (문자열) 
        /// </summary>
        /// <param name="map">컬럼 맵</param>
        /// <param name="row">행 데이터</param>
        /// <param name="col">추출할 컬럼명</param>
        /// <returns>추출된 문자열 (실패 시 string.Empty)</returns>
        private string ParseStringSafe(Dictionary<string, int> map, List<object> row, string col)
        {
            if (map.TryGetValue(col, out int idx) && row.Count > idx && row[idx] != null) 
                return row[idx].ToString();
            return string.Empty; 
        }

        /// <summary> 
        /// 사전 빌드된 맵을 사용하여 빠른 컬럼 데이터 추출 (컬러) 
        /// </summary>
        /// <param name="map">컬럼 맵</param>
        /// <param name="row">행 데이터</param>
        /// <param name="col">추출할 컬럼명</param>
        /// <returns>추출된 컬러 Enum 값 (실패 시 NotSet)</returns>
        private ColorData ParseColorSafe(Dictionary<string, int> map, List<object> row, string col)
        {
            if (map.TryGetValue(col, out int idx) && row.Count > idx && row[idx] != null)
            {
                if (int.TryParse(row[idx].ToString(), out int val))
                {
                    if (val >= (int)ColorData.NotSet && val <= (int)ColorData.Yellow) 
                        return (ColorData)val;   
                }
            }
            return ColorData.NotSet; 
        }

        /// <summary> 
        /// 타 모듈 클리어 데이터를 분석하여 카트리지 완성 여부 판단 
        /// </summary>
        /// <param name="targetListStr">요구 모듈 목록 문자열</param>
        /// <param name="resp">응답 구조체</param>
        /// <param name="row">해당 데이터 행</param>
        /// <returns>모든 요구 모듈 클리어 여부</returns>
        private bool ParseOtherCartridgeClearState(string targetListStr, ApiTableResponse resp, List<object> row)
        {   
            if (string.IsNullOrWhiteSpace(targetListStr)) return false;
            
            Dictionary<string, int> map = new Dictionary<string, int>();
            for (int i = 0; i < resp.COLUMNS.Count; i++) map[resp.COLUMNS[i]] = i;
            
            foreach (string target in targetListStr.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries))
            {   
                string code = target.Trim().ToUpper(); 
                if (code == (SessionManager.Instance ? SessionManager.Instance.CurrentModuleCode.ToUpper() : "C2")) 
                    continue;
                
                string val = ParseStringSafe(map, row, $"END_{code}");
                if (string.IsNullOrWhiteSpace(val) || val.Equals("null", StringComparison.OrdinalIgnoreCase)) 
                    return false; 
            }
            return true;
        }
    }
}