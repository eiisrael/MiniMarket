#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Instala manualmente os componentes editáveis do Instruction.
/// Não executa em hierarchyChanged ou após recompilar, evitando que a cena volte
/// a ficar modificada logo depois de Ctrl+S.
/// </summary>
public static class NewspaperInstructionTextInstaller
{
    private const string MenuPath =
        "Tools/MiniMarket/Jornal/Adicionar Editor de Textos ao Instruction";

    [MenuItem(MenuPath, priority = 2601)]
    public static void InstallFromMenu()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("[NewspaperInstruction] Saia do Play Mode antes de configurar.");
            return;
        }

        NewspaperWorldPromptVisual[] prompts =
            Object.FindObjectsByType<NewspaperWorldPromptVisual>(FindObjectsInactive.Include);

        int configured = 0;
        bool anySceneChanged = false;

        for (int i = 0; i < prompts.Length; i++)
        {
            NewspaperWorldPromptVisual prompt = prompts[i];
            if (prompt == null || !prompt.gameObject.scene.IsValid())
                continue;

            bool changed = false;
            NewspaperInstructionTextProfile profile =
                prompt.GetComponent<NewspaperInstructionTextProfile>();

            if (profile == null)
            {
                profile = Undo.AddComponent<NewspaperInstructionTextProfile>(prompt.gameObject);
                profile.hideFlags = HideFlags.HideInInspector;
                changed = true;
            }

            prompt.EnsurePersistentVisual();
            Transform instruction = prompt.transform.Find("Instruction");
            if (instruction == null || instruction.GetComponent<TMP_Text>() == null)
                continue;

            NewspaperInstructionTextSettings settings =
                instruction.GetComponent<NewspaperInstructionTextSettings>();

            if (settings == null)
            {
                settings = Undo.AddComponent<NewspaperInstructionTextSettings>(instruction.gameObject);
                changed = true;
            }

            settings.InitializeFromPersistentProfile();

            if (changed)
            {
                EditorUtility.SetDirty(profile);
                EditorUtility.SetDirty(settings);
                EditorUtility.SetDirty(prompt.gameObject);
                EditorSceneManager.MarkSceneDirty(prompt.gameObject.scene);
                anySceneChanged = true;
                configured++;
            }
        }

        if (anySceneChanged)
            AssetDatabase.SaveAssets();

        Debug.Log(
            "[NewspaperInstruction] Editor de textos adicionado em " +
            configured + " prompt(s). Nenhuma rotina automática ficou ativa."
        );
    }

    [MenuItem(MenuPath, true)]
    private static bool ValidateInstallFromMenu()
    {
        return !EditorApplication.isPlayingOrWillChangePlaymode;
    }
}
#endif