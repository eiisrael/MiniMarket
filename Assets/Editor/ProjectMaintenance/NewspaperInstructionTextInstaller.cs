#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Garante que cada objeto Instruction do sistema de jornal possua o editor de
/// textos e que as configurações sejam restauradas depois de uma reconstrução visual.
/// </summary>
[InitializeOnLoad]
public static class NewspaperInstructionTextInstaller
{
    private const string MenuPath =
        "Tools/MiniMarket/Jornal/Adicionar Editor de Textos ao Instruction";

    private static bool installQueued;

    static NewspaperInstructionTextInstaller()
    {
        QueueInstall();
        EditorApplication.hierarchyChanged += QueueInstall;
    }

    [MenuItem(MenuPath, priority = 2601)]
    public static void InstallFromMenu()
    {
        InstallMissingComponents(true);
    }

    private static void QueueInstall()
    {
        if (installQueued)
            return;

        installQueued = true;
        EditorApplication.delayCall += DelayedInstall;
    }

    private static void DelayedInstall()
    {
        installQueued = false;

        if (EditorApplication.isPlayingOrWillChangePlaymode ||
            EditorApplication.isCompiling ||
            EditorApplication.isUpdating)
        {
            QueueInstall();
            return;
        }

        InstallMissingComponents(false);
    }

    private static void InstallMissingComponents(bool logResult)
    {
        NewspaperWorldPromptVisual[] prompts =
            Object.FindObjectsByType<NewspaperWorldPromptVisual>(FindObjectsInactive.Include);

        int configured = 0;

        for (int i = 0; i < prompts.Length; i++)
        {
            NewspaperWorldPromptVisual prompt = prompts[i];
            if (prompt == null || !prompt.gameObject.scene.IsValid())
                continue;

            NewspaperInstructionTextProfile profile =
                prompt.GetComponent<NewspaperInstructionTextProfile>();

            if (profile == null)
            {
                profile = Undo.AddComponent<NewspaperInstructionTextProfile>(prompt.gameObject);
                profile.hideFlags = HideFlags.HideInInspector;
                EditorUtility.SetDirty(profile);
            }

            Transform instruction = prompt.transform.Find("Instruction");
            if (instruction == null || instruction.GetComponent<TMP_Text>() == null)
                continue;

            NewspaperInstructionTextSettings settings =
                instruction.GetComponent<NewspaperInstructionTextSettings>();

            if (settings == null)
                settings = Undo.AddComponent<NewspaperInstructionTextSettings>(instruction.gameObject);

            settings.InitializeFromPersistentProfile();
            EditorUtility.SetDirty(settings);
            EditorUtility.SetDirty(prompt.gameObject);
            EditorSceneManager.MarkSceneDirty(prompt.gameObject.scene);
            configured++;
        }

        if (logResult)
        {
            Debug.Log(
                "[NewspaperInstruction] Editor de textos configurado em " +
                configured + " prompt(s)."
            );
        }
    }
}
#endif
