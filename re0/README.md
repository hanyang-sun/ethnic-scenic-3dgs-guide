# re0 · Garden 3DGS 景区导览原型

这是三人小组的 Unity 复建工程。运行时已迁移为与课程参考工程一致的集中式架构：`TourApp` 统一管理玩家、POI、路线、到达状态、UI 和日志；`TourGraph` 提供带障碍边过滤的 Dijkstra；`TourHud`、`TourMap`、`PoiBoundsOverlay` 和 `TourLog` 都只读取 `TourApp` 的公开状态。当前已完成 Garden 3DGS 渲染、第一人称漫游、三个目的地、路线与小地图、到达区域、帮助、暂停、重置、访问计数、0.75 秒障碍感知重规划和 JSONL 轨迹日志。Garden 仍是技术原型，最终展示素材还需替换为中式景区场景。

## 运行时架构

```text
3DGS / POI / Nodes / Edges / Colliders
                    ↓
                TourApp
       ┌────────────┼────────────┐
       ↓            ↓            ↓
   TourGraph    TourHud/Map    TourLog
       ↓            ↓            ↓
障碍感知路线    交互与可视化    JSONL研究数据
```

场景中只有 `Tour - edit the settings here` 上的一个 `TourApp` 负责运行时状态。不要再让多个 UI 或导航脚本分别维护“当前目标”；新增推荐算法时实现 `ResearchContracts.cs` 中的接口，或替换 `TourGraph` 的边权，不要把算法继续堆进 UI。

## 克隆并打开

1. 安装 Git LFS，并运行一次 `git lfs install`。克隆本仓库后，在仓库目录运行 `git lfs pull`，取得 Garden 原始 PLY、转换后的 `.bytes` 和中文字体。GitHub 上的 LFS 指针文本不能直接当作模型文件使用。
2. 在 Unity Hub 中安装 **Unity Editor 6000.3.24f1**，然后把本 `re0` 文件夹添加为项目。工程使用 URP 17.3.0；首次打开时 Unity 会导入资源和解析依赖，可能需要联网。
3. 打开 `Assets/Scenes/GardenPrototype.unity`，点击 Play。按 `1`、`2`、`3` 或底部按钮选择景点；`WASD` 移动，按住鼠标右键转向，`Shift` 加速，`R` 重置，`Esc` 暂停，`H` 显示帮助，`B` 显示景点观察区域和到达区域。小地图同时绘制完整路网、当前高亮路线、景点、角色位置和朝向。

每次运行会在 `Application.persistentDataPath/Tours` 下创建一个 UTF-8 JSONL 文件，记录 `session_start`、`target_selected`、`route_updated`、`poi_enter`、`poi_leave`、`poi_arrival`、`pose`、`pause/resume`、`reset` 和 `session_end`。尚未实现的观看质量指标明确写为 `null`，不会把“进入到达区域”误写成“观看完成”。

场景里以一块覆盖 **整个 3DGS 模型包围盒** 的隐形地板承托漫游角色，桌子使用一个圆柱形代理碰撞体。地板位于 `WalkableGround`（Layer 8），桌子位于 `TourObstacle`（Layer 9）。`TourApp` 规划路线时会用胶囊检测过滤被 `TourObstacle` 阻断的边。要改变可走范围，在 Hierarchy 中找到 `Tour - edit the settings here/Walkable floor and table obstacle/Walkable floor (resize Box Collider in Inspector)`，调整 `Box Collider > Size`。现有路线仍沿 28 个人工路点形成的环线。

编辑器菜单 `Garden Prototype > 2. Build teacher-style TourApp scene` 可以从 SampleScene 和 Garden 相机位姿重新生成同一架构；`Validate teacher-style architecture` 会检查唯一 `TourApp`、玩家与相机引用、28 个节点和边、3 个 POI、观察/到达区域、障碍层、碰撞体和研究扩展接口。

3DGS 插件 1.1.1 已内置在 `Packages/org.nesnausk.gaussian-splatting`，无需修改本机绝对路径。Unity 生成的 `Library`、`Logs`、`Temp` 等目录不会提交。`SourceAssets/Garden` 与 `Assets` 同级，保存原始 PLY、相机位姿和来源信息；Unity Project 窗口只显示 `Assets` 等工程内容。

## 文档与素材

- [项目计划书](Docs/计划与分工/计算机图形学课程26年_孙含睿_B3民族建筑景区3DGS沉浸式导览与游览路线推荐系统_项目计划书.docx)
- [运行时架构](Docs/计划与分工/架构.docx)

Garden 数据来自 [VR27 3DGS DataSet](https://huggingface.co/datasets/warmstones/VR27_3DGS_DataSet/tree/main/scenes/garden)，其元数据指向 [Mip-NeRF 360 Garden](https://jonbarron.info/mipnerf360/) 和 [Graphdeco 官方预训练模型](https://repo-sam.inria.fr/fungraph/3d-gaussian-splatting/)。模型仅用于研究与课程演示；使用和再分发时应遵守[上游许可](Docs/GaussianSplatting-LICENSE.md)。渲染插件的 MIT 许可证保存在 `Packages/org.nesnausk.gaussian-splatting/LICENSE.md`。中文字体换为 [Noto Sans SC](https://github.com/notofonts/noto-cjk)，许可证见[字体许可文件](Docs/NotoSansSC-OFL.txt)。
