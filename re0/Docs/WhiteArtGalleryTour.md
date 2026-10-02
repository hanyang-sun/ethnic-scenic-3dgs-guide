# White Art Gallery 导览场景

打开 `Assets/Scenes/WhiteArtGalleryTour.unity`，点击 Play。此场景与 GardenPrototype 共用 TourApp、TourGraph、TourHud、TourMap 和 TourLog；它有自己的场景标识、标题、出生点、路网和 POI。

## 当前标定

- 3DGS 模型：`Assets/GaussianAssets/WhiteArtGallery/white_art_gallery.asset`，399,782 个 Gaussian。模型绕 X 轴旋转 180°、Z 缩放为 -1，使采集坐标中的地面朝向 Unity 的 Y 向上坐标。
- 物理地面：`Tour - edit the settings here/Physical Environment/Walkable Ground`，不可见 BoxCollider，Layer 8，顶面 Y = -1.5。玩家脚底与导航节点也使用 Y = -1.5；相机相对玩家高 1.65。
- 路网：14 个节点、14 条边，按 `SourceAssets/WhiteArtGallery/cameras.json` 中前一圈拍摄轨迹近似构成闭环。东、北、西三个 POI 是初始观察位，不是数据集提供的展品语义标注。
- 基础障碍：两个不可见 BoxCollider 对应画面中央的独立展台，Layer 9。位置和尺寸是依据点云分布估计的，应在编辑器 Scene 视图中与真实展台轮廓复核。
- 日志：`scene_id = white_art_gallery`。GardenPrototype 保留其原有场景标识和 UI 文案。

## 继续精修

可以先使用 `Tools → 导览 → 相机轨迹生成路网` 从本场景 `cameras.json` 生成橙色候选路线，再决定是否替换当前手工路网；步骤与安全限制见 `Docs/CameraTrajectoryRouteTool.md`。

1. 在 Scene 视图选中 `Tour - edit the settings here`，用现有节点手柄检查路径是否贴合走廊；在 Inspector 修改 `Nodes` 和 `Edges` 数组即可增加节点和连线。
2. 逐个检查展台碰撞体与可见物体的位置。新增墙体或不可穿越展品时，创建 Collider 并设为 `TourObstacle`（Layer 9）；它会影响玩家碰撞与路线可达性。
3. 在 `Pois` 数组补全经过实景核对的展品名称、介绍和关联节点。修改 POI 的 `Node` 索引后，编辑器会把该节点世界坐标填入 `Visual Bounds.Center`，保留原有范围大小；节点通常在脚底高度，因此可再手动上调 Center 的 Y，并调整 `Arrival Size`。当前三个“观察位”只是可运行的占位配置。
4. Play 模式测试：按 1/2/3 选择目的地，检查三条路线、到达区域和小地图；按 B 显示 POI 区域；尝试绕过两个展台。当前仍是 PC 键鼠漫游，未接入 VR。

3DGS 本身没有碰撞，地面和两个展台碰撞体也不代表完整的墙体/展品碰撞。不要把当前初始 POI 当成经过人工核验的展品标注。
