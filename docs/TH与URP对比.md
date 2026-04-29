# topheroes-client 渲染管线差异说明

## 目的

本文用于说明当前项目渲染管线与"默认 URP21 思路"的差异，帮助后续评估升级或回归官方管线的成本。

当前结论先行：

- 当前项目不是默认 URP21 模板。
- 当前项目是 `URP 12.1.13` 的定制分支（位于 `CustomPackages`）。
- 差异不只在参数配置，还包括核心渲染流程和相机模型扩展。

---

## 一、当前项目实际渲染管线状态

- Unity 版本：`2021.3.45f2c1`
- URP 包来源：`Packages/manifest.json`
  - `com.unity.render-pipelines.universal: file:../CustomPackages/com.unity.render-pipelines.universal@12.1.13`
- 当前生效 RenderPipelineAsset：
  - `Assets/Settings/UniversalRP-PC-Only.asset`
  - 由 `ProjectSettings/GraphicsSettings.asset` 的 `m_CustomRenderPipeline` 引用
- 各 Quality 档（Low/Medium/High/PC_Only）同样指向这份资产：
  - `ProjectSettings/QualitySettings.asset` 的 `customRenderPipeline`

---

## 二、与默认 URP 的核心差异总览

### 1) 管线资产字段被扩展（不是官方默认字段集合）

主要扩展点在 `CustomPackages/com.unity.render-pipelines.universal@12.1.13/Runtime/Data/UniversalRenderPipelineAsset.cs`：

- 深度/不透明纹理引用计数开关
  - `m_EnableDepthTextureReferenceCounter`
  - `m_EnableOpaqueTextureReferenceCounter`
- 软粒子全局开关
  - `m_AllowSoftParticles`
- 后处理全局开关
  - `m_SupportsPostProcess`
- 分辨率绝对像素上下限
  - `m_MinRenderResolution`
  - `m_MaxRenderResolution`
- FXAA 执行位置开关
  - `m_DoFxaaInUberPost`

这类字段带有自定义注释，属于项目改造而非标准模板直出。

### 2) 相机模型扩展（Base/Overlay/UI 三态）

在 `CustomPackages/com.unity.render-pipelines.universal@12.1.13/Runtime/UniversalAdditionalCameraData.cs`：

- `CameraRenderType` 包含 `UI`（官方常见为 Base/Overlay）
- 新增 `disableRender`：
  - 可以跳过渲染流程，但不禁用 Camera 组件
- 通过扩展文件 `UniversalAdditionalCameraData_Ext.cs` 增加：
  - `dynamicRenderScale`

### 3) RenderScale 计算策略扩展（DPI 模式 + 动态倍率）

在 `CustomPackages/com.unity.render-pipelines.universal@12.1.13/Runtime/UniversalRenderPipeline_Ext.cs`：

- 新增 `RenderResolutionMode`：
  - `CustomRenderScale`
  - `RenderDPI`
  - `UnityRenderScale`
- 新增全局覆盖值：
  - `UniversalRenderPipeline.overrideRenderScale`
- 最终 renderScale 计算额外乘相机级动态倍率：
  - `dynamicRenderScale`

这部分会直接影响全局分辨率行为，和默认 URP 模板不同。

### 4) 主渲染循环被改写

在 `CustomPackages/com.unity.render-pipelines.universal@12.1.13/Runtime/UniversalRenderPipeline.cs`：

- 遇到 `disableRender` 的相机直接跳过
- `CameraRenderType.UI` 相机会先收集后统一渲染
- AA 逻辑支持 `UsePipelineSettings` 回落资产级配置
- UI 相机在后处理、阴影、depth copy 等路径有特殊分支

### 5) Renderer 与 Clear/Attachment 行为有额外分支

在以下文件：

- `CustomPackages/com.unity.render-pipelines.universal@12.1.13/Runtime/UniversalRenderer.cs`
- `CustomPackages/com.unity.render-pipelines.universal@12.1.13/Runtime/ScriptableRenderer.cs`

