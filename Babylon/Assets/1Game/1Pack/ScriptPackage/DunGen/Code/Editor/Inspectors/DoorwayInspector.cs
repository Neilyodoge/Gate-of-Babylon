using UnityEngine;
using UnityEditor;
using UnityEditorInternal;
using System.Linq;
using System;
using DunGen.Weighting;

namespace DunGen.Editor
{
	[CustomEditor(typeof(Doorway))]
	[CanEditMultipleObjects]
	public class DoorwayInspector : UnityEditor.Editor
	{
		#region Constants

		private static readonly GUIContent socketGroupLabel = new GUIContent("Socket 接口", "决定两个门口能否连接。默认只有 socket 组匹配的门口才能相互连接");
		private static readonly GUIContent hideConditionalObjectsLabel = new GUIContent("隐藏条件对象？", "勾选后，场景中的门或封堵对象会被隐藏以减少杂乱。不影响运行时结果");
		private static readonly GUIContent connectorSceneObjectsLabel = new GUIContent("场景对象", "门口在使用中（已连接）时【保留】的场景对象。门口两侧都会保留");
		private static readonly GUIContent blockerSceneObjectsLabel = new GUIContent("场景对象", "门口在使用中（已连接）时【移除】的场景对象");
		private static readonly GUIContent priorityLabel = new GUIContent("优先级", "两个门口连接时，优先级较高的一方的门预制体会被采用");
		private static readonly GUIContent doorPrefabLabel = new GUIContent("随机预制体权重", "门口在使用中（已连接）时，从此列表（及连接的门口）随机生成一个预制体");
		private static readonly GUIContent blockerPrefabLabel = new GUIContent("随机预制体权重", "门口未使用（未连接）时，从此列表（及连接的门口）随机生成一个预制体");
		private static readonly GUIContent avoidRotationLabel = new GUIContent("避免旋转？", "勾选后，放置的预制体【不会】朝向对齐门口");
		private static readonly GUIContent prefabPositionOffsetLabel = new GUIContent("位置偏移", "生成该预制体时可选的位置偏移，相对于门口的 transform");
		private static readonly GUIContent prefabRotationOffsetLabel = new GUIContent("旋转偏移", "生成该预制体时可选的旋转偏移，相对于门口的 transform");
		private static readonly GUIContent connectorsLabel = new GUIContent("连接件 (Connectors)", "门口在使用中（已连接）时使用的场景对象与预制体");
		private static readonly GUIContent blockersLabel = new GUIContent("封堵件 (Blockers)", "门口未使用（未连接）时使用的场景对象与预制体");
		private static readonly GUIContent tagsLabel = new GUIContent("标签", "标签集合，可在代码中定义自定义连接逻辑（参见 DoorwayPairFinder.CustomConnectionRules）");

		#endregion

		private SerializedProperty socketProp;
		private SerializedProperty hideConditionalObjectsProp;
		private SerializedProperty priorityProp;
		private SerializedProperty avoidDoorPrefabRotationProp;
		private SerializedProperty doorPrefabPositionOffsetProp;
		private SerializedProperty doorPrefabRotationOffsetProp;
		private SerializedProperty avoidBlockerPrefabRotationProp;
		private SerializedProperty blockerPrefabPositionOffsetProp;
		private SerializedProperty blockerPrefabRotationOffsetProp;
		private SerializedProperty tagsProp;
		private SerializedProperty connectorPrefabs;
		private SerializedProperty blockerPrefabs;
		private ReorderableList connectorSceneObjectsList;
		private ReorderableList blockerSceneObjectsList;


