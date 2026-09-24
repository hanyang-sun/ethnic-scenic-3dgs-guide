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
        Require(camera != null && camera.GetComponent<CameraController>() != null && camera.GetComponent<CharacterController>() != null, "camera movement");
        Require(splat != null && splat.m_Asset != null && splat.m_Asset.splatCount == 1300000, "Garden splat asset");
        Require(splat.m_Asset.cameras != null && splat.m_Asset.cameras.Length == 185, "camera poses");
        Require(graph != null && graph.nodes.Length == 28 && graph.edges.Length == 28, "camera path graph");
        Require(pois != null && pois.pois.Count == 3, "three POIs");
        Require(route != null && route.graph == graph && route.GetComponent<LineRenderer>().positionCount == 0, "route renderer starts hidden");
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
        Require(Mathf.Abs(UnityEngine.Object.FindFirstObjectByType<POILabel>().worldScale - 0.004f) < 0.0001f, "compact world labels");
        Require(Mathf.Abs(UnityEngine.Object.FindFirstObjectByType<DistanceHUD>().fontSize - 22f) < 0.01f, "compact HUD text");
        ValidateArrivalDismissal(camera, pois);
        EditorSceneManager.OpenScene("Assets/Scenes/GardenPrototype.unity", OpenSceneMode.Single);
        Debug.Log("GARDEN_VALIDATE_OK: model, routes, 28 floor nodes, compact UI, close and re-entry behavior");
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
