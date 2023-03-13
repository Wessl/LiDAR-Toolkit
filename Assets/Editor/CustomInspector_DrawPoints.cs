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
            if(GUILayout.Button("Clear All Points"))
            {
                myTarget.ClearAllPoints();
            }
            myTarget.overrideColor = EditorGUILayout.BeginToggleGroup(new GUIContent("Override material color", "Turning this on will improve performance quite a bit while scanning with very high fire rate, since it won't need to poll for the material's color anymore."), myTarget.overrideColor);
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
