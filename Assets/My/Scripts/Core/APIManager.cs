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
    public enum ColorData { NotSet = -1, Cyan = 0, Pink = 1, Orange = 2, Green = 3, Red = 4, Yellow = 5 }
    
    public struct UserData
    {
        public string CARTRIDGE;
        public string BLOCK_CODE;
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

    public class ApiTableResponse
    {
        public List<string> COLUMNS { get; set; }
        public List<List<object>> DATA { get; set; } 
    }

    /// <summary>
    /// 외부 서버와 통신하여 유저 데이터를 가져오고 세션 매니저에 적용하는 클래스.
    /// </summary>
    public class APIManager : MonoBehaviour
    {
        private string userUid;

        /// <summary>
        /// 비동기 데이터 패치 작업을 백그라운드에서 실행함.
        /// </summary>
        /// <param name="uid">조회할 유저의 고유 식별자</param>
        public void FetchData(string uid) { FetchDataAsync(uid).Forget(); }
        
        /// <summary>
        /// 실제 서버 API를 호출하여 유저 정보를 가져옴.
        /// </summary>
        /// <param name="uid">조회할 유저의 고유 식별자</param>
        /// <returns>데이터 패치 성공 여부</returns>
        [ContextMenu("Fetch API Data")]
        public async UniTask<bool> FetchDataAsync(string uid)
        {
#if UNITY_EDITOR
            if (SessionManager.Instance)
            {
                SessionManager.Instance.CurrentUserId = -1;
                SessionManager.Instance.PlayerAUid = "TEST_A";
                SessionManager.Instance.PlayerBUid = "TEST_B";
                SessionManager.Instance.PlayerAFirstName = "에디터";
                SessionManager.Instance.PlayerBFirstName = "테스터";
                SessionManager.Instance.Cartridge = "A";
                SessionManager.Instance.BlockCode = "A1, B2, C2, D3";

                if (GameManager.Instance)
                {
                    if (GameManager.Instance.forceUserType)
                    {
                        SessionManager.Instance.CurrentUserType = GameManager.Instance.debugUserType;
                    }
                    else
                    {
                        SessionManager.Instance.CurrentUserType = UserType.A1;
                    }
                }
                else
                {
                    Debug.LogWarning("GameManager 인스턴스 누락됨.");
                }
                
                SessionManager.Instance.IsOtherCartridgeContentsCleared = true; 
            }
            else
            {
                Debug.LogWarning("SessionManager 인스턴스 누락됨.");
            }
            Debug.Log("에디터 모드: 유저 데이터 통신을 생략하고 가상 세션을 생성함.");
            return true;
#endif

            userUid = uid;
            ApiSettings config = null;

            if (GameManager.Instance)
            {
                config = GameManager.Instance.ApiConfig;
            }
            else
            {
                Debug.LogWarning("GameManager 인스턴스 누락됨.");
            }

            // 이유: 설정값이 누락되었을 때 하드코딩된 Fallback 데이터를 사용하지 않고 명시적인 에러 로그를 남겨 디버깅을 도움.
            if (config == null)
            {
                Debug.LogError("API 설정 데이터가 존재하지 않음.");
                return false;
            }

            string requestUrl = $"{config.GetUserUrl}?uid={userUid}";
            int maxRetries = 10;
            
            // # TODO: 재시도 횟수 및 딜레이 시간을 외부 설정 파일에서 주입받도록 구조 개선 필요.
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
                        Debug.LogWarning($"FetchDataAsync 통신 예외 발생 ({i + 1}/{maxRetries}): {e.Message}");
                    }
                    
                    Debug.LogWarning($"통신 실패 ({i + 1}/{maxRetries}). 1초 후 재시도: {webRequest.error}");
                    
                    if (i < maxRetries - 1) 
                    {
                        await UniTask.Delay(TimeSpan.FromSeconds(1f));
                    }
                }
            }
            
            Debug.LogError("FetchDataAsync 최대 재시도 횟수 초과됨.");
            return false;
        }

        /// <summary>
        /// 서버로부터 받은 JSON 응답을 역직렬화하고 SessionManager에 매핑함.
        /// </summary>
        /// <param name="jsonString">API 응답 문자열</param>
        /// <returns>파싱 및 적용 성공 여부</returns>
        public async UniTask<bool> ParseAndProcessDataAsync(string jsonString)
        {
            try
            {
                ApiTableResponse response = await UniTask.RunOnThreadPool(() => JsonConvert.DeserializeObject<ApiTableResponse>(jsonString));

                if (response == null || response.DATA == null || response.DATA.Count <= 0)
                {
                    Debug.LogWarning("유효한 API 응답 데이터가 없음.");
                    return false;
                }

                List<object> firstRow = response.DATA[0];

                Dictionary<string, int> colMap = new Dictionary<string, int>();
                for (int i = 0; i < response.COLUMNS.Count; i++)
                {
                    // 예시 입력: COLUMNS["IDX_USER"] = 0 번째 인덱스 할당.
                    colMap[response.COLUMNS[i]] = i;
                }

                UserData userData = new UserData();
                userData.IDX_USER = ParseIntSafe(colMap, firstRow, "IDX_USER");
                userData.CARTRIDGE = ParseStringSafe(colMap, firstRow, "CARTRIDGE"); 
                userData.BLOCK_CODE = ParseStringSafe(colMap, firstRow, "BLOCK_CODE");
                userData.UID_LEFT = ParseStringSafe(colMap, firstRow, "UID_LEFT");
                userData.UID_RIGHT = ParseStringSafe(colMap, firstRow, "UID_RIGHT");
                userData.LANG = ParseStringSafe(colMap, firstRow, "LANG");
                userData.RELATION = ParseIntSafe(colMap, firstRow, "RELATION");
                userData.RESERVATION_FIRST_NAME_LEFT = ParseStringSafe(colMap, firstRow, "RESERVATION_FIRST_NAME_LEFT");
                userData.RESERVATION_FIRST_NAME_RIGHT = ParseStringSafe(colMap, firstRow, "RESERVATION_FIRST_NAME_RIGHT");
                userData.COLOR_LEFT = ParseColorSafe(colMap, firstRow, "COLOR_LEFT");
                userData.COLOR_RIGHT = ParseColorSafe(colMap, firstRow, "COLOR_RIGHT");

                if (!SessionManager.Instance)
                {
                    Debug.LogWarning("SessionManager 인스턴스 누락됨.");
                    return false;
                }

                SessionManager.Instance.CurrentUserId = userData.IDX_USER;
                SessionManager.Instance.Cartridge = userData.CARTRIDGE; 
                SessionManager.Instance.BlockCode = userData.BLOCK_CODE; 
                SessionManager.Instance.PlayerAUid = userData.UID_LEFT;
                SessionManager.Instance.PlayerBUid = userData.UID_RIGHT;

                if (!string.IsNullOrWhiteSpace(userData.LANG)) 
                {
                    SessionManager.Instance.CurrentLanguage = userData.LANG.Trim();
                }

                if (!string.IsNullOrEmpty(userData.RESERVATION_FIRST_NAME_LEFT))
                {
                    SessionManager.Instance.PlayerAFirstName = userData.RESERVATION_FIRST_NAME_LEFT;
                }
                else
                {
                    Debug.LogWarning("1P 예약자 이름 데이터 누락됨.");
                }

                if (!string.IsNullOrEmpty(userData.RESERVATION_FIRST_NAME_RIGHT))
                {
                    SessionManager.Instance.PlayerBFirstName = userData.RESERVATION_FIRST_NAME_RIGHT;
                }
                else
                {
                    Debug.LogWarning("2P 예약자 이름 데이터 누락됨.");
                }
                
                SessionManager.Instance.PlayerAColor = userData.COLOR_LEFT;
                SessionManager.Instance.PlayerBColor = userData.COLOR_RIGHT;

                string cartridgeStr = userData.CARTRIDGE;
                if (string.IsNullOrEmpty(cartridgeStr))
                {
                    Debug.LogWarning("카트리지 데이터 누락됨.");
                }
                
                int relationInt = userData.RELATION;
                string typeStr = $"{cartridgeStr}{relationInt}";

                if (Enum.TryParse(typeStr, out UserType parsedType))
                {
                    SessionManager.Instance.CurrentUserType = parsedType;
                }
                else
                {
                    Debug.LogWarning($"알 수 없는 유저 타입({typeStr})입니다.");
                }

                Debug.Log($"유저 데이터 로드 완료. 인덱스: {userData.IDX_USER}, 이름: {userData.RESERVATION_FIRST_NAME_LEFT}/{userData.RESERVATION_FIRST_NAME_RIGHT}, 타입: {typeStr}, 컬러: {userData.COLOR_LEFT}/{userData.COLOR_RIGHT}");
                
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

                SessionManager.Instance.IsOtherCartridgeContentsCleared = ParseOtherCartridgeClearState(response, firstRow);

                return true; 
            }
            catch (Exception e)
            {
                Debug.LogError($"JSON 파싱 중 에러 발생: {e.Message}");
                return false;
            }
        }

        /// <summary>
        /// 동적 데이터 배열에서 정수형 값을 안전하게 파싱함.
        /// </summary>
        /// <param name="map">컬럼 맵핑 딕셔너리</param>
        /// <param name="row">데이터 배열</param>
        /// <param name="col">조회할 컬럼명</param>
        /// <returns>추출된 정수값</returns>
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
        /// 동적 데이터 배열에서 문자열 값을 안전하게 파싱함.
        /// </summary>
        /// <param name="map">컬럼 맵핑 딕셔너리</param>
        /// <param name="row">데이터 배열</param>
        /// <param name="col">조회할 컬럼명</param>
        /// <returns>추출된 문자열</returns>
        private string ParseStringSafe(Dictionary<string, int> map, List<object> row, string col)
        {
            if (map.TryGetValue(col, out int idx) && row.Count > idx && row[idx] != null) 
            {
                return row[idx].ToString();
            }
            return string.Empty; 
        }

        /// <summary>
        /// 동적 데이터 배열에서 색상 열거형 값을 안전하게 파싱함.
        /// </summary>
        /// <param name="map">컬럼 맵핑 딕셔너리</param>
        /// <param name="row">데이터 배열</param>
        /// <param name="col">조회할 컬럼명</param>
        /// <returns>추출된 색상 열거형 데이터</returns>
        private ColorData ParseColorSafe(Dictionary<string, int> map, List<object> row, string col)
        {
            if (map.TryGetValue(col, out int idx) && row.Count > idx && row[idx] != null)
            {
                if (int.TryParse(row[idx].ToString(), out int val))
                {
                    if (val >= (int)ColorData.NotSet && val <= (int)ColorData.Yellow) 
                    {
                        return (ColorData)val;   
                    }
                }
            }
            return ColorData.NotSet; 
        }

        /// <summary>
        /// 할당된 다른 블록 콘텐츠들의 클리어 여부를 검사하여 특별 엔딩 진입 조건을 판정함.
        /// </summary>
        /// <param name="resp">전체 응답 객체</param>
        /// <param name="row">데이터 배열</param>
        /// <returns>특별 엔딩 진입 가능 여부</returns>
        private bool ParseOtherCartridgeClearState(ApiTableResponse resp, List<object> row)
        {   
            Dictionary<string, int> map = new Dictionary<string, int>();
            for (int i = 0; i < resp.COLUMNS.Count; i++)
            {
                map[resp.COLUMNS[i]] = i;
            }
            
            string currentCode = "A1";
            if (SessionManager.Instance)
            {
                currentCode = SessionManager.Instance.CurrentModuleCode.ToUpper();
            }
            else
            {
                Debug.LogWarning("SessionManager 인스턴스 누락됨.");
            }
            
            string blockCodeStr = ParseStringSafe(map, row, "BLOCK_CODE");

            if (string.IsNullOrWhiteSpace(blockCodeStr) || blockCodeStr.Equals("null", StringComparison.OrdinalIgnoreCase))
            {
                return false; 
            }

            string[] assignedBlocks = blockCodeStr.Split(',', StringSplitOptions.RemoveEmptyEntries);
            int otherContentCount = 0;

            foreach (string blockId in assignedBlocks)
            {
                string cleanBlockId = blockId.Trim().ToUpper();
                if (string.IsNullOrEmpty(cleanBlockId)) continue;

                // 이유: 현재 플레이 중인 모듈은 클리어 판정 검사에서 제외함.
                if (cleanBlockId == currentCode) continue;

                // 이유: Z코드(추가 결제 등 특수 목적)는 클리어 진행률 검사에서 제외함.
                if (cleanBlockId.StartsWith("Z")) continue;

                otherContentCount++;

                string targetEndColumn = $"END_{cleanBlockId}";
                string endValue = ParseStringSafe(map, row, targetEndColumn);

                if (string.IsNullOrWhiteSpace(endValue) || endValue.Equals("null", StringComparison.OrdinalIgnoreCase))
                {
                    Debug.Log($"특별 엔딩 불가. 미클리어 상태인 블록: {cleanBlockId}");
                    return false; 
                }
            }
            
            bool isSpecial = otherContentCount > 0;
            
            Debug.Log($"특별 엔딩 판정 완료. 검사한 타 컨텐츠 수: {otherContentCount}. 진입 여부: {isSpecial}");
            
            return isSpecial;
        }
    }
}