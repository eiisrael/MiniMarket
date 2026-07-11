#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>
/// Migração única para ligar os sistemas desta atualização na cena atual.
/// Não altera scripts ou assets dentro de Brick Project Studio.
/// </summary>
[InitializeOnLoad]
public static class GameplaySystemsSetup
{
    private const string SetupKey = "MiniMarket.GameplaySystemsSetup.2026-07-11.v1";

    static GameplaySystemsSetup()
    {
        EditorApplication.delayCall += TryRunAutomatically;
    }

    [MenuItem("Tools/Game Systems/Repair Data HUD Interactions and Mobile", priority = 1)]
    public static void RunFromMenu()
    {
        EditorPrefs.DeleteKey(SetupKey);
        Run();
    }

    [MenuItem("Tools/Game Systems/Reset Automatic Setup Marker", priority = 20)]
    public static void ResetMarker()
    {
        EditorPrefs.DeleteKey(SetupKey);
        Debug.Log("[GameplaySystemsSetup] Marcador automático removido.");
    }

    private static void TryRunAutomatically()
    {
        if (EditorPrefs.GetBool(SetupKey, false))
            return;

        if (EditorApplication.isPlayingOrWillChangePlaymode ||
            EditorApplication.isCompiling ||
            EditorApplication.isUpdating)
        {
            EditorApplication.delayCall += TryRunAutomatically;
            return;
        }

        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded || string.IsNullOrEmpty(scene.path))
            return;

