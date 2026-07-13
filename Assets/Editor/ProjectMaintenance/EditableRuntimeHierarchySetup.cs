#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>
/// Converte os principais objetos que antes existiam somente durante o Play Mode
/// em objetos persistentes da cena, editáveis pela Hierarchy e Inspector.
/// </summary>
public static class EditableRuntimeHierarchySetup
{
    private const string ConfigurationRootName = "GameSystemsConfiguration";
    private const string CircleSpritePath = "Assets/Generated/MiniMarket/UI/MiniMapCircle.png";
    private const string BuyLineMaterialPath = "Assets/Generated/MiniMarket/Materials/BuyAreaLine.mat";

    [MenuItem("Tools/MiniMarket/Materializar Todos os Objetos Runtime na Hierarquia", priority = 0)]
    public static void MaterializarTudo()
    {
        if (!ValidarCenaParaEdicao(out Scene scene))
            return;

        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Materializar objetos runtime");

        try
        {
            GameObject configurationRoot = FindOrCreateRoot(ConfigurationRootName);
            EnsureEventSystem();

            Sprite circleSprite = EnsureCircleSpriteAsset();
            Material buyLineMaterial = EnsureBuyLineMaterialAsset();

            MaterializarMiniMapa(configurationRoot, circleSprite);
            MaterializarHudMobile(configurationRoot, circleSprite);
            MaterializarMira(configurationRoot);
            MaterializarDiagnosticos();
            MaterializarCompra(buyLineMaterial);

            // Cria EnergyProgressArea/EnergyProgressFill e salva as referências.
            EnergyProgressBarSetup.CriarOuReparar();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Undo.CollapseUndoOperations(undoGroup);

            Debug.Log(
                "[EditableRuntimeHierarchySetup] Concluído. Os sistemas visuais runtime " +
                "foram materializados e salvos na cena. Agora podem ser editados fora do Play Mode."
            );
        }
        catch (Exception exception)
        {
            Debug.LogError(
                "[EditableRuntimeHierarchySetup] Falha controlada: " + exception
            );
        }
    }

