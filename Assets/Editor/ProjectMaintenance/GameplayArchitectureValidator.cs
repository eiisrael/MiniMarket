#if UNITY_EDITOR
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>
/// Validação sem efeitos colaterais. Não cria nem remove componentes.
/// Use depois do git pull e antes do Play para detectar configuração incompleta.
/// </summary>
public static class GameplayArchitectureValidator
{
    [MenuItem("Tools/Game Systems/Validate Current Architecture", priority = 2)]
    public static void Validate()
    {
        StringBuilder report = new StringBuilder();
        int errors = 0;
        int warnings = 0;

        CameraRelativeMovement movement =
            Object.FindAnyObjectByType<CameraRelativeMovement>(FindObjectsInactive.Include);
        PlayerCameraController cameraController =
            Object.FindAnyObjectByType<PlayerCameraController>(FindObjectsInactive.Include);
        MiniMarketEnergySegmentHUD[] huds =
            Object.FindObjectsByType<MiniMarketEnergySegmentHUD>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None
            );

        Check(movement != null, "CameraRelativeMovement encontrado", true, ref errors, ref warnings, report);
        Check(cameraController != null, "PlayerCameraController encontrado", true, ref errors, ref warnings, report);

        if (movement != null)
        {
            Check(movement.GetComponent<CharacterController>() != null,
                "CharacterController no jogador", true, ref errors, ref warnings, report);
            Check(movement.animator != null,
                "Animator ligado ao movimento", true, ref errors, ref warnings, report);
            Check(movement.usePlayerDatabase,
                "Persistência de stamina habilitada", true, ref errors, ref warnings, report);
            Check(movement.useSegmentedEnergy,
                "Energia segmentada habilitada", false, ref errors, ref warnings, report);
        }

        if (cameraController != null)
        {
            Check(cameraController.GetComponent<Camera>() != null,
                "Camera no PlayerCameraRig", true, ref errors, ref warnings, report);
            Check(cameraController.GetComponent<AudioListener>() != null,
                "AudioListener no PlayerCameraRig", true, ref errors, ref warnings, report);
            Check(cameraController.GetComponent<GetItemController>() != null,
                "GetItemController no PlayerCameraRig", true, ref errors, ref warnings, report);
            Check(cameraController.GetComponent<InteractionFocusController>() != null,
                "InteractionFocusController no PlayerCameraRig", true, ref errors, ref warnings, report);
        }

        Check(huds.Length > 0, "HUD de energia encontrado", true, ref errors, ref warnings, report);

        for (int i = 0; i < huds.Length; i++)
        {
            MiniMarketEnergySegmentHUD hud = huds[i];
            if (hud == null)
                continue;

            Check(hud.textoEnergia != null,
                "HUD " + hud.name + " possui texto", false, ref errors, ref warnings, report);
            Check(hud.movimento != null,
                "HUD " + hud.name + " ligado ao movimento", false, ref errors, ref warnings, report);
        }

        Camera[] cameras = Object.FindObjectsByType<Camera>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        AudioListener[] listeners = Object.FindObjectsByType<AudioListener>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        int enabledCameras = CountEnabled(cameras);
        int enabledListeners = CountEnabled(listeners);

        Check(enabledCameras <= 1,
            "No máximo uma Camera ativa no Edit Mode (atual=" + enabledCameras + ")",
            false, ref errors, ref warnings, report);
        Check(enabledListeners <= 1,
            "No máximo um AudioListener ativo no Edit Mode (atual=" + enabledListeners + ")",
            false, ref errors, ref warnings, report);

        GrabbableItem[] grabbables = Object.FindObjectsByType<GrabbableItem>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None
        );

        for (int i = 0; i < grabbables.Length; i++)
        {
            GrabbableItem item = grabbables[i];
            if (item == null)
                continue;

            Check(item.GetComponent<InteractionHighlight>() != null,
                "Grabbable " + item.name + " possui InteractionHighlight",
                false, ref errors, ref warnings, report);
            Check(item.GetComponentInChildren<Collider>(true) != null,
                "Grabbable " + item.name + " possui Collider",
                true, ref errors, ref warnings, report);
        }

        Canvas[] canvases = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < canvases.Length; i++)
        {
            Canvas canvas = canvases[i];
            if (canvas == null || !canvas.isRootCanvas)
                continue;

            CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
            Check(scaler != null,
                "Canvas raiz " + canvas.name + " possui CanvasScaler",
                false, ref errors, ref warnings, report);
        }

        report.Insert(0,
            "[GameplayArchitectureValidator] Erros=" + errors +
            ", avisos=" + warnings + "\n");

        if (errors > 0)
            Debug.LogError(report.ToString());
        else if (warnings > 0)
            Debug.LogWarning(report.ToString());
        else
            Debug.Log(report.ToString());
    }

    private static int CountEnabled<T>(T[] components) where T : Behaviour
    {
        int count = 0;
        for (int i = 0; i < components.Length; i++)
        {
            T component = components[i];
            if (component != null && component.enabled && component.gameObject.activeInHierarchy)
                count++;
        }
        return count;
    }

    private static void Check(
        bool condition,
        string description,
        bool critical,
        ref int errors,
        ref int warnings,
        StringBuilder report)
    {
        if (condition)
        {
            report.AppendLine("[OK] " + description);
            return;
        }

        if (critical)
        {
            errors++;
            report.AppendLine("[ERRO] " + description);
        }
        else
        {
            warnings++;
            report.AppendLine("[AVISO] " + description);
        }
    }
}
#endif
