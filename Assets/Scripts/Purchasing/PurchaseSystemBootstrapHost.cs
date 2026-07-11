using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>
/// Repara automaticamente referências removidas pela organização antiga e recria os
/// LineRenderers da entrada/terrenos. Não cria uma área em posição arbitrária: quando
/// não encontra um trigger válido, registra um aviso preciso no Console.
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(-20000)]
public sealed class PurchaseSystemBootstrapHost : MonoBehaviour
{
    private float stopScanAt;
    private float nextScan;
    private bool warnedNoTrigger;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void CreateAfterSceneLoad()
    {
        PurchaseSystemBootstrapHost existing = Object.FindAnyObjectByType<PurchaseSystemBootstrapHost>(FindObjectsInactive.Include);
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

    private void RepairCurrentScene()
    {
        BuyableLandAreaMarker[] markers = Object.FindObjectsByType<BuyableLandAreaMarker>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None
        );

        BuySceneEntryTrigger[] triggers = Object.FindObjectsByType<BuySceneEntryTrigger>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None
        );

        if (markers.Length == 0 && triggers.Length == 0)
            return;

        CameraRelativeMovement movement = Object.FindAnyObjectByType<CameraRelativeMovement>(FindObjectsInactive.Include);
        PlayerCameraController playerCamera = Object.FindAnyObjectByType<PlayerCameraController>(FindObjectsInactive.Include);
        BuySceneCameraModeController controller = Object.FindAnyObjectByType<BuySceneCameraModeController>(FindObjectsInactive.Include);

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

        BuyScenePurchaseConfirmationPanel panel = RepairConfirmationPanel();
        BuySceneLandPurchaseController purchase = Object.FindAnyObjectByType<BuySceneLandPurchaseController>(FindObjectsInactive.Include);
        if (purchase == null)
            purchase = controller.gameObject.AddComponent<BuySceneLandPurchaseController>();

        purchase.controladorBuyScene = controller;
        purchase.cameraCompra = controller.cameraPrincipal;
        purchase.painelConfirmacao = panel;
        purchase.terrenos = markers;
        purchase.procurarTerrenosAutomaticamente = true;
        purchase.enabled = true;

        if (triggers.Length == 0)
            triggers = TryCreateTriggerFromNamedCollider();

        for (int i = 0; i < triggers.Length; i++)
        {
            BuySceneEntryTrigger trigger = triggers[i];
            if (trigger == null)
                continue;

            trigger.controladorBuyScene = controller;
            trigger.jogadorRaizOpcional = movement != null ? movement.transform : trigger.jogadorRaizOpcional;
            trigger.mostrarMarcacaoVisual = true;
            trigger.atualizarVisualEmTempoReal = true;
            trigger.enabled = true;
            trigger.SendMessage("CriarRenderizadores", SendMessageOptions.DontRequireReceiver);
            trigger.SendMessage("AtualizarVisualCompleto", SendMessageOptions.DontRequireReceiver);
        }

        for (int i = 0; i < markers.Length; i++)
        {
            BuyableLandAreaMarker marker = markers[i];
            if (marker == null)
                continue;

            marker.exibirDemarcacao = true;
            marker.atualizarEmTempoReal = true;
            marker.enabled = true;
            marker.SendMessage("CriarOuAtualizarLinhas", SendMessageOptions.DontRequireReceiver);
        }

        if (triggers.Length == 0 && !warnedNoTrigger)
        {
            warnedNoTrigger = true;
            Debug.LogWarning(
                "[PurchaseSystemRuntimeRepair] Terrenos encontrados, mas nenhum BuySceneEntryTrigger ou collider nomeado como entrada/compra/calçada foi localizado. " +
                "Adicione BuySceneEntryTrigger ao collider da calçada e execute novamente."
            );
        }
    }

    private BuyScenePurchaseConfirmationPanel RepairConfirmationPanel()
    {
        BuyScenePurchaseConfirmationPanel panel = Object.FindAnyObjectByType<BuyScenePurchaseConfirmationPanel>(FindObjectsInactive.Include);
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

        Transform textTransform = FindChildByNames(root, "TextAsking", "TextoConfirmacao", "Texto_Confirmacao");
        if (textTransform != null)
            panel.textoPrincipal = textTransform.GetComponent<Text>();

        panel.botaoConfirmar = EnsureImageButton(root, "ButtonConfirm", "BotaoConfirmar", "Botao_Confirmar");
        panel.botaoFechar = EnsureImageButton(root, "ButtonClose", "BotaoFechar", "Botao_Fechar");
        panel.enabled = true;
        return panel;
    }

    private BuySceneUIImageButton EnsureImageButton(Transform root, params string[] names)
    {
        Transform target = FindChildByNames(root, names);
        if (target == null)
            return null;

        BuySceneUIImageButton button = target.GetComponent<BuySceneUIImageButton>();
        if (button == null)
            button = target.gameObject.AddComponent<BuySceneUIImageButton>();

        return button;
    }

    private BuySceneEntryTrigger[] TryCreateTriggerFromNamedCollider()
    {
        Collider[] colliders = Object.FindObjectsByType<Collider>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider collider = colliders[i];
            if (collider == null || collider.GetComponentInParent<Canvas>() != null)
                continue;

            string lower = collider.name.ToLowerInvariant();
            bool entryName = ContainsAny(lower, "entrada", "entry", "calcada", "calçada", "pisar");
            bool purchaseName = ContainsAny(lower, "compra", "buy", "terreno", "land");
            if (!entryName || !purchaseName)
                continue;

            BuySceneEntryTrigger trigger = collider.GetComponent<BuySceneEntryTrigger>();
            if (trigger == null)
                trigger = collider.gameObject.AddComponent<BuySceneEntryTrigger>();

            return new[] { trigger };
        }

        return Array.Empty<BuySceneEntryTrigger>();
    }

    private Transform FindTransformByNames(params string[] names)
    {
        Transform[] transforms = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
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

    private Transform FindChildByNames(Transform root, params string[] names)
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

    private bool ContainsAny(string value, params string[] terms)
    {
        for (int i = 0; i < terms.Length; i++)
        {
            if (value.Contains(terms[i]))
                return true;
        }

        return false;
    }
}