    [MenuItem("Tools/MiniMarket/Validar Objetos Runtime Persistentes", priority = 1)]
    public static void ValidarTudo()
    {
        int errors = 0;
        int warnings = 0;

        RuntimeMiniMap miniMap = Object.FindAnyObjectByType<RuntimeMiniMap>(FindObjectsInactive.Include);
        RuntimeMiniMapHierarchyBinding miniMapBinding =
            Object.FindAnyObjectByType<RuntimeMiniMapHierarchyBinding>(FindObjectsInactive.Include);
        MobileControlsHUD mobile = Object.FindAnyObjectByType<MobileControlsHUD>(FindObjectsInactive.Include);
        MobileControlsHierarchyBinding mobileBinding =
            Object.FindAnyObjectByType<MobileControlsHierarchyBinding>(FindObjectsInactive.Include);
        FirstPersonReticleController reticle =
            Object.FindAnyObjectByType<FirstPersonReticleController>(FindObjectsInactive.Include);
        RuntimeDiagnosticsPanel diagnostics =
            Object.FindAnyObjectByType<RuntimeDiagnosticsPanel>(FindObjectsInactive.Include);
        PurchaseSystemBootstrapHost purchaseRepair =
            Object.FindAnyObjectByType<PurchaseSystemBootstrapHost>(FindObjectsInactive.Include);
        MiniMarketEnergyProgressBar energy =
            Object.FindAnyObjectByType<MiniMarketEnergyProgressBar>(FindObjectsInactive.Include);

        if (miniMap == null) { errors++; Debug.LogError("[RuntimeHierarchyValidator] RuntimeMiniMap ausente."); }
        if (miniMapBinding == null) { errors++; Debug.LogError("[RuntimeHierarchyValidator] RuntimeMiniMapHierarchyBinding ausente."); }
        else
        {
            if (miniMapBinding.mapCamera == null) { errors++; Debug.LogError("[RuntimeHierarchyValidator] Câmera persistente do minimapa ausente."); }
            if (miniMapBinding.canvas == null) { errors++; Debug.LogError("[RuntimeHierarchyValidator] Canvas persistente do minimapa ausente."); }
            if (miniMapBinding.mapImage == null) { errors++; Debug.LogError("[RuntimeHierarchyValidator] MapImage persistente ausente."); }
        }

        if (mobile == null) { warnings++; Debug.LogWarning("[RuntimeHierarchyValidator] MobileControlsHUD ausente."); }
        if (mobileBinding == null) { warnings++; Debug.LogWarning("[RuntimeHierarchyValidator] MobileControlsHierarchyBinding ausente."); }
        else if (mobileBinding.canvas == null || mobileBinding.joystickBase == null || mobileBinding.actionsRoot == null)
        {
            errors++;
            Debug.LogError("[RuntimeHierarchyValidator] Hierarquia persistente do HUD mobile está incompleta.");
        }

        if (reticle == null) { warnings++; Debug.LogWarning("[RuntimeHierarchyValidator] FirstPersonReticleController ausente."); }
        if (diagnostics == null) { warnings++; Debug.LogWarning("[RuntimeHierarchyValidator] RuntimeDiagnosticsPanel ausente."); }
        if (purchaseRepair == null) { warnings++; Debug.LogWarning("[RuntimeHierarchyValidator] PurchaseSystemBootstrapHost ausente."); }

        if (energy == null)
        {
            errors++;
            Debug.LogError("[RuntimeHierarchyValidator] MiniMarketEnergyProgressBar ausente.");
        }
        else
        {
            if (energy.areaPreenchimento == null) { errors++; Debug.LogError("[RuntimeHierarchyValidator] EnergyProgressArea ausente."); }
            if (energy.preenchimentoVerde == null) { errors++; Debug.LogError("[RuntimeHierarchyValidator] EnergyProgressFill ausente."); }
        }

        BuySceneEntryTrigger[] triggers = Object.FindObjectsByType<BuySceneEntryTrigger>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None
        );
        for (int i = 0; i < triggers.Length; i++)
        {
            if (triggers[i] == null)
                continue;

            if (triggers[i].transform.Find("BuyScene_Entrada_Borda") == null)
            {
                warnings++;
                Debug.LogWarning(
                    "[RuntimeHierarchyValidator] Marcação persistente ausente em " + triggers[i].name,
                    triggers[i]
                );
            }
        }

