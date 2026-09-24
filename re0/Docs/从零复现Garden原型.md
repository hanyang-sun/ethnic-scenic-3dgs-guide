# 从零复现 Garden 3DGS 导览原型

本文以当前工程已经验证过的实现为准，指导你从新的 Unity 6 URP 工程重建模型导入、场景、漫游、路网、UI 和到达反馈。推荐把新工程建在 `D:\桌面\计算机图形学\ScenicTour-Garden-Rebuild`，保留 `ScenicTour-Garden` 作为源码参照。脚本的完整代码就在本文列出的源文件中；关键算法和连接关系在下文逐项解释。

## 0. 最终目录与依赖关系

```text
ScenicTour-Garden-Rebuild/
├─ Assets/
│  ├─ Editor/                 ← 复制 5 个 Garden 编辑器脚本
│  ├─ Fonts/                  ← 复制中文 TMP 字体及源字体、.meta
│  ├─ Scripts/Runtime/        ← 复制 10 个运行时脚本
│  ├─ Scenes/SampleScene.unity ← 自己准备底座场景
│  ├─ GaussianAssets/Garden/  ← 导入步骤自动生成
│  ├─ Resources/NotoSansSC SDF.asset← 场景构建步骤自动复制
│  └─ Scenes/GardenPrototype.unity ← 场景构建步骤自动生成
├─ SourceAssets/Garden/       ← 自己放原始 PLY、cameras.json
├─ Packages/manifest.json     ← 引用 3DGS 包
└─ ProjectSettings/           ← Unity 项目设置
```

运行依赖：`PLY + cameras.json → GaussianSplatAsset → SampleScene → GardenSetup → GardenPrototype.unity → Play`。运行时目标状态由 `POIManager` 保存；`RouteLine` 和 `DistanceHUD` 读取该状态，`ArrivalPanel` 读取目标及玩家位置。

## 1. 创建空工程并配置图形环境

1. 在 Unity Hub 用 **Unity 6.3.24f1** 创建 **Universal 3D/URP** 工程，路径设为 `D:\桌面\计算机图形学\ScenicTour-Garden-Rebuild`。当前参照工程的 URP 包版本是 **17.3.0**。
2. 本资源包的插件位于 `UnityProject/Packages/org.nesnausk.gaussian-splatting`，其 `package.json` 报告版本 **1.1.1**。若从零创建新工程，可把该插件目录复制到新工程的 `Packages` 下；也可以用 Package Manager 的 **Add package from disk** 选择该目录的 `package.json`。如果插件位于工程外，还可在 `Packages/manifest.json` 的 `dependencies` 加入：

   ```json
   "org.nesnausk.gaussian-splatting": "file:D:/UnityGaussianSplatting/UnityGaussianSplatting-main/package"
   ```

   只有使用工程外的插件目录时才需要把 `file:` 后的路径改成电脑上的真实位置。插件的私有导入 API 可能随版本变化；精确复现以资源包内的 1.1.1 版本为准。
3. 打开 URP Renderer Data（当前参照工程是 `Assets/Settings/PC_Renderer.asset`），在 **Renderer Features** 加入 `GaussianSplatURPFeature`。在 URP 全局设置中关闭 **Render Graph Compatibility Mode**。
4. 在 **Project Settings → Player → Other Settings → Rendering** 将 Windows Graphics API 设为 **Direct3D12** 或 Vulkan。此插件的官方文档说明 Windows 的 DX11 不受支持。参照工程使用 Windows 的单一图形 API 配置。
5. 将 **Active Input Handling** 设为 **Both**。当前 `CameraController`、键盘选点和 `Esc` 关闭都使用旧 `UnityEngine.Input` API。
6. 确保 TextMeshPro 可用；Unity 提示导入 TMP Essentials 时按提示导入。中文字体的具体文件在第 4 步准备。

