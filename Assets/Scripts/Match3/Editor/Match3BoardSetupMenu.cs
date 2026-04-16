#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class Match3BoardSetupMenu
{
    [MenuItem("Tools/Match3/Create Match3 Board In Active Scene")]
    public static void CreateMatch3Board()
    {
        if (Object.FindAnyObjectByType<Match3Manager>() != null)
        {
            Debug.Log("[Match3] Match3Manager already exists in the scene.");
            return;
        }

        var go = new GameObject("Match3Board");
        Undo.RegisterCreatedObjectUndo(go, "Create Match3Board");
        go.AddComponent<Match3Manager>();
        Selection.activeGameObject = go;
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log("[Match3] Created Match3Board with Match3Manager. Assign 5 gem prefabs on the component.");
    }
}
#endif
