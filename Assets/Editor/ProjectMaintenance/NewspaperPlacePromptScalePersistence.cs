#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
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
/// Compatível com Unity 6.7: não usa GetInstanceID nem PropertyModification.Apply.
/// As propriedades são capturadas com tipo forte e reaplicadas por SerializedObject.
/// </summary>
[InitializeOnLoad]
internal static class NewspaperPlayModeHierarchyPersistence
{
    private const string ModificationSessionKey =
        "MiniMarket.Newspaper.PlayModeInspectorModifications.v3";

    private const string BaselineScaleSessionKey =
        "MiniMarket.Newspaper.PlacePromptScaleBeforePlay.v3";

    private static readonly Dictionary<string, RecordedModification> PendingModifications =
        new Dictionary<string, RecordedModification>(StringComparer.Ordinal);

    private static readonly Dictionary<UnityEngine.Object, TargetLocator> OriginalTargetLocators =
        new Dictionary<UnityEngine.Object, TargetLocator>(ObjectReferenceComparer.Instance);

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
        public int propertyType;

        public long longValue;
        public double doubleValue;
        public bool boolValue;
        public string stringValue;
        public Color colorValue;
        public Vector2 vector2Value;
        public Vector3 vector3Value;
        public Vector4 vector4Value;
        public Rect rectValue;
        public Bounds boundsValue;
        public Quaternion quaternionValue;
        public Vector2Int vector2IntValue;
        public Vector3Int vector3IntValue;
        public RectInt rectIntValue;
        public BoundsInt boundsIntValue;
        public string animationCurveJson;

