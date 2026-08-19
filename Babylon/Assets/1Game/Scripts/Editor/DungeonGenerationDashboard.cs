using System.Collections.Generic;
using System;
using Edgar.Unity;
using UnityEditor;
using UnityEngine;
using XianTu.LevelDesign;

namespace XianTu.Editor
{
    public sealed class DungeonGenerationDashboard : EditorWindow
    {
        private const string ProfilePath =
            "Assets/1Game/Resources/LevelDesign/EdgarGrid3D/地牢生成总控.asset";
        private const string DefaultGraphPath =
            "Assets/1Game/Resources/LevelDesign/EdgarGrid3D/WhiteboxLevelGraph.asset";

        private DungeonGenerationProfile _profile;
        private SerializedObject _serialized;
        private Vector2 _scroll;
        private readonly List<DungeonValidationMessage> _messages = new();
        private GameObject _previewHost;
        private EdgarDungeonRuntime _previewRuntime;
        private int _previewSeed;
        private string _previewSummary;
        private bool _previewBridgeOpen;
        private int _previewLiftMode;

        [MenuItem("仙途秘境/关卡工具/布局、注入与节奏校验", false, 301)]
        public static void ShowWindow()
        {
            var window = GetWindow<DungeonGenerationDashboard>("地牢生成总控");
            window.minSize = new Vector2(620f, 600f);
            window.Show();
        }

        private void OnEnable()
        {
            LoadProfile();
        }

        private void OnDisable()
        {
            ClearPreview();
        }

        private void OnGUI()
        {
            if (_profile == null)
            {
                EditorGUILayout.HelpBox(
                    "尚未创建地牢生成总控。创建后会登记当前 WhiteboxLevelGraph，" +
                    "并填入节奏校验与升降井捷径的默认值。",
                    MessageType.Info);
                if (GUILayout.Button("创建默认总控配置", GUILayout.Height(34f)))
                    CreateDefaultProfile();
                return;
            }

            _serialized.Update();
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            EditorGUILayout.HelpBox(
                "运行时顺序：按权重抽布局 → 伸缩连接段 → 抽取特殊建筑 → 注入特殊叶子房 → Edgar 生成 → " +
                "房间标签筛内容 → 按事件条件启用受控捷径。所有随机都由当局随机种子复现。",
                MessageType.Info);

            DrawProperty(
                "Layouts",
                "候选布局",
                "登记多个 Edgar 关卡图后按权重随机；节点名应保持玩法语义稳定。");
            DrawRhythmSettings();
            DrawProperty(
                "RoomInjections",
                "特殊房间注入",
                "把配置的房间模板作为叶子分支接到标签兼容的锚点；空规则不会改变现有关卡。");
            DrawProperty(
                "EdgeExpansions",
                "连接段伸缩",
                "把指定 A→B 边替换为 A→连接房→B；建议 MVP 每条边最多插入 1 房。");
            DrawProperty(
                "BuildingPools",
                "特殊建筑池",
                "从候选建筑中按权重无放回抽取，再固定到建筑槽节点；同一建筑当局不会重复。");
            DrawProperty(
                "Shortcuts",
                "受控捷径",
                "只连接明确节点；可配置昼夜、事件条件、单向/双向与使用提示。");

            EditorGUILayout.Space(12f);
            DrawPreviewControls();
            EditorGUILayout.Space(12f);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("校验全部启用布局", GUILayout.Height(30f)))
                ValidateLayouts();
            if (GUILayout.Button("保存配置", GUILayout.Height(30f)))
                SaveProfile();
            if (GUILayout.Button("重置默认阈值", GUILayout.Height(30f)))
            {
                Undo.RecordObject(_profile, "重置地牢节奏默认值");
                _profile.Validation = new DungeonRhythmValidationSettings();
                EditorUtility.SetDirty(_profile);
                _serialized = new SerializedObject(_profile);
            }
            EditorGUILayout.EndHorizontal();

            DrawValidationMessages();
            EditorGUILayout.EndScrollView();

            if (_serialized.ApplyModifiedProperties())
                EditorUtility.SetDirty(_profile);
        }

