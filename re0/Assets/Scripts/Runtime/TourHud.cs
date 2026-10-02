using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace CSU.Tour
{
    /// <summary>统一主界面：景点选择、状态、访问计数、小地图、帮助和重置。</summary>
    public sealed class TourHud : MonoBehaviour
    {
        static readonly Color Dark = new Color(0.05f, 0.10f, 0.15f, 0.95f);
        static readonly Color PanelColor = new Color(0.96f, 0.97f, 0.95f, 0.96f);
        static readonly Color Ink = new Color(0.08f, 0.16f, 0.17f, 1f);
        static readonly Color Muted = new Color(0.33f, 0.42f, 0.43f, 1f);
        static readonly Color Mint = new Color(0.18f, 0.58f, 0.48f, 1f);
        static readonly Color Pale = new Color(0.86f, 0.92f, 0.89f, 1f);

        TourApp app;
        TMP_FontAsset font;
        Canvas canvas;
        TextMeshProUGUI progress;
        TextMeshProUGUI statusTitle;
        TextMeshProUGUI statusBody;
        TextMeshProUGUI pauseLabel;
        Button[] destinationButtons;
        TextMeshProUGUI[] destinationNames;
        TourMap map;
        RectTransform help;

        public bool HelpVisible => help != null && help.gameObject.activeSelf;

        public void Initialize(TourApp owner, TMP_FontAsset typeface)
        {
            app = owner;
            font = typeface != null ? typeface : Resources.Load<TMP_FontAsset>("NotoSansSC SDF");
            if (font == null)
            {
                Debug.LogError("[TourHud] 缺少 Noto Sans SC SDF 字体资源。");
                enabled = false;
                return;
            }
            BuildInterface();
            app.StateChanged += Refresh;
            Refresh();
        }

        void BuildInterface()
        {
            var root = new GameObject("Tour interface", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster));
            root.transform.SetParent(transform, false);
            canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1000;
            canvas.pixelPerfect = true;
            CanvasScaler scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1440f, 900f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            if (EventSystem.current == null)
            {
                var eventObject = new GameObject("UI input", typeof(EventSystem), typeof(InputSystemUIInputModule));
                eventObject.transform.SetParent(transform, false);
            }

            RectTransform canvasRect = (RectTransform)root.transform;
            RectTransform title = Panel("Title", canvasRect, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(24f, -24f), new Vector2(300f, 100f), PanelColor);
            Label(title, app.SceneTag, 14, Mint, new Rect(18, 10, 260, 22));
            TextMeshProUGUI heading = Label(title, app.SceneTitle, 27, Ink, new Rect(18, 32, 265, 38));
            heading.fontStyle = FontStyles.Bold;
            Label(title, app.SceneSubtitle, 13, Muted, new Rect(18, 70, 265, 22));

            RectTransform helpButton = Panel("Help button", canvasRect, new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-146f, -24f), new Vector2(110f, 42f), PanelColor);
            MakeButton(helpButton, OpenHelp);
            Label(helpButton, "操作说明 H", 14, Ink, new Rect(0, 0, 110, 42), TextAlignmentOptions.Center);

            RectTransform resetButton = Panel("Reset button", canvasRect, new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-24f, -24f), new Vector2(110f, 42f), PanelColor);
            MakeButton(resetButton, app.ResetTour);
            Label(resetButton, "回到起点 R", 14, Ink, new Rect(0, 0, 110, 42), TextAlignmentOptions.Center);

            RectTransform mapPanel = Panel("Map panel", canvasRect, new Vector2(0f, 0f), new Vector2(0f, 0f),
                new Vector2(24f, 178f), new Vector2(300f, 260f), Dark);
            Label(mapPanel, "导览地图", 18, Color.white, new Rect(16, 10, 200, 28));
            RectTransform mapRect = Rect("Map", mapPanel);
            TopRect(mapRect, new Rect(14, 42, 272, 200));
            map = mapRect.gameObject.AddComponent<TourMap>();
            map.app = app;
            map.color = new Color(0.10f, 0.17f, 0.22f, 1f);

            RectTransform status = Panel("Status", canvasRect, new Vector2(1f, 0f), new Vector2(1f, 0f),
                new Vector2(-24f, 178f), new Vector2(390f, 150f), PanelColor);
            statusTitle = Label(status, "请选择目的地", 20, Ink, new Rect(18, 14, 352, 30));
            statusTitle.fontStyle = FontStyles.Bold;
            statusBody = Label(status, "选择下方景点后，系统会规划路线。", 15, Muted,
                new Rect(18, 48, 352, 74));
            statusBody.textWrappingMode = TextWrappingModes.Normal;
            statusBody.overflowMode = TextOverflowModes.Ellipsis;
            pauseLabel = Label(status, "", 13, Mint, new Rect(18, 122, 352, 22));

            RectTransform tray = StretchPanel("Destinations", canvasRect, new Vector2(0f, 0f), new Vector2(1f, 0f),
                new Vector2(24f, 22f), new Vector2(-24f, 152f), PanelColor);
            Label(tray, "选择游览目的地", 17, Ink, new Rect(18, 10, 230, 24));
            progress = Label(tray, "", 14, Muted, new Rect(250, 10, 260, 24));

            destinationButtons = new Button[app.pois.Length];
            destinationNames = new TextMeshProUGUI[app.pois.Length];
            float availableWidth = 1392f;
            float gap = 12f;
            float buttonWidth = (availableWidth - 36f - gap * Mathf.Max(0, app.pois.Length - 1)) /
                                Mathf.Max(1, app.pois.Length);
            for (int i = 0; i < app.pois.Length; i++)
            {
                int captured = i;
                RectTransform buttonRect = Panel("Destination " + i, tray, new Vector2(0f, 1f),
                    new Vector2(0f, 1f), new Vector2(18f + i * (buttonWidth + gap), -43f),
                    new Vector2(buttonWidth, 70f), Pale);
                destinationButtons[i] = MakeButton(buttonRect, () => app.SelectPoi(captured));
                destinationNames[i] = Label(buttonRect, (i + 1) + "  " + app.pois[i].title,
                    16, Ink, new Rect(14, 8, buttonWidth - 28f, 27));
                Label(buttonRect, app.pois[i].category, 13, Muted,
                    new Rect(14, 37, buttonWidth - 28f, 22));
            }

            BuildHelp(canvasRect);
        }

        void BuildHelp(RectTransform canvasRect)
        {
            help = StretchPanel("Help overlay", canvasRect, Vector2.zero, Vector2.one,
                Vector2.zero, Vector2.zero, new Color(0.02f, 0.05f, 0.07f, 0.78f));
            RectTransform card = Panel("Help card", help, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(520f, 390f), PanelColor);
            TextMeshProUGUI title = Label(card, "操作说明", 26, Ink, new Rect(28, 22, 460, 40));
            title.fontStyle = FontStyles.Bold;
            TextMeshProUGUI body = Label(card,
                "W / A / S / D　移动\n" +
                "按住鼠标右键　环顾四周\n" +
                "Shift　加速\n" +
                "数字 1 / 2 / 3　选择目的地\n" +
                "R　回到起点并重置本轮记录\n" +
                "Esc　暂停或继续\n" +
                "H　打开或关闭帮助\n" +
                "B　显示观察区域与到达区域\n\n" +
                "路线每 0.75 秒检查一次障碍并重新规划。",
                17, Muted, new Rect(30, 75, 460, 235));
            body.textWrappingMode = TextWrappingModes.Normal;
            body.overflowMode = TextOverflowModes.Overflow;

            RectTransform close = Panel("Continue", card, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 24f), new Vector2(450f, 48f), Mint);
            MakeButton(close, CloseHelp);
            Label(close, "继续探索", 17, Color.white, new Rect(0, 0, 450, 48), TextAlignmentOptions.Center);
            help.gameObject.SetActive(false);
        }

        public void OpenHelp()
        {
            if (help == null) return;
            help.gameObject.SetActive(true);
            app.SetPaused(true);
        }

        public void CloseHelp()
        {
            if (help == null) return;
            help.gameObject.SetActive(false);
            app.SetPaused(false);
        }

        void LateUpdate()
        {
            Refresh();
        }

        void Refresh()
        {
            if (app == null || app.Visited == null || progress == null) return;
            int visited = 0;
            foreach (bool value in app.Visited) if (value) visited++;
            progress.text = "已访问 " + visited + " / " + app.pois.Length;

            for (int i = 0; i < destinationButtons.Length; i++)
            {
                bool selected = app.Selected == i;
                ColorBlock colors = destinationButtons[i].colors;
                colors.normalColor = selected ? Mint : Pale;
                colors.highlightedColor = selected ? new Color(0.24f, 0.68f, 0.57f) : Color.white;
                colors.selectedColor = colors.normalColor;
                destinationButtons[i].colors = colors;
                destinationNames[i].color = selected ? Color.white : Ink;
            }

            if (app.paused)
            {
                statusTitle.text = "已暂停";
                statusBody.text = HelpVisible ? "查看操作说明，点击“继续探索”返回。" : "按 Esc 继续探索。";
                pauseLabel.text = "移动、视角与路线更新已暂停";
            }
            else if (app.Selected < 0)
            {
                statusTitle.text = "请选择目的地";
                statusBody.text = "选择下方景点后，系统会规划最短可行路线。";
                pauseLabel.text = "H 帮助　B 显示景点区域";
            }
            else if (app.Arrived)
            {
                statusTitle.text = "已到达 · " + app.pois[app.Selected].title;
                statusBody.text = app.pois[app.Selected].description;
                pauseLabel.text = "本次到达已计入访问数量";
            }
            else
            {
                statusTitle.text = "正在前往 · " + app.pois[app.Selected].title;
                statusBody.text = app.Route.Count == 0
                    ? "当前没有可行路线，请返回道路或按 R 重置。"
                    : "沿高亮路线剩余约 " + app.RemainingDistance.ToString("0.0") + " 场景单位。";
                pauseLabel.text = "路线修订 " + app.RouteRevision + "　移动 " + app.TravelDistance.ToString("0.0");
            }

            map?.SetVerticesDirty();
        }

        RectTransform Rect(string name, Transform parent)
        {
            var gameObject = new GameObject(name, typeof(RectTransform));
            gameObject.transform.SetParent(parent, false);
            return (RectTransform)gameObject.transform;
        }

        RectTransform Panel(string name, Transform parent, Vector2 anchor, Vector2 pivot,
            Vector2 position, Vector2 size, Color color)
        {
            RectTransform rect = Rect(name, parent);
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            Image image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            return rect;
        }

        RectTransform StretchPanel(string name, Transform parent, Vector2 min, Vector2 max,
            Vector2 offsetMin, Vector2 offsetMax, Color color)
        {
            RectTransform rect = Panel(name, parent, min, new Vector2(0.5f, 0.5f),
                Vector2.zero, Vector2.zero, color);
            rect.anchorMax = max;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
            return rect;
        }

        static void TopRect(RectTransform rect, Rect box)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(box.x, -box.y);
            rect.sizeDelta = box.size;
        }

        TextMeshProUGUI Label(Transform parent, string value, int size, Color color, Rect box,
            TextAlignmentOptions alignment = TextAlignmentOptions.MidlineLeft)
        {
            RectTransform rect = Rect("Text", parent);
            TopRect(rect, box);
            TextMeshProUGUI label = rect.gameObject.AddComponent<TextMeshProUGUI>();
            label.font = font;
            label.text = value;
            label.fontSize = size;
            label.color = color;
            label.alignment = alignment;
            label.raycastTarget = false;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Overflow;
            label.extraPadding = true;
            return label;
        }

        Button MakeButton(RectTransform rect, UnityEngine.Events.UnityAction action)
        {
            Image image = rect.GetComponent<Image>();
            Button button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            button.onClick.AddListener(action);
            return button;
        }

        void OnDestroy()
        {
            if (app != null) app.StateChanged -= Refresh;
        }
    }
}
