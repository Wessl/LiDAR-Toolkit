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
            DrawPropertiesExcluding(serializedObject, new string[]{ "lineRenderer", "useLineRenderer" });


            EditorGUILayout.Separator();
            myTarget.useLineRenderer = EditorGUILayout.BeginToggleGroup("Draw Lines", myTarget.useLineRenderer);
            myTarget.lineRenderer = (LineRenderer)EditorGUILayout.ObjectField("Line Renderer", myTarget.lineRenderer, typeof(LineRenderer), true);
            EditorGUILayout.EndToggleGroup();
            
            serializedObject.ApplyModifiedProperties();
        }
    }
}
