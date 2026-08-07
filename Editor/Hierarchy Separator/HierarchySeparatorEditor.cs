using UnityEditor;
using UnityEngine;

namespace PauloAragao.Utils
{
    [InitializeOnLoad]
    public class HierarchySeparatorEditor
    {
        static HierarchySeparatorEditor()
        {
            EditorApplication.hierarchyWindowItemOnGUI += OnHierarchyGUI;
        }

        static void OnHierarchyGUI(int instanceID, Rect selectionRect)
        {
            GameObject obj = EditorUtility.InstanceIDToObject(instanceID) as GameObject;
            if (obj == null) return;
            HierarchySeparator separator = obj.GetComponent<HierarchySeparator>();
            if (separator != null)
            {
                EditorGUI.DrawRect(selectionRect, separator.separatorColor);
                GUIStyle style = new GUIStyle(EditorStyles.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontStyle = FontStyle.Bold,
                    normal = { textColor = separator.textColor } 
                };
                EditorGUI.LabelField(selectionRect, obj.name, style);
            }
        }
    } 
}
