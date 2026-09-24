using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 原型零件 3｜三维景点标签   —— 归成员 C
/// 挂到 POIManager 所在的同一个物体上。
/// 运行时会为每个景点自动生成一个始终朝向相机的文字标签。
///
/// 标签大小分两种模式：
///   · 恒定世界大小（constantScreenSize 关）—— 远处的标签会显得更小，像真实的路牌
///   · 恒定屏幕大小（constantScreenSize 开）—— 无论远近看起来都一样大，更容易读
/// </summary>
public class POILabel : MonoBehaviour
{
    [Header("标签位置")]
    [Tooltip("标签高出景点位置多少米")]
    public float heightOffset = 1.4f;

    [Header("标签尺寸")]
    [Tooltip("标签在世界空间里的缩放。字太大就调小这个值")]
    public float worldScale = 0.004f;

    [Header("恒定屏幕大小")]
    [Tooltip("勾上之后远处的标签自动放大、近处自动缩小，看起来大小一致")]
    public bool constantScreenSize = true;

    [Tooltip("距离等于这个值时，用 worldScale 作为基准尺寸")]
    public float referenceDistance = 8f;

    [Tooltip("自动缩放的上下限，避免极端距离下大小失控")]
    public Vector2 scaleLimits = new Vector2(0.75f, 1.6f);

    [Header("外观")]
    public Color textColor = new Color(0.10f, 0.10f, 0.10f, 1f);
    public Color backColor = new Color(1f, 1f, 1f, 0.82f);
    public float fontSize = 32f;

    [Header("显示管理")]
    [Tooltip("超过这个距离就隐藏标签，避免远处糊成一片")]
    public float maxVisibleDistance = 60f;

    readonly List<Transform> labels = new List<Transform>();
    readonly List<Vector3> labelPositions = new List<Vector3>();
    Camera cam;

    // 画布本身的尺寸（米）。改这个会同时改变标签的长宽比，一般不用动
    static readonly Vector2 CanvasSize = new Vector2(320f, 64f);

    void Start()
    {
        POIManager mgr = GetComponent<POIManager>();
        if (mgr == null)
        {
            Debug.LogError("[POILabel] 需要和 POIManager 挂在同一个物体上。");
            enabled = false;
            return;
        }

        foreach (POI poi in mgr.pois)
        {
            labels.Add(BuildLabel(poi));
            labelPositions.Add(poi.position + Vector3.up * heightOffset);
        }
    }

    Transform BuildLabel(POI poi)
    {
        GameObject root = new GameObject("Label_" + poi.id);
        root.transform.SetParent(transform, false);

        Canvas canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 10;

        RectTransform rt = canvas.GetComponent<RectTransform>();
        rt.sizeDelta = CanvasSize;
        rt.localScale = Vector3.one * worldScale;

        GameObject bg = new GameObject("Bg");
        bg.transform.SetParent(root.transform, false);
        Image img = bg.AddComponent<Image>();
        img.color = backColor;
        img.raycastTarget = false;
        Stretch(bg.GetComponent<RectTransform>());

        GameObject txt = new GameObject("Text");
        txt.transform.SetParent(root.transform, false);
        TextMeshProUGUI t = txt.AddComponent<TextMeshProUGUI>();
        PrototypeFonts.Apply(t);
        t.text = poi.name;
        t.fontSize = fontSize;
        t.color = textColor;
        t.alignment = TextAlignmentOptions.Center;
        t.raycastTarget = false;
        Stretch(txt.GetComponent<RectTransform>());

        root.transform.position = poi.position + Vector3.up * heightOffset;
        return root.transform;
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    void LateUpdate()
    {
        if (cam == null) cam = Camera.main;
        if (cam == null) return;

        Vector3 camPos = cam.transform.position;
        for (int i = 0; i < labels.Count; i++)
        {
            Transform t = labels[i];
            if (t == null) continue;

            float dist = Vector3.Distance(camPos, labelPositions[i]);
            bool visible = dist <= maxVisibleDistance;
            t.gameObject.SetActive(visible);
            if (!visible) continue;

            t.position = labelPositions[i];
            t.rotation = Quaternion.LookRotation(t.position - camPos);

            float k = 1f;
            if (constantScreenSize && referenceDistance > 0.001f)
            {
                k = Mathf.Clamp(dist / referenceDistance, scaleLimits.x, scaleLimits.y);
            }
            t.localScale = Vector3.one * worldScale * k;
        }
    }
}
