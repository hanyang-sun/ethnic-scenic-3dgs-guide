using System;
using UnityEngine;

/// <summary>
/// 景点节点数据结构   —— 归成员 C
/// 这是全组共用的接口。改动字段之前，先跟另外两个人说一声。
/// 第 2-4 周会把这份数据改成从 pois.json 读取，字段名保持不变。
/// </summary>
[Serializable]
public class POI
{
    [Tooltip("唯一标识，例如 poi_01")]
    public string id = "poi_01";

    [Tooltip("景点名称，会显示在三维标签和按钮上")]
    public string name = "未命名景点";

    [Tooltip("建筑 / 自然景观 / 道路 / 广场 / 入口 / 观景位置")]
    public string category = "建筑";

    [Tooltip("所属区域，例如 入口区、中心区")]
    public string area = "未划分区域";

    [TextArea(2, 5)]
    [Tooltip("简短说明，到达后弹窗显示")]
    public string description = "";

    [Tooltip("景点中心位置（世界坐标）。在 Scene 视图里可拖动查看")]
    public Vector3 position = Vector3.zero;

    [Tooltip("三维范围尺寸。原型阶段不用，标注阶段才用得上")]
    public Vector3 boundsSize = new Vector3(2f, 2f, 2f);

    [Tooltip("候选观察位置。原型阶段留空")]
    public Vector3[] viewpoints = new Vector3[0];
}
