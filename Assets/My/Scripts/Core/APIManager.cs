using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks; 
using My.Scripts.Core.Data;
using UnityEngine;
using UnityEngine.Networking;
using Newtonsoft.Json; 
using Wonjeong.Utils;

namespace My.Scripts.Core
{
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

    public class ApiTableResponse
    {
        public List<string> COLUMNS { get; set; }
        public List<List<object>> DATA { get; set; } 
    }

    /// <summary> 
    /// 서버 API 통신 및 유저 데이터 파싱 매니저.
    /// UniTask를 활용한 비동기 처리를 통해 통신 및 대용량 JSON 파싱 중 프레임 드랍을 방지함.
    /// </summary>
    public class APIManager : MonoBehaviour
    {
        public static APIManager Instance;

        [Header("API Settings")]
        [Tooltip("조회할 유저의 UID를 입력하세요.")]
#if UNITY_EDITOR
        [SerializeField] private string userUid = "2270AE4A-ABFC-E349-1A0A5A69999CC1A8";
#else
        [SerializeField] private string userUid = "";
#endif

        private void Awake()
        {
            if (!Instance) Instance = this;
            else if (Instance != this) Destroy(gameObject);
        }

        private void Start()
        {
#if UNITY_EDITOR
            if (!string.IsNullOrEmpty(userUid))
            {
                FetchData(userUid);
            }
#endif
        }
        
        /// <summary> 
        /// 외부 콜백 호환을 위한 래퍼 메서드 
        /// </summary>
        public void FetchData(string uid, Action<bool> onComplete = null)
        {
            FetchDataWrapper(uid, onComplete).Forget();
        }

        private async UniTaskVoid FetchDataWrapper(string uid, Action<bool> onComplete)
        {
            bool result = await FetchDataAsync(uid);
            if (onComplete != null) onComplete(result);
        }

        /// <summary> 
        /// UID 기반 유저 데이터 비동기 요청.
        /// </summary>
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
            string maskedUrl = string.IsNullOrEmpty(userUid) ? requestUrl : requestUrl.Replace(userUid, "<masked_uid>");
            Debug.Log($"[APIManager] API 요청 시작: {maskedUrl}");
            
            using (UnityWebRequest webRequest = UnityWebRequest.Get(requestUrl))
            {
                webRequest.timeout = 10; 
                await webRequest.SendWebRequest().ToUniTask();

                if (webRequest.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogError($"[APIManager] 통신 실패: {webRequest.error}");
                    return false;
                }
                
                Debug.Log("[APIManager] 데이터 수신 성공! 파싱을 시작합니다.");
                return await ParseAndProcessDataAsync(webRequest.downloadHandler.text);
            }
        }

        /// <summary> 
        /// JSON 파싱 및 전역 데이터 할당. O(n) 탐색 비용을 줄이기 위해 컬럼 맵을 사전 빌드함.
        /// </summary>
        public async UniTask<bool> ParseAndProcessDataAsync(string jsonString)
        {
            try
            {
                ApiTableResponse response = await UniTask.RunOnThreadPool(() => JsonConvert.DeserializeObject<ApiTableResponse>(jsonString));

                if (response != null && response.DATA != null && response.DATA.Count > 0)
                {
                    List<object> firstRow = response.DATA[0];

                    // 컬럼 이름을 키로 하는 인덱스 맵 생성 (파싱 속도 최적화)
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
                    userData.RESERVATION_LAST_NAME_LEFT = ParseStringSafe(colMap, firstRow, "RESERVATION_LAST_NAME_LEFT");
                    userData.RESERVATION_FIRST_NAME_RIGHT = ParseStringSafe(colMap, firstRow, "RESERVATION_FIRST_NAME_RIGHT");
                    userData.RESERVATION_LAST_NAME_RIGHT = ParseStringSafe(colMap, firstRow, "RESERVATION_LAST_NAME_RIGHT");
                    userData.COLOR_LEFT = ParseColorSafe(colMap, firstRow, "COLOR_LEFT");
                    userData.COLOR_RIGHT = ParseColorSafe(colMap, firstRow, "COLOR_RIGHT");

                    if (GameManager.Instance)
                    {   
                        GameManager.Instance.CurrentUserIdx = userData.IDX_USER;
                        GameManager.Instance.Cartridge = userData.CARTRIDGE; 
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

                        if (!string.IsNullOrEmpty(userData.RESERVATION_FIRST_NAME_LEFT))
                            GameManager.Instance.PlayerAName = userData.RESERVATION_FIRST_NAME_LEFT;
                        if (!string.IsNullOrEmpty(userData.RESERVATION_FIRST_NAME_RIGHT))
                            GameManager.Instance.PlayerBName = userData.RESERVATION_FIRST_NAME_RIGHT;
                        
                        GameManager.Instance.PlayerAColor = userData.COLOR_LEFT;
                        GameManager.Instance.PlayerBColor = userData.COLOR_RIGHT;
                        
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
                        
                        GameManager.Instance.PieceA1 = Mathf.Max(0, userData.PIECE_A1);
                        GameManager.Instance.PieceA2 = Mathf.Max(0, userData.PIECE_A2);
                        GameManager.Instance.PieceA3 = Mathf.Max(0, userData.PIECE_A3);
                        GameManager.Instance.PieceB1 = Mathf.Max(0, userData.PIECE_B1);
                        GameManager.Instance.PieceB2 = Mathf.Max(0, userData.PIECE_B2);
                        GameManager.Instance.PieceB3 = Mathf.Max(0, userData.PIECE_B3);
                        GameManager.Instance.PieceC1 = Mathf.Max(0, userData.PIECE_C1);
                        GameManager.Instance.PieceC2 = Mathf.Max(0, userData.PIECE_C2);
                        GameManager.Instance.PieceC3 = Mathf.Max(0, userData.PIECE_C3);
                        GameManager.Instance.PieceD1 = Mathf.Max(0, userData.PIECE_D1);
                        GameManager.Instance.PieceD2 = Mathf.Max(0, userData.PIECE_D2);
                        GameManager.Instance.PieceD3 = Mathf.Max(0, userData.PIECE_D3);

                        // 타 카트리지 클리어 여부 확인 (C2 기준)
                        GameManager.Instance.IsOtherCartridgeContentsCleared = false;
                        if (!string.IsNullOrWhiteSpace(userData.CARTRIDGE))
                        {
                            await CheckOtherCartridgeContentsAsync(userData.CARTRIDGE, response, firstRow);
                        }

                        GameManager.Instance.NotifyUserDataUpdated();
                        return true; 
                    }
                }
                
                Debug.LogWarning("[APIManager] JSON 응답에 데이터가 없거나 형식이 일치하지 않습니다.");
                return false;
            }
            catch (Exception e)
            {
                Debug.LogError($"[APIManager] 파싱 중 에러 발생: {e.Message}");
                return false;
            }
        }

