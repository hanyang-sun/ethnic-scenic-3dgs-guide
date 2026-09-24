using UnityEngine;

/// <summary>
/// 临时工具｜代理碰撞体生成器   —— 归成员 A
/// 给 3DGS 场景临时造一套"碰撞替身"：一块地板 + 四面墙。
///
/// 为什么需要它：3DGS 是一坨高斯点，没有网格也没有碰撞体，
/// 所以相机可以直接穿墙。原型阶段先用这套手工替身挡住；
/// 等第 6-9 周从点云算出占据栅格后，再换成自动生成的碰撞。
///
/// 用法：把脚本挂到任意空物体 → 在 Inspector 里设好尺寸和地板高度
///      → 点组件右上角三个点（或右键组件标题）→ 选「生成代理碰撞体」
///      → 生成后逐个选中，在 Scene 视图里拖动对位
/// </summary>
public class ProxyColliderBuilder : MonoBehaviour
{
    [Header("地板")]
    [Tooltip("地板高度。先用 Scene 视图走到场景里，确认地面在哪个 Y 值")]
    public float floorY = 0f;

    [Tooltip("地板尺寸（米）")]
    public Vector2 floorSize = new Vector2(12f, 12f);

    [Header("墙壁")]
    public float wallHeight = 3f;
    public float wallThickness = 0.2f;

    [Tooltip("四面墙围成的区域尺寸（米），一般比地板略小")]
    public Vector2 roomSize = new Vector2(10f, 10f);

    [Header("显示")]
    [Tooltip("生成时显示白模方便对位。对好之后取消勾选并重新生成")]
    public bool showVisuals = true;

    [ContextMenu("生成代理碰撞体")]
    public void Build()
    {
        Transform root = transform.Find("ProxyColliders");
        if (root == null)
        {
            GameObject rootGo = new GameObject("ProxyColliders");
            rootGo.transform.SetParent(transform, false);
            root = rootGo.transform;
        }

        float halfX = roomSize.x * 0.5f;
        float halfZ = roomSize.y * 0.5f;

        // 地板用一个很扁的 Box，比 Plane 好对齐
        MakeBox(root, "Floor",
                new Vector3(0f, floorY - wallThickness * 0.5f, 0f),
                new Vector3(floorSize.x, wallThickness, floorSize.y));

        MakeBox(root, "Wall_North",
                new Vector3(0f, floorY + wallHeight * 0.5f, halfZ),
                new Vector3(roomSize.x + wallThickness, wallHeight, wallThickness));
        MakeBox(root, "Wall_South",
                new Vector3(0f, floorY + wallHeight * 0.5f, -halfZ),
                new Vector3(roomSize.x + wallThickness, wallHeight, wallThickness));
        MakeBox(root, "Wall_East",
                new Vector3(halfX, floorY + wallHeight * 0.5f, 0f),
                new Vector3(wallThickness, wallHeight, roomSize.y));
        MakeBox(root, "Wall_West",
                new Vector3(-halfX, floorY + wallHeight * 0.5f, 0f),
                new Vector3(wallThickness, wallHeight, roomSize.y));

        Debug.Log("[ProxyColliderBuilder] 已生成 1 块地板 + 4 面墙。请到 Scene 视图拖动对位，" +
                  "确认后取消勾选 showVisuals 并重新生成。");
    }

    [ContextMenu("删除代理碰撞体")]
    public void Clear()
    {
        Transform root = transform.Find("ProxyColliders");
        if (root == null)
        {
            Debug.Log("[ProxyColliderBuilder] 没有找到 ProxyColliders。");
            return;
        }
#if UNITY_EDITOR
        DestroyImmediate(root.gameObject);
#else
        Destroy(root.gameObject);
#endif
        Debug.Log("[ProxyColliderBuilder] 已删除代理碰撞体。");
    }

    void MakeBox(Transform parent, string name, Vector3 pos, Vector3 size)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = size;

        MeshRenderer mr = go.GetComponent<MeshRenderer>();
        if (mr != null) mr.enabled = showVisuals;
    }
}
