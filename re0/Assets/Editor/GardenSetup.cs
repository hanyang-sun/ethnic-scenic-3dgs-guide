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

        var colliders = new GameObject("Invisible walkway and perimeter colliders");
        colliders.transform.SetParent(tour.transform);
        for (int i = 0; i < graph.nodes.Length; i++)
            MakeFloorSegment(colliders.transform, graph.nodes[i].position, graph.nodes[(i + 1) % graph.nodes.Length].position, i);
        float averageFloor = graph.nodes.Average(n => n.position.y);
        for (int i = 0; i < 32; i++)
        {
            float a = i * Mathf.PI * 2f / 32f, b = (i + 1) * Mathf.PI * 2f / 32f;
            MakeWallSegment(colliders.transform, new Vector3(Mathf.Cos(a) * 2.45f, averageFloor, Mathf.Sin(a) * 2.45f),
                new Vector3(Mathf.Cos(b) * 2.45f, averageFloor, Mathf.Sin(b) * 2.45f), $"Table boundary {i}");
            MakeWallSegment(colliders.transform, new Vector3(Mathf.Cos(a) * 5.15f, averageFloor, Mathf.Sin(a) * 5.15f),
                new Vector3(Mathf.Cos(b) * 5.15f, averageFloor, Mathf.Sin(b) * 5.15f), $"Reconstruction boundary {i}");
        }

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

        POIManager.CurrentIndex = -1;
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene, ScenePath)) throw new Exception("Could not save GardenPrototype scene.");
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        AssetDatabase.SaveAssets();
        Debug.Log($"GARDEN_SCENE_OK {ScenePath} | 3 POIs, {graph.nodes.Length} route nodes, {colliders.transform.childCount} proxy colliders");
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

    static void MakeFloorSegment(Transform parent, Vector3 a, Vector3 b, int index)
    {
        Vector3 direction = b - a; direction.y = 0;
        var go = new GameObject($"Walkway {index}");
        go.transform.SetParent(parent);
        go.transform.position = (a + b) * 0.5f + Vector3.down * 0.1f;
        go.transform.rotation = Quaternion.LookRotation(direction);
        var box = go.AddComponent<BoxCollider>();
        box.size = new Vector3(2.7f, 0.2f, direction.magnitude + 0.6f);
    }

    static void MakeWallSegment(Transform parent, Vector3 a, Vector3 b, string name)
    {
        Vector3 direction = b - a;
        var go = new GameObject(name);
        go.transform.SetParent(parent);
        go.transform.position = (a + b) * 0.5f + Vector3.up * 0.5f;
        go.transform.rotation = Quaternion.LookRotation(direction);
        var box = go.AddComponent<BoxCollider>();
        box.size = new Vector3(0.15f, 2.4f, direction.magnitude + 0.1f);
    }
}
