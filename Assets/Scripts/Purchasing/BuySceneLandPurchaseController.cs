using UnityEngine;
using UnityEngine.EventSystems;
using Object = UnityEngine.Object;

/// <summary>
/// Controla seleção e compra dos terrenos durante o modo de compra.
/// Preserva a API pública do sistema antigo e usa o banco atual para gold e empresas.
/// </summary>
[DisallowMultipleComponent]
public sealed class BuySceneLandPurchaseController : MonoBehaviour
{
    [Header("Referências")]
    public BuySceneCameraModeController controladorBuyScene;
    public BuyScenePurchaseConfirmationPanel painelConfirmacao;
    public Camera cameraCompra;
    public PlayerGold playerGold;

    [Header("Terrenos")]
    public BuyableLandAreaMarker[] terrenos;
    public bool procurarTerrenosAutomaticamente = true;
    [Min(0f)] public float margemSelecao = 0.15f;

    [Header("Compra")]
    public bool marcarTerrenoComoIndisponivelAposComprar = true;
    public bool permitirComprarTerrenoIndisponivel;
    public bool fecharPainelAposCompraConfirmada = true;
    public bool bloquearCliqueQuandoMouseEstaSobreUI = true;

    [Header("Input")]
    public KeyCode botaoMouseCompra = KeyCode.Mouse0;

    [Header("Mensagens")]
    [TextArea(2, 3)] public string mensagemGoldInsuficiente = "Gold insuficiente para comprar este terreno.";
    [TextArea(2, 3)] public string mensagemTerrenoIndisponivel = "Este terreno não está disponível para compra.";

    [Header("Perfil / Empresas")]
    public bool registrarEmpresaNoPerfilAoComprar = true;

    [Header("Debug")]
    public bool logarEventos = true;

    private BuyableLandAreaMarker terrenoHover;
    private BuyableLandAreaMarker terrenoSelecionado;

    private void Awake()
    {
        ResolverReferencias(true);
    }

    private void Start()
    {
        ResolverReferencias(true);
        AtualizarListaTerrenosSeNecessario();
        AplicarEstadoSalvoDosTerrenos();
    }

    private void Update()
    {
        ResolverReferencias(false);

        if (controladorBuyScene == null || !controladorBuyScene.ModoCompraAtivo)
        {
            LimparHoverAtual();
            return;
        }

        if (painelConfirmacao != null && painelConfirmacao.PainelAberto)
        {
            LimparHoverAtual();
            return;
        }

        AtualizarHoverDoMouse();

        if (botaoMouseCompra != KeyCode.None && Input.GetKeyDown(botaoMouseCompra))
            TentarSelecionarTerrenoHover();
    }

    public void AtualizarListaTerrenosSeNecessario()
    {
        if (!procurarTerrenosAutomaticamente)
            return;

        if (terrenos != null && terrenos.Length > 0)
            return;

        terrenos = Object.FindObjectsByType<BuyableLandAreaMarker>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
    }

    public void AplicarEstadoSalvoDosTerrenos()
    {
        if (terrenos == null || terrenos.Length == 0)
            AtualizarListaTerrenosSeNecessario();

        if (terrenos == null)
            return;

        for (int i = 0; i < terrenos.Length; i++)
        {
            if (terrenos[i] != null)
                terrenos[i].SincronizarEstadoComBanco();
        }
    }

    private void ResolverReferencias(bool force)
    {
        if (force || controladorBuyScene == null)
            controladorBuyScene = Object.FindAnyObjectByType<BuySceneCameraModeController>(FindObjectsInactive.Include);

        if (force || painelConfirmacao == null)
            painelConfirmacao = Object.FindAnyObjectByType<BuyScenePurchaseConfirmationPanel>(FindObjectsInactive.Include);

        if (force || cameraCompra == null)
        {
            if (controladorBuyScene != null && controladorBuyScene.cameraPrincipal != null)
                cameraCompra = controladorBuyScene.cameraPrincipal;
            else
                cameraCompra = Camera.main;
        }

        if (force || playerGold == null)
            playerGold = PlayerGold.Instance != null
                ? PlayerGold.Instance
                : Object.FindAnyObjectByType<PlayerGold>(FindObjectsInactive.Include);

        if (terrenos == null || terrenos.Length == 0)
            AtualizarListaTerrenosSeNecessario();
    }

    private void AtualizarHoverDoMouse()
    {
        BuyableLandAreaMarker novoHover = EncontrarTerrenoSobMouse();
        if (terrenoHover == novoHover)
            return;

        if (terrenoHover != null && terrenoHover != terrenoSelecionado)
            terrenoHover.DefinirHover(false);

        terrenoHover = novoHover;

        if (terrenoHover != null && terrenoHover != terrenoSelecionado)
            terrenoHover.DefinirHover(true);
    }

