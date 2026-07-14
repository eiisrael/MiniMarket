#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Garante que cada Put_Area possua um Placed_Newspaper_Runtime persistente,
/// salvo na cena e totalmente editável fora do Play Mode.
/// </summary>
[InitializeOnLoad]
public static class PersistentPlacedNewspaperSetup
{
    private const string MenuPath =
        "Tools/MiniMarket/Jornal/Reconciliar Jornal Colocado Persistente";

    static PersistentPlacedNewspaperSetup()
    {
        EditorApplication.delayCall += AutoConfigureLoadedScene;
    }

    [MenuItem(MenuPath, priority = 2602)]
    public static void ConfigureNow()
    {
        ConfigureLoadedScene(true);
    }

    [MenuItem(MenuPath, true)]
    private static bool ValidateConfigureNow()
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

        ConfigureLoadedScene(false);
    }

    private static void ConfigureLoadedScene(bool logResult)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded)
            return;

        GameObject sourceNewspaper = FindSourceNewspaper(scene);
        NewspaperPlacementAreaController[] controllers =
            Object.FindObjectsByType<NewspaperPlacementAreaController>(FindObjectsInactive.Include);

        int configured = 0;
        bool sceneChanged = false;

        for (int i = 0; i < controllers.Length; i++)
        {
            NewspaperPlacementAreaController controller = controllers[i];
            if (controller == null || controller.gameObject.scene != scene)
                continue;

            if (controller.putArea == null)
            {
                Undo.RecordObject(controller, "Configurar Put_Area persistente");
                controller.putArea = controller.transform;
                sceneChanged = true;
            }

            if (controller.newspaperSourceVisual == null && sourceNewspaper != null)
            {
                Undo.RecordObject(controller, "Configurar fonte do jornal colocado");
                controller.newspaperSourceVisual = sourceNewspaper;
                sceneChanged = true;
            }

            GameObject persistent = EnsurePersistentPlacedVisual(
                controller,
                sourceNewspaper,
                ref sceneChanged
            );

            if (persistent == null)
                continue;

            PersistentPlacedNewspaperVisual marker =
                persistent.GetComponent<PersistentPlacedNewspaperVisual>();

            bool firstConfiguration = marker != null && !marker.SetupDefaultsApplied;
            if (firstConfiguration)
            {
                Undo.RecordObject(controller, "Ativar edição do jornal persistente");
                controller.keepPlacedVisualInScene = true;
                controller.usePlacedVisualTransformAsSource = true;
                controller.previewPlacedVisualInEditMode = true;

                Undo.RecordObject(marker, "Configurar jornal persistente");
                marker.previewInEditMode = true;
                marker.transformControlledByInspector = true;
                marker.MarkSetupDefaultsApplied();

                EditorUtility.SetDirty(marker);
                sceneChanged = true;
            }

            bool shouldPreview = controller.previewPlacedVisualInEditMode;
            if (persistent.activeSelf != shouldPreview)
            {
                Undo.RecordObject(persistent, "Atualizar preview do jornal persistente");
                persistent.SetActive(shouldPreview);
                sceneChanged = true;
            }

            EditorUtility.SetDirty(controller);
            configured++;
        }

        if (sceneChanged)
        {
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
        }

        if (logResult)
        {
            Debug.Log(
                "[PersistentNewspaperSetup] Jornais persistentes reconciliados: " + configured +
                " | Fonte: " + (sourceNewspaper != null ? sourceNewspaper.name : "não encontrada")
            );
        }
    }

    private static GameObject EnsurePersistentPlacedVisual(
        NewspaperPlacementAreaController controller,
        GameObject fallbackSource,
        ref bool sceneChanged)
    {
        Transform putArea = controller.putArea != null
            ? controller.putArea
            : controller.transform;

        GameObject persistent = controller.placedNewspaperVisual;
        if (persistent == null || !IsPersistentPlacedVisual(persistent))
        {
            Transform existing = FindPersistentChild(putArea);
            persistent = existing != null ? existing.gameObject : null;
        }

        if (persistent == null)
        {
            GameObject source = controller.newspaperSourceVisual != null
                ? controller.newspaperSourceVisual
                : fallbackSource;

            if (source == null)
                return null;

            persistent = Object.Instantiate(source);
            persistent.name = NewspaperPlacementAreaController.PersistentPlacedVisualName;
            persistent.transform.SetParent(putArea, false);

            persistent.transform.localPosition = controller.placedLocalPosition;
            persistent.transform.localRotation =
                controller.useSourceLocalRotation && source != null
                    ? source.transform.localRotation
                    : Quaternion.Euler(controller.placedLocalEuler);
            persistent.transform.localScale =
                controller.useSourceLocalScale && source != null
                    ? source.transform.localScale
                    : controller.placedLocalScale;

            Undo.RegisterCreatedObjectUndo(
                persistent,
                "Criar Placed_Newspaper_Runtime persistente"
            );
            sceneChanged = true;
        }

        if (controller.placedNewspaperVisual != persistent)
        {
            Undo.RecordObject(controller, "Vincular jornal persistente");
            controller.placedNewspaperVisual = persistent;
            sceneChanged = true;
        }

        PersistentPlacedNewspaperVisual marker =
            persistent.GetComponent<PersistentPlacedNewspaperVisual>();
        if (marker == null)
        {
            marker = Undo.AddComponent<PersistentPlacedNewspaperVisual>(persistent);
            sceneChanged = true;
        }

        ClearHideFlagsRecursively(persistent.transform);
        SanitizePersistentPlacedVisual(persistent);
        EditorUtility.SetDirty(persistent);
        return persistent;
    }

    private static GameObject FindSourceNewspaper(Scene scene)
    {
        Transform[] all = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include);

        for (int i = 0; i < all.Length; i++)
        {
            Transform candidate = all[i];
            if (candidate == null || candidate.gameObject.scene != scene)
                continue;
            if (candidate.name != "Newspaper_Stand")
                continue;

            Transform newspaper = FindChildRecursive(candidate, "Jornal");
            if (newspaper != null)
                return newspaper.gameObject;
        }

        return null;
    }

    private static Transform FindPersistentChild(Transform root)
    {
        if (root == null)
            return null;

        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);
            if (child.name.StartsWith(
                    NewspaperPlacementAreaController.PersistentPlacedVisualName,
                    System.StringComparison.Ordinal))
            {
                return child;
            }

            Transform nested = FindPersistentChild(child);
            if (nested != null)
                return nested;
        }

        return null;
    }

    private static bool IsPersistentPlacedVisual(GameObject target)
    {
        return target != null && target.name.StartsWith(
            NewspaperPlacementAreaController.PersistentPlacedVisualName,
            System.StringComparison.Ordinal
        );
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

    private static void ClearHideFlagsRecursively(Transform root)
    {
        if (root == null)
            return;

        root.gameObject.hideFlags = HideFlags.None;
        Component[] components = root.GetComponents<Component>();
        for (int i = 0; i < components.Length; i++)
        {
            if (components[i] != null)
                components[i].hideFlags = HideFlags.None;
        }

        for (int i = 0; i < root.childCount; i++)
            ClearHideFlagsRecursively(root.GetChild(i));
    }

    private static void SanitizePersistentPlacedVisual(GameObject target)
    {
        NewspaperStandController[] standControllers =
            target.GetComponentsInChildren<NewspaperStandController>(true);
        for (int i = 0; i < standControllers.Length; i++)
        {
            if (standControllers[i] != null)
                standControllers[i].enabled = false;
        }

        NewspaperPlacementAreaController[] placementControllers =
            target.GetComponentsInChildren<NewspaperPlacementAreaController>(true);
        for (int i = 0; i < placementControllers.Length; i++)
        {
            NewspaperPlacementAreaController value = placementControllers[i];
            if (value != null && value.gameObject != target)
                value.enabled = false;
        }

        NewspaperWorldPromptVisual[] prompts =
            target.GetComponentsInChildren<NewspaperWorldPromptVisual>(true);
        for (int i = 0; i < prompts.Length; i++)
        {
            if (prompts[i] == null)
                continue;
            prompts[i].SetVisible(false);
            prompts[i].enabled = false;
        }

        NewspaperInstructionTextSettings[] textSettings =
            target.GetComponentsInChildren<NewspaperInstructionTextSettings>(true);
        for (int i = 0; i < textSettings.Length; i++)
        {
            if (textSettings[i] != null)
                textSettings[i].enabled = false;
        }

        GrabbableItem[] grabbables = target.GetComponentsInChildren<GrabbableItem>(true);
        for (int i = 0; i < grabbables.Length; i++)
        {
            if (grabbables[i] == null)
                continue;
            grabbables[i].canBeGrabbed = false;
            grabbables[i].enabled = false;
            grabbables[i].SetSelected(false);
        }

        InteractionHighlight[] highlights =
            target.GetComponentsInChildren<InteractionHighlight>(true);
        for (int i = 0; i < highlights.Length; i++)
        {
            if (highlights[i] == null)
                continue;
            highlights[i].Clear();
            highlights[i].enabled = false;
        }

        Collider[] colliders = target.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] != null)
                colliders[i].enabled = false;
        }

        Rigidbody[] bodies = target.GetComponentsInChildren<Rigidbody>(true);
        for (int i = 0; i < bodies.Length; i++)
        {
            Rigidbody body = bodies[i];
            if (body == null)
                continue;

            body.useGravity = false;
            body.isKinematic = true;
        }
    }
}
#endif
