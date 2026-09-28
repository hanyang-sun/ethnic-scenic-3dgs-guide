using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 路网示意小地图。读取场景中的路点和景点，屏幕上方对应 Unity 世界 +Z。
/// 不依赖 Garden 的固定景点数量、名称或坐标。
/// </summary>
public class TourMiniMap : MonoBehaviour
{
    [Header("场景数据")]
    public RouteGraph graph;
    public POIManager poiManager;
    [Tooltip("可行走地板；留空时小地图使用路点和景点计算范围")]
    public BoxCollider walkableFloor;

    [Header("窗口")]
    public float mapSize = 256f;
    public float mapPadding = 28f;
    public float rightMargin = 24f;
    public float bottomMargin = 24f;

    readonly List<Image> poiMarkers = new List<Image>();
    GameObject canvasObject;
    RectTransform mapRect;
    RectTransform playerArrow;
    RectTransform playerArrowShadow;
    TextMeshProUGUI positionStatus;
    Texture2D arrowTexture;
    Sprite arrowSprite;
    Camera sceneCamera;
    Vector2 worldCenter;
    float pixelsPerUnit;
    float usableHalfSize;

    void Start()
    {
        if (graph == null) graph = GetComponent<RouteGraph>();
        if (poiManager == null) poiManager = GetComponent<POIManager>();
        if (sceneCamera == null) sceneCamera = Camera.main;
        if (graph == null || poiManager == null || sceneCamera == null)
        {
            Debug.LogError("[TourMiniMap] 需要 RouteGraph、POIManager 和主相机。");
            enabled = false;
            return;
        }

        CalculateBounds();
        BuildWindow();
        DrawWalkableFloor();
        DrawRoads();
        DrawPOIs();
        BuildPlayerArrow();
        RefreshPlayer();
        RefreshSelection();
    }

    void LateUpdate()
    {
        if (sceneCamera == null) sceneCamera = Camera.main;
        if (sceneCamera == null || playerArrow == null) return;
        RefreshPlayer();
        RefreshSelection();
    }

    void OnEnable()
    {
        if (canvasObject != null) canvasObject.SetActive(true);
    }

    void OnDisable()
    {
        if (canvasObject != null) canvasObject.SetActive(false);
    }

    void OnDestroy()
    {
        if (Application.isPlaying)
        {
            if (canvasObject != null) Destroy(canvasObject);
            if (arrowSprite != null) Destroy(arrowSprite);
            if (arrowTexture != null) Destroy(arrowTexture);
        }
        else
        {
            if (canvasObject != null) DestroyImmediate(canvasObject);
            if (arrowSprite != null) DestroyImmediate(arrowSprite);
            if (arrowTexture != null) DestroyImmediate(arrowTexture);
        }
    }

    void CalculateBounds()
    {
        float minX = float.PositiveInfinity, maxX = float.NegativeInfinity;
        float minZ = float.PositiveInfinity, maxZ = float.NegativeInfinity;
        Include(sceneCamera.transform.position, ref minX, ref maxX, ref minZ, ref maxZ);

        if (walkableFloor != null && walkableFloor.enabled)
        {
            Bounds floorBounds = walkableFloor.bounds;
            Include(floorBounds.min, ref minX, ref maxX, ref minZ, ref maxZ);
            Include(floorBounds.max, ref minX, ref maxX, ref minZ, ref maxZ);
        }

        if (graph.nodes != null)
            foreach (Transform node in graph.nodes)
                if (node != null) Include(node.position, ref minX, ref maxX, ref minZ, ref maxZ);

        if (poiManager.pois != null)
            foreach (POI poi in poiManager.pois)
                if (poi != null) Include(poi.position, ref minX, ref maxX, ref minZ, ref maxZ);

        worldCenter = new Vector2((minX + maxX) * 0.5f, (minZ + maxZ) * 0.5f);
        float worldSpan = Mathf.Max(maxX - minX, maxZ - minZ, 1f);
        usableHalfSize = Mathf.Max(20f, mapSize * 0.5f - mapPadding);
        pixelsPerUnit = 2f * usableHalfSize / worldSpan;
    }

    void DrawWalkableFloor()
    {
        if (walkableFloor == null || !walkableFloor.enabled) return;
        Bounds bounds = walkableFloor.bounds;
        RectTransform area = CreateRect("WalkableArea", mapRect);
        area.anchorMin = area.anchorMax = new Vector2(0.5f, 0.5f);
        area.sizeDelta = new Vector2(bounds.size.x, bounds.size.z) * pixelsPerUnit;
        area.anchoredPosition = MapPosition(bounds.center);
        AddImage(area.gameObject, new Color(0.22f, 0.34f, 0.36f, 0.96f));
    }

