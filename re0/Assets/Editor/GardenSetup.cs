using System;
using System.Linq;
using CSU.Tour;
using GaussianSplatting.Runtime;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;

public static class GardenSetup
{
    const string ScenePath = "Assets/Scenes/GardenPrototype.unity";
    const float EyeHeight = 1.65f;
    const float BoundaryPadding = 0.5f;
    const float TableRadius = 2.45f;
    const float TableHeight = 2.4f;

    public static void BuildAndVerify()
    {
        Build();
        GardenValidate.Validate();
        GardenCapture.Capture();
    }

    [MenuItem("Garden Prototype/2. Build teacher-style TourApp scene")]
    public static void Build()
    {
        var asset = AssetDatabase.LoadAssetAtPath<GaussianSplatAsset>(
            "Assets/GaussianAssets/Garden/garden.asset");
        if (asset == null || asset.cameras == null || asset.cameras.Length < 52)
            throw new Exception("Import Garden with cameras.json first (Garden Prototype > 1. Import 3DGS).");

        var scene = EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity", OpenSceneMode.Single);
        GameObject sampleVolume = GameObject.Find("Global Volume");
        if (sampleVolume != null) UnityEngine.Object.DestroyImmediate(sampleVolume);

        var splat = UnityEngine.Object.FindFirstObjectByType<GaussianSplatRenderer>();
        Camera camera = Camera.main;
        if (splat == null || camera == null)
            throw new Exception("SampleScene must contain a Gaussian renderer and Main Camera.");

        ConfigureSplatAndCamera(splat, asset, camera);

        Vector3[] nodes = Enumerable.Range(24, 28)
            .Select(index => WalkPoint(splat.transform, asset.cameras[index]))
            .ToArray();
        var edges = Enumerable.Range(0, nodes.Length)
            .Select(index => new TourEdge(index, (index + 1) % nodes.Length))
            .ToArray();

        GameObject player = BuildPlayer(camera, nodes[0]);
        CharacterController motor = player.GetComponent<CharacterController>();

        var tourObject = new GameObject("Tour - edit the settings here");
        TourApp app = tourObject.AddComponent<TourApp>();
        BoxCollider floor = CreateWalkableColliders(tourObject.transform, nodes, splat);

        app.player = player.transform;
        app.view = camera;
        app.motor = motor;
        app.splat = splat;
        app.ground = floor;
        app.startPosition = player.transform.position;
        app.startYaw = player.transform.eulerAngles.y;
        app.nodes = nodes;
        app.edges = edges;
        app.pois = new[]
        {
            CreatePoi("view_west", "西侧观景位", 7, nodes[7],
                "从西侧观察花园中央区域。"),
            CreatePoi("view_north", "北侧观景位", 15, nodes[15],
                "从另一角度观察花园与桌面细节。"),
            CreatePoi("view_east", "东侧观景位", 23, nodes[23],
                "沿环形路线到达东侧观察位置。")
        };
        app.speed = 1.7f;
        app.sprintMultiplier = 1.8f;
        app.replanInterval = 0.75f;
        app.obstacleLayer = 9;
        app.uiFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
            "Assets/Resources/NotoSansSC SDF.asset");

