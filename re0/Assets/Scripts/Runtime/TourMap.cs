using UnityEngine;
using UnityEngine.UI;

namespace CSU.Tour
{
    /// <summary>以 TourApp 为唯一数据源绘制路网、当前路线、POI 和玩家朝向。</summary>
    [RequireComponent(typeof(RectTransform), typeof(CanvasRenderer))]
    public sealed class TourMap : MaskableGraphic
    {
        public TourApp app;

        Bounds CalculateBounds()
        {
            Quaternion inverse = Quaternion.Euler(0f, -app.startYaw, 0f);
            var bounds = new Bounds(inverse * (app.nodes[0] - app.startPosition), Vector3.zero);
            foreach (Vector3 node in app.nodes)
                bounds.Encapsulate(inverse * (node - app.startPosition));
            bounds.Encapsulate(inverse * (app.player.position - app.startPosition));
            foreach (Vector3 point in app.Route)
                bounds.Encapsulate(inverse * (point - app.startPosition));
            bounds.Expand(1.4f);
            return bounds;
        }

        public Vector2 Project(Vector3 point)
        {
            if (app == null || app.nodes == null || app.nodes.Length == 0) return Vector2.zero;
            Quaternion inverse = Quaternion.Euler(0f, -app.startYaw, 0f);
            Bounds bounds = CalculateBounds();
            Rect rect = rectTransform.rect;
            float width = Mathf.Max(bounds.size.x, 0.01f);
            float depth = Mathf.Max(bounds.size.z, 0.01f);
            float scale = Mathf.Min((rect.width - 24f) / width, (rect.height - 24f) / depth);
            Vector3 local = inverse * (point - app.startPosition) - bounds.center;
            return rect.center + new Vector2(local.x, local.z) * scale;
        }

        protected override void OnPopulateMesh(VertexHelper helper)
        {
            helper.Clear();
            if (app == null || app.nodes == null || app.nodes.Length == 0) return;

            foreach (TourEdge edge in app.edges)
                if (edge.a >= 0 && edge.b >= 0 && edge.a < app.nodes.Length && edge.b < app.nodes.Length)
                    AddLine(helper, Project(app.nodes[edge.a]), Project(app.nodes[edge.b]), 2f,
                        new Color(0.55f, 0.67f, 0.70f, 0.9f));

            for (int i = 1; i < app.Route.Count; i++)
                AddLine(helper, Project(app.Route[i - 1]), Project(app.Route[i]), 4f,
                    new Color(0.18f, 0.93f, 0.77f, 1f));

            for (int i = 0; i < app.pois.Length; i++)
            {
                Vector2 position = Project(app.nodes[app.pois[i].node]);
                Color color = app.Visited[i]
                    ? new Color(0.55f, 0.82f, 0.44f)
                    : new Color(1f, 0.72f, 0.25f);
                AddDisc(helper, position, app.Selected == i ? 7f : 5f, color);
            }

            Rect rect = rectTransform.rect;
            Vector2 playerPoint = Project(app.player.position);
            playerPoint.x = Mathf.Clamp(playerPoint.x, rect.xMin + 9f, rect.xMax - 9f);
            playerPoint.y = Mathf.Clamp(playerPoint.y, rect.yMin + 9f, rect.yMax - 9f);
            AddDisc(helper, playerPoint, 8f, Color.white);

            Quaternion inverseHeading = Quaternion.Euler(0f, -app.startYaw, 0f);
            Vector3 forward3 = inverseHeading * app.player.forward;
            Vector2 forward = new Vector2(forward3.x, forward3.z).normalized;
            Vector2 side = new Vector2(-forward.y, forward.x);
            int start = helper.currentVertCount;
            Color arrow = new Color(0.05f, 0.40f, 0.50f);
            helper.AddVert(playerPoint + forward * 7f, arrow, Vector2.zero);
            helper.AddVert(playerPoint - forward * 4f + side * 4f, arrow, Vector2.zero);
            helper.AddVert(playerPoint - forward * 4f - side * 4f, arrow, Vector2.zero);
            helper.AddTriangle(start, start + 1, start + 2);
        }

        static void AddLine(VertexHelper helper, Vector2 a, Vector2 b, float width, Color color)
        {
            Vector2 delta = b - a;
            if (delta.sqrMagnitude < 0.001f) return;
            Vector2 side = new Vector2(-delta.y, delta.x).normalized * width * 0.5f;
            AddQuad(helper, a - side, a + side, b + side, b - side, color);
        }

        static void AddQuad(VertexHelper helper, Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color color)
        {
            int start = helper.currentVertCount;
            helper.AddVert(a, color, Vector2.zero);
            helper.AddVert(b, color, Vector2.zero);
            helper.AddVert(c, color, Vector2.zero);
            helper.AddVert(d, color, Vector2.zero);
            helper.AddTriangle(start, start + 1, start + 2);
            helper.AddTriangle(start, start + 2, start + 3);
        }

        static void AddDisc(VertexHelper helper, Vector2 point, float radius, Color color)
        {
            int start = helper.currentVertCount;
            helper.AddVert(point, color, Vector2.zero);
            for (int i = 0; i <= 20; i++)
            {
                float angle = i * Mathf.PI / 10f;
                helper.AddVert(point + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius,
                    color, Vector2.zero);
                if (i > 0) helper.AddTriangle(start, start + i, start + i + 1);
            }
        }
    }
}
