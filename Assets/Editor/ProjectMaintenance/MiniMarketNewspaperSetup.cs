#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Persiste e reconcilia na cena os componentes do sistema de jornais.
/// Também é executado uma vez após a recompilação para que o prompt do expositor
/// exista fora do Play Mode e permaneça editável pelo Inspector.
/// </summary>
[InitializeOnLoad]
public static class MiniMarketNewspaperSetup
{
    private const string MenuPath = "Tools/MiniMarket/Jornal/Configurar Sistema Automaticamente";

    static MiniMarketNewspaperSetup()
    {
        EditorApplication.delayCall += AutoConfigureLoadedScene;
    }

    [MenuItem(MenuPath, priority = 2600)]
    public static void ConfigureScene()
    {
        ConfigureSceneInternal(true);
    }

    [MenuItem(MenuPath, true)]
    private static bool ValidateConfigureScene()
    {
        return !EditorApplication.isPlayingOrWillChangePlaymode;
    }

    private static void AutoConfigureLoadedScene()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode ||
            EditorApplication.isCompiling ||
            EditorApplication.isUpdating)
        {
            EditorApplication.delayCall += AutoConfigureLoadedScene;
            return;
        }

        ConfigureSceneInternal(false);
    }

    private static void ConfigureSceneInternal(bool logResult)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            if (logResult)
                Debug.LogWarning("[NewspaperSetup] Saia do Play Mode antes de configurar.");
            return;
        }

        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded)
        {
            if (logResult)
                Debug.LogError("[NewspaperSetup] Nenhuma cena ativa válida.");
            return;
        }

        Transform[] all = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include);
        List<Transform> stands = FindAllByExactName(all, "Newspaper_Stand", scene);
        List<Transform> putAreas = FindAllByExactName(all, "Put_Area", scene);

        if (stands.Count == 0 && putAreas.Count == 0)
            return;

        GameObject sourceNewspaper = null;
        int configuredStands = 0;
        int configuredPlaces = 0;
        bool sceneChanged = false;

        for (int i = 0; i < stands.Count; i++)
        {
            Transform stand = stands[i];
            Transform newspaper = FindChildRecursive(stand, "Jornal");

            NewspaperStandController controller =
                GetOrAddComponent<NewspaperStandController>(stand.gameObject, ref sceneChanged);

            Undo.RecordObject(controller, "Configurar sistema de jornal");
            controller.promptAnchor = stand;
            controller.alwaysShowPrompt = true;
            controller.previewPromptInEditMode = true;

            if (newspaper != null)
            {
                controller.newspaperVisual = newspaper.gameObject;
                controller.interactionPoint = newspaper;

                if (sourceNewspaper == null)
                    sourceNewspaper = newspaper.gameObject;

                GrabbableItem grabbable = newspaper.GetComponentInChildren<GrabbableItem>(true);
                if (grabbable != null)
                {
                    Undo.RecordObject(grabbable, "Configurar jornal do expositor");
                    grabbable.canBeGrabbed = false;
                    grabbable.enabled = false;
                    controller.grabbableItem = grabbable;
                    EditorUtility.SetDirty(grabbable);
                }

                InteractionHighlight highlight = newspaper.GetComponent<InteractionHighlight>();
                if (highlight == null)
                {
                    highlight = Undo.AddComponent<InteractionHighlight>(newspaper.gameObject);
                    sceneChanged = true;
                }

                highlight.focusColor = new Color32(88, 210, 255, 255);
                highlight.activeColor = new Color32(89, 244, 128, 255);
                highlight.tintStrength = 0.55f;
                controller.highlight = highlight;
                EditorUtility.SetDirty(highlight);
            }

            NewspaperWorldPromptVisual prompt =
                EnsurePersistentStandPrompt(stand, ref sceneChanged);

            controller.promptVisual = prompt;

            if (prompt != null)
            {
                controller.promptLocalOffset = prompt.localOffset;
                controller.promptWorldScale = prompt.worldScale;
                controller.promptRotationSpeed = prompt.rotationDegreesPerSecond;
            }

            EditorUtility.SetDirty(controller);
            configuredStands++;
        }

        for (int i = 0; i < putAreas.Count; i++)
        {
            Transform putArea = putAreas[i];
            NewspaperPlacementAreaController controller =
                GetOrAddComponent<NewspaperPlacementAreaController>(putArea.gameObject, ref sceneChanged);

            Undo.RecordObject(controller, "Configurar área de jornal");
            controller.putArea = putArea;
            controller.promptAnchor = putArea;
            controller.areaCollider = putArea.GetComponent<Collider>();
            controller.placeId = BuildHierarchyId(putArea);

            if (sourceNewspaper != null)
                controller.newspaperSourceVisual = sourceNewspaper;

            if (controller.placedNewspaperVisual != null &&
                !controller.placedNewspaperVisual.name.StartsWith("Placed_Newspaper_Runtime"))
            {
                if (controller.placementGuideVisual == null)
                    controller.placementGuideVisual = controller.placedNewspaperVisual;

                controller.placedNewspaperVisual = null;
            }

            if (controller.placementGuideVisual == null)
                controller.placementGuideVisual = putArea.gameObject;

            EditorUtility.SetDirty(controller);
            configuredPlaces++;
        }

        DisableLegacyEnergyButton(scene);

        if (sceneChanged || configuredStands > 0 || configuredPlaces > 0)
        {
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
        }

        if (logResult)
        {
            Debug.Log(
                "[NewspaperSetup] Configuração concluída. Stands=" + configuredStands +
                " | Put_Areas=" + configuredPlaces +
                " | Fonte=" + (sourceNewspaper != null ? sourceNewspaper.name : "não encontrada")
            );

            if (configuredStands == 0)
                Debug.LogWarning("[NewspaperSetup] Objeto 'Newspaper_Stand' não encontrado.");
            if (configuredPlaces == 0)
                Debug.LogWarning("[NewspaperSetup] Objeto 'Put_Area' não encontrado.");
        }
    }

    private static NewspaperWorldPromptVisual EnsurePersistentStandPrompt(
        Transform stand,
        ref bool sceneChanged)
    {
        NewspaperWorldPromptVisual[] promptComponents =
            stand.GetComponentsInChildren<NewspaperWorldPromptVisual>(true);

        NewspaperWorldPromptVisual selected = null;

        for (int i = 0; i < promptComponents.Length; i++)
        {
            NewspaperWorldPromptVisual candidate = promptComponents[i];
            if (candidate != null && candidate.name == "Newspaper_InteractionPrompt")
            {
                selected = candidate;
                break;
            }
        }

        if (selected == null && promptComponents.Length > 0)
        {
            selected = promptComponents[0];
            Undo.RecordObject(selected.gameObject, "Renomear prompt de jornal");
            selected.gameObject.name = "Newspaper_InteractionPrompt";
            sceneChanged = true;
        }

        if (selected == null)
        {
            Transform existingByName = FindChildRecursive(stand, "Newspaper_InteractionPrompt");
            GameObject promptObject;

            if (existingByName != null)
            {
                promptObject = existingByName.gameObject;
            }
            else
            {
                promptObject = new GameObject("Newspaper_InteractionPrompt", typeof(RectTransform));
                Undo.RegisterCreatedObjectUndo(promptObject, "Criar prompt persistente de jornal");
                promptObject.transform.SetParent(stand, false);
                sceneChanged = true;
            }

            selected = promptObject.GetComponent<NewspaperWorldPromptVisual>();
            if (selected == null)
            {
                selected = Undo.AddComponent<NewspaperWorldPromptVisual>(promptObject);
                sceneChanged = true;
            }

            selected.worldScale = 0.0023f;
            selected.localOffset = new Vector3(0f, 0.72f, 0f);
            selected.circleDiameter = 86f;
            selected.instructionOffset = new Vector2(0f, 52f);
            selected.instructionFontSize = 15f;
            selected.centerFontSize = 30f;
        }

        Undo.RecordObject(selected, "Migrar visual do prompt de jornal");
        if (selected.worldScale >= 0.006f)
            selected.worldScale = 0.0023f;
        if (selected.localOffset.y > 1.05f)
            selected.localOffset = new Vector3(
                selected.localOffset.x,
                0.72f,
                selected.localOffset.z
            );
        if (selected.circleDiameter > 100f)
            selected.circleDiameter = 86f;
        if (selected.instructionOffset.y > 65f)
            selected.instructionOffset = new Vector2(selected.instructionOffset.x, 52f);

        selected.previewInEditMode = true;
        selected.useRootTransformAsSource = true;
        selected.faceCamera = false;
        selected.sortingOrder = 260;

        for (int i = 0; i < promptComponents.Length; i++)
        {
            NewspaperWorldPromptVisual candidate = promptComponents[i];
            if (candidate == null || candidate == selected)
                continue;

            Undo.DestroyObjectImmediate(candidate.gameObject);
            sceneChanged = true;
        }

        selected.RebuildVisual();
        selected.SetVisible(true);
        EditorUtility.SetDirty(selected);
        sceneChanged = true;
        return selected;
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

    private static void DisableLegacyEnergyButton(Scene scene)
    {
        MiniMarketMenuController[] menus =
            Object.FindObjectsByType<MiniMarketMenuController>(FindObjectsInactive.Include);

        for (int i = 0; i < menus.Length; i++)
        {
            MiniMarketMenuController menu = menus[i];
            if (menu == null || menu.gameObject.scene != scene)
                continue;

            Undo.RecordObject(menu, "Desativar energia grátis");
            menu.gemasGratisRecarregaEnergia = false;
            menu.usarCliqueManualDeSeguranca = false;

            Button button = menu.botaoGemasGratis;
            if (button != null)
            {
                Undo.RecordObject(button, "Desativar botão energia grátis");
                button.onClick = new Button.ButtonClickedEvent();
                button.interactable = false;
                button.gameObject.SetActive(false);
            }

            menu.botaoGemasGratis = null;
            EditorUtility.SetDirty(menu);
        }
    }
}
#endif
