using System;
using System.Collections.Generic;
using UnityEngine;

namespace CSU.Tour
{
    [Serializable]
    public sealed class TourPoi
    {
        public string id = "poi_01";
        public string title = "未命名景点";
        public string category = "观景位置";

        [TextArea(2, 5)]
        public string description = "";

        [Tooltip("与该景点关联的导航节点索引")]
        public int node;

        [Tooltip("人工标注的适合观察区域，不代表自动分割或可见性真值")]
        public Bounds visualBounds;

        [Tooltip("到达体积相对导航节点的世界坐标偏移")]
        public Vector3 arrivalOffset = new Vector3(0f, 0.8f, 0f);

        public Vector3 arrivalSize = new Vector3(1.5f, 2f, 1.5f);

        public Bounds ArrivalBounds(Vector3 goal)
            => new Bounds(goal + arrivalOffset, arrivalSize);
    }

    [Serializable]
    public struct TourEdge
    {
        public int a;
        public int b;

        public TourEdge(int from, int to)
        {
            a = from;
            b = to;
        }
    }

    /// <summary>
    /// 共享导航基线：在人工路网上按距离执行 Dijkstra。
    /// valid 用来过滤被障碍物阻断的边，后续研究可替换边权而不用改 UI。
    /// </summary>
    public static class TourGraph
    {
        public static List<int> Shortest(
            Vector3[] nodes,
            TourEdge[] edges,
            int start,
            int goal,
            Func<Vector3, Vector3, bool> valid)
        {
            if (nodes == null || edges == null || start < 0 || goal < 0 ||
                start >= nodes.Length || goal >= nodes.Length)
                return new List<int>();

            var distance = new float[nodes.Length];
            var previous = new int[nodes.Length];
            var visited = new bool[nodes.Length];
            for (int i = 0; i < nodes.Length; i++)
            {
                distance[i] = float.PositiveInfinity;
                previous[i] = -1;
            }
            distance[start] = 0f;

            for (int step = 0; step < nodes.Length; step++)
            {
                int current = -1;
                for (int i = 0; i < nodes.Length; i++)
                    if (!visited[i] && (current < 0 || distance[i] < distance[current]))
                        current = i;

                if (current < 0 || float.IsPositiveInfinity(distance[current])) break;
                if (current == goal) break;
                visited[current] = true;

                foreach (TourEdge edge in edges)
                {
                    if (edge.a < 0 || edge.b < 0 || edge.a >= nodes.Length || edge.b >= nodes.Length)
                        continue;

                    int next = edge.a == current ? edge.b : edge.b == current ? edge.a : -1;
                    if (next < 0 || visited[next] || (valid != null && !valid(nodes[current], nodes[next])))
                        continue;

                    float candidate = distance[current] + Vector3.Distance(nodes[current], nodes[next]);
                    if (candidate < distance[next])
                    {
                        distance[next] = candidate;
                        previous[next] = current;
                    }
                }
            }

            if (float.IsPositiveInfinity(distance[goal])) return new List<int>();

            var path = new List<int>();
            for (int node = goal; node >= 0; node = previous[node])
            {
                path.Add(node);
                if (node == start) break;
            }
            path.Reverse();
            return path;
        }
    }
}
