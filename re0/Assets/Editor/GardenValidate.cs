using System;
using System.Collections.Generic;
using CSU.Tour;
using GaussianSplatting.Runtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class GardenValidate
{
    const string ScenePath = "Assets/Scenes/GardenPrototype.unity";

    [MenuItem("Garden Prototype/Validate teacher-style architecture")]
    public static void Validate()
    {
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        Camera camera = Camera.main;
        TourApp app = UnityEngine.Object.FindFirstObjectByType<TourApp>();
        var splat = UnityEngine.Object.FindFirstObjectByType<GaussianSplatRenderer>();

        Require(app != null, "one TourApp control center");
        Require(UnityEngine.Object.FindObjectsByType<TourApp>(FindObjectsSortMode.None).Length == 1,
            "exactly one TourApp");
        Require(camera != null && app.view == camera, "TourApp camera reference");
        Require(app.player != null && camera.transform.parent == app.player, "player root with child camera");
        Require(app.motor != null && app.motor.transform == app.player, "player CharacterController");
        Require(splat != null && app.splat == splat && splat.m_Asset != null &&
                splat.m_Asset.splatCount == 1300000, "Garden splat asset");
        Require(splat.m_Asset.cameras != null && splat.m_Asset.cameras.Length == 185, "camera poses");
        Require(app.nodes != null && app.nodes.Length == 28, "28 authored route nodes");
        Require(app.edges != null && app.edges.Length == 28, "28 route edges");
        Require(app.pois != null && app.pois.Length == 3, "three TourPoi records");
        Require(Mathf.Abs(app.replanInterval - 0.75f) < 0.001f, "0.75 second replanning interval");
        Require(app.obstacleLayer == 9 && LayerMask.LayerToName(9) == "TourObstacle",
            "TourObstacle layer");
        Require(LayerMask.LayerToName(8) == "WalkableGround", "WalkableGround layer");
        Require(app.uiFont != null, "Chinese UI font");

        var ids = new HashSet<string>();
        foreach (TourPoi poi in app.pois)
        {
            Require(poi != null && !string.IsNullOrWhiteSpace(poi.id) && ids.Add(poi.id),
                "unique POI id");
            Require(poi.node >= 0 && poi.node < app.nodes.Length, "POI route node");
            Require(poi.visualBounds.size.sqrMagnitude > 0f, "POI observation bounds");
            Require(poi.arrivalSize.x > 0f && poi.arrivalSize.y > 0f && poi.arrivalSize.z > 0f,
                "POI arrival bounds");
            List<int> path = TourGraph.Shortest(app.nodes, app.edges, 0, poi.node, app.SegmentValid);
            Require(path.Count > 0 && path[0] == 0 && path[path.Count - 1] == poi.node,
                "obstacle-filtered route to " + poi.title);
        }

        Transform colliderRoot = app.transform.Find("Walkable floor and table obstacle");
        Require(colliderRoot != null, "walkable and obstacle root");
        BoxCollider floor = colliderRoot
            .Find("Walkable floor (resize Box Collider in Inspector)")?.GetComponent<BoxCollider>();
        MeshCollider table = colliderRoot.Find("Table obstacle")?.GetComponent<MeshCollider>();
        Require(floor != null && floor.enabled && floor.gameObject.layer == 8 && app.ground == floor,
            "TourApp walkable ground reference");
        Require(table != null && table.enabled && table.convex && table.sharedMesh != null &&
                table.gameObject.layer == 9, "TourObstacle table collider");
        Require(colliderRoot.GetComponentsInChildren<Collider>().Length == 2,
            "only floor and table colliders");

        Bounds modelBounds = WorldBounds(splat.transform, splat.m_Asset);
        Require(floor.bounds.min.x <= modelBounds.min.x && floor.bounds.max.x >= modelBounds.max.x &&
                floor.bounds.min.z <= modelBounds.min.z && floor.bounds.max.z >= modelBounds.max.z,
            "floor covers transformed 3DGS bounds");

        int floorHits = 0;
        foreach (Vector3 node in app.nodes)
            if (Physics.Raycast(node + Vector3.up, Vector3.down, out RaycastHit hit, 2f) &&
                hit.collider == floor)
                floorHits++;
        Require(floorHits == app.nodes.Length,
            "floor support at every route node (" + floorHits + "/" + app.nodes.Length + ")");

        Require(typeof(TourHud).IsSealed && typeof(TourMap).IsSealed &&
                typeof(TourLog).IsSealed && typeof(PoiBoundsOverlay).IsSealed,
            "teacher-style UI, map, bounds and log modules");
        Require(typeof(IAdaptiveTourPlanner).IsInterface && typeof(IViewingStateEstimator).IsInterface,
            "research extension contracts");

        Debug.Log("GARDEN_VALIDATE_OK: centralized TourApp, obstacle-aware TourGraph, TourHud/TourMap, " +
                  "POI bounds, reset/pause/visit state and JSONL TourLog");
    }

    static Bounds WorldBounds(Transform transform, GaussianSplatAsset asset)
    {
        Vector3 min = asset.boundsMin;
        Vector3 max = asset.boundsMax;
        var bounds = new Bounds(transform.TransformPoint(min), Vector3.zero);
        for (int x = 0; x < 2; x++)
        for (int y = 0; y < 2; y++)
        for (int z = 0; z < 2; z++)
            bounds.Encapsulate(transform.TransformPoint(new Vector3(
                x == 0 ? min.x : max.x,
                y == 0 ? min.y : max.y,
                z == 0 ? min.z : max.z)));
        return bounds;
    }

    static void Require(bool condition, string message)
    {
        if (!condition) throw new Exception("GARDEN_VALIDATE_FAIL: " + message);
    }
}
