#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Instala uma única vez a camada visual premium nos prompts de pegar e colocar jornal.
/// A rotina é idempotente: depois que o componente e a hierarquia existem, não altera
/// novamente a cena ao recompilar nem faz o asterisco reaparecer após Ctrl+S.
/// </summary>
[InitializeOnLoad]
internal static class NewspaperPromptPremiumKeyInstaller
{
    private const string MenuPath =
        "Tools/MiniMarket/Jornal/Instalar Visual Premium da Tecla E";

    private static bool installScheduled;

    static NewspaperPromptPremiumKeyInstaller()
    {
        ScheduleAutomaticInstall();
        EditorApplication.playModeStateChanged += HandlePlayModeStateChanged;
    }

    [MenuItem(MenuPath, priority = 2604)]
    private static void InstallFromMenu()
    {
        InstallLoadedPrompts(true, true);
    }

    [MenuItem(MenuPath, true)]
    private static bool ValidateInstallFromMenu()
    {
        return !EditorApplication.isPlayingOrWillChangePlaymode &&
               !EditorApplication.isCompiling &&
               !EditorApplication.isUpdating;
    }

    private static void HandlePlayModeStateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredEditMode)
            ScheduleAutomaticInstall();
    }

    private static void ScheduleAutomaticInstall()
    {
        if (installScheduled)
            return;

        installScheduled = true;
        EditorApplication.delayCall += RunScheduledInstall;
    }

    private static void RunScheduledInstall()
    {
        installScheduled = false;

        if (EditorApplication.isPlayingOrWillChangePlaymode ||
            EditorApplication.isCompiling ||
            EditorApplication.isUpdating)
        {
            ScheduleAutomaticInstall();
            return;
        }

        InstallLoadedPrompts(false, false);
    }

    private static void InstallLoadedPrompts(bool logResult, bool forceDefaults)
    {
        NewspaperWorldPromptVisual[] prompts =
            Object.FindObjectsByType<NewspaperWorldPromptVisual>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None
            );

        int installedCount = 0;
        int updatedCount = 0;

        for (int i = 0; i < prompts.Length; i++)
        {
            NewspaperWorldPromptVisual prompt = prompts[i];
            if (!IsPersistentNewspaperPrompt(prompt))
                continue;

            Scene scene = prompt.gameObject.scene;
            if (!scene.IsValid() || !scene.isLoaded || string.IsNullOrEmpty(scene.path))
                continue;

            NewspaperPromptPremiumKeyVisual premium =
                prompt.GetComponent<NewspaperPromptPremiumKeyVisual>();

            bool componentAdded = premium == null;
            bool hierarchyMissing = prompt.transform.Find(
                "CircularPrompt/PremiumKeyVisual"
            ) == null;

            if (!componentAdded && !hierarchyMissing && !forceDefaults)
                continue;

            if (componentAdded)
            {
                premium = Undo.AddComponent<NewspaperPromptPremiumKeyVisual>(
                    prompt.gameObject
                );
                installedCount++;
            }
            else
            {
                Undo.RecordObject(premium, "Atualizar visual premium da tecla E");
            }

            bool changed = premium.EnsureEditableHierarchy(forceDefaults);

            if (!changed && !componentAdded)
                continue;

            EditorUtility.SetDirty(premium);
            EditorUtility.SetDirty(prompt);
            EditorSceneManager.MarkSceneDirty(scene);
            updatedCount++;
        }

        if (logResult || installedCount > 0 || updatedCount > 0)
        {
            Debug.Log(
                "[NewspaperPremiumKey] Instalados: " + installedCount +
                ", atualizados: " + updatedCount +
                ". O visual é editável na Hierarchy; use Ctrl+S para salvar a cena."
            );
        }
    }

    private static bool IsPersistentNewspaperPrompt(NewspaperWorldPromptVisual prompt)
    {
        if (prompt == null)
            return false;

        string objectName = prompt.name;
        return objectName == "Newspaper_InteractionPrompt" ||
               objectName == "Newspaper_PlacePrompt" ||
               HasNewspaperOwner(prompt.transform);
    }

    private static bool HasNewspaperOwner(Transform target)
    {
        Transform current = target;

        while (current != null)
        {
            if (current.GetComponent<NewspaperStandController>() != null ||
                current.GetComponent<NewspaperPlacementAreaController>() != null)
            {
                return true;
            }

            current = current.parent;
        }

        return false;
    }
}
#endif
