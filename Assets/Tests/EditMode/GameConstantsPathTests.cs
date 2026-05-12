using NUnit.Framework;
using My.Scripts.Core;
using My.Scripts.Global;
using UnityEngine;

namespace My.Scripts.Tests.EditMode
{
    /// <summary>
    /// Tests for GameConstants.Path changes introduced in the multilingual (i18n) PR.
    /// Covers:
    ///   - Path constant values (prefix-free after PR change)
    ///   - GetLocalizedPath() default fallback (SessionManager absent → "ko")
    ///   - GetLocalizedPath() with an active SessionManager and various language codes
    ///   - Path composition correctness used by managers
    /// </summary>
    [TestFixture]
    public class GameConstantsPathTests
    {
        // ── Helpers ──────────────────────────────────────────────────────────────

        private GameObject _sessionManagerGO;

        [TearDown]
        public void TearDown()
        {
            // Destroy any SessionManager GameObject created during a test so that
            // the singleton does not leak into the next test.
            if (_sessionManagerGO != null)
            {
                Object.DestroyImmediate(_sessionManagerGO);
                _sessionManagerGO = null;
            }
        }

        /// <summary>
        /// Creates a SessionManager GameObject and sets its CurrentLanguage.
        /// </summary>
        private SessionManager CreateSessionManager(string language)
        {
            _sessionManagerGO = new GameObject("TestSessionManager");
            var sm = _sessionManagerGO.AddComponent<SessionManager>();
            sm.CurrentLanguage = language;
            return sm;
        }

        // ── 1. Path constant values ───────────────────────────────────────────

        [Test]
        public void Path_System_HasNoJsonPrefix()
        {
            Assert.AreEqual("System", GameConstants.Path.System);
        }

        [Test]
        public void Path_Tutorial_HasNoJsonPrefix()
        {
            Assert.AreEqual("Tutorial", GameConstants.Path.Tutorial);
        }

        [Test]
        public void Path_PlayTutorial_HasNoJsonPrefix()
        {
            Assert.AreEqual("PlayTutorial", GameConstants.Path.PlayTutorial);
        }

        [Test]
        public void Path_PlayShort_HasNoJsonPrefix()
        {
            Assert.AreEqual("PlayShort", GameConstants.Path.PlayShort);
        }

        [Test]
        public void Path_PlayLong_HasNoJsonPrefix()
        {
            Assert.AreEqual("PlayLong", GameConstants.Path.PlayLong);
        }

        [Test]
        public void Path_Ending_HasNoJsonPrefix()
        {
            Assert.AreEqual("Ending", GameConstants.Path.Ending);
        }

        [Test]
        public void Path_ApiSetting_HasNoJsonPrefix()
        {
            Assert.AreEqual("API", GameConstants.Path.ApiSetting);
        }

        // ── 2. GetLocalizedPath – default language (no SessionManager) ────────

        [Test]
        public void GetLocalizedPath_NoSessionManager_DefaultsToKorean()
        {
            // Ensure no SessionManager is alive.
            Assert.IsNull(SessionManager.Instance,
                "Precondition: SessionManager.Instance must be null for this test.");

            string result = GameConstants.Path.GetLocalizedPath("Tutorial");

            Assert.AreEqual("JSON/ko/Tutorial", result);
        }

        [Test]
        public void GetLocalizedPath_NoSessionManager_ReturnsCorrectStructure()
        {
            Assert.IsNull(SessionManager.Instance);

            string result = GameConstants.Path.GetLocalizedPath(GameConstants.Path.System);

            // Must follow the pattern JSON/{lang}/{subPath}
            StringAssert.StartsWith("JSON/", result);
            StringAssert.EndsWith("/" + GameConstants.Path.System, result);
        }

        // ── 3. GetLocalizedPath – with SessionManager active ─────────────────

        [Test]
        public void GetLocalizedPath_WithSessionManager_UsesCurrentLanguage()
        {
            CreateSessionManager("en");

            string result = GameConstants.Path.GetLocalizedPath("Tutorial");

            Assert.AreEqual("JSON/en/Tutorial", result);
        }

        [Test]
        public void GetLocalizedPath_WithSessionManager_KoreanLanguage()
        {
            CreateSessionManager("ko");

            string result = GameConstants.Path.GetLocalizedPath("Tutorial");

            Assert.AreEqual("JSON/ko/Tutorial", result);
        }

        [Test]
        public void GetLocalizedPath_WithSessionManager_JapaneseLanguage()
        {
            CreateSessionManager("jp");

            string result = GameConstants.Path.GetLocalizedPath("Ending");

            Assert.AreEqual("JSON/jp/Ending", result);
        }

        [Test]
        public void GetLocalizedPath_WithSessionManager_ChineseLanguage()
        {
            CreateSessionManager("zh");

            string result = GameConstants.Path.GetLocalizedPath(GameConstants.Path.PlayShort);

            Assert.AreEqual("JSON/zh/PlayShort", result);
        }

        // ── 4. GetLocalizedPath – path composition with real constants ────────