    private BuyableLandAreaMarker EncontrarTerrenoSobMouse()
    {
        if (cameraCompra == null)
            return null;

        if (bloquearCliqueQuandoMouseEstaSobreUI &&
            EventSystem.current != null &&
            EventSystem.current.IsPointerOverGameObject())
        {
            return null;
        }

        if (terrenos == null || terrenos.Length == 0)
            return null;

        Ray ray = cameraCompra.ScreenPointToRay(Input.mousePosition);
        BuyableLandAreaMarker melhorTerreno = null;
        float melhorDistancia = float.MaxValue;

        for (int i = 0; i < terrenos.Length; i++)
        {
            BuyableLandAreaMarker terreno = terrenos[i];
            if (terreno == null)
                continue;

            terreno.SincronizarEstadoComBanco();

            if (!permitirComprarTerrenoIndisponivel && !terreno.EstaDisponivelParaCompra)
                continue;

            Plane plano = new Plane(
                Vector3.up,
                new Vector3(0f, terreno.ObterAlturaDoPlanoDeSelecao(), 0f));

            if (!plano.Raycast(ray, out float distancia) || distancia < 0f)
                continue;

            Vector3 ponto = ray.GetPoint(distancia);
            if (!terreno.ContemPontoMundo(ponto, margemSelecao))
                continue;

            if (distancia < melhorDistancia)
            {
                melhorDistancia = distancia;
                melhorTerreno = terreno;
            }
        }

        return melhorTerreno;
    }

    private void TentarSelecionarTerrenoHover()
    {
        if (terrenoHover == null)
            return;

        terrenoHover.SincronizarEstadoComBanco();

        if (!permitirComprarTerrenoIndisponivel && !terrenoHover.EstaDisponivelParaCompra)
            return;

        if (painelConfirmacao == null)
        {
            Debug.LogWarning("[BuySceneLandPurchaseController] Painel de confirmação não encontrado.", this);
            return;
        }

        terrenoSelecionado = terrenoHover;
        terrenoSelecionado.DefinirHover(false);
        terrenoSelecionado.DefinirSelecionado(true);

        painelConfirmacao.Mostrar(
            terrenoSelecionado,
            ConfirmarCompraDoTerreno,
            FecharPainelSemComprar);

        if (logarEventos)
            Debug.Log("[BuySceneLandPurchaseController] Terreno selecionado: " + terrenoSelecionado.nomeDoTerreno, this);
    }

    private void ConfirmarCompraDoTerreno(BuyableLandAreaMarker terreno)
    {
        if (terreno == null)
            return;

        terreno.SincronizarEstadoComBanco();

        if (!permitirComprarTerrenoIndisponivel && !terreno.EstaDisponivelParaCompra)
        {
            if (painelConfirmacao != null)
                painelConfirmacao.MostrarMensagemErro(mensagemTerrenoIndisponivel);
            return;
        }

        if (playerGold == null)
            ResolverReferencias(false);

        if (playerGold == null)
        {
            Debug.LogWarning("[BuySceneLandPurchaseController] PlayerGold não encontrado; compra cancelada.", this);
            return;
        }

        if (!playerGold.TemGoldSuficiente(terreno.precoGold) || !playerGold.RemoverGold(terreno.precoGold))
        {
            if (painelConfirmacao != null)
                painelConfirmacao.MostrarMensagemErro(mensagemGoldInsuficiente);
            return;
        }

        terreno.DefinirSelecionado(false);
        terreno.DefinirHover(false);

        if (marcarTerrenoComoIndisponivelAposComprar)
            terreno.MarcarComoComprado();

        RegistrarEmpresaComprada(terreno);

        if (fecharPainelAposCompraConfirmada && painelConfirmacao != null)
        {
            painelConfirmacao.OcultarSemCallback();
            painelConfirmacao.LimparCallbacks();
        }

        if (terrenoHover == terreno)
            terrenoHover = null;

        terrenoSelecionado = null;

        if (logarEventos)
        {
            Debug.Log(
                "[BuySceneLandPurchaseController] Compra confirmada. Gold debitado: " +
                terreno.precoGold + " | Área: " + terreno.IdPersistente,
                this);
        }
    }

    private void RegistrarEmpresaComprada(BuyableLandAreaMarker terreno)
    {
        if (!registrarEmpresaNoPerfilAoComprar || terreno == null)
            return;

        MiniMarketPlayerDatabase database = MiniMarketPlayerDatabase.ObterOuCriar();
        if (database != null)
            database.RegistrarEmpresaComprada(terreno.IdPersistente);
    }

    private void FecharPainelSemComprar()
    {
        if (terrenoSelecionado != null)
        {
            terrenoSelecionado.DefinirSelecionado(false);

            if (terrenoSelecionado == terrenoHover)
                terrenoSelecionado.DefinirHover(true);
        }

        terrenoSelecionado = null;

        if (logarEventos)
            Debug.Log("[BuySceneLandPurchaseController] Painel fechado sem comprar.", this);
    }

    private void LimparHoverAtual()
    {
        if (terrenoHover != null && terrenoHover != terrenoSelecionado)
            terrenoHover.DefinirHover(false);

        terrenoHover = null;
    }

    private void OnValidate()
    {
        margemSelecao = Mathf.Max(0f, margemSelecao);
    }
}
