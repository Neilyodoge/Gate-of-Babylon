using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace DunGen.Editor
{
	[CanEditMultipleObjects]
	[CustomEditor(typeof(Tile))]
	public class TileInspector : UnityEditor.Editor
	{
		#region Labels

		private static class Label
		{
			public static readonly GUIContent AllowRotation = new GUIContent("允许旋转", "勾选后，该 Tile 允许被生成器旋转。此设置可在生成器全局设置中统一覆盖");
			public static readonly GUIContent RepeatMode = new GUIContent("重复模式", "决定该 Tile 在地牢中如何重复出现。此设置可在生成器全局设置中统一覆盖");
			public static readonly GUIContent OverrideAutomaticTileBounds = new GUIContent("手动 Tile 包围盒", "DunGen 会自动计算 Tile 的包围体积。若自动包围盒有问题，勾选此项手动设置。");
			public static readonly GUIContent FitToTile = new GUIContent("适配到 Tile", "用 DunGen 的自动包围盒计算，尝试让包围盒贴合该 Tile。");
			public static readonly GUIContent Entrances = new GUIContent("入口门", "若设置，DunGen 会始终用其中一个门口作为该 Tile 的入口。");
			public static readonly GUIContent Exits = new GUIContent("出口门", "若设置，DunGen 会始终用其中一个门口作为该 Tile 的首个出口。");
			public static readonly GUIContent OverrideConnectionChance = new GUIContent("覆盖连接概率", "勾选后，该 Tile 会覆盖流程图中的全局连接概率。若两个 Tile 都覆盖，则取较低值");
			public static readonly GUIContent ConnectionChance = new GUIContent("连接概率", "该 Tile 与重叠门口相连接的概率");
			public static readonly GUIContent Tags = new GUIContent("标签", "用户自定义标签集合，可在流程图中限制 Tile 连接，或在代码中引用以实现自定义逻辑");
		}

		#endregion

		private SerializedProperty allowRotation;
		private SerializedProperty repeatMode;
		private SerializedProperty overrideAutomaticTileBounds;
		private SerializedProperty tileBoundsOverride;
		private SerializedProperty entrances;
		private SerializedProperty exits;
		private SerializedProperty overrideConnectionChance;
		private SerializedProperty connectionChance;
		private SerializedProperty tags;

		private BoxBoundsHandle overrideBoundsHandle;


		private void OnEnable()
		{
			allowRotation = serializedObject.FindProperty("AllowRotation");
			repeatMode = serializedObject.FindProperty("RepeatMode");
			overrideAutomaticTileBounds = serializedObject.FindProperty("OverrideAutomaticTileBounds");
			tileBoundsOverride = serializedObject.FindProperty("TileBoundsOverride");
			entrances = serializedObject.FindProperty("Entrances");
			exits = serializedObject.FindProperty("Exits");
			overrideConnectionChance = serializedObject.FindProperty("OverrideConnectionChance");
			connectionChance = serializedObject.FindProperty("ConnectionChance");
			tags = serializedObject.FindProperty("Tags");


			overrideBoundsHandle = new BoxBoundsHandle();
			overrideBoundsHandle.SetColor(Color.red);
		}

		public override void OnInspectorGUI()
		{
			serializedObject.Update();

			EditorGUILayout.PropertyField(allowRotation, Label.AllowRotation);
			EditorGUILayout.PropertyField(repeatMode, Label.RepeatMode);

			EditorGUILayout.Space();

			// Tile Bounds Override
			EditorGUILayout.BeginVertical("box");

			EditorGUILayout.PropertyField(overrideAutomaticTileBounds, Label.OverrideAutomaticTileBounds);

			EditorGUI.BeginDisabledGroup(!overrideAutomaticTileBounds.boolValue);

			EditorGUILayout.Space();
			EditorGUILayout.PropertyField(tileBoundsOverride, GUIContent.none);

			if (GUILayout.Button(Label.FitToTile))
			{
				Undo.RecordObjects(targets, "Fit Bounds to Tile(s)");

				foreach (var t in targets)
				{
					var tile = t as Tile;

					if (tile == null)
						continue;

					var newBounds = tile.GetBoundsCalculator().CalculateLocalBounds(tile.gameObject);

					var so = new SerializedObject(tile);
					so.Update();

					var overrideBoundsProp = so.FindProperty(nameof(Tile.TileBoundsOverride));
					overrideBoundsProp.boundsValue = newBounds;

					so.ApplyModifiedProperties();
					EditorUtility.SetDirty(tile);
				}

				serializedObject.Update();
			}

			EditorGUI.EndDisabledGroup();
			EditorGUILayout.Space();
			EditorGUILayout.EndVertical();


			// Connection Chance Override
			EditorGUILayout.BeginVertical("box");

			EditorGUILayout.PropertyField(overrideConnectionChance, Label.OverrideConnectionChance);

			EditorGUI.BeginDisabledGroup(!overrideConnectionChance.boolValue);

			EditorGUILayout.Slider(connectionChance, 0f, 1f, Label.ConnectionChance);

			EditorGUI.EndDisabledGroup();
			EditorGUILayout.Space();
			EditorGUILayout.EndVertical();


			// Entrance & Exit doorways
			EditorGUILayout.BeginVertical("box");
			EditorGUILayout.HelpBox("可选：为该 Tile 指定某些门口作为入口或出口", MessageType.Info);

			EditorGUI.indentLevel++;
			EditorGUILayout.PropertyField(entrances, Label.Entrances);
			EditorGUILayout.PropertyField(exits, Label.Exits);
			EditorGUI.indentLevel--;

			EditorGUILayout.Space();
			EditorGUILayout.EndVertical();

			EditorGUILayout.Space();
			EditorGUILayout.Space();

			EditorGUILayout.PropertyField(tags, Label.Tags);

			EditorGUILayout.Space();

			if (GUILayout.Button("重新计算包围盒"))
			{
				Undo.RecordObjects(targets, "Recalculate Tile Bounds");

				foreach (var t in targets)
				{
					var tile = t as Tile;

					if (tile == null)
						continue;

					if (tile.RecalculateBounds())
						EditorUtility.SetDirty(tile);
				}

				serializedObject.Update();
			}

			serializedObject.ApplyModifiedProperties();
		}

		private void OnSceneGUI()
		{
			var tile = target as Tile;

			if (tile == null)
				return;

			// Create a temporary SerializedObject for this specific target
			using (var so = new SerializedObject(tile))
			{
				var overrideBoundsProp = so.FindProperty(nameof(Tile.OverrideAutomaticTileBounds));
				var boundsProp = so.FindProperty(nameof(Tile.TileBoundsOverride));

				// If the property setup is invalid or unchecked, exit
				if (overrideBoundsProp == null || !overrideBoundsProp.boolValue)
					return;

				// Sync handle to this specific object's bounds
				overrideBoundsHandle.center = boundsProp.boundsValue.center;
				overrideBoundsHandle.size = boundsProp.boundsValue.size;

				// Allow Unity to identify this handle uniquely
				int controlId = GUIUtility.GetControlID(FocusType.Passive);

				EditorGUI.BeginChangeCheck();

				using (new Handles.DrawingScope(tile.transform.localToWorldMatrix))
				{
					overrideBoundsHandle.DrawHandle();
				}

				if (EditorGUI.EndChangeCheck())
				{
					boundsProp.boundsValue = new Bounds(overrideBoundsHandle.center, overrideBoundsHandle.size);
					so.ApplyModifiedProperties();
				}
			}
		}
	}
}