    static void Include(Vector3 point, ref float minX, ref float maxX, ref float minZ, ref float maxZ)
    {
        minX = Mathf.Min(minX, point.x);
        maxX = Mathf.Max(maxX, point.x);
        minZ = Mathf.Min(minZ, point.z);
        maxZ = Mathf.Max(maxZ, point.z);
    }

    Vector2 MapPosition(Vector3 worldPosition)
        => new Vector2(worldPosition.x - worldCenter.x, worldPosition.z - worldCenter.y) * pixelsPerUnit;

    void BuildWindow()
    {
        canvasObject = new GameObject("TourMiniMapCanvas");
        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 15;
        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        RectTransform panel = CreateRect("NavigationWindow", canvasObject.transform);
        panel.anchorMin = panel.anchorMax = new Vector2(1f, 0f);
        panel.pivot = new Vector2(1f, 0f);
        panel.sizeDelta = new Vector2(mapSize + 32f, mapSize + 82f);
        panel.anchoredPosition = new Vector2(-rightMargin, bottomMargin);
        AddImage(panel.gameObject, new Color(0.06f, 0.11f, 0.17f, 0.94f));

        RectTransform titleRect = CreateRect("Title", panel);
        titleRect.anchorMin = titleRect.anchorMax = new Vector2(0.5f, 1f);
        titleRect.sizeDelta = new Vector2(mapSize, 32f);
        titleRect.anchoredPosition = new Vector2(0f, -21f);
        CreateText(titleRect.gameObject, "导览地图", 20f, Color.white);

        mapRect = CreateRect("MapArea", panel);
        mapRect.anchorMin = mapRect.anchorMax = new Vector2(0.5f, 0f);
        mapRect.pivot = new Vector2(0.5f, 0f);
        mapRect.sizeDelta = Vector2.one * mapSize;
        mapRect.anchoredPosition = new Vector2(0f, 28f);
        AddImage(mapRect.gameObject, new Color(0.13f, 0.20f, 0.28f, 0.98f));
        mapRect.gameObject.AddComponent<RectMask2D>();

        RectTransform hintRect = CreateRect("MapDirection", panel);
        hintRect.anchorMin = hintRect.anchorMax = new Vector2(0.5f, 0f);
        hintRect.sizeDelta = new Vector2(mapSize, 22f);
        hintRect.anchoredPosition = new Vector2(0f, 13f);
        positionStatus = CreateText(hintRect.gameObject, "上方为世界 +Z", 14f, new Color(0.75f, 0.84f, 0.91f));
    }

    void DrawRoads()
    {
        if (graph.nodes == null || graph.edges == null) return;
        foreach (RouteGraph.Edge edge in graph.edges)
        {
            if (edge.a < 0 || edge.b < 0 || edge.a >= graph.nodes.Length || edge.b >= graph.nodes.Length) continue;
            Transform a = graph.nodes[edge.a], b = graph.nodes[edge.b];
            if (a == null || b == null) continue;
            Vector2 start = MapPosition(a.position), end = MapPosition(b.position);
            Vector2 delta = end - start;
            RectTransform line = CreateRect("Road", mapRect);
            line.anchorMin = line.anchorMax = new Vector2(0.5f, 0.5f);
            line.pivot = new Vector2(0f, 0.5f);
            line.sizeDelta = new Vector2(delta.magnitude, 3f);
            line.anchoredPosition = start;
            line.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
            AddImage(line.gameObject, new Color(0.53f, 0.71f, 0.78f, 0.8f));
        }
    }

    void DrawPOIs()
    {
        if (poiManager.pois == null) return;
        foreach (POI poi in poiManager.pois)
        {
            if (poi == null) { poiMarkers.Add(null); continue; }
            Vector2 point = MapPosition(poi.position);
            RectTransform marker = CreateRect("POI_" + poi.id, mapRect);
            marker.anchorMin = marker.anchorMax = new Vector2(0.5f, 0.5f);
            marker.sizeDelta = new Vector2(14f, 14f);
            marker.anchoredPosition = point;
            marker.localRotation = Quaternion.Euler(0f, 0f, 45f);
            poiMarkers.Add(AddImage(marker.gameObject, new Color(1f, 0.77f, 0.30f)));

            RectTransform label = CreateRect("Name_" + poi.id, mapRect);
            label.anchorMin = label.anchorMax = new Vector2(0.5f, 0.5f);
            label.sizeDelta = new Vector2(120f, 22f);
            label.anchoredPosition = new Vector2(
                Mathf.Clamp(point.x, -mapSize * 0.5f + 60f, mapSize * 0.5f - 60f),
                Mathf.Clamp(point.y + 19f, -mapSize * 0.5f + 12f, mapSize * 0.5f - 12f));
            CreateText(label.gameObject, poi.name, 15f, Color.white);
        }
    }

