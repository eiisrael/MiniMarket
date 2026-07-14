#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Faz as edições manuais realizadas durante o Play Mode virarem os valores reais
/// da cena quando o Editor volta para Stop.
///
/// O sistema observa somente modificações registradas pelo Undo/Inspector/Gizmos em
/// objetos já existentes da estrutura do jornal. Alterações automáticas do gameplay
/// não são copiadas, porque não passam pelo Undo do Editor.
///
/// Abrange Transform/RectTransform e propriedades serializadas de todos os componentes
/// existentes sob Newspaper_Stand, Jornal_Place, Put_Area, prompts e jornal colocado.
/// A cena é marcada como modificada, mas não é salva automaticamente.
/// </summary>
[InitializeOnLoad]
internal static class NewspaperPlayModeHierarchyPersistence
{
    private const string ModificationSessionKey =
        "MiniMarket.Newspaper.PlayModeInspectorModifications.v2";

    private const string BaselineScaleSessionKey =
        "MiniMarket.Newspaper.PlacePromptScaleBeforePlay.v2";

    private static readonly Dictionary<string, RecordedModification> PendingModifications =
        new Dictionary<string, RecordedModification>(StringComparer.Ordinal);

    private static readonly Dictionary<int, TargetLocator> OriginalTargetLocators =
        new Dictionary<int, TargetLocator>();

    private static readonly FieldInfo LegacyRepairAppliedField =
        typeof(NewspaperWorldPromptVisual).GetField(
            "legacyPlacementRepairApplied",
            BindingFlags.Instance | BindingFlags.NonPublic
        );

    private static bool applyingRecordedChanges;

    [Serializable]
    private sealed class ModificationCollection
    {
        public List<RecordedModification> items = new List<RecordedModification>();
    }

    [Serializable]
    private sealed class ScaleSnapshotCollection
    {
        public List<ScaleSnapshot> items = new List<ScaleSnapshot>();
    }

    [Serializable]
    private sealed class RecordedModification
    {
        public TargetLocator target;
        public string propertyPath;
        public string value;
        public bool isObjectReference;
        public bool objectReferenceIsNull;
        public AssetReference assetReference;
        public TargetLocator sceneObjectReference;
    }

    [Serializable]
    private sealed class ScaleSnapshot
    {
        public TargetLocator target;
        public Vector3 localScale;
    }

    [Serializable]
    private sealed class TargetLocator
    {
        public string scenePath;
        public string siblingIndexPath;
        public bool targetsGameObject;
        public string componentAssemblyQualifiedName;
        public string componentFullName;
        public int componentIndex;
    }

    [Serializable]
    private sealed class AssetReference
    {
        public string guid;
        public long localFileId;
    }

    static NewspaperPlayModeHierarchyPersistence()
    {
        Undo.postprocessModifications -= HandlePostprocessModifications;
        Undo.postprocessModifications += HandlePostprocessModifications;

        EditorApplication.playModeStateChanged -= HandlePlayModeStateChanged;
        EditorApplication.playModeStateChanged += HandlePlayModeStateChanged;

        EditorApplication.delayCall += DisableLegacyPlacementRepairInLoadedScenes;
    }

    private static UndoPropertyModification[] HandlePostprocessModifications(
        UndoPropertyModification[] modifications)
    {
        if (applyingRecordedChanges ||
            !EditorApplication.isPlaying ||
            modifications == null)
        {
            return modifications;
        }

        for (int i = 0; i < modifications.Length; i++)
        {
            PropertyModification current = modifications[i].currentValue;
            if (current == null || current.target == null)
                continue;

            if (!TryGetTargetGameObject(current.target, out GameObject targetObject) ||
                !IsInsidePersistentNewspaperHierarchy(targetObject.transform))
            {
                continue;
            }

            if (string.IsNullOrEmpty(current.propertyPath) ||
                string.Equals(current.propertyPath, "m_Script", StringComparison.Ordinal))
            {
                continue;
            }

            TargetLocator targetLocator = GetOriginalOrCurrentLocator(current.target);
            if (targetLocator == null)
                continue;

            RecordedModification record = new RecordedModification
            {
                target = targetLocator,
                propertyPath = current.propertyPath,
                value = current.value ?? string.Empty
            };

            if (!CaptureObjectReferenceIfNeeded(current, record))
                continue;

            PendingModifications[BuildModificationKey(record)] = record;
        }

        return modifications;
    }

