#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>
/// Protege o MiniMarketPlayerDatabase durante as transições do Play Mode do Unity 6.
/// Não grava alterações nas cenas e não deixa referências para objetos que foram
/// movidos para DontDestroyOnLoad.
/// </summary>
[InitializeOnLoad]
internal static class PlayerDatabaseEditorLifecycleGuard
{
    private const BindingFlags StaticPrivate = BindingFlags.Static | BindingFlags.NonPublic;
    private const BindingFlags InstanceFields =
        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

    private static readonly FieldInfo ClosingField =
        typeof(MiniMarketPlayerDatabase).GetField("encerrandoAplicacao", StaticPrivate);

    private static readonly Dictionary<string, bool> TriggerDatabaseSync =
        new Dictionary<string, bool>(StringComparer.Ordinal);

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
        MiniMarketPlayerDatabase.ObterOuCriar();
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

            TriggerDatabaseSync[BuildStableObjectKey(trigger.transform)] =
                trigger.sincronizarMarcacaoComStatusDosTerrenos;

            // Apenas em memória; não marca a cena como alterada.
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

            string key = BuildStableObjectKey(trigger.transform);
            if (TriggerDatabaseSync.TryGetValue(key, out bool previous))
                trigger.sincronizarMarcacaoComStatusDosTerrenos = previous;
        }

        TriggerDatabaseSync.Clear();
    }

    private static string BuildStableObjectKey(Transform target)
    {
        if (target == null)
            return string.Empty;

        string hierarchy = target.name;
        Transform current = target.parent;
        while (current != null)
        {
            hierarchy = current.name + "/" + hierarchy;
            current = current.parent;
        }

        string scenePath = target.gameObject.scene.IsValid()
            ? target.gameObject.scene.path
            : string.Empty;

        return scenePath + "|" + hierarchy;
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
                        ClearDatabaseReferencesInMemory(component);
                }
            }
        }
    }

    private static void ClearDatabaseReferencesInMemory(Component component)
    {
        Type type = component.GetType();

        while (type != null && type != typeof(MonoBehaviour) && type != typeof(Behaviour) &&
               type != typeof(Component) && type != typeof(Object))
        {
            FieldInfo[] fields = type.GetFields(InstanceFields);
            for (int i = 0; i < fields.Length; i++)
            {
                FieldInfo field = fields[i];
                if (field.IsStatic || field.IsInitOnly ||
                    !typeof(Object).IsAssignableFrom(field.FieldType))
                {
                    continue;
                }

                try
                {
                    Object referenced = field.GetValue(component) as Object;
                    if (IsRuntimeDatabaseReference(referenced))
                        field.SetValue(component, null);
                }
                catch
                {
                    // Alguns campos internos do Unity não permitem leitura/escrita.
                }
            }

            type = type.BaseType;
        }
    }

    private static bool IsRuntimeDatabaseReference(Object referenced)
    {
        if (referenced == null || referenced is MonoScript)
            return false;

        if (referenced is MiniMarketPlayerDatabase || referenced is PlayerDatabase)
            return true;

        GameObject target = referenced as GameObject;
        if (target == null && referenced is Component component)
            target = component.gameObject;

        return target != null && string.Equals(
            target.name,
            "MiniMarket_PlayerDatabase",
            StringComparison.OrdinalIgnoreCase
        );
    }
}
#endif
