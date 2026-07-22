using DunGen.Collision;
using DunGen.Generation;
using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace DunGen.Editor.Drawers
{
	namespace DunGen.Editor.Drawers
	{
		[AttributeUsage(AttributeTargets.Field)]
		public sealed class EditorTimeDungeonGeneratorAttribute : Attribute { }

		[CustomPropertyDrawer(typeof(DungeonGeneratorSettings))]
		public sealed class DungeonGeneratorSettingsDrawer : PropertyDrawer
		{
			private static class Labels
			{
				public static readonly GUIContent DungeonFlow = new GUIContent("地牢流程 (Dungeon Flow)", "定义待生成地牢布局与结构的 Dungeon Flow 资产");
				public static readonly GUIContent PipelineOverride = new GUIContent("管线覆盖", "若设置，将使用此生成管线，而非 DungeonFlow 中定义的管线");
				public static readonly GUIContent RandomizeSeed = new GUIContent("随机种子", "勾选后，每次生成地牢都会创建新的随机种子。取消勾选则每次使用固定种子");
				public static readonly GUIContent Seed = new GUIContent("种子", "用于生成地牢布局的种子。用相同种子多次生成会得到完全一致的结果");
				public static readonly GUIContent MaxFailedAttempts = new GUIContent("最大失败次数", "DunGen 生成地牢布局失败多少次后放弃。仅在编辑器内生效；打包后 DunGen 会无限重试");
				public static readonly GUIContent LengthMultiplier = new GUIContent("长度倍率", "在不修改 Dungeon Flow 资产的前提下改变地牢长度。1 = 正常长度，2 = 双倍，0.5 = 一半，以此类推");
				public static readonly GUIContent UpDirection = new GUIContent("上方向", "地牢的上方向。它不会真的旋转地牢，但必须与你地牢布局期望的上向量一致——通常 3D 与横版 2D 用 +Y，俯视 2D 用 -Z");
				public static readonly GUIContent TriggerPlacement = new GUIContent("触发器放置", "在 Tile 周围放置触发碰撞体，可配合 DungenCharacter 组件在切换房间时接收事件");
				public static readonly GUIContent TriggerLayer = new GUIContent("触发器层", "勾选“放置 Tile 触发器”时，Tile 根对象所放置的层");
				public static readonly GUIContent GenerateAsynchronously = new GUIContent("异步生成", "勾选后，DunGen 会在不阻塞 Unity 主线程的情况下生成布局，可用于显示加载动画等");
				public static readonly GUIContent MaxFrameTime = new GUIContent("每帧最大耗时", "地牢生成每帧允许占用的毫秒数");
				public static readonly GUIContent PauseBetweenRooms = new GUIContent("房间间暂停", "若大于零，每放置一个房间后地牢生成会暂停设定的时间（秒）；便于观察生成过程");
				public static readonly GUIContent OverlapThreshold = new GUIContent("重叠阈值", "两个相连 Tile 允许重叠而不被丢弃的最大距离。若门口不完全位于 Tile 的 AABB 上，相连时可能略有重叠，此属性可帮助修正");
				public static readonly GUIContent MultiDungeonCollisionMode = new GUIContent("多地牢碰撞", "检测 Tile 是否碰撞时，应检查哪些其他地牢？");
				public static readonly GUIContent DisallowOverhangs = new GUIContent("禁止悬垂", "勾选后，两个 Tile 不能沿上向量重叠（房间不能生成在另一房间上方）");
				public static readonly GUIContent Padding = new GUIContent("间距", "两个未连接 Tile 之间的最小缓冲距离");
				public static readonly GUIContent RestrictToBounds = new GUIContent("限制在包围盒内？", "勾选后，Tile 只会放置在下方指定的包围盒内。可能增加生成耗时");
				public static readonly GUIContent PlacementBounds = new GUIContent("放置包围盒", "Tile 不允许放置在这些包围盒之外");
				public static readonly GUIContent RepeatMode = new GUIContent("重复模式");

				public static readonly GUIContent[] UpDirectionDisplayOptions = new GUIContent[]
				{
					new GUIContent("+X"),
					new GUIContent("-X"),
					new GUIContent("+Y"),
					new GUIContent("-Y"),
					new GUIContent("+Z"),
					new GUIContent("-Z"),
				};
			}

			public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
			{
				// This drawer uses IMGUI layout (EditorGUILayout)
				return 0;
			}

			public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
			{
				// Determine if this is an editor-time or runtime dungeon generator
				// by inspecting the `DungeonGenerator` field that owns this settings instance.
				var splitPropertyPath = property.propertyPath.Split('.');
				var generatorPath = string.Join(".", splitPropertyPath, 0, splitPropertyPath.Length - 1);

				var dungeonGeneratorField = property.serializedObject.targetObject.GetType().GetField(generatorPath, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);

				var editorTimeAttribute = dungeonGeneratorField.GetCustomAttribute<EditorTimeDungeonGeneratorAttribute>();
				bool isRuntimeDungeon = editorTimeAttribute == null;

				// We intentionally ignore `position` and use GUILayout so the existing UI structure stays intact.
				EditorGUI.BeginProperty(position, label, property);

				var dungeonFlowProp = property.FindPropertyRelative(nameof(DungeonGeneratorSettings.DungeonFlow));
				var pipelineOverrideProp = property.FindPropertyRelative(nameof(DungeonGeneratorSettings.PipelineOverride));
				var shouldRandomizeSeedProp = property.FindPropertyRelative(nameof(DungeonGeneratorSettings.ShouldRandomizeSeed));
				var seedProp = property.FindPropertyRelative(nameof(DungeonGeneratorSettings.Seed));
				var maxAttemptCountProp = property.FindPropertyRelative(nameof(DungeonGeneratorSettings.MaxAttemptCount));
				var lengthMultiplierProp = property.FindPropertyRelative(nameof(DungeonGeneratorSettings.LengthMultiplier));
				var upDirectionProp = property.FindPropertyRelative(nameof(DungeonGeneratorSettings.UpDirection));
				var debugRenderSettingsProp = property.FindPropertyRelative(nameof(DungeonGeneratorSettings.DebugRenderSettings));
				var triggerPlacementProp = property.FindPropertyRelative(nameof(DungeonGeneratorSettings.TriggerPlacement));
				var tileTriggerLayerProp = property.FindPropertyRelative(nameof(DungeonGeneratorSettings.TileTriggerLayer));
				var generateAsyncProp = property.FindPropertyRelative(nameof(DungeonGeneratorSettings.GenerateAsynchronously));
				var maxAsyncFrameMsProp = property.FindPropertyRelative(nameof(DungeonGeneratorSettings.MaxAsyncFrameMilliseconds));
				var pauseBetweenRoomsProp = property.FindPropertyRelative(nameof(DungeonGeneratorSettings.PauseBetweenRooms));
				var restrictToBoundsProp = property.FindPropertyRelative(nameof(DungeonGeneratorSettings.RestrictDungeonToBounds));
				var placementBoundsProp = property.FindPropertyRelative(nameof(DungeonGeneratorSettings.TilePlacementBounds));
				var overrideRepeatModeProp = property.FindPropertyRelative(nameof(DungeonGeneratorSettings.OverrideRepeatMode));
				var repeatModeProp = property.FindPropertyRelative(nameof(DungeonGeneratorSettings.RepeatMode));
				var overrideAllowTileRotationProp = property.FindPropertyRelative(nameof(DungeonGeneratorSettings.OverrideAllowTileRotation));
				var allowTileRotationProp = property.FindPropertyRelative(nameof(DungeonGeneratorSettings.AllowTileRotation));
				var collisionSettingsProp = property.FindPropertyRelative(nameof(DungeonGeneratorSettings.CollisionSettings));

				EditorGUILayout.PropertyField(dungeonFlowProp, Labels.DungeonFlow);
				EditorGUILayout.PropertyField(shouldRandomizeSeedProp, Labels.RandomizeSeed);

				if (!shouldRandomizeSeedProp.boolValue)
					EditorGUILayout.PropertyField(seedProp, Labels.Seed);

				EditorGUILayout.PropertyField(lengthMultiplierProp, Labels.LengthMultiplier);

				upDirectionProp.enumValueIndex = EditorGUILayout.Popup(Labels.UpDirection, upDirectionProp.enumValueIndex, Labels.UpDirectionDisplayOptions);

				if (lengthMultiplierProp.floatValue < 0f)
					lengthMultiplierProp.floatValue = 0f;

				if (isRuntimeDungeon)
				{
					// Asynchronous Generation
					EditorGUILayout.Space();
					EditorGUILayout.BeginVertical(EditorStyles.helpBox);
					{
						EditorGUI.indentLevel++;
						generateAsyncProp.isExpanded = EditorGUILayout.Foldout(generateAsyncProp.isExpanded, "异步生成", true);

						if (generateAsyncProp.isExpanded)
						{
							EditorGUILayout.PropertyField(generateAsyncProp, Labels.GenerateAsynchronously);

							var unitsLabelSize = EditorStyles.label.CalcSize(new GUIContent("milliseconds"));

							EditorGUI.BeginDisabledGroup(!generateAsyncProp.boolValue);

							EditorGUILayout.BeginHorizontal();
							maxAsyncFrameMsProp.floatValue = EditorGUILayout.Slider(Labels.MaxFrameTime, maxAsyncFrameMsProp.floatValue, 0f, 1000f);
							EditorGUILayout.LabelField("毫秒", GUILayout.Width(unitsLabelSize.x));
							EditorGUILayout.EndHorizontal();

							EditorGUILayout.BeginHorizontal();
							pauseBetweenRoomsProp.floatValue = EditorGUILayout.Slider(Labels.PauseBetweenRooms, pauseBetweenRoomsProp.floatValue, 0f, 5f);
							EditorGUILayout.LabelField("秒", GUILayout.Width(unitsLabelSize.x));
							EditorGUILayout.EndHorizontal();

							EditorGUI.EndDisabledGroup();
						}

						EditorGUI.indentLevel--;
					}
					EditorGUILayout.EndVertical();
				}

				// Collision
				if (collisionSettingsProp != null)
				{
					EditorGUILayout.BeginVertical(EditorStyles.helpBox);
					{
						EditorGUI.indentLevel++;
						collisionSettingsProp.isExpanded = EditorGUILayout.Foldout(collisionSettingsProp.isExpanded, "碰撞", true);

						if (collisionSettingsProp.isExpanded)
						{
							EditorGUILayout.PropertyField(triggerPlacementProp, Labels.TriggerPlacement);

							EditorGUI.BeginDisabledGroup(triggerPlacementProp.enumValueIndex == 0);
							{
								tileTriggerLayerProp.intValue = EditorGUILayout.LayerField(Labels.TriggerLayer, tileTriggerLayerProp.intValue);
							}
							EditorGUI.EndDisabledGroup();

							EditorGUILayout.Space();

							EditorGUILayout.PropertyField(collisionSettingsProp.FindPropertyRelative(nameof(DungeonCollisionSettings.OverlapThreshold)), Labels.OverlapThreshold);
							EditorGUILayout.PropertyField(collisionSettingsProp.FindPropertyRelative(nameof(DungeonCollisionSettings.MultiDungeonCollisionMode)), Labels.MultiDungeonCollisionMode);
							EditorGUILayout.PropertyField(collisionSettingsProp.FindPropertyRelative(nameof(DungeonCollisionSettings.DisallowOverhangs)), Labels.DisallowOverhangs);

							var paddingProp = collisionSettingsProp.FindPropertyRelative("Padding");
							EditorGUI.BeginChangeCheck();

							float padding = EditorGUILayout.DelayedFloatField(Labels.Padding, paddingProp.floatValue);

							if (EditorGUI.EndChangeCheck())
								paddingProp.floatValue = Mathf.Max(0f, padding);
						}

						EditorGUI.indentLevel--;
					}
					EditorGUILayout.EndVertical();
				}

				// Constraints
				EditorGUILayout.BeginVertical(EditorStyles.helpBox);
				{
					EditorGUI.indentLevel++;
					restrictToBoundsProp.isExpanded = EditorGUILayout.Foldout(restrictToBoundsProp.isExpanded, "约束", true);

					if (restrictToBoundsProp.isExpanded)
					{
						EditorGUILayout.HelpBox("约束会让地牢生成更容易失败。约束越严格，失败概率越高。", MessageType.Info);
						EditorGUILayout.Space();

						EditorGUILayout.PropertyField(restrictToBoundsProp, Labels.RestrictToBounds);

						EditorGUI.BeginDisabledGroup(!restrictToBoundsProp.boolValue);
						EditorGUILayout.PropertyField(placementBoundsProp, Labels.PlacementBounds);
						EditorGUI.EndDisabledGroup();
					}

					EditorGUI.indentLevel--;
				}
				EditorGUILayout.EndVertical();

				// Global Overrides
				EditorGUILayout.BeginVertical(EditorStyles.helpBox);
				{
					EditorGUI.indentLevel++;
					overrideRepeatModeProp.isExpanded = EditorGUILayout.Foldout(overrideRepeatModeProp.isExpanded, "全局覆盖", true);

					if (overrideRepeatModeProp.isExpanded)
					{
						EditorGUILayout.BeginHorizontal();
						{
							EditorGUILayout.PropertyField(overrideRepeatModeProp, GUIContent.none, GUILayout.Width(10));
							EditorGUI.BeginDisabledGroup(!overrideRepeatModeProp.boolValue);
							EditorGUILayout.PropertyField(repeatModeProp, Labels.RepeatMode);
							EditorGUI.EndDisabledGroup();
						}
						EditorGUILayout.EndHorizontal();

						DrawOverride("允许 Tile 旋转", overrideAllowTileRotationProp, allowTileRotationProp);
					}

					EditorGUI.indentLevel--;
				}
				EditorGUILayout.EndVertical();

				// Debug
				EditorGUILayout.BeginVertical(EditorStyles.helpBox);
				{
					EditorGUI.indentLevel++;
					debugRenderSettingsProp.isExpanded = EditorGUILayout.Foldout(debugRenderSettingsProp.isExpanded, "调试", true);

					if (debugRenderSettingsProp.isExpanded)
					{
						if(isRuntimeDungeon)
							DrawDebugRenderSettings(debugRenderSettingsProp);

						EditorGUILayout.PropertyField(maxAttemptCountProp, Labels.MaxFailedAttempts);
					}

					EditorGUI.indentLevel--;
				}
				EditorGUILayout.EndVertical();

				// Advanced
				EditorGUILayout.BeginVertical(EditorStyles.helpBox);
				{
					EditorGUI.indentLevel++;
					pipelineOverrideProp.isExpanded = EditorGUILayout.Foldout(pipelineOverrideProp.isExpanded, "高级", true);

					if (pipelineOverrideProp.isExpanded)
					{
						EditorGUILayout.PropertyField(pipelineOverrideProp, Labels.PipelineOverride);
					}

					EditorGUI.indentLevel--;
				}
				EditorGUILayout.EndVertical();

				EditorGUI.EndProperty();
			}

			private static void DrawOverride(string label, SerializedProperty overrideProp, SerializedProperty valueProp)
			{
				EditorGUILayout.BeginHorizontal();
				EditorGUILayout.PropertyField(overrideProp, GUIContent.none, GUILayout.Width(10));
				EditorGUI.BeginDisabledGroup(!overrideProp.boolValue);
				EditorGUILayout.PropertyField(valueProp, new GUIContent(label));
				EditorGUI.EndDisabledGroup();
				EditorGUILayout.EndHorizontal();
			}

			private static void DrawDebugRenderSettings(SerializedProperty debugRenderSettingsProp)
			{
				var enabledProp = debugRenderSettingsProp.FindPropertyRelative(nameof(DebugRenderSettings.Enabled));
				var showCollisionProp = debugRenderSettingsProp.FindPropertyRelative(nameof(DebugRenderSettings.ShowCollision));
				var showPathColoursProp = debugRenderSettingsProp.FindPropertyRelative(nameof(DebugRenderSettings.ShowPathColours));

				EditorGUILayout.PropertyField(enabledProp, new GUIContent("启用调试渲染"));

				if (enabledProp.boolValue)
				{
					EditorGUI.indentLevel++;

					EditorGUILayout.PropertyField(showCollisionProp, new GUIContent("碰撞", "显示碰撞粗检测阶段的可视化（若已启用）"));
					EditorGUILayout.PropertyField(showPathColoursProp, new GUIContent("路径颜色", "在地牢各 Tile 周围绘制方框，按 Tile 所属路径类型及沿分支的深度着色。主路径：红→绿，分支路径：蓝→紫"));

					EditorGUI.indentLevel--;
				}
			}
		}
	}
}