#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>
/// Remove referências serializadas inválidas de objetos de cenas normais para objetos
/// existentes na cena especial DontDestroyOnLoad.
///
/// Segurança:
/// - nunca marca/salva cena enquanto o jogo está em Play Mode;
/// - limpa antes de entrar em Play Mode;
/// - limpa apenas em memória ao sair do Play Mode, antes do Hierarchy salvar o estado;
/// - limpa e salva novamente quando o Editor volta ao Edit Mode;
/// - não executa varreduras concorrentes durante compilação/importação.
/// </summary>
[InitializeOnLoad]
public static class CrossSceneReferenceCleaner
{
    private const double ScanIntervalSeconds = 3d;

    private static double nextScanTime;
    private static bool isCleaning;
    private static bool playTransitionInProgress;

    static CrossSceneReferenceCleaner()
    {
        EditorApplication.update -= EditorUpdate;
        EditorApplication.update += EditorUpdate;

        EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;

        EditorApplication.delayCall += CleanLoadedScenes;
    }

    [MenuItem("Tools/Project Maintenance/Clean Cross-Scene References", priority = 2)]
    public static void CleanLoadedScenes()
    {
        CleanLoadedScenesInternal(saveSceneChanges: true, allowDuringTransition: false);
    }

    private static void EditorUpdate()
    {
        if (isCleaning ||
            playTransitionInProgress ||
            EditorApplication.isPlaying ||
            EditorApplication.isPlayingOrWillChangePlaymode ||
            EditorApplication.isCompiling ||
            EditorApplication.isUpdating)
        {
            return;
        }

        if (EditorApplication.timeSinceStartup < nextScanTime)
            return;

        nextScanTime = EditorApplication.timeSinceStartup + ScanIntervalSeconds;
        CleanLoadedScenesInternal(saveSceneChanges: true, allowDuringTransition: false);
    }

    private static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        switch (state)
        {
            case PlayModeStateChange.ExitingEditMode:
                playTransitionInProgress = true;

                // Ainda estamos no Edit Mode. Limpa e salva antes do Play começar.
                CleanLoadedScenesInternal(saveSceneChanges: true, allowDuringTransition: true);
                break;

            case PlayModeStateChange.EnteredPlayMode:
                playTransitionInProgress = false;
                break;

            case PlayModeStateChange.ExitingPlayMode:
                playTransitionInProgress = true;

                // Durante o Play, apenas desfaz referências runtime em memória.
                // Não chama MarkSceneDirty nem SaveScene.
                CleanLoadedScenesInternal(saveSceneChanges: false, allowDuringTransition: true);
                break;

            case PlayModeStateChange.EnteredEditMode:
                playTransitionInProgress = false;
                nextScanTime = 0d;
                EditorApplication.delayCall += CleanLoadedScenes;
                break;
        }
    }

    private static void CleanLoadedScenesInternal(bool saveSceneChanges, bool allowDuringTransition)
    {
        if (isCleaning || EditorApplication.isCompiling || EditorApplication.isUpdating)
            return;

        if (!allowDuringTransition &&
            (EditorApplication.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode))
        {
            return;
        }

        // Marcar/salvar cenas em Play Mode gera InvalidOperationException.
        if (saveSceneChanges && (EditorApplication.isPlaying || Application.isPlaying))
            saveSceneChanges = false;

        isCleaning = true;
        int cleared = 0;
        int changedScenes = 0;

        try
        {
            for (int sceneIndex = 0; sceneIndex < SceneManager.sceneCount; sceneIndex++)
            {
                Scene scene = SceneManager.GetSceneAt(sceneIndex);
                if (!scene.IsValid() ||
                    !scene.isLoaded ||
                    string.IsNullOrEmpty(scene.path) ||
                    string.Equals(scene.name, "DontDestroyOnLoad", StringComparison.Ordinal))
                {
                    continue;
                }

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

                        cleared += CleanComponent(component, ref sceneChanged);
                    }
                }

                if (!sceneChanged)
                    continue;

                changedScenes++;

                if (saveSceneChanges && !EditorApplication.isPlaying && !Application.isPlaying)
                {
                    EditorSceneManager.MarkSceneDirty(scene);
                    EditorSceneManager.SaveScene(scene);
                }
            }

            if (cleared > 0)
            {
                Debug.Log(
                    "[CrossSceneReferenceCleaner] " + cleared +
                    " referência(s) inválida(s) removida(s) em " + changedScenes +
                    " cena(s). Salvar cenas=" + saveSceneChanges + "."
                );
            }
        }
        catch (InvalidOperationException exception)
        {
            // Proteção adicional para mudanças de estado do Editor ocorridas no mesmo frame.
            Debug.LogWarning(
                "[CrossSceneReferenceCleaner] Limpeza adiada durante transição do Play Mode: " +
                exception.Message
            );
        }
        catch (Exception exception)
        {
            Debug.LogError("[CrossSceneReferenceCleaner] Falha ao limpar referências: " + exception);
        }
        finally
        {
            isCleaning = false;
        }
    }

    private static int CleanComponent(Component owner, ref bool sceneChanged)
    {
        if (owner == null)
            return 0;

        Scene ownerScene = owner.gameObject.scene;
        if (!ownerScene.IsValid() ||
            string.Equals(ownerScene.name, "DontDestroyOnLoad", StringComparison.Ordinal))
        {
            return 0;
        }

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
            if (reference == null || !TryGetScene(reference, out Scene referenceScene))
                continue;

            bool pointsToRuntimeScene = referenceScene.IsValid() &&
                                        string.Equals(
                                            referenceScene.name,
                                            "DontDestroyOnLoad",
                                            StringComparison.Ordinal
                                        );

            if (!pointsToRuntimeScene)
                continue;

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
