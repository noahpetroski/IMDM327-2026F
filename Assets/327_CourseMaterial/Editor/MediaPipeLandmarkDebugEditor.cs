using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(MediaPipeLandmarkDebug))]
public class MediaPipeLandmarkDebugEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        serializedObject.Update();
        SerializedProperty debugMode = serializedObject.FindProperty("debugMode");
        string caption = "Show landmarks";
        if (debugMode.boolValue) caption = "Hide landmarks";
        if (GUILayout.Button(caption))
        {
            debugMode.boolValue = !debugMode.boolValue;
            serializedObject.ApplyModifiedProperties();
        }
    }
}
