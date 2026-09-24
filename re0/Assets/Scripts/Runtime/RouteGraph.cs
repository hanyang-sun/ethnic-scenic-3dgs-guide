using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Hand-authored walkable waypoints. Edges describe actual paths, not visual splat geometry.</summary>
public class RouteGraph : MonoBehaviour
{
    [Serializable] public struct Edge { public int a, b; }
    public Transform[] nodes = Array.Empty<Transform>();
    public Edge[] edges = Array.Empty<Edge>();

    public bool TryFindPath(Vector3 start, Vector3 goal, out Vector3[] path, out float length)
    {
        path = Array.Empty<Vector3>();
        length = 0;
        if (nodes == null || nodes.Length == 0) return false;
        int source = Closest(start), destination = Closest(goal);
        if (source < 0 || destination < 0) return false;
        int count = nodes.Length;
        var dist = new float[count];
        var previous = new int[count];
        var visited = new bool[count];
        for (int i = 0; i < count; i++) { dist[i] = float.PositiveInfinity; previous[i] = -1; }
        dist[source] = 0;
        for (int step = 0; step < count; step++)
        {
            int u = -1;
            for (int i = 0; i < count; i++) if (!visited[i] && (u < 0 || dist[i] < dist[u])) u = i;
            if (u < 0 || float.IsInfinity(dist[u])) break;
            if (u == destination) break;
            visited[u] = true;
            foreach (Edge edge in edges)
            {
                int v = edge.a == u ? edge.b : edge.b == u ? edge.a : -1;
                if (v < 0 || v >= count || visited[v] || nodes[v] == null) continue;
                float candidate = dist[u] + Vector3.Distance(nodes[u].position, nodes[v].position);
                if (candidate < dist[v]) { dist[v] = candidate; previous[v] = u; }
            }
        }
        if (float.IsInfinity(dist[destination])) return false;
        var reverse = new List<Vector3> { goal };
        for (int at = destination; at >= 0; at = previous[at]) { reverse.Add(nodes[at].position); if (at == source) break; }
        reverse.Add(start);
        reverse.Reverse();
        path = reverse.ToArray();
        for (int i = 1; i < path.Length; i++) length += Vector3.Distance(path[i - 1], path[i]);
        return true;
    }

    int Closest(Vector3 point)
    {
        int best = -1; float bestDistance = float.PositiveInfinity;
        for (int i = 0; i < nodes.Length; i++)
        {
            if (nodes[i] == null) continue;
            float distance = (nodes[i].position - point).sqrMagnitude;
            if (distance < bestDistance) { best = i; bestDistance = distance; }
        }
        return best;
    }

    void OnDrawGizmos()
    {
        if (nodes == null) return;
        Gizmos.color = new Color(0.1f, 0.8f, 0.9f, 0.8f);
        foreach (Transform node in nodes) if (node != null) Gizmos.DrawSphere(node.position, 0.12f);
        if (edges == null) return;
        foreach (Edge edge in edges)
            if (edge.a >= 0 && edge.b >= 0 && edge.a < nodes.Length && edge.b < nodes.Length && nodes[edge.a] != null && nodes[edge.b] != null)
                Gizmos.DrawLine(nodes[edge.a].position, nodes[edge.b].position);
    }
}