        /// <summary> 카트리지 묶음 콘텐츠의 클리어 상태 추가 조회 </summary>
        private async UniTask CheckOtherCartridgeContentsAsync(string cartridgeStr, ApiTableResponse firstApiResponse, List<object> firstApiRow)
        {
            ApiSettings config = GameManager.Instance ? GameManager.Instance.ApiConfig : null;
            if (config == null) return;

            string url = $"{config.GetCartridgeContentUrl}?cartridge={UnityWebRequest.EscapeURL(cartridgeStr)}";
            
            using (UnityWebRequest req = UnityWebRequest.Get(url))
            {
                req.timeout = 10;
                await req.SendWebRequest().ToUniTask();

                if (req.result == UnityWebRequest.Result.Success)
                {
                    string targetListStr = req.downloadHandler.text;
                    if (GameManager.Instance)
                    {
                        GameManager.Instance.IsOtherCartridgeContentsCleared = ParseOtherCartridgeClearState(targetListStr, firstApiResponse, firstApiRow);
                    }
                }
            }
        }

        private int ParseIntSafe(Dictionary<string, int> map, List<object> row, string col)
        {
            if (map.TryGetValue(col, out int idx) && row.Count > idx && row[idx] != null)
            {
                string valStr = row[idx].ToString().Trim();
                if (string.IsNullOrEmpty(valStr)) return 0;
                
                if (int.TryParse(valStr, out int val)) return val;
                
                if (float.TryParse(valStr, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out float fVal)) 
                    return (int)fVal;
            }
            return 0; 
        }

        private string ParseStringSafe(Dictionary<string, int> map, List<object> row, string col)
        {
            if (map.TryGetValue(col, out int idx) && row.Count > idx && row[idx] != null) 
                return row[idx].ToString();
            return string.Empty; 
        }

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

        /// <summary> 타 모듈 클리어 데이터를 분석하여 카트리지 완성 여부 판단 (C2 기준 제외 처리) </summary>
        private bool ParseOtherCartridgeClearState(string targetListStr, ApiTableResponse resp, List<object> row)
        {
            if (string.IsNullOrWhiteSpace(targetListStr)) return false;

            Dictionary<string, int> map = new Dictionary<string, int>();
            for (int i = 0; i < resp.COLUMNS.Count; i++) map[resp.COLUMNS[i]] = i;

            foreach (string target in targetListStr.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string code = target.Trim().ToUpper(); 
                
                // 현재 콘텐츠(C2)는 자기 자신이므로 평가에서 제외함
                if (code == "C2") continue;
                
                string val = ParseStringSafe(map, row, $"END_{code}");
                if (string.IsNullOrWhiteSpace(val) || val.Equals("null", StringComparison.OrdinalIgnoreCase)) 
                    return false; 
            }
            return true;
        }
    }
}