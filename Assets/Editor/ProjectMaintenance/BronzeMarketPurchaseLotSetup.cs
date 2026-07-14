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
/// Mantém cada Bronze_Market como um lote independente.
/// Ao duplicar a raiz, preserva integralmente Transform, RectTransform, cores, fontes,
/// tamanhos e filhos copiados. A automação altera somente o ID e os vínculos locais.
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
                ". Layouts existentes foram preservados."
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

        executing = true;
        try
        {
            ConfigureRoot(root, CollectIdsExcept(root), true);
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
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("[BronzeMarketSetup] Saia do Play Mode antes de executar.");
            return;
        }

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

        if (lot.visualStatus != null)
            lot.visualStatus.AtualizarVisualImediato();

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

            ValidateLocalReference(root, lot.buyArea, "Buy_Area", lot, ref errors);
            ValidateLocalComponent(root, lot.triggerEntrada, "Trigger local", lot, ref errors);
            ValidateLocalComponent(root, lot.terrenoPrincipal, "Terreno local", lot, ref errors);
            ValidateLocalComponent(root, lot.controladorCamera, "Controlador de câmera local", lot, ref errors);
            ValidateLocalComponent(root, lot.controladorCompra, "Controlador de compra local", lot, ref errors);

            if (lot.controladorCompra != null)
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

            if (lot.triggerEntrada != null &&
                (lot.triggerEntrada.usarTerrenosProximosSeListaVazia ||
                 lot.triggerEntrada.sincronizarComTerrenosEncontradosAutomaticamente))
            {
                errors++;
                Debug.LogError("[BronzeMarketValidator] Trigger ainda permite busca global/proximidade.", lot.triggerEntrada);
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

                bool requiresSetup = lot == null ||
                                     string.IsNullOrWhiteSpace(currentId) ||
                                     usedIds.Contains(currentId);

                if (requiresSetup)
                {
                    if (ConfigureRoot(root, usedIds, false))
                        changed = true;
                }
                else
                {
                    usedIds.Add(currentId);
                    lot.AplicarVinculosRuntime();
                }
            }

            if (changed)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                SceneView.RepaintAll();
                Debug.Log(
                    "[BronzeMarketSetup] Cópia detectada: layout preservado e novo ID atribuído. Salve com Ctrl+S."
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
            BoxCollider createdCollider = Undo.AddComponent<BoxCollider>(buyArea.gameObject);
            FitBoxColliderToRenderers(createdCollider, buyArea);
            solidCollider = createdCollider;
            changed = true;
        }
        if (solidCollider.isTrigger)
        {
            Undo.RecordObject(solidCollider, "Preservar collider sólido da calçada");
            solidCollider.isTrigger = false;
            changed = true;
        }

        GameObject triggerObject = FindOrCreateChild(buyArea, RuntimeTriggerName, false, out bool triggerObjectCreated);
        BoxCollider triggerCollider = EnsureComponent<BoxCollider>(triggerObject, out bool triggerColliderCreated);
        BuySceneEntryTrigger entryTrigger = EnsureComponent<BuySceneEntryTrigger>(triggerObject, out bool triggerCreated);

        if (triggerObjectCreated || triggerColliderCreated)
            ConfigureTriggerCollider(solidCollider, triggerObject.transform, triggerCollider);
        triggerCollider.isTrigger = true;
        changed |= triggerObjectCreated || triggerColliderCreated || triggerCreated;

        Bounds storeBounds = CalculateStoreBounds(root, solidCollider.bounds);
        BuyableLandAreaMarker marker = FindLocalMarker(root);
        if (marker == null)
        {
            GameObject areaObject = FindOrCreateChild(root, LotAreaName, false, out _);
            marker = EnsureComponent<BuyableLandAreaMarker>(areaObject, out _);
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

        GameObject focusObject = FindOrCreateChild(root, FocusName, false, out bool focusCreated);
        if (focusCreated)
        {
            focusObject.transform.position = marker.ObterPontoDeFoco();
            focusObject.transform.rotation = root.rotation;
            changed = true;
        }

        Transform buySystem = FindDescendant(root, BuySystemName, "BuySystem", "Buy_SystemShop");
        if (buySystem == null)
        {
            buySystem = FindOrCreateChild(root, BuySystemName, false, out _).transform;
            changed = true;
        }

        BuySceneCameraModeController cameraController = ChoosePreferredController(root);
        if (cameraController == null)
        {
            cameraController = Undo.AddComponent<BuySceneCameraModeController>(buySystem.gameObject);
            changed = true;
        }
        cameraController.enabled = true;

        PurchaseModeBridge bridge = cameraController.GetComponent<PurchaseModeBridge>();
        if (bridge == null)
        {
            bridge = Undo.AddComponent<PurchaseModeBridge>(cameraController.gameObject);
            changed = true;
        }

        BuySceneLandPurchaseController purchase = cameraController.GetComponent<BuySceneLandPurchaseController>();
        if (purchase == null)
        {
            purchase = Undo.AddComponent<BuySceneLandPurchaseController>(cameraController.gameObject);
            changed = true;
        }

        DisableOtherPurchaseControllers(root, cameraController);

        PlayerCameraController playerCamera = Object.FindAnyObjectByType<PlayerCameraController>(FindObjectsInactive.Include);
        CameraRelativeMovement movement = Object.FindAnyObjectByType<CameraRelativeMovement>(FindObjectsInactive.Include);
        BuyScenePurchaseConfirmationPanel panel = FindConfirmationPanel(root);

        BronzeMarketLotStatusView status = EnsureStatusView(
            root,
            marker,
            cameraController,
            lot,
            storeBounds,
            ref changed
        );

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
        if (string.IsNullOrWhiteSpace(lot.nomeDaLoja))
            lot.nomeDaLoja = root.name;

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
        bridge.enabled = true;
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
        if (status != null)
            status.AtualizarVisualImediato();

        MarkDirty(lot, marker, entryTrigger, cameraController, bridge, purchase, status);
        return changed || forceRepair;
    }

    private static BronzeMarketLotStatusView EnsureStatusView(
        Transform root,
        BuyableLandAreaMarker marker,
        BuySceneCameraModeController cameraController,
        BronzeMarketPurchaseLot lot,
        Bounds storeBounds,
        ref bool changed)
    {
        GameObject statusObject = FindOrCreateChild(root, StatusName, true, out bool statusCreated);
        RectTransform statusRect = statusObject.GetComponent<RectTransform>();

        if (statusCreated)
        {
            statusObject.transform.position = new Vector3(
                storeBounds.center.x,
                storeBounds.max.y + 1.2f,
                storeBounds.center.z
            );
            statusObject.transform.localScale = Vector3.one * 0.01f;
            statusRect.sizeDelta = new Vector2(360f, 150f);
            changed = true;
        }

        Canvas canvas = EnsureComponent<Canvas>(statusObject, out bool canvasCreated);
        if (canvasCreated)
        {
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 250;
            changed = true;
        }

        CanvasGroup group = EnsureComponent<CanvasGroup>(statusObject, out bool groupCreated);
        if (groupCreated)
        {
            group.interactable = false;
            group.blocksRaycasts = false;
            changed = true;
        }

        Image background = EnsureImage(
            statusRect,
            "Panel",
            new Color(0.035f, 0.045f, 0.055f, 0.9f),
            out bool backgroundCreated
        );
        if (backgroundCreated)
        {
            Stretch(background.rectTransform);
            background.raycastTarget = false;
            changed = true;
        }

        string shortId = ShortDisplayId(lot != null ? lot.idLote : string.Empty);
        Text statusText = EnsureText(
            background.rectTransform,
            "StatusText",
            "DISPONÍVEL\nID: " + shortId,
            30,
            Color.green,
            out bool statusTextCreated
        );
        if (statusTextCreated)
        {
            statusText.rectTransform.anchorMin = new Vector2(0f, 0.42f);
            statusText.rectTransform.anchorMax = new Vector2(1f, 1f);
            statusText.rectTransform.offsetMin = new Vector2(12f, 0f);
            statusText.rectTransform.offsetMax = new Vector2(-12f, -6f);
            statusText.alignment = TextAnchor.MiddleCenter;
            statusText.fontStyle = FontStyle.Bold;
            changed = true;
        }

        Text priceText = EnsureText(
            background.rectTransform,
            "PriceText",
            "Gold: 20.000",
            24,
            Color.white,
            out bool priceCreated
        );
        if (priceCreated)
        {
            priceText.rectTransform.anchorMin = new Vector2(0f, 0f);
            priceText.rectTransform.anchorMax = new Vector2(1f, 0.42f);
            priceText.rectTransform.offsetMin = new Vector2(12f, 8f);
            priceText.rectTransform.offsetMax = new Vector2(-12f, 0f);
            priceText.alignment = TextAnchor.MiddleCenter;
            changed = true;
        }

        Text arrowText = EnsureText(
            statusRect,
            "HoverArrow",
            "▼",
            44,
            new Color(0.1f, 0.95f, 1f, 1f),
            out bool arrowCreated
        );
        if (arrowCreated)
        {
            arrowText.rectTransform.anchorMin = new Vector2(0.5f, 0f);
            arrowText.rectTransform.anchorMax = new Vector2(0.5f, 0f);
            arrowText.rectTransform.pivot = new Vector2(0.5f, 1f);
            arrowText.rectTransform.anchoredPosition = new Vector2(0f, -8f);
            arrowText.rectTransform.sizeDelta = new Vector2(80f, 70f);
            arrowText.alignment = TextAnchor.UpperCenter;
            arrowText.raycastTarget = false;
            changed = true;
        }

        BronzeMarketLotStatusView view = statusObject.GetComponent<BronzeMarketLotStatusView>();
        if (view == null)
        {
            view = Undo.AddComponent<BronzeMarketLotStatusView>(statusObject);
            view.mostrarPreviewForaDoPlay = true;
            view.ocultarQuandoForaDoModoCompra = true;
            changed = true;
        }

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

        return view;
    }

    private static BuySceneCameraModeController ChoosePreferredController(Transform root)
    {
        BuySceneCameraModeController[] controllers =
            root.GetComponentsInChildren<BuySceneCameraModeController>(true);

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

    private static void DisableOtherPurchaseControllers(
        Transform root,
        BuySceneCameraModeController preferred)
    {
        BuySceneCameraModeController[] controllers =
            root.GetComponentsInChildren<BuySceneCameraModeController>(true);

        for (int i = 0; i < controllers.Length; i++)
        {
            BuySceneCameraModeController other = controllers[i];
            if (other == null || other == preferred)
                continue;

            Undo.RecordObject(other, "Desativar controlador de compra duplicado");
            other.enabled = false;

            PurchaseModeBridge bridge = other.GetComponent<PurchaseModeBridge>();
            if (bridge != null)
            {
                Undo.RecordObject(bridge, "Desativar ponte duplicada");
                bridge.enabled = false;
            }

            BuySceneLandPurchaseController purchase =
                other.GetComponent<BuySceneLandPurchaseController>();
            if (purchase != null)
            {
                Undo.RecordObject(purchase, "Desativar compra duplicada");
                purchase.enabled = false;
            }
        }
    }

    private static BuyScenePurchaseConfirmationPanel FindConfirmationPanel(Transform root)
    {
        BuyScenePurchaseConfirmationPanel local =
            root.GetComponentInChildren<BuyScenePurchaseConfirmationPanel>(true);
        if (local != null)
            return local;

        return Object.FindAnyObjectByType<BuyScenePurchaseConfirmationPanel>(
            FindObjectsInactive.Include
        );
    }

    private static BuyableLandAreaMarker FindLocalMarker(Transform root)
    {
        Transform preferred = FindDescendant(root, LotAreaName);
        if (preferred != null)
        {
            BuyableLandAreaMarker marker = preferred.GetComponent<BuyableLandAreaMarker>();
            if (marker != null)
                return marker;
        }

        BuyableLandAreaMarker[] markers =
            root.GetComponentsInChildren<BuyableLandAreaMarker>(true);
        return markers.Length > 0 ? markers[0] : null;
    }

    private static List<Transform> FindBronzeMarketRoots()
    {
        HashSet<Transform> unique = new HashSet<Transform>();

        BronzeMarketPurchaseLot[] lots = Object.FindObjectsByType<BronzeMarketPurchaseLot>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None
        );
        for (int i = 0; i < lots.Length; i++)
        {
            if (lots[i] != null)
                unique.Add(lots[i].transform);
        }

        Transform[] all = Object.FindObjectsByType<Transform>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None
        );

        for (int i = 0; i < all.Length; i++)
        {
            Transform current = all[i];
            if (current == null || !IsBronzeMarketName(current.name))
                continue;

            bool parentIsBronze = false;
            Transform parent = current.parent;
            while (parent != null)
            {
                if (IsBronzeMarketName(parent.name) ||
                    parent.GetComponent<BronzeMarketPurchaseLot>() != null)
                {
                    parentIsBronze = true;
                    break;
                }
                parent = parent.parent;
            }

            if (!parentIsBronze)
                unique.Add(current);
        }

        List<Transform> roots = new List<Transform>(unique);
        roots.Sort(CompareHierarchyOrder);
        return roots;
    }

    private static int CompareHierarchyOrder(Transform a, Transform b)
    {
        if (a == b) return 0;
        if (a == null) return 1;
        if (b == null) return -1;

        int sceneCompare = a.gameObject.scene.handle.CompareTo(b.gameObject.scene.handle);
        if (sceneCompare != 0)
            return sceneCompare;

        string pathA = AnimationUtility.CalculateTransformPath(a, null);
        string pathB = AnimationUtility.CalculateTransformPath(b, null);
        return string.CompareOrdinal(pathA, pathB);
    }

    private static Transform FindBronzeRootFromSelection()
    {
        Transform current = Selection.activeTransform;
        while (current != null)
        {
            if (current.GetComponent<BronzeMarketPurchaseLot>() != null ||
                IsBronzeMarketName(current.name))
            {
                return current;
            }
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
            id = "BRONZE_MARKET_" + Guid.NewGuid().ToString("N")
                .Substring(0, 10)
                .ToUpperInvariant();
        }
        while (usedIds.Contains(id));

        usedIds.Add(id);
        return id;
    }

    private static string ShortDisplayId(string id)
    {
        string normalized = BronzeMarketPurchaseLot.NormalizarId(id);
        if (string.IsNullOrWhiteSpace(normalized))
            return "----";

        const int length = 8;
        return normalized.Length <= length
            ? normalized
            : normalized.Substring(normalized.Length - length, length);
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
        Collider[] colliders = buyArea.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider candidate = colliders[i];
            if (candidate == null || candidate.isTrigger)
                continue;
            if (string.Equals(candidate.name, RuntimeTriggerName, StringComparison.OrdinalIgnoreCase))
                continue;
            return candidate;
        }
        return null;
    }

    private static void ConfigureTriggerCollider(
        Collider source,
        Transform trigger,
        BoxCollider target)
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
        bool found = false;
        Bounds bounds = new Bounds(root.position, Vector3.one);

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null || renderer is LineRenderer ||
                renderer.GetComponentInParent<Canvas>() != null)
            {
                continue;
            }

            if (!found)
            {
                bounds = renderer.bounds;
                found = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        if (!found)
        {
            box.center = Vector3.zero;
            box.size = new Vector3(2f, 0.2f, 2f);
            return;
        }

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

    private static GameObject FindOrCreateChild(
        Transform parent,
        string name,
        bool rectTransform,
        out bool created)
    {
        Transform existing = parent.Find(name);
        if (existing != null)
        {
            created = false;
            return existing.gameObject;
        }

        GameObject result = rectTransform
            ? new GameObject(name, typeof(RectTransform))
            : new GameObject(name);
        Undo.RegisterCreatedObjectUndo(result, "Criar " + name);
        result.transform.SetParent(parent, false);
        created = true;
        return result;
    }

    private static T EnsureComponent<T>(GameObject target, out bool created)
        where T : Component
    {
        T component = target.GetComponent<T>();
        if (component != null)
        {
            created = false;
            return component;
        }

        component = Undo.AddComponent<T>(target);
        created = true;
        return component;
    }

    private static Image EnsureImage(
        Transform parent,
        string name,
        Color defaultColor,
        out bool created)
    {
        GameObject target = FindOrCreateChild(parent, name, true, out bool objectCreated);
        Image image = target.GetComponent<Image>();
        bool componentCreated = false;
        if (image == null)
        {
            image = Undo.AddComponent<Image>(target);
            componentCreated = true;
        }

        created = objectCreated || componentCreated;
        if (created)
            image.color = defaultColor;
        return image;
    }

    private static Text EnsureText(
        Transform parent,
        string name,
        string defaultValue,
        int defaultSize,
        Color defaultColor,
        out bool created)
    {
        GameObject target = FindOrCreateChild(parent, name, true, out bool objectCreated);
        Text text = target.GetComponent<Text>();
        bool componentCreated = false;
        if (text == null)
        {
            text = Undo.AddComponent<Text>(target);
            componentCreated = true;
        }

        created = objectCreated || componentCreated;
        if (created)
        {
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.text = defaultValue;
            text.fontSize = defaultSize;
            text.color = defaultColor;
            text.raycastTarget = false;
        }
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

    private static void ValidateLocalReference(
        Transform root,
        Transform value,
        string label,
        Object context,
        ref int errors)
    {
        if (value == null || !value.IsChildOf(root))
        {
            errors++;
            Debug.LogError("[BronzeMarketValidator] " + label + " não pertence à própria loja.", context);
        }
    }

    private static void ValidateLocalComponent<T>(
        Transform root,
        T value,
        string label,
        Object context,
        ref int errors)
        where T : Component
    {
        if (value == null || !value.transform.IsChildOf(root))
        {
            errors++;
            Debug.LogError("[BronzeMarketValidator] " + label + " ausente ou fora da própria loja.", context);
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
