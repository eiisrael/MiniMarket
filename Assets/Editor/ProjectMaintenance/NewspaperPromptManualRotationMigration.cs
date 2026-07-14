#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Libera a rotação manual do Newspaper_InteractionPrompt.
/// O prompt deixa de usar billboard automático e passa a respeitar integralmente
/// a rotação definida no RectTransform pelo Inspector.
/// </summary>
[InitializeOnLoad]
public static class NewspaperPromptManualRotationMigration
{
    private const string MenuPath = "Tools/MiniMarket/Jornal/Liberar Rotação Manual do Prompt";

    static NewspaperPromptManualRotationMigration()
    {
        EditorApplication.delayCall += ApplyAfterCompilation;
    }

    [MenuItem(MenuPath, priority = 2601)]
    public static void ApplyManually()
    {
        ApplyToLoadedScenes(true);
    }

    [MenuItem(MenuPath, true)]
    private static bool ValidateApplyManually()
    {
        return !EditorApplication.isPlayingOrWillChangePlaymode;
    }

    private static void ApplyAfterCompilation()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode ||
            EditorApplication.isCompiling ||
            EditorApplication.isUpdating)
        {
            EditorApplication.delayCall += ApplyAfterCompilation;
            return;
        }

        ApplyToLoadedScenes(false);
    }

    private static void ApplyToLoadedScenes(bool logResult)
    {
        NewspaperWorldPromptVisual[] prompts =
            Object.FindObjectsByType<NewspaperWorldPromptVisual>(FindObjectsInactive.Include);

        int changedCount = 0;

        for (int i = 0; i < prompts.Length; i++)
        {
            NewspaperWorldPromptVisual prompt = prompts[i];
            if (prompt == null || prompt.name != "Newspaper_InteractionPrompt")
                continue;

            if (!prompt.gameObject.scene.IsValid() || !prompt.gameObject.scene.isLoaded)
                continue;

            bool changed = false;
            Undo.RecordObject(prompt, "Liberar rotação manual do prompt de jornal");

            if (prompt.faceCamera)
            {
                prompt.faceCamera = false;
                changed = true;
            }

            if (!prompt.useRootTransformAsSource)
            {
                prompt.useRootTransformAsSource = true;
                changed = true;
            }

            if (!changed)
                continue;

            EditorUtility.SetDirty(prompt);
            EditorSceneManager.MarkSceneDirty(prompt.gameObject.scene);
            changedCount++;
        }

        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);
            if (scene.IsValid() && scene.isLoaded && scene.isDirty)
                EditorSceneManager.SaveScene(scene);
        }

        if (logResult)
        {
            Debug.Log(
                "[NewspaperPrompt] Rotação manual liberada. Prompts atualizados: " +
                changedCount
            );
        }
    }
}
#endif
