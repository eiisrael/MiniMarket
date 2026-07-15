using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>
/// Repara referências de compra sem misturar lojas independentes.
/// Lojas com BronzeMarketPurchaseLot mantêm controlador, trigger e terreno restritos à
/// própria hierarquia. Objetos legados continuam usando um controlador global de fallback.
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(-20000)]
public sealed class PurchaseSystemBootstrapHost : MonoBehaviour
{
    private const string RuntimeTriggerName = "BuySceneEntryTrigger_Runtime";

    private float stopScanAt;
    private float nextScan;
    private bool warnedNoTrigger;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void CreateAfterSceneLoad()
    {
        PurchaseSystemBootstrapHost existing = Object.FindAnyObjectByType<PurchaseSystemBootstrapHost>(
            FindObjectsInactive.Include
        );

        if (existing != null)
        {
            existing.ScheduleScan();
            return;
        }

        GameObject host = new GameObject("PurchaseSystemRuntimeRepair");
        DontDestroyOnLoad(host);
        host.AddComponent<PurchaseSystemBootstrapHost>();
    }

    private void Awake()
    {
        if (transform.parent == null)
            DontDestroyOnLoad(gameObject);

        SceneManager.sceneLoaded += HandleSceneLoaded;
        ScheduleScan();
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ScheduleScan();
    }

    private void ScheduleScan()
    {
        stopScanAt = Time.unscaledTime + 10f;
        nextScan = 0f;
        warnedNoTrigger = false;
    }

    private void Update()
    {
        if (Time.unscaledTime > stopScanAt || Time.unscaledTime < nextScan)
            return;

        nextScan = Time.unscaledTime + 1f;
        RepairCurrentScene();
    }

    [ContextMenu("Compra/Reparar sistema agora")]
    public void RepairCurrentScene()
    {
        EnsureTriggersForAllNamedBuyAreas();

        CameraRelativeMovement movement = Object.FindAnyObjectByType<CameraRelativeMovement>(
            FindObjectsInactive.Include
        );
        PlayerCameraController playerCamera = Object.FindAnyObjectByType<PlayerCameraController>(
            FindObjectsInactive.Include
        );
        BuyScenePurchaseConfirmationPanel panel = RepairConfirmationPanel();

        BronzeMarketPurchaseLot[] bronzeLots = Object.FindObjectsByType<BronzeMarketPurchaseLot>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None
        );

        for (int i = 0; i < bronzeLots.Length; i++)
        {
            BronzeMarketPurchaseLot lot = bronzeLots[i];
            if (lot == null)
                continue;

            if (lot.cameraDoJogador == null)
                lot.cameraDoJogador = playerCamera;
            if (lot.movimentoDoJogador == null)
                lot.movimentoDoJogador = movement;
            if (lot.painelConfirmacao == null)
                lot.painelConfirmacao = panel;

            lot.AplicarVinculosRuntime();
        }

