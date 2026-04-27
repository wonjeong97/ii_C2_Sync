using System;
using My.Scripts.Core;
using My.Scripts.Global;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace My.Scripts._03_PlayShort
{
    [DefaultExecutionOrder(-200)]
    public class PlayShortDebugStarter : MonoBehaviour
    {
        public static bool IsReady { get; private set; } = true;

        private static readonly UserType[] Types =
        {
            UserType.A1, UserType.A2, UserType.A3, UserType.A4, UserType.A5,
            UserType.B1, UserType.B2, UserType.B3, UserType.B4, UserType.B5,
        };

        private void Awake()
        {
            if (GameManager.Instance != null)
            {
                IsReady = true;
                Destroy(gameObject);
                return;
            }

            IsReady = false;

            if (!SessionManager.Instance)
            {
                var sessionObj = new GameObject("[Debug] SessionManager");
                sessionObj.AddComponent<SessionManager>();
            }

            var gmObj = new GameObject("[Debug] GameManager");
            var gm = gmObj.AddComponent<GameManager>();
            gm.forceUserType = true;
            gm.debugUserType = UserType.A1;
        }

        private void Start()
        {
            if (IsReady) return;
            BuildUI();
        }

        private void BuildUI()
        {
            var canvasObj = new GameObject("[Debug] TypeSelectCanvas");
            var canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 999;

            var scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            canvasObj.AddComponent<GraphicRaycaster>();

            var panel = MakePanel(canvasObj.transform, new Color(0f, 0f, 0f, 0.88f));

            MakeLabel(panel.transform, "PlayShort 테스트 — 타입 선택", 40,
                new Vector2(0f, 200f), new Vector2(800f, 60f));

            string[][] rows = { new[] { "A1", "A2", "A3", "A4", "A5" },
                                 new[] { "B1", "B2", "B3", "B4", "B5" } };
            float[] rowY = { 60f, -60f };

            for (int r = 0; r < rows.Length; r++)
            {
                for (int c = 0; c < rows[r].Length; c++)
                {
                    string name = rows[r][c];
                    UserType type = (UserType)Enum.Parse(typeof(UserType), name);
                    float x = (c - 2) * 170f;
                    var captured = (type, canvasObj);
                    MakeButton(panel.transform, name, x, rowY[r],
                        new Vector2(150f, 80f), () => OnSelect(captured.type, captured.canvasObj));
                }
            }
        }

        private void OnSelect(UserType type, GameObject canvasObj)
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.debugUserType = type;
                GameManager.Instance.forceUserType = true;
            }

            Destroy(canvasObj);
            IsReady = true;
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }

        // ── UI helpers ──────────────────────────────────────────────────────

        private static GameObject MakePanel(Transform parent, Color color)
        {
            var obj = new GameObject("Panel");
            obj.transform.SetParent(parent, false);
            var img = obj.AddComponent<Image>();
            img.color = color;
            var rt = obj.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            return obj;
        }

        private static void MakeLabel(Transform parent, string text, int fontSize,
            Vector2 pos, Vector2 size)
        {
            var obj = new GameObject("Label");
            obj.transform.SetParent(parent, false);
            var t = obj.AddComponent<Text>();
            t.text = text;
            t.fontSize = fontSize;
            t.color = Color.white;
            t.alignment = TextAnchor.MiddleCenter;
            t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var rt = obj.GetComponent<RectTransform>();
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
        }

        private static void MakeButton(Transform parent, string label,
            float x, float y, Vector2 size, Action onClick)
        {
            var obj = new GameObject($"Btn_{label}");
            obj.transform.SetParent(parent, false);

            var img = obj.AddComponent<Image>();
            img.color = new Color(0.18f, 0.45f, 0.9f);

            var btn = obj.AddComponent<Button>();
            var colors = btn.colors;
            colors.highlightedColor = new Color(0.3f, 0.6f, 1f);
            colors.pressedColor = new Color(0.1f, 0.3f, 0.7f);
            btn.colors = colors;
            btn.onClick.AddListener(() => onClick());

            var rt = obj.GetComponent<RectTransform>();
            rt.anchoredPosition = new Vector2(x, y);
            rt.sizeDelta = size;

            MakeLabel(obj.transform, label, 34, Vector2.zero, size);
        }
    }
}
