#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Remove de componentes salvos na cena referências serializadas para o banco runtime
/// que vive em DontDestroyOnLoad. O banco deve ser resolvido pelo singleton durante o
/// gameplay; uma referência entre essas cenas não é suportada pelo Unity.
/// </summary>
[InitializeOnLoad]
internal static class PlayerDatabaseCrossSceneReferenceGuard
{
    private static bool repairScheduled;

    static PlayerDatabaseCrossSceneReferenceGuard()
    {
        EditorApplication.playModeStateChanged += HandlePlayModeStateChanged;
        ScheduleRepair();
    }

    private static void HandlePlayModeStateChanged(PlayModeStateChange state)
    {
        switch (state)
        {
            case PlayModeStateChange.ExitingEditMode:
                ClearInvalidReferences(false, false);
                break;

            case PlayModeStateChange.EnteredEditMode:
                ScheduleRepair();
                break;
        }
    }

    private static void ScheduleRepair()
    {
        if (repairScheduled)
            return;

        repairScheduled = true;
        EditorApplication.delayCall += RunScheduledRepair;
    }

    private static void RunScheduledRepair()
    {
        repairScheduled = false;

        if (EditorApplication.isPlayingOrWillChangePlaymode ||
            EditorApplication.isCompiling ||
            EditorApplication.isUpdating)
        {
            ScheduleRepair();
            return;
        }

        ClearInvalidReferences(true, true);
    }

    [MenuItem("Tools/MiniMarket/Banco/Limpar Referências Cross-Scene do Banco", priority = 2580)]
    private static void RepairFromMenu()
    {
        ClearInvalidReferences(true, true);
    }

    private static void ClearInvalidReferences(bool markSceneDirty, bool logResult)
    {
        MonoBehaviour[] behaviours = Object.FindObjectsByType<MonoBehaviour>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None
        );

        int clearedCount = 0;

        for (int i = 0; i < behaviours.Length; i++)
        {
            MonoBehaviour behaviour = behaviours[i];
            if (behaviour == null)
                continue;

            Scene ownerScene = behaviour.gameObject.scene;
            if (!ownerScene.IsValid() || !ownerScene.isLoaded ||
                ownerScene.name == "DontDestroyOnLoad")
            {
                continue;
            }

            SerializedObject serializedObject;

            try
            {
                serializedObject = new SerializedObject(behaviour);
            }
            catch
            {
                continue;
            }

            SerializedProperty property = serializedObject.GetIterator();
            bool enterChildren = true;
            bool componentChanged = false;

            while (property.Next(enterChildren))
            {
                enterChildren = false;

                if (property.propertyType != SerializedPropertyType.ObjectReference)
                    continue;

                Object reference = property.objectReferenceValue;
                if (!IsInvalidRuntimeDatabaseReference(reference, ownerScene))
                    continue;

                property.objectReferenceValue = null;
                componentChanged = true;
                clearedCount++;
            }

            if (!componentChanged)
                continue;

            serializedObject.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(behaviour);

            if (markSceneDirty)
                EditorSceneManager.MarkSceneDirty(ownerScene);
        }

        if (logResult && clearedCount > 0)
        {
            Debug.Log(
                "[PlayerDatabase] Referências cross-scene removidas: " + clearedCount +
                ". O singleton será resolvido novamente durante o Play. Use Ctrl+S uma vez."
            );
        }
    }

    private static bool IsInvalidRuntimeDatabaseReference(
        Object reference,
        Scene ownerScene)
    {
        if (reference == null)
            return false;

        GameObject targetObject = ResolveGameObject(reference);
        if (targetObject == null)
            return false;

        Scene targetScene = targetObject.scene;
        if (targetScene == ownerScene)
            return false;

        bool runtimeScene = targetScene.IsValid() &&
                            targetScene.name == "DontDestroyOnLoad";

        if (!runtimeScene)
            return false;

        if (targetObject.name == "MiniMarket_PlayerDatabase")
            return true;

        Component component = reference as Component;
        string typeName = component != null
            ? component.GetType().Name
            : string.Empty;

        return typeName == "PlayerDatabase" ||
               typeName == "MiniMarketPlayerDatabase" ||
               targetObject.GetComponent("PlayerDatabase") != null ||
               targetObject.GetComponent("MiniMarketPlayerDatabase") != null;
    }

    private static GameObject ResolveGameObject(Object reference)
    {
        GameObject gameObject = reference as GameObject;
        if (gameObject != null)
            return gameObject;

        Component component = reference as Component;
        return component != null ? component.gameObject : null;
    }
}
#endif
