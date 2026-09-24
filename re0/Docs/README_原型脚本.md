# 原型脚本安装说明

> 此文档是复制项目时附带的旧脚本说明。Garden 场景已由编辑器脚本自动搭建，实际使用请先看 [README.md](../README.md)。其中“直线路线”和“手工挂载”步骤不再适用于 GardenPrototype。

这七个脚本对应计划书 5.1 节的六件事，以及 7.1 节的分工表。

## 文件清单

| 脚本 | 对应零件 | 归属 |
|---|---|---|
| `CameraController.cs` | ① 自由移动 | 成员 A |
| `POI.cs` | 数据结构（全组接口） | 成员 C |
| `POIManager.cs` | ② 景点数据与当前目标 | 成员 C |
| `POILabel.cs` | 三维景点标签 | 成员 C |
| `RouteLine.cs` | ⑤ 路线绘制 | 成员 C |
| `DestinationSelector.cs` | ③ 目的地选择 | 成员 B |
| `DistanceHUD.cs` | ④ 距离与方向提示 | 成员 B |
| `ArrivalPanel.cs` | ⑥ 到达提示 | 成员 B |
| `ProxyColliderBuilder.cs` | 临时工具：代理碰撞体 | 成员 A |

## 第一步：改两个项目设置

**这两个不改，运行起来一定报错。**

### 1. 输入系统

Unity 6 新建的 URP 工程默认用的是新版输入系统，而 `CameraController` 用的是传统写法。

`Edit → Project Settings → Player → Other Settings → Active Input Handling`

改成 **Both**，然后 Unity 会提示重启编辑器，点确认。

### 2. TextMeshPro 资源

如果 Console 报 TextMeshPro 相关错误：

`Window → TextMeshPro → Import TMP Essential Resources`

点一下导入，一次性的事。

## 第二步：复制脚本

在工程的 `Assets` 下建目录结构：

```
Assets/
  Scripts/
    Runtime/
      CameraController.cs
      POI.cs
      POIManager.cs
      POILabel.cs
      RouteLine.cs
      DestinationSelector.cs
      DistanceHUD.cs
      ArrivalPanel.cs
```

把七个 `.cs` 文件复制进去。

> 如果后面还有 `Assets/Scripts/Editor/`，那是标注工具放的地方，现在不用建。

## 第三步：挂到场景里

### 相机（成员 A）

1. 选中 Hierarchy 里的 **Main Camera**
2. Add Component → 搜 `CameraController`，挂上
3. 给高斯渲染器指定 `garden.asset`，再在其 Inspector 的 **Cameras** 中选择第 `24` 个采集视角，作为 Garden 的起始画面

### 原型管理器（成员 C）

1. Hierarchy 空白处右键 → Create Empty，改名 **PrototypeManager**
2. 把它的 Transform Position 设成 `(0, 0, 0)`
3. 依次挂上三个组件：
   - `POIManager`
   - `POILabel`
   - `RouteLine`（挂上时会自动附带一个 LineRenderer，正常）

### 界面（成员 B）

再建两个空物体，各挂一个组件：

- **UISelector** → 挂 `DestinationSelector`
- **UIHUD** → 挂 `DistanceHUD` 和 `ArrivalPanel`（两个挂同一个物体上）

这两个脚本会自己在运行时创建 Canvas，**不需要你手动搭 UI**。

## 第四步：调景点位置

选中 **PrototypeManager**，Inspector 里展开 `POIManager` 的 `Pois` 列表，有三个默认景点：

| 名称 | 默认位置 |
|---|---|
| 入口 | (0, 0, -3) |
| 主展区 | (0, 0, 0) |
| 观景台 | (3, 0, 2) |

**这三个坐标是占位的，必须改。** 改的方法：

1. 选一个景点，把 Scene 视图切到 3D 视角
2. 在 Scene 视图里找到实际想标的那个位置
3. 把那个位置的坐标填进 `Position` 字段

或者更省事的办法：先按 Play，用 WASD 走到目标位置，看 Console 或记下 Inspector 里相机的 Position 值，退出再填进去。

**注意 Y 值。** 那个展厅里的地板高度需要你自己确认——如果景点标签浮在半空或者埋在地下，就是 Y 值填错了。先在 Scene 视图里用相机走一圈，找到地面的 Y 值。

