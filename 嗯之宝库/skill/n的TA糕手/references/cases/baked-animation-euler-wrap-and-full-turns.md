# TA-117 · 动画烘焙后跨 180° 错转一圈

**制作决定。** 小幅旋转越过 180° 时，先看导出曲线的中间姿态，确认它是否真的绕了长弧；有意转一圈的动画则另验中间键是否保留。欧拉角的等价表达可以改，动作想走的路径不能只靠首尾方向恢复。

## 公开证据

- [Blender 4.1 FBX 导出器](https://github.com/blender/blender-addons/blob/blender-v4.1-release/io_scene_fbx/export_fbx_bin.py) 的动画烘焙循环逐样本设置帧与子帧，调用 `fbx_object_tx(..., rot_euler_compat=p_rot)` 后把这次欧拉角留给下一个样本。[fbx_utils.py](https://github.com/blender/blender-addons/blob/blender-v4.1-release/io_scene_fbx/fbx_utils.py) 先拆变换，再用 `rot.to_euler('XYZ', rot_euler_compat)` 选择和上一样本兼容的欧拉表达。[Blender API](https://docs.blender.org/api/4.1/mathutils.html#mathutils.Quaternion.to_euler) 明确把该参数用于连续动画曲线转换。这个做法作用于已采到的姿态序列；如果没采到中途的旋转，它不会凭端点补回整圈。
- [Unity 6000.0 · Rotation in animations](https://docs.unity3d.com/6000.0/Documentation/Manual/AnimationRotate.html) 给出 0°→270° 的例子：若只保留首尾姿态，最短弧会向反方向转 90°；两键相隔六帧时，每帧重采可把正向动作拆成相邻 45° 的键。[Euler curve resampling](https://docs.unity.com/en-us/engine/6000.0/manual/animation-section/animation-mecanim/animation-clips/animation-euler-curve-import) 说明 Unity 默认每帧重采为四元数；关闭 Resample Curves 才尝试保留源欧拉曲线，但含 PreRotation／PostRotation 的 FBX 关节会被 FBX SDK 自动重采。关勾后仍需核对导入产物实际是哪种曲线。

## 最小对照

1. **小幅跨界。** 用单轴转动和不对称指针，输入意图为 179°→181°。如果把两端写成 179°→−179°，并直接线性插值这条欧拉分量，差值是 −358°，中点到 0°；按连贯表达写成 179°→181°，差值 +2°，中点到 180°。不同数值可能表示同一个端点方向，单看两键跨界并不能判断一定翻错；应在 DCC、导出文件评估结果、引擎预览的同一中间时刻比较指针方向。
2. **再验有意转圈。** 给同一物体一个 0°→360° 的动作。首尾姿态相同，只用两键做四元数最短弧会转 0°；单轴保留 0°、120°、240°、360° 四个足够密的样本时，相邻最短弧都是正向 120°。四元数选同号一侧可修正表示跳号，不能从相同端点恢复漏掉的圈数。比较未简化和导入后曲线；若删键或采样过疏让相邻键跨越 180°，优先保留旋转过渡键或提高采样，再复验转速及总弧长。
3. **最后查多轴。** 单轴已对才换三轴样件，记录旋转顺序、局部参考姿态、父级和 FBX Pre/PostRotation。若源 FBX 中点已错，在 DCC 烘焙或欧拉转换阶段修；若源文件读回正确、引擎才偏，分别对照重采样与曲线简化，别通过换全局轴顺序掩盖问题。原始欧拉曲线能否保留及其开销，以当前导入器和目标动画为准。

**验证状态。** 已核对 Blender 4.1 导出源码与 Unity 6000.0 文档，并复算角度、中点和整圈样本。未导出 FBX 对照件，未在引擎或真机验证。
