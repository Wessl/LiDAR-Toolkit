using UnityEditor;

namespace Editor
{
    [CustomEditor(typeof(DrawPoints))]
    public class CustomInspector_DrawPoints : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            // DrawDefaultInspector();
            
            DrawPoints myTarget = (DrawPoints)target;
            
            // Default stuff, except color
            DrawPropertiesExcluding(serializedObject, new string[]{ "pointColor", "overrideColor" });


            EditorGUILayout.Separator();
            myTarget.overrideColor = EditorGUILayout.BeginToggleGroup("Should material color be overwritten", myTarget.overrideColor);
            myTarget.pointColor = EditorGUILayout.ColorField("Color", myTarget.pointColor);
            EditorGUILayout.EndToggleGroup();
            
        }
    }
}
