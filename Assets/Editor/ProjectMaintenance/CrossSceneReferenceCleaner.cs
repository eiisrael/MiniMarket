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
/// Esta versão nunca modifica, marca ou salva cenas durante Play Mode ou durante a
/// transição para Play Mode. A limpeza acontece somente em Edit Mode.
/// </summary>
[InitializeOnLoad]
public static class CrossSceneReferenceCleaner
{
    private const double ScanIntervalSeconds = 2d;

    private static bool isCleaning;
    private static double nextScanTime;

    static CrossSceneReferenceCleaner()
    {
        EditorApplication.update -= EditorUpdate;
        EditorApplication.update += EditorUpdate;

        EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;

        EditorApplication.delayCall += TryCleanWhenSafe;
    }

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

        CleanLoadedScenesInternal(saveSceneChanges: true);
    }

    private static void EditorUpdate()
    {
        if (!CanModifyScenes() || isCleaning)
            return;

        if (EditorApplication.timeSinceStartup < nextScanTime)
            return;

        nextScanTime = EditorApplication.timeSinceStartup + ScanIntervalSeconds;
        CleanLoadedScenesInternal(saveSceneChanges: true);
    }

    private static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        switch (state)
        {
            case PlayModeStateChange.ExitingEditMode:
            case PlayModeStateChange.EnteredPlayMode:
            case PlayModeStateChange.ExitingPlayMode:
                // Não tocar em SerializedObject, MarkSceneDirty ou SaveScene durante
                // qualquer etapa do Play Mode.
                break;

            case PlayModeStateChange.EnteredEditMode:
                nextScanTime = 0d;
                EditorApplication.delayCall += TryCleanWhenSafe;
                break;
        }
    }

    private static void TryCleanWhenSafe()
    {
        if (!CanModifyScenes() || isCleaning)
            return;

        CleanLoadedScenesInternal(saveSceneChanges: true);
    }

    private static bool CanModifyScenes()
    {
        return !EditorApplication.isPlaying &&
               !EditorApplication.isPlayingOrWillChangePlaymode &&
               !Application.isPlaying &&
               !EditorApplication.isCompiling &&
               !EditorApplication.isUpdating;
    }

    private static void CleanLoadedScenesInternal(bool saveSceneChanges)
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

                if (saveSceneChanges && CanModifyScenes())
                {
                    EditorSceneManager.MarkSceneDirty(scene);
                    EditorSceneManager.SaveScene(scene);
                }
            }

            if (clearedReferences > 0)
            {
                Debug.Log(
                    "[CrossSceneReferenceCleaner] " + clearedReferences +
                    " referência(s) inválida(s) removida(s) em " + changedScenes +
                    " cena(s)."
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

            iterator.objectReferenceValue = null;
            cleared++;
            sceneChanged = true;
        }

        if (cleared > 0)
            serializedObject.ApplyModifiedPropertiesWithoutUndo();

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
