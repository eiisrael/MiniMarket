#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>
/// Reconciliador complementar das lojas Bronze. Prefere o controlador já existente em
/// BuySceneController, preservando os ajustes de câmera que o usuário já fez, e desativa
/// controladores duplicados criados por versões antigas do reparo.
/// </summary>
[InitializeOnLoad]
public static class BronzeMarketLocalControllerReconciler
{
    private const string MaterialPath = "Assets/Generated/MiniMarket/Materials/BuyAreaLine.mat";
    private static bool scheduled;
    private static bool executing;

    static BronzeMarketLocalControllerReconciler()
    {
        EditorApplication.hierarchyChanged -= Schedule;
        EditorApplication.hierarchyChanged += Schedule;
    }

    [MenuItem("Tools/MiniMarket/Bronze Market/Reconciliar Controladores e Visuais", priority = 4)]
    public static void RunAndSave()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("[BronzeMarketReconciler] Saia do Play Mode antes de executar.");
            return;
        }

        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded || string.IsNullOrWhiteSpace(scene.path))
        {
            Debug.LogWarning("[BronzeMarketReconciler] Abra e salve a SampleScene primeiro.");
            return;
        }

        int changed = ReconcileAll();
        if (changed > 0)
        {
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
        }

        Debug.Log("[BronzeMarketReconciler] Finalizado. Lojas reconciliadas=" + changed + ".");
    }

    private static void Schedule()
    {
        if (scheduled || executing || EditorApplication.isPlayingOrWillChangePlaymode ||
            EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            return;
        }

        scheduled = true;
        EditorApplication.delayCall += RunDelayed;
    }

    private static void RunDelayed()
    {
        scheduled = false;
        if (executing || EditorApplication.isPlayingOrWillChangePlaymode ||
            EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            return;
        }

        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded || string.IsNullOrWhiteSpace(scene.path))
            return;

        int changed = ReconcileAll();
        if (changed > 0)
        {
            EditorSceneManager.MarkSceneDirty(scene);
            SceneView.RepaintAll();
        }
    }

    private static int ReconcileAll()
    {
        BronzeMarketPurchaseLot[] lots = Object.FindObjectsByType<BronzeMarketPurchaseLot>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None
        );

        if (lots.Length == 0)
            return 0;

        executing = true;
        int changed = 0;
        try
        {
            Material lineMaterial = EnsureLineMaterial();
            for (int i = 0; i < lots.Length; i++)
            {
                if (ReconcileLot(lots[i], lineMaterial))
                    changed++;
            }
        }
        finally
        {
            executing = false;
        }

        return changed;
    }

    private static bool ReconcileLot(BronzeMarketPurchaseLot lot, Material lineMaterial)
    {
        if (lot == null)
            return false;

        BuySceneCameraModeController[] controllers =
            lot.GetComponentsInChildren<BuySceneCameraModeController>(true);
        if (controllers.Length == 0)
            return false;

        BuySceneCameraModeController preferred = ChoosePreferredController(lot, controllers);
        if (preferred == null)
            return false;

        bool changed = lot.controladorCamera != preferred;
        Undo.RecordObject(lot, "Reconciliar controlador da Bronze_Market");
        lot.controladorCamera = preferred;

        PurchaseModeBridge bridge = preferred.GetComponent<PurchaseModeBridge>();
        if (bridge == null)
        {
            bridge = Undo.AddComponent<PurchaseModeBridge>(preferred.gameObject);
            changed = true;
        }

        BuySceneLandPurchaseController purchase =
            preferred.GetComponent<BuySceneLandPurchaseController>();
        if (purchase == null)
        {
            purchase = Undo.AddComponent<BuySceneLandPurchaseController>(preferred.gameObject);
            changed = true;
        }

        Undo.RecordObject(bridge, "Reconciliar ponte de câmera Bronze");
        Undo.RecordObject(purchase, "Reconciliar compra Bronze");
        bridge.purchaseController = preferred;
        bridge.playerCamera = lot.cameraDoJogador;
        bridge.movement = lot.movimentoDoJogador;

        purchase.controladorBuyScene = preferred;
        purchase.cameraCompra = preferred.cameraPrincipal;
        purchase.painelConfirmacao = lot.painelConfirmacao;
        purchase.terrenos = lot.terrenoPrincipal != null
            ? new[] { lot.terrenoPrincipal }
            : Array.Empty<BuyableLandAreaMarker>();
        purchase.procurarTerrenosAutomaticamente = false;
        purchase.enabled = true;

        lot.ponteCamera = bridge;
        lot.controladorCompra = purchase;

        if (lot.triggerEntrada != null)
        {
            Undo.RecordObject(lot.triggerEntrada, "Reconciliar trigger Bronze");
            lot.triggerEntrada.controladorBuyScene = preferred;
            lot.triggerEntrada.terrenosDestaArea = lot.terrenoPrincipal != null
                ? new[] { lot.terrenoPrincipal }
                : Array.Empty<BuyableLandAreaMarker>();
            lot.triggerEntrada.usarTerrenosProximosSeListaVazia = false;
            lot.triggerEntrada.sincronizarComTerrenosEncontradosAutomaticamente = false;
            lot.triggerEntrada.SendMessage("CriarRenderizadores", SendMessageOptions.DontRequireReceiver);
            lot.triggerEntrada.SendMessage("AtualizarVisualCompleto", SendMessageOptions.DontRequireReceiver);
            AssignLineMaterial(lot.triggerEntrada.transform, lineMaterial);
        }

        if (lot.terrenoPrincipal != null)
        {
            Undo.RecordObject(lot.terrenoPrincipal, "Reconciliar terreno Bronze");
            lot.terrenoPrincipal.idPersistente = lot.idLote;
            lot.terrenoPrincipal.nomeDoTerreno = lot.nomeDaLoja;
            lot.terrenoPrincipal.precoGold = lot.precoGold;
            lot.terrenoPrincipal.SendMessage("CriarOuAtualizarLinhas", SendMessageOptions.DontRequireReceiver);
            AssignLineMaterial(lot.terrenoPrincipal.transform, lineMaterial);
        }

        if (lot.visualStatus != null)
        {
            Undo.RecordObject(lot.visualStatus, "Reconciliar status Bronze");
            lot.visualStatus.lote = lot;
            lot.visualStatus.terreno = lot.terrenoPrincipal;
            lot.visualStatus.controladorCamera = preferred;
            lot.visualStatus.AtualizarVisualImediato();
        }

        for (int i = 0; i < controllers.Length; i++)
        {
            BuySceneCameraModeController other = controllers[i];
            if (other == null || other == preferred)
                continue;

            Undo.RecordObject(other, "Desativar controlador Bronze duplicado");
            other.enabled = false;

            PurchaseModeBridge otherBridge = other.GetComponent<PurchaseModeBridge>();
            if (otherBridge != null)
            {
                Undo.RecordObject(otherBridge, "Desativar ponte Bronze duplicada");
                otherBridge.enabled = false;
            }

            BuySceneLandPurchaseController otherPurchase =
                other.GetComponent<BuySceneLandPurchaseController>();
            if (otherPurchase != null)
            {
                Undo.RecordObject(otherPurchase, "Desativar compra Bronze duplicada");
                otherPurchase.enabled = false;
            }
            changed = true;
        }

        lot.AplicarVinculosRuntime();
        MarkDirty(lot, preferred, bridge, purchase, lot.triggerEntrada, lot.terrenoPrincipal, lot.visualStatus);
        return changed || true;
    }

    private static BuySceneCameraModeController ChoosePreferredController(
        BronzeMarketPurchaseLot lot,
        BuySceneCameraModeController[] controllers)
    {
        BuySceneCameraModeController best = null;
        int bestScore = int.MinValue;

        for (int i = 0; i < controllers.Length; i++)
        {
            BuySceneCameraModeController current = controllers[i];
            if (current == null)
                continue;

            string compact = Compact(current.name);
            int score = 0;
            if (compact.Contains("buyscenecontroller")) score += 1000;
            if (compact.Contains("cameracontroller")) score += 600;
            if (current.cameraPrincipal != null) score += 150;
            if (current.jogadorRaiz != null) score += 100;
            if (current == lot.controladorCamera) score += 40;
            if (Compact(current.transform.parent != null ? current.transform.parent.name : string.Empty)
                .Contains("buysystemshop")) score += 200;

            if (score > bestScore)
            {
                bestScore = score;
                best = current;
            }
        }

        return best;
    }

    private static Material EnsureLineMaterial()
    {
        Material existing = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (existing != null)
            return existing;

        string folder = System.IO.Path.GetDirectoryName(MaterialPath);
        if (!System.IO.Directory.Exists(folder))
            System.IO.Directory.CreateDirectory(folder);

        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Sprites/Default");
        if (shader == null) shader = Shader.Find("Unlit/Color");
        if (shader == null) shader = Shader.Find("Standard");
        if (shader == null)
            return null;

        Material material = new Material(shader) { name = "BuyAreaLine" };
        AssetDatabase.CreateAsset(material, MaterialPath);
        AssetDatabase.SaveAssets();
        return material;
    }

    private static void AssignLineMaterial(Transform root, Material material)
    {
        if (root == null || material == null)
            return;

        LineRenderer[] lines = root.GetComponentsInChildren<LineRenderer>(true);
        for (int i = 0; i < lines.Length; i++)
        {
            if (lines[i] == null)
                continue;

            Undo.RecordObject(lines[i], "Atribuir material persistente da compra");
            lines[i].sharedMaterial = material;
            EditorUtility.SetDirty(lines[i]);
        }
    }

    private static void MarkDirty(params Object[] objects)
    {
        for (int i = 0; i < objects.Length; i++)
        {
            if (objects[i] != null)
                EditorUtility.SetDirty(objects[i]);
        }
    }

    private static string Compact(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        return value.Trim().ToLowerInvariant()
            .Replace("_", string.Empty)
            .Replace("-", string.Empty)
            .Replace(" ", string.Empty)
            .Replace("á", "a")
            .Replace("ã", "a")
            .Replace("ç", "c");
    }
}
#endif