        Debug.Log(
            "[RuntimeHierarchyValidator] Finalizado. Erros=" + errors +
            ", avisos=" + warnings + "."
        );
    }

    private static bool ValidarCenaParaEdicao(out Scene scene)
    {
        scene = SceneManager.GetActiveScene();

        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning(
                "[EditableRuntimeHierarchySetup] Saia do Play Mode antes de executar."
            );
            return false;
        }

        if (!scene.IsValid() || !scene.isLoaded || string.IsNullOrWhiteSpace(scene.path))
        {
            Debug.LogWarning(
                "[EditableRuntimeHierarchySetup] Abra e salve a SampleScene antes de executar."
            );
            return false;
        }

        return true;
    }

    private static void MaterializarMiniMapa(GameObject configurationRoot, Sprite circleSprite)
    {
        RuntimeMiniMap miniMap = Object.FindAnyObjectByType<RuntimeMiniMap>(FindObjectsInactive.Include);
        if (miniMap == null)
        {
            GameObject host = FindOrCreateChild(configurationRoot.transform, "MiniMapSystem");
            miniMap = EnsureComponent<RuntimeMiniMap>(host);
        }

        GameObject hostObject = miniMap.gameObject;
        RuntimeMiniMapHierarchyBinding binding = EnsureComponent<RuntimeMiniMapHierarchyBinding>(hostObject);

        GameObject cameraObject = FindOrCreateChild(hostObject.transform, "RuntimeMiniMapCamera");
        Camera mapCamera = EnsureComponent<Camera>(cameraObject);
        mapCamera.enabled = false;
        mapCamera.orthographic = true;
        mapCamera.clearFlags = CameraClearFlags.SolidColor;
        mapCamera.depth = -100f;
        mapCamera.allowHDR = false;
        mapCamera.allowMSAA = false;
        mapCamera.useOcclusionCulling = false;
        AudioListener listener = cameraObject.GetComponent<AudioListener>();
        if (listener != null)
            Undo.DestroyObjectImmediate(listener);

        GameObject canvasObject = FindOrCreateChild(hostObject.transform, "RuntimeMiniMapCanvas", true);
        Canvas canvas = EnsureComponent<Canvas>(canvasObject);
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        CanvasScaler scaler = EnsureComponent<CanvasScaler>(canvasObject);
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        EnsureComponent<GraphicRaycaster>(canvasObject);

        RectTransform root = EnsureRectChild(canvasObject.transform, "MiniMap");
        Image border = EnsureImage(root, "Border", circleSprite, miniMap.borderColor);
        RectTransform borderRect = border.rectTransform;

        Image maskImage = EnsureImage(borderRect, "CircularMask", circleSprite, Color.white);
        RectTransform maskRect = maskImage.rectTransform;
        maskRect.anchorMin = Vector2.zero;
        maskRect.anchorMax = Vector2.one;
        maskRect.offsetMin = new Vector2(5f, 5f);
        maskRect.offsetMax = new Vector2(-5f, -5f);
        Mask mask = EnsureComponent<Mask>(maskImage.gameObject);
        mask.showMaskGraphic = false;

        RawImage mapImage = EnsureComponent<RawImage>(
            FindOrCreateChild(maskRect, "MapImage", true)
        );
        Stretch(mapImage.rectTransform);
        mapImage.raycastTarget = false;
        mapImage.texture = null;

        Image playerDot = EnsureImage(maskRect, "PlayerDot", circleSprite, miniMap.playerColor);
        playerDot.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        playerDot.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        playerDot.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        playerDot.rectTransform.anchoredPosition = Vector2.zero;
        playerDot.rectTransform.sizeDelta = new Vector2(14f, 14f);
        playerDot.raycastTarget = false;

        Button zoomIn = EnsureButton(root, "ZoomIn", "+", circleSprite, miniMap.buttonColor, 24);
        Button zoomOut = EnsureButton(root, "ZoomOut", "−", circleSprite, miniMap.buttonColor, 24);

        Undo.RecordObject(binding, "Ligar hierarquia persistente do minimapa");
        binding.miniMap = miniMap;
        binding.mapCamera = mapCamera;
        binding.canvas = canvas;
        binding.rootRect = root;
        binding.mapImage = mapImage;
        binding.borderImage = border;
        binding.borderRect = borderRect;
        binding.playerDot = playerDot;
        binding.zoomInButton = zoomIn;
        binding.zoomOutButton = zoomOut;
        binding.aplicarPreviewNoEditor = true;

        if (miniMap.target == null)
        {
            CameraRelativeMovement movement =
                Object.FindAnyObjectByType<CameraRelativeMovement>(FindObjectsInactive.Include);
            if (movement != null)
                miniMap.target = movement.transform;
        }

        binding.AplicarHierarquiaPersistente();
        root.gameObject.SetActive(true);

        EditorUtility.SetDirty(miniMap);
        EditorUtility.SetDirty(binding);
    }

    private static void MaterializarHudMobile(GameObject configurationRoot, Sprite circleSprite)
    {
        MobileControlsHUD mobile = Object.FindAnyObjectByType<MobileControlsHUD>(FindObjectsInactive.Include);
        if (mobile == null)
        {
            GameObject host = FindOrCreateChild(configurationRoot.transform, "MobileControlsSystem");
            mobile = EnsureComponent<MobileControlsHUD>(host);
        }

        GameObject hostObject = mobile.gameObject;
        MobileControlsHierarchyBinding binding = EnsureComponent<MobileControlsHierarchyBinding>(hostObject);

        GameObject canvasObject = FindOrCreateChild(hostObject.transform, "MobileControlsRuntime", true);
        Canvas canvas = EnsureComponent<Canvas>(canvasObject);
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = mobile.canvasSortingOrder;
        CanvasScaler scaler = EnsureComponent<CanvasScaler>(canvasObject);
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        EnsureComponent<GraphicRaycaster>(canvasObject);

        RectTransform safeArea = EnsureRectChild(canvasObject.transform, "SafeArea");
        Stretch(safeArea);

        Image lookArea = EnsureImage(
            safeArea,
            "LookArea",
            null,
            new Color(0f, 0f, 0f, 0.001f)
        );
        lookArea.rectTransform.anchorMin = new Vector2(1f - mobile.larguraAreaOlhar, 0f);
        lookArea.rectTransform.anchorMax = Vector2.one;
        lookArea.rectTransform.offsetMin = Vector2.zero;
        lookArea.rectTransform.offsetMax = Vector2.zero;
        lookArea.raycastTarget = true;
        EnsureComponent<EventTrigger>(lookArea.gameObject);

        Image joystick = EnsureImage(
            safeArea,
            "MoveJoystick",
            circleSprite,
            mobile.corJoystickFundo
        );
        joystick.rectTransform.anchorMin = Vector2.zero;
        joystick.rectTransform.anchorMax = Vector2.zero;
        joystick.rectTransform.pivot = Vector2.zero;
        joystick.rectTransform.anchoredPosition = mobile.margemJoystick;
        joystick.rectTransform.sizeDelta = Vector2.one * mobile.tamanhoJoystick;
        joystick.raycastTarget = true;
        EnsureComponent<EventTrigger>(joystick.gameObject);

        Image thumb = EnsureImage(
            joystick.rectTransform,
            "Thumb",
            circleSprite,
            mobile.corJoystickPino
        );
        thumb.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        thumb.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        thumb.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        thumb.rectTransform.anchoredPosition = Vector2.zero;
        thumb.rectTransform.sizeDelta = Vector2.one * (mobile.tamanhoJoystick * 0.42f);
        thumb.raycastTarget = false;

        RectTransform actions = EnsureRectChild(safeArea, "Actions");
        actions.anchorMin = new Vector2(1f, 0f);
        actions.anchorMax = new Vector2(1f, 0f);
        actions.pivot = new Vector2(1f, 0f);
        actions.anchoredPosition = new Vector2(-mobile.margemBotoes.x, mobile.margemBotoes.y);
        actions.sizeDelta = new Vector2(
            mobile.tamanhoBotao * 3f + mobile.espacamentoBotoes * 2f,
            mobile.tamanhoBotao * 3f + mobile.espacamentoBotoes * 2f
        );

        float step = mobile.tamanhoBotao + mobile.espacamentoBotoes;
        Image aim = EnsureMobileButton(actions, "Aim", mobile.textoMirar, Vector2.zero, mobile, circleSprite);
        Image jump = EnsureMobileButton(actions, "Jump", mobile.textoPular, new Vector2(-step, 0f), mobile, circleSprite);
        Image run = EnsureMobileButton(actions, "Run", mobile.textoCorrer, new Vector2(-step * 2f, 0f), mobile, circleSprite);
        Image grab = EnsureMobileButton(actions, "Grab", mobile.textoPegar, new Vector2(0f, step), mobile, circleSprite);
        Image interact = EnsureMobileButton(actions, "Interact", mobile.textoInteragir, new Vector2(-step, step), mobile, circleSprite);
        Image throwButton = EnsureMobileButton(actions, "Throw", mobile.textoArremessar, new Vector2(0f, step * 2f), mobile, circleSprite);

        Undo.RecordObject(binding, "Ligar hierarquia persistente do HUD mobile");
        binding.mobileHud = mobile;
        binding.canvas = canvas;
        binding.safeArea = safeArea;
        binding.lookArea = lookArea.gameObject;
        binding.joystickBase = joystick.rectTransform;
        binding.joystickThumb = thumb.rectTransform;
        binding.actionsRoot = actions;
        binding.aimButton = aim;
        binding.jumpButton = jump;
        binding.runButton = run;
        binding.grabButton = grab;
        binding.interactButton = interact;
        binding.throwButton = throwButton;
        binding.manterVisivelForaDoPlay = true;

        CameraRelativeMovement movement =
            Object.FindAnyObjectByType<CameraRelativeMovement>(FindObjectsInactive.Include);
        PlayerCameraController camera =
            Object.FindAnyObjectByType<PlayerCameraController>(FindObjectsInactive.Include);
        InteractionFocusController interaction =
            Object.FindAnyObjectByType<InteractionFocusController>(FindObjectsInactive.Include);
        GetItemController getItem =
            Object.FindAnyObjectByType<GetItemController>(FindObjectsInactive.Include);

        mobile.movimento = movement;
        mobile.cameraController = camera;
        mobile.interactionController = interaction;
        mobile.getItemController = getItem;

        binding.AplicarHierarquiaPersistente();
        canvasObject.SetActive(true);

        EditorUtility.SetDirty(mobile);
        EditorUtility.SetDirty(binding);
    }

    private static void MaterializarMira(GameObject configurationRoot)
    {
        FirstPersonReticleController controller =
            Object.FindAnyObjectByType<FirstPersonReticleController>(FindObjectsInactive.Include);

        if (controller == null)
        {
            GameObject host = FindOrCreateChild(configurationRoot.transform, "FirstPersonReticleSystem");
            controller = EnsureComponent<FirstPersonReticleController>(host);
        }

        Sprite clickOff = FindSpriteByName("click_off");
        Sprite clickOn = FindSpriteByName("click_on");

        if (clickOff != null)
            controller.idleSprite = clickOff;
        if (clickOn != null)
        {
            controller.selectedSprite = clickOn;
            controller.holdingSprite = clickOn;
        }

        Image reticle = FindReticleImage();
        if (reticle == null)
        {
            Canvas targetCanvas = FindPrimaryCanvas();
            if (targetCanvas != null)
            {
                reticle = EnsureImage(targetCanvas.transform, "Mira", clickOff, Color.white);
                reticle.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
                reticle.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                reticle.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                reticle.rectTransform.anchoredPosition = Vector2.zero;
                reticle.rectTransform.sizeDelta = new Vector2(48f, 48f);
                reticle.preserveAspect = true;
                reticle.raycastTarget = false;
            }
        }
        else if (reticle.sprite == null && clickOff != null)
        {
            reticle.sprite = clickOff;
        }

        controller.Rescan();
        EditorUtility.SetDirty(controller);
        if (reticle != null)
            EditorUtility.SetDirty(reticle);
    }

    private static void MaterializarDiagnosticos()
    {
        RuntimeDiagnosticsPanel panel =
            Object.FindAnyObjectByType<RuntimeDiagnosticsPanel>(FindObjectsInactive.Include);

        if (panel == null)
        {
            GameObject host = FindOrCreateRoot("RuntimeDiagnosticsPanel");
            panel = EnsureComponent<RuntimeDiagnosticsPanel>(host);
        }

        EditorUtility.SetDirty(panel);
    }

    private static void MaterializarCompra(Material persistentLineMaterial)
    {
        PurchaseSystemBootstrapHost repair =
            Object.FindAnyObjectByType<PurchaseSystemBootstrapHost>(FindObjectsInactive.Include);

        if (repair == null)
        {
            GameObject repairHost = FindOrCreateRoot("PurchaseSystemRuntimeRepair");
            repair = EnsureComponent<PurchaseSystemBootstrapHost>(repairHost);
        }

        CameraRelativeMovement movement =
            Object.FindAnyObjectByType<CameraRelativeMovement>(FindObjectsInactive.Include);
        PlayerCameraController playerCamera =
            Object.FindAnyObjectByType<PlayerCameraController>(FindObjectsInactive.Include);
        BuySceneCameraModeController controller =
            Object.FindAnyObjectByType<BuySceneCameraModeController>(FindObjectsInactive.Include);

        if (controller == null)
        {
            GameObject buySystem = FindRootByNames("BUY_SYSTEM", "BuySceneSystem", "BuySceneSystemRuntime");
            if (buySystem == null)
                buySystem = FindOrCreateRoot("BUY_SYSTEM");
            controller = EnsureComponent<BuySceneCameraModeController>(buySystem);
        }

        if (controller.cameraPrincipal == null)
            controller.cameraPrincipal = playerCamera != null ? playerCamera.gameCamera : Camera.main;
        if (controller.jogadorRaiz == null && movement != null)
            controller.jogadorRaiz = movement.transform;

        PurchaseModeBridge bridge = EnsureComponent<PurchaseModeBridge>(controller.gameObject);
        bridge.purchaseController = controller;
        bridge.playerCamera = playerCamera;
        bridge.movement = movement;

        BuySceneLandPurchaseController purchase =
            EnsureComponent<BuySceneLandPurchaseController>(controller.gameObject);
        purchase.controladorBuyScene = controller;
        purchase.cameraCompra = controller.cameraPrincipal;
        purchase.painelConfirmacao =
            Object.FindAnyObjectByType<BuyScenePurchaseConfirmationPanel>(FindObjectsInactive.Include);
        purchase.terrenos = Object.FindObjectsByType<BuyableLandAreaMarker>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None
        );
        purchase.procurarTerrenosAutomaticamente = true;

        repair.RepairCurrentScene();

        BuySceneEntryTrigger[] triggers = Object.FindObjectsByType<BuySceneEntryTrigger>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None
        );
        for (int i = 0; i < triggers.Length; i++)
        {
            BuySceneEntryTrigger trigger = triggers[i];
            if (trigger == null)
                continue;

            trigger.SendMessage("CriarRenderizadores", SendMessageOptions.DontRequireReceiver);
            trigger.SendMessage("AtualizarVisualCompleto", SendMessageOptions.DontRequireReceiver);
            AssignLineMaterial(trigger.transform, persistentLineMaterial);
            EditorUtility.SetDirty(trigger);
        }

        BuyableLandAreaMarker[] markers = Object.FindObjectsByType<BuyableLandAreaMarker>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None
        );
        for (int i = 0; i < markers.Length; i++)
        {
            BuyableLandAreaMarker marker = markers[i];
            if (marker == null)
                continue;

            marker.materialLinha = persistentLineMaterial;
            marker.SendMessage("CriarOuAtualizarLinhas", SendMessageOptions.DontRequireReceiver);
            AssignLineMaterial(marker.transform, persistentLineMaterial);
            EditorUtility.SetDirty(marker);
        }

        EditorUtility.SetDirty(repair);
        EditorUtility.SetDirty(controller);
        EditorUtility.SetDirty(bridge);
        EditorUtility.SetDirty(purchase);
    }

    private static Image EnsureMobileButton(
        Transform parent,
        string name,
        string label,
        Vector2 position,
        MobileControlsHUD mobile,
        Sprite sprite)
    {
        Image image = EnsureImage(parent, name, sprite, mobile.corBotao);
        RectTransform rect = image.rectTransform;
        rect.anchorMin = new Vector2(1f, 0f);
        rect.anchorMax = new Vector2(1f, 0f);
        rect.pivot = new Vector2(1f, 0f);
        rect.anchoredPosition = position;
        rect.sizeDelta = Vector2.one * mobile.tamanhoBotao;
        image.raycastTarget = true;
        EnsureComponent<EventTrigger>(image.gameObject);

        Text text = EnsureText(rect, "Label", label, mobile.tamanhoFonte, mobile.corTexto);
        Stretch(text.rectTransform);
        text.alignment = TextAnchor.MiddleCenter;
        text.raycastTarget = false;
        return image;
    }

    private static Button EnsureButton(
        Transform parent,
        string name,
        string label,
        Sprite sprite,
        Color color,
        int fontSize)
    {
        Image image = EnsureImage(parent, name, sprite, color);
        Button button = EnsureComponent<Button>(image.gameObject);
        button.targetGraphic = image;

        Text text = EnsureText(image.rectTransform, "Label", label, fontSize, Color.white);
        Stretch(text.rectTransform);
        text.alignment = TextAnchor.MiddleCenter;
        text.raycastTarget = false;
        return button;
    }

    private static Image EnsureImage(
        Transform parent,
        string name,
        Sprite sprite,
        Color color)
    {
        GameObject target = FindOrCreateChild(parent, name, true);
        Image image = EnsureComponent<Image>(target);
        image.sprite = sprite;
        image.color = color;
        return image;
    }

    private static Text EnsureText(
        Transform parent,
        string name,
        string value,
        int fontSize,
        Color color)
    {
        GameObject target = FindOrCreateChild(parent, name, true);
        Text text = EnsureComponent<Text>(target);
        text.text = value;
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = fontSize;
        text.color = color;
        return text;
    }

    private static RectTransform EnsureRectChild(Transform parent, string name)
    {
        GameObject target = FindOrCreateChild(parent, name, true);
        RectTransform rect = target.GetComponent<RectTransform>();
        if (rect == null)
        {
            Undo.DestroyObjectImmediate(target);
            target = FindOrCreateChild(parent, name, true);
            rect = target.GetComponent<RectTransform>();
        }
        return rect;
    }

    private static GameObject FindOrCreateChild(
        Transform parent,
        string name,
        bool rectTransform = false)
    {
        Transform existing = parent != null ? parent.Find(name) : null;
        if (existing != null)
            return existing.gameObject;

        GameObject created = rectTransform
            ? new GameObject(name, typeof(RectTransform))
            : new GameObject(name);

        Undo.RegisterCreatedObjectUndo(created, "Criar " + name);
        if (parent != null)
            created.transform.SetParent(parent, false);
        return created;
    }

    private static GameObject FindOrCreateRoot(string name)
    {
        GameObject existing = FindRootByNames(name);
        if (existing != null)
            return existing;

        GameObject created = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(created, "Criar " + name);
        return created;
    }

    private static GameObject FindRootByNames(params string[] names)
    {
        GameObject[] objects = Object.FindObjectsByType<GameObject>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None
        );

        for (int i = 0; i < objects.Length; i++)
        {
            GameObject current = objects[i];
            if (current == null || current.transform.parent != null)
                continue;

            for (int n = 0; n < names.Length; n++)
            {
                if (string.Equals(current.name, names[n], StringComparison.OrdinalIgnoreCase))
                    return current;
            }
        }

        return null;
    }

    private static T EnsureComponent<T>(GameObject target) where T : Component
    {
        T component = target.GetComponent<T>();
        if (component == null)
            component = Undo.AddComponent<T>(target);
        return component;
    }

    private static void EnsureEventSystem()
    {
        EventSystem existing = Object.FindAnyObjectByType<EventSystem>(FindObjectsInactive.Include);
        if (existing != null)
            return;

        GameObject eventSystem = new GameObject(
            "EventSystem",
            typeof(EventSystem),
            typeof(StandaloneInputModule)
        );
        Undo.RegisterCreatedObjectUndo(eventSystem, "Criar EventSystem");
    }

    private static Canvas FindPrimaryCanvas()
    {
        Canvas[] canvases = Object.FindObjectsByType<Canvas>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None
        );

        Canvas fallback = null;
        for (int i = 0; i < canvases.Length; i++)
        {
            Canvas canvas = canvases[i];
            if (canvas == null)
                continue;

            string lower = canvas.name.ToLowerInvariant();
            if (lower.Contains("runtime") || lower.Contains("mobile") || lower.Contains("minimap"))
                continue;

            if (string.Equals(canvas.name, "Canvas", StringComparison.OrdinalIgnoreCase))
                return canvas;

            if (fallback == null)
                fallback = canvas;
        }

        return fallback;
    }

    private static Image FindReticleImage()
    {
        Image[] images = Object.FindObjectsByType<Image>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None
        );

        for (int i = 0; i < images.Length; i++)
        {
            Image image = images[i];
            if (image == null || image.GetComponentInParent<Canvas>() == null)
                continue;

            string lower = image.name.ToLowerInvariant();
            if (lower.Contains("mira") ||
                lower.Contains("reticle") ||
                lower.Contains("crosshair") ||
                lower.Contains("click_cursor"))
            {
                return image;
            }
        }

        return null;
    }

    private static Sprite FindSpriteByName(string desiredName)
    {
        string[] guids = AssetDatabase.FindAssets(desiredName + " t:Sprite", new[] { "Assets" });
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            Object[] assets = AssetDatabase.LoadAllAssetsAtPath(path);
            for (int a = 0; a < assets.Length; a++)
            {
                if (assets[a] is Sprite sprite &&
                    sprite.name.IndexOf(desiredName, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return sprite;
                }
            }
        }

        return null;
    }

    private static Sprite EnsureCircleSpriteAsset()
    {
        Sprite existing = AssetDatabase.LoadAssetAtPath<Sprite>(CircleSpritePath);
        if (existing != null)
            return existing;

        string directory = Path.GetDirectoryName(CircleSpritePath);
        if (!Directory.Exists(directory))
            Directory.CreateDirectory(directory);

        const int resolution = 128;
        Texture2D texture = new Texture2D(
            resolution,
            resolution,
            TextureFormat.RGBA32,
            false
        );
        texture.name = "MiniMapCircle";

        Color[] pixels = new Color[resolution * resolution];
        Vector2 center = new Vector2((resolution - 1) * 0.5f, (resolution - 1) * 0.5f);
        float radius = resolution * 0.5f - 1f;

        for (int y = 0; y < resolution; y++)
        {
            for (int x = 0; x < resolution; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), center);
                float alpha = Mathf.Clamp01(radius - distance + 1f);
                pixels[y * resolution + x] = new Color(1f, 1f, 1f, alpha);
            }
        }

        texture.SetPixels(pixels);
        texture.Apply(false, false);
        File.WriteAllBytes(CircleSpritePath, texture.EncodeToPNG());
        Object.DestroyImmediate(texture);

        AssetDatabase.ImportAsset(CircleSpritePath, ImportAssetOptions.ForceSynchronousImport);
        TextureImporter importer = AssetImporter.GetAtPath(CircleSpritePath) as TextureImporter;
        if (importer != null)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }

        return AssetDatabase.LoadAssetAtPath<Sprite>(CircleSpritePath);
    }

    private static Material EnsureBuyLineMaterialAsset()
    {
        Material existing = AssetDatabase.LoadAssetAtPath<Material>(BuyLineMaterialPath);
        if (existing != null)
            return existing;

        string directory = Path.GetDirectoryName(BuyLineMaterialPath);
        if (!Directory.Exists(directory))
            Directory.CreateDirectory(directory);

        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Sprites/Default");
        if (shader == null) shader = Shader.Find("Unlit/Color");
        if (shader == null) shader = Shader.Find("Standard");

        if (shader == null)
            return null;

        Material material = new Material(shader)
        {
            name = "BuyAreaLine"
        };
        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", Color.white);
        if (material.HasProperty("_Color"))
            material.SetColor("_Color", Color.white);

        AssetDatabase.CreateAsset(material, BuyLineMaterialPath);
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

            Undo.RecordObject(lines[i], "Atribuir material persistente");
            lines[i].sharedMaterial = material;
            EditorUtility.SetDirty(lines[i]);
        }
    }

    private static void Stretch(RectTransform rect)
    {
        if (rect == null)
            return;

        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }
}
#endif
