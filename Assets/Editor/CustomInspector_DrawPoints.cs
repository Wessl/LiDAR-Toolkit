using UnityEditor;

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
            DrawPropertiesExcluding(serializedObject, new string[]{ "pointColor", "overrideColor" });


            EditorGUILayout.Separator();
            myTarget.overrideColor = EditorGUILayout.BeginToggleGroup("Override material color", myTarget.overrideColor);
            myTarget.pointColor = EditorGUILayout.ColorField("Color", myTarget.pointColor);
            EditorGUILayout.EndToggleGroup();
            
            serializedObject.ApplyModifiedProperties();
        }
    }
}