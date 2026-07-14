using System;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// Autoridade de compra de uma loja Bronze_Market.
/// Cada cópia da raiz da loja possui seu próprio ID, Buy_Area, terreno e controladores.
/// As referências são sempre resolvidas dentro da própria hierarquia antes de qualquer fallback.
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
[DefaultExecutionOrder(-25000)]
public sealed class BronzeMarketPurchaseLot : MonoBehaviour
{
    [Header("Identificação única")]
    [Tooltip("ID persistente desta loja. Cópias recebem um novo ID pela ferramenta do Editor.")]
    public string idLote;

    public string nomeDaLoja = "Loja Bronze";
    [Min(0)] public int precoGold = 20000;

    [Header("Objetos desta loja")]
    public Transform buyArea;
    public Collider colliderSolidoDaCalcada;
    public BuySceneEntryTrigger triggerEntrada;
    public BuyableLandAreaMarker terrenoPrincipal;
    public Transform pontoFocoCamera;
    public BronzeMarketLotStatusView visualStatus;

    [Header("Sistema local de compra")]
    public BuySceneCameraModeController controladorCamera;
    public PurchaseModeBridge ponteCamera;
    public BuySceneLandPurchaseController controladorCompra;
    public BuyScenePurchaseConfirmationPanel painelConfirmacao;

    [Header("Jogador compartilhado")]
    public PlayerCameraController cameraDoJogador;
    public CameraRelativeMovement movimentoDoJogador;

    [Header("Automação")]
    public bool aplicarVinculosAoIniciar = true;
    public bool manterEscopoRestritoAHierarquia = true;
    public bool registrarLogs;

    public string IdLoteNormalizado => NormalizarId(idLote);

    private void Awake()
    {
        if (Application.isPlaying && aplicarVinculosAoIniciar)
            AplicarVinculosRuntime();
    }

    private void OnEnable()
    {
        if (Application.isPlaying && aplicarVinculosAoIniciar)
            AplicarVinculosRuntime();
    }

    private void OnValidate()
    {
        precoGold = Mathf.Max(0, precoGold);

        // Não cria objetos no OnValidate. Apenas mantém referências já existentes coerentes.
        ResolverReferenciasNaPropriaHierarquia();
        AplicarConfiguracaoSemCriarObjetos();
    }

    [ContextMenu("Bronze Market/Aplicar vínculos desta loja")]
    public void AplicarVinculosRuntime()
    {
        ResolverReferenciasNaPropriaHierarquia();

        if (cameraDoJogador == null)
        {
            cameraDoJogador = Object.FindAnyObjectByType<PlayerCameraController>(
                FindObjectsInactive.Include
            );
        }

        if (movimentoDoJogador == null)
        {
            movimentoDoJogador = Object.FindAnyObjectByType<CameraRelativeMovement>(
                FindObjectsInactive.Include
            );
        }

        if (painelConfirmacao == null)
        {
            painelConfirmacao = GetComponentInChildren<BuyScenePurchaseConfirmationPanel>(true);
            if (painelConfirmacao == null)
            {
                painelConfirmacao = Object.FindAnyObjectByType<BuyScenePurchaseConfirmationPanel>(
                    FindObjectsInactive.Include
                );
            }
        }

        AplicarConfiguracaoSemCriarObjetos();

        if (registrarLogs)
        {
            Debug.Log(
                "[BronzeMarketPurchaseLot] Vínculos aplicados. Loja=" + name +
                " | ID=" + IdLoteNormalizado +
                " | Terreno=" + (terrenoPrincipal != null ? terrenoPrincipal.name : "ausente"),
                this
            );
        }
    }

    public bool ContemTerreno(BuyableLandAreaMarker terreno)
    {
        return terreno != null && terrenoPrincipal == terreno;
    }

    public BuyableLandAreaMarker[] ObterTerrenosDestaLoja()
    {
        return terrenoPrincipal != null
            ? new[] { terrenoPrincipal }
            : Array.Empty<BuyableLandAreaMarker>();
    }

