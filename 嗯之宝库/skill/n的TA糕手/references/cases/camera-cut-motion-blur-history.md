# TA-142 · 镜头跳切后出现一帧异常运动模糊

**制作决定。** 同一台相机硬切或瞬移后，静止物体只有首帧被拖成大片时，先按切镜事件重置该相机的运动历史，再分别验静物和独立运动物。保留普通平滑运镜的历史；如果重置只消不掉残影，再查运动矢量纹理和实际后处理路径。

## 公开证据

- **Unity URP 6000.0 相机历史。** [UniversalAdditionalCameraData.resetHistory](https://github.com/Unity-Technologies/Graphics/blob/6000.0/staging/Packages/com.unity.render-pipelines.universal/Runtime/UniversalAdditionalCameraData.cs#L708-L724) 的设置器会重置相机运动历史。[MotionVectors.cs](https://github.com/Unity-Technologies/Graphics/blob/6000.0/staging/Packages/com.unity.render-pipelines.universal/Runtime/MotionVectors.cs#L204-L252) 在历史无效时以当前矩阵初始化历史。对复用同一相机的硬切，可在新镜位就绪且渲染前设置 `resetHistory = true` 做对照。切换相机实例、相机堆叠或自定义时域效果时，先核对实际历史的归属。
- **先看模糊从哪取速度。** [CameraMotionVectors.shader](https://github.com/Unity-Technologies/Graphics/blob/6000.0/staging/Packages/com.unity.render-pipelines.universal/Shaders/CameraMotionVectors.shader#L79-L122) 比较同一世界点的当前与上一帧投影；[CameraMotionBlur.shader](https://github.com/Unity-Technologies/Graphics/blob/6000.0/staging/Packages/com.unity.render-pipelines.universal/Shaders/PostProcessing/CameraMotionBlur.shader#L62-L116) 有取运动矢量纹理与直接计算相机速度的分支。验独立物体时，必须确认配置走包含物体运动矢量的路径。相机速度清零不能证明独立物体速度必然保留。
- **UE 5.5 的边界。** [SetGameCameraCutThisFrame](https://dev.epicgames.com/documentation/en-us/unreal-engine/API/Runtime/Engine/Camera/APlayerCameraManager/SetGameCameraCutThisFrame?application_version=5.5) 是发出本帧切镜标记的入口；每个效果是否读取该标记及如何处理首帧，仍须分别确认。

## 最小算例和验收

固定正交相机的画面宽 10 m、输出宽 1920 px，镜头向右瞬移 2 m。以右为正，按“当前投影减上一帧投影”定义算例位移，不当成 GPU 缓冲的编码值或最终模糊半径。

| 比较 | 算例 |
| --- | ---: |
| 保留旧镜位，静物 `u=0.5 → 0.3` | `(0.3−0.5) × 1920 = −384 px` |
| 历史以当前镜位初始化，静物两次投影重合 | `0 px` 的相机贡献 |
| 在相同镜位下，独立物体向右移动 0.1 m | `0.1 / 10 × 1920 = 19.2 px`，几何上的物体位移 |

1. 把静物和独立移动物并排放置，用同一输出、曝光、强度和质量档比较平滑移动、硬切不重置、硬切前重置三组。固定移动物每帧位移，记录切前、切镜帧、切后一帧；逐组并排看运动矢量缓冲和最终模糊。
2. 重置组应重点核对静物的切镜尖峰是否消失、下一帧是否恢复正常运动。只有包含物体运动的模式才用移动物验独立速度是否保留；若速度缓冲正确而画面仍有残影，关运动模糊再查其他使用历史的效果。

**验证状态。** 已核对公开源码和文档，复算 CPU 几何位移。未做引擎抓帧或真机对照；独立对象在切镜帧能否保留速度，以及最终模糊幅度，均未实测。