可见的行为差异包括：

- UI 相机直接绑定 `CameraTarget` 的 color/depth attachment
- Skybox 对 UI 相机单独过滤
- `CopyDepthPass` 对 UI 和 `MiniMap` 标签相机单独处理
- `ScriptableRenderer.ClearRenderingState()` 增加了自定义 keyword 清理：
  - `DepthTextureOn`
  - `OpaqueTextureOn`
  - `AllowSoftParticles`
- `Clear()` 初次绑定标志里将 `UI` 视作与 `Base` 同类处理

---

## 三、资产配置差异（当前项目不是模板参数）

### 1) 当前生效资产：`Assets/Settings/UniversalRP-PC-Only.asset`

可见非默认倾向配置（示例）：

- `m_ResolutionMode: 1`
- `m_StandaloneRenderDPI: 300`
- `m_EnableFxDistortionInPost: 1`
- `m_MinRenderResolution: 1920`
- `m_MaxRenderResolution: 2144`

### 2) 当前主 Renderer：`Assets/Settings/ForwardRenderer.asset`

挂载了多组项目特性（默认模板通常不会有）：

- `Render Effects`（RenderObjects）
- `PreZ`（RenderObjects）
- `NewOutlineRenderFeature`
- `NewShadowRenderFeature`
- `ScreenSpaceDistortion`（当前关闭）

### 3) UI Renderer：`Assets/Settings/URPSettingsFromCProject/UniversalRenderPipelineAsset_RendererUI.asset`

存在一套 UI 专用 RenderFeature 链，含：

- Blur 系列（多层）
- FogRenderPassFeature
- GrabRenderPassFeature
- UI Gamma Correction
- RenderObjects（部分开关）

这说明项目有明确的分场景/分相机渲染策略，不是单一默认 ForwardRenderer。

---

## 四、Blur（UI）实现补充说明

本节补充 `BlurURP` 的具体实现细节，便于后续优化和迁移时校验行为一致性。

### 1) 执行范围

- `BlurURP` 仅在 `CameraRenderType.UI` 下入队执行。
- 入口逻辑在 `AddRenderPasses()`，明确判断 `renderingData.cameraData.renderType == CameraRenderType.UI`。

### 2) Tap 与迭代次数

- 模糊 Shader：`Assets/_Art_LastDay/Shader/Blur.shader`
- 单次 fragment 采样：
  - 中心 1 次
  - 对角 4 次
  - 合计 `5 tap / pass`
- `BlurURP.cs` 当前把 `passes` 固定为 `3`（代码常量覆盖 Inspector 配置）。

因此当前模糊主路径约为：

- `3 pass * 5 tap = 15 tap`

另有前后普通 Blit 拷贝（不属于 blur shader 采样）：

- 抓帧拷贝 1 次
- 输出拷贝 1 次

### 3) 是否降采样

- 有降采样。
- `BlurURP.cs` 中模糊 RT 的尺寸按 `width / downsample`、`height / downsample`。
- 当前 `downsample` 同样被代码固定为 `3`，即模糊计算在约 `1/3` 尺寸上进行（像素量约 `1/9`）。

### 4) 触发方式（按需更新）

- 通过 `BlurURP.EnableFeature(layer)` 将 `IsCapter` 置为 `false` 后触发一次捕获和模糊。
- 默认并非每帧重算，属于按需更新机制。
- 模糊结果写入全局纹理：`blurURP_<layer>`，供后续材质/特效采样。

---

## 五、UI 相机处理细节

本节专门说明当前项目对 UI 相机的处理方式。这是项目最重的定制点之一，直接影响主循环、Renderer、Clear、RenderFeature 五个层级。

### 1) 调度层：UI 相机被"延后单独渲染"

文件：`CustomPackages/com.unity.render-pipelines.universal@12.1.13/Runtime/UniversalRenderPipeline.cs`

