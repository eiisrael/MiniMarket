#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// Executa a reconciliação em um ciclo posterior ao gerador de IDs. Isso garante que uma
/// Bronze_Market recém-duplicada termine ligada ao BuySceneController correto, independentemente
/// da ordem em que os callbacks de hierarchyChanged forem chamados pelo Unity.
/// </summary>
[InitializeOnLoad]
public static class BronzeMarketPostDuplicateFinalizer
{
    private static bool scheduled;
    private static bool running;

    static BronzeMarketPostDuplicateFinalizer()
    {
        EditorApplication.hierarchyChanged -= Schedule;
        EditorApplication.hierarchyChanged += Schedule;
    }

    private static void Schedule()
    {
        if (scheduled || running || EditorApplication.isPlayingOrWillChangePlaymode ||
            EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            return;
        }

        scheduled = true;
        EditorApplication.delayCall += FirstDelay;
    }

    private static void FirstDelay()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode ||
            EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            scheduled = false;
            return;
        }

        // O primeiro delay permite ao BronzeMarketPurchaseLotSetup gerar o novo ID.
        // O segundo roda depois de todas as ligações iniciais da cópia.
        EditorApplication.delayCall += FinalizeIfNeeded;
    }

    private static void FinalizeIfNeeded()
    {
        scheduled = false;

        if (running || EditorApplication.isPlayingOrWillChangePlaymode ||
            EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            return;
        }

        BronzeMarketPurchaseLot[] lots = Object.FindObjectsByType<BronzeMarketPurchaseLot>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None
        );

        bool needsRepair = false;
        for (int i = 0; i < lots.Length; i++)
        {
            if (NeedsRepair(lots[i]))
            {
                needsRepair = true;
                break;
            }
        }

        if (!needsRepair)
            return;

        running = true;
        try
        {
            BronzeMarketLocalControllerReconciler.RunAndSave();
        }
        finally
        {
            running = false;
        }
    }

    private static bool NeedsRepair(BronzeMarketPurchaseLot lot)
    {
        if (lot == null || string.IsNullOrWhiteSpace(lot.idLote))
            return false; // O gerador principal ainda será responsável por criar/configurar.

        if (lot.controladorCamera == null || !lot.controladorCamera.enabled)
            return true;

        if (lot.triggerEntrada == null || lot.terrenoPrincipal == null || lot.controladorCompra == null)
            return true;

        if (lot.triggerEntrada.controladorBuyScene != lot.controladorCamera)
            return true;

        if (lot.triggerEntrada.terrenosDestaArea == null ||
            lot.triggerEntrada.terrenosDestaArea.Length != 1 ||
            lot.triggerEntrada.terrenosDestaArea[0] != lot.terrenoPrincipal)
        {
            return true;
        }

        if (lot.controladorCompra.controladorBuyScene != lot.controladorCamera ||
            lot.controladorCompra.procurarTerrenosAutomaticamente ||
            lot.controladorCompra.terrenos == null ||
            lot.controladorCompra.terrenos.Length != 1 ||
            lot.controladorCompra.terrenos[0] != lot.terrenoPrincipal)
        {
            return true;
        }

        BuySceneCameraModeController[] controllers =
            lot.GetComponentsInChildren<BuySceneCameraModeController>(true);
        int enabledCount = 0;
        for (int i = 0; i < controllers.Length; i++)
        {
            if (controllers[i] != null && controllers[i].enabled)
                enabledCount++;
        }

        return enabledCount != 1;
    }
}
#endif
