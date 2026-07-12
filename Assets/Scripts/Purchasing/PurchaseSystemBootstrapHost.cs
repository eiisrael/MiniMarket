using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>
/// Repara automaticamente referências removidas pela organização antiga e recria os
/// LineRenderers da entrada/terrenos. Reconhece explicitamente objetos Buy_Area e cria
/// um collider filho somente para detecção, preservando o collider sólido da calçada.
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
        BuySceneEntryTrigger[] triggers = Object.FindObjectsByType<BuySceneEntryTrigger>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None
        );

        if (triggers.Length == 0)
            triggers = TryCreateTriggerFromNamedCollider();

        BuyableLandAreaMarker[] markers = Object.FindObjectsByType<BuyableLandAreaMarker>(
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

        for (int i = 0; i < triggers.Length; i++)
        {
            BuySceneEntryTrigger trigger = triggers[i];
            if (trigger == null)
                continue;

            trigger.controladorBuyScene = controller;
            trigger.jogadorRaizOpcional = movement != null ? movement.transform : trigger.jogadorRaizOpcional;
            trigger.mostrarMarcacaoVisual = true;
            trigger.mostrarXCentral = true;
            trigger.larguraLinha = Mathf.Max(trigger.larguraLinha, 0.14f);
            trigger.alturaAcimaDoCollider = Mathf.Max(trigger.alturaAcimaDoCollider, 0.12f);
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
                "[PurchaseSystemRuntimeRepair] Terrenos encontrados, mas nenhum BuySceneEntryTrigger ou objeto Buy_Area foi localizado. " +
                "Mantenha um collider no objeto da calçada/Buy_Area para que a entrada seja criada com segurança."
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
        Collider best = null;
        int bestScore = int.MinValue;

        for (int i = 0; i < colliders.Length; i++)
        {
            Collider collider = colliders[i];
            if (collider == null || collider.GetComponentInParent<Canvas>() != null)
                continue;

            string lower = collider.name.ToLowerInvariant();
            string compact = Compact(lower);
            bool exactBuyArea = compact.Contains("buyarea") || compact.Contains("areabuy");
            bool entryName = ContainsAny(lower, "entrada", "entry", "calcada", "calçada", "pisar", "trigger");
            bool purchaseName = ContainsAny(lower, "compra", "buy", "terreno", "land", "area", "área");

            if (!exactBuyArea && !(entryName && purchaseName))
                continue;

            int score = exactBuyArea ? 100 : 20;
            if (collider.gameObject.activeInHierarchy) score += 5;
            if (collider.enabled) score += 3;
            if (collider.bounds.size.x * collider.bounds.size.z > 0.5f) score += 2;

            if (score > bestScore)
            {
                best = collider;
                bestScore = score;
            }
        }

        if (best == null)
            return Array.Empty<BuySceneEntryTrigger>();

        BuySceneEntryTrigger direct = best.GetComponent<BuySceneEntryTrigger>();
        if (direct != null)
            return new[] { direct };

        Transform existing = best.transform.Find(RuntimeTriggerName);
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
            triggerObject.transform.SetParent(best.transform, false);
            triggerCollider = triggerObject.AddComponent<BoxCollider>();
        }

        ConfigureChildTriggerFromSource(best, triggerObject.transform, triggerCollider);

        BuySceneEntryTrigger trigger = triggerObject.GetComponent<BuySceneEntryTrigger>();
        if (trigger == null)
            trigger = triggerObject.AddComponent<BuySceneEntryTrigger>();

        trigger.mostrarMarcacaoVisual = true;
        trigger.mostrarXCentral = true;
        trigger.larguraLinha = Mathf.Max(trigger.larguraLinha, 0.14f);
        trigger.alturaAcimaDoCollider = Mathf.Max(trigger.alturaAcimaDoCollider, 0.12f);
        trigger.atualizarVisualEmTempoReal = true;
        trigger.enabled = true;

        return new[] { trigger };
    }

    private void ConfigureChildTriggerFromSource(Collider source, Transform triggerTransform, BoxCollider triggerCollider)
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

    private string Compact(string value)
    {
        return value.Replace("_", string.Empty)
                    .Replace("-", string.Empty)
                    .Replace(" ", string.Empty)
                    .Replace("á", "a")
                    .Replace("ç", "c");
    }
}