        SetLayerName(8, "WalkableGround");
        SetLayerName(9, "TourObstacle");
        Physics.IgnoreLayerCollision(0, 8, false);
        Physics.IgnoreLayerCollision(0, 9, false);

        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene, ScenePath))
            throw new Exception("Could not save GardenPrototype scene.");
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        AssetDatabase.SaveAssets();
        Debug.Log($"GARDEN_TOUR_APP_SCENE_OK {ScenePath} | one TourApp, 3 POIs, " +
                  $"{nodes.Length} route nodes, obstacle-aware Dijkstra and JSONL logging");
    }

    static void ConfigureSplatAndCamera(
        GaussianSplatRenderer splat,
        GaussianSplatAsset asset,
        Camera camera)
    {
        splat.name = "Garden 3DGS (visual only)";
        splat.m_Asset = asset;
        splat.transform.position = Vector3.zero;
        splat.transform.rotation = Quaternion.Euler(-154f, 0f, 0f);
        splat.transform.localScale = new Vector3(1f, 1f, -1f);

        camera.name = "Main Camera";
        camera.tag = "MainCamera";
        camera.nearClipPlane = 0.05f;
        camera.farClipPlane = 200f;
        camera.fieldOfView = 60f;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.56f, 0.69f, 0.79f, 1f);
        camera.GetUniversalAdditionalCameraData().renderPostProcessing = false;
        splat.ActivateCamera(24);
    }

    static GameObject BuildPlayer(Camera camera, Vector3 startPosition)
    {
        CharacterController oldController = camera.GetComponent<CharacterController>();
        if (oldController != null) UnityEngine.Object.DestroyImmediate(oldController);

        Vector3 forward = Vector3.ProjectOnPlane(camera.transform.forward, Vector3.up);
        if (forward.sqrMagnitude < 0.0001f) forward = Vector3.forward;
        float yaw = Quaternion.LookRotation(forward.normalized, Vector3.up).eulerAngles.y;

        var player = new GameObject("Player");
        player.transform.SetPositionAndRotation(startPosition, Quaternion.Euler(0f, yaw, 0f));
        camera.transform.SetParent(player.transform, false);
        camera.transform.localPosition = Vector3.up * EyeHeight;
        camera.transform.localRotation = Quaternion.Euler(8f, 0f, 0f);

        CharacterController controller = player.AddComponent<CharacterController>();
        controller.height = EyeHeight;
        controller.center = Vector3.up * EyeHeight * 0.5f;
        controller.radius = 0.25f;
        controller.stepOffset = 0.3f;
        controller.skinWidth = 0.03f;
        return player;
    }

    [MenuItem("Garden Prototype/3. Expand walkable area")]
    public static void ExpandWalkableArea()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        TourApp app = UnityEngine.Object.FindFirstObjectByType<TourApp>();
        var splat = UnityEngine.Object.FindFirstObjectByType<GaussianSplatRenderer>();
        if (app == null || splat == null || splat.m_Asset == null)
            throw new Exception("GardenPrototype needs TourApp and the Garden 3DGS asset.");

        Transform oldColliders = app.transform.Find("Walkable floor and table obstacle");
        if (oldColliders != null) UnityEngine.Object.DestroyImmediate(oldColliders.gameObject);
        app.ground = CreateWalkableColliders(app.transform, app.nodes, splat);

        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene, ScenePath))
            throw new Exception("Could not save GardenPrototype scene.");
        Debug.Log($"GARDEN_WALKABLE_AREA_OK {ScenePath} | {app.ground.bounds.size.x:F1} x " +
                  $"{app.ground.bounds.size.z:F1} floor, one TourObstacle table collider");
    }

    static Vector3 WalkPoint(Transform splat, GaussianSplatAsset.CameraInfo camera)
        => splat.TransformPoint(camera.pos) + Vector3.down * EyeHeight;

    static TourPoi CreatePoi(string id, string title, int node, Vector3 position, string description)
    {
        return new TourPoi
        {
            id = id,
            title = title,
            category = "Garden 技术原型",
            description = description,
            node = node,
            visualBounds = new Bounds(position + Vector3.up * 0.9f, new Vector3(2.5f, 1.8f, 2.5f)),
            arrivalOffset = new Vector3(0f, 0.8f, 0f),
            arrivalSize = new Vector3(2f, 2f, 2f)
        };
    }

    static BoxCollider CreateWalkableColliders(
        Transform tour,
        Vector3[] nodes,
        GaussianSplatRenderer splat)
    {
        if (nodes == null || nodes.Length == 0)
            throw new Exception("TourApp needs valid nodes to position the floor.");

        float floorTop = nodes.Min(node => node.y) - 0.05f;
        Bounds modelBounds = GetSplatWorldBounds(splat.transform, splat.m_Asset);
        var root = new GameObject("Walkable floor and table obstacle");
        root.transform.SetParent(tour);

        var floor = new GameObject("Walkable floor (resize Box Collider in Inspector)");
        floor.layer = 8;
        floor.transform.SetParent(root.transform);
        floor.transform.position = new Vector3(modelBounds.center.x, floorTop - 0.1f, modelBounds.center.z);
        BoxCollider floorCollider = floor.AddComponent<BoxCollider>();
        floorCollider.size = new Vector3(
            modelBounds.size.x + BoundaryPadding * 2f,
            0.2f,
            modelBounds.size.z + BoundaryPadding * 2f);

        GameObject table = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        table.name = "Table obstacle";
        table.layer = 9;
        table.transform.SetParent(root.transform);
        table.transform.position = new Vector3(0f, floorTop + TableHeight * 0.5f, 0f);
        table.transform.localScale = new Vector3(TableRadius * 2f, TableHeight * 0.5f, TableRadius * 2f);
        Mesh cylinderMesh = table.GetComponent<MeshFilter>().sharedMesh;
        UnityEngine.Object.DestroyImmediate(table.GetComponent<CapsuleCollider>());
        table.GetComponent<MeshRenderer>().enabled = false;
        MeshCollider tableCollider = table.AddComponent<MeshCollider>();
        tableCollider.sharedMesh = cylinderMesh;
        tableCollider.convex = true;
        return floorCollider;
    }

    static Bounds GetSplatWorldBounds(Transform splatTransform, GaussianSplatAsset asset)
    {
        Vector3 min = asset.boundsMin;
        Vector3 max = asset.boundsMax;
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

    static void SetLayerName(int index, string name)
    {
        UnityEngine.Object tagManager = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0];
        var serialized = new SerializedObject(tagManager);
        SerializedProperty layers = serialized.FindProperty("layers");
        layers.GetArrayElementAtIndex(index).stringValue = name;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }
}