        Run();
    }

    private static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("[GameplaySystemsSetup] Saia do Play Mode antes do reparo.");
            return;
        }

        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded || string.IsNullOrEmpty(scene.path))
        {
            Debug.LogWarning("[GameplaySystemsSetup] Abra uma cena salva antes do reparo.");
            return;
        }

        CameraRelativeMovement movement =
            Object.FindAnyObjectByType<CameraRelativeMovement>(FindObjectsInactive.Include);
        PlayerCameraController cameraController =
            Object.FindAnyObjectByType<PlayerCameraController>(FindObjectsInactive.Include);

        if (movement == null)
        {
            Debug.LogWarning(
                "[GameplaySystemsSetup] CameraRelativeMovement não encontrado. " +
                "Execute Tools > Player System > Create or Repair Player System e tente novamente."
            );
            return;
        }

        int highlights = 0;
        int interactives = 0;
        int huds = 0;
        bool changed = false;

        try
        {
            EditorSceneManager.SaveOpenScenes();
            CrossSceneReferenceCleaner.CleanLoadedScenes();

            changed |= ConfigureMovement(movement);
            changed |= ConfigureCameraRig(cameraController, movement);
            changed |= ConfigureEnergyHud(movement, ref huds);
            changed |= ConfigureInteractionObjects(movement, ref highlights, ref interactives);
            changed |= RemoveObsoleteRuntimeGuards();

            if (changed)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                AssetDatabase.SaveAssets();
            }

            EditorPrefs.SetBool(SetupKey, true);

            Debug.Log(
                "[GameplaySystemsSetup] Concluído. HUDs=" + huds +
                ", highlights=" + highlights +
                ", interativos=" + interactives +
                ", cena alterada=" + changed + "."
            );
        }
        catch (Exception exception)
        {
            Debug.LogError("[GameplaySystemsSetup] Falha: " + exception);
        }
    }

    private static bool ConfigureMovement(CameraRelativeMovement movement)
    {
        bool changed = false;

        changed |= SetIfDifferent(ref movement.usePlayerDatabase, true);
        changed |= SetIfDifferent(ref movement.useSegmentedEnergy, true);
        changed |= SetIfDifferent(ref movement.maxEnergySegments, Mathf.Max(5, movement.maxEnergySegments));
        changed |= SetIfDifferent(ref movement.useLegacyInput, true);

        if (movement.animator == null)
        {
            Animator animator = movement.GetComponentInChildren<Animator>(true);
            if (animator != null)
            {
                movement.animator = animator;
                changed = true;
            }
        }

        if (changed)
            EditorUtility.SetDirty(movement);

        return changed;
    }

    private static bool ConfigureCameraRig(
        PlayerCameraController cameraController,
        CameraRelativeMovement movement)
    {
        if (cameraController == null)
            return false;

        bool changed = false;
        Camera camera = cameraController.gameCamera != null
            ? cameraController.gameCamera
            : cameraController.GetComponent<Camera>();

        if (cameraController.player == null)
        {
            cameraController.player = movement.transform;
            changed = true;
        }

        if (cameraController.movement != movement)
        {
            cameraController.movement = movement;
            changed = true;
        }

        GetItemController getItem = cameraController.GetComponent<GetItemController>();
        if (getItem == null)
        {
            getItem = cameraController.gameObject.AddComponent<GetItemController>();
            changed = true;
        }

        if (getItem.cameraSource != camera)
        {
            getItem.cameraSource = camera;
            changed = true;
        }
        if (getItem.cameraController != cameraController)
        {
            getItem.cameraController = cameraController;
            changed = true;
        }
        if (getItem.playerRoot != movement.transform)
        {
            getItem.playerRoot = movement.transform;
            changed = true;
        }
        if (getItem.onlyInFirstPerson)
        {
            getItem.onlyInFirstPerson = false;
            changed = true;
        }

        InteractionFocusController focus = cameraController.GetComponent<InteractionFocusController>();
        if (focus == null)
        {
            focus = cameraController.gameObject.AddComponent<InteractionFocusController>();
            changed = true;
        }

        if (focus.cameraSource != camera)
        {
            focus.cameraSource = camera;
            changed = true;
        }
        if (focus.cameraController != cameraController)
        {
            focus.cameraController = cameraController;
            changed = true;
        }
        if (focus.playerRoot != movement.transform)
        {
            focus.playerRoot = movement.transform;
            changed = true;
        }
        if (focus.getItemController != getItem)
        {
            focus.getItemController = getItem;
            changed = true;
        }

        if (changed)
        {
            EditorUtility.SetDirty(cameraController);
            EditorUtility.SetDirty(getItem);
            EditorUtility.SetDirty(focus);
        }

        return changed;
    }

    private static bool ConfigureEnergyHud(CameraRelativeMovement movement, ref int configuredCount)
    {
        bool changed = false;
        MiniMarketEnergySegmentHUD[] huds =
            Object.FindObjectsByType<MiniMarketEnergySegmentHUD>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None
            );

        for (int i = 0; i < huds.Length; i++)
        {
            MiniMarketEnergySegmentHUD hud = huds[i];
            if (hud == null)
                continue;

            configuredCount++;
            if (hud.movimento != movement)
            {
                hud.movimento = movement;
                EditorUtility.SetDirty(hud);
                changed = true;
            }
        }

        if (huds.Length > 0)
            return changed;

        Text candidate = FindEnergyTextCandidate();
        if (candidate == null)
            return changed;

        MiniMarketEnergySegmentHUD created = candidate.GetComponent<MiniMarketEnergySegmentHUD>();
        if (created == null)
        {
            created = candidate.gameObject.AddComponent<MiniMarketEnergySegmentHUD>();
            changed = true;
        }

        created.textoEnergia = candidate;
        created.movimento = movement;
        EditorUtility.SetDirty(created);
        configuredCount++;
        return true;
    }

    private static Text FindEnergyTextCandidate()
    {
        Text[] texts = Object.FindObjectsByType<Text>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None
        );

        Text fallback = null;
        for (int i = 0; i < texts.Length; i++)
        {
            Text text = texts[i];
            if (text == null || text.GetComponentInParent<Canvas>() == null)
                continue;

            string lower = text.name.ToLowerInvariant();
            if (lower.Contains("energy") || lower.Contains("energia") || lower.Contains("stamina"))
                return text;

            if (fallback == null && (text.text.Contains("/") || text.text.Contains("%")))
                fallback = text;
        }

        return fallback;
    }

    private static bool ConfigureInteractionObjects(
        CameraRelativeMovement movement,
        ref int highlightCount,
        ref int interactiveCount)
    {
        bool changed = false;

        GrabbableItem[] grabbables = Object.FindObjectsByType<GrabbableItem>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None
        );

        for (int i = 0; i < grabbables.Length; i++)
        {
            GrabbableItem item = grabbables[i];
            if (!IsSceneObject(item != null ? item.gameObject : null))
                continue;

            InteractionHighlight highlight = item.GetComponent<InteractionHighlight>();
            if (highlight == null)
            {
                highlight = item.gameObject.AddComponent<InteractionHighlight>();
                changed = true;
                highlightCount++;
            }

            if (item.highlight != highlight)
            {
                item.highlight = highlight;
                EditorUtility.SetDirty(item);
                changed = true;
            }
        }

        GameObject[] allObjects = Object.FindObjectsByType<GameObject>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None
        );

        for (int i = 0; i < allObjects.Length; i++)
        {
            GameObject target = allObjects[i];
            if (!IsCandidateInteractiveObject(target, movement.transform))
                continue;

            if (target.GetComponentInParent<GrabbableItem>() != null)
                continue;

            InteractionHighlight highlight = target.GetComponent<InteractionHighlight>();
            if (highlight == null)
            {
                highlight = target.AddComponent<InteractionHighlight>();
                changed = true;
                highlightCount++;
            }

            InteractiveObject interactive = target.GetComponent<InteractiveObject>();
            if (interactive == null)
            {
                interactive = target.AddComponent<InteractiveObject>();
                interactive.displayName = target.name;
                changed = true;
                interactiveCount++;
            }

            if (interactive.highlight != highlight)
            {
                interactive.highlight = highlight;
                EditorUtility.SetDirty(interactive);
                changed = true;
            }
        }

        return changed;
    }

    private static bool IsCandidateInteractiveObject(GameObject target, Transform playerRoot)
    {
        if (!IsSceneObject(target) || target.transform == playerRoot || target.transform.IsChildOf(playerRoot))
            return false;

        if (target.GetComponentInParent<Canvas>() != null || target.GetComponent<Camera>() != null)
            return false;

        if (target.GetComponentInChildren<Renderer>(true) == null ||
            target.GetComponentInChildren<Collider>(true) == null)
        {
            return false;
        }

        string lower = target.name.ToLowerInvariant();
        if (ContainsAny(
                lower,
                "porta", "door", "gate", "portao", "portão",
                "caixa", "box", "crate", "package", "pacote",
                "interruptor", "switch", "lever", "alavanca",
                "drawer", "gaveta", "armario", "armário", "cabinet"))
        {
            return true;
        }

        MonoBehaviour[] behaviours = target.GetComponents<MonoBehaviour>();
        for (int i = 0; i < behaviours.Length; i++)
        {
            MonoBehaviour behaviour = behaviours[i];
            if (behaviour == null)
                continue;

            string typeName = behaviour.GetType().Name.ToLowerInvariant();
            if (ContainsAny(typeName, "door", "porta", "interact", "open", "toggle", "switch"))
                return true;
        }

        return false;
    }

    private static bool RemoveObsoleteRuntimeGuards()
    {
        bool changed = false;
        MiniMarketSegmentedStaminaRuntimeGuard[] guards =
            Object.FindObjectsByType<MiniMarketSegmentedStaminaRuntimeGuard>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None
            );

        for (int i = 0; i < guards.Length; i++)
        {
            MiniMarketSegmentedStaminaRuntimeGuard guard = guards[i];
            if (guard == null || !IsSceneObject(guard.gameObject))
                continue;

            Object.DestroyImmediate(guard);
            changed = true;
        }

        return changed;
    }

    private static bool IsSceneObject(GameObject target)
    {
        return target != null &&
               target.scene.IsValid() &&
               target.scene.isLoaded &&
               !string.Equals(target.scene.name, "DontDestroyOnLoad", StringComparison.Ordinal);
    }

    private static bool ContainsAny(string value, params string[] terms)
    {
        if (string.IsNullOrEmpty(value))
            return false;

        for (int i = 0; i < terms.Length; i++)
        {
            if (value.Contains(terms[i]))
                return true;
        }

        return false;
    }

    private static bool SetIfDifferent(ref bool current, bool desired)
    {
        if (current == desired)
            return false;

        current = desired;
        return true;
    }

    private static bool SetIfDifferent(ref int current, int desired)
    {
        if (current == desired)
            return false;

        current = desired;
        return true;
    }
}
#endif