        BuySceneEntryTrigger[] triggers = Object.FindObjectsByType<BuySceneEntryTrigger>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None
        );
        BuyableLandAreaMarker[] markers = Object.FindObjectsByType<BuyableLandAreaMarker>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None
        );

        List<BuySceneEntryTrigger> legacyTriggers = new List<BuySceneEntryTrigger>();
        List<BuyableLandAreaMarker> legacyMarkers = new List<BuyableLandAreaMarker>();

        for (int i = 0; i < triggers.Length; i++)
        {
            BuySceneEntryTrigger trigger = triggers[i];
            if (trigger == null)
                continue;

            BronzeMarketPurchaseLot lot = trigger.GetComponentInParent<BronzeMarketPurchaseLot>();
            if (lot != null)
            {
                lot.triggerEntrada = trigger;
                lot.AplicarVinculosRuntime();
                ConfigureTriggerVisual(trigger, movement);
                continue;
            }

            legacyTriggers.Add(trigger);
        }

        for (int i = 0; i < markers.Length; i++)
        {
            BuyableLandAreaMarker marker = markers[i];
            if (marker == null)
                continue;

            marker.exibirDemarcacao = true;
            marker.atualizarEmTempoReal = true;
            marker.enabled = true;
            marker.AtualizarVisualRuntime();

            if (marker.GetComponentInParent<BronzeMarketPurchaseLot>() == null)
                legacyMarkers.Add(marker);
        }

        if (legacyTriggers.Count > 0)
        {
            ConfigureLegacyFallback(
                legacyTriggers,
                legacyMarkers,
                movement,
                playerCamera,
                panel
            );
        }

        if (triggers.Length == 0 && !warnedNoTrigger)
        {
            warnedNoTrigger = true;
            Debug.LogWarning(
                "[PurchaseSystemRuntimeRepair] Nenhum BuySceneEntryTrigger ou Buy_Area válido foi localizado. " +
                "Cada Bronze_Market deve manter Buy_Area dentro da própria hierarquia."
            );
        }
    }

    private void ConfigureLegacyFallback(
        List<BuySceneEntryTrigger> triggers,
        List<BuyableLandAreaMarker> markers,
        CameraRelativeMovement movement,
        PlayerCameraController playerCamera,
        BuyScenePurchaseConfirmationPanel panel)
    {
        BuySceneCameraModeController controller = FindLegacyController();
        if (controller == null)
        {
            GameObject system = new GameObject("BuySceneSystemRuntime");
            controller = system.AddComponent<BuySceneCameraModeController>();
        }

        if (controller.cameraPrincipal == null)
            controller.cameraPrincipal = playerCamera != null ? playerCamera.gameCamera : Camera.main;

        if (controller.jogadorRaiz == null && movement != null)
            controller.jogadorRaiz = movement.transform;

        PurchaseModeBridge bridge = controller.GetComponent<PurchaseModeBridge>();
        if (bridge == null)
            bridge = controller.gameObject.AddComponent<PurchaseModeBridge>();

        bridge.purchaseController = controller;
        bridge.playerCamera = playerCamera;
        bridge.movement = movement;

        BuySceneLandPurchaseController purchase = controller.GetComponent<BuySceneLandPurchaseController>();
        if (purchase == null)
            purchase = controller.gameObject.AddComponent<BuySceneLandPurchaseController>();

        purchase.controladorBuyScene = controller;
        purchase.cameraCompra = controller.cameraPrincipal;
        purchase.painelConfirmacao = panel;
        purchase.terrenos = markers.ToArray();
        purchase.procurarTerrenosAutomaticamente = markers.Count == 0;
        purchase.enabled = true;

        for (int i = 0; i < triggers.Count; i++)
        {
            BuySceneEntryTrigger trigger = triggers[i];
            if (trigger == null)
                continue;

            trigger.controladorBuyScene = controller;
            trigger.jogadorRaizOpcional = movement != null
                ? movement.transform
                : trigger.jogadorRaizOpcional;
            ConfigureTriggerVisual(trigger, movement);
        }
    }

    private static BuySceneCameraModeController FindLegacyController()
    {
        BuySceneCameraModeController[] controllers = Object.FindObjectsByType<BuySceneCameraModeController>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None
        );

        for (int i = 0; i < controllers.Length; i++)
        {
            BuySceneCameraModeController controller = controllers[i];
            if (controller != null && controller.GetComponentInParent<BronzeMarketPurchaseLot>() == null)
                return controller;
        }

        return null;
    }

    private static void ConfigureTriggerVisual(
        BuySceneEntryTrigger trigger,
        CameraRelativeMovement movement)
    {
        trigger.jogadorRaizOpcional = movement != null
            ? movement.transform
            : trigger.jogadorRaizOpcional;
        trigger.mostrarMarcacaoVisual = true;
        trigger.mostrarXCentral = true;
        trigger.larguraLinha = Mathf.Max(trigger.larguraLinha, 0.14f);
        trigger.alturaAcimaDoCollider = Mathf.Max(trigger.alturaAcimaDoCollider, 0.12f);
        trigger.atualizarVisualEmTempoReal = true;
        trigger.enabled = true;
        trigger.AtualizarVisualRuntime();
    }

    private BuyScenePurchaseConfirmationPanel RepairConfirmationPanel()
    {
        BuyScenePurchaseConfirmationPanel panel = Object.FindAnyObjectByType<BuyScenePurchaseConfirmationPanel>(
            FindObjectsInactive.Include
        );
        if (panel != null)
        {
            panel.enabled = true;
            return panel;
        }

        Transform root = FindTransformByNames("PainelWarning", "PanelWarning", "Painel_Confirmacao");
        if (root == null)
            return null;

        panel = root.GetComponent<BuyScenePurchaseConfirmationPanel>();
        if (panel == null)
            panel = root.gameObject.AddComponent<BuyScenePurchaseConfirmationPanel>();

        panel.painelRaiz = root.gameObject;

        Transform textTransform = FindChildByNames(
            root,
            "TextAsking",
            "TextoConfirmacao",
            "Texto_Confirmacao"
        );
        if (textTransform != null)
            panel.textoPrincipal = textTransform.GetComponent<Text>();

        panel.botaoConfirmar = EnsureImageButton(
            root,
            "ButtonConfirm",
            "BotaoConfirmar",
            "Botao_Confirmar"
        );
        panel.botaoFechar = EnsureImageButton(
            root,
            "ButtonClose",
            "BotaoFechar",
            "Botao_Fechar"
        );
        panel.enabled = true;
        return panel;
    }

    private static BuySceneUIImageButton EnsureImageButton(Transform root, params string[] names)
    {
        Transform target = FindChildByNames(root, names);
        if (target == null)
            return null;

        BuySceneUIImageButton button = target.GetComponent<BuySceneUIImageButton>();
        if (button == null)
            button = target.gameObject.AddComponent<BuySceneUIImageButton>();
        return button;
    }

    private static void EnsureTriggersForAllNamedBuyAreas()
    {
        Collider[] colliders = Object.FindObjectsByType<Collider>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None
        );

        for (int i = 0; i < colliders.Length; i++)
        {
            Collider source = colliders[i];
            if (!IsNamedBuyAreaSource(source))
                continue;

            BuySceneEntryTrigger direct = source.GetComponent<BuySceneEntryTrigger>();
            if (direct != null)
                continue;

            Transform existing = source.transform.Find(RuntimeTriggerName);
            GameObject triggerObject;
            BoxCollider triggerCollider;

            if (existing != null)
            {
                triggerObject = existing.gameObject;
                triggerCollider = triggerObject.GetComponent<BoxCollider>();
                if (triggerCollider == null)
                    triggerCollider = triggerObject.AddComponent<BoxCollider>();
            }
            else
            {
                triggerObject = new GameObject(RuntimeTriggerName);
                triggerObject.transform.SetParent(source.transform, false);
                triggerCollider = triggerObject.AddComponent<BoxCollider>();
            }

            ConfigureChildTriggerFromSource(source, triggerObject.transform, triggerCollider);

            BuySceneEntryTrigger trigger = triggerObject.GetComponent<BuySceneEntryTrigger>();
            if (trigger == null)
                trigger = triggerObject.AddComponent<BuySceneEntryTrigger>();

            BronzeMarketPurchaseLot lot = source.GetComponentInParent<BronzeMarketPurchaseLot>();
            if (lot != null)
            {
                lot.buyArea = source.transform;
                lot.colliderSolidoDaCalcada = source;
                lot.triggerEntrada = trigger;
            }
        }
    }

    private static bool IsNamedBuyAreaSource(Collider collider)
    {
        if (collider == null || collider.GetComponentInParent<Canvas>() != null)
            return false;
        if (collider.GetComponent<BuySceneEntryTrigger>() != null)
            return false;
        if (string.Equals(collider.name, RuntimeTriggerName, StringComparison.OrdinalIgnoreCase))
            return false;

        string lower = collider.name.ToLowerInvariant();
        string compact = Compact(lower);
        bool exactBuyArea = compact.Contains("buyarea") || compact.Contains("areabuy");
        bool entryName = ContainsAny(lower, "entrada", "entry", "calcada", "calçada", "pisar");
        bool purchaseName = ContainsAny(lower, "compra", "buy", "terreno", "land", "area", "área");

        return exactBuyArea || (entryName && purchaseName);
    }

    private static void ConfigureChildTriggerFromSource(
        Collider source,
        Transform triggerTransform,
        BoxCollider triggerCollider)
    {
        Bounds worldBounds = source.bounds;
        Vector3 scale = source.transform.lossyScale;
        float scaleX = Mathf.Max(0.0001f, Mathf.Abs(scale.x));
        float scaleY = Mathf.Max(0.0001f, Mathf.Abs(scale.y));
        float scaleZ = Mathf.Max(0.0001f, Mathf.Abs(scale.z));

        triggerTransform.localPosition = source.transform.InverseTransformPoint(worldBounds.center);
        triggerTransform.localRotation = Quaternion.identity;
        triggerTransform.localScale = Vector3.one;

        triggerCollider.center = Vector3.zero;
        triggerCollider.size = new Vector3(
            Mathf.Max(0.8f, worldBounds.size.x / scaleX),
            Mathf.Max(1.2f, worldBounds.size.y / scaleY + 1f),
            Mathf.Max(0.8f, worldBounds.size.z / scaleZ)
        );
        triggerCollider.isTrigger = true;
        triggerCollider.enabled = true;
    }

    private static Transform FindTransformByNames(params string[] names)
    {
        Transform[] transforms = Object.FindObjectsByType<Transform>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None
        );

        for (int i = 0; i < transforms.Length; i++)
        {
            Transform current = transforms[i];
            if (current == null)
                continue;

            for (int n = 0; n < names.Length; n++)
            {
                if (string.Equals(current.name, names[n], StringComparison.OrdinalIgnoreCase))
                    return current;
            }
        }

        return null;
    }

    private static Transform FindChildByNames(Transform root, params string[] names)
    {
        if (root == null)
            return null;

        Transform[] children = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            Transform current = children[i];
            for (int n = 0; n < names.Length; n++)
            {
                if (string.Equals(current.name, names[n], StringComparison.OrdinalIgnoreCase))
                    return current;
            }
        }

        return null;
    }

    private static bool ContainsAny(string value, params string[] terms)
    {
        for (int i = 0; i < terms.Length; i++)
        {
            if (value.Contains(terms[i]))
                return true;
        }
        return false;
    }

    private static string Compact(string value)
    {
        return value.Replace("_", string.Empty)
            .Replace("-", string.Empty)
            .Replace(" ", string.Empty)
            .Replace("á", "a")
            .Replace("ç", "c");
    }
}
