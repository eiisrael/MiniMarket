#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>
/// Reparo não destrutivo dos recursos recuperados após a reorganização de scripts.
/// Não remove objetos, não muda GUIDs e não toca em Brick Project Studio.
/// </summary>
public static class GameplayFeatureRepair
{
    [MenuItem("Tools/Game Systems/Repair Purchase Minimap Diagnostics Energy Reticle", priority = 3)]
    public static void RepairCurrentScene()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
        {
            Debug.LogWarning("[GameplayFeatureRepair] Saia do Play Mode e aguarde a compilação terminar.");
            return;
        }

        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded || string.IsNullOrEmpty(scene.path))
        {
            Debug.LogWarning("[GameplayFeatureRepair] Abra e salve a cena antes do reparo.");
            return;
        }

        bool changed = false;
        int triggerCount = 0;
        int markerCount = 0;
        int hudCount = 0;

        CameraRelativeMovement movement = Object.FindAnyObjectByType<CameraRelativeMovement>(FindObjectsInactive.Include);
        PlayerCameraController playerCamera = Object.FindAnyObjectByType<PlayerCameraController>(FindObjectsInactive.Include);

        BuySceneEntryTrigger[] triggers = Object.FindObjectsByType<BuySceneEntryTrigger>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None
        );

        BuyableLandAreaMarker[] markers = Object.FindObjectsByType<BuyableLandAreaMarker>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None
        );

        BuySceneCameraModeController purchaseController =
            Object.FindAnyObjectByType<BuySceneCameraModeController>(FindObjectsInactive.Include);

        if (purchaseController == null && (triggers.Length > 0 || markers.Length > 0))
        {
            GameObject host = new GameObject("BuySceneSystem");
            purchaseController = host.AddComponent<BuySceneCameraModeController>();
            Undo.RegisterCreatedObjectUndo(host, "Criar BuySceneSystem");
            changed = true;
        }

        if (purchaseController != null)
        {
            if (purchaseController.cameraPrincipal == null && playerCamera != null)
            {
                purchaseController.cameraPrincipal = playerCamera.gameCamera;
                changed = true;
            }

            if (purchaseController.jogadorRaiz == null && movement != null)
            {
                purchaseController.jogadorRaiz = movement.transform;
                changed = true;
            }

            PurchaseModeBridge bridge = purchaseController.GetComponent<PurchaseModeBridge>();
            if (bridge == null)
            {
                bridge = Undo.AddComponent<PurchaseModeBridge>(purchaseController.gameObject);
                changed = true;
            }

            if (bridge.purchaseController != purchaseController)
            {
                bridge.purchaseController = purchaseController;
                changed = true;
            }

            if (bridge.playerCamera != playerCamera)
            {
                bridge.playerCamera = playerCamera;
                changed = true;
            }

            if (bridge.movement != movement)
            {
                bridge.movement = movement;
                changed = true;
            }

            EditorUtility.SetDirty(purchaseController);
            EditorUtility.SetDirty(bridge);
        }

        for (int i = 0; i < triggers.Length; i++)
        {
            BuySceneEntryTrigger trigger = triggers[i];
            if (trigger == null)
                continue;

            triggerCount++;

            if (purchaseController != null && trigger.controladorBuyScene != purchaseController)
            {
                trigger.controladorBuyScene = purchaseController;
                changed = true;
            }

            if (movement != null && trigger.jogadorRaizOpcional != movement.transform)
            {
                trigger.jogadorRaizOpcional = movement.transform;
                changed = true;
            }

            if (!trigger.mostrarMarcacaoVisual)
            {
                trigger.mostrarMarcacaoVisual = true;
                changed = true;
            }

            if (!trigger.atualizarVisualEmTempoReal)
            {
                trigger.atualizarVisualEmTempoReal = true;
                changed = true;
            }

            if (!trigger.enabled)
            {
                trigger.enabled = true;
                changed = true;
            }

            EditorUtility.SetDirty(trigger);
        }

        for (int i = 0; i < markers.Length; i++)
        {
            BuyableLandAreaMarker marker = markers[i];
            if (marker == null)
                continue;

            markerCount++;

            if (!marker.exibirDemarcacao)
            {
                marker.exibirDemarcacao = true;
                changed = true;
            }

            if (!marker.atualizarEmTempoReal)
            {
                marker.atualizarEmTempoReal = true;
                changed = true;
            }

            if (!marker.enabled)
            {
                marker.enabled = true;
                changed = true;
            }

            EditorUtility.SetDirty(marker);
        }

        if (purchaseController != null)
        {
            BuySceneLandPurchaseController landController =
                Object.FindAnyObjectByType<BuySceneLandPurchaseController>(FindObjectsInactive.Include);

            if (landController == null && markers.Length > 0)
            {
                landController = Undo.AddComponent<BuySceneLandPurchaseController>(purchaseController.gameObject);
                changed = true;
            }

            if (landController != null)
            {
                if (landController.controladorBuyScene != purchaseController)
                {
                    landController.controladorBuyScene = purchaseController;
                    changed = true;
                }

                Camera purchaseCamera = purchaseController.cameraPrincipal != null
                    ? purchaseController.cameraPrincipal
                    : playerCamera != null ? playerCamera.gameCamera : Camera.main;

                if (landController.cameraCompra != purchaseCamera)
                {
                    landController.cameraCompra = purchaseCamera;
                    changed = true;
                }

                BuyScenePurchaseConfirmationPanel panel =
                    Object.FindAnyObjectByType<BuyScenePurchaseConfirmationPanel>(FindObjectsInactive.Include);

                if (panel != null && landController.painelConfirmacao != panel)
                {
                    landController.painelConfirmacao = panel;
                    changed = true;
                }

                landController.terrenos = markers;
                landController.procurarTerrenosAutomaticamente = true;
                landController.enabled = true;
                EditorUtility.SetDirty(landController);
            }
        }

        MiniMarketEnergySegmentHUD[] huds = Object.FindObjectsByType<MiniMarketEnergySegmentHUD>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None
        );

        for (int i = 0; i < huds.Length; i++)
        {
            MiniMarketEnergySegmentHUD hud = huds[i];
            if (hud == null)
                continue;

            hudCount++;

            if (movement != null && hud.movimento != movement)
            {
                hud.movimento = movement;
                changed = true;
            }

            if (!hud.autoDetectarBarras)
            {
                hud.autoDetectarBarras = true;
                changed = true;
            }

            hud.RebuscarBarrasEAtualizar();
            EditorUtility.SetDirty(hud);
        }

        if (changed)
        {
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
        }

        Debug.Log(
            "[GameplayFeatureRepair] Concluído. " +
            "Triggers=" + triggerCount +
            ", terrenos=" + markerCount +
            ", HUDs=" + hudCount +
            ", cena alterada=" + changed + ". " +
            "Minimapa, F10, energia grátis e mira são inicializados automaticamente no Play Mode."
        );

        if (markers.Length > 0 && triggers.Length == 0)
        {
            Debug.LogWarning(
                "[GameplayFeatureRepair] Existem terrenos, mas nenhum BuySceneEntryTrigger foi encontrado. " +
                "Selecione o collider da calçada e adicione BuySceneEntryTrigger."
            );
        }
    }
}
#endif
