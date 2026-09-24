using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 原型零件 6｜距离与方向提示   —— 归成员 B
/// 挂到一个空物体上。屏幕右上角显示到当前目的地的距离。
///
/// 距离取的是"水平距离"——因为景点位置标在地板高度，
/// 而相机在眼睛高度，直接算三维距离会凭空多出一人多高。
/// </summary>
public class DistanceHUD : MonoBehaviour
{
    [Header("布局")]
    public float rightMargin = 32f;
    public float topMargin = 28f;
    public float width = 360f;
    public float height = 60f;

    [Header("外观")]
    public Color textColor = new Color(0.10f, 0.10f, 0.10f, 1f);
    public float fontSize = 22f;

    TextMeshProUGUI label;
    Camera cam;
    RouteLine route;

    void Start()
    {
        GameObject canvasGo = new GameObject("HUDCanvas");
        Canvas c = canvasGo.AddComponent<Canvas>();
        c.renderMode = RenderMode.ScreenSpaceOverlay;
        c.sortingOrder = 20;
        CanvasScaler s = canvasGo.AddComponent<CanvasScaler>();
        s.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        s.referenceResolution = new Vector2(1920f, 1080f);

        GameObject go = new GameObject("DistanceText");
        go.transform.SetParent(canvasGo.transform, false);
        RectTransform rt = go.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(1f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(1f, 1f);
        rt.sizeDelta = new Vector2(width, height);
        rt.anchoredPosition = new Vector2(-rightMargin, -topMargin);

        label = go.AddComponent<TextMeshProUGUI>();
        PrototypeFonts.Apply(label);
        label.fontSize = fontSize;
        label.color = textColor;
        label.alignment = TextAlignmentOptions.Right;
        label.raycastTarget = false;
    }

    void LateUpdate()
    {
        if (cam == null) cam = Camera.main;
        if (label == null || cam == null) return;
        if (route == null) route = FindFirstObjectByType<RouteLine>();

        POI target = POIManager.Target;
        if (target == null)
        {
            label.text = "未选择目的地";
            return;
        }

        Vector3 flat = FlatOffset(target.position, cam.transform.position);
        float dist = route != null && route.hasRoute ? route.routeLength : flat.magnitude;
        string arrow = DescribeDirection(flat, cam.transform);

        label.text = route != null && route.hasRoute
            ? string.Format("{0}　{1}\n路线约 {2:F1} 场景单位", target.name, arrow, dist)
            : string.Format("{0}　{1}\n直线约 {2:F1} 场景单位（无可用路线）", target.name, arrow, dist);
    }

    /// <summary>把"目标减相机"投影到水平面。</summary>
    public static Vector3 FlatOffset(Vector3 targetPos, Vector3 camPos)
    {
        Vector3 d = targetPos - camPos;
        d.y = 0f;
        return d;
    }

    /// <summary>把目标方向转成"直走/左转/右转/向后"，方便原型阶段辨认。</summary>
    static string DescribeDirection(Vector3 flat, Transform cam)
    {
        if (flat.sqrMagnitude < 0.0001f) return "就在脚下";

        Vector3 camFlat = Vector3.ProjectOnPlane(cam.forward, Vector3.up);
        if (camFlat.sqrMagnitude < 0.0001f) camFlat = Vector3.forward;

        float signed = Vector3.SignedAngle(camFlat.normalized, flat.normalized, Vector3.up);
        float a = Mathf.Abs(signed);
        if (a < 30f) return "↑ 直走";
        if (a > 150f) return "↓ 向后";
        return signed > 0f ? "→ 右转" : "← 左转";
    }
}
