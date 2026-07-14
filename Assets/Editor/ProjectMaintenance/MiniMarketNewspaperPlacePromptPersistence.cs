#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Repara manualmente o Newspaper_PlacePrompt persistente.
/// Não roda após recompilar nem ao sair do Play Mode, evitando o ciclo em que
/// o asterisco da cena reaparece depois de Ctrl+S.
/// </summary>
public static class MiniMarketNewspaperPlacePromptPersistence
{
    private const string PromptName = "Newspaper_PlacePrompt";
    private const string MenuPath = "Tools/MiniMarket/Jornal/Reparar Prompt da Put Area";

    [MenuItem(MenuPath, priority = 2601)]
    public static void RepairFromMenu()
    {
        RepairLoadedScenes(true);
    }

    [MenuItem(MenuPath, true)]
    private static bool ValidateRepairFromMenu()
    {
        return !EditorApplication.isPlayingOrWillChangePlaymode;
    }

    public static void RepairLoadedScenes(bool logResult)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        NewspaperPlacementAreaController[] controllers =
            Object.FindObjectsByType<NewspaperPlacementAreaController>(FindObjectsInactive.Include);

        int repaired = 0;

        for (int i = 0; i < controllers.Length; i++)
        {
            NewspaperPlacementAreaController controller = controllers[i];
            if (controller == null)
                continue;

            Scene scene = controller.gameObject.scene;
            if (!scene.IsValid() || !scene.isLoaded)
                continue;

            bool sceneChanged = false;
            Transform anchor = controller.promptAnchor != null
                ? controller.promptAnchor
                : controller.putArea != null
                    ? controller.putArea
                    : controller.transform;

            NewspaperWorldPromptVisual prompt = ResolveExistingPrompt(controller, anchor);

            if (prompt == null)
            {
                GameObject promptObject = new GameObject(PromptName, typeof(RectTransform));
                Undo.RegisterCreatedObjectUndo(promptObject, "Criar prompt persistente da Put Area");
                promptObject.transform.SetParent(anchor, false);

                prompt = Undo.AddComponent<NewspaperWorldPromptVisual>(promptObject);

                float scale = NormalizeScale(controller.promptWorldScale);
                promptObject.transform.localPosition = controller.promptLocalOffset;
                promptObject.transform.localRotation = Quaternion.identity;
                promptObject.transform.localScale = Vector3.one * scale;

                RectTransform rootRect = promptObject.GetComponent<RectTransform>();
                rootRect.sizeDelta = new Vector2(220f, 125f);

                prompt.localOffset = controller.promptLocalOffset;
                prompt.worldScale = scale;
                prompt.sortingOrder = 261;
                prompt.rotationDegreesPerSecond = controller.promptRotationSpeed;
                prompt.previewInEditMode = true;
                prompt.useRootTransformAsSource = true;
                prompt.useInstructionTransformAsSource = true;
                prompt.useInstructionGraphicAsSource = true;
                prompt.faceCamera = false;
                sceneChanged = true;
            }

            bool visualWasMissing = prompt.transform.Find("CircularPrompt") == null ||
                                    prompt.transform.Find("Instruction") == null;
            prompt.EnsurePersistentVisual();
            prompt.SetVisible(true);
            sceneChanged |= visualWasMissing;

            Transform instruction = prompt.transform.Find("Instruction");
            if (instruction != null)
            {
                NewspaperInstructionTextSettings textSettings =
                    instruction.GetComponent<NewspaperInstructionTextSettings>();

                if (textSettings == null)
                {
                    textSettings = Undo.AddComponent<NewspaperInstructionTextSettings>(instruction.gameObject);
                    textSettings.previewState = NewspaperInstructionTextSettings.InstructionState.Placement;
                    textSettings.placementText = controller.promptInstruction;
                    sceneChanged = true;
                }
            }

            if (controller.promptVisual != prompt)
            {
                Undo.RecordObject(controller, "Vincular prompt persistente da Put Area");
                controller.promptVisual = prompt;
                sceneChanged = true;
            }

            if (controller.promptAnchor != anchor)
            {
                Undo.RecordObject(controller, "Vincular âncora do prompt da Put Area");
                controller.promptAnchor = anchor;
                sceneChanged = true;
            }

            if (!sceneChanged)
                continue;

            EditorUtility.SetDirty(prompt);
            EditorUtility.SetDirty(controller);
            EditorSceneManager.MarkSceneDirty(scene);
            repaired++;
        }

        if (logResult)
        {
            Debug.Log(
                "[NewspaperPlacePrompt] Prompts reparados: " + repaired +
                ". A manutenção automática foi desativada para preservar o Ctrl+S."
            );
        }
    }

    private static NewspaperWorldPromptVisual ResolveExistingPrompt(
        NewspaperPlacementAreaController controller,
        Transform anchor)
    {
        if (controller.promptVisual != null &&
            controller.promptVisual.gameObject.scene == controller.gameObject.scene)
        {
            return controller.promptVisual;
        }

        NewspaperWorldPromptVisual[] prompts =
            anchor.GetComponentsInChildren<NewspaperWorldPromptVisual>(true);

        for (int i = 0; i < prompts.Length; i++)
        {
            NewspaperWorldPromptVisual candidate = prompts[i];
            if (candidate != null && candidate.name == PromptName)
                return candidate;
        }

        return null;
    }

    private static float NormalizeScale(float value)
    {
        if (value >= 0.006f)
            return 0.0023f;

        return Mathf.Clamp(value, 0.0001f, 0.02f);
    }
}
#endif