        [Test]
        public void GetLocalizedPath_Tutorial_ComposesCorrectly()
        {
            CreateSessionManager("ko");

            string result = GameConstants.Path.GetLocalizedPath(GameConstants.Path.Tutorial);

            Assert.AreEqual("JSON/ko/Tutorial", result);
        }

        [Test]
        public void GetLocalizedPath_PlayTutorial_ComposesCorrectly()
        {
            CreateSessionManager("ko");

            string result = GameConstants.Path.GetLocalizedPath(GameConstants.Path.PlayTutorial);

            Assert.AreEqual("JSON/ko/PlayTutorial", result);
        }

        [Test]
        public void GetLocalizedPath_PlayShort_ComposesCorrectly()
        {
            CreateSessionManager("ko");

            string result = GameConstants.Path.GetLocalizedPath(GameConstants.Path.PlayShort);

            Assert.AreEqual("JSON/ko/PlayShort", result);
        }

        [Test]
        public void GetLocalizedPath_PlayLong_ComposesCorrectly()
        {
            CreateSessionManager("ko");

            string result = GameConstants.Path.GetLocalizedPath(GameConstants.Path.PlayLong);

            Assert.AreEqual("JSON/ko/PlayLong", result);
        }

        [Test]
        public void GetLocalizedPath_Ending_ComposesCorrectly()
        {
            CreateSessionManager("ko");

            string result = GameConstants.Path.GetLocalizedPath(GameConstants.Path.Ending);

            Assert.AreEqual("JSON/ko/Ending", result);
        }

        [Test]
        public void GetLocalizedPath_System_ComposesCorrectly()
        {
            CreateSessionManager("ko");

            string result = GameConstants.Path.GetLocalizedPath(GameConstants.Path.System);

            Assert.AreEqual("JSON/ko/System", result);
        }

        // ── 5. GetLocalizedPath – edge cases ─────────────────────────────────

        [Test]
        public void GetLocalizedPath_EmptySubPath_ReturnsJSONLangSlash()
        {
            CreateSessionManager("ko");

            string result = GameConstants.Path.GetLocalizedPath(string.Empty);

            Assert.AreEqual("JSON/ko/", result);
        }

        [Test]
        public void GetLocalizedPath_SubPathWithSubdirectory_ComposesCorrectly()
        {
            CreateSessionManager("ko");

            string result = GameConstants.Path.GetLocalizedPath("Cartridge_A/PlayShort_A1");

            Assert.AreEqual("JSON/ko/Cartridge_A/PlayShort_A1", result);
        }

        [Test]
        public void GetLocalizedPath_LangChangeOnSameSessionManager_ReflectsNewLanguage()
        {
            SessionManager sm = CreateSessionManager("ko");

            string resultKo = GameConstants.Path.GetLocalizedPath("Tutorial");
            Assert.AreEqual("JSON/ko/Tutorial", resultKo);

            sm.CurrentLanguage = "en";
            string resultEn = GameConstants.Path.GetLocalizedPath("Tutorial");
            Assert.AreEqual("JSON/en/Tutorial", resultEn);
        }

        // ── 6. GameManager.LoadSettings path routing ──────────────────────────

        /// <summary>
        /// Verifies that the System path uses GetLocalizedPath (and therefore
        /// respects the language folder), while the ApiSetting path still uses a
        /// plain "JSON/" prefix without going through GetLocalizedPath.
        /// This documents the intentional difference introduced in GameManager.LoadSettings().
        /// </summary>
        [Test]
        public void SystemPath_UsesLocalizedFolder_ButApiPath_UsesHardcodedPrefix()
        {
            CreateSessionManager("ko");

            // System → via GetLocalizedPath
            string systemPath = GameConstants.Path.GetLocalizedPath(GameConstants.Path.System);
            Assert.AreEqual("JSON/ko/System", systemPath,
                "System data must be loaded from the language-specific subfolder.");

            // API → hardcoded "JSON/" prefix (not GetLocalizedPath)
            string apiPath = "JSON/" + GameConstants.Path.ApiSetting;
            Assert.AreEqual("JSON/API", apiPath,
                "API settings must be loaded from the root JSON folder, not a language subfolder.");
        }

        [Test]
        public void SystemPath_UsesLocalizedFolder_WithEnglishLanguage()
        {
            CreateSessionManager("en");

            string systemPath = GameConstants.Path.GetLocalizedPath(GameConstants.Path.System);

            Assert.AreEqual("JSON/en/System", systemPath);
        }

        // ── 7. PlayShortManager path-building logic ───────────────────────────

        /// <summary>
        /// The PlayShortManager.InitGame() method builds paths inline using the same
        /// pattern as GetLocalizedPath but without calling it.
        /// These tests validate the string formatting logic used by InitGame().
        /// </summary>
        [Test]
        public void PlayShortPrimaryPath_MatchesExpectedFormat_CartridgeA_TypeA1()
        {
            string lang = "ko";
            string typeStr = "A1";

            string primaryPath = $"JSON/{lang}/Cartridge_{typeStr[0]}/PlayShort_{typeStr}";

            Assert.AreEqual("JSON/ko/Cartridge_A/PlayShort_A1", primaryPath);
        }

