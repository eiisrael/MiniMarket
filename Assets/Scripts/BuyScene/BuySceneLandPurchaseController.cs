using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Controla selecao e compra dos terrenos durante a BuyScene.
/// - Mouse em cima do terreno muda a cor do marcador.
/// - Clique esquerdo abre painel de confirmacao.
/// - Confirmar remove gold global do PlayerGold e marca o terreno como comprado/indisponivel.
/// </summary>
public class BuySceneLandPurchaseController : MonoBehaviour
{
    [Header("Referencias")]
    public BuySceneCameraModeController controladorBuyScene;
    public BuyScenePurchaseConfirmationPanel painelConfirmacao;
    public Camera cameraCompra;
    public PlayerGold playerGold;

    [Header("Terrenos")]
    public BuyableLandAreaMarker[] terrenos;
    public bool procurarTerrenosAutomaticamente = true;

    [Tooltip("Margem extra para facilitar passar o mouse em cima da area.")]
    [Min(0f)]
    public float margemSelecao = 0.15f;

    [Header("Compra")]
    public bool marcarTerrenoComoIndisponivelAposComprar = true;
    public bool permitirComprarTerrenoIndisponivel = false;
    public bool fecharPainelAposCompraConfirmada = true;

    [Tooltip("Se ligado, clicar em UI nao seleciona terreno atras do painel.")]
    public bool bloquearCliqueQuandoMouseEstaSobreUI = true;

    [Header("Input")]
    public KeyCode botaoMouseCompra = KeyCode.Mouse0;

    [Header("Mensagens")]
    [TextArea(2, 3)]
    public string mensagemGoldInsuficiente = "Gold insuficiente para comprar este terreno.";

    [TextArea(2, 3)]
    public string mensagemTerrenoIndisponivel = "Este terreno nao esta disponivel para compra.";

    [Header("Debug")]
    public bool logarEventos = true;

    private BuyableLandAreaMarker terrenoHover;
    private BuyableLandAreaMarker terrenoSelecionado;

    private void Awake()
    {
        ResolverReferencias();
    }

    private void Start()
    {
        ResolverReferencias();
        AtualizarListaTerrenosSeNecessario();
    }

    private void Update()
    {
        ResolverReferenciasLeves();

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

        if (Input.GetKeyDown(botaoMouseCompra))
            TentarSelecionarTerrenoHover();
    }

    public void AtualizarListaTerrenosSeNecessario()
    {
        if (!procurarTerrenosAutomaticamente)
            return;

        if (terrenos != null && terrenos.Length > 0)
            return;

        terrenos = FindObjectsOfType<BuyableLandAreaMarker>();
    }

    private void ResolverReferencias()
    {
        if (controladorBuyScene == null)
            controladorBuyScene = FindObjectOfType<BuySceneCameraModeController>();

        if (painelConfirmacao == null)
            painelConfirmacao = FindObjectOfType<BuyScenePurchaseConfirmationPanel>();

        if (cameraCompra == null)
            cameraCompra = Camera.main;

        if (playerGold == null)
            playerGold = PlayerGold.Instance != null ? PlayerGold.Instance : FindObjectOfType<PlayerGold>();
    }

    private void ResolverReferenciasLeves()
    {
        if (cameraCompra == null)
            cameraCompra = Camera.main;

        if (playerGold == null)
            playerGold = PlayerGold.Instance;

        if (painelConfirmacao == null)
            painelConfirmacao = FindObjectOfType<BuyScenePurchaseConfirmationPanel>();

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

        if (bloquearCliqueQuandoMouseEstaSobreUI && EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            return null;

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

            if (!permitirComprarTerrenoIndisponivel && !terreno.EstaDisponivelParaCompra)
                continue;

            Plane plano = new Plane(Vector3.up, new Vector3(0f, terreno.ObterAlturaDoPlanoDeSelecao(), 0f));

            if (!plano.Raycast(ray, out float distancia))
                continue;

            if (distancia < 0f)
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

        if (painelConfirmacao == null)
        {
            Debug.LogWarning("[BuySceneLandPurchaseController] Nenhum painel de confirmacao foi encontrado na cena.");
            return;
        }

        terrenoSelecionado = terrenoHover;
        terrenoSelecionado.DefinirHover(false);
        terrenoSelecionado.DefinirSelecionado(true);

        painelConfirmacao.Mostrar(
            terrenoSelecionado,
            ConfirmarCompraDoTerreno,
            FecharPainelSemComprar
        );

        if (logarEventos)
            Debug.Log("[BuySceneLandPurchaseController] Terreno selecionado: " + terrenoSelecionado.nomeDoTerreno);
    }

    private void ConfirmarCompraDoTerreno(BuyableLandAreaMarker terreno)
    {
        if (terreno == null)
            return;

        if (!permitirComprarTerrenoIndisponivel && !terreno.EstaDisponivelParaCompra)
        {
            painelConfirmacao.MostrarMensagemErro(mensagemTerrenoIndisponivel);
            return;
        }

        if (playerGold == null)
            playerGold = PlayerGold.Instance != null ? PlayerGold.Instance : FindObjectOfType<PlayerGold>();

        if (playerGold == null)
        {
            Debug.LogWarning("[BuySceneLandPurchaseController] PlayerGold nao encontrado. Nao foi possivel debitar gold.");
            return;
        }

        if (!playerGold.TemGoldSuficiente(terreno.precoGold))
        {
            painelConfirmacao.MostrarMensagemErro(mensagemGoldInsuficiente);
            return;
        }

        bool removeuGold = playerGold.RemoverGold(terreno.precoGold);

        if (!removeuGold)
        {
            painelConfirmacao.MostrarMensagemErro(mensagemGoldInsuficiente);
            return;
        }

        terreno.DefinirSelecionado(false);
        terreno.DefinirHover(false);

        if (marcarTerrenoComoIndisponivelAposComprar)
            terreno.MarcarComoComprado();

        if (fecharPainelAposCompraConfirmada && painelConfirmacao != null)
        {
            painelConfirmacao.OcultarSemCallback();
            painelConfirmacao.LimparCallbacks();
        }

        if (terrenoHover == terreno)
            terrenoHover = null;

        terrenoSelecionado = null;

        if (logarEventos)
            Debug.Log("[BuySceneLandPurchaseController] Compra confirmada. Gold debitado: " + terreno.precoGold);
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
            Debug.Log("[BuySceneLandPurchaseController] Painel fechado sem comprar.");
    }

    private void LimparHoverAtual()
    {
        if (terrenoHover != null && terrenoHover != terrenoSelecionado)
            terrenoHover.DefinirHover(false);

        terrenoHover = null;
    }
}
