using UnityEditor;
using UnityEngine;

namespace Edgar.Unity.Editor
{
    [CustomEditor(typeof(FogOfWarGrid2D))]
    public class FogOfWarInspector : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var fogOfWar = (FogOfWarGrid2D) target;

            EditorGUIUtility.labelWidth = EditorGUIUtility.currentViewWidth * 0.6f;
            EditorGUIUtility.fieldWidth = 0;

            if (fogOfWar.UseMesh && !fogOfWar.MeshRenderer)
            {
                EditorGUILayout.HelpBox("Please click the 'Create mesh' button to create a new mesh for the Fog of War.", MessageType.Warning);
            }
            
            EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(FogOfWarGrid2D.UseMesh)));

            if (fogOfWar.UseMesh)
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(FogOfWarGrid2D.MeshRenderer)));
            }

            EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(FogOfWarGrid2D.ColorMode)));

            if (fogOfWar.ColorMode == FogOfWarColorMode.CustomColor)
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(FogOfWarGrid2D.FogColor)));
            }

            EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(FogOfWarGrid2D.TransitionMode)));

            if (fogOfWar.TransitionMode == FogOfWarTransitionMode.Custom)
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(FogOfWarGrid2D.TileGranularity)));
                EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(FogOfWarGrid2D.FogSmoothness)));
            }

            EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(FogOfWarGrid2D.Mode)));

            if (fogOfWar.Mode == FogOfWarMode.Wave)
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(FogOfWarGrid2D.WaveSpeed)));
                EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(FogOfWarGrid2D.WaveRevealThreshold)));
            }

            if (fogOfWar.Mode == FogOfWarMode.FadeIn)
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(FogOfWarGrid2D.FadeInDuration)));
            }

            EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(FogOfWarGrid2D.RevealCorridors)));

            if (fogOfWar.RevealCorridors)
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(FogOfWarGrid2D.RevealCorridorsTiles)));
                EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(FogOfWarGrid2D.RevealCorridorsGradually)));
            }

            EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(FogOfWarGrid2D.InitialFogTransparency)));

            EditorGUIUtility.labelWidth = 0;
            EditorGUIUtility.fieldWidth = 0;

            if (GUILayout.Button("Reload"))
            {
                fogOfWar.Reload();
            }

            if (!fogOfWar.MeshRenderer && fogOfWar.UseMesh && GUILayout.Button("Create mesh"))
            {
                fogOfWar.CreateMesh();
            }

            serializedObject.ApplyModifiedProperties();
        }
    }
}