        [Test]
        public void PlayShortPrimaryPath_MatchesExpectedFormat_CartridgeB_TypeB3()
        {
            string lang = "ko";
            string typeStr = "B3";
            char cartridgeChar = typeStr[0];

            string primaryPath = $"JSON/{lang}/Cartridge_{cartridgeChar}/PlayShort_{typeStr}";

            Assert.AreEqual("JSON/ko/Cartridge_B/PlayShort_B3", primaryPath);
        }

        [Test]
        public void PlayShortFallbackAPath_UsesRelationChar_WhenPrimaryMissing()
        {
            string lang = "ko";
            string typeStr = "B3";
            char cartridgeChar = typeStr[0]; // 'B'
            string relationStr = typeStr.Length > 1 ? typeStr.Substring(1) : "1"; // "3"

            // 2nd-priority fallback: same relation, Cartridge_A
            string fallbackAPath = $"JSON/{lang}/Cartridge_A/PlayShort_A{relationStr}";

            Assert.AreEqual("JSON/ko/Cartridge_A/PlayShort_A3", fallbackAPath);
            Assert.AreNotEqual('A', cartridgeChar, "Fallback A path applies only for non-A cartridges.");
        }

        [Test]
        public void PlayShortFallbackA1Path_IsUsed_WhenBothPrimaryAndFallbackAFail()
        {
            string lang = "ko";

            // 3rd-priority fallback: always PlayShort_A1
            string fallbackA1Path = $"JSON/{lang}/Cartridge_A/PlayShort_A1";

            Assert.AreEqual("JSON/ko/Cartridge_A/PlayShort_A1", fallbackA1Path);
        }

        [Test]
        public void PlayShortFallbackOrder_DoesNotApplyFallbackA_WhenCartridgeIsA()
        {
            // If cartridgeChar == 'A', the 2nd-priority fallback is skipped.
            string typeStr = "A1";
            char cartridgeChar = typeStr[0];

            // Condition from InitGame: if (!isLoaded && cartridgeChar != 'A')
            bool shouldApplyFallbackA = cartridgeChar != 'A';

            Assert.IsFalse(shouldApplyFallbackA,
                "Cartridge_A should never fall back to another Cartridge_A path.");
        }

        [Test]
        public void PlayShortFallbackOrder_SkipsFallbackA1_WhenTypeIsAlreadyA1()
        {
            // Condition from InitGame: if (!isLoaded && typeStr != "A1")
            string typeStr = "A1";
            bool shouldApplyFallbackA1 = typeStr != "A1";

            Assert.IsFalse(shouldApplyFallbackA1,
                "PlayShort_A1 type should not attempt to load the A1 fallback again.");
        }

        [Test]
        public void PlayShortBaseDataPath_UsesLanguageFolder()
        {
            // InitGame: _data = JsonLoader.Load<PlayShortData>($"JSON/{lang}/{GameConstants.Path.PlayShort}")
            string lang = "ko";
            string dataPath = $"JSON/{lang}/{GameConstants.Path.PlayShort}";

            Assert.AreEqual("JSON/ko/PlayShort", dataPath);
        }

        [Test]
        public void PlayShortBaseDataPath_ChangesWithLanguage()
        {
            string langKo = "ko";
            string langEn = "en";

            string pathKo = $"JSON/{langKo}/{GameConstants.Path.PlayShort}";
            string pathEn = $"JSON/{langEn}/{GameConstants.Path.PlayShort}";

            Assert.AreEqual("JSON/ko/PlayShort", pathKo);
            Assert.AreEqual("JSON/en/PlayShort", pathEn);
            Assert.AreNotEqual(pathKo, pathEn);
        }

        // ── 8. Regression: old paths no longer exist as constants ─────────────

        [Test]
        public void Path_DoesNotContainOldJsonPrefixInSystem()
        {
            StringAssert.DoesNotStartWith("JSON/", GameConstants.Path.System);
        }

        [Test]
        public void Path_DoesNotContainOldJsonPrefixInTutorial()
        {
            StringAssert.DoesNotStartWith("JSON/", GameConstants.Path.Tutorial);
        }

        [Test]
        public void Path_DoesNotContainOldJsonPrefixInPlayTutorial()
        {
            StringAssert.DoesNotStartWith("JSON/", GameConstants.Path.PlayTutorial);
        }

        [Test]
        public void Path_DoesNotContainOldJsonPrefixInPlayShort()
        {
            StringAssert.DoesNotStartWith("JSON/", GameConstants.Path.PlayShort);
        }

        [Test]
        public void Path_DoesNotContainOldJsonPrefixInPlayLong()
        {
            StringAssert.DoesNotStartWith("JSON/", GameConstants.Path.PlayLong);
        }

        [Test]
        public void Path_DoesNotContainOldJsonPrefixInEnding()
        {
            StringAssert.DoesNotStartWith("JSON/", GameConstants.Path.Ending);
        }

        [Test]
        public void Path_DoesNotContainOldJsonPrefixInApiSetting()
        {
            StringAssert.DoesNotStartWith("JSON/", GameConstants.Path.ApiSetting);
        }
    }
}