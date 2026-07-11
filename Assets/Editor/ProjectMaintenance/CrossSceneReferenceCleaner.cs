#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>
/// Remove referências serializadas de objetos de cena para objetos runtime da cena
/// DontDestroyOnLoad. Essas referências não podem ser salvas pelo Unity e causam o warning
/// "Cross scene references are not supported".
///
/// A limpeza ocorre no Edit Mode e antes de entrar no Play Mode. Referências runtime devem
/// ser resolvidas por singleton/busca durante Awake/OnEnable, nunca armazenadas na cena.
/// </summary>
[InitializeOnLoad]
public static class CrossSceneReferenceCleaner
{
    private const double ScanIntervalSeconds = 0.75d;
    private static double nextScanTime;
    private static bool isCleaning;

    static CrossSceneReferenceCleaner()
    {
        EditorApplication.update += EditorUpdate;
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        EditorApplication.delayCall += CleanLoadedScenes;
    }

    [MenuItem("Tools/Project Maintenance/Clean Cross-Scene References", priority = 2)]
    public static void CleanLoadedScenes()
    {
        if (isCleaning || EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        isCleaning = true;
        int cleared = 0;

        try
        {
            for (int sceneIndex = 0; sceneIndex < SceneManager.sceneCount; sceneIndex++)
            {
                Scene scene = SceneManager.GetSceneAt(sceneIndex);
                if (!scene.IsValid() || !scene.isLoaded || string.IsNullOrEmpty(scene.path))
                    continue;

                bool sceneChanged = false;
                GameObject[] roots = scene.GetRootGameObjects();

                for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
                {
                    Component[] components = roots[rootIndex].GetComponentsInChildren<Component>(true);
                    for (int componentIndex = 0; componentIndex < components.Length; componentIndex++)
                    {
                        Component component = components[componentIndex];
                        if (component == null || component is Transform)
                            continue;

                        cleared += CleanComponent(component, scene, ref sceneChanged);
                    }
                }

                if (sceneChanged)
                {
                    EditorSceneManager.MarkSceneDirty(scene);
                    EditorSceneManager.SaveScene(scene);
                }
            }

            if (cleared > 0)
            {
                Debug.Log(
                    "[CrossSceneReferenceCleaner] " + cleared +
                    " referência(s) inválida(s) para DontDestroyOnLoad foram removidas e as cenas foram salvas."
                );
            }
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

    private static void EditorUpdate()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
            return;

        if (EditorApplication.timeSinceStartup < nextScanTime)
            return;

        nextScanTime = EditorApplication.timeSinceStartup + ScanIntervalSeconds;
        CleanLoadedScenes();
    }

    private static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredEditMode)
        {
            nextScanTime = 0d;
            EditorApplication.delayCall += CleanLoadedScenes;
        }
    }

    private static int CleanComponent(Component owner, Scene ownerScene, ref bool sceneChanged)
    {
        int cleared = 0;
        SerializedObject serializedObject;

        try
        {
            serializedObject = new SerializedObject(owner);
        }
        catch
        {
            return 0;
        }

        SerializedProperty iterator = serializedObject.GetIterator();
        bool enterChildren = true;

        while (iterator.NextVisible(enterChildren))
        {
            enterChildren = true;

            if (iterator.propertyType != SerializedPropertyType.ObjectReference)
                continue;

            Object reference = iterator.objectReferenceValue;
            if (reference == null || !TryGetScene(reference, out Scene referenceScene))
                continue;

            if (!referenceScene.IsValid() || referenceScene == ownerScene)
                continue;

            bool runtimeScene = string.Equals(
                referenceScene.name,
                "DontDestroyOnLoad",
                StringComparison.Ordinal
            );

            bool differentLoadedScene = referenceScene.isLoaded && ownerScene.isLoaded &&
                                        !string.Equals(referenceScene.path, ownerScene.path, StringComparison.Ordinal);

            if (!runtimeScene && !differentLoadedScene)
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
