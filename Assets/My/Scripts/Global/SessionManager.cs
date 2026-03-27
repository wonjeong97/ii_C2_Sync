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
    /// 현재 플레이 중인 사용자의 세션 데이터(이름, UID, 점수 등)를 전담하여 보관하는 매니저입니다.
    /// GameManager에서 분리되어 단일 책임 원칙(SRP)을 준수합니다.
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
        public string CurrentModuleCode { get; set; } = GameConstants.Module.Code; // "C2"
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
        
        public int TotalPieces
        {
            get
            {
                if (string.IsNullOrWhiteSpace(BlockCode)) 
                {
                    return PieceA1 + PieceA2 + PieceA3 +
                           PieceB1 + PieceB2 + PieceB3 +
                           PieceC1 + PieceC3 +
                           PieceD1 + PieceD2 + PieceD3;
                }

                
                int sum = 0;
                string[] blocks = BlockCode.Split(',');
                string currentCode = CurrentModuleCode.ToUpper();

                foreach (string b in blocks)
                {
                    string block = b.Trim().ToUpper();
                    
                    // 이유: 현재 콘텐츠(C2)의 조각은 게임 종료 시점에 별도로 합산되므로 기존 누적치에서는 제외함.
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

        private void Awake()
        {
            if (!Instance)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }

        public void ClearSession()
        {
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