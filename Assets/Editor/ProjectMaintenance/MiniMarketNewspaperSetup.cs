#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Configuração manual e idempotente do sistema de jornais.
/// Não executa automaticamente após recompilar, não reconstrói prompts existentes
/// e não salva a cena sozinho. Isso impede o asterisco de reaparecer após Ctrl+S.
/// </summary>
public static class MiniMarketNewspaperSetup
{
    private const string MenuPath =
        "Tools/MiniMarket/Jornal/Configurar Sistema Automaticamente";

    [MenuItem(MenuPath, priority = 2600)]
    public static void ConfigureScene()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("[NewspaperSetup] Saia do Play Mode antes de configurar.");
            return;
        }

        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded)
        {
            Debug.LogError("[NewspaperSetup] Nenhuma cena ativa válida.");
            return;
        }

        Transform[] all = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include);
        List<Transform> stands = FindAllByExactName(all, "Newspaper_Stand", scene);
        List<Transform> putAreas = FindAllByExactName(all, "Put_Area", scene);

        GameObject sourceNewspaper = null;
        bool sceneChanged = false;

        for (int i = 0; i < stands.Count; i++)
        {
            Transform stand = stands[i];
            Transform newspaper = FindChildRecursive(stand, "Jornal");
            NewspaperStandController controller =
                GetOrAddComponent<NewspaperStandController>(stand.gameObject, ref sceneChanged);

            bool controllerChanged = false;

            if (controller.promptAnchor != stand)
            {
                Undo.RecordObject(controller, "Configurar âncora do jornal");
                controller.promptAnchor = stand;
                controllerChanged = true;
            }

            if (!controller.alwaysShowPrompt || !controller.previewPromptInEditMode)
            {
                Undo.RecordObject(controller, "Configurar visualização do prompt");
                controller.alwaysShowPrompt = true;
                controller.previewPromptInEditMode = true;
                controllerChanged = true;
            }

            if (newspaper != null)
            {
                sourceNewspaper ??= newspaper.gameObject;

                if (controller.newspaperVisual != newspaper.gameObject ||
                    controller.interactionPoint != newspaper)
                {
                    Undo.RecordObject(controller, "Vincular jornal do expositor");
                    controller.newspaperVisual = newspaper.gameObject;
                    controller.interactionPoint = newspaper;
                    controllerChanged = true;
                }

                GrabbableItem grabbable = newspaper.GetComponentInChildren<GrabbableItem>(true);
                if (grabbable != null)
                {
                    bool grabbableChanged = grabbable.canBeGrabbed || grabbable.enabled;
                    if (grabbableChanged)
                    {
                        Undo.RecordObject(grabbable, "Configurar jornal do expositor");
                        grabbable.canBeGrabbed = false;
                        grabbable.enabled = false;
                        EditorUtility.SetDirty(grabbable);
                        sceneChanged = true;
                    }

                    if (controller.grabbableItem != grabbable)
                    {
                        Undo.RecordObject(controller, "Vincular GrabbableItem do jornal");
                        controller.grabbableItem = grabbable;
                        controllerChanged = true;
                    }
                }

                InteractionHighlight highlight = newspaper.GetComponent<InteractionHighlight>();
                if (highlight == null)
                {
                    highlight = Undo.AddComponent<InteractionHighlight>(newspaper.gameObject);
                    sceneChanged = true;
                }

                if (controller.highlight != highlight)
                {
                    Undo.RecordObject(controller, "Vincular destaque do jornal");
                    controller.highlight = highlight;
                    controllerChanged = true;
                }
            }

            NewspaperWorldPromptVisual prompt = ResolveStandPrompt(stand, controller);
            if (prompt == null)
            {
                GameObject promptObject = new GameObject(
                    "Newspaper_InteractionPrompt",
                    typeof(RectTransform)
                );
                Undo.RegisterCreatedObjectUndo(promptObject, "Criar prompt persistente de jornal");
                promptObject.transform.SetParent(stand, false);

                prompt = Undo.AddComponent<NewspaperWorldPromptVisual>(promptObject);
                prompt.worldScale = 0.0023f;
                prompt.localOffset = new Vector3(0f, 0.72f, 0f);
                prompt.circleDiameter = 86f;
                prompt.previewInEditMode = true;
                prompt.useRootTransformAsSource = true;
                prompt.useInstructionTransformAsSource = true;
                prompt.useInstructionGraphicAsSource = true;
                prompt.faceCamera = false;
                prompt.sortingOrder = 260;
                promptObject.transform.localPosition = prompt.localOffset;
                promptObject.transform.localScale = Vector3.one * prompt.worldScale;
                sceneChanged = true;
            }

            bool visualMissing = prompt.transform.Find("CircularPrompt") == null ||
                                 prompt.transform.Find("Instruction") == null;
            if (visualMissing)
            {
                prompt.EnsurePersistentVisual();
                sceneChanged = true;
            }

            prompt.SetVisible(true);

            if (controller.promptVisual != prompt)
            {
                Undo.RecordObject(controller, "Vincular prompt persistente");
                controller.promptVisual = prompt;
                controllerChanged = true;
            }

            if (controllerChanged)
            {
                EditorUtility.SetDirty(controller);
                sceneChanged = true;
            }
        }

        for (int i = 0; i < putAreas.Count; i++)
        {
            Transform putArea = putAreas[i];
            NewspaperPlacementAreaController controller =
                GetOrAddComponent<NewspaperPlacementAreaController>(putArea.gameObject, ref sceneChanged);

            bool changed = false;
            string expectedId = BuildHierarchyId(putArea);
            Collider collider = putArea.GetComponent<Collider>();

            if (controller.putArea != putArea ||
                controller.promptAnchor != putArea ||
                controller.areaCollider != collider ||
                controller.placeId != expectedId)
            {
                Undo.RecordObject(controller, "Configurar área de jornal");
                controller.putArea = putArea;
                controller.promptAnchor = putArea;
                controller.areaCollider = collider;
                controller.placeId = expectedId;
                changed = true;
            }

            if (controller.newspaperSourceVisual == null && sourceNewspaper != null)
            {
                Undo.RecordObject(controller, "Vincular fonte do jornal colocado");
                controller.newspaperSourceVisual = sourceNewspaper;
                changed = true;
            }

            if (changed)
            {
                EditorUtility.SetDirty(controller);
                sceneChanged = true;
            }
        }

        sceneChanged |= DisableLegacyEnergyButton(scene);

        // As rotinas abaixo também são manuais e só alteram o que estiver ausente.
        MiniMarketNewspaperPlacePromptPersistence.RepairLoadedScenes(false);
        PersistentPlacedNewspaperSetup.ConfigureLoadedScene(false);
        NewspaperInstructionTextInstaller.InstallFromMenu();

        if (sceneChanged)
            EditorSceneManager.MarkSceneDirty(scene);

        Debug.Log(
            "[NewspaperSetup] Configuração manual concluída. Use Ctrl+S para salvar. " +
            "Nenhuma rotina automática continuará reabrindo a cena como modificada."
        );
    }

    [MenuItem(MenuPath, true)]
    private static bool ValidateConfigureScene()
    {
        return !EditorApplication.isPlayingOrWillChangePlaymode;
    }

    private static NewspaperWorldPromptVisual ResolveStandPrompt(
        Transform stand,
        NewspaperStandController controller)
    {
        if (controller.promptVisual != null &&
            controller.promptVisual.gameObject.scene == stand.gameObject.scene)
        {
            return controller.promptVisual;
        }

        NewspaperWorldPromptVisual[] prompts =
            stand.GetComponentsInChildren<NewspaperWorldPromptVisual>(true);

        for (int i = 0; i < prompts.Length; i++)
        {
            if (prompts[i] != null && prompts[i].name == "Newspaper_InteractionPrompt")
                return prompts[i];
        }

        return null;
    }

    private static T GetOrAddComponent<T>(GameObject target, ref bool sceneChanged)
        where T : Component
    {
        T existing = target.GetComponent<T>();
        if (existing != null)
            return existing;

        sceneChanged = true;
        return Undo.AddComponent<T>(target);
    }

    private static List<Transform> FindAllByExactName(
        Transform[] all,
        string exactName,
        Scene scene)
    {
        List<Transform> result = new List<Transform>();

        for (int i = 0; i < all.Length; i++)
        {
            Transform candidate = all[i];
            if (candidate == null || candidate.name != exactName)
                continue;
            if (candidate.gameObject.scene != scene)
                continue;
            result.Add(candidate);
        }

        return result;
    }

    private static Transform FindChildRecursive(Transform root, string exactName)
    {
        if (root == null)
            return null;

        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);
            if (child.name == exactName)
                return child;

            Transform nested = FindChildRecursive(child, exactName);
            if (nested != null)
                return nested;
        }

        return null;
    }

    private static string BuildHierarchyId(Transform target)
    {
        string path = target != null ? target.name : "PUT_AREA";
        Transform current = target != null ? target.parent : null;

        while (current != null)
        {
            path = current.name + "_" + path;
            current = current.parent;
        }

        string sceneName = target != null && target.gameObject.scene.IsValid()
            ? target.gameObject.scene.name
            : "SCENE";

        return (sceneName + "_" + path)
            .ToUpperInvariant()
            .Replace(' ', '_')
            .Replace('/', '_')
            .Replace('\\', '_');
    }

    private static bool DisableLegacyEnergyButton(Scene scene)
    {
        bool changed = false;
        MiniMarketMenuController[] menus =
            Object.FindObjectsByType<MiniMarketMenuController>(FindObjectsInactive.Include);

        for (int i = 0; i < menus.Length; i++)
        {
            MiniMarketMenuController menu = menus[i];
            if (menu == null || menu.gameObject.scene != scene)
                continue;

            Button button = menu.botaoGemasGratis;
            bool needsChange = menu.gemasGratisRecarregaEnergia ||
                               menu.usarCliqueManualDeSeguranca ||
                               button != null;

            if (!needsChange)
                continue;

            Undo.RecordObject(menu, "Desativar energia grátis");
            menu.gemasGratisRecarregaEnergia = false;
            menu.usarCliqueManualDeSeguranca = false;

            if (button != null)
            {
                Undo.RecordObject(button, "Desativar botão energia grátis");
                button.onClick = new Button.ButtonClickedEvent();
                button.interactable = false;
                button.gameObject.SetActive(false);
            }

            menu.botaoGemasGratis = null;
            EditorUtility.SetDirty(menu);
            changed = true;
        }

        return changed;
    }
}
#endif