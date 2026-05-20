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
            /// 파일 성격에 따라 전역 JSON 또는 언어별 JSON 경로를 반환합니다.
            /// 싱글톤 제거에 따라 현재 세션의 언어 코드(lang)를 외부에서 주입받도록 개편되었습니다.
            /// </summary>
            public static string GetLocalizedPath(string fileName, string lang = "ko")
            {
                // 1. 전역 설정 파일(API)은 언어 폴더를 거치지 않고 JSON 폴더에서 직접 참조
                if (fileName == ApiSetting)
                {
                    return $"JSON/{fileName}.json";
                }

                // 2. Settings.json은 루트 폴더에 있으므로 그대로 반환
                if (fileName == JsonSetting) return $"{fileName}.json";

                // 3. 그 외 나머지는 현재 주입된 언어 폴더(ko/en/jp) 경로 반환
                return $"JSON/{lang}/{fileName}.json";
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