#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>
/// Protege o PlayerDatabase durante as transições do Play Mode do Unity 6.
/// Não grava alterações nas cenas e não deixa referências para objetos que foram
/// movidos para DontDestroyOnLoad.
/// </summary>
[InitializeOnLoad]
internal static class PlayerDatabaseEditorLifecycleGuard
{
    private const BindingFlags StaticPrivate = BindingFlags.Static | BindingFlags.NonPublic;

    private static readonly FieldInfo ClosingField =
        typeof(PlayerDatabase).GetField("encerrandoAplicacao", StaticPrivate);

    private static readonly Dictionary<int, bool> TriggerDatabaseSync =
        new Dictionary<int, bool>();

    static PlayerDatabaseEditorLifecycleGuard()
    {
        EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;

        SetDatabaseCreationBlocked(!EditorApplication.isPlaying);
    }

    private static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        switch (state)
        {
            case PlayModeStateChange.ExitingEditMode:
                // OnValidate ainda pode ser executado durante esta etapa.
                SetDatabaseCreationBlocked(true);
                SuspendPurchaseDatabaseSync();
                ClearRuntimeDatabaseReferencesFromOpenScenes();
                break;

            case PlayModeStateChange.EnteredPlayMode:
                SetDatabaseCreationBlocked(false);
                RestorePurchaseDatabaseSync();
                EditorApplication.delayCall += CreateDatabaseAfterPlayModeStarted;
                break;

            case PlayModeStateChange.ExitingPlayMode:
                SetDatabaseCreationBlocked(true);
                ClearRuntimeDatabaseReferencesFromOpenScenes();
                break;

            case PlayModeStateChange.EnteredEditMode:
                SetDatabaseCreationBlocked(true);
                RestorePurchaseDatabaseSync();
                ClearRuntimeDatabaseReferencesFromOpenScenes();
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
            Debug.LogWarning(
                "[PlayerDatabaseEditorGuard] Falha ao alterar a trava do banco: " +
                exception.Message
            );
        }
    }

    private static void CreateDatabaseAfterPlayModeStarted()
    {
        if (!EditorApplication.isPlaying)
            return;

        SetDatabaseCreationBlocked(false);
        PlayerDatabase.ObterOuCriar();
    }

    private static void SuspendPurchaseDatabaseSync()
    {
        TriggerDatabaseSync.Clear();

        BuySceneEntryTrigger[] triggers =
            Object.FindObjectsByType<BuySceneEntryTrigger>(FindObjectsInactive.Include);

        for (int i = 0; i < triggers.Length; i++)
        {
            BuySceneEntryTrigger trigger = triggers[i];
            if (trigger == null || !trigger.gameObject.scene.IsValid())
                continue;

            int id = trigger.GetInstanceID();
            TriggerDatabaseSync[id] = trigger.sincronizarMarcacaoComStatusDosTerrenos;

            // Atribuição apenas em memória. Não usa SetDirty e não altera o arquivo da cena.
            trigger.sincronizarMarcacaoComStatusDosTerrenos = false;
        }
    }

    private static void RestorePurchaseDatabaseSync()
    {
        if (TriggerDatabaseSync.Count == 0)
            return;

        BuySceneEntryTrigger[] triggers =
            Object.FindObjectsByType<BuySceneEntryTrigger>(FindObjectsInactive.Include);

        for (int i = 0; i < triggers.Length; i++)
        {
            BuySceneEntryTrigger trigger = triggers[i];
            if (trigger == null)
                continue;

            if (TriggerDatabaseSync.TryGetValue(trigger.GetInstanceID(), out bool previous))
                trigger.sincronizarMarcacaoComStatusDosTerrenos = previous;
        }

        TriggerDatabaseSync.Clear();
    }

    private static void ClearRuntimeDatabaseReferencesFromOpenScenes()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            return;

        for (int sceneIndex = 0; sceneIndex < SceneManager.sceneCount; sceneIndex++)
        {
            Scene scene = SceneManager.GetSceneAt(sceneIndex);
            if (!scene.IsValid() || !scene.isLoaded)
                continue;

            GameObject[] roots = scene.GetRootGameObjects();
            for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
            {
                Component[] components = roots[rootIndex].GetComponentsInChildren<Component>(true);
                for (int componentIndex = 0; componentIndex < components.Length; componentIndex++)
                {
                    Component component = components[componentIndex];
                    if (component != null)
                        ClearDatabaseReferences(component);
                }
            }
        }
    }

    private static void ClearDatabaseReferences(Component component)
    {
        SerializedObject serializedObject;
        try
        {
            serializedObject = new SerializedObject(component);
        }
        catch
        {
            return;
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

        // Intencionalmente não chama EditorUtility.SetDirty/MarkSceneDirty.
        // A limpeza vale para a transição atual sem reabrir a cena como modificada.
        if (changed)
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
    }

    private static bool IsRuntimeDatabaseReference(Object referenced)
    {
        if (referenced == null || referenced is MonoScript)
            return false;

        if (referenced is PlayerDatabase)
            return true;

        GameObject target = referenced as GameObject;
        if (target == null && referenced is Component component)
            target = component.gameObject;

        if (target == null)
            return false;

        return string.Equals(
            target.name,
            "MiniMarket_PlayerDatabase",
            StringComparison.OrdinalIgnoreCase
        );
    }
}
#endif