    private static void HandlePlayModeStateChanged(PlayModeStateChange state)
    {
        switch (state)
        {
            case PlayModeStateChange.ExitingEditMode:
                PendingModifications.Clear();
                OriginalTargetLocators.Clear();
                SessionState.EraseString(ModificationSessionKey);
                CapturePlacementPromptScaleBaseline();
                DisableLegacyPlacementRepairInLoadedScenes();
                break;

            case PlayModeStateChange.EnteredPlayMode:
                BuildOriginalTargetLocatorCache();
                DisableLegacyPlacementRepairInLoadedScenes();
                break;

            case PlayModeStateChange.ExitingPlayMode:
                StorePendingModifications();
                break;

            case PlayModeStateChange.EnteredEditMode:
                EditorApplication.delayCall += RestoreAfterPlayMode;
                break;
        }
    }

    private static void RestoreAfterPlayMode()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        applyingRecordedChanges = true;

        try
        {
            DisableLegacyPlacementRepairInLoadedScenes();
            RestorePlacementPromptScaleBaseline();
            ApplyStoredModifications();
        }
        finally
        {
            applyingRecordedChanges = false;
            PendingModifications.Clear();
            OriginalTargetLocators.Clear();
        }
    }

    private static void BuildOriginalTargetLocatorCache()
    {
        OriginalTargetLocators.Clear();

        Transform[] transforms = UnityEngine.Object.FindObjectsByType<Transform>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None
        );

        for (int i = 0; i < transforms.Length; i++)
        {
            Transform transform = transforms[i];
            if (transform == null || !IsInsidePersistentNewspaperHierarchy(transform))
                continue;

            CacheLocator(transform.gameObject);

            Component[] components = transform.GetComponents<Component>();
            for (int componentIndex = 0; componentIndex < components.Length; componentIndex++)
                CacheLocator(components[componentIndex]);
        }
    }

    private static void CacheLocator(UnityEngine.Object target)
    {
        if (target == null || OriginalTargetLocators.ContainsKey(target.GetInstanceID()))
            return;

        if (TryCreateTargetLocator(target, out TargetLocator locator))
            OriginalTargetLocators[target.GetInstanceID()] = locator;
    }

    private static TargetLocator GetOriginalOrCurrentLocator(UnityEngine.Object target)
    {
        if (target == null)
            return null;

        if (OriginalTargetLocators.TryGetValue(target.GetInstanceID(), out TargetLocator cached))
            return CloneLocator(cached);

        return TryCreateTargetLocator(target, out TargetLocator current)
            ? current
            : null;
    }

    private static void StorePendingModifications()
    {
        ModificationCollection collection = new ModificationCollection();

        foreach (KeyValuePair<string, RecordedModification> pair in PendingModifications)
            collection.items.Add(pair.Value);

        if (collection.items.Count == 0)
        {
            SessionState.EraseString(ModificationSessionKey);
            return;
        }

        SessionState.SetString(
            ModificationSessionKey,
            JsonUtility.ToJson(collection)
        );
    }

    private static void ApplyStoredModifications()
    {
        string json = SessionState.GetString(ModificationSessionKey, string.Empty);
        SessionState.EraseString(ModificationSessionKey);

        if (string.IsNullOrWhiteSpace(json))
            return;

        ModificationCollection collection;

        try
        {
            collection = JsonUtility.FromJson<ModificationCollection>(json);
        }
        catch (Exception exception)
        {
            Debug.LogWarning(
                "[NewspaperPlayEdit] Falha ao ler as edições feitas no Play: " +
                exception.Message
            );
            return;
        }

        if (collection == null || collection.items == null || collection.items.Count == 0)
            return;

        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Aplicar edições do jornal feitas em Play Mode");

        int appliedCount = 0;
        HashSet<int> recordedTargets = new HashSet<int>();
        HashSet<string> dirtyScenePaths = new HashSet<string>(StringComparer.Ordinal);

        for (int i = 0; i < collection.items.Count; i++)
        {
            RecordedModification record = collection.items[i];
            UnityEngine.Object target = ResolveTarget(record != null ? record.target : null);

            if (target == null || record == null || string.IsNullOrEmpty(record.propertyPath))
                continue;

            if (!TryGetTargetGameObject(target, out GameObject targetObject) ||
                !IsInsidePersistentNewspaperHierarchy(targetObject.transform))
            {
                continue;
            }

            PropertyModification modification = new PropertyModification
            {
                target = target,
                propertyPath = record.propertyPath,
                value = record.value ?? string.Empty
            };

            if (record.isObjectReference)
            {
                modification.objectReference = record.objectReferenceIsNull
                    ? null
                    : ResolveRecordedObjectReference(record);

                if (!record.objectReferenceIsNull && modification.objectReference == null)
                    continue;
            }

            int instanceId = target.GetInstanceID();
            if (recordedTargets.Add(instanceId))
                Undo.RecordObject(target, "Aplicar edição do jornal feita em Play Mode");

            try
            {
                modification.Apply();
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    "[NewspaperPlayEdit] Não foi possível aplicar '" +
                    record.propertyPath + "' em '" + targetObject.name + "': " +
                    exception.GetBaseException().Message,
                    targetObject
                );
                continue;
            }

            EditorUtility.SetDirty(target);

            Scene scene = targetObject.scene;
            if (scene.IsValid() && scene.isLoaded && !string.IsNullOrEmpty(scene.path))
                dirtyScenePaths.Add(scene.path);

            appliedCount++;
        }

        Undo.CollapseUndoOperations(undoGroup);

        foreach (string scenePath in dirtyScenePaths)
        {
            Scene scene = FindLoadedScene(scenePath);
            if (scene.IsValid() && scene.isLoaded)
                EditorSceneManager.MarkSceneDirty(scene);
        }

        if (appliedCount > 0)
        {
            Debug.Log(
                "[NewspaperPlayEdit] " + appliedCount +
                " alteração(ões) feita(s) durante o Play foram aplicadas à cena. " +
                "Use Ctrl+S para salvar."
            );
        }
    }

    private static bool CaptureObjectReferenceIfNeeded(
        PropertyModification modification,
        RecordedModification record)
    {
        SerializedProperty property = FindSerializedProperty(
            modification.target,
            modification.propertyPath
        );

        bool isObjectReference = property != null &&
                                 property.propertyType == SerializedPropertyType.ObjectReference;

        if (!isObjectReference)
            return true;

        record.isObjectReference = true;
        UnityEngine.Object reference = modification.objectReference;

        if (reference == null)
        {
            record.objectReferenceIsNull = true;
            return true;
        }

        if (AssetDatabase.Contains(reference) &&
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(
                reference,
                out string guid,
                out long localFileId
            ))
        {
            record.assetReference = new AssetReference
            {
                guid = guid,
                localFileId = localFileId
            };
            return true;
        }

        TargetLocator sceneLocator = GetOriginalOrCurrentLocator(reference);
        if (sceneLocator != null)
        {
            record.sceneObjectReference = sceneLocator;
            return true;
        }

        return false;
    }

    private static SerializedProperty FindSerializedProperty(
        UnityEngine.Object target,
        string propertyPath)
    {
        if (target == null || string.IsNullOrEmpty(propertyPath))
            return null;

        try
        {
            SerializedObject serializedObject = new SerializedObject(target);
            return serializedObject.FindProperty(propertyPath);
        }
        catch
        {
            return null;
        }
    }

    private static UnityEngine.Object ResolveRecordedObjectReference(
        RecordedModification record)
    {
        if (record == null)
            return null;

        if (record.assetReference != null &&
            !string.IsNullOrEmpty(record.assetReference.guid))
        {
            string path = AssetDatabase.GUIDToAssetPath(record.assetReference.guid);
            if (!string.IsNullOrEmpty(path))
            {
                UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(path);

                for (int i = 0; i < assets.Length; i++)
                {
                    UnityEngine.Object asset = assets[i];
                    if (asset == null)
                        continue;

                    if (AssetDatabase.TryGetGUIDAndLocalFileIdentifier(
                            asset,
                            out _,
                            out long localFileId
                        ) &&
                        localFileId == record.assetReference.localFileId)
                    {
                        return asset;
                    }
                }

                return AssetDatabase.LoadMainAssetAtPath(path);
            }
        }

        return ResolveTarget(record.sceneObjectReference);
    }

    private static void CapturePlacementPromptScaleBaseline()
    {
        ScaleSnapshotCollection collection = new ScaleSnapshotCollection();
        NewspaperWorldPromptVisual[] prompts =
            UnityEngine.Object.FindObjectsByType<NewspaperWorldPromptVisual>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None
            );

        for (int i = 0; i < prompts.Length; i++)
        {
            NewspaperWorldPromptVisual prompt = prompts[i];
            if (!IsPlacementPrompt(prompt) ||
                !TryCreateTargetLocator(prompt.transform, out TargetLocator locator))
            {
                continue;
            }

            collection.items.Add(new ScaleSnapshot
            {
                target = locator,
                localScale = prompt.transform.localScale
            });
        }

        SessionState.SetString(
            BaselineScaleSessionKey,
            JsonUtility.ToJson(collection)
        );
    }

    private static void RestorePlacementPromptScaleBaseline()
    {
        string json = SessionState.GetString(BaselineScaleSessionKey, string.Empty);
        SessionState.EraseString(BaselineScaleSessionKey);

        if (string.IsNullOrWhiteSpace(json))
            return;

        ScaleSnapshotCollection collection;

        try
        {
            collection = JsonUtility.FromJson<ScaleSnapshotCollection>(json);
        }
        catch
        {
            return;
        }

        if (collection == null || collection.items == null)
            return;

        for (int i = 0; i < collection.items.Count; i++)
        {
            ScaleSnapshot snapshot = collection.items[i];
            Transform target = ResolveTarget(snapshot != null ? snapshot.target : null) as Transform;

            if (target == null ||
                !IsPlacementPrompt(target.GetComponent<NewspaperWorldPromptVisual>()) ||
                (target.localScale - snapshot.localScale).sqrMagnitude <= 0.000000000001f)
            {
                continue;
            }

            Undo.RecordObject(target, "Restaurar escala-base do Newspaper_PlacePrompt");
            target.localScale = snapshot.localScale;
            EditorUtility.SetDirty(target);

            Scene scene = target.gameObject.scene;
            if (scene.IsValid() && scene.isLoaded)
                EditorSceneManager.MarkSceneDirty(scene);
        }
    }

    private static void DisableLegacyPlacementRepairInLoadedScenes()
    {
        NewspaperWorldPromptVisual[] prompts =
            UnityEngine.Object.FindObjectsByType<NewspaperWorldPromptVisual>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None
            );

        for (int i = 0; i < prompts.Length; i++)
        {
            NewspaperWorldPromptVisual prompt = prompts[i];
            if (!IsPlacementPrompt(prompt))
                continue;

            // Alteração somente em memória: não toca no Transform e não salva a cena.
            prompt.useRootTransformAsSource = true;
            prompt.repairLegacyPlacementPrompt = false;

            if (LegacyRepairAppliedField != null)
                LegacyRepairAppliedField.SetValue(prompt, true);
        }
    }

    private static bool IsInsidePersistentNewspaperHierarchy(Transform target)
    {
        Transform current = target;

        while (current != null)
        {
            if (current.GetComponent<NewspaperStandController>() != null ||
                current.GetComponent<NewspaperPlacementAreaController>() != null ||
                current.GetComponent<NewspaperWorldPromptVisual>() != null ||
                current.GetComponent<NewspaperInstructionTextSettings>() != null ||
                current.GetComponent<NewspaperInstructionTextProfile>() != null ||
                current.GetComponent<PersistentPlacedNewspaperVisual>() != null)
            {
                return true;
            }

            string objectName = current.name;

            if (NameMatchesNewspaperHierarchy(objectName))
                return true;

            current = current.parent;
        }

        return false;
    }

    private static bool NameMatchesNewspaperHierarchy(string objectName)
    {
        if (string.IsNullOrEmpty(objectName))
            return false;

        return objectName.IndexOf("Newspaper_Stand", StringComparison.OrdinalIgnoreCase) >= 0 ||
               objectName.IndexOf("Newspaper_InteractionPrompt", StringComparison.OrdinalIgnoreCase) >= 0 ||
               objectName.IndexOf("Newspaper_PlacePrompt", StringComparison.OrdinalIgnoreCase) >= 0 ||
               objectName.IndexOf("Placed_Newspaper_Runtime", StringComparison.OrdinalIgnoreCase) >= 0 ||
               objectName.IndexOf("NewspaperPutArea_", StringComparison.OrdinalIgnoreCase) >= 0 ||
               objectName.IndexOf("Jornal_Place", StringComparison.OrdinalIgnoreCase) >= 0 ||
               objectName.IndexOf("Jornal_Stand", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static bool IsPlacementPrompt(NewspaperWorldPromptVisual prompt)
    {
        if (prompt == null)
            return false;

        Transform current = prompt.transform;

        while (current != null)
        {
            string objectName = current.name;

            if (objectName.IndexOf("Newspaper_PlacePrompt", StringComparison.OrdinalIgnoreCase) >= 0 ||
                objectName.IndexOf("Put_Area", StringComparison.OrdinalIgnoreCase) >= 0 ||
                objectName.IndexOf("Jornal_Place", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }

            current = current.parent;
        }

        return false;
    }

    private static bool TryGetTargetGameObject(
        UnityEngine.Object target,
        out GameObject gameObject)
    {
        if (target is GameObject directGameObject)
        {
            gameObject = directGameObject;
            return true;
        }

        if (target is Component component)
        {
            gameObject = component.gameObject;
            return true;
        }

        gameObject = null;
        return false;
    }

    private static bool TryCreateTargetLocator(
        UnityEngine.Object target,
        out TargetLocator locator)
    {
        locator = null;

        if (!TryGetTargetGameObject(target, out GameObject targetObject))
            return false;

        Scene scene = targetObject.scene;
        if (!scene.IsValid() || !scene.isLoaded || string.IsNullOrEmpty(scene.path))
            return false;

        locator = new TargetLocator
        {
            scenePath = scene.path,
            siblingIndexPath = BuildSiblingIndexPath(targetObject.transform),
            targetsGameObject = target is GameObject
        };

        if (target is Component component)
        {
            Type type = component.GetType();
            locator.componentAssemblyQualifiedName = type.AssemblyQualifiedName;
            locator.componentFullName = type.FullName;
            locator.componentIndex = FindComponentIndex(component);

            if (locator.componentIndex < 0)
            {
                locator = null;
                return false;
            }
        }

        return true;
    }

    private static int FindComponentIndex(Component component)
    {
        if (component == null)
            return -1;

        Component[] components = component.gameObject.GetComponents(component.GetType());

        for (int i = 0; i < components.Length; i++)
        {
            if (ReferenceEquals(components[i], component))
                return i;
        }

        return -1;
    }

    private static UnityEngine.Object ResolveTarget(TargetLocator locator)
    {
        if (locator == null ||
            string.IsNullOrEmpty(locator.scenePath) ||
            string.IsNullOrEmpty(locator.siblingIndexPath))
        {
            return null;
        }

        Scene scene = FindLoadedScene(locator.scenePath);
        if (!scene.IsValid() || !scene.isLoaded)
            return null;

        Transform transform = ResolveTransformBySiblingPath(scene, locator.siblingIndexPath);
        if (transform == null)
            return null;

        if (locator.targetsGameObject)
            return transform.gameObject;

        Type componentType = ResolveType(
            locator.componentAssemblyQualifiedName,
            locator.componentFullName
        );

        if (componentType == null || !typeof(Component).IsAssignableFrom(componentType))
            return null;

        Component[] components = transform.gameObject.GetComponents(componentType);
        return locator.componentIndex >= 0 && locator.componentIndex < components.Length
            ? components[locator.componentIndex]
            : null;
    }

    private static Type ResolveType(string assemblyQualifiedName, string fullName)
    {
        Type type = !string.IsNullOrEmpty(assemblyQualifiedName)
            ? Type.GetType(assemblyQualifiedName, false)
            : null;

        if (type != null)
            return type;

        if (string.IsNullOrEmpty(fullName))
            return null;

        Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
        for (int i = 0; i < assemblies.Length; i++)
        {
            type = assemblies[i].GetType(fullName, false);
            if (type != null)
                return type;
        }

        return null;
    }

    private static Transform ResolveTransformBySiblingPath(
        Scene scene,
        string siblingIndexPath)
    {
        string[] parts = siblingIndexPath.Split('/');
        if (parts.Length == 0 || !int.TryParse(parts[0], out int rootIndex))
            return null;

        GameObject[] roots = scene.GetRootGameObjects();
        if (rootIndex < 0 || rootIndex >= roots.Length)
            return null;

        Transform current = roots[rootIndex].transform;

        for (int i = 1; i < parts.Length; i++)
        {
            if (!int.TryParse(parts[i], out int childIndex) ||
                childIndex < 0 ||
                childIndex >= current.childCount)
            {
                return null;
            }

            current = current.GetChild(childIndex);
        }

        return current;
    }

    private static string BuildSiblingIndexPath(Transform target)
    {
        if (target == null)
            return string.Empty;

        List<int> indices = new List<int>();
        Transform current = target;

        while (current != null)
        {
            indices.Add(current.GetSiblingIndex());
            current = current.parent;
        }

        indices.Reverse();
        return string.Join("/", indices);
    }

    private static string BuildModificationKey(RecordedModification record)
    {
        TargetLocator locator = record.target;

        return locator.scenePath + "|" +
               locator.siblingIndexPath + "|" +
               locator.targetsGameObject + "|" +
               locator.componentFullName + "|" +
               locator.componentIndex + "|" +
               record.propertyPath;
    }

    private static TargetLocator CloneLocator(TargetLocator source)
    {
        if (source == null)
            return null;

        return new TargetLocator
        {
            scenePath = source.scenePath,
            siblingIndexPath = source.siblingIndexPath,
            targetsGameObject = source.targetsGameObject,
            componentAssemblyQualifiedName = source.componentAssemblyQualifiedName,
            componentFullName = source.componentFullName,
            componentIndex = source.componentIndex
        };
    }

    private static Scene FindLoadedScene(string scenePath)
    {
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);
            if (scene.path == scenePath)
                return scene;
        }

        return default;
    }
}
#endif
