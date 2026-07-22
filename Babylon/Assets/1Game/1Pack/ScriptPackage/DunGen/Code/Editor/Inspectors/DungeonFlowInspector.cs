using DunGen.Editor.Validation;
using DunGen.Graph;
using System;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace DunGen.Editor
{
	[CustomEditor(typeof(DungeonFlow))]
	public sealed class DungeonFlowInspector : UnityEditor.Editor
	{
		#region Helpers

		private sealed class Properties
		{
			public SerializedProperty Length;
			public SerializedProperty BranchMode;
			public SerializedProperty BranchCount;
			public SerializedProperty KeyManager;
			public SerializedProperty DoorwayConnectionChance;
			public SerializedProperty TileInjectionRules;
			public SerializedProperty RestrictConnectionToSameSection;
			public SerializedProperty TileTagConnectionMode;
			public SerializedProperty TileConnectionTags;
			public SerializedProperty DoorwayTagConnectionMode;
			public SerializedProperty OverrideDoorwaySockets;
			public SerializedProperty DoorwayConnectionTags;
			public SerializedProperty BranchTagPruneMode;
			public SerializedProperty BranchPruneTags;
			public SerializedProperty StraighteningSettings;
			public SerializedProperty CustomPipeline;

			public ReorderableList GlobalProps;
			public ReorderableList TileConnectionTagsList;
			public ReorderableList DoorwayConnectionTagsList;
			public ReorderableList BranchPruneTagsList;
			public ReorderableList TileInjectionRulesList;

		}

		private static class Labels
		{
			public static readonly GUIContent Validate = new GUIContent("校验地牢", "对地牢完整性运行一系列自动检查，报告发现的任何错误");
			public static readonly GUIContent Length = new GUIContent("长度", "主路径的最小/最大长度，决定地牢有多长");
			public static readonly GUIContent BranchMode = new GUIContent("分支模式", "决定分支数量如何计算");
			public static readonly GUIContent BranchCount = new GUIContent("分支数量", "整个地牢中出现的分支总数。仅当分支模式为 Global（全局）时生效");
			public static readonly GUIContent GlobalProps = new GUIContent("全局道具");
			public static readonly GUIContent KeyManager = new GUIContent("钥匙管理器", "定义哪些钥匙可放置在地牢中。若不使用锁与钥匙系统，可留空");
			public static readonly GUIContent DoorwayConnectionHeader = new GUIContent("门口连接");
			public static readonly GUIContent DoorwayConnectionChance = new GUIContent("连接概率", "未连接但重叠的门口被连接的百分比概率。可在每个 Tile 上单独覆盖");
			public static readonly GUIContent RestrictConnectionToSameSection = new GUIContent("限制在同一区段", "勾选后，只有位于流程图同一线段上的门口才会连接");
			public static readonly GUIContent TileInjection = new GUIContent("特殊 Tile 注入", "按一组规则将特定 Tile 注入到地牢布局中");
			public static readonly GUIContent OpenFlowEditor = new GUIContent("打开流程编辑器", "节点图让你设计地牢应如何布局");
			public static readonly GUIContent GlobalPropGroupID = new GUIContent("分组 ID", "道具 ID。应与放置在 Tile 内的 GlobalProp 组件上的 ID 匹配");
			public static readonly GUIContent GlobalPropGroupCount = new GUIContent("数量", "该道具在整个地牢中应出现的次数");
			public static readonly GUIContent TileConnectionTagMode = new GUIContent("模式", "如何应用下方的标签规则。注意：若标签对列表为空，本节被忽略。\n    Accept（接受）：只有 Tile 的标签匹配下方某个标签对时才连接。\n    Reject（拒绝）：Tile 总是连接，除非其标签匹配下方某个标签对。");
			public static readonly GUIContent TileConnectionTags = new GUIContent("标签对");
			public static readonly GUIContent DoorwayConnectionTagMode = new GUIContent("模式", "如何应用下方的标签规则。注意：若标签对列表为空，本节被忽略。\n    Accept（接受）：只有门口的标签匹配下方某个标签对时才连接。\n    Reject（拒绝）：门口总是连接，除非其标签匹配下方某个标签对。");
			public static readonly GUIContent OverrideDoorwaySockets = new GUIContent("覆盖 Socket", "若为真，这些规则将忽略相连门口的 socket，即使 socket 不匹配也允许门口连接");
			public static readonly GUIContent DoorwayConnectionTags = new GUIContent("门口对");
			public static readonly GUIContent ConnectionRules = new GUIContent("连接规则", "基于 Tile 或门口上的标签对，接受或拒绝两个 Tile 之间的连接。规则处理顺序：\n    1. 代码中的自定义规则\n    2. 若自定义规则未处理该连接，则处理 Tile 规则\n    3. 若 Tile 规则接受连接，则处理门口规则");
			public static readonly GUIContent TileConnectionRules = new GUIContent("Tile", "检测 Tile 上的标签");
			public static readonly GUIContent DoorwayConnectionRules = new GUIContent("门口", "检测门口上的标签");
			public static readonly GUIContent BranchPruneMode = new GUIContent("分支修剪模式", "依据下方标签修剪分支末端 Tile 的方式");
			public static readonly GUIContent BranchPruneTags = new GUIContent("分支修剪标签", "分支末端的 Tile 将依据其标签被删除，取决于分支修剪模式");
			public static readonly GUIContent PathStraighteningHeader = new GUIContent("路径拉直", "决定是否以及如何拉直路径。这些设置可在 Archetype 资产及流程图节点上覆盖");
			public static readonly GUIContent BranchingHeader = new GUIContent("分支");
			public static readonly GUIContent CustomPipeline = new GUIContent("自定义管线", "可选的自定义管线资产，用于进一步定制生成流程。留空则使用默认管线。");

			public static readonly GUIContent[] ConnectionRulesTabs = { TileConnectionRules, DoorwayConnectionRules };

			public static readonly string LocalBranchMode = "Local（局部）模式下，分支数量按每个 Tile 使用 Archetype 的 'Branch Count' 属性计算";
			public static readonly string GlobalBranchMode = "Global（全局）模式下，分支数量按整个地牢计算。注意：分支数可能少于指定最小值，但绝不会超过最大值";
			public static readonly string SectionBranchMode = "Section（区段）模式下，分支数量按每个区段使用该区段 Archetype 设置中的 'Branch Count' 属性计算";
		}

		#endregion

		private Properties properties;
		private int selectedConnectionRulesTab;


		private void OnEnable()
		{
			properties = new Properties()
			{
				Length = serializedObject.FindProperty(nameof(DungeonFlow.Length)),
				BranchMode = serializedObject.FindProperty(nameof(DungeonFlow.BranchMode)),
				BranchCount = serializedObject.FindProperty(nameof(DungeonFlow.BranchCount)),
				KeyManager = serializedObject.FindProperty(nameof(DungeonFlow.KeyManager)),
				DoorwayConnectionChance = serializedObject.FindProperty(nameof(DungeonFlow.DoorwayConnectionChance)),
				RestrictConnectionToSameSection = serializedObject.FindProperty(nameof(DungeonFlow.RestrictConnectionToSameSection)),
				TileInjectionRules = serializedObject.FindProperty(nameof(DungeonFlow.TileInjectionRules)),
				TileTagConnectionMode = serializedObject.FindProperty(nameof(DungeonFlow.TileTagConnectionMode)),
				TileConnectionTags = serializedObject.FindProperty(nameof(DungeonFlow.TileConnectionTags)),
				DoorwayTagConnectionMode = serializedObject.FindProperty(nameof(DungeonFlow.DoorwayTagConnectionMode)),
				OverrideDoorwaySockets = serializedObject.FindProperty(nameof(DungeonFlow.OverrideDoorwaySockets)),
				DoorwayConnectionTags = serializedObject.FindProperty(nameof(DungeonFlow.DoorwayConnectionTags)),
				BranchTagPruneMode = serializedObject.FindProperty(nameof(DungeonFlow.BranchTagPruneMode)),
				BranchPruneTags = serializedObject.FindProperty(nameof(DungeonFlow.BranchPruneTags)),
				StraighteningSettings = serializedObject.FindProperty(nameof(DungeonFlow.GlobalStraighteningSettings)),
				CustomPipeline = serializedObject.FindProperty(nameof(DungeonFlow.CustomPipeline)),

				GlobalProps = new ReorderableList(serializedObject, serializedObject.FindProperty("GlobalProps"), true, false, true, true)
				{
					drawElementCallback = (rect, index, isActive, isFocused) => DrawGlobalProp(rect, index),
					elementHeightCallback = GetGlobalPropHeight,
				},
			};

			properties.TileConnectionTagsList = CreateTagPairList(properties.TileConnectionTags, Labels.TileConnectionTags);
			properties.DoorwayConnectionTagsList = CreateTagPairList(properties.DoorwayConnectionTags, Labels.DoorwayConnectionTags);

			properties.BranchPruneTagsList = new ReorderableList(serializedObject, properties.BranchPruneTags)
			{
				drawHeaderCallback = (Rect rect) =>
				{
					EditorGUI.LabelField(rect, Labels.BranchPruneTags);
				},
				drawElementCallback = (Rect rect, int index, bool isActive, bool isFocused) =>
				{
					EditorGUI.PropertyField(rect, properties.BranchPruneTags.GetArrayElementAtIndex(index), GUIContent.none);
				},
			};

			properties.TileInjectionRulesList = new ReorderableList(serializedObject, properties.TileInjectionRules, true, true, true, true)
			{
				drawHeaderCallback = rect =>
				{
					EditorGUI.LabelField(rect, Labels.TileInjection);
				},
				drawElementCallback = DrawTileInjectionRule,
				elementHeightCallback = index =>
				{
					var element = properties.TileInjectionRules.GetArrayElementAtIndex(index);

					// If collapsed, just one line
					if (!element.isExpanded)
						return EditorGUIUtility.singleLineHeight + 6;

					// If expanded, enough lines for all fields
					int lines = 8;
					return (lines + 1) * (EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing) + 4;
				}
			};

			var flow = target as DungeonFlow;

			if (flow != null)
			{
				foreach (var line in flow.Lines)
					line.Graph = flow;
				foreach (var node in flow.Nodes)
					node.Graph = flow;
			}
		}

		private ReorderableList CreateTagPairList(SerializedProperty tagPairsProperty, GUIContent header)
		{
			return new ReorderableList(serializedObject, tagPairsProperty)
			{
				drawHeaderCallback = rect =>
				{
					EditorGUI.LabelField(rect, header);
				},
				drawElementCallback = (rect, index, isActive, isFocused) =>
				{
					EditorGUI.PropertyField(rect, tagPairsProperty.GetArrayElementAtIndex(index), GUIContent.none);
				},
			};
		}

		private void DrawTileInjectionRule(Rect rect, int index, bool isActive, bool isFocused)
		{
			var element = properties.TileInjectionRules.GetArrayElementAtIndex(index);
			rect.y += 2;
			float lineHeight = EditorGUIUtility.singleLineHeight;
			float spacing = EditorGUIUtility.standardVerticalSpacing;

			var tileSetProp = element.FindPropertyRelative(nameof(TileInjectionRule.TileSet));
			string tileSetName = "无";

			if (tileSetProp != null && tileSetProp.objectReferenceValue != null)
				tileSetName = tileSetProp.objectReferenceValue.name;

			// Foldout
			element.isExpanded = EditorGUI.Foldout(
				new Rect(rect.x + 10, rect.y, rect.width, lineHeight),
				element.isExpanded,
				new GUIContent(tileSetName),
				true);

			if (!element.isExpanded)
				return;

			float y = rect.y + lineHeight + spacing;

			// TileSet
			EditorGUI.PropertyField(
				new Rect(rect.x, y, rect.width, lineHeight),
				tileSetProp, new GUIContent("Tile 集 (Tile Set)"));
			y += lineHeight + spacing;

			// IsRequired
			var isRequiredProp = element.FindPropertyRelative("IsRequired");
			EditorGUI.PropertyField(
				new Rect(rect.x, y, rect.width, lineHeight),
				isRequiredProp, new GUIContent("是否必需？"));
			y += lineHeight + spacing;

			// CanAppearOnMainPath
			var canAppearOnMainPathProp = element.FindPropertyRelative("CanAppearOnMainPath");
			EditorGUI.PropertyField(
				new Rect(rect.x, y, rect.width, lineHeight),
				canAppearOnMainPathProp, new GUIContent("可出现在主路径？"));
			y += lineHeight + spacing;

			// CanAppearOnBranchPath
			var canAppearOnBranchPathProp = element.FindPropertyRelative("CanAppearOnBranchPath");
			EditorGUI.PropertyField(
				new Rect(rect.x, y, rect.width, lineHeight),
				canAppearOnBranchPathProp, new GUIContent("可出现在分支路径？"));
			y += lineHeight + spacing;

			// IsLocked
			var isLockedProp = element.FindPropertyRelative("IsLocked");
			EditorGUI.PropertyField(
				new Rect(rect.x, y, rect.width, lineHeight),
				isLockedProp, new GUIContent("锁定"));
			y += lineHeight + spacing;

			// LockID
			EditorGUI.BeginDisabledGroup(isLockedProp == null || !isLockedProp.boolValue);
			{
				var lockIDProp = element.FindPropertyRelative("LockID");
				var dungeonFlow = target as DungeonFlow;

				int keyID = lockIDProp.intValue;

				EditorGUI.BeginChangeCheck();
				EditorUtil.DrawKey(
					new Rect(rect.x, y, rect.width, lineHeight),
					new GUIContent("锁类型"), dungeonFlow.KeyManager, ref keyID);

				if (EditorGUI.EndChangeCheck())
					lockIDProp.intValue = keyID;
			}
			EditorGUI.EndDisabledGroup();
			y += lineHeight + spacing;

			// NormalizedPathDepth
			var pathDepthProp = element.FindPropertyRelative("NormalizedPathDepth");
			EditorGUI.PropertyField(
				new Rect(rect.x, y, rect.width, lineHeight),
				pathDepthProp, new GUIContent("路径深度"));
			y += lineHeight + spacing;

			// NormalizedBranchDepth
			var branchDepthProp = element.FindPropertyRelative("NormalizedBranchDepth");
			EditorGUI.PropertyField(
				new Rect(rect.x, y, rect.width, lineHeight),
				branchDepthProp, new GUIContent("分支深度"));
		}

		private string GetCurrentBranchModeLabel()
		{
			var dungeonFlow = target as DungeonFlow;

			switch (dungeonFlow.BranchMode)
			{
				case BranchMode.Local:
					return Labels.LocalBranchMode;
				case BranchMode.Global:
					return Labels.GlobalBranchMode;
				case BranchMode.Section:
					return Labels.SectionBranchMode;

				default:
					throw new NotImplementedException(string.Format("{0}.{1} is not implemented", typeof(BranchMode).Name, dungeonFlow.BranchMode));
			}
		}

		public override void OnInspectorGUI()
		{
			var data = target as DungeonFlow;

			if (data == null)
				return;

			serializedObject.Update();

			if (GUILayout.Button(Labels.Validate))
				DungeonValidator.Instance.Validate(data);

			EditorGUILayout.Space();
			EditorGUILayout.Space();
			EditorGUILayout.Space();

			EditorGUILayout.PropertyField(properties.KeyManager, Labels.KeyManager);
			EditorGUILayout.PropertyField(properties.CustomPipeline, Labels.CustomPipeline);
			EditorGUILayout.PropertyField(properties.Length, Labels.Length);

			// Doorway Connections
			using (new EditorGUILayout.VerticalScope("box"))
			{
				EditorGUILayout.LabelField(Labels.DoorwayConnectionHeader, EditorStyles.boldLabel);
				EditorGUILayout.PropertyField(properties.DoorwayConnectionChance, Labels.DoorwayConnectionChance);
				EditorGUILayout.PropertyField(properties.RestrictConnectionToSameSection, Labels.RestrictConnectionToSameSection);
			}

			// Straightening Section
			using (new EditorGUILayout.VerticalScope("box"))
			{
				EditorGUILayout.LabelField(Labels.PathStraighteningHeader, EditorStyles.boldLabel);
				EditorUtil.DrawStraightenSettings(properties.StraighteningSettings, true);
			}

			// Branches Section
			using (new EditorGUILayout.VerticalScope("box"))
			{
				EditorGUILayout.LabelField(Labels.BranchingHeader, EditorStyles.boldLabel);

				// Branch Mode
				EditorGUILayout.HelpBox(GetCurrentBranchModeLabel(), MessageType.Info);
				EditorGUILayout.PropertyField(properties.BranchMode, Labels.BranchMode);

				EditorGUI.BeginDisabledGroup(data.BranchMode != BranchMode.Global);
				EditorGUILayout.PropertyField(properties.BranchCount, Labels.BranchCount);
				EditorGUI.EndDisabledGroup();

				EditorGUILayout.Space();
				EditorGUILayout.Space();

				// Branch Prune Tags
				EditorGUILayout.PropertyField(properties.BranchTagPruneMode, Labels.BranchPruneMode);
				EditorGUILayout.Space();
				properties.BranchPruneTagsList.DoLayoutList();
			}

			EditorGUILayout.Space();
			EditorGUILayout.Space();
			EditorGUILayout.Space();

			// Open Flow Editor
			if (GUILayout.Button(Labels.OpenFlowEditor))
				DungeonFlowEditorWindow.Open(data);

			EditorGUILayout.Space();

			// Tile Injection Rules (ReorderableList)
			properties.TileInjectionRulesList.DoLayoutList();

			EditorGUILayout.Space();

			// Global Props
			using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
			{
				EditorGUI.indentLevel++;

				var globalProps = properties.GlobalProps.serializedProperty;
				globalProps.isExpanded = EditorGUILayout.Foldout(globalProps.isExpanded, Labels.GlobalProps, true);
				EditorGUILayout.Space();

				if (globalProps.isExpanded)
					properties.GlobalProps.DoLayoutList();

				EditorGUI.indentLevel--;
			}

			EditorGUILayout.Space();

			// Tile Connection Rules
			using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
			{
				EditorGUI.indentLevel++;
				properties.TileTagConnectionMode.isExpanded = EditorGUILayout.Foldout(properties.TileTagConnectionMode.isExpanded, Labels.ConnectionRules, true);
				EditorGUILayout.Space();

				if (properties.TileTagConnectionMode.isExpanded)
				{
					selectedConnectionRulesTab = GUILayout.Toolbar(selectedConnectionRulesTab, Labels.ConnectionRulesTabs);

					// Tile Connection Rules
					if (selectedConnectionRulesTab == 0)
					{
						EditorGUILayout.Space();
						EditorGUILayout.PropertyField(properties.TileTagConnectionMode, Labels.TileConnectionTagMode);
						EditorGUILayout.Space();
						properties.TileConnectionTagsList.DoLayoutList();
					}
					// Doorway Connection Rules
					else
					{
						EditorGUILayout.Space();
						EditorGUILayout.PropertyField(properties.DoorwayTagConnectionMode, Labels.DoorwayConnectionTagMode);
						EditorGUILayout.PropertyField(properties.OverrideDoorwaySockets, Labels.OverrideDoorwaySockets);
						EditorGUILayout.Space();
						properties.DoorwayConnectionTagsList.DoLayoutList();
					}

					EditorGUILayout.Space();
				}

				EditorGUI.indentLevel--;
			}


			if (GUI.changed)
				EditorUtility.SetDirty(data);

			serializedObject.ApplyModifiedProperties();
		}

		private float GetGlobalPropHeight(int index)
		{
			return EditorGUI.GetPropertyHeight(properties.GlobalProps.serializedProperty.GetArrayElementAtIndex(index));
		}

		private void DrawGlobalProp(Rect rect, int index)
		{
			var propProperty = properties.GlobalProps.serializedProperty.GetArrayElementAtIndex(index);
			EditorGUI.PropertyField(rect, propProperty);
		}
	}
}
