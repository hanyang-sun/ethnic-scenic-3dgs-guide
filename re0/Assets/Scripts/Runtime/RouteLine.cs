using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
public class RouteLine : MonoBehaviour
{
    public RouteGraph graph;
    public float lineWidth = 0.09f;
    public Color lineColor = new Color(0.1f, 0.85f, 0.9f, 0.95f);
    public float lineHeight = 0.12f;
    public float cameraHeight = 1.6f;
    public float refreshDistance = 0.5f;
    public float routeLength { get; private set; }
    public bool hasRoute { get; private set; }

    LineRenderer line;
    Camera sceneCamera;
    Vector3 previousPosition;
    int previousTarget = int.MinValue;

    void Awake()
    {
        line = GetComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.widthMultiplier = lineWidth;
        line.numCapVertices = 4;
        line.startColor = line.endColor = lineColor;
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader != null)
        {
            line.material = new Material(shader);
            line.material.SetColor("_BaseColor", lineColor);
        }
        line.positionCount = 0;
    }

    void LateUpdate()
    {
        if (sceneCamera == null) sceneCamera = Camera.main;
        if (sceneCamera == null) return;
        int targetIndex = POIManager.CurrentIndex;
        if (targetIndex == previousTarget && (sceneCamera.transform.position - previousPosition).sqrMagnitude < refreshDistance * refreshDistance) return;
        previousTarget = targetIndex;
        previousPosition = sceneCamera.transform.position;
        POI target = POIManager.Target;
        Vector3 feet = sceneCamera.transform.position + Vector3.down * cameraHeight;
        if (target == null || graph == null || !graph.TryFindPath(feet, target.position, out Vector3[] path, out float length))
        {
            hasRoute = false;
            routeLength = 0;
            line.positionCount = 0;
            return;
        }
        hasRoute = true;
        routeLength = length;
        line.positionCount = path.Length;
        for (int i = 0; i < path.Length; i++) line.SetPosition(i, path[i] + Vector3.up * lineHeight);
    }
}
