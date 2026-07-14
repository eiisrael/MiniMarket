#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Cria/repara manualmente o Placed_Newspaper_Runtime persistente.
/// Não executa após recompilar, não usa hierarchyChanged e não salva a cena sozinho.
/// </summary>
public static class PersistentPlacedNewspaperSetup
{
    private const string MenuPath =
        "Tools/MiniMarket/Jornal/Reconciliar Jornal Colocado Persistente";

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

    public static void ConfigureLoadedScene(bool logResult)
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

            bool controllerChanged = false;

            if (controller.putArea == null)
            {
                Undo.RecordObject(controller, "Configurar Put_Area persistente");
                controller.putArea = controller.transform;
                controllerChanged = true;
            }

            if (controller.newspaperSourceVisual == null && sourceNewspaper != null)
            {
                Undo.RecordObject(controller, "Configurar fonte do jornal colocado");
                controller.newspaperSourceVisual = sourceNewspaper;
                controllerChanged = true;
            }

            GameObject persistent = ResolvePersistentPlacedVisual(controller);
            bool createdNow = false;

            if (persistent == null)
            {
                GameObject source = controller.newspaperSourceVisual != null
                    ? controller.newspaperSourceVisual
                    : sourceNewspaper;

                if (source == null)
                    continue;

                persistent = Object.Instantiate(source);
                persistent.name = NewspaperPlacementAreaController.PersistentPlacedVisualName;
                persistent.transform.SetParent(controller.putArea, false);
                persistent.transform.localPosition = controller.placedLocalPosition;
                persistent.transform.localRotation = controller.useSourceLocalRotation
                    ? source.transform.localRotation
                    : Quaternion.Euler(controller.placedLocalEuler);
                persistent.transform.localScale = controller.useSourceLocalScale
                    ? source.transform.localScale
                    : controller.placedLocalScale;

                Undo.RegisterCreatedObjectUndo(
                    persistent,
                    "Criar Placed_Newspaper_Runtime persistente"
                );

                SanitizeNewPersistentVisual(persistent);
                createdNow = true;
                sceneChanged = true;
            }

            if (controller.placedNewspaperVisual != persistent)
            {
                Undo.RecordObject(controller, "Vincular jornal persistente");
                controller.placedNewspaperVisual = persistent;
                controllerChanged = true;
            }

            PersistentPlacedNewspaperVisual marker =
                persistent.GetComponent<PersistentPlacedNewspaperVisual>();

            if (marker == null)
            {
                marker = Undo.AddComponent<PersistentPlacedNewspaperVisual>(persistent);
                createdNow = true;
                sceneChanged = true;
            }

            if (!marker.SetupDefaultsApplied)
            {
                Undo.RecordObject(controller, "Ativar edição do jornal persistente");
                controller.keepPlacedVisualInScene = true;
                controller.usePlacedVisualTransformAsSource = true;
                controller.previewPlacedVisualInEditMode = true;

                Undo.RecordObject(marker, "Configurar jornal persistente");
                marker.previewInEditMode = true;
                marker.transformControlledByInspector = true;
                marker.MarkSetupDefaultsApplied();

                controllerChanged = true;
                createdNow = true;
            }

            bool shouldPreview = controller.previewPlacedVisualInEditMode;
            if (persistent.activeSelf != shouldPreview)
            {
                Undo.RecordObject(persistent, "Atualizar preview do jornal persistente");
                persistent.SetActive(shouldPreview);
                sceneChanged = true;
            }

            if (controllerChanged)
            {
                EditorUtility.SetDirty(controller);
                sceneChanged = true;
            }

            if (createdNow)
            {
                EditorUtility.SetDirty(marker);
                EditorUtility.SetDirty(persistent);
            }

            configured++;
        }

        if (sceneChanged)
            EditorSceneManager.MarkSceneDirty(scene);

        if (logResult)
        {
            Debug.Log(
                "[PersistentNewspaperSetup] Jornais persistentes verificados: " + configured +
                ". Use Ctrl+S uma vez; nenhuma manutenção automática continuará alterando a cena."
            );
        }
    }

    private static GameObject ResolvePersistentPlacedVisual(
        NewspaperPlacementAreaController controller)
    {
        GameObject current = controller.placedNewspaperVisual;
        if (IsPersistentPlacedVisual(current))
            return current;

        Transform root = controller.putArea != null ? controller.putArea : controller.transform;
        Transform found = FindPersistentChild(root);
        return found != null ? found.gameObject : null;
    }

    private static GameObject FindSourceNewspaper(Scene scene)
    {
        NewspaperStandController[] stands =
            Object.FindObjectsByType<NewspaperStandController>(FindObjectsInactive.Include);

        for (int i = 0; i < stands.Length; i++)
        {
            NewspaperStandController stand = stands[i];
            if (stand != null && stand.gameObject.scene == scene && stand.newspaperVisual != null)
                return stand.newspaperVisual;
        }

        Transform[] all = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include);
        for (int i = 0; i < all.Length; i++)
        {
            Transform candidate = all[i];
            if (candidate == null || candidate.gameObject.scene != scene ||
                candidate.name != "Newspaper_Stand")
            {
                continue;
            }

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

    private static void SanitizeNewPersistentVisual(GameObject target)
    {
        if (target == null)
            return;

        target.hideFlags = HideFlags.None;

        NewspaperStandController[] standControllers =
            target.GetComponentsInChildren<NewspaperStandController>(true);
        for (int i = 0; i < standControllers.Length; i++)
        {
            if (standControllers[i] != null)
                standControllers[i].enabled = false;
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