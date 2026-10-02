using System;
using System.Collections.Generic;
using GaussianSplatting.Runtime;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace CSU.Tour
{
    /// <summary>
    /// 导览系统唯一运行时控制中心。玩家、目标、路线、到达、UI 和日志都从这里取状态。
    /// </summary>
    public sealed class TourApp : MonoBehaviour
    {
        [Header("场景引用")]
        public Transform player;
        public Camera view;
        public CharacterController motor;
        public GaussianSplatRenderer splat;
        public Collider ground;
        public Material routeMaterial;
        public TMP_FontAsset uiFont;

        [Header("场景标识与界面文案")]
        public string sceneId = "garden_re0";
        public string sceneTag = "GARDEN / 3DGS";
        public string sceneTitle = "花园沉浸式导览";
        public string sceneSubtitle = "TourApp 集中式运行框架";
        public string SceneId => string.IsNullOrWhiteSpace(sceneId) ? "garden_re0" : sceneId;
        public string SceneTag => string.IsNullOrWhiteSpace(sceneTag) ? "GARDEN / 3DGS" : sceneTag;
        public string SceneTitle => string.IsNullOrWhiteSpace(sceneTitle) ? "花园沉浸式导览" : sceneTitle;
        public string SceneSubtitle => string.IsNullOrWhiteSpace(sceneSubtitle)
            ? "TourApp 集中式运行框架" : sceneSubtitle;

        [Header("出生点与人工路网（Unity 世界坐标）")]
        public Vector3 startPosition;
        public float startYaw;
        public Vector3[] nodes = Array.Empty<Vector3>();
        public TourEdge[] edges = Array.Empty<TourEdge>();
        public TourPoi[] pois = Array.Empty<TourPoi>();

        [Header("导航")]
        [Range(0.3f, 5f)] public float speed = 1.7f;
        [Range(1f, 4f)] public float sprintMultiplier = 1.8f;
        [Range(0.2f, 3f)] public float replanInterval = 0.75f;
        [Range(0, 31)] public int obstacleLayer = 9;

        [Header("显示")]
        public bool showPoiBounds;
        public bool showRoute = true;

        public bool Arrived { get; private set; }
        public float TravelDistance { get; private set; }
        public float PlannedLength { get; private set; }
        public float RemainingDistance => route.Count > 1 ? PolylineLength(route) : 0f;
        public float FrameTimeMs => smoothedDeltaTime * 1000f;
        public string RouteId { get; private set; } = "";
        public int RouteRevision { get; private set; }
        public int TourIndex { get; private set; }
        public float TargetSelectedAt { get; private set; }
        public string Status { get; private set; } = "请选择一个目的地。";
        public int Selected { get; private set; } = -1;
        public bool paused;
        public IReadOnlyList<Vector3> Route => route;
        public bool[] Visited { get; private set; }
        public TourLog Log { get; private set; }
        public TourHud Hud { get; private set; }

        public event Action StateChanged;

        readonly List<Vector3> route = new List<Vector3>();
        bool[] inside;
        LineRenderer routeLine;
        Material ownedRouteMaterial;
        float pitch = 8f;
        float nextPose;
        float nextFlush;
        float nextReplan;
        float smoothedDeltaTime;
        bool configured;

        public static float PolylineLength(IReadOnlyList<Vector3> points)
        {
            float distance = 0f;
            for (int i = 1; i < points.Count; i++)
                distance += Vector3.Distance(points[i - 1], points[i]);
            return distance;
        }

        void Awake()
        {
            if (!ValidateConfiguration())
            {
                enabled = false;
                return;
            }

            configured = true;
            Visited = new bool[pois.Length];
            inside = new bool[pois.Length];
            Application.targetFrameRate = 60;

            CreateRouteRenderer();
            Log = new TourLog(this);
            Log.Write(
                "session_start",
                SystemInfo.graphicsDeviceName + " | " + SystemInfo.graphicsDeviceType +
                " | scene=" + SceneId + " | path=graph_dijkstra_obstacle_filtered | arrival=body_in_box",
                view.transform);

            ResetTour();
            Hud = gameObject.AddComponent<TourHud>();
            Hud.Initialize(this, uiFont);
            gameObject.AddComponent<PoiBoundsOverlay>().Initialize(this);
        }

        bool ValidateConfiguration()
        {
            if (player == null || view == null || motor == null || splat == null || ground == null ||
                nodes == null || nodes.Length < 2 || edges == null || pois == null || pois.Length == 0)
            {
                Status = "场景配置不完整，请检查 TourApp 的 Inspector 引用。";
                Debug.LogError("[TourApp] " + Status);
                return false;
            }

            var ids = new HashSet<string>();
            foreach (TourPoi poi in pois)
            {
                if (poi == null || string.IsNullOrWhiteSpace(poi.id) || !ids.Add(poi.id) ||
                    poi.node < 0 || poi.node >= nodes.Length || poi.arrivalSize.x <= 0f ||
                    poi.arrivalSize.y <= 0f || poi.arrivalSize.z <= 0f ||
                    poi.visualBounds.size.sqrMagnitude <= 0f)
                {
                    Status = "POI 配置无效：请检查唯一 ID、节点索引和包围盒尺寸。";
                    Debug.LogError("[TourApp] " + Status);
                    return false;
                }
            }
            return true;
        }

        void CreateRouteRenderer()
        {
            var routeObject = new GameObject("Active route");
            routeObject.transform.SetParent(transform, false);
            routeLine = routeObject.AddComponent<LineRenderer>();
            routeLine.useWorldSpace = true;
            routeLine.startWidth = routeLine.endWidth = 0.06f;
            routeLine.numCornerVertices = 4;
            routeLine.numCapVertices = 4;

            if (routeMaterial != null)
            {
                routeLine.sharedMaterial = routeMaterial;
                return;
            }

            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            if (shader == null) return;
            ownedRouteMaterial = new Material(shader);
            SetMaterialColor(ownedRouteMaterial, new Color(0.18f, 0.93f, 0.77f, 0.95f));
            routeLine.sharedMaterial = ownedRouteMaterial;
        }

        internal static void SetMaterialColor(Material material, Color color)
        {
            if (material == null) return;
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color")) material.SetColor("_Color", color);
        }

        public void ReleasePointer()
        {
            if (Application.isBatchMode) return;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        public void ResetTour()
        {
            if (!configured) return;
            if (inside != null)
                for (int i = 0; i < inside.Length; i++)
                    if (inside[i]) Log?.Write("poi_leave", "reset", view.transform, pois[i].id);

            paused = false;
            Selected = -1;
            Arrived = false;
            route.Clear();
            UpdateRouteLine();
            TourIndex++;
            TravelDistance = 0f;
            PlannedLength = 0f;
            RouteId = "";
            RouteRevision = 0;
            nextReplan = 0f;
            Array.Clear(inside, 0, inside.Length);
            Array.Clear(Visited, 0, Visited.Length);
            Hud?.CloseHelp();

            motor.enabled = false;
            player.SetPositionAndRotation(startPosition, Quaternion.Euler(0f, startYaw, 0f));
            motor.enabled = true;
            pitch = 8f;
            view.transform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
            ReleasePointer();
            Status = "请选择一个目的地。";
            Log?.Write("reset", "", view.transform);
            StateChanged?.Invoke();
        }

        public void SetPaused(bool value)
        {
            if (paused == value) return;
            paused = value;
            ReleasePointer();
            Log?.Write(paused ? "pause" : "resume", "", view.transform);
            StateChanged?.Invoke();
        }

        public void ToggleHelp()
        {
            if (Hud == null) return;
            if (Hud.HelpVisible) Hud.CloseHelp();
            else Hud.OpenHelp();
        }

        public bool SegmentValid(Vector3 a, Vector3 b)
        {
            int obstacles = 1 << obstacleLayer;
            Vector3 bottomA = a + Vector3.up * 0.35f;
            Vector3 topA = bottomA + Vector3.up * 0.95f;
            Vector3 bottomB = b + Vector3.up * 0.35f;
            Vector3 topB = bottomB + Vector3.up * 0.95f;

            if (Physics.CheckCapsule(bottomA, topA, 0.22f, obstacles, QueryTriggerInteraction.Ignore) ||
                Physics.CheckCapsule(bottomB, topB, 0.22f, obstacles, QueryTriggerInteraction.Ignore))
                return false;

            Vector3 delta = b - a;
            float length = delta.magnitude;
            return length < 0.0001f ||
                !Physics.CapsuleCast(bottomA, topA, 0.22f, delta / length, length,
                    obstacles, QueryTriggerInteraction.Ignore);
        }

        public bool SelectPoi(int index)
        {
            if (index < 0 || index >= pois.Length) return false;

            Selected = index;
            Arrived = false;
            route.Clear();
            PlannedLength = 0f;
            RouteId = Guid.NewGuid().ToString("N");
            RouteRevision = 0;
            TargetSelectedAt = Time.unscaledTime;
            Log?.Write("target_selected", "manual", view.transform, pois[index].id);

            bool reachable = PlanRoute();
            nextReplan = Time.unscaledTime + replanInterval;
            Status = reachable
                ? "请沿高亮路线前往“" + pois[index].title + "”。"
                : "当前没有可达路线，请返回道路或按 R 重置。";
            Log?.Write("route_selected", reachable ? "reachable" : "unreachable", view.transform, pois[index].id);
            StateChanged?.Invoke();
            return reachable;
        }

        bool PlanRoute()
        {
            route.Clear();
            if (Selected < 0) return false;

            // 起点只能接入附近路网；否则无障碍时会直接连到目标节点，绕过整张路径图。
            float nearestNodeDistance = float.PositiveInfinity;
            var connectorDistances = new float[nodes.Length];
            for (int i = 0; i < nodes.Length; i++)
            {
                connectorDistances[i] = SegmentValid(player.position, nodes[i])
                    ? Vector3.Distance(player.position, nodes[i])
                    : float.PositiveInfinity;
                nearestNodeDistance = Mathf.Min(nearestNodeDistance, connectorDistances[i]);
            }

            float bestCost = float.PositiveInfinity;
            List<int> bestPath = null;
            for (int candidate = 0; candidate < nodes.Length; candidate++)
            {
                if (connectorDistances[candidate] > nearestNodeDistance + 0.75f) continue;
                List<int> path = TourGraph.Shortest(nodes, edges, candidate, pois[Selected].node, SegmentValid);
                if (path.Count == 0) continue;

                float cost = Vector3.Distance(player.position, nodes[candidate]);
                for (int i = 1; i < path.Count; i++)
                    cost += Vector3.Distance(nodes[path[i - 1]], nodes[path[i]]);

                if (cost < bestCost)
                {
                    bestCost = cost;
                    bestPath = path;
                }
            }

            if (bestPath != null)
            {
                route.Add(player.position);
                foreach (int node in bestPath) route.Add(nodes[node]);
            }

            PlannedLength = PolylineLength(route);
            RouteRevision++;
            UpdateRouteLine();
            Log?.Write("route_updated", route.Count > 0 ? "graph_dijkstra" : "unreachable", view.transform);
            StateChanged?.Invoke();
            return route.Count > 0;
        }

        void UpdateRouteLine()
        {
            if (routeLine == null) return;
            routeLine.enabled = showRoute;
            routeLine.positionCount = route.Count;
            for (int i = 0; i < route.Count; i++)
                routeLine.SetPosition(i, route[i] + Vector3.up * 0.08f);
        }

        public void MoveWorld(Vector3 direction, float deltaTime, bool sprint)
        {
            if (paused) return;
            Vector3 previous = player.position;
            float movementSpeed = speed * (sprint ? sprintMultiplier : 1f);
            Vector3 displacement = Vector3.ClampMagnitude(Vector3.ProjectOnPlane(direction, Vector3.up), 1f) *
                                   movementSpeed * Mathf.Clamp(deltaTime, 0f, 0.05f);
            motor.Move(displacement);
            TravelDistance += Vector3.ProjectOnPlane(player.position - previous, Vector3.up).magnitude;
        }

        void Update()
        {
            if (!configured) return;
            smoothedDeltaTime = Mathf.Lerp(smoothedDeltaTime, Time.unscaledDeltaTime, 0.05f);
            HandleInput();
            if (!paused) UpdateTourState();

            if (Time.unscaledTime >= nextPose)
            {
                nextPose = Time.unscaledTime + 0.1f;
                Log?.Write("pose", "desktop", view.transform);
            }
            if (Time.unscaledTime >= nextFlush)
            {
                nextFlush = Time.unscaledTime + 2f;
                Log?.Flush();
            }
        }

        void HandleInput()
        {
            Keyboard keyboard = Keyboard.current;
            Mouse mouse = Mouse.current;
            if (Application.isBatchMode || keyboard == null) return;

            if (keyboard.rKey.wasPressedThisFrame) ResetTour();
            if (keyboard.hKey.wasPressedThisFrame) ToggleHelp();
            if (keyboard.bKey.wasPressedThisFrame)
            {
                showPoiBounds = !showPoiBounds;
                Log?.Write("bounds_overlay", showPoiBounds ? "on" : "off", view.transform);
            }
            if (keyboard.escapeKey.wasPressedThisFrame)
            {
                if (Hud != null && Hud.HelpVisible) Hud.CloseHelp();
                else SetPaused(!paused);
            }
            if (keyboard.digit1Key.wasPressedThisFrame) SelectPoi(0);
            if (keyboard.digit2Key.wasPressedThisFrame) SelectPoi(1);
            if (keyboard.digit3Key.wasPressedThisFrame) SelectPoi(2);
            if (paused) return;

            bool looking = mouse != null && mouse.rightButton.isPressed &&
                           (EventSystem.current == null || !EventSystem.current.IsPointerOverGameObject());
            Cursor.lockState = looking ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !looking;
            if (looking)
            {
                Vector2 delta = mouse.delta.ReadValue() * 0.1f;
                player.Rotate(0f, delta.x, 0f);
                pitch = Mathf.Clamp(pitch - delta.y, -75f, 75f);
                view.transform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
            }

            Vector2 axis = new Vector2(
                (keyboard.dKey.isPressed ? 1f : 0f) - (keyboard.aKey.isPressed ? 1f : 0f),
                (keyboard.wKey.isPressed ? 1f : 0f) - (keyboard.sKey.isPressed ? 1f : 0f));
            MoveWorld(player.right * axis.x + player.forward * axis.y,
                Time.unscaledDeltaTime, keyboard.leftShiftKey.isPressed);
        }

        void UpdateTourState()
        {
            for (int i = 0; i < pois.Length; i++)
            {
                bool isInside = pois[i].ArrivalBounds(nodes[pois[i].node])
                    .Contains(player.position + Vector3.up * 0.8f);
                if (isInside == inside[i]) continue;
                inside[i] = isInside;
                Log?.Write(isInside ? "poi_enter" : "poi_leave", "arrival_volume", view.transform, pois[i].id);
            }

            if (Selected < 0 || Arrived) return;
            if (inside[Selected])
            {
                Arrived = true;
                Visited[Selected] = true;
                route.Clear();
                UpdateRouteLine();
                Status = "已到达：“" + pois[Selected].title + "”。";
                Log?.Write("poi_arrival", "arrival is not viewing completion", view.transform, pois[Selected].id);
                StateChanged?.Invoke();
                return;
            }

            while (route.Count > 2 &&
                   Vector3.ProjectOnPlane(route[1] - player.position, Vector3.up).magnitude < 0.25f)
                route.RemoveAt(1);
            if (route.Count > 0) route[0] = player.position;

            if (Time.unscaledTime >= nextReplan)
            {
                nextReplan = Time.unscaledTime + replanInterval;
                if (!PlanRoute()) Status = "路线不可用，请返回道路或按 R 重置。";
            }
            UpdateRouteLine();
        }

        void OnApplicationFocus(bool focus)
        {
            if (!focus && !Application.isBatchMode) SetPaused(true);
        }

        void OnApplicationQuit()
        {
            Log?.Write("session_end", "", view.transform);
            Log?.Flush();
        }

        void OnDisable()
        {
            ReleasePointer();
            Log?.Dispose();
        }

        void OnDestroy()
        {
            if (ownedRouteMaterial != null) Destroy(ownedRouteMaterial);
        }
    }
}
