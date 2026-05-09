# Tools 工具集说明

本目录包含项目中所有自定义 Editor 工具，统一通过 Unity 菜单栏 **nTools** 访问。

---

## 目录结构

```
Tools/Editor/
├── ArtTools/               # 美术工具
│   ├── BatchAssetRenamer.cs        # 批量重命名
│   ├── BentNormalBakeTool.cs       # Bent Normal 烘焙
│   ├── PrefabAssetExtractor.cs     # Prefab 资源快速复制
│   ├── SDFGenerator.cs             # SDF 生成器
│   ├── SmoothNormalBaker.cs        # 平滑法线烘焙
│   └── TextureNormalizer.cs        # 贴图规范化
├── TATools/                # TA 工具
│   ├── ChannelRemapper.cs          # 通道重映射
│   ├── SRPBatcherChecker.cs        # SRP Batcher 兼容性检查
│   ├── TextureDebug.shader         # 贴图调试 Shader
│   └── TextureDebugShaderGUI.cs    # 贴图调试 ShaderGUI
└── OptimizeTool/           # 性能优化工具
    └── SceneOptimizeTool.cs        # 场景优化
```

---

## 菜单路径一览

| 菜单路径 | 工具名称 | 分类 |
|---------|---------|------|
| `nTools/美术工具/批量重命名` | BatchAssetRenamer | 美术工具 |
| `nTools/美术工具/贴图规范化` | TextureNormalizer | 美术工具 |
| `nTools/美术工具/SDF Generator` | SDFGenerator | 美术工具 |
| `nTools/美术工具/Outline平滑法线烘焙` | SmoothNormalBaker | 美术工具 |
| `nTools/美术工具/Bent Normal Baker` | BentNormalBakeTool | 美术工具 |
| `nTools/美术工具/Prefab资源快速复制` | PrefabAssetExtractor | 美术工具 |
| `nTools/TA工具/通道重映射` | ChannelRemapper | TA工具 |
| `nTools/TA工具/SRP Batcher Checker` | SRPBatcherChecker | TA工具 |
| `nTools/性能优化/场景优化` | SceneOptimizeTool | 性能优化 |

---

## 工具详细说明

### 一、美术工具 (ArtTools)

#### 1. 批量重命名 (BatchAssetRenamer)

- **菜单路径**：`nTools/美术工具/批量重命名`
- **功能**：对选中文件夹内的资产进行批量重命名，按序号排列（01, 02, ... 或 001, 002, ...）
- **使用方法**：
  1. 将文件夹或散文件拖入工具窗口
  2. 设置重命名前缀
  3. 点击执行

#### 2. 贴图规范化 (TextureNormalizer)

- **菜单路径**：`nTools/美术工具/贴图规范化`
- **功能**：根据贴图文件名后缀自动设置 sRGB 和 Texture Type
- **默认规则**：
  - 以 `D` 结尾 → 勾选 sRGB（Diffuse / BaseColor 等颜色贴图）
  - 以 `N` 结尾 → Texture Type 设为 Normal Map，关闭 sRGB
  - 其他 → 关闭 sRGB（Mask、AO、金属度等线性数据贴图）
- **使用方法**：
  1. 在 Project 视图中选择一个或多个文件夹
  2. 打开工具窗口
  3. 根据需要修改后缀匹配规则（可填写多个后缀，用逗号分隔）
  4. 点击"执行规范化"

#### 3. SDF 生成器 (SDFGenerator)

- **菜单路径**：`nTools/美术工具/SDF Generator`
- **功能**：将贴图指定通道转换为有向距离场（SDF），输出带 `_sdf` 后缀的 PNG 贴图
- **适用场景**：植被 Alpha-Clip 贴图等
- **使用方法**：
  1. 选择要处理的贴图
  2. 选择源通道（R/G/B/A）
  3. 设置阈值和扩散范围
  4. 点击生成

#### 4. Outline 平滑法线烘焙 (SmoothNormalBaker)

