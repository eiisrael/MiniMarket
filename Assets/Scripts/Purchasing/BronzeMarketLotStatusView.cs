using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Painel mundial persistente de uma loja Bronze_Market.
/// No Play aparece somente quando o controlador local desta própria loja está no modo de compra.
/// Exibe status, ID, preço e anima uma seta quando o mouse está sobre o terreno correto.
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
[DefaultExecutionOrder(1200)]
public sealed class BronzeMarketLotStatusView : MonoBehaviour
{
    [Header("Vínculos")]
    public BronzeMarketPurchaseLot lote;
    public BuyableLandAreaMarker terreno;
    public BuySceneCameraModeController controladorCamera;

    [Header("Hierarquia persistente")]
    public Canvas canvasMundial;
    public CanvasGroup canvasGroup;
    public Image fundoPainel;
    public Text textoStatus;
    public Text textoPreco;
    public RectTransform seta;
    public Graphic graficoSeta;

    [Header("Textos")]
    public string statusDisponivel = "DISPONÍVEL";
    public string statusIndisponivel = "INDISPONÍVEL";
    public string prefixoPreco = "Gold: ";

    [Header("Identificação visual do lote")]
    public bool mostrarIdDoLote = true;
    public string prefixoId = "ID: ";
    [Range(4, 24)] public int caracteresIdVisiveis = 8;
    public bool mostrarIdCompleto;

    [Header("Cores")]
    public Color corDisponivel = new Color(0.12f, 0.95f, 0.22f, 1f);
    public Color corHover = new Color(0.1f, 0.9f, 1f, 1f);
    public Color corIndisponivel = new Color(1f, 0.18f, 0.08f, 1f);
    public Color corFundo = new Color(0.035f, 0.045f, 0.055f, 0.9f);

    [Header("Seta ao passar o mouse")]
    public bool mostrarSetaSomenteNoHover = true;
    [Min(0f)] public float distanciaHoverExtra = 0.15f;
    [Min(0f)] public float amplitudeSeta = 10f;
    [Min(0.1f)] public float velocidadeSeta = 5f;
    [Min(0f)] public float aumentoSetaHover = 0.12f;

    [Header("Painel")]
    public bool olharParaCamera = true;
    public bool mostrarPreviewForaDoPlay = true;
    public bool ocultarQuandoForaDoModoCompra = true;

    private Vector2 posicaoBaseSeta;
    private Vector3 escalaBaseSeta = Vector3.one;
    private bool basesCapturadas;
    private bool hoverAtual;

    private void Awake()
    {
        ResolverReferencias();
        CapturarBases();
        AtualizarVisualImediato();
    }

    private void OnEnable()
    {
        ResolverReferencias();
        CapturarBases();
        AtualizarVisualImediato();
    }

    private void Update()
    {
        ResolverReferencias();

        if (!Application.isPlaying)
        {
            AplicarVisibilidade(mostrarPreviewForaDoPlay);
            AtualizarTextosECores(false);
            RestaurarSeta();
            return;
        }

        bool pertenceAoModoAtual =
            controladorCamera != null && controladorCamera.ModoCompraAtivo;

        bool visivel = !ocultarQuandoForaDoModoCompra || pertenceAoModoAtual;
        AplicarVisibilidade(visivel);

        if (!visivel || !pertenceAoModoAtual)
        {
            hoverAtual = false;
            RestaurarSeta();
            return;
        }

        if (terreno != null)
            terreno.SincronizarEstadoComBanco();

        hoverAtual = TerrenoEstaSobMouse();
        AtualizarTextosECores(hoverAtual);
        AnimarSeta(hoverAtual);
        AtualizarBillboard();
    }

    private void OnValidate()
    {
        caracteresIdVisiveis = Mathf.Clamp(caracteresIdVisiveis, 4, 24);
        distanciaHoverExtra = Mathf.Max(0f, distanciaHoverExtra);
        amplitudeSeta = Mathf.Max(0f, amplitudeSeta);
        velocidadeSeta = Mathf.Max(0.1f, velocidadeSeta);
        aumentoSetaHover = Mathf.Max(0f, aumentoSetaHover);

        ResolverReferencias();
        CapturarBases();
        AtualizarVisualImediato();
    }

    [ContextMenu("Bronze Market/Atualizar preview do status")]
    public void AtualizarVisualImediato()
    {
        ResolverReferencias();
        AplicarVisibilidade(!Application.isPlaying ? mostrarPreviewForaDoPlay : true);
        AtualizarTextosECores(false);
        RestaurarSeta();
    }

