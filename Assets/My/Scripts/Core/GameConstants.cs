namespace My.Scripts.Core
{
    /// <summary> 게임 전역 상수 관리 클래스 </summary>
    public static class GameConstants
    {
        /// <summary> 씬 이름 상수 모음 </summary>
        public static class Scene
        { 
            public const string Title = "00_Title";
            public const string Tutorial = "01_Tutorial";
            public const string PlayTutorial = "02_PlayTutorial";
            public const string PlayShort = "03_PlayShort";
            public const string PlayLong = "04_PlayLong";
            public const string Ending = "05_Ending";
        }

        /// <summary> 리소스 경로 상수 모음 </summary>
        public static class Path
        {
            public const string System = "System";
            public const string JsonSetting = "Settings"; 
            public const string Tutorial = "Tutorial";
            public const string PlayTutorial = "PlayTutorial";
            public const string PlayShort = "PlayShort";
            public const string PlayLong = "PlayLong";
            public const string Ending = "Ending";
            public const string ApiSetting = "API";
            
            /// <summary>
            /// 현재 세션의 언어 설정을 반영하여 최종 리소스 경로를 반환함.
            /// </summary>
            /// <param name="subPath">데이터 파일 이름 또는 하위 경로</param>
            /// <returns>JSON/ko/파일이름 형태의 경로</returns>
            public static string GetLocalizedPath(string subPath)
            {
                string lang = "ko";
                if (Global.SessionManager.Instance)
                {
                    string currentLanguage = Global.SessionManager.Instance.CurrentLanguage;
                    if (!string.IsNullOrWhiteSpace(currentLanguage))
                    {
                        lang = currentLanguage;
                    }
                }
                return $"JSON/{lang}/{subPath}";
            }
        }
        
        /// <summary> 모듈 및 레벨 상수 모음 </summary>
        public static class Module
        {
            public const string Code = "C2"; 
        }

        /// <summary> API 상태 상수 모음 </summary>
        public static class Api
        {
            public const string StatusEmpty = "EMPTY";
            public const string StatusUsing = "USING";
        }
    }
}