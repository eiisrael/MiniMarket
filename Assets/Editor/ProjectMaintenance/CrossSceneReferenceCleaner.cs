#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>
/// Remove referências serializadas inválidas de cenas normais para objetos da cena
/// especial DontDestroyOnLoad.
///
/// A limpeza é manual, registra Undo e deixa a cena marcada como alterada para revisão.
/// Nunca salva uma cena automaticamente.
/// </summary>
public static class CrossSceneReferenceCleaner
{
    private static bool isCleaning;

    [MenuItem("Tools/Project Maintenance/Clean Cross-Scene References", priority = 2)]
    public static void CleanLoadedScenes()
    {
        if (!CanModifyScenes())
        {
            Debug.LogWarning(
                "[CrossSceneReferenceCleaner] Saia do Play Mode e aguarde a compilação antes de limpar referências."
            );
            return;
        }

        CleanLoadedScenesInternal();
    }

    private static bool CanModifyScenes()
    {
        return !EditorApplication.isPlaying &&
               !EditorApplication.isPlayingOrWillChangePlaymode &&
               !Application.isPlaying &&
               !EditorApplication.isCompiling &&
               !EditorApplication.isUpdating;
    }

    private static void CleanLoadedScenesInternal()
    {
        if (!CanModifyScenes() || isCleaning)
            return;

        isCleaning = true;
        int clearedReferences = 0;
        int changedScenes = 0;

        try
        {
            for (int sceneIndex = 0; sceneIndex < SceneManager.sceneCount; sceneIndex++)
            {
                Scene scene = SceneManager.GetSceneAt(sceneIndex);
                if (!IsEditableScene(scene))
                    continue;

                bool sceneChanged = false;
                GameObject[] roots = scene.GetRootGameObjects();

                for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
                {
                    GameObject root = roots[rootIndex];
                    if (root == null)
                        continue;

                    Component[] components = root.GetComponentsInChildren<Component>(true);
                    for (int componentIndex = 0; componentIndex < components.Length; componentIndex++)
                    {
                        Component component = components[componentIndex];
                        if (component == null || component is Transform)
                            continue;

                        clearedReferences += CleanComponent(component, ref sceneChanged);
                    }
                }

                if (!sceneChanged)
                    continue;

                changedScenes++;

                if (CanModifyScenes())
                    EditorSceneManager.MarkSceneDirty(scene);
            }

            if (clearedReferences > 0)
            {
                Debug.Log(
                    "[CrossSceneReferenceCleaner] " + clearedReferences +
                    " referência(s) inválida(s) removida(s) em " + changedScenes +
                    " cena(s). Revise as alterações e salve manualmente."
                );
            }
        }
        catch (InvalidOperationException exception)
        {
            // Caso o estado do Editor mude no mesmo frame, a limpeza será tentada
            // novamente quando o Edit Mode estiver estável.
            Debug.LogWarning(
                "[CrossSceneReferenceCleaner] Limpeza adiada: " + exception.Message
            );
        }
        catch (Exception exception)
        {
            Debug.LogError(
                "[CrossSceneReferenceCleaner] Falha ao limpar referências: " + exception
            );
        }
        finally
        {
            isCleaning = false;
        }
    }

    private static bool IsEditableScene(Scene scene)
    {
        return scene.IsValid() &&
               scene.isLoaded &&
               !string.IsNullOrEmpty(scene.path) &&
               !string.Equals(scene.name, "DontDestroyOnLoad", StringComparison.Ordinal);
    }

    private static int CleanComponent(Component owner, ref bool sceneChanged)
    {
        if (owner == null)
            return 0;

        Scene ownerScene = owner.gameObject.scene;
        if (!IsEditableScene(ownerScene))
            return 0;

        SerializedObject serializedObject;
        try
        {
            serializedObject = new SerializedObject(owner);
            serializedObject.UpdateIfRequiredOrScript();
        }
        catch
        {
            return 0;
        }

        int cleared = 0;
        SerializedProperty iterator = serializedObject.GetIterator();
        bool enterChildren = true;

        while (iterator.NextVisible(enterChildren))
        {
            enterChildren = true;

            if (iterator.propertyType != SerializedPropertyType.ObjectReference ||
                iterator.propertyPath == "m_Script")
            {
                continue;
            }

            Object reference = iterator.objectReferenceValue;
            if (reference == null)
                continue;

            if (!TryGetScene(reference, out Scene referenceScene))
                continue;

            if (!referenceScene.IsValid() ||
                !string.Equals(referenceScene.name, "DontDestroyOnLoad", StringComparison.Ordinal))
            {
                continue;
            }

            if (cleared == 0)
                Undo.RecordObject(owner, "Clean cross-scene references");

            iterator.objectReferenceValue = null;
            cleared++;
            sceneChanged = true;
        }

        if (cleared > 0)
        {
            serializedObject.ApplyModifiedProperties();

            if (PrefabUtility.IsPartOfPrefabInstance(owner))
                PrefabUtility.RecordPrefabInstancePropertyModifications(owner);
        }

        return cleared;
    }

    private static bool TryGetScene(Object target, out Scene scene)
    {
        scene = default;

        if (target is GameObject gameObject)
        {
            scene = gameObject.scene;
            return true;
        }

        if (target is Component component)
        {
            scene = component.gameObject.scene;
            return true;
        }

        return false;
    }
}
#endif
