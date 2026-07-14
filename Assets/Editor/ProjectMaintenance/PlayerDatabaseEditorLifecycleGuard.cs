#if UNITY_EDITOR
using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>
/// Protege o PlayerDatabase durante transições do Unity Editor.
///
/// No Unity 6.7 alpha, alguns OnValidate são executados durante o pre-start do Play Mode,
/// quando Application.isPlaying já pode retornar true. Sem esta proteção, os marcadores
/// de compra tentam criar um GameObject DontDestroyOnLoad dentro do OnValidate, gerando
/// SendMessage/OnDidAddComponent e referências inválidas entre cenas.
/// </summary>
[InitializeOnLoad]
internal static class PlayerDatabaseEditorLifecycleGuard
{
    private const BindingFlags StaticPrivate = BindingFlags.Static | BindingFlags.NonPublic;
    private static readonly FieldInfo ClosingField =
        typeof(PlayerDatabase).GetField("encerrandoAplicacao", StaticPrivate);

    static PlayerDatabaseEditorLifecycleGuard()
    {
        EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;

        SetDatabaseCreationBlocked(!EditorApplication.isPlaying);
        EditorApplication.delayCall += ClearInvalidDatabaseReferencesFromOpenScenes;
    }

    private static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        switch (state)
        {
            case PlayModeStateChange.ExitingEditMode:
                SetDatabaseCreationBlocked(true);
                ClearInvalidDatabaseReferencesFromOpenScenes();
                break;

            case PlayModeStateChange.EnteredPlayMode:
                SetDatabaseCreationBlocked(false);
                EditorApplication.delayCall += CreateDatabaseAfterPlayModeStarted;
                break;

            case PlayModeStateChange.ExitingPlayMode:
                SetDatabaseCreationBlocked(true);
                ClearInvalidDatabaseReferencesFromOpenScenes();
                break;

            case PlayModeStateChange.EnteredEditMode:
                SetDatabaseCreationBlocked(true);
                EditorApplication.delayCall += ClearInvalidDatabaseReferencesFromOpenScenes;
                break;
        }
    }

    private static void SetDatabaseCreationBlocked(bool blocked)
    {
        if (ClosingField == null)
            return;

        try
        {
            ClosingField.SetValue(null, blocked);
        }
        catch (Exception exception)
        {
            Debug.LogWarning("[PlayerDatabaseEditorGuard] Falha ao alterar trava do banco: " + exception.Message);
        }
    }

    private static void CreateDatabaseAfterPlayModeStarted()
    {
        if (!EditorApplication.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode == false)
            return;

        SetDatabaseCreationBlocked(false);
        PlayerDatabase.ObterOuCriar();
    }

    private static void ClearInvalidDatabaseReferencesFromOpenScenes()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            return;

        for (int sceneIndex = 0; sceneIndex < SceneManager.sceneCount; sceneIndex++)
        {
            Scene scene = SceneManager.GetSceneAt(sceneIndex);
            if (!scene.IsValid() || !scene.isLoaded)
                continue;

            bool sceneChanged = false;
            GameObject[] roots = scene.GetRootGameObjects();

            for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
            {
                Component[] components = roots[rootIndex].GetComponentsInChildren<Component>(true);
                for (int componentIndex = 0; componentIndex < components.Length; componentIndex++)
                {
                    Component component = components[componentIndex];
                    if (component == null)
                        continue;

                    if (ClearDatabaseReferences(component))
                    {
                        sceneChanged = true;
                        EditorUtility.SetDirty(component);
                    }
                }
            }

            if (sceneChanged && !EditorApplication.isPlaying)
                EditorSceneManager.MarkSceneDirty(scene);
        }
    }

    private static bool ClearDatabaseReferences(Component component)
    {
        SerializedObject serializedObject;
        try
        {
            serializedObject = new SerializedObject(component);
        }
        catch
        {
            return false;
        }

        bool changed = false;
        SerializedProperty iterator = serializedObject.GetIterator();
        bool enterChildren = true;

        while (iterator.NextVisible(enterChildren))
        {
            enterChildren = false;

            if (iterator.propertyType != SerializedPropertyType.ObjectReference ||
                iterator.propertyPath == "m_Script")
            {
                continue;
            }

            Object referenced = iterator.objectReferenceValue;
            if (!IsRuntimeDatabaseReference(referenced))
                continue;

            iterator.objectReferenceValue = null;
            changed = true;
        }

        if (changed)
            serializedObject.ApplyModifiedPropertiesWithoutUndo();

        return changed;
    }

    private static bool IsRuntimeDatabaseReference(Object referenced)
    {
        if (referenced == null || referenced is MonoScript)
            return false;

        if (referenced is PlayerDatabase)
            return true;

        string typeName = referenced.GetType().Name;
        if (typeName.IndexOf("PlayerDatabase", StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        GameObject target = referenced as GameObject;
        if (target == null && referenced is Component component)
            target = component.gameObject;

        return target != null &&
               string.Equals(
                   target.name,
                   "MiniMarket_PlayerDatabase",
                   StringComparison.OrdinalIgnoreCase
               );
    }
}
#endif
