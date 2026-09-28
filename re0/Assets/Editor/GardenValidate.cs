using System;
using System.Reflection;
using GaussianSplatting.Runtime;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class GardenValidate
{
    [MenuItem("Garden Prototype/Validate scene")]
    public static void Validate()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/GardenPrototype.unity", OpenSceneMode.Single);
        var camera = Camera.main;
        var splat = UnityEngine.Object.FindFirstObjectByType<GaussianSplatRenderer>();
        var graph = UnityEngine.Object.FindFirstObjectByType<RouteGraph>();
        var pois = UnityEngine.Object.FindFirstObjectByType<POIManager>();
        var route = UnityEngine.Object.FindFirstObjectByType<RouteLine>();
        var miniMap = UnityEngine.Object.FindFirstObjectByType<TourMiniMap>();
        Require(camera != null && camera.GetComponent<CameraController>() != null && camera.GetComponent<CharacterController>() != null, "camera movement");
        Require(splat != null && splat.m_Asset != null && splat.m_Asset.splatCount == 1300000, "Garden splat asset");
        Require(splat.m_Asset.cameras != null && splat.m_Asset.cameras.Length == 185, "camera poses");
        Require(graph != null && graph.nodes.Length == 28 && graph.edges.Length == 28, "camera path graph");
        Require(pois != null && pois.pois.Count == 3, "three POIs");
        Require(route != null && route.graph == graph && route.GetComponent<LineRenderer>().positionCount == 0, "route renderer starts hidden");
        Require(miniMap != null && miniMap.graph == graph && miniMap.poiManager == pois, "mini map data references");
        var colliderRoot = graph.transform.Find("Walkable floor and table obstacle");
        Require(colliderRoot != null, "walkable area root");
        var floor = colliderRoot.Find("Walkable floor (resize Box Collider in Inspector)")?.GetComponent<BoxCollider>();
        var table = colliderRoot.Find("Table obstacle")?.GetComponent<MeshCollider>();
        Require(floor != null && floor.enabled && floor.size.x > 0f && floor.size.z > 0f, "walkable floor");
        Require(table != null && table.enabled && table.convex && table.sharedMesh != null, "single round table obstacle");
        Require(colliderRoot.GetComponentsInChildren<Collider>().Length == 2, "only floor and table colliders");
        Require(miniMap.walkableFloor == floor, "mini map uses the walkable floor bounds");
        Vector3 modelMin = splat.m_Asset.boundsMin, modelMax = splat.m_Asset.boundsMax;
        for (int x = 0; x < 2; x++)
        for (int y = 0; y < 2; y++)
        for (int z = 0; z < 2; z++)
        {
            Vector3 corner = splat.transform.TransformPoint(new Vector3(
                x == 0 ? modelMin.x : modelMax.x,
                y == 0 ? modelMin.y : modelMax.y,
                z == 0 ? modelMin.z : modelMax.z));
            Require(corner.x >= floor.bounds.min.x && corner.x <= floor.bounds.max.x &&
                corner.z >= floor.bounds.min.z && corner.z <= floor.bounds.max.z,
                "floor covers the transformed 3DGS model bounds");
        }
        var chineseFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Resources/NotoSansSC SDF.asset");
        Require(chineseFont != null, "Chinese UI font");
        Require(chineseFont.TryAddCharacters("景区导览", out string missingCharacters) && string.IsNullOrEmpty(missingCharacters), "Chinese UI glyphs");

        Vector3 start = camera.transform.position + Vector3.down * 1.6f;
        foreach (POI poi in pois.pois)
        {
            Require(graph.TryFindPath(start, poi.position, out Vector3[] path, out float distance), "route to " + poi.name);
            Require(path.Length >= 3 && distance > 0 && Vector3.Distance(path[path.Length - 1], poi.position) < 0.01f, "route geometry to " + poi.name);
        }
        int floorHits = 0;
        foreach (Transform node in graph.nodes)
            if (Physics.Raycast(node.position + Vector3.up, Vector3.down, out _, 2f)) floorHits++;
        Require(floorHits == graph.nodes.Length, $"floor support at every route node ({floorHits}/{graph.nodes.Length})");
        Vector3 farWalkablePoint = floor.bounds.center + Vector3.right * (floor.bounds.extents.x * 0.8f) + Vector3.up;
        Require(Physics.Raycast(farWalkablePoint, Vector3.down, out RaycastHit floorHit, 2f) && floorHit.collider == floor,
            "floor support outside the former perimeter");
        Vector3 tableApproach = table.bounds.center + Vector3.right * (table.bounds.extents.x + 1f);
        Require(Physics.Raycast(tableApproach, Vector3.left, out RaycastHit tableHit, 2f) && tableHit.collider == table,
            "table stops horizontal movement");
        Require(Mathf.Abs(UnityEngine.Object.FindFirstObjectByType<POILabel>().worldScale - 0.004f) < 0.0001f, "compact world labels");
        Require(Mathf.Abs(UnityEngine.Object.FindFirstObjectByType<DistanceHUD>().fontSize - 22f) < 0.01f, "compact HUD text");
        ValidateMiniMap(camera, miniMap, pois);
        ValidateArrivalDismissal(camera, pois);
        EditorSceneManager.OpenScene("Assets/Scenes/GardenPrototype.unity", OpenSceneMode.Single);
        Debug.Log("GARDEN_VALIDATE_OK: model, routes, large walkable floor, single table obstacle, mini map, compact UI, close and re-entry behavior");
    }

    static void ValidateMiniMap(Camera camera, TourMiniMap miniMap, POIManager pois)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        MethodInfo start = typeof(TourMiniMap).GetMethod("Start", flags);
        MethodInfo update = typeof(TourMiniMap).GetMethod("LateUpdate", flags);
        Require(start != null && update != null, "mini map lifecycle methods");
        Vector3 originalPosition = camera.transform.position;
        Quaternion originalRotation = camera.transform.rotation;
        try
        {
            start.Invoke(miniMap, null);
            var canvas = GameObject.Find("TourMiniMapCanvas");
            Require(canvas != null, "mini map canvas");
            var panel = canvas.transform.Find("NavigationWindow");
            var map = panel?.Find("MapArea");
            Require(map != null, "mini map area");
            Require(map.Find("WalkableArea") != null, "mini map displays the walkable area");
            float previousLegendY = float.PositiveInfinity;
            foreach (POI poi in pois.pois)
            {
                Require(map.Find("POI_" + poi.id) != null, "mini map marker for " + poi.name);
                Require(map.Find("Name_" + poi.id) == null, "no overlapping POI name inside map");
                var legend = panel.Find("Legend_" + poi.id)?.GetComponent<RectTransform>();
                Require(legend != null && legend.Find("Name")?.GetComponent<TextMeshProUGUI>()?.text == poi.name,
                    "readable POI legend for " + poi.name);
                Require(previousLegendY - legend.anchoredPosition.y >= legend.sizeDelta.y,
                    "POI legend rows do not overlap");
                previousLegendY = legend.anchoredPosition.y;
            }
            var arrow = map.Find("PlayerArrow").GetComponent<RectTransform>();

            camera.transform.rotation = Quaternion.identity;
            update.Invoke(miniMap, null);
            Vector2 initialPosition = arrow.anchoredPosition;
            Require(Mathf.Abs(Mathf.DeltaAngle(arrow.localEulerAngles.z, 0f)) < 0.01f, "mini map forward arrow");

            camera.transform.position += Vector3.right * 0.5f;
            camera.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
            update.Invoke(miniMap, null);
            Require(arrow.anchoredPosition.x > initialPosition.x, "mini map player moves east");
            Require(Mathf.Abs(Mathf.DeltaAngle(arrow.localEulerAngles.z, 270f)) < 0.01f, "mini map arrow turns east");

            POIManager.CurrentIndex = 1;
            update.Invoke(miniMap, null);
            var selected = map.Find("POI_" + pois.pois[1].id).GetComponent<Image>();
            Require(selected.rectTransform.sizeDelta.x > 14f, "mini map selected POI highlight");
        }
        finally
        {
            camera.transform.SetPositionAndRotation(originalPosition, originalRotation);
            POIManager.CurrentIndex = -1;
            var canvas = GameObject.Find("TourMiniMapCanvas");
            if (canvas != null) UnityEngine.Object.DestroyImmediate(canvas);
        }
    }

    static void ValidateArrivalDismissal(Camera camera, POIManager pois)
    {
        var arrival = UnityEngine.Object.FindFirstObjectByType<ArrivalPanel>();
        Require(arrival != null, "arrival component");
        POIManager.Instance = pois;
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        MethodInfo start = typeof(ArrivalPanel).GetMethod("Start", flags);
        MethodInfo update = typeof(ArrivalPanel).GetMethod("LateUpdate", flags);
        Require(start != null && update != null, "arrival lifecycle methods");
        start.Invoke(arrival, null);
        var canvas = GameObject.Find("ArrivalCanvas");
        Require(canvas != null && canvas.GetComponent<GraphicRaycaster>() != null, "clickable arrival canvas");
        var panel = canvas.transform.Find("Panel").gameObject;
        var close = panel.transform.Find("CloseButton").GetComponent<Button>();
        Require(close != null && close.interactable, "arrival close button");

        POIManager.Select(0);
        camera.transform.position = pois.pois[0].position + Vector3.up * 1.6f;
        update.Invoke(arrival, null);
        Require(panel.activeSelf, "arrival appears near target");
        close.onClick.Invoke();
        Require(!panel.activeSelf, "close hides arrival");
        update.Invoke(arrival, null);
        Require(!panel.activeSelf, "closed arrival stays hidden nearby");

        camera.transform.position += Vector3.right * (arrival.hideDistance + 1f);
        update.Invoke(arrival, null);
        camera.transform.position = pois.pois[0].position + Vector3.up * 1.6f;
        update.Invoke(arrival, null);
        Require(panel.activeSelf, "arrival reopens after leaving and returning");

        close.onClick.Invoke();
        POIManager.Select(1);
        camera.transform.position = pois.pois[1].position + Vector3.up * 1.6f;
        update.Invoke(arrival, null);
        Require(panel.activeSelf, "new destination reopens arrival");
        POIManager.CurrentIndex = -1;
        POIManager.Instance = null;
    }

    static void Require(bool condition, string message)
    {
        if (!condition) throw new Exception("GARDEN_VALIDATE_FAIL: " + message);
    }
}
