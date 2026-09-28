# re0 · Garden 3DGS 景区导览原型

这是三人小组的 Unity 复建工程。当前已完成 Garden 场景的 3DGS 渲染、第一人称漫游、三个目的地、路径引导、到达提示和显示角色位置、朝向及景点的小地图。帮助、暂停、重置、访问计数、轨迹记录和障碍感知重规划属于下一阶段任务，见[三人任务表](Docs/计划与分工/三人任务表_前两周Garden复建与增强.md)。Garden 只是技术原型，最终展示素材还需替换为中式景区场景。

## 克隆并打开

1. 安装 Git LFS，并运行一次 `git lfs install`。克隆本仓库后，在仓库目录运行 `git lfs pull`，取得 Garden 原始 PLY、转换后的 `.bytes` 和中文字体。GitHub 上的 LFS 指针文本不能直接当作模型文件使用。
2. 在 Unity Hub 中安装 **Unity Editor 6000.3.24f1**，然后把本 `re0` 文件夹添加为项目。工程使用 URP 17.3.0；首次打开时 Unity 会导入资源和解析依赖，可能需要联网。
3. 打开 `Assets/Scenes/GardenPrototype.unity`，点击 Play。按 `1`、`2`、`3` 选择景点，`0` 取消；`WASD` 移动，按住鼠标右键转向，`Shift` 加速。右下角小地图显示道路、景点标记和角色朝向，景点名称按标记颜色列在地图下方；到达后用 `Esc` 或弹窗右上角的 `×` 关闭提示。

场景里以一块覆盖 **整个 3DGS 模型包围盒** 的隐形地板承托漫游角色，桌子使用一个圆柱形代理碰撞体；原先的窄步道和内外圈墙已移除。要改变可走范围，在 Hierarchy 中找到 `Garden Tour/Walkable floor and table obstacle/Walkable floor (resize Box Collider in Inspector)`，调整它的 `Box Collider > Size`（X、Z）；小地图会读取地板范围。3DGS 包围盒包含场景外围散点，地板高度也是统一的，因此部分位置可能没有可靠的地面画面；现有路线引导仍沿 28 个路点形成的环线。

3DGS 插件 1.1.1 已内置在 `Packages/org.nesnausk.gaussian-splatting`，无需修改本机绝对路径。Unity 生成的 `Library`、`Logs`、`Temp` 等目录不会提交。`SourceAssets/Garden` 与 `Assets` 同级，保存原始 PLY、相机位姿和来源信息；Unity Project 窗口只显示 `Assets` 等工程内容。

## 文档与素材

- [项目计划书](Docs/计划与分工/计算机图形学课程26年_孙含睿_B3民族建筑景区3DGS沉浸式导览与游览路线推荐系统_项目计划书.docx)
- [三人任务表](Docs/计划与分工/三人任务表_前两周Garden复建与增强.md)

Garden 数据来自 [VR27 3DGS DataSet](https://huggingface.co/datasets/warmstones/VR27_3DGS_DataSet/tree/main/scenes/garden)，其元数据指向 [Mip-NeRF 360 Garden](https://jonbarron.info/mipnerf360/) 和 [Graphdeco 官方预训练模型](https://repo-sam.inria.fr/fungraph/3d-gaussian-splatting/)。模型仅用于研究与课程演示；使用和再分发时应遵守[上游许可](Docs/GaussianSplatting-LICENSE.md)。渲染插件的 MIT 许可证保存在 `Packages/org.nesnausk.gaussian-splatting/LICENSE.md`。中文字体换为 [Noto Sans SC](https://github.com/notofonts/noto-cjk)，许可证见[字体许可文件](Docs/NotoSansSC-OFL.txt)。
