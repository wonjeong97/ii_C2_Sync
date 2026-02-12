namespace My.Scripts.Global
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
        }

        /// <summary> 리소스 경로 상수 모음 </summary>
        public static class Path
        {
            public const string JsonSetting = "Settings"; 
            public const string Tutorial = "JSON/Tutorial";
            public const string PlayTutorial = "JSON/PlayTutorial";
            public const string PlayShort = "JSON/PlayShort";
            public const string PlayLong = "JSON/PlayLong";
        }
    }
}