#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Preserva exatamente a escala editada no Transform de Newspaper_PlacePrompt.
///
/// O reparo legado de escala do NewspaperWorldPromptVisual é desativado em memória
/// antes do Play Mode. A escala anterior ao Play é registrada no SessionState e
/// restaurada após o Stop apenas se algum código antigo tiver alterado o Transform.
/// Não há Update, busca por frame ou salvamento automático da cena.
/// </summary>
[InitializeOnLoad]
internal static class NewspaperPlacePromptScalePersistence
{
    private const string SessionKey =
        "MiniMarket.NewspaperPlacePrompt.ScaleBeforePlay.v1";

    private static readonly FieldInfo LegacyRepairAppliedField =
        typeof(NewspaperWorldPromptVisual).GetField(
            "legacyPlacementRepairApplied",
            BindingFlags.Instance | BindingFlags.NonPublic
        );

    [Serializable]
    private sealed class ScaleSnapshotCollection
    {
        public List<ScaleSnapshot> items = new List<ScaleSnapshot>();
    }

    [Serializable]
    private sealed class ScaleSnapshot
    {
        public string scenePath;
        public string siblingIndexPath;
        public Vector3 localScale;
    }

    static NewspaperPlacePromptScalePersistence()
    {
        EditorApplication.playModeStateChanged -= HandlePlayModeStateChanged;
        EditorApplication.playModeStateChanged += HandlePlayModeStateChanged;
        EditorApplication.delayCall += DisableLegacyRepairInLoadedScenes;
    }

    private static void HandlePlayModeStateChanged(PlayModeStateChange state)
    {
        switch (state)
        {
            case PlayModeStateChange.ExitingEditMode:
                CaptureScaleBeforePlay();
                DisableLegacyRepairInLoadedScenes();
                break;

            case PlayModeStateChange.EnteredPlayMode:
                DisableLegacyRepairInLoadedScenes();
                break;

            case PlayModeStateChange.EnteredEditMode:
                EditorApplication.delayCall += RestoreScaleAfterPlay;
                break;
        }
    }

    private static void CaptureScaleBeforePlay()
    {
        ScaleSnapshotCollection collection = new ScaleSnapshotCollection();
        NewspaperWorldPromptVisual[] prompts = FindLoadedPrompts();

        for (int i = 0; i < prompts.Length; i++)
        {
            NewspaperWorldPromptVisual prompt = prompts[i];
            if (!IsPlacementPrompt(prompt))
                continue;

            Scene scene = prompt.gameObject.scene;
            if (!scene.IsValid() || string.IsNullOrEmpty(scene.path))
                continue;

            collection.items.Add(new ScaleSnapshot
            {
                scenePath = scene.path,
                siblingIndexPath = BuildSiblingIndexPath(prompt.transform),
                localScale = prompt.transform.localScale
            });
        }

        SessionState.SetString(SessionKey, JsonUtility.ToJson(collection));
    }

    private static void RestoreScaleAfterPlay()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        DisableLegacyRepairInLoadedScenes();

        string json = SessionState.GetString(SessionKey, string.Empty);
        SessionState.EraseString(SessionKey);

        if (string.IsNullOrWhiteSpace(json))
            return;

        ScaleSnapshotCollection collection;

        try
        {
            collection = JsonUtility.FromJson<ScaleSnapshotCollection>(json);
        }
        catch (Exception exception)
        {
            Debug.LogWarning(
                "[NewspaperPlacePrompt] Falha ao ler escala anterior ao Play: " +
                exception.Message
            );
            return;
        }

        if (collection == null || collection.items == null)
            return;

        for (int i = 0; i < collection.items.Count; i++)
        {
            ScaleSnapshot snapshot = collection.items[i];
            Transform target = ResolveTransform(snapshot);

            if (target == null || !IsPlacementPrompt(target.GetComponent<NewspaperWorldPromptVisual>()))
                continue;

            if ((target.localScale - snapshot.localScale).sqrMagnitude <= 0.000000000001f)
                continue;

            Undo.RecordObject(target, "Restaurar escala do Newspaper_PlacePrompt");
            target.localScale = snapshot.localScale;
            EditorUtility.SetDirty(target);

            Scene scene = target.gameObject.scene;
            if (scene.IsValid() && scene.isLoaded)
                EditorSceneManager.MarkSceneDirty(scene);
        }
    }

    private static void DisableLegacyRepairInLoadedScenes()
    {
        NewspaperWorldPromptVisual[] prompts = FindLoadedPrompts();

        for (int i = 0; i < prompts.Length; i++)
        {
            NewspaperWorldPromptVisual prompt = prompts[i];
            if (!IsPlacementPrompt(prompt))
                continue;

            // Não altera Transform, não normaliza escala e não salva a cena.
            prompt.useRootTransformAsSource = true;
            prompt.repairLegacyPlacementPrompt = false;

            if (LegacyRepairAppliedField != null)
                LegacyRepairAppliedField.SetValue(prompt, true);
        }
    }

    private static NewspaperWorldPromptVisual[] FindLoadedPrompts()
    {
        return UnityEngine.Object.FindObjectsByType<NewspaperWorldPromptVisual>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None
        );
    }

    private static bool IsPlacementPrompt(NewspaperWorldPromptVisual prompt)
    {
        if (prompt == null)
            return false;

        Transform current = prompt.transform;

        while (current != null)
        {
            string objectName = current.name;

            if (objectName.IndexOf(
                    "Newspaper_PlacePrompt",
                    StringComparison.OrdinalIgnoreCase
                ) >= 0 ||
                objectName.IndexOf(
                    "Put_Area",
                    StringComparison.OrdinalIgnoreCase
                ) >= 0 ||
                objectName.IndexOf(
                    "Jornal_Place",
                    StringComparison.OrdinalIgnoreCase
                ) >= 0)
            {
                return true;
            }

            current = current.parent;
        }

        return false;
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

    private static Transform ResolveTransform(ScaleSnapshot snapshot)
    {
        if (snapshot == null ||
            string.IsNullOrEmpty(snapshot.scenePath) ||
            string.IsNullOrEmpty(snapshot.siblingIndexPath))
        {
            return null;
        }

        Scene scene = FindLoadedScene(snapshot.scenePath);
        if (!scene.IsValid() || !scene.isLoaded)
            return null;

        string[] parts = snapshot.siblingIndexPath.Split('/');
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
