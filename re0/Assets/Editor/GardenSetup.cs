using System;
using System.IO;
using System.Linq;
using GaussianSplatting.Runtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;

public static class GardenSetup
{
    const string ScenePath = "Assets/Scenes/GardenPrototype.unity";
    const float EyeHeight = 1.6f;
    const float BoundaryPadding = 0.5f;
    const float TableRadius = 2.45f;
    const float TableHeight = 2.4f;

    public static void BuildAndVerify()
    {
        Build();
        GardenValidate.Validate();
        GardenCapture.Capture();
    }

    [MenuItem("Garden Prototype/2. Build scene")]
    public static void Build()
    {
        var asset = AssetDatabase.LoadAssetAtPath<GaussianSplatAsset>("Assets/GaussianAssets/Garden/garden.asset");
        if (asset == null || asset.cameras == null || asset.cameras.Length < 52)
            throw new Exception("Import Garden with cameras.json first (Garden Prototype > 1. Import 3DGS).");

        if (!AssetDatabase.IsValidFolder("Assets/Resources")) AssetDatabase.CreateFolder("Assets", "Resources");
        if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>("Assets/Resources/NotoSansSC SDF.asset") == null)
            if (!AssetDatabase.CopyAsset("Assets/Fonts/NotoSansSC SDF.asset", "Assets/Resources/NotoSansSC SDF.asset"))
                throw new Exception("Could not copy the existing Chinese TMP font.");

        var scene = EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity", OpenSceneMode.Single);
        var sampleVolume = GameObject.Find("Global Volume");
        if (sampleVolume != null) UnityEngine.Object.DestroyImmediate(sampleVolume);
        var splat = UnityEngine.Object.FindFirstObjectByType<GaussianSplatRenderer>();
        var camera = Camera.main;
        if (splat == null || camera == null) throw new Exception("SampleScene must contain a Gaussian renderer and Main Camera.");
        splat.name = "Garden 3DGS (visual only)";
        splat.m_Asset = asset;
        splat.transform.position = Vector3.zero;
        splat.transform.rotation = Quaternion.Euler(-154f, 0f, 0f);
        splat.transform.localScale = new Vector3(1f, 1f, -1f);

