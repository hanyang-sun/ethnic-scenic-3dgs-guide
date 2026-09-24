# Garden 3DGS 导览原型：搭建全流程

本文件记录当前 `ScenicTour-Garden` 项目实际采用的流程和实现参数。项目使用 Unity 6.3.24f1、URP 和 UnityGaussianSplatting。Garden 是验证交互链路的临时素材，不是最终的中式景区场景。

## 0. 最终得到什么

- 主场景：`Assets/Scenes/GardenPrototype.unity`，也是 Build Settings 中唯一启用的场景。
- 3DGS 资源：`Assets/GaussianAssets/Garden/garden.asset` 和同目录的五个 `.bytes` 文件。
- 可运行功能：第一人称漫游、碰撞、三个目的地、三维景点标签、环形路网最短路径、地面路线、距离提示、到达介绍和关闭按钮。
- 预览：`Preview/Garden.png`；验证记录：`garden-ui-validate.log`。

主数据流如下：

```text
garden.ply + cameras.json
          ↓ 转换
garden.asset（负责画面）
          ↓ 绑定到场景
Garden 3DGS ── Main Camera / CharacterController
                    ↓ 玩家位置
POIManager → RouteGraph → RouteLine → DistanceHUD
     ↑                                ↓
DestinationSelector              ArrivalPanel

独立的透明步道与边界 Collider 负责碰撞；3DGS 本身没有可用的网格碰撞。
```

## 1. 准备工程和依赖

1. 从已有的 `ScenicTour` 工程复制 `Assets`、`Packages`、`ProjectSettings` 到独立的 `ScenicTour-Garden`，避免直接改动原工程。
2. 用 **Unity 6.3.24f1** 打开。资源包副本已将 3DGS 包内置于 `Packages/org.nesnausk.gaussian-splatting`，换电脑不需要修改插件路径。
3. 项目采用 URP；`Assets/Settings/PC_Renderer.asset` 包含 `GaussianSplatURPFeature`。如果场景只有背景色而没有高斯画面，应先检查这个 Renderer Feature 和 3DGS 对象上的资源引用。
4. `ProjectSettings/ProjectSettings.asset` 的 Active Input Handling 为 **Both**，因此当前 `CameraController` 使用的 `Input.GetKey` 和 `Input.GetAxis` 能工作。TextMeshPro 资源和中文 `NotoSansSC SDF` 字体已在工程中。

## 2. 获取并核对 Garden 数据