**检查点：**Package Manager 能看到 Gaussian Splatting；Renderer Features 列表中有 `GaussianSplatURPFeature`；Console 没有脚本编译错误。插件的 [使用说明](https://github.com/aras-p/UnityGaussianSplatting)和 [URP 集成说明](https://github.com/aras-p/UnityGaussianSplatting/blob/main/docs/render-pipeline-integration.md)可核对平台要求。

## 2. 准备原始 Garden 模型

1. 到 [VR27 Garden 场景目录](https://huggingface.co/datasets/warmstones/VR27_3DGS_DataSet/tree/main/scenes/garden)取得 Garden 的 3DGS PLY、`cameras.json` 和 `metadata.json`。VR27 页面目录可能调整，因此以文件内容和下列校验值为准。参照工程的本地原始文件在 `ScenicTour-Garden/SourceAssets/Garden`，可以用来对照。
2. 在新工程根目录创建 `SourceAssets/Garden`。把 PLY 命名为 `garden.ply`，将 `cameras.json` 放在同一目录。`metadata.json` 可以同放，用于保留来源信息。`SourceAssets` 位于工程根目录，不在 `Assets` 下；导入脚本使用相对项目根目录的路径读取。
3. 对照参照工程：`garden.ply` 为 **322,401,532 字节**，SHA-256 为 `6d02fdf27e56d6e583c9dc8ed1cf0b668798b92cc10a01787008c5e66cd9b0e0`，PLY 头部记录 **1,300,000** 个点；`cameras.json` 中有 **185** 个位姿。可以在 PowerShell 运行：

   ```powershell
   Get-FileHash -Algorithm SHA256 'D:\桌面\计算机图形学\ScenicTour-Garden-Rebuild\SourceAssets\Garden\garden.ply'
   ```

   `metadata.json` 中还记录了一个 583 万点的原始预训练模型。它与当前 130 万点的交付 PLY 不同；若拿到另一版本的 PLY，后续点数检查和画面可能与当前原型不一致。

**检查点：**`garden.ply` 与 `cameras.json` 同目录、PLY 哈希相同。插件会搜索 PLY 同目录或上级目录中的 `cameras.json`，当前摆法最直接。

## 3. 准备最小 SampleScene

`GardenSetup.Build` 会打开 `Assets/Scenes/SampleScene.unity`，并查找 `Camera.main` 和第一个 `GaussianSplatRenderer`。因此新工程必须先有这个底座场景。

1. 建立 `Assets/Scenes`，将当前空场景另存为 `Assets/Scenes/SampleScene.unity`。
2. 创建或保留 **Main Camera**，Tag 必须是 `MainCamera`。它应有 `Camera`、`AudioListener` 和 URP 的 Additional Camera Data。
3. 创建空对象 `GaussianSplats`，添加 **GaussianSplatRenderer** 组件。此时 Asset 可以为空，场景构建脚本会填入导入后的 `garden.asset`。
4. 保存 SampleScene。是否有 Directional Light 或 Global Volume 不影响核心导入；构建脚本会删除名为 `Global Volume` 的示例对象。

**检查点：**在 Hierarchy 中能找到上述相机和高斯对象；Inspector 能显示 `GaussianSplatRenderer`，无 Missing Script。当前参照用的底座是 [SampleScene.unity](../Assets/Scenes/SampleScene.unity)。

## 4. 放入运行时源码和中文字体

在新工程复制参照工程的 `Assets/Scripts/Runtime` 中下列源文件（可以连同 `.meta` 复制；若不复制 `.meta`，应在创建场景前让 Unity 先完成新 GUID 导入）：

| 文件 | 为什么需要 |
|---|---|
| [POI.cs](../Assets/Scripts/Runtime/POI.cs) | 景点数据字段：ID、名称、类别、区域、说明、位置等 |
| [POIManager.cs](../Assets/Scripts/Runtime/POIManager.cs) | 景点列表、当前目标、键盘 `1/2/3/0` |
| [CameraController.cs](../Assets/Scripts/Runtime/CameraController.cs) | 第一人称移动、视角与重力 |
| [RouteGraph.cs](../Assets/Scripts/Runtime/RouteGraph.cs) | 路网节点、边与 Dijkstra 求路 |
| [RouteLine.cs](../Assets/Scripts/Runtime/RouteLine.cs) | 地面路线绘制与重算 |
| [POILabel.cs](../Assets/Scripts/Runtime/POILabel.cs) | 世界空间中文景点标签 |
| [DestinationSelector.cs](../Assets/Scripts/Runtime/DestinationSelector.cs) | 左上角目的地按钮 |
| [DistanceHUD.cs](../Assets/Scripts/Runtime/DistanceHUD.cs) | 右上角距离和方向 |
| [ArrivalPanel.cs](../Assets/Scripts/Runtime/ArrivalPanel.cs) | 到达介绍、关闭与重新触发 |
| [PrototypeFonts.cs](../Assets/Scripts/Runtime/PrototypeFonts.cs) | 从 `Resources` 加载中文 TMP 字体 |

`ProxyColliderBuilder.cs` 是早期手动造地板和四面墙的辅助脚本，**当前 Garden 场景的 92 个碰撞体由 `GardenSetup` 直接生成**，所以复现当前结果无需运行该辅助脚本。

中文字体：同机复现可复制参照工程的 `Assets/Fonts` 整个目录，保留 `NotoSansSC SDF.asset`、源字体 `NotoSansSC-VF.ttf` 及对应 `.meta`。`GardenSetup` 会把 `NotoSansSC SDF.asset` 复制到 `Assets/Resources`，运行时 `PrototypeFonts` 使用 `Resources.Load<TMP_FontAsset>("NotoSansSC SDF")` 取得字体。若用别的中文字体，应生成同名 TMP Font Asset，并核对中文字符是否显示。

**检查点：**Unity 编译通过；在 Project 窗口可看到所有脚本和 `Assets/Fonts/NotoSansSC SDF.asset`。

## 5. 放入编辑器源码并导入 3DGS

复制参照工程的 `Assets/Editor` 下五个脚本：[GardenImport.cs](../Assets/Editor/GardenImport.cs)、[GardenSetup.cs](../Assets/Editor/GardenSetup.cs)、[GardenUiSettings.cs](../Assets/Editor/GardenUiSettings.cs)、[GardenValidate.cs](../Assets/Editor/GardenValidate.cs)、[GardenCapture.cs](../Assets/Editor/GardenCapture.cs)。让 Unity 完成脚本重新编译后，顶部会出现 **Garden Prototype** 菜单。

点击 **Garden Prototype → 1. Import 3DGS**。`GardenImport` 的关键代码做三件事：

```csharp
const string InputPath = "SourceAssets/Garden/garden.ply";
const string AssetPath = "Assets/GaussianAssets/Garden/garden.asset";
// 设置插件创建器的输入、输出与质量，然后调用 CreateAsset。
qualityField.SetValue(creator, Enum.ToObject(qualityField.FieldType, 2)); // Medium
type.GetMethod("CreateAsset", flags).Invoke(creator, null);
```

脚本通过反射访问当前插件 `GaussianSplatAssetCreator` 的私有字段与方法：`m_InputFile`、`m_OutputFolder`、`m_Quality`、`ApplyQualityLevel`、`CreateAsset`。因此安装了另一版本插件时，这些名字若变化，导入脚本需同步修改。

插件读取 PLY 和旁边的相机 JSON，生成 `garden.asset` 与五个 `.bytes` 文件。它们分别保存分块索引、颜色、其它属性、位置和球谐数据，总计约 **62.8 MB**。

**检查点：**`Assets/GaussianAssets/Garden` 下有 `garden.asset` 和五个 `garden_*.bytes`；Console 有 `GARDEN_IMPORT_OK count=1300000 cameras=185`。若 `cameras=0`，重点检查 `cameras.json` 是否与 PLY 同目录及是否选中了正确数据。

## 6. 生成 Garden 场景、玩家和碰撞体

点击 **Garden Prototype → 2. Build scene**。`GardenSetup.Build` 执行以下顺序，完成后将场景保存为 `Assets/Scenes/GardenPrototype.unity`，并设成 Build Settings 中唯一启用的场景。

### 6.1 画面与相机

打开 `SampleScene`，找到 `GaussianSplatRenderer` 与主相机；赋值 `garden.asset`。高斯对象的旋转设为 `Quaternion.Euler(-154f, 0f, 0f)`，缩放设为 `(1,1,-1)`，用于对齐数据集和 Unity 坐标系。`ActivateCamera(24)` 将第 24 个采集相机位姿作为初始视角。相机视野角为 60°、近裁剪 0.05、远裁剪 200。

在主相机上加入 `CharacterController`，高度 1.6、中心 `(0,-0.8,0)`、半径 0.25、台阶偏移 0.3。再加入 `CameraController`，基础速度 1.7、Shift 倍率 1.8、开启重力。移动脚本先把前进方向投影到水平面，再调用 `CharacterController.Move`，使抬头时按 W 不会飞起。

### 6.2 路点与代理碰撞

取 `asset.cameras[24]` 到 `[51]` 共 28 个相机位置。每个位置经高斯对象的 `TransformPoint` 转到世界坐标，再向下 1.6 单位成为脚下路点：

```csharp
node.transform.position = splat.TransformPoint(asset.cameras[index].pos)
                        + Vector3.down * 1.6f;
```

相邻路点按 `i → (i+1) % 28` 连成一圈。每条边生成一块不可见的 `BoxCollider` 地板，宽 2.7、厚 0.2；中央半径 2.45 和外侧半径 5.15 各生成 32 段边界，合计 **28 + 32 + 32 = 92** 个碰撞对象。这些 BoxCollider 只负责物理阻挡，不承担 3DGS 画面渲染。

**检查点：**Hierarchy 有 `Garden 3DGS (visual only)`、`Main Camera`、`Garden Tour`。在 `Garden Tour` 下可展开 28 个 `WayPoint` 和 92 个代理碰撞对象。按 Play 后玩家不应直接坠落。

## 7. 创建 POI 与导航

`GardenSetup` 在图节点 `[7]`、`[15]`、`[23]` 处添加三个 POI，名称依次为西侧、北侧、东侧观景位。每个 POI 是 `POI` 类数据，`POIManager.CurrentIndex` 保存当前目标索引，`-1` 表示未选择。

`RouteGraph.TryFindPath(start, goal, out path, out length)` 的逻辑是：找最近的起点节点与终点节点；用 Dijkstra 按边长求最短路；从 `previous[]` 回溯节点序列；在路径两端分别加入玩家脚下和 POI 坐标；计算所有线段总长。完整实现见 [RouteGraph.cs](../Assets/Scripts/Runtime/RouteGraph.cs)。

`RouteLine` 读取 `POIManager.Target`，在玩家移动超过 0.5 单位或目标索引变化时重算路线；把每个路径点加高 0.12 单位后交给 `LineRenderer`。无目标时将 `positionCount` 设为零。`POILabel` 在 POI 上方 1.4 单位生成世界空间 Canvas，并使标签面向相机。

**检查点：**Play 后能看见景点标签；选中不同目的地时，地面路线沿环形路网变化。当前路线的起点接入和末端接入是直连，尚未做真实障碍检测。

## 8. 创建 UI、方向提示与到达面板

运行时由 `DestinationSelector` 创建左上角按钮，按钮点击调用 `POIManager.Select(index)`；`POIManager.Update` 也支持键盘 `1/2/3` 和 `0`。`DistanceHUD` 创建右上角 HUD：有路时显示 `RouteLine.routeLength`，无路时显示水平直线距离；方向通过目标相对玩家的水平向量与相机前方求有符号夹角。箭头目前指向目标本身，并非下一转弯点。

`ArrivalPanel` 用**水平距离**判断到达，因为 POI 在地板、相机在眼高。小于 2 单位显示，达到 3 单位后隐藏和重置；两个阈值之间维持状态，防止边界抖动。`×` 按钮和 `Esc` 都会调用 `Close()`：

```csharp
void Close()
{
    dismissedIndex = activeIndex;
    panel.SetActive(false);
}
```

`LateUpdate` 在玩家仍处于目标附近时检查 `dismissedIndex != activeIndex`，因此关闭后不会原地立即弹回；离开 3 单位外或换目标会清除关闭标记。当前弹窗尺寸 520×210，标题字号 26、正文字号 18；按钮字号 20、HUD 22。`GardenUiSettings.Configure` 统一设置这些较小的尺寸。

**检查点：**按钮可以点击；路线和距离随目标变化；到达弹窗能打开、关闭、原地保持关闭，离开返回后再次打开。

## 9. 自动验证、截图与人工走查

1. 点击 **Garden Prototype → Validate scene**。`GardenValidate` 检查模型 130 万点、185 相机、28 点 28 边、3 个可达 POI、每个路点脚下的地板、中文字体、较小 UI，以及弹窗关闭和重新出现的状态。成功时 Console 输出 `GARDEN_VALIDATE_OK`。
2. 点击 **Garden Prototype → 3. Capture preview**。`GardenCapture` 用主相机输出 1280×720 的 `Preview/Garden.png`，用于确认 3DGS 渲染；它不包含 Play 模式运行时 UI。
3. 人工进入 Play：右键转视角、WASD 行走、Shift 加速；选择三个目的地，观察路线；到达后点击 `×` 或按 Esc，验证离开后重进；按 0 取消目标。

**常见定位方法：**

| 现象 | 优先检查 |
|---|---|
| `Garden Prototype` 菜单没有出现 | Console 编译错误；`Assets/Editor` 脚本是否完整；插件是否成功加载 |
| 导入找不到 PLY | 路径必须是工程根目录下 `SourceAssets/Garden/garden.ply` |
| 有 `garden.asset`，却无 185 相机 | `cameras.json` 是否在 PLY 同目录；是否用了另一版本数据 |
| 画面空白 | 3DGS Renderer Feature、Render Graph Compatibility Mode、DX12/Vulkan、Asset 引用 |
| 构建脚本找不到相机或渲染器 | `SampleScene.unity` 路径、相机的 `MainCamera` Tag、GaussianSplatRenderer 组件 |
| 中文方框 | `Assets/Resources/NotoSansSC SDF.asset` 及其源字体引用 |
| 角色下坠或穿墙 | 92 个 BoxCollider 是否生成，步道 Y 值及方向是否对齐 |
| 路线不显示 | 是否选中 POI；`RouteGraph` 是否 28 点 28 边；`RouteLine` 引用是否已设 |

## 10. 复现后的边界

当前 Garden PLY 是技术素材，画面并非中式景区。路点与碰撞来自拍摄轨迹的近似，距离是场景单位；POI 文案是占位说明。把原型移植到真实景区时，优先替换模型和相机轨迹，再重新校准场景坐标、步道、碰撞、POI 和尺度。
