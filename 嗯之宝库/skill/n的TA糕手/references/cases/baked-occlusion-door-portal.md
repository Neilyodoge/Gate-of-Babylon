# TA-126 · 烘焙遮挡后门开了仍看不见室内

**制作决定。** 门已经打开，门内物体仍消失时，先固定相机与门扇姿态，只切相机的烘焙遮挡开关。关遮挡才恢复，就继续查门洞状态与烘焙数据；关了仍不恢复，先查视锥、层和实际挡在前面的几何。可动门扇不当作静态墙烘进去；需要关门时省绘制，就保留静态门框和两侧墙体，另外用门洞状态控制遮挡。

## 公开证据

- [Unity 6000.0 · Occlusion Culling](https://docs.unity3d.com/6000.0/Documentation/Manual/OcclusionCulling.html) 区分视锥剔除和按烘焙数据查询的遮挡。相机可以分别关闭 [useOcclusionCulling](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Camera-useOcclusionCulling.html)，视锥仍有效。[动态对象说明](https://docs.unity3d.com/6000.0/Documentation/Manual/occlusion-culling-dynamic-gameobjects.html) 说明普通动态对象可被剔除，但不作为其他对象的烘焙遮挡体。门扇在深度上挡住室内像素，与室内渲染器是否提前被剔除，是两回事。
- [Unity 6000.0 · Occlusion Portal](https://docs.unity3d.com/6000.0/Documentation/Manual/class-OcclusionPortal.html) 的官方门例要求代表门的对象不标 Occluder Static／Occludee Static，再烘焙；门洞组件可以放在别的对象上。Portal 关闭时挡住穿过它的可见性，开启时这个 Portal 自身不再挡。手册的运行时代码先设 `open = true`，再播放开门动画，并提供 Visualization 里固定门前相机、只切 `Open` 的检查步骤。
- [Unity Occlusion Culling 窗口](https://docs.unity3d.com/6000.0/Documentation/Manual/occlusion-culling-window.html) 的 `Smallest Hole` 是相机可看过的最小缝隙直径，单位米；[故障排查](https://docs.unity3d.com/6000.0/Documentation/Manual/occlusion-culling-troubleshooting.html) 将减小它、复查背面阈值与静态遮挡标记列为可见性错误的排查项。烘焙精度与数据大小、烘焙时间须一起验。对照 [UE 5.5 · Precomputed Visibility Volumes](https://dev.epicgames.com/documentation/en-us/unreal-engine/precomputed-visibility-volumes-in-unreal-engine?application_version=5.5)，其预计算可见性由相机所在单元读取，文档限制不处理可移动 Actor；不要把 Unity 门洞开关直接套给别的预计算系统。

## 最小对照与处理

1. **把门固定到完全打开。** 两房间之间先只留一个门洞，室内放一个不透明醒目标记。确认标记落在同一相机视锥，固定相机、层和门扇。记录该标记是否出现在相机的绘制或可见集合，再关相机遮挡对照。如果一直有绘制却看不见，查深度遮挡或材质；如果两次都没绘制，不优先重烘遮挡。
2. **门扇不动，只切门洞。** 相机遮挡开启时，把 `OcclusionPortal.open` 从 false 切 true，记录门内标记是否恢复。若只在 true 时正常，核对开门流程、初始状态和是否操作了正确的 Portal。若两档都缺而关相机遮挡能恢复，查旧烘焙是否把闭合门扇算作静态遮挡、门洞是否被其他静态体挡住，以及有效开口和 `Smallest Hole`。只改一项再烘，避免把整组参数变化误当成根因。
3. **恢复动画并查代价。** 门洞体积固定在门框内，门动画单独驱动其状态。动作开始时可先放行，在门扇完全封住视线后再关；中间半开的状态先保守避免可见物突然消失。两房间还有窗或另一道门时，关闭单个 Portal 并不代表房间必定整体消失。按玩家真实机位对照门前、门缝和跨门时的漏剔除或错剔除，再记录可见绘制数、CPU 剔除时间和烘焙数据大小。

**验证状态。** 已核对 Unity 门例、开关语义、烘焙参数和 UE 预计算限制。未在本地搭两房间对照、重烘或做真机计时。
