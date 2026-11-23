# 薄面叶片照明：先拆开背面法线、透射和阴影

**一句话结论**：背光叶片发黑，先在固定镜头下单独开关阴影，隔离遮挡；再测试背面法线和薄材质透射。以 Unity HDRP 的 Lit 为例，换法线模式会改透射的角度，薄透射仍会被直射光阴影压暗。仅提亮底色容易连正面一起改亮。

## 可核对的证据

- **官方画面对照**：[Epic Shading Models · Two Sided Foliage](https://dev.epicgames.com/documentation/unreal-engine/shading-models-in-unreal-engine#twosidedfoliage) 的滑块中，同一位置左侧花瓣从接近黑红变为仍可读出红色和花心；文档用 Subsurface Color 调透射，可用纹理遮罩让叶脉或茎的透射更弱。这个图切换整个着色模型，不能把变化单独归给法线、透射或阴影。
- **公开实现，法线**：Unity Graphics 2022.3/staging 的 [MaterialUtilities.hlsl，ApplyDoubleSidedFlipOrMirror / GetNormalWS](https://github.com/Unity-Technologies/Graphics/blob/2022.3/staging/Packages/com.unity.render-pipelines.high-definition/Runtime/Material/MaterialUtilities.hlsl#L28-L69) 中，None 保留基底；Flip 和 Mirror 都翻转背面的基底法线，Flip 还反向切线空间中的扰动。[LitData.hlsl 的材质数据入口](https://github.com/Unity-Technologies/Graphics/blob/2022.3/staging/Packages/com.unity.render-pipelines.high-definition/Runtime/Material/Lit/LitData.hlsl#L211-L273) 随后分别得到几何法线和着色法线。平坦法线贴图下若 Flip/Mirror 差异不明显，用带方向的叶脉法线继续比。
- **公开实现，透射与阴影**：[Lit.hlsl 的 EvaluateBSDF](https://github.com/Unity-Technologies/Graphics/blob/2022.3/staging/Packages/com.unity.render-pipelines.high-definition/Runtime/Material/Lit/Lit.hlsl#L1321-L1425) 用同一个着色法线计算 N·L：正面漫反射用截断后的值，漫透射用反号后的包裹漫反射角度函数。[SurfaceShading.hlsl 的合光与方向光分支](https://github.com/Unity-Technologies/Graphics/blob/2022.3/staging/Packages/com.unity.render-pipelines.high-definition/Runtime/Lighting/SurfaceShading.hlsl#L40-L98) 中，反射和透射共享已受阴影影响的光色；背光侧略过微阴影，并不因此略过投射阴影。厚物体透射的方向光分支另用预计算值，不能混同。改变双面法线也可能影响阴影偏移：该 Lit 源码的 [GetNormalForShadowBias](https://github.com/Unity-Technologies/Graphics/blob/2022.3/staging/Packages/com.unity.render-pipelines.high-definition/Runtime/Material/Lit/Lit.hlsl#L192-L200) 在前向用几何法线，其他分支用着色法线。这里的实现结论只针对所查 HDRP 版本，不能直接套给 Epic 的模型。
- **资产做法线索**：[UE4 的 SpeedTree 树冠讨论](https://forums.unrealengine.com/t/twosided-foliage-shading-model/298609/2) 建议抵消背面法线翻转以保住朝冠外的近似法线；适用于用卡片代表叶簇的场合。讨论图未核验，不能据此给每片几何叶统一使用同样做法。

## 怎么对照

1. 同一张叶片，固定相机、曝光、光照强度、底色、法线贴图和 Alpha 阈值。先关投射阴影，只切透射蒙版，再只切双面法线；对照正照、背照和接近掠射的三组固定光位，记录叶面平均亮度、叶脉仍否可辨及切到背面时的突变。先用平坦法线贴图定位基底问题，再换带方向的叶脉法线。
2. 固定法线和透射，恢复阴影，并用独立挡板做有／无遮挡对照。若遮挡边界附近异常早于整个叶片发黑，单看阴影偏移和自阴影；若无阴影时角度一变仍突暗，回到着色法线与透射角度。然后才在实际叶簇和玩法距离复查。
3. 独立叶片和叶簇卡分别验收：独立叶片看两面是否都读得出合理的叶脉与明暗；叶簇卡看冠层明暗是否连续，是否仍读得出卡片轮廓。公开实现只能帮助选排查顺序；目标管线的实际效果和成本仍由 A/B 决定。

**状态**：已看 Epic 官方花瓣对照和 Unity HDRP 公开源码；未执行上述 A/B，未在本地或真机测画面和性能。
