using UnityEditor;
using UnityEngine;

namespace PauloAragao.Utils
{
    public static class HierarchySeparatorMenu
    {
        [MenuItem("GameObject/Create Hierarchy Separator", false, 10)]
        public static void CreateHierarchySeparator(MenuCommand menuCommand)
        {
            GameObject separatorGO = new GameObject("New Separator");
            separatorGO.AddComponent<HierarchySeparator>();
            GameObjectUtility.SetParentAndAlign(separatorGO, menuCommand.context as GameObject);
            Undo.RegisterCreatedObjectUndo(separatorGO, "Hierarchy Separator");
            Selection.activeObject = separatorGO;
        }
    }  
}