    private void ResolverReferenciasNaPropriaHierarquia()
    {
        if (buyArea == null)
            buyArea = EncontrarDescendentePorNome(transform, "Buy_Area", "BuyArea", "Area_Compra", "AreaCompra");

        if (colliderSolidoDaCalcada == null && buyArea != null)
        {
            Collider[] colliders = buyArea.GetComponents<Collider>();
            for (int i = 0; i < colliders.Length; i++)
            {
                Collider candidate = colliders[i];
                if (candidate != null && !candidate.isTrigger)
                {
                    colliderSolidoDaCalcada = candidate;
                    break;
                }
            }

            if (colliderSolidoDaCalcada == null)
                colliderSolidoDaCalcada = buyArea.GetComponent<Collider>();
        }

        if (triggerEntrada == null)
            triggerEntrada = GetComponentInChildren<BuySceneEntryTrigger>(true);

        if (terrenoPrincipal == null)
        {
            BuyableLandAreaMarker[] terrenos = GetComponentsInChildren<BuyableLandAreaMarker>(true);
            for (int i = 0; i < terrenos.Length; i++)
            {
                if (terrenos[i] != null)
                {
                    terrenoPrincipal = terrenos[i];
                    break;
                }
            }
        }

        if (controladorCamera == null)
            controladorCamera = GetComponentInChildren<BuySceneCameraModeController>(true);

        if (controladorCompra == null)
            controladorCompra = GetComponentInChildren<BuySceneLandPurchaseController>(true);

        if (ponteCamera == null && controladorCamera != null)
            ponteCamera = controladorCamera.GetComponent<PurchaseModeBridge>();

        if (painelConfirmacao == null)
            painelConfirmacao = GetComponentInChildren<BuyScenePurchaseConfirmationPanel>(true);

        if (visualStatus == null)
            visualStatus = GetComponentInChildren<BronzeMarketLotStatusView>(true);

        if (pontoFocoCamera == null)
        {
            Transform focus = EncontrarDescendentePorNome(
                transform,
                "PurchaseCameraFocus",
                "BuyCameraFocus",
                "PontoFocoCompra"
            );
            pontoFocoCamera = focus != null ? focus : (terrenoPrincipal != null ? terrenoPrincipal.transform : transform);
        }
    }

    private void AplicarConfiguracaoSemCriarObjetos()
    {
        string id = IdLoteNormalizado;

        if (terrenoPrincipal != null)
        {
            if (!string.IsNullOrWhiteSpace(id))
                terrenoPrincipal.idPersistente = id;

            terrenoPrincipal.nomeDoTerreno = string.IsNullOrWhiteSpace(nomeDaLoja)
                ? gameObject.name
                : nomeDaLoja;
            terrenoPrincipal.precoGold = precoGold;
        }

        if (triggerEntrada != null)
        {
            triggerEntrada.controladorBuyScene = controladorCamera;
            triggerEntrada.pontoDeFocoDaCamera = pontoFocoCamera != null
                ? pontoFocoCamera
                : (terrenoPrincipal != null ? terrenoPrincipal.transform : transform);
            triggerEntrada.terrenosDestaArea = ObterTerrenosDestaLoja();
            triggerEntrada.usarTerrenosProximosSeListaVazia = false;
            triggerEntrada.sincronizarComTerrenosEncontradosAutomaticamente = false;
            triggerEntrada.mostrarMarcacaoVisual = true;
            triggerEntrada.mostrarXCentral = true;
        }

        if (controladorCamera != null)
        {
            if (controladorCamera.cameraPrincipal == null && cameraDoJogador != null)
                controladorCamera.cameraPrincipal = cameraDoJogador.gameCamera;

            if (controladorCamera.jogadorRaiz == null && movimentoDoJogador != null)
                controladorCamera.jogadorRaiz = movimentoDoJogador.transform;
        }

        if (ponteCamera != null)
        {
            ponteCamera.purchaseController = controladorCamera;
            ponteCamera.playerCamera = cameraDoJogador;
            ponteCamera.movement = movimentoDoJogador;
        }

        if (controladorCompra != null)
        {
            controladorCompra.controladorBuyScene = controladorCamera;
            controladorCompra.cameraCompra = controladorCamera != null
                ? controladorCamera.cameraPrincipal
                : (cameraDoJogador != null ? cameraDoJogador.gameCamera : null);
            controladorCompra.painelConfirmacao = painelConfirmacao;
            controladorCompra.terrenos = ObterTerrenosDestaLoja();
            controladorCompra.procurarTerrenosAutomaticamente = false;
            controladorCompra.enabled = true;
        }

        if (visualStatus != null)
        {
            visualStatus.lote = this;
            visualStatus.terreno = terrenoPrincipal;
            visualStatus.controladorCamera = controladorCamera;
        }
    }

    private static Transform EncontrarDescendentePorNome(Transform root, params string[] names)
    {
        if (root == null)
            return null;

        Transform[] all = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < all.Length; i++)
        {
            Transform current = all[i];
            if (current == null)
                continue;

            string compact = Compactar(current.name);
            for (int n = 0; n < names.Length; n++)
            {
                if (compact == Compactar(names[n]))
                    return current;
            }
        }

        return null;
    }

    public static string NormalizarId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        return value.Trim()
            .ToUpperInvariant()
            .Replace(' ', '_')
            .Replace('-', '_');
    }

    private static string Compactar(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        return value.Trim()
            .ToLowerInvariant()
            .Replace("_", string.Empty)
            .Replace("-", string.Empty)
            .Replace(" ", string.Empty)
            .Replace("á", "a")
            .Replace("ã", "a")
            .Replace("ç", "c");
    }
}
