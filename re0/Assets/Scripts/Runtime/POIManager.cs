using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 原型零件 2｜景点数据与当前目标   —— 归成员 C
/// 挂到一个空物体上，建议命名 POIManager。
///
/// 景点列表在编辑器里就能编辑：如果 Inspector 里 Pois 是空的，
/// 点组件右上角三个点（或右键组件标题）→ 选「生成默认景点」。
/// </summary>
public class POIManager : MonoBehaviour
{
    public static POIManager Instance;

    [Tooltip("原型阶段先写死三个景点。第 2-4 周改成从 pois.json 读取")]
    public List<POI> pois = new List<POI>();

    /// <summary>当前选中的目的地索引，-1 表示还没选。</summary>
    public static int CurrentIndex = -1;

    /// <summary>当前目的地。没选或索引越界时返回 null。</summary>
    public static POI Target
    {
        get
        {
            if (Instance == null) return null;
            if (CurrentIndex < 0 || CurrentIndex >= Instance.pois.Count) return null;
            return Instance.pois[CurrentIndex];
        }
    }

    void Awake()
    {
        Instance = this;
        if (pois == null || pois.Count == 0)
        {
            PopulateDefaults();
        }
    }

    /// <summary>编辑器里第一次挂上这个组件时会自动调用。</summary>
    void Reset()
    {
        PopulateDefaults();
    }

    void Update()
    {
        // 按 1/2/3 也能选目的地，方便快速验证
        if (Input.GetKeyDown(KeyCode.Alpha1)) Select(0);
        if (Input.GetKeyDown(KeyCode.Alpha2)) Select(1);
        if (Input.GetKeyDown(KeyCode.Alpha3)) Select(2);
        if (Input.GetKeyDown(KeyCode.Alpha0)) Select(-1);
    }

    public static void Select(int index)
    {
        CurrentIndex = index;
        Debug.Log(index < 0 ? "已取消选择" : "已选择目的地：" + Target.name);
    }

    /// <summary>
    /// 生成三个占位景点。
    /// 坐标是散布在房间里的，但仍然需要你按实际情况调整——
    /// 尤其是 Y 值，它应该等于那个位置的地板高度。
    /// </summary>
    [ContextMenu("生成默认景点")]
    public void PopulateDefaults()
    {
        pois = new List<POI>
        {
            new POI
            {
                id = "poi_01", name = "入口", category = "入口", area = "入口区",
                description = "这是入口景点的占位说明，后面换成真实介绍。",
                position = new Vector3(-3f, 0f, -3f)
            },
            new POI
            {
                id = "poi_02", name = "主展区", category = "建筑", area = "中心区",
                description = "这是主展区的占位说明，后面换成真实介绍。",
                position = new Vector3(0f, 0f, 0f)
            },
            new POI
            {
                id = "poi_03", name = "观景台", category = "观景位置", area = "东侧",
                description = "这是观景台的占位说明，后面换成真实介绍。",
                position = new Vector3(3f, 0f, 3f)
            },
        };

        for (int i = 0; i < pois.Count; i++)
        {
            pois[i].boundsSize = new Vector3(2f, 2f, 2f);
            pois[i].viewpoints = new Vector3[0];
        }

        Debug.Log("[POIManager] 已生成 3 个占位景点。请到 Inspector 里把 Position 改成实际位置，" +
                  "Y 值填那个位置的地板高度。");
    }
}
