using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 原型零件 5｜目的地选择   —— 归成员 B
/// 挂到一个空物体上即可，运行时会自动生成一列景点按钮。
/// 不需要手动搭 Canvas、不需要拖引用。
/// </summary>
public class DestinationSelector : MonoBehaviour
{
    [Header("布局")]
    public float buttonWidth = 176f;
    public float buttonHeight = 40f;
    public float leftMargin = 24f;
    public float topMargin = 24f;
    public float spacing = 8f;

    [Header("外观")]
    public Color normalColor = new Color(1f, 1f, 1f, 0.9f);
    public Color selectedColor = new Color(0.12f, 0.22f, 0.39f, 0.95f);
    public Color textColor = new Color(0.10f, 0.10f, 0.10f, 1f);
    public Color selectedTextColor = Color.white;

    readonly System.Collections.Generic.List<Button> buttons = new System.Collections.Generic.List<Button>();
    readonly System.Collections.Generic.List<TextMeshProUGUI> texts = new System.Collections.Generic.List<TextMeshProUGUI>();

    void Start()
    {
        EnsureEventSystem();

        POIManager mgr = POIManager.Instance != null ? POIManager.Instance : FindFirstObjectByType<POIManager>();
        if (mgr == null)
        {
            Debug.LogError("[DestinationSelector] 场景里找不到 POIManager。");
            enabled = false;
            return;
        }

        Canvas canvas = BuildCanvas("SelectorCanvas");

        for (int i = 0; i < mgr.pois.Count; i++)
        {
            int index = i;   // 闭包捕获，别直接用 i
            BuildButton(canvas, mgr.pois[i], index);
        }
    }

    void Update()
    {
        RefreshVisual();
    }

    void BuildButton(Canvas canvas, POI poi, int index)
    {
        GameObject go = new GameObject("Btn_" + poi.id);
        go.transform.SetParent(canvas.transform, false);

        RectTransform rt = go.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.sizeDelta = new Vector2(buttonWidth, buttonHeight);
        rt.anchoredPosition = new Vector2(leftMargin, -topMargin - index * (buttonHeight + spacing));

        Image img = go.AddComponent<Image>();
        img.color = normalColor;

        Button btn = go.AddComponent<Button>();
        btn.targetGraphic = img;
        btn.onClick.AddListener(() => POIManager.Select(index));

        GameObject txt = new GameObject("Text");
        txt.transform.SetParent(go.transform, false);
        TextMeshProUGUI t = txt.AddComponent<TextMeshProUGUI>();
        PrototypeFonts.Apply(t);
        t.text = poi.name;
        t.fontSize = 20f;
        t.alignment = TextAlignmentOptions.Center;
        t.color = textColor;
        t.raycastTarget = false;
        RectTransform trt = txt.GetComponent<RectTransform>();
        trt.anchorMin = Vector2.zero;
        trt.anchorMax = Vector2.one;
        trt.offsetMin = Vector2.zero;
        trt.offsetMax = Vector2.zero;

        buttons.Add(btn);
        texts.Add(t);
    }

    void RefreshVisual()
    {
        for (int i = 0; i < buttons.Count; i++)
        {
            bool on = i == POIManager.CurrentIndex;
            if (buttons[i].targetGraphic is Image im) im.color = on ? selectedColor : normalColor;
            texts[i].color = on ? selectedTextColor : textColor;
        }
    }

    static void EnsureEventSystem()
    {
        if (FindFirstObjectByType<EventSystem>() != null) return;
        GameObject es = new GameObject("EventSystem");
        es.AddComponent<EventSystem>();
        es.AddComponent<StandaloneInputModule>();
    }

    static Canvas BuildCanvas(string name)
    {
        GameObject go = new GameObject(name);
        Canvas c = go.AddComponent<Canvas>();
        c.renderMode = RenderMode.ScreenSpaceOverlay;
        CanvasScaler s = go.AddComponent<CanvasScaler>();
        s.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        s.referenceResolution = new Vector2(1920f, 1080f);
        go.AddComponent<GraphicRaycaster>();
        return c;
    }
}