- **菜单路径**：`nTools/美术工具/Outline平滑法线烘焙`
- **功能**：使用角度加权算法计算模型的平滑法线，转换到切线空间后存入指定 UV 通道（默认 UV3 / TEXCOORD3）的 xyz 中，供 PBRToon 描边使用
- **算法**：参考 Best-Smooth-Normal-Tool，按顶点位置分组，以三角面夹角为权重累加面法线 → 归一化得到对象空间平滑法线 → 用 TBN 矩阵的转置转换到切线空间
- **数据编码**：`TEXCOORDn.xyz = tangentSpaceSmoothNormal.xyz`（3 分量直接存储，无压缩，Shader 读到后乘 TBN 即可还原对象空间法线）
- **UV 通道分配约定（默认）**：
  - UV0 (TEXCOORD0)：主纹理坐标 (2D)
  - UV1 (TEXCOORD1)：Lightmap / 自定义数据 (2D)
  - UV2 (TEXCOORD2)：Bent Normal 数据 (3D / 4D，由 Bent Normal Baker 写入)
  - UV3 (TEXCOORD3)：平滑法线 (3D，本工具写入)
  - 工具支持烘焙到 **UV0~UV7 任意通道**（默认 UV3，可在窗口下拉框选择）
- **关于 UV 通道维度**：
  - Unity 的 UV 通道（TEXCOORD0~7）每个最多可存 **4 个 float 分量**（不是只能存 2 个 UV 坐标）
  - 不同通道可以是 2D / 3D / 4D，本工具会用 `Mesh.GetVertexAttributeDimension` 查询并保留每个非目标通道的原始维度（不会把 3D 数据降为 2D）
  - 因此切换目标通道时，其它通道的 baked 数据（如 UV2 上的 Bent Normal）能完好保留
- **输出策略**：
  - 始终在源 Mesh 同目录生成 `xxx_SmoothN.asset`，**不会修改任何原始 .asset 或 FBX 文件**
  - 列表中 `[H]` 条目（来自场景）烘焙后会自动把 GameObject 的 MeshFilter / SkinnedMeshRenderer 引用替换为新 `_SmoothN.asset`
  - 列表中 `[P]` 条目（来自 Project）只生成文件，不动场景
- **目标 UV 通道占用警告**：若选中的 UV 通道在某些 Mesh 上已有数据，列表中会显示 ⚠ 标记，提示烘焙将覆盖该通道
- **还原原始资源 ↺**：
  - 用于撤销 `[H]` 烘焙：把场景对象当前引用的 `mesh_SmoothN` 还原为原始 `mesh`（去掉后缀的同名资源）
  - 在同目录 → 上一级目录 → 全工程依次搜索原始 Mesh，优先返回 FBX/OBJ 等 Model 子 Mesh
  - **默认只改场景 / Prefab 上的引用，不会修改 .asset 文件**
  - 旁边的「删除原始资源」勾选后，还原成功的条目会同步删除被替换掉的 `_SmoothN.asset` 文件；为防误删，仅当工具列表中没有其它条目仍引用该资源时才会真正删除
- **使用方法**：
  1. 在 Hierarchy / Project 中选择含 MeshFilter / SkinnedMeshRenderer 的对象（也可拖拽到工具窗口的拖入区）
  2. 打开工具窗口，按需在"写入 UV 通道"下拉框选择目标通道
  3. 检查列表中是否有 ⚠ 警告，若有数据会被覆盖
  4. 点击"▶ 烘焙选中的 N 个 Mesh 到 UVx"
  5. 想撤销时，选中场景中的 `_SmoothN` 条目，点"↺ 用原始资源替换选中的 N 个"
