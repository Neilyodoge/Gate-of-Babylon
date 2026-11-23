# Unity 贴图导入预检：默认、强制规则与手工例外

**要解决的事**：让新贴图得到合理默认值，同时保证老资产和美术手调不在重导入时被悄悄覆盖。做决定前区分“缺少设置时填一次”“明确纳管的字段每次纠正”和“仅检查”。三类规则不应使用同一触发条件。

## 证据：同一类回调，两种产品意图

核对日期：2026-11-23。

- [Unity Discussions · Shadoninja 的设置覆盖案例，首帖，2018-05-28](https://discussions.unity.com/t/my-custom-assetpostprocessor-onpreprocesstexture-logic-is-being-run-every-time-my-project-is-saved/703071)：作者公开的 `OnPreprocessTexture` 对匹配的 PNG 无条件重写 PPU、Point 滤波和无压缩。作者报告在 Inspector 改值并点 Apply 后，回调把设置改回默认。证据是源码与作者报告，不据此断言“保存任何场景都会重导入”或“某个修复在所有版本都有效”。
- [Unity 6000.0 · AssetImporter.importSettingsMissing](https://docs.unity.com/en-us/engine/6000.0/script-reference/unityeditor/assetimporter/importsettingsmissing)：属性为真指的是**没有随资源提供 `.meta`**。因此它适合作为“无元数据就填默认”的门槛，不是项目历史里“从未导入”的标志。新同事第一次打开带 `.meta` 的资产与漏带 `.meta` 的旧资产，是两种相反边界。
- [Unity 6000.0 · Default presets per folder，Update assets already imported](https://docs.unity.com/en-us/engine/6000.0/manual/assets-and-media/presets/default-presets-by-folder#update-assets-already-imported)：官方扩展示例的 `OnPreprocessAsset` 没有“无 `.meta`”门槛，按父目录到近目录调用 `Preset.ApplyTo`；近目录最后应用，示例没有逐资产手工豁免。它明确支持更新**已导入**资源，因此只应借给长期受规则控制的范围，不能当“填一次默认”的方案。
- **预设刷新细节**：该例对已适用/尚未加载的 preset 登记 `DependsOnArtifact`，用于追值变化；另对目录登记 custom dependency，目录 hash 加的是排序后的 preset 路径和目标类型，用于发现增删、移动或类型变化。变化处理过滤 `.preset`、按目录去重，`delayCall` 后更新依赖并刷新。它没有把每张贴图都拿去单独调用重导入。另见 [Unity 6000.0 · Restarting the refresh process](https://docs.unity.com/en-us/engine/6000.0/manual/assets-and-media/asset-database/refreshing#restarting-the-refresh-process)：在 `OnPostprocessAllAssets` 调用 `SaveAndReimport`、`ImportAsset` 等会重启刷新；不能把这些 API 无条件挂进导入后的“纠错”。

## 该怎么落地

| 场景 | 建议 | 保留的例外 |
| --- | --- | --- |
| 新资源有明确语义，只想给默认值 | 只在确认纳管的目录/语义且 `importSettingsMissing` 时，通过当前导入器设置必要字段。提交资源时保留其 `.meta`。 | 已提供 `.meta` 的外部资产和旧资源保持原值；老资产缺 `.meta` 先核对来源。 |
| 某资源组必须遵守规则 | 用明确的目录或可审查的标记圈定资产和受控字段。若设计上允许豁免，在预设/规则写入之前读取随仓库共享的例外，并限定到具体字段；记录理由。 | 不用只存在某台电脑的 EditorPrefs 作为团队的例外真值。同一资产的其他字段可继续由美术维护。 |
| 仅凭命名无法判断贴图语义，或老资产要迁移 | 先只读报警，给资产路径、当前值、期望值和触发规则。迁移另开显式批处理，先看预计修改清单，只保存确有差异且已批准的项。 | 例如后缀像数据图但 RGB 还承担颜色用途；源图有 A 也不证明 A 是透明度。检查项不足以自动切 sRGB 或扩色。 |

**防重复触发**：导入前的规则对相同输入应得到相同设置，不在回调中再请求当前资源重导入。校验阶段只读，不写资源或 `.meta` 让刷新重新触发自己。需要根据策略变更重导入时，限定到策略依赖；若仿照官方按目录预设，保留“过滤输入、记录依赖、合并变化、延期刷新”四个约束，验证而非保证循环会收敛。

## 最小验收

在目标 Unity 版本建隔离夹具：新贴图无 `.meta`、带已提交 `.meta` 的贴图、带手工例外的受控贴图、未纳管老贴图各一张。比较首次导入、Inspector Apply、源图更新、规则增删四次的 importer 值与 `.meta` 差异，记录受影响路径和导入次数。相同输入第二次应无额外配置差异；策略改动不应重导入无关目录；删 preset 时检查已有值是否回到预期，不能假设会恢复旧值。暂定“证据不足的项只报警”作为导入验证基线。

**状态**：已读公开代码与官方文档；诊断方法和夹具是实现候选，未在项目或 Unity 中运行、未测导入耗时。