- 主循环遍历 `cameras` 时：
  - `disableRender == true` 的相机直接 `continue`（不渲染但不禁用 Camera 组件）
  - `renderType == UI` 的相机被收集到独立列表 `uiCameraList` / `uiCameraAdditionalDataList`，本轮**先不渲染**
  - 其他相机正常走 `RenderCameraStack`
- 在所有非 UI 相机渲染完后，统一遍历 `uiCameraList` 单独渲染。

效果：UI 相机一定排在所有 3D/Game 相机之后，且不参与常规 Camera Stack 的合成顺序。

### 2) Camera Stack 层：UI 不参与 Overlay 堆栈

- Base 相机的 stack 列表中如果出现 `renderType == UI` 的相机，会被跳过/排除，不被当作 Overlay 处理。
- UI 相机不会以 Overlay 的方式叠加到 Base 相机的 RT 上，而是走自己的渲染流程，直接输出到 `CameraTarget`。

### 3) CameraData 初始化：UI 相机被强制限制能力

在 UI 相机的 `CameraData` 初始化路径中（`InitializeAdditionalCameraData` 等）：

- `postProcessEnabled` 强制为 `false`（UI 不走后处理）
- 阴影相关：`maxShadowDistance` 置 0（UI 不参与阴影计算）
- `requiresOpaqueTexture` 关闭（UI 不需要 `_CameraOpaqueTexture`）
- `renderScale` 强制为 `1.0`（UI 不跟随全局 RenderScale / DPI 缩放）
- 不应用 `dynamicRenderScale` 倍率

效果：UI 相机走的是"高分辨率、无后处理、无阴影、无 Opaque Texture"的轻量路径，避免 UI 因为 RenderScale<1 出现糊字。

### 4) Renderer 层：UI 走 CameraTarget 直绑分支

文件：`CustomPackages/com.unity.render-pipelines.universal@12.1.13/Runtime/UniversalRenderer.cs`

- 颜色与深度附件分配：
  - 非 UI：`m_ColorBufferSystem.GetBackBuffer()` + `m_CameraDepthAttachment`（中间 RT）
  - UI：`RenderTargetHandle.CameraTarget` + `RenderTargetHandle.CameraTarget`（直接绑后台 buffer）
- Skybox：当 `clearFlags == Skybox` 且 `renderType == UI` 时**不画 Skybox**
- CopyDepth：UI 相机和 `MiniMap` 相机会**跳过 CopyDepthPass**

效果：UI 相机省掉了一次中间 RT 的分配 + Skybox 绘制 + 一次深度 Copy，大量降低带宽和 setpass。

### 5) Clear / 状态层：UI 有专门清屏与首帧绑定逻辑

文件：`CustomPackages/com.unity.render-pipelines.universal@12.1.13/Runtime/ScriptableRenderer.cs`

- `GetCameraClearFlag()` 中 UI 分支独立：
  - `CameraClearFlags.Color` → `ClearFlag.All`
  - 其他 → 仅按 `clearDepth` 决定是否清深度
- `Clear(CameraRenderType)` 中：
  - `m_FirstTimeCameraColorTargetIsBound` 对 `Base` 与 `UI` 都置 true
  - 即 UI 相机被视为"首次绑定颜色目标"的主体之一，与 Base 同级，而不是 Overlay
- `ClearRenderingState()` 每帧清理自定义全局 keyword：
  - `DepthTextureOn` / `OpaqueTextureOn` / `AllowSoftParticles`
  - 由 CopyDepth/CopyColor Pass 按需重新打开

### 6) Feature 层：按 `renderType` 做"UI 专用 / 非 UI 专用"切流

具体 RenderFeature 在 `AddRenderPasses()` 中根据 `cameraData.renderType` 分流，例如：

- `BlurURP`：仅当 `renderType == UI` 时入队（UI 模糊背板）
- 其他业务 Feature（Outline / Shadow / Fog / Grab 等）：根据需要判断"UI 专用 / 非 UI 专用"，不会在所有相机都跑

