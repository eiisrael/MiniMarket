#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// Garante que o reparo de dados/HUD/interações seja executado depois que o
/// PlayerSystemSetupWizard recriar o PlayerCameraRig em um projeto novo ou cena nova.
/// </summary>
[InitializeOnLoad]
public static class GameplaySystemsAutoRepair
{
    private const int MaxAttempts = 20;
    private static int attempts;
    private static bool executed;

    static GameplaySystemsAutoRepair()
    {
        EditorApplication.delayCall += Check;
    }

    private static void Check()
    {
        if (executed)
            return;

        if (EditorApplication.isPlayingOrWillChangePlaymode ||
            EditorApplication.isCompiling ||
            EditorApplication.isUpdating)
        {
            ScheduleRetry();
            return;
        }

        CameraRelativeMovement movement =
            Object.FindAnyObjectByType<CameraRelativeMovement>(FindObjectsInactive.Include);
        PlayerCameraController cameraController =
            Object.FindAnyObjectByType<PlayerCameraController>(FindObjectsInactive.Include);

        if (movement == null || cameraController == null)
        {
            ScheduleRetry();
            return;
        }

        bool needsRepair =
            cameraController.GetComponent<GetItemController>() == null ||
            cameraController.GetComponent<InteractionFocusController>() == null ||
            !movement.usePlayerDatabase ||
            !movement.useSegmentedEnergy;

        if (!needsRepair)
        {
            MiniMarketEnergySegmentHUD[] huds = Object.FindObjectsByType<MiniMarketEnergySegmentHUD>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None
            );

            for (int i = 0; i < huds.Length; i++)
            {
                if (huds[i] != null && huds[i].movimento != movement)
                {
                    needsRepair = true;
                    break;
                }
            }
        }

        if (!needsRepair)
        {
            GrabbableItem[] items = Object.FindObjectsByType<GrabbableItem>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None
            );

            for (int i = 0; i < items.Length; i++)
            {
                if (items[i] != null && items[i].GetComponent<InteractionHighlight>() == null)
                {
                    needsRepair = true;
                    break;
                }
            }
        }

        executed = true;

        if (needsRepair)
            EditorApplication.ExecuteMenuItem("Tools/Game Systems/Repair Data HUD Interactions and Mobile");
    }

    private static void ScheduleRetry()
    {
        attempts++;
        if (attempts < MaxAttempts)
            EditorApplication.delayCall += Check;
    }
}
#endif
