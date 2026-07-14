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
/// Materializa e mantém cada Bronze_Market como um lote de compra independente.
/// Ao duplicar a raiz da loja, detecta o ID copiado, gera outro ID e religa somente os
/// componentes existentes dentro da nova hierarquia.
/// </summary>
[InitializeOnLoad]
public static class BronzeMarketPurchaseLotSetup
{
    private const string RuntimeTriggerName = "BuySceneEntryTrigger_Runtime";
    private const string LotAreaName = "PurchaseLotArea";
    private const string FocusName = "PurchaseCameraFocus";
    private const string StatusName = "PurchaseLotStatus";
    private const string BuySystemName = "BUY_SYSTEM";

    private static bool scheduled;
    private static bool executing;

    static BronzeMarketPurchaseLotSetup()
    {
        EditorApplication.hierarchyChanged -= ScheduleAutomaticRepair;
        EditorApplication.hierarchyChanged += ScheduleAutomaticRepair;
    }

    [MenuItem("Tools/MiniMarket/Bronze Market/Preparar Todas as Lojas Bronze", priority = 0)]
    public static void PrepararTodas()
    {
        if (!ValidateScene(out Scene scene))
            return;

        List<Transform> roots = FindBronzeMarketRoots();
        if (roots.Count == 0)
        {
            Debug.LogWarning("[BronzeMarketSetup] Nenhum objeto Bronze_Market foi encontrado.");
            return;
        }

        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Preparar lojas Bronze independentes");

        HashSet<string> usedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        int configured = 0;

        executing = true;
        try
        {
            for (int i = 0; i < roots.Count; i++)
            {
                if (ConfigureRoot(roots[i], usedIds, true))
                    configured++;
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Undo.CollapseUndoOperations(undoGroup);

            Debug.Log(
                "[BronzeMarketSetup] Concluído. Lojas configuradas=" + configured +
                ". Agora basta duplicar a raiz Bronze_Market; a cópia receberá ID próprio."
            );
        }
        finally
        {
            executing = false;
        }
    }

    [MenuItem("Tools/MiniMarket/Bronze Market/Preparar Loja Bronze Selecionada", priority = 1)]
    public static void PrepararSelecionada()
    {
        if (!ValidateScene(out Scene scene))
            return;

        Transform root = FindBronzeRootFromSelection();
        if (root == null)
        {
            Debug.LogWarning("[BronzeMarketSetup] Selecione a raiz Bronze_Market ou um de seus filhos.");
            return;
        }

        HashSet<string> usedIds = CollectIdsExcept(root);
        executing = true;
        try
        {
            ConfigureRoot(root, usedIds, true);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Selection.activeGameObject = root.gameObject;
            EditorGUIUtility.PingObject(root.gameObject);
        }
        finally
        {
            executing = false;
        }
    }

    [MenuItem("Tools/MiniMarket/Bronze Market/Gerar Novo ID para Loja Selecionada", priority = 2)]
    public static void GenerateNewIdForSelected()
    {
        Transform root = FindBronzeRootFromSelection();
        if (root == null)
        {
            Debug.LogWarning("[BronzeMarketSetup] Selecione uma Bronze_Market.");
            return;
        }

        BronzeMarketPurchaseLot lot = root.GetComponent<BronzeMarketPurchaseLot>();
        if (lot == null)
            lot = Undo.AddComponent<BronzeMarketPurchaseLot>(root.gameObject);

        Undo.RecordObject(lot, "Gerar novo ID da loja Bronze");
        lot.idLote = GenerateUniqueId(CollectIdsExcept(root));
        lot.AplicarVinculosRuntime();
        EditorUtility.SetDirty(lot);
        EditorSceneManager.MarkSceneDirty(root.gameObject.scene);

        Debug.Log("[BronzeMarketSetup] Novo ID: " + lot.idLote, lot);
    }

    [MenuItem("Tools/MiniMarket/Bronze Market/Validar Lojas Bronze", priority = 3)]
    public static void ValidateAll()
    {
        List<Transform> roots = FindBronzeMarketRoots();
        HashSet<string> ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        int errors = 0;
        int warnings = 0;

        for (int i = 0; i < roots.Count; i++)
        {
            Transform root = roots[i];
            BronzeMarketPurchaseLot lot = root.GetComponent<BronzeMarketPurchaseLot>();
            if (lot == null)
            {
                errors++;
                Debug.LogError("[BronzeMarketValidator] Componente BronzeMarketPurchaseLot ausente.", root);
                continue;
            }

            string id = BronzeMarketPurchaseLot.NormalizarId(lot.idLote);
            if (string.IsNullOrWhiteSpace(id))
            {
                errors++;
                Debug.LogError("[BronzeMarketValidator] ID vazio em " + root.name + ".", lot);
            }
            else if (!ids.Add(id))
            {
                errors++;
                Debug.LogError("[BronzeMarketValidator] ID duplicado: " + id + ".", lot);
            }

            if (lot.buyArea == null || !lot.buyArea.IsChildOf(root))
            {
                errors++;
                Debug.LogError("[BronzeMarketValidator] Buy_Area não pertence à própria loja.", lot);
            }

            if (lot.triggerEntrada == null || !lot.triggerEntrada.transform.IsChildOf(root))
            {
                errors++;
                Debug.LogError("[BronzeMarketValidator] Trigger local ausente.", lot);
            }

            if (lot.terrenoPrincipal == null || !lot.terrenoPrincipal.transform.IsChildOf(root))
            {
                errors++;
                Debug.LogError("[BronzeMarketValidator] Terreno local ausente.", lot);
            }

            if (lot.controladorCamera == null || !lot.controladorCamera.transform.IsChildOf(root))
            {
                errors++;
                Debug.LogError("[BronzeMarketValidator] Controlador de câmera local ausente.", lot);
            }

            if (lot.controladorCompra == null || !lot.controladorCompra.transform.IsChildOf(root))
            {
                errors++;
                Debug.LogError("[BronzeMarketValidator] Controlador de compra local ausente.", lot);
            }
            else
            {
                if (lot.controladorCompra.procurarTerrenosAutomaticamente)
                {
                    errors++;
                    Debug.LogError("[BronzeMarketValidator] Busca global de terrenos deve estar desligada.", lot.controladorCompra);
                }

                if (lot.controladorCompra.terrenos == null ||
                    lot.controladorCompra.terrenos.Length != 1 ||
                    lot.controladorCompra.terrenos[0] != lot.terrenoPrincipal)
                {
                    errors++;
                    Debug.LogError("[BronzeMarketValidator] Compra não está restrita ao terreno da própria loja.", lot.controladorCompra);
                }
            }

            if (lot.triggerEntrada != null)
            {
                if (lot.triggerEntrada.usarTerrenosProximosSeListaVazia ||
                    lot.triggerEntrada.sincronizarComTerrenosEncontradosAutomaticamente)
                {
                    errors++;
                    Debug.LogError("[BronzeMarketValidator] Trigger ainda permite busca global/proximidade.", lot.triggerEntrada);
                }
            }

            if (lot.visualStatus == null)
            {
                warnings++;
                Debug.LogWarning("[BronzeMarketValidator] Painel de status/seta ausente.", lot);
            }
        }

        Debug.Log(
            "[BronzeMarketValidator] Finalizado. Lojas=" + roots.Count +
            ", erros=" + errors + ", avisos=" + warnings + "."
        );
    }

    private static void ScheduleAutomaticRepair()
    {
        if (executing || scheduled || EditorApplication.isPlayingOrWillChangePlaymode ||
            EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            return;
        }

        scheduled = true;
        EditorApplication.delayCall += AutomaticRepairAfterDuplicate;
    }

    private static void AutomaticRepairAfterDuplicate()
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

        List<Transform> roots = FindBronzeMarketRoots();
        if (roots.Count == 0)
            return;

        HashSet<string> usedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        bool changed = false;

        executing = true;
        try
        {
            for (int i = 0; i < roots.Count; i++)
            {
                Transform root = roots[i];
                BronzeMarketPurchaseLot lot = root.GetComponent<BronzeMarketPurchaseLot>();
                string currentId = lot != null
                    ? BronzeMarketPurchaseLot.NormalizarId(lot.idLote)
                    : string.Empty;

                bool requiresSetup = lot == null || string.IsNullOrWhiteSpace(currentId) || usedIds.Contains(currentId);
                if (requiresSetup)
                {
                    if (ConfigureRoot(root, usedIds, false))
                        changed = true;
                }
                else
                {
                    usedIds.Add(currentId);
                    // A duplicação preserva referências para filhos copiados. Reforça o escopo sem recriar layout.
                    lot.AplicarVinculosRuntime();
                }
            }

            if (changed)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                SceneView.RepaintAll();
                Debug.Log(
                    "[BronzeMarketSetup] Nova cópia Bronze_Market detectada e configurada com ID próprio. Salve a cena com Ctrl+S."
                );
            }
        }
        finally
        {
            executing = false;
        }
    }

    private static bool ConfigureRoot(Transform root, HashSet<string> usedIds, bool forceRepair)
    {
        if (root == null)
            return false;

        bool changed = false;
        BronzeMarketPurchaseLot lot = root.GetComponent<BronzeMarketPurchaseLot>();
        if (lot == null)
        {
            lot = Undo.AddComponent<BronzeMarketPurchaseLot>(root.gameObject);
            changed = true;
        }

        string currentId = BronzeMarketPurchaseLot.NormalizarId(lot.idLote);
        if (string.IsNullOrWhiteSpace(currentId) || usedIds.Contains(currentId))
        {
            Undo.RecordObject(lot, "Definir ID único da Bronze_Market");
            lot.idLote = GenerateUniqueId(usedIds);
            currentId = lot.idLote;
            changed = true;
        }
        usedIds.Add(currentId);

        Transform buyArea = FindDescendant(root, "Buy_Area", "BuyArea", "Area_Compra", "AreaCompra");
        if (buyArea == null)
        {
            Debug.LogError("[BronzeMarketSetup] Buy_Area não foi encontrado dentro de " + root.name + ".", root);
            return changed;
        }

        Collider solidCollider = FindSolidCollider(buyArea);
        if (solidCollider == null)
        {
            solidCollider = Undo.AddComponent<BoxCollider>(buyArea.gameObject);
            FitBoxColliderToRenderers((BoxCollider)solidCollider, buyArea);
            changed = true;
        }
        solidCollider.isTrigger = false;

        GameObject triggerObject = FindOrCreateChild(buyArea, RuntimeTriggerName);
        BoxCollider triggerCollider = EnsureComponent<BoxCollider>(triggerObject);
        triggerCollider.isTrigger = true;
        ConfigureTriggerCollider(solidCollider, triggerObject.transform, triggerCollider);
        BuySceneEntryTrigger entryTrigger = EnsureComponent<BuySceneEntryTrigger>(triggerObject);

        Bounds storeBounds = CalculateStoreBounds(root, solidCollider.bounds);
        BuyableLandAreaMarker marker = root.GetComponentInChildren<BuyableLandAreaMarker>(true);
        if (marker == null)
        {
            GameObject areaObject = FindOrCreateChild(root, LotAreaName);
            marker = EnsureComponent<BuyableLandAreaMarker>(areaObject);
            Vector3 groundPoint = new Vector3(
                storeBounds.center.x,
                solidCollider.bounds.max.y + 0.03f,
                storeBounds.center.z
            );
            areaObject.transform.position = groundPoint;
            areaObject.transform.rotation = root.rotation;
            marker.centroLocal = Vector3.zero;
            marker.tamanhoArea = new Vector2(
                Mathf.Max(2f, storeBounds.size.x),
                Mathf.Max(2f, storeBounds.size.z)
            );
            marker.alturaAcimaDoChao = 0.04f;
            changed = true;
        }

        GameObject focusObject = FindOrCreateChild(root, FocusName);
        focusObject.transform.position = marker.ObterPontoDeFoco();
        focusObject.transform.rotation = root.rotation;

        Transform buySystemTransform = FindDescendant(root, BuySystemName, "BuySystem", "Buy_SystemShop");
        if (buySystemTransform == null)
            buySystemTransform = FindOrCreateChild(root, BuySystemName).transform;

        BuySceneCameraModeController cameraController =
            buySystemTransform.GetComponent<BuySceneCameraModeController>();
        if (cameraController == null)
            cameraController = Undo.AddComponent<BuySceneCameraModeController>(buySystemTransform.gameObject);

        PurchaseModeBridge bridge = buySystemTransform.GetComponent<PurchaseModeBridge>();
        if (bridge == null)
            bridge = Undo.AddComponent<PurchaseModeBridge>(buySystemTransform.gameObject);

        BuySceneLandPurchaseController purchase =
            buySystemTransform.GetComponent<BuySceneLandPurchaseController>();
        if (purchase == null)
            purchase = Undo.AddComponent<BuySceneLandPurchaseController>(buySystemTransform.gameObject);

        PlayerCameraController playerCamera =
            Object.FindAnyObjectByType<PlayerCameraController>(FindObjectsInactive.Include);
        CameraRelativeMovement movement =
            Object.FindAnyObjectByType<CameraRelativeMovement>(FindObjectsInactive.Include);

        BuyScenePurchaseConfirmationPanel panel =
            root.GetComponentInChildren<BuyScenePurchaseConfirmationPanel>(true);
        if (panel == null)
        {
            panel = Object.FindAnyObjectByType<BuyScenePurchaseConfirmationPanel>(
                FindObjectsInactive.Include
            );
        }

        BronzeMarketLotStatusView status = EnsureStatusView(root, marker, cameraController, lot, storeBounds);

        Undo.RecordObject(lot, "Configurar lote Bronze");
        lot.buyArea = buyArea;
        lot.colliderSolidoDaCalcada = solidCollider;
        lot.triggerEntrada = entryTrigger;
        lot.terrenoPrincipal = marker;
        lot.pontoFocoCamera = focusObject.transform;
        lot.controladorCamera = cameraController;
        lot.ponteCamera = bridge;
        lot.controladorCompra = purchase;
        lot.painelConfirmacao = panel;
        lot.cameraDoJogador = playerCamera;
        lot.movimentoDoJogador = movement;
        lot.visualStatus = status;
        lot.nomeDaLoja = string.IsNullOrWhiteSpace(lot.nomeDaLoja) ? root.name : lot.nomeDaLoja;

        Undo.RecordObject(marker, "Configurar terreno da loja Bronze");
        marker.idPersistente = lot.idLote;
        marker.nomeDoTerreno = lot.nomeDaLoja;
        marker.precoGold = lot.precoGold;
        marker.exibirDemarcacao = true;

        Undo.RecordObject(entryTrigger, "Restringir trigger à própria loja");
        entryTrigger.controladorBuyScene = cameraController;
        entryTrigger.pontoDeFocoDaCamera = focusObject.transform;
        entryTrigger.terrenosDestaArea = new[] { marker };
        entryTrigger.usarTerrenosProximosSeListaVazia = false;
        entryTrigger.sincronizarComTerrenosEncontradosAutomaticamente = false;
        entryTrigger.mostrarMarcacaoVisual = true;
        entryTrigger.mostrarXCentral = true;

        Undo.RecordObject(cameraController, "Configurar câmera local da loja Bronze");
        if (cameraController.cameraPrincipal == null && playerCamera != null)
            cameraController.cameraPrincipal = playerCamera.gameCamera;
        if (cameraController.jogadorRaiz == null && movement != null)
            cameraController.jogadorRaiz = movement.transform;

        Undo.RecordObject(bridge, "Configurar ponte local da loja Bronze");
        bridge.purchaseController = cameraController;
        bridge.playerCamera = playerCamera;
        bridge.movement = movement;

        Undo.RecordObject(purchase, "Restringir compra à própria loja");
        purchase.controladorBuyScene = cameraController;
        purchase.cameraCompra = cameraController.cameraPrincipal;
        purchase.painelConfirmacao = panel;
        purchase.terrenos = new[] { marker };
        purchase.procurarTerrenosAutomaticamente = false;
        purchase.enabled = true;

        lot.AplicarVinculosRuntime();
        status.AtualizarVisualImediato();

        MarkDirty(lot, marker, entryTrigger, cameraController, bridge, purchase, status);
        return true || changed || forceRepair;
    }

    private static BronzeMarketLotStatusView EnsureStatusView(
        Transform root,
        BuyableLandAreaMarker marker,
        BuySceneCameraModeController cameraController,
        BronzeMarketPurchaseLot lot,
        Bounds storeBounds)
    {
        GameObject statusObject = FindOrCreateChild(root, StatusName, true);
        RectTransform statusRect = statusObject.GetComponent<RectTransform>();
        statusObject.transform.position = new Vector3(
            storeBounds.center.x,
            storeBounds.max.y + 1.2f,
            storeBounds.center.z
        );
        statusObject.transform.localScale = Vector3.one * 0.01f;
        statusRect.sizeDelta = new Vector2(360f, 150f);

        Canvas canvas = EnsureComponent<Canvas>(statusObject);
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 250;
        CanvasGroup group = EnsureComponent<CanvasGroup>(statusObject);
        group.interactable = false;
        group.blocksRaycasts = false;

        Image background = EnsureImage(statusRect, "Panel", new Color(0.035f, 0.045f, 0.055f, 0.9f));
        Stretch(background.rectTransform);
        background.raycastTarget = false;

        Text statusText = EnsureText(background.rectTransform, "StatusText", "DISPONÍVEL", 30, Color.green);
        statusText.rectTransform.anchorMin = new Vector2(0f, 0.48f);
        statusText.rectTransform.anchorMax = new Vector2(1f, 1f);
        statusText.rectTransform.offsetMin = new Vector2(12f, 0f);
        statusText.rectTransform.offsetMax = new Vector2(-12f, -6f);
        statusText.alignment = TextAnchor.MiddleCenter;
        statusText.fontStyle = FontStyle.Bold;

        Text priceText = EnsureText(background.rectTransform, "PriceText", "Gold: 20.000", 24, Color.white);
        priceText.rectTransform.anchorMin = new Vector2(0f, 0f);
        priceText.rectTransform.anchorMax = new Vector2(1f, 0.48f);
        priceText.rectTransform.offsetMin = new Vector2(12f, 8f);
        priceText.rectTransform.offsetMax = new Vector2(-12f, 0f);
        priceText.alignment = TextAnchor.MiddleCenter;

        Text arrowText = EnsureText(statusRect, "HoverArrow", "▼", 44, new Color(0.1f, 0.95f, 1f, 1f));
        arrowText.rectTransform.anchorMin = new Vector2(0.5f, 0f);
        arrowText.rectTransform.anchorMax = new Vector2(0.5f, 0f);
        arrowText.rectTransform.pivot = new Vector2(0.5f, 1f);
        arrowText.rectTransform.anchoredPosition = new Vector2(0f, -8f);
        arrowText.rectTransform.sizeDelta = new Vector2(80f, 70f);
        arrowText.alignment = TextAnchor.UpperCenter;
        arrowText.raycastTarget = false;

        BronzeMarketLotStatusView view = statusObject.GetComponent<BronzeMarketLotStatusView>();
        if (view == null)
            view = Undo.AddComponent<BronzeMarketLotStatusView>(statusObject);

        Undo.RecordObject(view, "Configurar status da loja Bronze");
        view.lote = lot;
        view.terreno = marker;
        view.controladorCamera = cameraController;
        view.canvasMundial = canvas;
        view.canvasGroup = group;
        view.fundoPainel = background;
        view.textoStatus = statusText;
        view.textoPreco = priceText;
        view.seta = arrowText.rectTransform;
        view.graficoSeta = arrowText;
        view.mostrarPreviewForaDoPlay = true;
        view.ocultarQuandoForaDoModoCompra = true;

        return view;
    }

    private static List<Transform> FindBronzeMarketRoots()
    {
        Transform[] all = Object.FindObjectsByType<Transform>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None
        );
        List<Transform> roots = new List<Transform>();

        for (int i = 0; i < all.Length; i++)
        {
            Transform current = all[i];
            if (current == null || !IsBronzeMarketName(current.name))
                continue;

            bool parentIsBronze = false;
            Transform parent = current.parent;
            while (parent != null)
            {
                if (IsBronzeMarketName(parent.name))
                {
                    parentIsBronze = true;
                    break;
                }
                parent = parent.parent;
            }

            if (!parentIsBronze)
                roots.Add(current);
        }

        roots.Sort((a, b) => a.GetSiblingIndex().CompareTo(b.GetSiblingIndex()));
        return roots;
    }

    private static Transform FindBronzeRootFromSelection()
    {
        Transform current = Selection.activeTransform;
        while (current != null)
        {
            if (IsBronzeMarketName(current.name) || current.GetComponent<BronzeMarketPurchaseLot>() != null)
                return current;
            current = current.parent;
        }
        return null;
    }

    private static HashSet<string> CollectIdsExcept(Transform exceptRoot)
    {
        HashSet<string> ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        List<Transform> roots = FindBronzeMarketRoots();
        for (int i = 0; i < roots.Count; i++)
        {
            if (roots[i] == exceptRoot)
                continue;

            BronzeMarketPurchaseLot lot = roots[i].GetComponent<BronzeMarketPurchaseLot>();
            if (lot == null)
                continue;

            string id = BronzeMarketPurchaseLot.NormalizarId(lot.idLote);
            if (!string.IsNullOrWhiteSpace(id))
                ids.Add(id);
        }
        return ids;
    }

    private static string GenerateUniqueId(HashSet<string> usedIds)
    {
        string id;
        do
        {
            id = "BRONZE_MARKET_" + Guid.NewGuid().ToString("N").Substring(0, 10).ToUpperInvariant();
        }
        while (usedIds.Contains(id));
        return id;
    }

    private static Transform FindDescendant(Transform root, params string[] names)
    {
        if (root == null)
            return null;

        Transform[] all = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < all.Length; i++)
        {
            string compact = Compact(all[i].name);
            for (int n = 0; n < names.Length; n++)
            {
                if (compact == Compact(names[n]))
                    return all[i];
            }
        }
        return null;
    }

    private static bool IsBronzeMarketName(string value)
    {
        return Compact(value).StartsWith("bronzemarket", StringComparison.OrdinalIgnoreCase);
    }

    private static Collider FindSolidCollider(Transform buyArea)
    {
        Collider[] colliders = buyArea.GetComponents<Collider>();
        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] != null && !colliders[i].isTrigger)
                return colliders[i];
        }
        return null;
    }

    private static void ConfigureTriggerCollider(Collider source, Transform trigger, BoxCollider target)
    {
        Bounds bounds = source.bounds;
        Vector3 scale = source.transform.lossyScale;
        trigger.localPosition = source.transform.InverseTransformPoint(bounds.center);
        trigger.localRotation = Quaternion.identity;
        trigger.localScale = Vector3.one;
        target.center = Vector3.zero;
        target.size = new Vector3(
            Mathf.Max(0.8f, bounds.size.x / Mathf.Max(0.0001f, Mathf.Abs(scale.x))),
            Mathf.Max(1.2f, bounds.size.y / Mathf.Max(0.0001f, Mathf.Abs(scale.y)) + 1f),
            Mathf.Max(0.8f, bounds.size.z / Mathf.Max(0.0001f, Mathf.Abs(scale.z)))
        );
        target.isTrigger = true;
    }

    private static void FitBoxColliderToRenderers(BoxCollider box, Transform root)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
        {
            box.center = Vector3.zero;
            box.size = new Vector3(2f, 0.2f, 2f);
            return;
        }

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            bounds.Encapsulate(renderers[i].bounds);

        box.center = root.InverseTransformPoint(bounds.center);
        Vector3 scale = root.lossyScale;
        box.size = new Vector3(
            bounds.size.x / Mathf.Max(0.0001f, Mathf.Abs(scale.x)),
            Mathf.Max(0.1f, bounds.size.y / Mathf.Max(0.0001f, Mathf.Abs(scale.y))),
            bounds.size.z / Mathf.Max(0.0001f, Mathf.Abs(scale.z))
        );
    }

    private static Bounds CalculateStoreBounds(Transform root, Bounds fallback)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        Bounds result = fallback;
        bool hasResult = false;

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null || renderer is LineRenderer ||
                renderer.GetComponentInParent<Canvas>() != null)
            {
                continue;
            }

            if (!hasResult)
            {
                result = renderer.bounds;
                hasResult = true;
            }
            else
            {
                result.Encapsulate(renderer.bounds);
            }
        }

        return result;
    }

    private static GameObject FindOrCreateChild(Transform parent, string name, bool rectTransform = false)
    {
        Transform existing = parent.Find(name);
        if (existing != null)
            return existing.gameObject;

        GameObject created = rectTransform
            ? new GameObject(name, typeof(RectTransform))
            : new GameObject(name);
        Undo.RegisterCreatedObjectUndo(created, "Criar " + name);
        created.transform.SetParent(parent, false);
        return created;
    }

    private static T EnsureComponent<T>(GameObject target) where T : Component
    {
        T component = target.GetComponent<T>();
        if (component == null)
            component = Undo.AddComponent<T>(target);
        return component;
    }

    private static Image EnsureImage(Transform parent, string name, Color color)
    {
        GameObject target = FindOrCreateChild(parent, name, true);
        Image image = EnsureComponent<Image>(target);
        image.color = color;
        return image;
    }

    private static Text EnsureText(Transform parent, string name, string value, int size, Color color)
    {
        GameObject target = FindOrCreateChild(parent, name, true);
        Text text = EnsureComponent<Text>(target);
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.text = value;
        text.fontSize = size;
        text.color = color;
        text.raycastTarget = false;
        return text;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static bool ValidateScene(out Scene scene)
    {
        scene = SceneManager.GetActiveScene();
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("[BronzeMarketSetup] Saia do Play Mode antes de executar.");
            return false;
        }
        if (!scene.IsValid() || !scene.isLoaded || string.IsNullOrWhiteSpace(scene.path))
        {
            Debug.LogWarning("[BronzeMarketSetup] Abra e salve a SampleScene primeiro.");
            return false;
        }
        return true;
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