    private void ResolverReferencias()
    {
        if (lote == null)
            lote = GetComponentInParent<BronzeMarketPurchaseLot>();

        if (terreno == null && lote != null)
            terreno = lote.terrenoPrincipal;

        if (controladorCamera == null && lote != null)
            controladorCamera = lote.controladorCamera;

        if (canvasMundial == null)
            canvasMundial = GetComponent<Canvas>();

        if (canvasGroup == null)
            canvasGroup = GetComponent<CanvasGroup>();

        if (graficoSeta == null && seta != null)
            graficoSeta = seta.GetComponent<Graphic>();
    }

    private void CapturarBases()
    {
        if (seta == null || basesCapturadas)
            return;

        posicaoBaseSeta = seta.anchoredPosition;
        escalaBaseSeta = seta.localScale;
        basesCapturadas = true;
    }

    private void AplicarVisibilidade(bool visivel)
    {
        if (canvasGroup != null)
        {
            canvasGroup.alpha = visivel ? 1f : 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }
        else if (canvasMundial != null)
        {
            canvasMundial.enabled = visivel;
        }
    }

    private void AtualizarTextosECores(bool hover)
    {
        bool disponivel = terreno == null || terreno.EstaDisponivelParaCompra;
        Color corEstado = disponivel
            ? (hover ? corHover : corDisponivel)
            : corIndisponivel;

        if (textoStatus != null)
        {
            string status = disponivel ? statusDisponivel : statusIndisponivel;
            string idVisivel = ObterIdVisivel();

            textoStatus.text = mostrarIdDoLote && !string.IsNullOrWhiteSpace(idVisivel)
                ? status + "\n" + prefixoId + idVisivel
                : status;
            textoStatus.color = corEstado;
        }

        if (textoPreco != null)
        {
            int preco = terreno != null
                ? terreno.precoGold
                : (lote != null ? lote.precoGold : 0);
            textoPreco.text = prefixoPreco + preco.ToString("N0");
            textoPreco.color = Color.white;
        }

        if (fundoPainel != null)
            fundoPainel.color = hover ? Color.Lerp(corFundo, corEstado, 0.22f) : corFundo;

        if (graficoSeta != null)
            graficoSeta.color = corEstado;
    }

    private string ObterIdVisivel()
    {
        string id = lote != null ? lote.IdLoteNormalizado : string.Empty;
        if (string.IsNullOrWhiteSpace(id) && terreno != null)
            id = terreno.IdPersistente;

        if (string.IsNullOrWhiteSpace(id))
            return "----";

        if (mostrarIdCompleto || id.Length <= caracteresIdVisiveis)
            return id;

        return id.Substring(id.Length - caracteresIdVisiveis, caracteresIdVisiveis);
    }

    private bool TerrenoEstaSobMouse()
    {
        if (terreno == null || controladorCamera == null ||
            controladorCamera.cameraPrincipal == null)
        {
            return false;
        }

        Camera camera = controladorCamera.cameraPrincipal;
        Ray ray = camera.ScreenPointToRay(Input.mousePosition);
        Plane plano = new Plane(
            Vector3.up,
            new Vector3(0f, terreno.ObterAlturaDoPlanoDeSelecao(), 0f)
        );

        if (!plano.Raycast(ray, out float distancia) || distancia < 0f)
            return false;

        return terreno.ContemPontoMundo(
            ray.GetPoint(distancia),
            distanciaHoverExtra
        );
    }

    private void AnimarSeta(bool hover)
    {
        if (seta == null)
            return;

        CapturarBases();

        bool mostrar = !mostrarSetaSomenteNoHover || hover;
        seta.gameObject.SetActive(mostrar);
        if (!mostrar)
            return;

        float onda = (Mathf.Sin(Time.unscaledTime * velocidadeSeta) + 1f) * 0.5f;
        seta.anchoredPosition = posicaoBaseSeta + Vector2.up * (onda * amplitudeSeta);

        float escalaExtra = hover ? aumentoSetaHover * onda : 0f;
        seta.localScale = escalaBaseSeta * (1f + escalaExtra);
    }

    private void RestaurarSeta()
    {
        if (seta == null)
            return;

        CapturarBases();
        seta.anchoredPosition = posicaoBaseSeta;
        seta.localScale = escalaBaseSeta;
        seta.gameObject.SetActive(
            !mostrarSetaSomenteNoHover && mostrarPreviewForaDoPlay
        );
    }

    private void AtualizarBillboard()
    {
        if (!olharParaCamera || canvasMundial == null)
            return;

        Camera camera = controladorCamera != null
            ? controladorCamera.cameraPrincipal
            : Camera.main;

        if (camera == null)
            return;

        Vector3 direcao = canvasMundial.transform.position - camera.transform.position;
        if (direcao.sqrMagnitude > 0.0001f)
        {
            canvasMundial.transform.rotation = Quaternion.LookRotation(
                direcao.normalized,
                Vector3.up
            );
        }
    }
}