        private void DrawPreviewControls()
        {
            EditorGUILayout.LabelField("场景随机预览", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "随机抽取启用的布局流程、连接段和建筑池，在当前场景中生成临时预览。" +
                "预览对象不会保存进场景；关闭窗口或点击清除会销毁。",
                MessageType.None);
            using (new EditorGUI.DisabledScope(
                       EditorApplication.isPlayingOrWillChangePlaymode))
            {
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("随机预览一次", GUILayout.Height(32f)))
                    GeneratePreview(false);
                using (new EditorGUI.DisabledScope(_previewSeed <= 0))
                    if (GUILayout.Button("用相同随机种子重生成", GUILayout.Height(32f)))
                        GeneratePreview(true);
                using (new EditorGUI.DisabledScope(_previewHost == null))
                    if (GUILayout.Button("清除预览", GUILayout.Height(32f)))
                        ClearPreview();
                EditorGUILayout.EndHorizontal();
            }
            if (!string.IsNullOrWhiteSpace(_previewSummary))
                EditorGUILayout.HelpBox(_previewSummary, MessageType.Info);

            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("地图事件预览开关", EditorStyles.boldLabel);
            using (new EditorGUI.DisabledScope(_previewRuntime == null))
            {
                EditorGUI.BeginChangeCheck();
                bool bridgeOpen = EditorGUILayout.ToggleLeft(
                    "开放巡礼桥（可进入桥后封藏室）",
                    _previewBridgeOpen);
                int liftMode = EditorGUILayout.Popup(
                    "狱城升降井状态",
                    _previewLiftMode,
                    new[] { "保持封锁", "单向返回中庭", "双向运行" });
                if (EditorGUI.EndChangeCheck())
                {
                    _previewBridgeOpen = bridgeOpen;
                    _previewLiftMode = liftMode;
                    ApplyPreviewEventState();
                }
            }
            EditorGUILayout.HelpBox(
                "这些开关只修改当前场景预览，不写存档、不完成正式事件。" +
                "巡礼桥会同步切换实体阻挡与导航；升降井会生成对应方向的临时传送台。",
                MessageType.None);
        }

        private void GeneratePreview(bool reuseSeed)
        {
            _serialized.ApplyModifiedProperties();
            SaveProfile();
            int seed = reuseSeed && _previewSeed > 0
                ? _previewSeed
                : Guid.NewGuid().GetHashCode() & int.MaxValue;
            if (seed == 0)
                seed = 1;
            DungeonLayoutCandidate layout =
                DungeonGenerationProfile.Instance.SelectLayout(seed);
            if (layout?.LevelGraph == null)
            {
                _previewSummary = "没有启用且已绑定 LevelGraph 的候选布局。";
                return;
            }

            DungeonGraphValidationReport validation =
                DungeonGraphRhythmValidator.Validate(
                    layout.LevelGraph,
                    layout.StartNodeName,
                    layout.BossNodeName,
                    _profile.Validation,
                    GetShortcutsForLayout(layout.ID));
            if (!validation.IsValid)
            {
                _previewSummary =
                    $"布局 {layout.ID} 未通过节奏校验，请先查看下方校验结果。";
                ValidateLayouts();
                return;
            }

            ClearPreview(false);
            _previewSeed = seed;
            _previewHost = new GameObject("__地牢Flow预览")
            {
                hideFlags = HideFlags.DontSave,
            };
            try
            {
                _previewRuntime = _previewHost.AddComponent<EdgarDungeonRuntime>();
                bool generated = _previewRuntime.Generate(
                    seed,
                    layout.ResolveStartNode(false),
                    32,
                    500);
                if (!generated || _previewRuntime.GeneratedRoot == null)
                    throw new InvalidOperationException("Edgar 未返回有效预览地牢。");
                _previewRuntime.GeneratedRoot.hideFlags = HideFlags.DontSave;
                ApplyPreviewEventState();
                Selection.activeGameObject = _previewRuntime.GeneratedRoot;
                SceneView.lastActiveSceneView?.FrameSelected();
                Repaint();
            }
            catch (Exception ex)
            {
                ClearPreview(false);
                _previewSeed = seed;
                _previewSummary =
                    $"预览失败：随机种子={seed} · {ex.GetBaseException().Message}";
                Debug.LogException(ex);
            }
        }

        private static int CountAssignedBuildings(EdgarDungeonRuntime runtime)
        {
            int count = 0;
            foreach (EdgarRoomPlacement room in runtime.Rooms)
                if (runtime.TryGetBuildingAssignment(room.NodeName, out _))
                    count++;
            return count;
        }

        private void ApplyPreviewEventState()
        {
            if (_previewRuntime == null || !_previewRuntime.IsReady)
                return;

            DungeonLayoutCandidate layout = _previewRuntime.CurrentLayout;
            string bridgeSource =
                layout?.OptionalBranchSourceNodeName ?? "O1";
            string bridgeTarget =
                layout?.OptionalBranchTargetNodeName ?? "B0";
            _previewRuntime.SetOptionalBranchAccess(
                bridgeSource,
                bridgeTarget,
                _previewBridgeOpen);

            RemovePreviewLiftPortals();
            if (_previewLiftMode > 0
                && TryGetPreviewRoomRoot("O1", out Transform sourceRoom)
                && TryGetPreviewRoomRoot("C0", out Transform targetRoom))
            {
                Vector3 sourcePoint = ResolvePreviewPoint(
                    sourceRoom,
                    DungeonContentSocketType.Event);
                Vector3 targetPoint = ResolvePreviewPoint(
                    targetRoom,
                    DungeonContentSocketType.PlayerSpawn);
                DungeonShortcutPortal.Create(
                    sourcePoint,
                    targetPoint,
                    _previewLiftMode == 1 ? "单向返回王城中庭" : "前往王城中庭",
                    sourceRoom,
                    "__PreviewLiftForward");
                if (_previewLiftMode == 2)
                {
                    DungeonShortcutPortal.Create(
                        targetPoint,
                        sourcePoint,
                        "前往狱城升降井",
                        targetRoom,
                        "__PreviewLiftReverse");
                }
            }

            _previewRuntime.RebuildNavigation();
            _previewSummary =
                $"随机种子={_previewSeed} · 布局={_previewRuntime.CurrentLayoutID} · " +
                $"{_previewRuntime.RoomCount} 房 · " +
                $"{CountAssignedBuildings(_previewRuntime)} 座随机建筑 · " +
                $"巡礼桥={(_previewBridgeOpen ? "开放" : "封锁")} · " +
                $"升降井={LiftModeName(_previewLiftMode)}";
            SceneView.RepaintAll();
            Repaint();
        }

        private void RemovePreviewLiftPortals()
        {
            if (_previewRuntime == null)
                return;
            foreach (EdgarRoomPlacement placement in _previewRuntime.Rooms)
            {
                Transform root =
                    placement.Instance?.RoomTemplateInstance?.transform;
                if (root == null)
                    continue;
                foreach (DungeonShortcutPortal portal in
                         root.GetComponentsInChildren<DungeonShortcutPortal>(true))
                {
                    if (portal.RuleKey == null
                        || !portal.RuleKey.StartsWith(
                            "__PreviewLift",
                            StringComparison.Ordinal))
                        continue;
                    DestroyImmediate(portal.gameObject);
                }
            }
        }

        private bool TryGetPreviewRoomRoot(
            string nodeName,
            out Transform roomRoot)
        {
            foreach (EdgarRoomPlacement placement in _previewRuntime.Rooms)
            {
                if (placement.NodeName != nodeName)
                    continue;
                roomRoot =
                    placement.Instance?.RoomTemplateInstance?.transform;
                return roomRoot != null;
            }
            roomRoot = null;
            return false;
        }

        private static Vector3 ResolvePreviewPoint(
            Transform roomRoot,
            DungeonContentSocketType socketType)
        {
            Vector3? authoredFallback = null;
            foreach (DungeonContentSocket socket in
                     roomRoot.GetComponentsInChildren<DungeonContentSocket>(true))
            {
                if (socket.SocketType != socketType)
                    continue;
                authoredFallback ??= socket.transform.position + Vector3.up * 0.1f;
                if (DungeonSpawnSafety.TryFindGroundedPoint(
                        roomRoot,
                        socket.transform.position,
                        0.45f,
                        1.8f,
                        0.08f,
                        out Vector3 grounded))
                    return grounded;
            }
            return authoredFallback ?? roomRoot.position + Vector3.up * 0.1f;
        }

        private static string LiftModeName(int mode)
        {
            return mode switch
            {
                1 => "单向",
                2 => "双向",
                _ => "封锁",
            };
        }

        private void ClearPreview(bool clearSeed = true)
        {
            if (_previewRuntime != null)
                _previewRuntime.Clear();
            if (_previewHost != null)
                DestroyImmediate(_previewHost);
            _previewRuntime = null;
            _previewHost = null;
            if (clearSeed)
            {
                _previewSeed = 0;
                _previewSummary = null;
                _previewBridgeOpen = false;
                _previewLiftMode = 0;
            }
        }

        private void DrawProperty(string propertyName, string title, string help)
        {
            EditorGUILayout.Space(10f);
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(help, MessageType.None);
            EditorGUILayout.PropertyField(
                _serialized.FindProperty(propertyName),
                new GUIContent(title),
                true);
        }

        private void DrawRhythmSettings()
        {
            EditorGUILayout.Space(10f);
            EditorGUILayout.LabelField("关卡节奏校验参数", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "路径深度按“房间连接数”计算。例如深度 6 表示从降落房到首领房的最短路线经过 6 条连接。" +
                "这些参数只负责检查和提示，不会自动修改策划布局。",
                MessageType.None);

            SerializedProperty rhythm = _serialized.FindProperty("Validation");
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(
                rhythm.FindPropertyRelative("MinBossDepth"),
                new GUIContent(
                    "首领最小路径深度（连接数）",
                    "首领不能离降落点过近。"));
            EditorGUILayout.PropertyField(
                rhythm.FindPropertyRelative("MaxBossDepth"),
                new GUIContent(
                    "首领最大路径深度（连接数）",
                    "首领不能离降落点过远。"));
            EditorGUILayout.PropertyField(
                rhythm.FindPropertyRelative("MaxConsecutiveCombatRooms"),
                new GUIContent(
                    "主路线连续战斗房上限",
                    "普通战斗、精英和首领房都计为战斗房。"));
            EditorGUILayout.PropertyField(
                rhythm.FindPropertyRelative("MinEventRooms"),
                new GUIContent("事件房数量下限"));
            EditorGUILayout.PropertyField(
                rhythm.FindPropertyRelative("MaxEventRooms"),
                new GUIContent("事件房数量上限"));
            EditorGUILayout.PropertyField(
                rhythm.FindPropertyRelative("MinLandmarkRooms"),
                new GUIContent("地标房数量下限"));
            EditorGUILayout.PropertyField(
                rhythm.FindPropertyRelative("MaxUnrewardedDeadEnds"),
                new GUIContent(
                    "无奖励死路数量上限",
                    "死路末端必须有事件、精英、商店、首领、建筑或宝箱等明确收益。"));
            EditorGUILayout.PropertyField(
                rhythm.FindPropertyRelative("MinShortcutSavedEdges"),
                new GUIContent(
                    "捷径最少节省连接数",
                    "捷径相对正常步行路线至少减少多少条连接。"));
            EditorGUILayout.PropertyField(
                rhythm.FindPropertyRelative("RequireConnectedGraph"),
                new GUIContent("必须保证全部房间连通"));
            EditorGUI.indentLevel--;
        }

        private void DrawValidationMessages()
        {
            if (_messages.Count == 0)
                return;
            EditorGUILayout.Space(12f);
            EditorGUILayout.LabelField("校验结果", EditorStyles.boldLabel);
            foreach (DungeonValidationMessage message in _messages)
            {
                MessageType type = message.Severity switch
                {
                    DungeonValidationSeverity.Error => MessageType.Error,
                    DungeonValidationSeverity.Warning => MessageType.Warning,
                    _ => MessageType.Info,
                };
                EditorGUILayout.HelpBox(message.Text, type);
            }
        }

        private void ValidateLayouts()
        {
            _serialized.ApplyModifiedProperties();
            _messages.Clear();
            bool found = false;
            foreach (DungeonLayoutCandidate layout in _profile.Layouts)
            {
                if (layout == null || !layout.Enabled || layout.LevelGraph == null)
                    continue;
                found = true;
                _messages.Add(new DungeonValidationMessage(
                    DungeonValidationSeverity.Info,
                    $"[{layout.ID}] {layout.DisplayName}"));
                DungeonGraphValidationReport report = DungeonGraphRhythmValidator.Validate(
                    layout.LevelGraph,
                    layout.StartNodeName,
                    layout.BossNodeName,
                    _profile.Validation,
                    GetShortcutsForLayout(layout.ID));
                _messages.AddRange(report.Messages);
                ValidateSemanticBindings(layout);
                if (!string.IsNullOrWhiteSpace(layout.AlternateStartNodeName)
                    && !string.IsNullOrWhiteSpace(layout.AlternateBossNodeName))
                {
                    _messages.Add(new DungeonValidationMessage(
                        DungeonValidationSeverity.Info,
                        $"[{layout.ID}] 反向出生"));
                    DungeonGraphValidationReport alternate =
                        DungeonGraphRhythmValidator.Validate(
                            layout.LevelGraph,
                            layout.AlternateStartNodeName,
                            layout.AlternateBossNodeName,
                            _profile.Validation,
                            GetShortcutsForLayout(layout.ID));
                    _messages.AddRange(alternate.Messages);
                }
            }

            if (!found)
                _messages.Add(new DungeonValidationMessage(
                    DungeonValidationSeverity.Error,
                    "没有启用且已绑定 LevelGraph 的布局。"));
        }

        private IReadOnlyList<DungeonShortcutRule> GetShortcutsForLayout(
            string layoutID)
        {
            var result = new List<DungeonShortcutRule>();
            if (_profile.Shortcuts == null)
                return result;
            foreach (DungeonShortcutRule shortcut in _profile.Shortcuts)
            {
                if (shortcut == null)
                    continue;
                bool restricted = false;
                bool matched = false;
                foreach (string candidate in shortcut.LayoutIDs ?? Array.Empty<string>())
                {
                    if (string.IsNullOrWhiteSpace(candidate))
                        continue;
                    restricted = true;
                    if (string.Equals(
                            candidate.Trim(),
                            layoutID,
                            StringComparison.OrdinalIgnoreCase))
                        matched = true;
                }
                if (!restricted || matched)
                    result.Add(shortcut);
            }
            return result;
        }

        private void ValidateSemanticBindings(DungeonLayoutCandidate layout)
        {
            var names = new HashSet<string>();
            foreach (RoomBase room in layout.LevelGraph.Rooms)
                if (room != null)
                    names.Add(room.GetDisplayName());

            CheckNode(layout.ID, "正向起点", layout.StartNodeName, names);
            CheckNode(layout.ID, "正向 Boss", layout.BossNodeName, names);
            CheckNode(layout.ID, "反向起点", layout.AlternateStartNodeName, names);
            CheckNode(layout.ID, "反向 Boss", layout.AlternateBossNodeName, names);
            CheckNode(layout.ID, "路线事件", layout.LayoutEventNodeName, names);
            CheckNode(layout.ID, "战斗事件", layout.StrengthEventNodeName, names);
            CheckNode(layout.ID, "商店", layout.ShopNodeName, names);
            foreach (string node in layout.EliteNodeNames ?? Array.Empty<string>())
                CheckNode(layout.ID, "精英", node, names);
            foreach (string node in layout.LandmarkNodeNames ?? Array.Empty<string>())
                CheckNode(layout.ID, "地标", node, names);
            CheckNode(
                layout.ID,
                "可选分支入口",
                layout.OptionalBranchSourceNodeName,
                names,
                true);
            CheckNode(
                layout.ID,
                "可选分支目标",
                layout.OptionalBranchTargetNodeName,
                names,
                true);
        }

        private void CheckNode(
            string layoutID,
            string role,
            string nodeName,
            ISet<string> names,
            bool optional = false)
        {
            if (string.IsNullOrWhiteSpace(nodeName))
            {
                if (!optional)
                    _messages.Add(new DungeonValidationMessage(
                        DungeonValidationSeverity.Error,
                        $"[{layoutID}] 未配置{role}节点。"));
                return;
            }
            if (!names.Contains(nodeName.Trim()))
                _messages.Add(new DungeonValidationMessage(
                    DungeonValidationSeverity.Error,
                    $"[{layoutID}] {role}节点不存在：{nodeName}。"));
        }

        private void LoadProfile()
        {
            _profile = AssetDatabase.LoadAssetAtPath<DungeonGenerationProfile>(ProfilePath);
            _serialized = _profile != null ? new SerializedObject(_profile) : null;
        }

        private void CreateDefaultProfile()
        {
            var graph = AssetDatabase.LoadAssetAtPath<LevelGraph>(DefaultGraphPath);
            if (graph == null)
            {
                EditorUtility.DisplayDialog(
                    "缺少默认关卡图",
                    $"找不到 {DefaultGraphPath}",
                    "确定");
                return;
            }

            var profile = CreateInstance<DungeonGenerationProfile>();
            profile.ResetToDefaults();
            profile.Layouts.Add(new DungeonLayoutCandidate
            {
                ID = "Layout_A",
                DisplayName = "双区主轴",
                LevelGraph = graph,
                Weight = 100,
                Enabled = true,
                StartNodeName = "O4",
                BossNodeName = "I4",
                AlternateStartNodeName = "I3",
                AlternateBossNodeName = "O0",
            });
            AssetDatabase.CreateAsset(profile, ProfilePath);
            AssetDatabase.SaveAssets();
            DungeonGenerationProfile.ClearCache();
            LoadProfile();
            Selection.activeObject = _profile;
        }

        private void SaveProfile()
        {
            _serialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(_profile);
            AssetDatabase.SaveAssets();
            DungeonGenerationProfile.ClearCache();
        }
    }
}