		private void OnEnable()
		{
			socketProp = serializedObject.FindProperty("socket");
			hideConditionalObjectsProp = serializedObject.FindProperty("hideConditionalObjects");
			priorityProp = serializedObject.FindProperty(nameof(Doorway.DoorPrefabPriority));
			avoidDoorPrefabRotationProp = serializedObject.FindProperty(nameof(Doorway.AvoidRotatingDoorPrefab));
			doorPrefabPositionOffsetProp = serializedObject.FindProperty(nameof(Doorway.DoorPrefabPositionOffset));
			doorPrefabRotationOffsetProp = serializedObject.FindProperty(nameof(Doorway.DoorPrefabRotationOffset));
			avoidBlockerPrefabRotationProp = serializedObject.FindProperty(nameof(Doorway.AvoidRotatingBlockerPrefab));
			blockerPrefabPositionOffsetProp = serializedObject.FindProperty(nameof(Doorway.BlockerPrefabPositionOffset));
			blockerPrefabRotationOffsetProp = serializedObject.FindProperty(nameof(Doorway.BlockerPrefabRotationOffset));
			tagsProp = serializedObject.FindProperty(nameof(Doorway.Tags));
			connectorPrefabs = serializedObject.FindProperty(nameof(Doorway.ConnectorPrefabs));
			blockerPrefabs = serializedObject.FindProperty(nameof(Doorway.BlockerPrefabs));

			connectorSceneObjectsList = new ReorderableList(serializedObject, serializedObject.FindProperty(nameof(Doorway.ConnectorSceneObjects)), true, true, true, true);
			connectorSceneObjectsList.drawElementCallback = (rect, index, isActive, isFocused) => DrawGameObject(connectorSceneObjectsList, rect, index, true);
			connectorSceneObjectsList.drawHeaderCallback = (rect) => EditorGUI.LabelField(rect, new GUIContent($"{connectorSceneObjectsLabel.text} ({connectorSceneObjectsList.count})", connectorSceneObjectsLabel.tooltip));

			blockerSceneObjectsList = new ReorderableList(serializedObject, serializedObject.FindProperty(nameof(Doorway.BlockerSceneObjects)), true, true, true, true);
			blockerSceneObjectsList.drawElementCallback = (rect, index, isActive, isFocused) => DrawGameObject(blockerSceneObjectsList, rect, index, true);
			blockerSceneObjectsList.drawHeaderCallback = (rect) => EditorGUI.LabelField(rect, new GUIContent($"{blockerSceneObjectsLabel.text} ({blockerSceneObjectsList.count})", blockerSceneObjectsLabel.tooltip));
		}

		private void DrawGameObject(ReorderableList list, Rect rect, int index, bool requireSceneObject)
		{
			rect = new Rect(rect.x, rect.y + 2, rect.width, EditorGUIUtility.singleLineHeight);

			EditorGUI.BeginChangeCheck();

			var element = list.serializedProperty.GetArrayElementAtIndex(index);
			var newObject = EditorGUI.ObjectField(rect, element.objectReferenceValue, typeof(GameObject), requireSceneObject);
			bool isValidEntry = true;

			if (newObject != null)
			{
				bool isAsset = EditorUtility.IsPersistent(newObject);
				isValidEntry = isAsset != requireSceneObject;
			}

			if (EditorGUI.EndChangeCheck() && isValidEntry)
				element.objectReferenceValue = newObject;
		}