        public bool isObjectReference;
        public bool objectReferenceIsNull;
        public AssetReference assetReference;
        public TargetLocator sceneObjectReference;
    }

    [Serializable]
    private sealed class AnimationCurveContainer
    {
        public AnimationCurve value;
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

    private sealed class ObjectReferenceComparer : IEqualityComparer<UnityEngine.Object>
    {
        public static readonly ObjectReferenceComparer Instance =
            new ObjectReferenceComparer();

        public bool Equals(UnityEngine.Object x, UnityEngine.Object y)
        {
            return ReferenceEquals(x, y);
        }

        public int GetHashCode(UnityEngine.Object value)
        {
            return value == null ? 0 : RuntimeHelpers.GetHashCode(value);
        }
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

            if (!TryCaptureSerializedProperty(
                    current.target,
                    current.propertyPath,
                    targetLocator,
                    out RecordedModification record
                ))
            {
                continue;
            }

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
            Transform current = transforms[i];
            if (current == null || !IsInsidePersistentNewspaperHierarchy(current))
                continue;

            CacheLocator(current.gameObject);

            Component[] components = current.GetComponents<Component>();
            for (int componentIndex = 0; componentIndex < components.Length; componentIndex++)
                CacheLocator(components[componentIndex]);
        }
    }

    private static void CacheLocator(UnityEngine.Object target)
    {
        if (ReferenceEquals(target, null) || OriginalTargetLocators.ContainsKey(target))
            return;

        if (TryCreateTargetLocator(target, out TargetLocator locator))
            OriginalTargetLocators[target] = locator;
    }

    private static TargetLocator GetOriginalOrCurrentLocator(UnityEngine.Object target)
    {
        if (ReferenceEquals(target, null))
            return null;

        if (OriginalTargetLocators.TryGetValue(target, out TargetLocator cached))
            return CloneLocator(cached);

        return TryCreateTargetLocator(target, out TargetLocator current)
            ? current
            : null;
    }

    private static bool TryCaptureSerializedProperty(
        UnityEngine.Object target,
        string propertyPath,
        TargetLocator targetLocator,
        out RecordedModification record)
    {
        record = null;

        SerializedObject serializedObject;
        SerializedProperty property;

        try
        {
            serializedObject = new SerializedObject(target);
            serializedObject.UpdateIfRequiredOrScript();
            property = serializedObject.FindProperty(propertyPath);
        }
        catch
        {
            return false;
        }

        if (property == null)
            return false;

        record = new RecordedModification
        {
            target = CloneLocator(targetLocator),
            propertyPath = propertyPath,
            propertyType = (int)property.propertyType
        };

        try
        {
            switch (property.propertyType)
            {
                case SerializedPropertyType.Integer:
                case SerializedPropertyType.LayerMask:
                case SerializedPropertyType.ArraySize:
                case SerializedPropertyType.Character:
                    record.longValue = property.longValue;
                    return true;

                case SerializedPropertyType.Boolean:
                    record.boolValue = property.boolValue;
                    return true;

                case SerializedPropertyType.Float:
                    record.doubleValue = property.doubleValue;
                    return true;

                case SerializedPropertyType.String:
                    record.stringValue = property.stringValue ?? string.Empty;
                    return true;

                case SerializedPropertyType.Color:
                    record.colorValue = property.colorValue;
                    return true;

                case SerializedPropertyType.ObjectReference:
                    return CaptureObjectReference(property.objectReferenceValue, record);

                case SerializedPropertyType.Enum:
                    record.longValue = property.enumValueIndex;
                    return true;

                case SerializedPropertyType.Vector2:
                    record.vector2Value = property.vector2Value;
                    return true;

                case SerializedPropertyType.Vector3:
                    record.vector3Value = property.vector3Value;
                    return true;

                case SerializedPropertyType.Vector4:
                    record.vector4Value = property.vector4Value;
                    return true;

                case SerializedPropertyType.Rect:
                    record.rectValue = property.rectValue;
                    return true;

                case SerializedPropertyType.AnimationCurve:
                    record.animationCurveJson = JsonUtility.ToJson(
                        new AnimationCurveContainer { value = property.animationCurveValue }
                    );
                    return true;

                case SerializedPropertyType.Bounds:
                    record.boundsValue = property.boundsValue;
                    return true;

                case SerializedPropertyType.Quaternion:
                    record.quaternionValue = property.quaternionValue;
                    return true;

                case SerializedPropertyType.ExposedReference:
                    return CaptureObjectReference(property.exposedReferenceValue, record);

                case SerializedPropertyType.Vector2Int:
                    record.vector2IntValue = property.vector2IntValue;
                    return true;

                case SerializedPropertyType.Vector3Int:
                    record.vector3IntValue = property.vector3IntValue;
                    return true;

                case SerializedPropertyType.RectInt:
                    record.rectIntValue = property.rectIntValue;
                    return true;

                case SerializedPropertyType.BoundsInt:
                    record.boundsIntValue = property.boundsIntValue;
                    return true;

                default:
                    return false;
            }
        }
        catch
        {
            record = null;
            return false;
        }
    }

    private static bool CaptureObjectReference(
        UnityEngine.Object reference,
        RecordedModification record)
    {
        record.isObjectReference = true;

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
        if (sceneLocator == null)
            return false;

        record.sceneObjectReference = sceneLocator;
        return true;
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
        HashSet<UnityEngine.Object> recordedTargets =
            new HashSet<UnityEngine.Object>(ObjectReferenceComparer.Instance);
        HashSet<string> dirtyScenePaths = new HashSet<string>(StringComparer.Ordinal);

        for (int i = 0; i < collection.items.Count; i++)
        {
            RecordedModification record = collection.items[i];
            if (record == null || string.IsNullOrEmpty(record.propertyPath))
                continue;

            UnityEngine.Object target = ResolveTarget(record.target);
            if (target == null)
                continue;

            if (!TryGetTargetGameObject(target, out GameObject targetObject) ||
                !IsInsidePersistentNewspaperHierarchy(targetObject.transform))
            {
                continue;
            }

            if (recordedTargets.Add(target))
                Undo.RecordObject(target, "Aplicar edição do jornal feita em Play Mode");

            if (!TryApplySerializedProperty(target, record, out string error))
            {
                Debug.LogWarning(
                    "[NewspaperPlayEdit] Não foi possível aplicar '" +
                    record.propertyPath + "' em '" + targetObject.name + "': " + error,
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

    private static bool TryApplySerializedProperty(
        UnityEngine.Object target,
        RecordedModification record,
        out string error)
    {
        error = string.Empty;

        try
        {
            SerializedObject serializedObject = new SerializedObject(target);
            serializedObject.UpdateIfRequiredOrScript();
            SerializedProperty property = serializedObject.FindProperty(record.propertyPath);

            if (property == null)
            {
                error = "propriedade serializada não encontrada";
                return false;
            }

            SerializedPropertyType recordedType = (SerializedPropertyType)record.propertyType;
            if (property.propertyType != recordedType)
            {
                error = "tipo serializado mudou de " + recordedType +
                        " para " + property.propertyType;
                return false;
            }

            switch (recordedType)
            {
                case SerializedPropertyType.Integer:
                case SerializedPropertyType.LayerMask:
                case SerializedPropertyType.ArraySize:
                case SerializedPropertyType.Character:
                    property.longValue = record.longValue;
                    break;

                case SerializedPropertyType.Boolean:
                    property.boolValue = record.boolValue;
                    break;

                case SerializedPropertyType.Float:
                    property.doubleValue = record.doubleValue;
                    break;

                case SerializedPropertyType.String:
                    property.stringValue = record.stringValue ?? string.Empty;
                    break;

                case SerializedPropertyType.Color:
                    property.colorValue = record.colorValue;
                    break;

                case SerializedPropertyType.ObjectReference:
                    property.objectReferenceValue = ResolveRecordedObjectReference(record);
                    if (!record.objectReferenceIsNull && property.objectReferenceValue == null)
                    {
                        error = "referência de objeto não pôde ser resolvida";
                        return false;
                    }
                    break;

                case SerializedPropertyType.Enum:
                    property.enumValueIndex = (int)record.longValue;
                    break;

                case SerializedPropertyType.Vector2:
                    property.vector2Value = record.vector2Value;
                    break;

                case SerializedPropertyType.Vector3:
                    property.vector3Value = record.vector3Value;
                    break;

                case SerializedPropertyType.Vector4:
                    property.vector4Value = record.vector4Value;
                    break;

                case SerializedPropertyType.Rect:
                    property.rectValue = record.rectValue;
                    break;

                case SerializedPropertyType.AnimationCurve:
                    AnimationCurveContainer curveContainer =
                        JsonUtility.FromJson<AnimationCurveContainer>(record.animationCurveJson);
                    property.animationCurveValue = curveContainer != null
                        ? curveContainer.value
                        : new AnimationCurve();
                    break;

                case SerializedPropertyType.Bounds:
                    property.boundsValue = record.boundsValue;
                    break;

                case SerializedPropertyType.Quaternion:
                    property.quaternionValue = record.quaternionValue;
                    break;

                case SerializedPropertyType.ExposedReference:
                    property.exposedReferenceValue = ResolveRecordedObjectReference(record);
                    if (!record.objectReferenceIsNull && property.exposedReferenceValue == null)
                    {
                        error = "referência exposta não pôde ser resolvida";
                        return false;
                    }
                    break;

                case SerializedPropertyType.Vector2Int:
                    property.vector2IntValue = record.vector2IntValue;
                    break;

                case SerializedPropertyType.Vector3Int:
                    property.vector3IntValue = record.vector3IntValue;
                    break;

                case SerializedPropertyType.RectInt:
                    property.rectIntValue = record.rectIntValue;
                    break;

                case SerializedPropertyType.BoundsInt:
                    property.boundsIntValue = record.boundsIntValue;
                    break;

                default:
                    error = "tipo de propriedade não suportado: " + recordedType;
                    return false;
            }

            serializedObject.ApplyModifiedPropertiesWithoutUndo();
            return true;
        }
        catch (Exception exception)
        {
            error = exception.GetBaseException().Message;
            return false;
        }
    }

    private static UnityEngine.Object ResolveRecordedObjectReference(
        RecordedModification record)
    {
        if (record == null || record.objectReferenceIsNull)
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

            if (NameMatchesNewspaperHierarchy(current.name))
                return true;

            current = current.parent;
        }

        return false;
    }

    private static bool NameMatchesNewspaperHierarchy(string objectName)
    {
        if (string.IsNullOrEmpty(objectName))
            return false;

        return string.Equals(objectName, "Put_Area", StringComparison.OrdinalIgnoreCase) ||
               objectName.IndexOf("Newspaper_Stand", StringComparison.OrdinalIgnoreCase) >= 0 ||
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