    void BuildPlayerArrow()
    {
        arrowTexture = new Texture2D(32, 32, TextureFormat.RGBA32, false);
        arrowTexture.filterMode = FilterMode.Bilinear;
        var pixels = new Color32[32 * 32];
        for (int y = 0; y < 32; y++)
        {
            float halfWidth = (31f - y) * 0.46f;
            for (int x = 0; x < 32; x++)
                pixels[y * 32 + x] = Mathf.Abs(x - 15.5f) <= halfWidth
                    ? new Color32(255, 255, 255, 255)
                    : new Color32(0, 0, 0, 0);
        }
        arrowTexture.SetPixels32(pixels);
        arrowTexture.Apply();
        arrowSprite = Sprite.Create(arrowTexture, new Rect(0f, 0f, 32f, 32f), new Vector2(0.5f, 0.5f));

        playerArrowShadow = CreateRect("PlayerArrowShadow", mapRect);
        playerArrowShadow.anchorMin = playerArrowShadow.anchorMax = new Vector2(0.5f, 0.5f);
        playerArrowShadow.sizeDelta = new Vector2(32f, 32f);
        Image shadow = AddImage(playerArrowShadow.gameObject, new Color(0.02f, 0.06f, 0.10f));
        shadow.sprite = arrowSprite;

        playerArrow = CreateRect("PlayerArrow", mapRect);
        playerArrow.anchorMin = playerArrow.anchorMax = new Vector2(0.5f, 0.5f);
        playerArrow.sizeDelta = new Vector2(25f, 25f);
        Image arrow = AddImage(playerArrow.gameObject, new Color(0.22f, 0.93f, 1f));
        arrow.sprite = arrowSprite;
    }

    void RefreshPlayer()
    {
        Vector2 point = MapPosition(sceneCamera.transform.position);
        bool outside = Mathf.Abs(point.x) > usableHalfSize || Mathf.Abs(point.y) > usableHalfSize;
        point.x = Mathf.Clamp(point.x, -usableHalfSize, usableHalfSize);
        point.y = Mathf.Clamp(point.y, -usableHalfSize, usableHalfSize);
        playerArrow.anchoredPosition = playerArrowShadow.anchoredPosition = point;
        positionStatus.text = outside ? "角色位于地图范围外" : "上方为世界 +Z";

        Vector3 horizontal = Vector3.ProjectOnPlane(sceneCamera.transform.forward, Vector3.up);
        if (horizontal.sqrMagnitude < 0.0001f) return;
        Vector2 facing = new Vector2(horizontal.x, horizontal.z).normalized;
        Quaternion heading = Quaternion.Euler(0f, 0f, Vector2.SignedAngle(Vector2.up, facing));
        playerArrow.localRotation = playerArrowShadow.localRotation = heading;
    }

    void RefreshSelection()
    {
        for (int i = 0; i < poiMarkers.Count; i++)
        {
            Image marker = poiMarkers[i];
            if (marker == null) continue;
            bool selected = i == POIManager.CurrentIndex;
            marker.color = selected ? new Color(1f, 0.94f, 0.23f) : new Color(1f, 0.60f, 0.26f);
            marker.rectTransform.sizeDelta = selected ? new Vector2(19f, 19f) : new Vector2(14f, 14f);
        }
    }

    static RectTransform CreateRect(string name, Transform parent)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        return go.AddComponent<RectTransform>();
    }

    static Image AddImage(GameObject go, Color color)
    {
        Image image = go.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    static TextMeshProUGUI CreateText(GameObject go, string value, float size, Color color)
    {
        TextMeshProUGUI text = go.AddComponent<TextMeshProUGUI>();
        PrototypeFonts.Apply(text);
        text.text = value;
        text.fontSize = size;
        text.color = color;
        text.alignment = TextAlignmentOptions.Center;
        text.overflowMode = TextOverflowModes.Ellipsis;
        text.raycastTarget = false;
        return text;
    }
}