## 第四步补充：让相机不穿墙

3DGS 是一坨高斯点，**没有网格也没有碰撞体**，所以相机默认可以直接穿墙。
原型阶段先用一套手工"碰撞替身"挡住。

### 1. 确认相机上有 CharacterController

选中 **Main Camera**，看 Inspector 里有没有 **Character Controller** 组件。
没有就手动加：Add Component → 搜 `Character` → 选 `Character Controller`。

参数建议：Center `(0, 0.85, 0)`、Radius `0.3`、Height `1.7`。

`CameraController` 会自动找到它，不用你拖引用。

### 2. 生成地板和墙

1. Hierarchy 空白处右键 → Create Empty，改名 **ColliderBuilder**
2. 挂上 `ProxyColliderBuilder`
3. Inspector 里设参数：`Floor Y`（地板高度）、`Floor Size` 填 12×12、`Room Size` 填 10×10、`Wall Height` 填 3
4. **右键点组件标题**（或点组件右上角三个点）→ 选 **「生成代理碰撞体」**
5. Hierarchy 里会多出 `ProxyColliders`，里面是 1 块地板 + 4 面墙

### 3. 怎么找 Floor Y

1. 先把相机的 `Use Gravity` **取消勾选**，并勾上 `Allow Vertical Fly`
2. 按 Play，用 Z/X 在场景里上下移动，找到地板看起来在哪个高度
3. 记下这时相机的 Y 值，停止 Play，填进 `Floor Y`

### 4. 对位

生成出来的白模是正方的矩形，而你的展厅可能是斜的或不规则的。所以要：

1. 在 Scene 视图里逐面选中墙，拖动 / 旋转到贴着实际墙面
2. 对好之后取消勾选 `Show Visuals`，**重新生成一次**

### 5. 回到相机调参数

| 字段 | 建议值 |
|---|---|
| `Use Gravity` | ✅ 勾上（有了地板就能开重力）|
| `Allow Vertical Fly` | ❌ 取消（正式走动时关掉）|
| `Move Speed` | `2.5` 左右，更像人的步速 |

### 6. 验证

按 Play，往墙上撞、往地板下沉。**撞上去停下来就对了。**

> **这是临时方案。** 白模只能挡住你摆上去的那几面墙，摆不到的地方照样能穿。
> 第 4-6 周接入路网、第 6-9 周从点云生成占据栅格之后，会被自动化的方案替换。

## 第五步：验收

按 Play，依次确认：

1. WASD 能走动，按住鼠标右键拖动能看到四周
2. 三个景点位置浮着白底黑字的标签，转视角时标签始终朝向自己
3. 屏幕左边有三个按钮，点一个（或者按小键盘 1/2/3）
4. 场景里出现一条线，从脚下连到那个景点
5. 右上角显示景点名、方向和距离
6. 沿着线走过去，距离数字变小
7. 走到 2 米以内，屏幕中间弹出介绍面板

**七步都走通，原型就完成了。** 三个人各自在自己电脑上跑一遍。

## 已知限制

这些是**故意留下的**，属于第二阶段要替换的内容：

| 项 | 现状 | 替换时间 |
|---|---|---|
| 路线 | 相机到目标的一条直线 | 第 4-6 周换真实路网 + A* |
| 景点数据 | 写死在代码里 | 第 2-4 周换 `pois.json` |
| 遮挡计算 | 完全没有 | 第 6-9 周 |
| VR | 键鼠操作 | 第 9-13 周 |
| 界面外观 | 默认样式，只保证能用 | 最后再说 |

`RouteLine.SetPath(Vector3[])` 这个接口是给第二阶段预留的——接入 A* 之后，
把路径点列传进去就行，其他部分一行都不用改。

## 出问题先查这里

| 现象 | 先查 |
|---|---|
| 一片黑，看不到场景 | 相机位置、图形 API 是不是 Vulkan |
| 报 `Input` 相关错误 | Active Input Handling 改成 Both 了没 |
| 报 TextMeshPro 错误 | 导入 TMP Essential Resources 了没 |
| 看不到标签 | 景点 Y 值不对，标签可能在地下 |
| 点按钮没反应 | 场景里有没有 EventSystem（脚本会自动建，看 Console 有没有报错） |
| 线看不到 | LineRenderer 的材质没找到，Console 会有对应提示 |
