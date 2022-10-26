using UnityEditor;
using UnityEngine;

namespace Editor
{
    [CustomEditor(typeof(LiDAR))]
    public class CustomInspector_LiDAR : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            // DrawDefaultInspector();
            serializedObject.Update();
            LiDAR myTarget = (LiDAR)target;
            
            // Default stuff, except line renderer
            DrawPropertiesExcluding(serializedObject, new string[]{ "lineRenderer", "useLineRenderer", "lineSpawnSource", "maxLinesPerFrame" });


            EditorGUILayout.Separator();
            myTarget.useLineRenderer = EditorGUILayout.BeginToggleGroup("Draw Lines", myTarget.useLineRenderer);
            myTarget.lineRenderer = (LineRenderer)EditorGUILayout.ObjectField("Line Renderer", myTarget.lineRenderer, typeof(LineRenderer), true);
            myTarget.lineSpawnSource = (Transform)EditorGUILayout.ObjectField("Line Spawn Source", myTarget.lineSpawnSource, typeof(Transform), true);
            myTarget.maxLinesPerFrame = EditorGUILayout.IntField("Max Lines Drawn Per Frame", myTarget.maxLinesPerFrame);
            EditorGUILayout.EndToggleGroup();
            
            serializedObject.ApplyModifiedProperties();
        }
    }
}
