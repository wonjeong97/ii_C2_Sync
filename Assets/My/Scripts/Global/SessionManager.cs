using My.Scripts.Core;
using UnityEngine;

namespace My.Scripts.Global
{
    public enum UserType
    {
        A1, A2, A3, A4, A5, A6,
        B1, B2, B3, B4, B5, B6,
        C1, C2, C3, C4, C5, C6,
        D1, D2, D3, D4, D5, D6
    }

    /// <summary>
    /// 현재 플레이 중인 사용자의 세션 데이터(이름, UID, 점수 등)를 전담하여 보관하는 매니저 클래스.
    /// GameManager에서 분리되어 단일 책임 원칙(SRP)을 준수함.
    /// </summary>
    public class SessionManager : MonoBehaviour
    {
        public static SessionManager Instance { get; private set; }

        public int CurrentUserId { get; set; } 
        public string PlayerAUid { get; set; } = string.Empty;
        public string PlayerBUid { get; set; } = string.Empty;
        public string CurrentLanguage { get; set; } = "ko";
        
        public string PlayerAFirstName { get; set; } = "Player A";
        public string PlayerBFirstName { get; set; } = "Player B";
        
        public ColorData PlayerAColor { get; set; } = ColorData.NotSet;
        public ColorData PlayerBColor { get; set; } = ColorData.NotSet;

        public UserType CurrentUserType { get; set; } = UserType.A1;
        public string CurrentModuleCode { get; set; } = GameConstants.Module.Code; 
        public string Cartridge { get; set; } = string.Empty;
        public string BlockCode { get; set; } = string.Empty;
        
        public bool IsOtherCartridgeContentsCleared { get; set; } = false;
        public int ClearedEndCount { get; set; } = 0; 

        public int PieceA1 { get; set; } 
        public int PieceA2 { get; set; }
        public int PieceA3 { get; set; }
        public int PieceB1 { get; set; }
        public int PieceB2 { get; set; }
        public int PieceB3 { get; set; }
        public int PieceC1 { get; set; }
        public int PieceC2 { get; set; }
        public int PieceC3 { get; set; }
        public int PieceD1 { get; set; }
        public int PieceD2 { get; set; }
        public int PieceD3 { get; set; }
        
        /// <summary>
        /// 할당된 블록 코드 리스트를 분석하여 현재 모듈을 제외한 나머지 모듈의 마음 조각 총합을 계산함.
        /// </summary>
        public int TotalPieces
        {
            get
            {
                // 이유: 할당된 블록 정보가 없는 신규 유저의 경우 모든 조각의 단순 합계를 반환함.
                if (string.IsNullOrWhiteSpace(BlockCode) ||
                    string.Equals(BlockCode.Trim(), "null", System.StringComparison.OrdinalIgnoreCase)) 
                {
                    return PieceA1 + PieceA2 + PieceA3 +
                           PieceB1 + PieceB2 + PieceB3 +
                           PieceC1 + PieceC3 +
                           PieceD1 + PieceD2 + PieceD3;
                }

                int sum = 0;
                string[] blocks = BlockCode.Split(',');
                string currentCode = CurrentModuleCode.ToUpper();

                // # TODO: 반복적인 문자열 비교 및 Switch 연산 비용 절감을 위해 Dictionary 기반 점수 매핑 구조로 개선 필요.
                // 예시 입력: BlockCode="A1,B2,C2", CurrentModuleCode="C2", PieceA1=5, PieceB2=10, PieceC2=3 -> 결과값 = 15 (C2 제외)
                foreach (string b in blocks)
                {
                    string block = b.Trim().ToUpper();
                    
                    // 이유: 현재 플레이 중인 모듈의 조각은 게임 결과 정산 시점에 합산되므로 기존 누적치 계산에서는 제외함.
                    if (block == currentCode)
                    {
                        continue;
                    }

                    switch (block)
                    {
                        case "A1": sum += PieceA1; break;
                        case "A2": sum += PieceA2; break;
                        case "A3": sum += PieceA3; break;
                        case "B1": sum += PieceB1; break;
                        case "B2": sum += PieceB2; break;
                        case "B3": sum += PieceB3; break;
                        case "C1": sum += PieceC1; break;
                        case "C2": sum += PieceC2; break;
                        case "C3": sum += PieceC3; break;
                        case "D1": sum += PieceD1; break;
                        case "D2": sum += PieceD2; break;
                        case "D3": sum += PieceD3; break;
                    }
                }
                return sum;
            }
        }

        /// <summary>
        /// 객체 생성 시 싱글톤 인스턴스를 할당하고 씬 전환 시 파괴되지 않도록 설정함.
        /// </summary>
        private void Awake()
        {
            if (!Instance)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                // 이유: 중복 생성된 세션 매니저를 제거하여 데이터 무결성을 유지함.
                Destroy(gameObject);
            }
        }

        /// <summary>
        /// 현재 세션의 모든 유저 관련 데이터를 초기화함.
        /// </summary>
        public void ClearSession()
        {
            // 이유: 새로운 사용자가 진입할 때 이전 사용자의 데이터가 잔존하여 발생하는 오류를 방지함.
            CurrentUserId = 0;
            PlayerAUid = string.Empty;
            PlayerBUid = string.Empty;
            BlockCode = string.Empty;
            CurrentLanguage = "ko";
            
            PlayerAFirstName = "Player A";
            PlayerBFirstName = "Player B";
            
            PlayerAColor = ColorData.NotSet;
            PlayerBColor = ColorData.NotSet;

            CurrentUserType = UserType.A1;
            CurrentModuleCode = GameConstants.Module.Code;
            Cartridge = string.Empty;
            
            IsOtherCartridgeContentsCleared = false;
            ClearedEndCount = 0; 

            PieceA1 = 0; PieceA2 = 0; PieceA3 = 0;
            PieceB1 = 0; PieceB2 = 0; PieceB3 = 0;
            PieceC1 = 0; PieceC2 = 0; PieceC3 = 0;
            PieceD1 = 0; PieceD2 = 0; PieceD3 = 0;
        }
    }
}