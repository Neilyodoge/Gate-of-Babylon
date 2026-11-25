# APV 昼夜方案变了，墙的 lightmap 没变

**TA-215 · 制作决定。** Unity 6000.0 URP 中，Lighting Scenario 切换或混合的是 APV 探针的烘焙光照。角色响应而墙没响应时，先确认墙是采 lightmap 还是 APV；把混合因子从 0 调到 1 并不会替换墙正在用的 lightmap。需要墙也随昼夜变化时，先评估把墙改为探针接收并重烘的画质；需要保留贴图里的细节时，lightmap 另做版本管理与切换。实时灯、天空和反射要在各自路径处理。

## 公开证据

- [Unity 6000.0 · Bake different lighting setups with Lighting Scenarios](https://docs.unity3d.com/6000.0/Documentation/Manual/urp/probevolumes-bakedifferentlightingsetups.html) 明说切换只改变 Light Probes 的间接数据，移动几何、灯属性与实时直射光要另处理。使用 `ProbeReferenceVolume.instance.lightingScenario` 选当前方案，启用有效 URP 资产的 **Lighting Scenarios** 与 **Scenario Blending** 后用 `BlendLightingScenario` 指向另一方案，示例还控制每帧混合的 cell 数。
- [URP 6000.0 · GlobalIllumination.hlsl](https://raw.githubusercontent.com/Unity-Technologies/Graphics/refs/heads/6000.0/staging/Packages/com.unity.render-pipelines.universal/ShaderLibrary/GlobalIllumination.hlsl) 的 `SAMPLE_GI` 在 `LIGHTMAP_ON` 分支调用 `SampleLightmap`；未走 lightmap 路径且启用探针体积变体时才采 APV。选中墙的实际 Renderer 和材质验证这一路径；[MeshRenderer.receiveGI](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/MeshRenderer-receiveGI.html) 是编辑器设置，不能把它当成 Player 里的逐帧切换手段。
- [Baking Sets](https://docs.unity.com/en-us/engine/6000.0/manual/lighting-overview/lighting/probevolumes/usebakingsets) 说明同场加载的多个 APV 场景要归同一 Baking Set，一场景只能归一个。需混合的两个方案要有相同探针数量和位置。按手册先烘第一套，再切下一套，并在 **Probe Placement → Probe Positions** 选 **Don't Recalculate**；如果更改了静态几何，复用位置前仍需验漏光与遮挡。[ProbeReferenceVolume.cs](https://raw.githubusercontent.com/Unity-Technologies/Graphics/refs/heads/6000.0/staging/Packages/com.unity.render-pipelines.core/Runtime/Lighting/ProbeVolume/ProbeReferenceVolume.cs) 的混合接口转给当前烘焙组，支持条件还检查计算着色器与非零混合内存预算。

## 墙和移动球的最小对照

同一室内，使用两套亮度明确不同的烘焙方案，以固定曝光、材质、实时灯和反射作基线。A 组让墙收 lightmap，移动球收 APV；B 组把同一面墙在编辑器设置为收 Light Probes / APV，在相同位置重烘两个方案。依次设混合为 0、0.5、1，每次等探针 cell 更新稳定再采相同画面点。同一质量档下同时记探针方案、已加载 lightmap 资源与当前烘焙 GI，避免最终画面的直射、高光或色调映射遮住差别。

简单对照值：若测试点探针的某一线性分量两端为 0.25、0.75，中点为 0.50；同一组中墙若一直从已加载的 lightmap 取 0.40，三个采样都是 0.40。这是对取样路径的辨识算例，不是实际像素或亮度预测；也不能确定墙当前 lightmap 对应哪一次烘焙。若墙转为 APV 后过渡正确但墙角变软或漏光，再和原贴图在游戏视距比较，必要时保留贴图并单独切换。若球也不变，再查资产开关、方案数据、探针布局和混合预算，而不是先改墙的材质。

**状态**：核对了 Unity 6000.0 官方手册、URP 与 Core 源码，复算了单分量例子；未在编辑器实际烘焙、抓帧或真机验证。
