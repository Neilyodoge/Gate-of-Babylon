# TA-136 · 动态分辨率在两档间来回跳

**制作决定。** 如果固定比例分别稳定，自动模式却在两档间反复跳，先把每条 GPU 计时关联到产生它的帧与比例，再查变档策略。较短的等待窗口能抑制噪声，但如果高档总是超预算、低档只要达标就立即升档，延长等待只会放慢循环。降档可对持续超预算快速响应；升档需要确认当前比例的样本，并给较高档留足预算。

## 公开证据

- [Epic UE 5.5 · Dynamic Resolution](https://dev.epicgames.com/documentation/en-us/unreal-engine/dynamic-resolution-in-unreal-engine?application_version=5.5) 区分 `FrameTimeBudget`、`TargetedGPUHeadRoom`、`ChangePercentageThreshold` 与 `MinResolutionChangePeriod`。最小变动百分比过滤很小的尺寸更改；等待帧数让启发式获得当前比例的样本，也减轻频繁变化对时域采样的干扰。`Over Budget Panic` 依据已取到的连续超预算 GPU 帧触发较快的降档并重置历史，避免之前较轻的负载压低估计。Epic 另说明从检测到生效之间可能有延迟；这里只核对了文档正文，没有在 UE 重放示例。
- [Unity 6000.0 · FrameTimingManager data availability](https://docs.unity3d.com/6000.0/Documentation/Manual/frame-timing-manager.html) 写明此 API 的结果有四帧延迟，而且部分平台的 GPU 结果可能不可用或不准；[FrameTiming.heightScale](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/FrameTiming-heightScale.html) 是与该条计时对应的比例。不同 API 的延迟不能直接照抄。公开 [DynamicResolutionHandler](https://github.com/Unity-Technologies/Graphics/blob/6000.0/staging/Packages/com.unity.render-pipelines.core/Runtime/Common/DynamicResolutionHandler.cs#L437-L461) 读取活动 scaler，按其策略插值或夹到比例范围内；这一段不会替用户回调选择等待窗口与升档预算。排查时区分请求比例、实际生效比例和带回报的历史比例。

## 同一负载的控制算例

CPU 模型设 `GPU ms = 7 + 10 × scale²`，预算 16.67 ms，档位 H=100%、L=90%，固定场景下分别为 17.0 和 15.1 ms。帧 `i` 结束后可用帧 `i−d` 的报告决定下一帧档位；`hold=4` 表示档位至少维持 4 帧。默认策略超预算降、低于预算升；最后一组要求样本来自这次变档之后，且只有低于 14.8 ms 才升。输入里没有噪声、CPU 限制或时域重建。

| 策略 | 16 帧档位 | 变档次数 | 超预算帧 |
| --- | --- | ---: | ---: |
| 即时报告，每帧判断 | `HLHLHLHLHLHLHLHL` | 15 | 8 |
| 报告晚两帧，每帧判断 | `HHHLLLHHHLLLHHHL` | 5 | 9 |
| 晚两帧，最短驻留四帧 | `HHHHLLLLHHHHLLLL` | 3 | 8 |
| 晚两帧，驻留四帧，并用新样本与升档余量 | `HHHHLLLLLLLLLLLL` | 1 | 4 |

这组模型中驻留四帧已足以等到当前档位首个样本，最后一行停止反复是因为升档门槛的余量。14.8 ms 只是算例；实际应先测 H/L 相差多少、停在哪档可接受，再定余量或估算下一档的成本。固定成本或 CPU 是主瓶颈时，再降分辨率也可能过不了预算。

## 最小验收

1. 用同一段可重放镜头，固定高低两个比例各走一遍，区分 GPU 过预算与 CPU、VSync、本来已逼近下限的情况。自动模式逐帧记预算、请求／实际比例、GPU 计时原始帧和报告到达帧、换档理由；对照画面，确认抖动确实随档位变化。固定档也会模糊时，另查 [TA-33 的合成与时域路径](dynamic-resolution-ui-and-translucency.md)。
2. 在同段重放单独比较最小驻留、升档余量和更快的持续超预算降档。用稳定段统计每秒切档及超预算帧，用镜头跳切或重特效测降档反应、超预算峰值和恢复到高档所需帧数。样本有效性、实际比例粒度和画面恢复时间都要在目标设备验证。

**验证状态。** 已核对 Epic 与 Unity 公开资料及 Unity 源码，并执行四组 16 帧 CPU 算例；未在 UE 或 Unity 抓帧，未做真机或时域画质验证。
