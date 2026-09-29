using CSU.Tour;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[CustomEditor(typeof(TourApp))]
public sealed class TourAppEditor : UnityEditor.Editor
{
    public override void OnInspectorGUI()
    {
        EditorGUILayout.HelpBox(
            "集中式导览配置：POI、路网、玩家、UI 和日志均由 TourApp 统一管理。" +
            "WalkableGround 使用 Layer 8，导航障碍使用 Layer 9。",
            MessageType.Info);
        DrawDefaultInspector();
    }

    void OnSceneGUI()
    {
        var app = (TourApp)target;
        if (app.nodes == null) return;

        Handles.color = new Color(0.18f, 0.93f, 0.77f);
        if (app.edges != null)
            foreach (TourEdge edge in app.edges)
                if (edge.a >= 0 && edge.a < app.nodes.Length &&
                    edge.b >= 0 && edge.b < app.nodes.Length)
                    Handles.DrawLine(app.nodes[edge.a], app.nodes[edge.b]);

        if (app.pois != null)
            foreach (TourPoi poi in app.pois)
            {
                if (poi == null || poi.node < 0 || poi.node >= app.nodes.Length) continue;
                Handles.color = new Color(1f, 0.70f, 0.24f);
                Handles.DrawWireCube(poi.visualBounds.center, poi.visualBounds.size);
                Handles.Label(poi.visualBounds.center, poi.title + " / observation ROI");

                Bounds arrival = poi.ArrivalBounds(app.nodes[poi.node]);
                Handles.color = new Color(0.25f, 0.85f, 0.77f);
                Handles.DrawWireCube(arrival.center, arrival.size);
            }

        Handles.color = new Color(0.18f, 0.93f, 0.77f);
        for (int i = 0; i < app.nodes.Length; i++)
        {
            Handles.Label(app.nodes[i] + Vector3.up * 0.2f, "Node " + i);
            EditorGUI.BeginChangeCheck();
            Vector3 position = Handles.PositionHandle(app.nodes[i], Quaternion.identity);
            if (!EditorGUI.EndChangeCheck()) continue;

            Undo.RecordObject(app, "Move tour node");
            if (app.ground != null &&
                app.ground.Raycast(new Ray(position + Vector3.up * 10f, Vector3.down),
                    out RaycastHit hit, 20f))
                position.y = hit.point.y + 0.05f;
            app.nodes[i] = position;
            EditorUtility.SetDirty(app);
            EditorSceneManager.MarkSceneDirty(app.gameObject.scene);
        }
    }
}