效果：同一份 RendererData 可以被 UI 和非 UI 相机共用，但运行时执行的 Pass 集合按相机类型动态裁剪。

### 7) 与官方 URP Overlay 的本质区别

| 维度 | 官方 URP Overlay | 当前项目的 UI 相机 |
| --- | --- | --- |
| 调度位置 | 跟随 Base Stack 的下一帧顺序 | 主循环结束后单独遍历 |
| 输出目标 | 写入 Base 的中间 RT | 直接写 `CameraTarget` |
| 后处理 | 跟随 Base 的后处理设置 | 强制关闭后处理 |
| RenderScale | 跟随全局 + dynamic | 强制 1.0 |
| Skybox / CopyDepth | 走完整流程 | 跳过 |
| 在 RenderFeature 中的可识别度 | 仅 Base/Overlay | 多了 `UI` 一态可判别 |

结论：项目的 UI 相机不是 Overlay 的别名，而是一条**完全独立的轻量渲染路径**。这是大多数 SLG / 手游 UI 渲染量大、清晰度要求高场景的常见妥协方案，和官方 URP 默认模板差异显著。

---

## 六、与"默认 URP21 思路"对比的关键结论

> 下列内容不是版本号比较，而是功能与结构层面的差异判断。

- 当前项目是 URP12 定制分支，不是官方默认模板。
- 项目渲染逻辑依赖了自定义字段与扩展流程，不能简单替换包版本。
- UI 相机三态模型（Base/Overlay/UI）是关键定制点，影响主循环和 Renderer。
- 分辨率策略（DPI + override + dynamicRenderScale）是关键业务逻辑，迁移时必须保留或等价替代。
- RendererFeature 资产链路较重，升级时属于高风险区。

---

## 七、后续如果要升级/对齐官方 URP 的建议顺序

1. 冻结并文档化当前行为（本文件完成第一步）
2. 先做"行为兼容层"而不是先删改特性
3. 优先保留三块核心定制：
   - UI 相机三态
   - 分辨率计算策略
   - 关键 RendererFeature（Shadow/Outline/Blur/Fog/Grab）
4. 做场景级回归清单：
   - 主城
   - 大地图
   - 战斗
   - UI 叠加
5. 最后再做性能和画质调参，不要与功能迁移并行

---

## 八、参考文件索引

- `Packages/manifest.json`
- `ProjectSettings/GraphicsSettings.asset`
- `ProjectSettings/QualitySettings.asset`
- `Assets/Settings/UniversalRP-PC-Only.asset`
- `Assets/Settings/ForwardRenderer.asset`
- `Assets/Settings/ForwardRendererForRTCamera.asset`
- `Assets/Settings/URPSettingsFromCProject/UniversalRenderPipelineAsset_RendererUI.asset`
- `CustomPackages/com.unity.render-pipelines.universal@12.1.13/Runtime/RendererFeatures/BlurURP.cs`
- `Assets/_Art_LastDay/Shader/Blur.shader`
- `CustomPackages/com.unity.render-pipelines.universal@12.1.13/Runtime/UniversalRenderPipeline.cs`
- `CustomPackages/com.unity.render-pipelines.universal@12.1.13/Runtime/UniversalRenderPipeline_Ext.cs`
- `CustomPackages/com.unity.render-pipelines.universal@12.1.13/Runtime/UniversalAdditionalCameraData.cs`
- `CustomPackages/com.unity.render-pipelines.universal@12.1.13/Runtime/UniversalAdditionalCameraData_Ext.cs`
- `CustomPackages/com.unity.render-pipelines.universal@12.1.13/Runtime/UniversalRenderer.cs`
- `CustomPackages/com.unity.render-pipelines.universal@12.1.13/Runtime/ScriptableRenderer.cs`
- `CustomPackages/com.unity.render-pipelines.universal@12.1.13/Runtime/Data/UniversalRenderPipelineAsset.cs`

