using UnityEngine;

namespace CSU.Tour
{
    /// <summary>B 键切换的人工标注辅助线：观察区域和可达区域不参与物理碰撞。</summary>
    public sealed class PoiBoundsOverlay : MonoBehaviour
    {
        static readonly int[] CornerOrder = { 0, 1, 3, 2, 0, 4, 5, 1, 5, 7, 3, 7, 6, 2, 6, 4 };

        TourApp app;
        LineRenderer[] outlines;
        Material observationMaterial;
        Material arrivalMaterial;

        public void Initialize(TourApp owner)
        {
            app = owner;
            outlines = new LineRenderer[app.pois.Length * 2];
            observationMaterial = CreateMaterial(new Color(1f, 0.70f, 0.24f, 0.75f));
            arrivalMaterial = CreateMaterial(new Color(0.25f, 0.85f, 0.77f, 0.75f));

            for (int i = 0; i < outlines.Length; i++)
            {
                var lineObject = new GameObject(i % 2 == 0
                    ? "POI observation ROI"
                    : "Reachable arrival volume");
                lineObject.transform.SetParent(transform, false);
                LineRenderer line = lineObject.AddComponent<LineRenderer>();
                line.useWorldSpace = true;
                line.widthMultiplier = 0.018f;
                line.sharedMaterial = i % 2 == 0 ? observationMaterial : arrivalMaterial;
                line.positionCount = CornerOrder.Length;
                outlines[i] = line;
            }
        }

        Material CreateMaterial(Color color)
        {
            Material source = app.routeMaterial;
            Material material;
            if (source != null) material = new Material(source);
            else
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
                if (shader == null) shader = Shader.Find("Sprites/Default");
                material = new Material(shader);
            }
            TourApp.SetMaterialColor(material, color);
            return material;
        }

        void LateUpdate()
        {
            if (app == null || outlines == null) return;
            for (int i = 0; i < outlines.Length; i++)
            {
                LineRenderer line = outlines[i];
                line.enabled = app.showPoiBounds;
                if (!line.enabled) continue;

                TourPoi poi = app.pois[i / 2];
                Bounds bounds = i % 2 == 0
                    ? poi.visualBounds
                    : poi.ArrivalBounds(app.nodes[poi.node]);

                for (int cornerIndex = 0; cornerIndex < CornerOrder.Length; cornerIndex++)
                {
                    int corner = CornerOrder[cornerIndex];
                    Vector3 sign = new Vector3(
                        (corner & 1) == 0 ? -1f : 1f,
                        (corner & 2) == 0 ? -1f : 1f,
                        (corner & 4) == 0 ? -1f : 1f);
                    line.SetPosition(cornerIndex,
                        bounds.center + Vector3.Scale(bounds.extents, sign));
                }
            }
        }

        void OnDestroy()
        {
            if (observationMaterial != null) Destroy(observationMaterial);
            if (arrivalMaterial != null) Destroy(arrivalMaterial);
        }
    }
}
