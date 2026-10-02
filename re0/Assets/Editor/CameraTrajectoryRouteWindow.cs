using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using CSU.Tour;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CSU.Tour.EditorTools
{
    /// <summary>
    /// 从 3DGS 的 cameras.json 生成可人工复核的路网候选；不会自动修改 POI 或场景。
    /// </summary>
    public sealed class CameraTrajectoryRouteWindow : EditorWindow
    {
        [Serializable]
        sealed class CameraFile
        {
            public CameraRecord[] cameras = Array.Empty<CameraRecord>();
        }

        [Serializable]
        sealed class CameraRecord
        {
            public string img_name = "";
            public float[] position = Array.Empty<float>();
        }

        struct CameraSample
        {
            public int frame;
            public Vector3 position;
        }

        sealed class RoutePreview
        {
            public Vector3[] nodes;
            public TourEdge[] edges;
            public int sourceCount;
            public int selectedCount;
            public int projectedCount;
            public int groundMisses;
            public int obstaclePoints;
            public int disconnectedSteps;
            public int singlePointRuns;
            public bool loopClosed;
        }

        TourApp app;
        string cameraFilePath = "";
        string message = "";
        RoutePreview preview;
        Vector2 scroll;

        int firstFrame = 1;
        int lastFrame = 90;
        int maxFrameGap = 3;
        float minimumCameraSpacing = 0.08f;
        float maximumCameraJump = 2.5f;
        float maximumEdgeLength = 2.5f;
        float maximumDeviation = 0.25f;
        float maximumHeightChange = 0.35f;
        float groundOffset;
        bool closeLoop;
        bool preservePois = true;
        float maximumPoiRemapDistance = 1.5f;

        [MenuItem("Tools/导览/相机轨迹生成路网")]
        public static void Open()
        {
            GetWindow<CameraTrajectoryRouteWindow>("相机轨迹路网");
        }

        void OnEnable()
        {
            SceneView.duringSceneGui += DrawPreview;
            if (app != null) return;
            foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
            {
                app = root.GetComponentInChildren<TourApp>(true);
                if (app != null) break;
            }
        }

        void OnDisable()
        {
            SceneView.duringSceneGui -= DrawPreview;
        }

        void OnGUI()
        {
            scroll = EditorGUILayout.BeginScrollView(scroll);
            EditorGUILayout.HelpBox(
                "读取 3DGS cameras.json，按帧号排序、投影到物理地面，再生成待检查的节点与边。" +
                "预览不会修改场景；相机轨迹不等于已验证的可行走区域。", MessageType.Info);

            EditorGUI.BeginChangeCheck();
            app = (TourApp)EditorGUILayout.ObjectField("目标 TourApp", app, typeof(TourApp), true);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PrefixLabel("cameras.json");
            EditorGUILayout.SelectableLabel(cameraFilePath, EditorStyles.textField,
                GUILayout.Height(EditorGUIUtility.singleLineHeight));
            if (GUILayout.Button("选择文件", GUILayout.Width(82)))
            {
                string initialDirectory = Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;
                string selected = EditorUtility.OpenFilePanel("选择 3DGS 相机位姿", initialDirectory, "json");
                if (!string.IsNullOrEmpty(selected)) cameraFilePath = selected;
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("轨迹范围", EditorStyles.boldLabel);
            firstFrame = EditorGUILayout.IntField("起始帧", firstFrame);
            lastFrame = EditorGUILayout.IntField("结束帧（0 = 全部）", lastFrame);
            maxFrameGap = EditorGUILayout.IntSlider("允许的最大帧号间隔", maxFrameGap, 1, 30);
            EditorGUILayout.HelpBox("默认只取前 90 帧，适合先预览一圈；换场景后请检查帧范围。", MessageType.None);

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("简化与安全边界（Unity 世界单位）", EditorStyles.boldLabel);
            minimumCameraSpacing = EditorGUILayout.Slider("忽略相邻抖动", minimumCameraSpacing, 0.01f, 0.5f);
            maximumCameraJump = EditorGUILayout.Slider("最大相机跳距", maximumCameraJump, 0.5f, 10f);
            maximumEdgeLength = EditorGUILayout.Slider("最大路网边长", maximumEdgeLength, 0.5f, 10f);
            maximumDeviation = EditorGUILayout.Slider("允许的路径偏差", maximumDeviation, 0.02f, 1f);
            maximumHeightChange = EditorGUILayout.Slider("相邻点最大高差", maximumHeightChange, 0.05f, 2f);
            groundOffset = EditorGUILayout.FloatField("节点离地偏移", groundOffset);
            closeLoop = EditorGUILayout.Toggle("首尾接成闭环", closeLoop);

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("应用到现有配置", EditorStyles.boldLabel);
            preservePois = EditorGUILayout.Toggle("保留 POI 并重映射", preservePois);
            if (preservePois)
                maximumPoiRemapDistance = EditorGUILayout.FloatField("POI 最大重映射距离", maximumPoiRemapDistance);
            else
                EditorGUILayout.HelpBox("关闭后，应用时会清空已有 POI；请之后重新标注。", MessageType.Warning);

            if (EditorGUI.EndChangeCheck()) InvalidatePreview();

            EditorGUILayout.Space(8);
            using (new EditorGUI.DisabledScope(EditorApplication.isPlaying))
            {
                if (GUILayout.Button("生成预览", GUILayout.Height(30))) GeneratePreview();
                using (new EditorGUI.DisabledScope(preview == null))
                    if (GUILayout.Button("确认并写入 TourApp", GUILayout.Height(30))) ApplyPreview();
            }

            if (!string.IsNullOrEmpty(message))
                EditorGUILayout.HelpBox(message, preview == null ? MessageType.Warning : MessageType.Info);
            if (preview != null)
            {
                EditorGUILayout.LabelField("预览结果", EditorStyles.boldLabel);
                EditorGUILayout.LabelField($"读取 {preview.sourceCount} 帧；范围内 {preview.selectedCount} 帧；落地 {preview.projectedCount} 点");
                EditorGUILayout.LabelField($"候选路网：{preview.nodes.Length} 节点、{preview.edges.Length} 边" +
                                           (preview.loopClosed ? "（已闭环）" : ""));
                EditorGUILayout.LabelField($"无地面 {preview.groundMisses}；位于障碍内 {preview.obstaclePoints}；" +
                                           $"断开的轨迹步 {preview.disconnectedSteps}；孤立点段 {preview.singlePointRuns}");
                if (app != null && app.nodes != null && app.nodes.Length > 0)
                    EditorGUILayout.HelpBox("写入会替换当前 Nodes/Edges。请先在 Scene 视图检查橙色预览线；" +
                                            "现有 POI 只能按旧节点位置近邻重映射，不能自动识别展品。", MessageType.Warning);
            }
            EditorGUILayout.EndScrollView();
        }

        void InvalidatePreview()
        {
            preview = null;
            message = "参数已变更，请重新生成预览。";
            SceneView.RepaintAll();
        }

        void GeneratePreview()
        {
            preview = null;
            if (app == null || app.splat == null || app.ground == null)
            {
                message = "请指定场景中的 TourApp，并先配置 3DGS Renderer 与物理地面 Collider。";
                return;
            }
            if (app.gameObject.scene != SceneManager.GetActiveScene())
            {
                message = "目标 TourApp 必须位于当前活动场景。";
                return;
            }
            if (string.IsNullOrWhiteSpace(cameraFilePath) || !File.Exists(cameraFilePath))
            {
                message = "请选择存在的 cameras.json 文件。";
                return;
            }
            if (firstFrame < 0 || (lastFrame != 0 && lastFrame < firstFrame) ||
                maximumPoiRemapDistance < 0f || maximumCameraJump <= 0f ||
                maximumEdgeLength <= 0f || maximumDeviation <= 0f)
            {
                message = "请检查帧范围与距离参数，数值必须有效。";
                return;
            }

            try
            {
                CameraSample[] samples = ReadCameras(cameraFilePath);
                preview = BuildPreview(app, samples);
                message = preview.edges.Length > 0
                    ? "预览已生成。橙色为候选路段；断开的轨迹不会被自动补成通路。"
                    : "没有生成可通行的边。请检查帧范围、地面范围和障碍碰撞体。";
                if (preview.edges.Length == 0) preview = null;
            }
            catch (Exception exception)
            {
                message = "读取或生成失败：" + exception.Message;
                preview = null;
            }
            SceneView.RepaintAll();
        }

        static CameraSample[] ReadCameras(string path)
        {
            string json = File.ReadAllText(path).TrimStart('\uFEFF', ' ', '\t', '\r', '\n');
            if (!json.StartsWith("[", StringComparison.Ordinal))
                throw new FormatException("需要顶层数组格式的 cameras.json。");

            CameraFile data = JsonUtility.FromJson<CameraFile>("{\"cameras\":" + json + "}");
            if (data?.cameras == null || data.cameras.Length == 0)
                throw new FormatException("文件中没有相机记录。");

            var samples = new List<CameraSample>(data.cameras.Length);
            var seenFrames = new HashSet<int>();
            foreach (CameraRecord record in data.cameras)
            {
                if (record?.position == null || record.position.Length != 3) continue;
                string name = Path.GetFileNameWithoutExtension(record.img_name ?? "");
                Match frameNumber = Regex.Match(name, @"\d+$");
                if (!frameNumber.Success || !int.TryParse(frameNumber.Value, out int frame) ||
                    seenFrames.Contains(frame)) continue;
                Vector3 point = new Vector3(record.position[0], record.position[1], record.position[2]);
                if (float.IsNaN(point.x) || float.IsNaN(point.y) || float.IsNaN(point.z) ||
                    float.IsInfinity(point.x) || float.IsInfinity(point.y) || float.IsInfinity(point.z)) continue;
                seenFrames.Add(frame);
                samples.Add(new CameraSample { frame = frame, position = point });
            }
            if (samples.Count < 2)
                throw new FormatException("至少需要两帧带数字文件名和三维 position 的相机记录。");
            samples.Sort((a, b) => a.frame.CompareTo(b.frame));
            return samples.ToArray();
        }

        RoutePreview BuildPreview(TourApp target, CameraSample[] samples)
        {
            var result = new RoutePreview { sourceCount = samples.Length };
            var runs = new List<List<CameraSample>>();
            List<CameraSample> current = null;
            Physics.SyncTransforms();

            foreach (CameraSample sample in samples)
            {
                if (sample.frame < firstFrame || (lastFrame != 0 && sample.frame > lastFrame)) continue;
                result.selectedCount++;

                Vector3 world = target.splat.transform.TransformPoint(sample.position);
                Ray ray = new Ray(world + Vector3.up * 20f, Vector3.down);
                if (!target.ground.Raycast(ray, out RaycastHit hit, 100f))
                {
                    result.groundMisses++;
                    current = null;
                    continue;
                }
                Vector3 node = hit.point + Vector3.up * groundOffset;
                if (!target.SegmentValid(node, node))
                {
                    result.obstaclePoints++;
                    current = null;
                    continue;
                }

                result.projectedCount++;
                if (current != null && current.Count > 0)
                {
                    CameraSample previous = current[current.Count - 1];
                    float distance = Vector3.Distance(previous.position, node);
                    if (distance < minimumCameraSpacing) continue;
                    if (sample.frame - previous.frame > maxFrameGap ||
                        distance > maximumCameraJump || distance > maximumEdgeLength ||
                        Mathf.Abs(previous.position.y - node.y) > maximumHeightChange ||
                        !target.SegmentValid(previous.position, node))
                    {
                        result.disconnectedSteps++;
                        current = null;
                    }
                }
                if (current == null)
                {
                    current = new List<CameraSample>();
                    runs.Add(current);
                }
                current.Add(new CameraSample { frame = sample.frame, position = node });
            }

            var nodes = new List<Vector3>();
            var edges = new List<TourEdge>();
            int usefulRuns = 0;
            foreach (List<CameraSample> run in runs)
            {
                if (run.Count < 2)
                {
                    result.singlePointRuns++;
                    continue;
                }
                usefulRuns++;
                int previousNode = nodes.Count;
                nodes.Add(run[0].position);
                int start = 0;
                while (start < run.Count - 1)
                {
                    int best = start + 1;
                    for (int candidate = start + 2; candidate < run.Count; candidate++)
                    {
                        Vector3 from = run[start].position;
                        Vector3 to = run[candidate].position;
                        if (Vector3.Distance(from, to) > maximumEdgeLength ||
                            MaximumDeviation(run, start, candidate) > maximumDeviation ||
                            !target.SegmentValid(from, to)) break;
                        best = candidate;
                    }
                    int nextNode = nodes.Count;
                    nodes.Add(run[best].position);
                    edges.Add(new TourEdge(previousNode, nextNode));
                    previousNode = nextNode;
                    start = best;
                }
            }

            if (closeLoop && usefulRuns == 1 && nodes.Count > 2 &&
                Vector3.Distance(nodes[0], nodes[nodes.Count - 1]) <= maximumEdgeLength &&
                target.SegmentValid(nodes[nodes.Count - 1], nodes[0]))
            {
                edges.Add(new TourEdge(nodes.Count - 1, 0));
                result.loopClosed = true;
            }
            result.nodes = nodes.ToArray();
            result.edges = edges.ToArray();
            return result;
        }

        static float MaximumDeviation(List<CameraSample> run, int first, int last)
        {
            Vector3 a = run[first].position;
            Vector3 b = run[last].position;
            Vector3 ab = b - a;
            float lengthSquared = ab.sqrMagnitude;
            float maximum = 0f;
            for (int i = first + 1; i < last; i++)
            {
                float t = lengthSquared > 0.000001f
                    ? Mathf.Clamp01(Vector3.Dot(run[i].position - a, ab) / lengthSquared)
                    : 0f;
                maximum = Mathf.Max(maximum, Vector3.Distance(run[i].position, a + t * ab));
            }
            return maximum;
        }

        void ApplyPreview()
        {
            if (preview == null || app == null || EditorApplication.isPlaying) return;
            if (app.gameObject.scene != SceneManager.GetActiveScene())
            {
                InvalidatePreview();
                message = "目标场景已变化，请重新生成预览。";
                return;
            }

            var poiNodes = new List<int>();
            if (preservePois && app.pois != null)
            {
                foreach (TourPoi poi in app.pois)
                {
                    if (poi == null || app.nodes == null || poi.node < 0 || poi.node >= app.nodes.Length)
                    {
                        message = "已有 POI 的节点索引无效。请修正 POI，或关闭“保留 POI”后重新预览。";
                        return;
                    }
                    Vector3 oldPosition = app.nodes[poi.node];
                    int nearest = -1;
                    float nearestDistance = float.PositiveInfinity;
                    for (int i = 0; i < preview.nodes.Length; i++)
                    {
                        float distance = Vector3.Distance(oldPosition, preview.nodes[i]);
                        if (distance >= nearestDistance) continue;
                        nearest = i;
                        nearestDistance = distance;
                    }
                    if (nearest < 0 || nearestDistance > maximumPoiRemapDistance)
                    {
                        message = $"POI“{poi.title}”离新路网最近节点仍有 {nearestDistance:0.00} 单位，" +
                                  "已取消写入。请调整轨迹范围或明确选择清空 POI。";
                        return;
                    }
                    poiNodes.Add(nearest);
                }
            }

            string poiAction = preservePois ? "按原位置重映射现有 POI" : "清空现有 POI";
            if (!EditorUtility.DisplayDialog("写入候选路网",
                    $"将用 {preview.nodes.Length} 个节点、{preview.edges.Length} 条边替换当前路网，" +
                    $"并{poiAction}。此操作支持 Ctrl+Z 撤销。确认继续吗？", "写入", "取消")) return;

            Undo.RecordObject(app, "Generate route from camera trajectory");
            app.nodes = (Vector3[])preview.nodes.Clone();
            app.edges = (TourEdge[])preview.edges.Clone();
            if (preservePois && app.pois != null)
                for (int i = 0; i < poiNodes.Count; i++) app.pois[i].node = poiNodes[i];
            if (!preservePois) app.pois = Array.Empty<TourPoi>();
            EditorUtility.SetDirty(app);
            EditorSceneManager.MarkSceneDirty(app.gameObject.scene);
            message = "已写入 TourApp。请在 Scene 视图复核路段、障碍和 POI，再保存场景。";
            preview = null;
            SceneView.RepaintAll();
        }

        void DrawPreview(SceneView sceneView)
        {
            if (preview == null || app == null || app.gameObject.scene != SceneManager.GetActiveScene()) return;
            Handles.color = new Color(1f, 0.55f, 0.12f, 1f);
            foreach (TourEdge edge in preview.edges)
                Handles.DrawAAPolyLine(4f, preview.nodes[edge.a] + Vector3.up * 0.08f,
                    preview.nodes[edge.b] + Vector3.up * 0.08f);
            for (int i = 0; i < preview.nodes.Length; i++)
            {
                Vector3 position = preview.nodes[i] + Vector3.up * 0.08f;
                Handles.DotHandleCap(0, position, Quaternion.identity,
                    HandleUtility.GetHandleSize(position) * 0.08f, EventType.Repaint);
                Handles.Label(position + Vector3.up * 0.1f, i.ToString());
            }
        }
    }
}
