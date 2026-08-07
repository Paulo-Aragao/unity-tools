using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(Transform))]
[CanEditMultipleObjects]
public class TransformResetEditor : Editor
{
    SerializedProperty _posProp;
    SerializedProperty _scaleProp;

    void OnEnable()
    {
        _posProp   = serializedObject.FindProperty("m_LocalPosition");
        _scaleProp = serializedObject.FindProperty("m_LocalScale");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.PropertyField(_posProp, new GUIContent("Position"));
        if (GUILayout.Button("Reset", GUILayout.MaxWidth(50)))
        {
            _posProp.vector3Value = Vector3.zero;
        }
        EditorGUILayout.EndHorizontal();
        
        EditorGUILayout.BeginHorizontal();
     
        var t = (Transform)target;
        Vector3 rot = t.localEulerAngles;
        Vector3 newRot = EditorGUILayout.Vector3Field("Rotation", rot);
        if (GUILayout.Button("Reset", GUILayout.MaxWidth(50)))
        {
            newRot = Vector3.zero;
        }
        if (newRot != rot)
        {
            Undo.RecordObject(t, "Change Rotation");
            t.localEulerAngles = newRot;
            EditorUtility.SetDirty(t);
        }
        EditorGUILayout.EndHorizontal();
        
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.PropertyField(_scaleProp, new GUIContent("Scale"));
        if (GUILayout.Button("Reset", GUILayout.MaxWidth(50)))
        {
            _scaleProp.vector3Value = Vector3.one;
        }
        EditorGUILayout.EndHorizontal();

        serializedObject.ApplyModifiedProperties();
    }
}