		public override void OnInspectorGUI()
		{
			var doorways = targets.OfType<Doorway>();
			serializedObject.Update();

			if (socketProp.objectReferenceValue == null)
				socketProp.objectReferenceValue = DunGenSettings.Instance.DefaultSocket;

			EditorGUILayout.PropertyField(socketProp, socketGroupLabel);

			EditorGUI.BeginChangeCheck();
			EditorGUILayout.PropertyField(hideConditionalObjectsProp, hideConditionalObjectsLabel);
			if (EditorGUI.EndChangeCheck())
			{
				foreach(var d in doorways)
					d.HideConditionalObjects = hideConditionalObjectsProp.boolValue;
			}

			EditorGUILayout.Space();
			EditorGUILayout.Space();

			EditorGUI.indentLevel++;

			// Connectors
			EditorGUILayout.BeginVertical("box");

			priorityProp.isExpanded = EditorGUILayout.Foldout(priorityProp.isExpanded, connectorsLabel, true);
			if (priorityProp.isExpanded)
			{
				EditorGUILayout.PropertyField(priorityProp, priorityLabel);
				EditorGUILayout.PropertyField(avoidDoorPrefabRotationProp, avoidRotationLabel);

				EditorGUILayout.PropertyField(doorPrefabPositionOffsetProp, prefabPositionOffsetLabel);
				EditorGUILayout.PropertyField(doorPrefabRotationOffsetProp, prefabRotationOffsetLabel);

				EditorGUILayout.Space();

				EditorGUILayout.BeginVertical(); // We create a group here so the whole list is a drag and drop target
				EditorGUILayout.PropertyField(connectorPrefabs, doorPrefabLabel);
				EditorGUILayout.EndVertical();

				HandlePropDragAndDrop(GUILayoutUtility.GetLastRect(), false, true, (doorway, obj) => doorway.ConnectorPrefabs.Entries.Add(new WeightedEntry<GameObject>(obj)));

				EditorGUILayout.Space();

				EditorGUILayout.BeginVertical(); // We create a group here so the whole list is a drag and drop target
				connectorSceneObjectsList.DoLayoutList();
				EditorGUILayout.EndVertical();

				HandlePropDragAndDrop(GUILayoutUtility.GetLastRect(), true, false, (doorway, obj) => doorway.ConnectorSceneObjects.Add(obj));
			}

			EditorGUILayout.EndVertical();

			// Blockers
			EditorGUILayout.BeginVertical("box");

			avoidBlockerPrefabRotationProp.isExpanded = EditorGUILayout.Foldout(avoidBlockerPrefabRotationProp.isExpanded, blockersLabel, true);
			if (avoidBlockerPrefabRotationProp.isExpanded)
			{
				EditorGUILayout.PropertyField(avoidBlockerPrefabRotationProp, avoidRotationLabel);

				EditorGUILayout.PropertyField(blockerPrefabPositionOffsetProp, prefabPositionOffsetLabel);
				EditorGUILayout.PropertyField(blockerPrefabRotationOffsetProp, prefabRotationOffsetLabel);

				EditorGUILayout.Space();

				EditorGUILayout.BeginVertical(); // We create a group here so the whole list is a drag and drop target
				EditorGUILayout.PropertyField(blockerPrefabs, blockerPrefabLabel);
				EditorGUILayout.EndVertical();

				HandlePropDragAndDrop(GUILayoutUtility.GetLastRect(), false, true, (doorway, obj) => doorway.BlockerPrefabs.Entries.Add(new WeightedEntry<GameObject>(obj)));


				EditorGUILayout.Space();

				EditorGUILayout.BeginVertical(); // We create a group here so the whole list is a drag and drop target
				blockerSceneObjectsList.DoLayoutList();
				EditorGUILayout.EndVertical();

				HandlePropDragAndDrop(GUILayoutUtility.GetLastRect(), true, false, (doorway, obj) => doorway.BlockerSceneObjects.Add(obj));
			}

			EditorGUILayout.EndVertical();
			EditorGUI.indentLevel--;

			EditorGUILayout.PropertyField(tagsProp, tagsLabel);

			serializedObject.ApplyModifiedProperties();



			bool isPlacementInvalid = false;

			// Check if any of the doorways have an invalid transform
			foreach (var doorway in doorways)
			{
				if (!doorway.ValidateTransform(out _, out _, out _))
				{
					isPlacementInvalid = true;
					break;
				}
			}

			// Show a warning message if the doorway(s) appear to be placed incorrectly and offer to fix the issue
			if (isPlacementInvalid)
			{
				EditorGUILayout.Space(20);
				EditorGUILayout.HelpBox("门口的摆放可能不正确。门口应当：\n\n- 朝向背离 Tile（朝外）\n- 旋转对齐到世界坐标轴\n- 位于 Tile 包围盒的边缘\n\n若门口运行正常可忽略此提示，否则可点击下方按钮尝试自动修复摆放问题\n", MessageType.Warning, true);
				EditorGUILayout.Space();

				if (GUILayout.Button(new GUIContent("修复门口摆放")))
				{
					Undo.RecordObjects(doorways.Select(d => d.transform).ToArray(), "Snap Doorway");

					foreach (var doorway in doorways)
						doorway.TrySnapToCorrectedTransform();

					Undo.FlushUndoRecordObjects();
				}
			}
		}

		private void HandlePropDragAndDrop(Rect dragTargetRect, bool allowSceneObjects, bool allowAssetObjects, Action<Doorway, GameObject> addGameObject)
		{
			var evt = Event.current;
			var doorways = targets.OfType<Doorway>();

			if (evt.type == EventType.DragUpdated || evt.type == EventType.DragPerform)
			{
				var validGameObjects = EditorUtil.GetValidGameObjects(DragAndDrop.objectReferences, allowSceneObjects, allowAssetObjects);

				if (dragTargetRect.Contains(evt.mousePosition) && validGameObjects.Any())
				{
					DragAndDrop.visualMode = DragAndDropVisualMode.Copy;

					if (evt.type == EventType.DragPerform)
					{
						Undo.RecordObjects(doorways.ToArray(), "Modify Doorway");
						DragAndDrop.AcceptDrag();

						foreach (var doorway in doorways)
							foreach (var dragObject in validGameObjects)
								addGameObject(doorway, dragObject);

						Undo.FlushUndoRecordObjects();
					}
				}
			}
		}
	}
}