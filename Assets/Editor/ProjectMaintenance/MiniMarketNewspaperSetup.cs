#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Persiste na cena os componentes do sistema de jornais.
/// Pode ser executado novamente com segurança para reconciliar referências.
/// </summary>
public static class MiniMarketNewspaperSetup
{
    private const string MenuPath = "Tools/MiniMarket/Jornal/Configurar Sistema Automaticamente";

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

        Transform[] all = Object.FindObjectsByType<Transform>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None
        );

        List<Transform> stands = FindAllByExactName(all, "Newspaper_Stand", scene);
        List<Transform> putAreas = FindAllByExactName(all, "Put_Area", scene);

        GameObject sourceNewspaper = null;
        int configuredStands = 0;
        int configuredPlaces = 0;

        for (int i = 0; i < stands.Count; i++)
        {
            Transform stand = stands[i];
            Transform newspaper = FindChildRecursive(stand, "Jornal");

            NewspaperStandController controller =
                GetOrAddComponent<NewspaperStandController>(stand.gameObject);

            controller.promptAnchor = stand;

            if (newspaper != null)
            {
                controller.newspaperVisual = newspaper.gameObject;
                controller.interactionPoint = newspaper;
                sourceNewspaper ??= newspaper.gameObject;

                GrabbableItem grabbable = newspaper.GetComponentInChildren<GrabbableItem>(true);
                if (grabbable != null)
                {
                    Undo.RecordObject(grabbable, "Configurar jornal do expositor");
                    grabbable.canBeGrabbed = false;
                    controller.grabbableItem = grabbable;
                    EditorUtility.SetDirty(grabbable);
                }

                InteractionHighlight highlight = newspaper.GetComponent<InteractionHighlight>();
                if (highlight == null)
                    highlight = Undo.AddComponent<InteractionHighlight>(newspaper.gameObject);

                highlight.focusColor = new Color32(88, 210, 255, 255);
                highlight.activeColor = new Color32(89, 244, 128, 255);
                highlight.tintStrength = 0.55f;
                controller.highlight = highlight;
                EditorUtility.SetDirty(highlight);
            }

            EditorUtility.SetDirty(controller);
            configuredStands++;
        }

        for (int i = 0; i < putAreas.Count; i++)
        {
            Transform putArea = putAreas[i];
            NewspaperPlacementAreaController controller =
                GetOrAddComponent<NewspaperPlacementAreaController>(putArea.gameObject);

            controller.putArea = putArea;
            controller.promptAnchor = putArea;
            controller.areaCollider = putArea.GetComponent<Collider>();
            controller.placeId = BuildHierarchyId(putArea);

            if (sourceNewspaper != null)
                controller.newspaperSourceVisual = sourceNewspaper;

            EditorUtility.SetDirty(controller);
            configuredPlaces++;
        }

        DisableLegacyEnergyButton(scene);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();

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

    [MenuItem(MenuPath, true)]
    private static bool ValidateConfigureScene()
    {
        return !EditorApplication.isPlayingOrWillChangePlaymode;
    }

    private static T GetOrAddComponent<T>(GameObject target) where T : Component
    {
        T existing = target.GetComponent<T>();
        return existing != null ? existing : Undo.AddComponent<T>(target);
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
        MiniMarketMenuController[] menus = Object.FindObjectsByType<MiniMarketMenuController>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None
        );

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