1. 从 [VR27 的 Garden 目录](https://huggingface.co/datasets/warmstones/VR27_3DGS_DataSet/tree/main/scenes/garden)取得 `3dgs/point_cloud.ply`、`cameras.json` 和 `metadata.json`。
2. 把 PLY 放到 `SourceAssets/Garden/garden.ply`。当前文件为 **322,401,532 字节**，SHA-256：`6d02fdf27e56d6e583c9dc8ed1cf0b668798b92cc10a01787008c5e66cd9b0e0`。PLY 文件头记录 **1,300,000** 个高斯点。
3. `cameras.json` 放在 PLY 同一目录。它含 **185** 个拍摄相机位姿，导入器会一并写进 Unity 资源。普通 PLY 只有模型，没有道路、碰撞和景点信息；这些要在 Unity 中另建。

## 3. 把 PLY 转成 Unity 能渲染的资源

在 Unity 顶部菜单选 **Garden Prototype → 1. Import 3DGS**。执行的是 `Assets/Editor/GardenImport.cs`：

1. 检查 `garden.ply` 是否存在，以及目标 `garden.asset` 是否已经带有相机位姿。
2. 调用 UnityGaussianSplatting 的 `GaussianSplatAssetCreator`，输入 PLY，输出到 `Assets/GaussianAssets/Garden`，画质设为 **Medium**。
3. 导出位置、颜色、其它属性、球谐系数和分块索引五个 `.bytes` 文件，再创建 `garden.asset`。五个数据文件合计约 **62.8 MB**。
4. 检查生成资源的高斯点数及相机数；当前分别为 **1,300,000** 和 **185**。

这里是“导入已训练的 3DGS”，没有在本机重新训练模型。生成的 `.asset` 引用同目录的 `.bytes`，复制工程时不能只拿走 `.asset`。

## 4. 构建场景与第一人称漫游（A）

菜单 **Garden Prototype → 2. Build scene** 调用 `Assets/Editor/GardenSetup.cs`。它从已有的 `SampleScene.unity` 创建 `GardenPrototype.unity`，复用主相机和高斯渲染器，同时移除示例后期处理对象。

1. 将 `garden.asset` 赋给 `GaussianSplatRenderer.m_Asset`。高斯对象命名为 `Garden 3DGS (visual only)`；X 轴旋转 **−154°**、Z 轴镜像 **−1**。这是为了让数据集拍摄轨迹在 Unity 场景里接近水平，后续还需人工校准。
2. 调用高斯渲染器的 `ActivateCamera(24)`，以数据集第 24 个相机位姿作为进入场景的视角。相机视野角 **60°**、近裁剪面 **0.05**、远裁剪面 **200**，背景使用纯色。
3. 在 Main Camera 上挂 `CharacterController` 和 `CameraController`。角色高度 **1.6**、半径 **0.25**；基础速度 **1.7 场景单位/秒**，Shift 加速倍率 **1.8**。`CameraController` 将前进方向投影到水平面，通过 `CharacterController.Move` 处理移动和碰撞，并开启重力。
4. 操作方式：`WASD` 平移，按住右键移动鼠标转视角，`Shift` 加速。当前正式漫游关闭了 `Z/X` 上下飞行选项。

3DGS 高斯点没有供 `CharacterController` 使用的 MeshCollider，所以碰撞几何必须另外搭建。

## 5. 建立环形步道与代理碰撞（A）

`GardenSetup` 读取导入资源中第 **24—51** 个相机位姿，共 **28** 个。每个位姿先经过高斯对象的世界坐标变换，再沿 Y 轴下移角色眼高 **1.6**，作为步道中心路点：

```text
步道路点 = splat.TransformPoint(相机位置) + (0, -1.6, 0)
```

路点放在 `Garden Tour/Walkable camera path (indices 24-51)`。相邻点首尾连接形成一圈。每条边生成一块看不见的 `BoxCollider` 步道，宽 **2.7**、厚 **0.2** 场景单位；中央半径 **2.45** 和外侧半径 **5.15** 各放 32 段边界碰撞体，防止直接穿越中央物体或离开重建区域。总数为 **28 块步道 + 64 段边界 = 92 个代理碰撞体**，统一放在 `Invisible walkway and perimeter colliders`。

这只是沿拍摄轨迹推定的可走范围，并非从 3DGS 自动生成的真实 NavMesh。正式景区需要逐段检查地面高度、步道净宽、障碍位置和入口出口。

## 6. 设置景点与路线（C）

`Garden Tour` 上的 `POIManager` 持有 `POI` 列表。每条数据包含 ID、名称、类别、区域、介绍、世界坐标等字段。当前三个目标分别取环线路点 `nodes[7]`、`nodes[15]`、`nodes[23]`：西侧、北侧、东侧观景位。这些介绍是原型文案。

1. `POILabel` 在每个 POI 坐标上方 **1.4** 单位创建世界空间 Canvas；标签始终朝向主相机，并随距离做有限缩放。
2. `RouteGraph` 保存 28 个路点和 28 条连接边。选择目标后，先寻找离玩家脚下和目标最近的路点，再用 **Dijkstra** 求图上最短路；边权重是两路点的三维欧氏距离。
3. `RouteLine` 读取路径点，生成贴近地面的 `LineRenderer`。玩家移动超过 **0.5** 单位或切换目标时重算；没有目标时隐藏路线。路线长度是所有路径段长度之和。

这里最短路沿路网绕行，但从玩家当前位置到最近路点、以及最后路点到 POI 的接入线仍是直连，复杂场景需要再做可见性和碰撞检查。

## 7. 搭建选择、距离、到达界面（B）

界面对象在 Play 后由脚本创建，不需要在场景中手工拖三个 Canvas：

1. `DestinationSelector` 读取 `POIManager.pois`，在左上角创建三个按钮；点击调用 `POIManager.Select(index)`。`POIManager` 同时支持键盘 `1/2/3` 选择、`0` 取消。
2. `DistanceHUD` 在右上角显示目标名、相对方向和路线长度。若当前没有可用路网，退回显示直线距离并标明“无可用路线”。方向箭头目前指向**目标本身**，不是下一路口；距离单位写为“场景单位”，没有冒充实地米数。
3. `ArrivalPanel` 用玩家与目标的**水平距离**判断：小于 **2** 单位弹出，到达后离开到 **3** 单位外收起。面板显示 POI 名称、区域和介绍；点击右上角 `×` 或按 `Esc` 可以关闭。关闭后留在原地不会立即重弹；离开并返回、或切换到另一个目的地时可再次出现。
4. `PrototypeFonts` 从 `Assets/Resources/NotoSansSC SDF.asset` 加载中文 TMP 字体。当前选择按钮字号 **20**、距离 HUD **22**、三维标签 **32**、到达标题 **26**、正文 **18**。界面使用 1920×1080 参考分辨率缩放。

这几处都通过 `POIManager.Target` 和当前索引共享目标状态。`GardenUiSettings` 可以把已保存场景的 UI 参数同步到当前较小的尺寸。

## 8. 验证与预览

菜单 **Garden Prototype → Validate scene** 调用 `Assets/Editor/GardenValidate.cs`，实际核查：

- 场景包含主相机、移动组件、CharacterController、130 万高斯点和 185 个相机位姿。
- 路网为 28 点 28 边，三个 POI 都能算出路线；对每个路点向下射线，确认脚下有代理步道。
- 中文字体和较小的 UI 参数已写入场景。
- 到达弹窗能出现，关闭后不会原地重弹；离开返回和切换目标都能再次触发。

最近一次验证记录在 `garden-ui-validate.log`，含 `GARDEN_VALIDATE_OK` 和 Unity 退出码 `0`。菜单 **Garden Prototype → 3. Capture preview** 用主相机生成 1280×720 的 `Preview/Garden.png`；这张图验证的是场景渲染，不代表所有运行时 UI 已做人工视觉走查。

## 9. 运行和演示

1. 打开 `Assets/Scenes/GardenPrototype.unity`，点击 Unity 的 **Play**。
2. 确认 Garden 画面正常；用右键转向、`WASD` 行走，观察中央与外围边界是否阻挡。
3. 点击左侧“西侧观景位”，检查地面路线与右上角距离；沿线走到目标附近，检查介绍弹窗。
4. 点击 `×` 或按 `Esc` 关闭，在原地观察是否保持关闭；离开 3 单位外再返回，检查重新弹出。
5. 选择另外两个目的地，检查路线重算、标签和文案。按 `0` 清除选择，路线应消失。

如需从资源重新搭建，顺序是 **Import 3DGS → Build scene → Validate scene → Capture preview**。**Build scene 会覆盖 `GardenPrototype.unity`**；手工调整过 POI、路点或碰撞体之后不要直接重跑。复制工程到别的电脑时先修正 `Packages/manifest.json` 的插件本机路径。

## 10. ABC 分工与当前边界

| 分工 | 当前可展示的结果 | 主要文件 |
|---|---|---|
| A：场景与漫游 | 模型导入、场景朝向、相机运动、透明步道与边界碰撞 | `GardenImport.cs`、`GardenSetup.cs`、`CameraController.cs` |
| B：界面与反馈 | 目的地按钮、距离方向、中文字体、到达弹窗与关闭状态 | `DestinationSelector.cs`、`DistanceHUD.cs`、`ArrivalPanel.cs`、`PrototypeFonts.cs` |
| C：数据与导航 | 三个 POI、空间标签、28 点路网、Dijkstra、路线绘制 | `POI.cs`、`POIManager.cs`、`POILabel.cs`、`RouteGraph.cs`、`RouteLine.cs` |

`GardenSetup.cs` 同时连接三组组件，因此也是团队共同集成入口。当前原型没有真正的中式建筑素材、实地尺度标定、自动 NavMesh、逐路口转向提示、示意地图和日志系统。VR27 数据元数据写明研究用途交付；公开传播模型或商业使用前，需要核对 [原始模型许可](https://github.com/graphdeco-inria/gaussian-splatting/blob/main/LICENSE.md) 与 [Mip-NeRF 360 数据说明](https://jonbarron.info/mipnerf360/)。
