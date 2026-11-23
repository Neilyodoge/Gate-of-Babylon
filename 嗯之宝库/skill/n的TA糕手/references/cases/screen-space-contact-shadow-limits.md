# TA-37 · 接触阴影：补细节前先验离屏退化

**制作决定**：先关接触阴影，单独看阴影贴图。如果角色脚下或墙根只剩分离的投影，先查主阴影的偏移、分辨率与覆盖。主阴影形状正确、只有贴近表面的小细节缺失时，再对需要的灯试短距离屏幕空间接触阴影。遮挡物离屏后必须保留的形状，要由覆盖这些投影物的主阴影路径承担。

## 原始对照和实现边界

- **静态原图 · Epic**：[Contact Shadows · Scenes with Contact Shadows](https://dev.epicgames.com/documentation/unreal-engine/contact-shadows-in-unreal-engine#scenes-with-contact-shadows) 的同一藤蔓门洞，[Contact Shadow Off](https://dev.epicgames.com/community/api/documentation/image/08805be0-e892-41d4-8605-d0a9c8918d7e?resizing_type=fit) 与 [Light's Contact Shadow Length = 0.1](https://dev.epicgames.com/community/api/documentation/image/e5f65d06-84c9-454e-8308-6d4c8f418028?resizing_type=fit) 相比，开启后门洞左侧的浅墙和门框边缘，多出了更清楚的叶片暗纹；原有大块明暗仍可辨。这张图只验证屏内静态细节，不展示离屏或显露。Epic 的[长度说明](https://dev.epicgames.com/documentation/unreal-engine/contact-shadows-in-unreal-engine#enabling-contact-shadows)指出，常规屏幕空间模式拉长射线时采样数保持不变，较长值更容易有噪点；`0.1` 是示例值，不是跨场景推荐。文档还说明启用 Virtual Shadow Maps 后，锐利接触细节通常不需要额外开接触阴影；未写进阴影图的物体仍可能受益。
- **屏幕边界 · Unity 2022.3 HDRP**：[ContactShadows.compute](https://github.com/Unity-Technologies/Graphics/blob/2022.3/staging/Packages/com.unity.render-pipelines.high-definition/Runtime/Lighting/Shadow/ContactShadows.compute#L74-L143) 在 UV 与深度上步进；采样坐标越出 `[0,1]` 就退出循环，命中靠屏边时另有渐隐。未被当前相机深度记录的遮挡物也不会作为这条射线的命中来源。调整步数和距离只能改变现有深度的取样。[HDRP 的覆盖项文档](https://github.com/Unity-Technologies/Graphics/blob/2022.3/staging/Packages/com.unity.render-pipelines.high-definition/Documentation~/Override-Contact-Shadows.md#properties) 有独立 Sample Count、Bias 和渐入/渐出距离；它与 UE 的长度、采样设置不能逐值互换。
- **投影资格也要单独验。** [HDRP 2022.3 文档](https://github.com/Unity-Technologies/Graphics/blob/2022.3/staging/Packages/com.unity.render-pipelines.high-definition/Documentation~/Override-Contact-Shadows.md#using-contact-shadows) 写明，写入相机深度的 Mesh Renderer 即使关掉自己的 `Cast Shadows`，也仍可贡献接触阴影；Epic 的[设置说明](https://dev.epicgames.com/documentation/unreal-engine/contact-shadows-in-unreal-engine#contactshadowsettings)则说图元的 `Contact Shadow` 设置要求开启 `Cast Shadow`。移植材质或为小件取消主阴影时，在目标管线里分别观察两个阴影阶段。
- **主阴影偏移 · Microsoft**：[Shadow Depth Maps · Peter Panning](https://learn.microsoft.com/en-us/windows/win32/dxtecharts/common-techniques-to-improve-shadow-depth-maps#peter-panning) 解释了光空间深度偏移过大会使投影与物体分离；偏移太小又容易出现自阴影斑点。只加屏幕空间暗边可能暂时盖住间隙，保留一组关闭接触阴影的画面判断主阴影是否已修好。

## 同一镜头怎样验收

1. 固定光源、材质、曝光、接收面和遮挡物，准备脚下贴地与墙面细件两个位置。先关接触阴影，从主阴影检查接触间隙和自阴影斑点；单测光空间偏移，仍缺时再查覆盖或分辨率。恢复基准后只开一个灯的短程接触阴影，比较细节新增在哪里、墙边是否产生假阴影。
2. 沿固定路线移动镜头，让遮挡物跨出屏幕而地面投影仍在视野内；尽量保持接收面距离一致，以免把离屏与距离渐隐混在一起。分别记录关闭与开启时固定世界坐标处的接收亮度、阴影是否突变。可抓帧时同时看相机深度、接触阴影遮罩与主阴影。另测遮挡物被前景挡住但不离屏的情况，确认深度不可见的边界。
3. 场内比较关闭、短程和拉长，固定分辨率、灯和路径；支持调步数时单独比步数。记录噪点、假命中、接触阴影阶段与整帧 GPU 时间，再用目标同时亮灯数复测。重要投影必须在多个镜头下持续可读时，优先修主阴影，接触阴影只补可接受丢失的局部。

**状态**：已核对 Epic 同构图静态原图、Unity HDRP 源码和阴影偏移文档；未取得遮挡物离屏的动态同镜头对照，没有本地或真机测量。
