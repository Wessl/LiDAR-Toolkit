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
            DrawPropertiesExcluding(serializedObject, new string[]{ "pointColor", "overrideColor", "useColorGradient", "farPointColor", "farPointDistance", "fadePointsOverTime", "fadeTime" });

            myTarget.fadePointsOverTime = EditorGUILayout.BeginToggleGroup("Fade out points over time", myTarget.fadePointsOverTime);
            myTarget.fadeTime = EditorGUILayout.FloatField("Fade out time", myTarget.fadeTime);
            EditorGUILayout.EndToggleGroup();
            EditorGUILayout.Separator();
            if(GUILayout.Button("Clear All Points"))
            {
                myTarget.ClearAllPoints();
            }
           

            myTarget.useColorGradient = EditorGUILayout.BeginToggleGroup("Use color gradient for point distance", myTarget.useColorGradient);
            myTarget.farPointColor = EditorGUILayout.ColorField("Far Color", myTarget.farPointColor);
            myTarget.farPointDistance = EditorGUILayout.FloatField("Far Distance", myTarget.farPointDistance);
            EditorGUILayout.EndToggleGroup();

            if (GUI.changed) myTarget.OnValidate();
            
            serializedObject.ApplyModifiedProperties();
        }
    }
}