- **Shader 解码**（参考 `Babylon/Assets/Effect/PBRToon/Shaders/PBRToonBase.shader`）：
  ```hlsl
  float3 snTS = input.uvN.xyz;          // 从烘焙的目标 UV 通道取出 3 分量
  float3 T = normalize(tangentOS.xyz);
  float3 N = normalize(normalOS);
  float3 B = normalize(cross(N, T) * tangentOS.w);
  float3 smoothNormalOS = normalize(T * snTS.x + B * snTS.y + N * snTS.z);
  ```

#### 5. Bent Normal 烘焙 (BentNormalBakeTool)

- **菜单路径**：`nTools/美术工具/Bent Normal Baker`
- **功能**：CPU Raycast 方式烘焙 Bent Normal，将数据编码为 `Vector4(relativeB, theta, aperture, scale)` 存入 Mesh 的 UV2（TEXCOORD2）通道
- **特点**：不依赖 DXR 硬件光追，适配标准 URP 项目
- **使用方法**：
  1. 选择需要烘焙的模型
  2. 打开工具窗口
  3. 设置采样参数
  4. 点击烘焙

#### 6. Prefab 资源快速复制 (PrefabAssetExtractor)

- **菜单路径**：`nTools/美术工具/Prefab资源快速复制`
- **功能**：将选中 Prefab 中引用的贴图、模型、材质球复制到指定目录，并按类型分类存放。支持将 Prefab 内的资产引用替换为新复制出来的资产
- **复制模式**：
  - 全部复制（模型 + 材质 + 贴图）
  - 仅复制模型
  - 仅复制材质和贴图
- **使用方法**：
  1. 将 Prefab 拖入工具窗口
  2. 设置输出目录和命名前缀
  3. 选择复制模式
  4. 点击执行

---

### 二、TA 工具 (TATools)

#### 1. 通道重映射 (ChannelRemapper)

- **菜单路径**：`nTools/TA工具/通道重映射`
- **功能**：重新调整贴图 RGBA 各通道的位置，并支持各通道的数值反转（1 - x）。输出贴图以 `_ChanFix` 为后缀保存
- **使用方法**：
  1. 在 Project 视图中选择贴图或文件夹
  2. 打开工具窗口
  3. 可使用后缀筛选来过滤文件
  4. 在文件列表中勾选需要处理的贴图
  5. 为输出的 R/G/B/A 各通道指定来源通道
  6. 点击"重映射并保存"

#### 2. SRP Batcher 兼容性检查 (SRPBatcherChecker)

- **菜单路径**：`nTools/TA工具/SRP Batcher Checker`
- **功能**：批量检查 Shader 是否兼容 SRP Batcher，并输出不兼容的原因
- **使用方法**：
  1. 打开工具窗口
  2. 将需要检查的 Shader 拖入列表（或点击"添加场景中使用的 Shader"批量添加）
  3. 点击"检查"按钮
  4. 查看结果列表：✅ 兼容 / ❌ 不兼容（附原因说明）

#### 3. 贴图调试 Shader (TextureDebug)

- **功能**：通用贴图调试 Shader（Unlit），以可视化方式单独查看各通道数据
- **支持的调试模式**：
  - 贴图通道：RGB / R / G / B / A
  - 顶点色：RGB / R / G / B / A
  - 法线：Normal Map / Mesh Normal / Smooth Normal (UV3)
  - UV 坐标：UV0 / UV1
- **使用方法**：在材质球上选择 `Hidden/Tools/TextureDebug` Shader，通过 Inspector 切换调试模式

---

### 三、性能优化工具 (OptimizeTool)

#### 1. 场景优化 (SceneOptimizeTool)

- **菜单路径**：`nTools/性能优化/场景优化`
- **功能**：提供特效、材质、模型面数三个维度的场景检查功能
- **检查维度**：
  - **特效**：粒子数量上限、发射速率、LineRenderer 顶点数
  - **材质**：贴图尺寸检查
  - **模型**：高面数模型检测
- **使用方法**：
  1. 打开工具窗口
  2. 选择检查维度（特效 / 材质 / 模型面数）
  3. 根据需要调整阈值参数
  4. 执行检查查看结果