        camera.name = "Main Camera";
        camera.nearClipPlane = 0.05f;
        camera.farClipPlane = 200f;
        camera.fieldOfView = 60f;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.56f, 0.69f, 0.79f, 1f);
        var cameraData = camera.GetUniversalAdditionalCameraData();
        cameraData.renderPostProcessing = false;
        splat.ActivateCamera(24);
        var character = camera.GetComponent<CharacterController>();
        if (character == null) character = camera.gameObject.AddComponent<CharacterController>();
        character.height = EyeHeight;
        character.center = Vector3.down * EyeHeight * 0.5f;
        character.radius = 0.25f;
        character.stepOffset = 0.3f;
        var movement = camera.GetComponent<CameraController>();
        if (movement == null) movement = camera.gameObject.AddComponent<CameraController>();
        movement.moveSpeed = 1.7f;
        movement.boostMultiplier = 1.8f;
        movement.useGravity = true;
        movement.allowVerticalFly = false;

        var tour = new GameObject("Garden Tour");
        var graph = tour.AddComponent<RouteGraph>();
        var graphRoot = new GameObject("Walkable camera path (indices 24-51)");
        graphRoot.transform.SetParent(tour.transform);
        graph.nodes = Enumerable.Range(24, 28).Select(index =>
        {
            var node = new GameObject($"WayPoint_{index}");
            node.transform.SetParent(graphRoot.transform);
            node.transform.position = WalkPoint(splat.transform, asset.cameras[index]);
            return node.transform;
        }).ToArray();
        graph.edges = Enumerable.Range(0, graph.nodes.Length)
            .Select(i => new RouteGraph.Edge { a = i, b = (i + 1) % graph.nodes.Length }).ToArray();

        BoxCollider walkableFloor = CreateWalkableColliders(tour.transform, graph, splat);

        var pois = tour.AddComponent<POIManager>();
        pois.pois.Clear();
        AddPoi(pois, "view_west", "西侧观景位", graph.nodes[7].position, "从西侧观察花园中央区域。");
        AddPoi(pois, "view_north", "北侧观景位", graph.nodes[15].position, "从另一角度观察花园与桌面细节。");
        AddPoi(pois, "view_east", "东侧观景位", graph.nodes[23].position, "沿环形路线到达东侧观察位置。");
        var labels = tour.AddComponent<POILabel>();
        var selector = tour.AddComponent<DestinationSelector>();
        var hud = tour.AddComponent<DistanceHUD>();
        var arrival = tour.AddComponent<ArrivalPanel>();
        GardenUiSettings.Configure(labels, selector, hud, arrival);
        var route = tour.AddComponent<RouteLine>();
        route.graph = graph;
        route.cameraHeight = EyeHeight;
        tour.GetComponent<LineRenderer>().positionCount = 0;
        var miniMap = tour.AddComponent<TourMiniMap>();
        miniMap.graph = graph;
        miniMap.poiManager = pois;
        miniMap.walkableFloor = walkableFloor;

        POIManager.CurrentIndex = -1;
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene, ScenePath)) throw new Exception("Could not save GardenPrototype scene.");
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        AssetDatabase.SaveAssets();
        Debug.Log($"GARDEN_SCENE_OK {ScenePath} | 3 POIs, {graph.nodes.Length} route nodes, one floor and one table collider");
    }

    [MenuItem("Garden Prototype/3. Expand walkable area")]
    public static void ExpandWalkableArea()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var tour = GameObject.Find("Garden Tour");
        if (tour == null) throw new Exception("Garden Tour was not found in GardenPrototype.");
        var graph = tour.GetComponent<RouteGraph>();
        var miniMap = tour.GetComponent<TourMiniMap>();
        var splat = UnityEngine.Object.FindFirstObjectByType<GaussianSplatRenderer>();
        if (graph == null || miniMap == null || splat == null || splat.m_Asset == null)
            throw new Exception("GardenPrototype needs RouteGraph, TourMiniMap and the Garden 3DGS asset.");

        var oldColliders = tour.transform.Find("Invisible walkway and perimeter colliders");
        if (oldColliders != null) UnityEngine.Object.DestroyImmediate(oldColliders.gameObject);
        oldColliders = tour.transform.Find("Walkable floor and table obstacle");
        if (oldColliders != null) UnityEngine.Object.DestroyImmediate(oldColliders.gameObject);
        miniMap.walkableFloor = CreateWalkableColliders(tour.transform, graph, splat);
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene, ScenePath)) throw new Exception("Could not save GardenPrototype scene.");
        Debug.Log($"GARDEN_WALKABLE_AREA_OK {ScenePath} | {miniMap.walkableFloor.size.x:F1} x {miniMap.walkableFloor.size.z:F1} floor, one table collider");
    }

    static Vector3 WalkPoint(Transform splat, GaussianSplatAsset.CameraInfo camera)
        => splat.TransformPoint(camera.pos) + Vector3.down * EyeHeight;

    static void AddPoi(POIManager manager, string id, string name, Vector3 position, string description)
    {
        manager.pois.Add(new POI
        {
            id = id, name = name, category = "观景位置", area = "Garden 技术原型", description = description,
            position = position, boundsSize = Vector3.one * 1.5f, viewpoints = new[] { position + Vector3.up * EyeHeight }
        });
    }

    static BoxCollider CreateWalkableColliders(Transform tour, RouteGraph graph, GaussianSplatRenderer splat)
    {
        if (graph.nodes == null || graph.nodes.Length == 0 || graph.nodes.Any(node => node == null))
            throw new Exception("RouteGraph needs valid nodes to position the floor.");
        float floorTop = graph.nodes.Min(node => node.position.y) - 0.05f;
        Bounds modelBounds = GetSplatWorldBounds(splat.transform, splat.m_Asset);
        var root = new GameObject("Walkable floor and table obstacle");
        root.transform.SetParent(tour);

        var floor = new GameObject("Walkable floor (resize Box Collider in Inspector)");
        floor.transform.SetParent(root.transform);
        floor.transform.position = new Vector3(modelBounds.center.x, floorTop - 0.1f, modelBounds.center.z);
        var floorCollider = floor.AddComponent<BoxCollider>();
        floorCollider.size = new Vector3(modelBounds.size.x + BoundaryPadding * 2f, 0.2f,
            modelBounds.size.z + BoundaryPadding * 2f);

        // The built-in cylinder mesh gives the round table one solid, invisible collider.
        var table = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        table.name = "Table obstacle";
        table.transform.SetParent(root.transform);
        table.transform.position = new Vector3(0f, floorTop + TableHeight * 0.5f, 0f);
        table.transform.localScale = new Vector3(TableRadius * 2f, TableHeight * 0.5f, TableRadius * 2f);
        Mesh cylinderMesh = table.GetComponent<MeshFilter>().sharedMesh;
        UnityEngine.Object.DestroyImmediate(table.GetComponent<CapsuleCollider>());
        table.GetComponent<MeshRenderer>().enabled = false;
        var tableCollider = table.AddComponent<MeshCollider>();
        tableCollider.sharedMesh = cylinderMesh;
        tableCollider.convex = true;
        return floorCollider;
    }

    static Bounds GetSplatWorldBounds(Transform splatTransform, GaussianSplatAsset asset)
    {
        Vector3 min = asset.boundsMin, max = asset.boundsMax;
        var bounds = new Bounds(splatTransform.TransformPoint(min), Vector3.zero);
        for (int x = 0; x < 2; x++)
        for (int y = 0; y < 2; y++)
        for (int z = 0; z < 2; z++)
            bounds.Encapsulate(splatTransform.TransformPoint(new Vector3(
                x == 0 ? min.x : max.x,
                y == 0 ? min.y : max.y,
                z == 0 ? min.z : max.z)));
        return bounds;
    }
}
