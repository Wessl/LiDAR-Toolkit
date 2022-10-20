using UnityEditor;
using UnityEngine;

namespace Editor
{
    [CustomEditor(typeof(DrawPoints))]
    public class CustomInspector_DrawPoints : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            // DrawDefaultInspector();
            serializedObject.Update();
            DrawPoints myTarget = (DrawPoints)target;
            
            // Default stuff, except color
            DrawPropertiesExcluding(serializedObject, new string[]{ "pointColor", "overrideColor", "useColorGradient", "farPointColor", "farPointDistance" });


            EditorGUILayout.Separator();
            myTarget.overrideColor = EditorGUILayout.BeginToggleGroup("Override material color", myTarget.overrideColor);
            myTarget.pointColor = EditorGUILayout.ColorField("Color", myTarget.pointColor);
            EditorGUILayout.EndToggleGroup();
            
            myTarget.useColorGradient = EditorGUILayout.BeginToggleGroup("Use color gradient for point distance", myTarget.useColorGradient);
            myTarget.farPointColor = EditorGUILayout.ColorField("Far Color", myTarget.farPointColor);
            myTarget.farPointDistance = EditorGUILayout.FloatField("Far Distance", myTarget.farPointDistance);
            EditorGUILayout.EndToggleGroup();
            
            if (GUI.changed) myTarget.OnValidate();
            
            serializedObject.ApplyModifiedProperties();
        }
    }
}
