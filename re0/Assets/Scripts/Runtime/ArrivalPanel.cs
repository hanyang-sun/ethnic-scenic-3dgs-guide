using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 原型零件 7｜到达提示   —— 归成员 B
/// 挂到一个空物体上。走到目的地附近时，屏幕中间弹出景点介绍。
///
/// 到达判定用的是"水平距离"，理由和 DistanceHUD 一样：
/// 景点位置在地板高度，相机在眼睛高度，三维距离会虚高。
/// </summary>
public class ArrivalPanel : MonoBehaviour
{
    [Header("触发")]
    [Tooltip("水平距离小于这个值就弹出")]
    public float showDistance = 2.0f;

    [Tooltip("离开超过这个距离才收起，避免在边界反复开关")]
    public float hideDistance = 3.0f;

    [Header("面板尺寸")]
    public float panelWidth = 520f;
    public float panelHeight = 210f;

    [Header("外观")]
    public Color panelColor = new Color(1f, 1f, 1f, 0.95f);
    public Color titleColor = new Color(0.12f, 0.22f, 0.39f, 1f);
    public Color bodyColor = new Color(0.15f, 0.15f, 0.15f, 1f);

    GameObject panel;
    TextMeshProUGUI title;
    TextMeshProUGUI body;
    Camera cam;
    int activeIndex = -1;
    int dismissedIndex = -1;

    void Start()
    {
        if (FindFirstObjectByType<EventSystem>() == null)
        {
            GameObject events = new GameObject("EventSystem");
            events.AddComponent<EventSystem>();
            events.AddComponent<StandaloneInputModule>();
        }
        BuildUI();
        if (panel != null) panel.SetActive(false);
    }

    void BuildUI()
    {
        GameObject canvasGo = new GameObject("ArrivalCanvas");
        Canvas c = canvasGo.AddComponent<Canvas>();
        c.renderMode = RenderMode.ScreenSpaceOverlay;
        c.sortingOrder = 30;
        CanvasScaler s = canvasGo.AddComponent<CanvasScaler>();
        s.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        s.referenceResolution = new Vector2(1920f, 1080f);
        canvasGo.AddComponent<GraphicRaycaster>();

        panel = new GameObject("Panel");
        panel.transform.SetParent(canvasGo.transform, false);
        RectTransform prt = panel.AddComponent<RectTransform>();
        prt.anchorMin = new Vector2(0.5f, 0.5f);
        prt.anchorMax = new Vector2(0.5f, 0.5f);
        prt.pivot = new Vector2(0.5f, 0.5f);
        prt.sizeDelta = new Vector2(panelWidth, panelHeight);
        prt.anchoredPosition = Vector2.zero;

        Image bg = panel.AddComponent<Image>();
        bg.color = panelColor;
        bg.raycastTarget = false;

        // 标题：贴在面板顶部，高度固定
        GameObject titleGo = new GameObject("Title");
        titleGo.transform.SetParent(panel.transform, false);
        RectTransform trt = titleGo.AddComponent<RectTransform>();
        trt.anchorMin = new Vector2(0f, 1f);
        trt.anchorMax = new Vector2(1f, 1f);
        trt.pivot = new Vector2(0.5f, 1f);
        trt.sizeDelta = new Vector2(-96f, 48f);
        trt.anchoredPosition = new Vector2(0f, -16f);
        title = titleGo.AddComponent<TextMeshProUGUI>();
        PrototypeFonts.Apply(title);
        title.fontSize = 26f;
        title.color = titleColor;
        title.alignment = TextAlignmentOptions.Center;
        title.raycastTarget = false;

        // 正文：占据标题以下的全部空间
        GameObject bodyGo = new GameObject("Body");
        bodyGo.transform.SetParent(panel.transform, false);
        RectTransform brt = bodyGo.AddComponent<RectTransform>();
        brt.anchorMin = new Vector2(0f, 0f);
        brt.anchorMax = new Vector2(1f, 1f);
        brt.pivot = new Vector2(0.5f, 0.5f);
        brt.offsetMin = new Vector2(24f, 20f);
        brt.offsetMax = new Vector2(-24f, -70f);
        body = bodyGo.AddComponent<TextMeshProUGUI>();
        PrototypeFonts.Apply(body);
        body.fontSize = 18f;
        body.color = bodyColor;
        body.alignment = TextAlignmentOptions.TopLeft;
        body.raycastTarget = false;

        GameObject closeGo = new GameObject("CloseButton");
        closeGo.transform.SetParent(panel.transform, false);
        RectTransform closeRect = closeGo.AddComponent<RectTransform>();
        closeRect.anchorMin = closeRect.anchorMax = new Vector2(1f, 1f);
        closeRect.pivot = new Vector2(1f, 1f);
        closeRect.sizeDelta = new Vector2(40f, 40f);
        closeRect.anchoredPosition = new Vector2(-8f, -8f);
        Image closeBackground = closeGo.AddComponent<Image>();
        closeBackground.color = new Color(0.9f, 0.91f, 0.93f, 1f);
        Button closeButton = closeGo.AddComponent<Button>();
        closeButton.targetGraphic = closeBackground;
        closeButton.onClick.AddListener(Close);

        GameObject closeTextGo = new GameObject("Text");
        closeTextGo.transform.SetParent(closeGo.transform, false);
        TextMeshProUGUI closeText = closeTextGo.AddComponent<TextMeshProUGUI>();
        PrototypeFonts.Apply(closeText);
        closeText.text = "×";
        closeText.fontSize = 24f;
        closeText.color = titleColor;
        closeText.alignment = TextAlignmentOptions.Center;
        closeText.raycastTarget = false;
        RectTransform closeTextRect = closeText.GetComponent<RectTransform>();
        closeTextRect.anchorMin = Vector2.zero;
        closeTextRect.anchorMax = Vector2.one;
        closeTextRect.offsetMin = Vector2.zero;
        closeTextRect.offsetMax = Vector2.zero;
    }

    void Close()
    {
        dismissedIndex = activeIndex;
        if (panel != null) panel.SetActive(false);
    }

    void Update()
    {
        if (panel != null && panel.activeSelf && Input.GetKeyDown(KeyCode.Escape)) Close();
    }

    void LateUpdate()
    {
        if (cam == null) cam = Camera.main;
        if (panel == null || cam == null) return;

        POI target = POIManager.Target;
        if (target == null)
        {
            panel.SetActive(false);
            activeIndex = -1;
            dismissedIndex = -1;
            return;
        }

        if (activeIndex != POIManager.CurrentIndex)
        {
            activeIndex = POIManager.CurrentIndex;
            dismissedIndex = -1;
            panel.SetActive(false);
        }

        float dist = DistanceHUD.FlatOffset(target.position, cam.transform.position).magnitude;
        if (dist >= hideDistance)
        {
            panel.SetActive(false);
            dismissedIndex = -1;
            return;
        }

        if (dist < showDistance && dismissedIndex != activeIndex && !panel.activeSelf)
        {
            title.text = target.name + "　|　" + target.area;
            body.text = target.description;
            panel.SetActive(true);
        }
    }
}
