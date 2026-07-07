using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Painel de confirmacao de compra da BuyScene.
/// Funciona de duas formas:
/// 1. Modo recomendado: usando uma hierarquia manual dentro de um Canvas, igual HUD.
/// 2. Modo automatico: o script cria a janela em runtime se os RectTransforms nao forem preenchidos.
///
/// Para editar sem o painel voltar de posicao, use Fonte Do Layout = RectTransform Manual.
/// Nesse modo, voce move pelo proprio Canvas/RectTransform e o script apenas sincroniza os campos.
/// </summary>
public class BuyScenePurchaseConfirmationPanel : MonoBehaviour
{
    public enum FonteDoLayout
    {
        CamposDoScript,
        RectTransformManual
    }

    [Header("Canvas")]
    [Tooltip("Canvas onde o painel esta ou sera criado. Recomendado usar o Canvas do HUD ou um Canvas_BuyScene_UI separado.")]
    public Canvas canvasAlvo;

    [Tooltip("Se nenhum painel manual for arrastado, cria a hierarquia da janela automaticamente ao iniciar.")]
    public bool criarInterfaceAutomaticamente = true;

    [Header("Hierarquia Manual Opcional - Recomendado")]
    [Tooltip("RectTransform do painel principal. Se preencher, o script usa a UI manual do Canvas.")]
    public RectTransform painelRect;

    public RawImage imagemPainel;

    [Tooltip("RectTransform do texto principal da janela.")]
    public RectTransform textoPrincipalRect;

    public Text textoPrincipal;

    [Tooltip("RectTransform do botao Confirmar.")]
    public RectTransform botaoConfirmarRect;

    public RawImage imagemBotaoConfirmar;
    public BuySceneUIImageButton botaoConfirmar;

    [Tooltip("RectTransform do texto dentro do botao Confirmar.")]
    public RectTransform textoBotaoConfirmarRect;

    public Text textoConfirmar;

    [Tooltip("RectTransform do botao Fechar/X.")]
    public RectTransform botaoFecharRect;

    public RawImage imagemBotaoFechar;
    public BuySceneUIImageButton botaoFechar;

    [Header("Texturas")]
    public Texture2D painelWarning;
    public Texture2D buttonOff;
    public Texture2D buttonOn;
    public Texture2D closeOff;
    public Texture2D closeOn;

    [Header("Texto do Painel")]
    [TextArea(2, 4)]
    public string textoConfirmacao = "Você tem certeza que deseja comprar?";

    public bool exibirNomeDoTerreno = true;
    public bool exibirPrecoDoTerreno = true;
    public string textoBotaoConfirmar = "Confirmar";

    [TextArea(2, 3)]
    public string textoGoldInsuficiente = "Gold insuficiente para comprar este terreno.";

    [Header("Layout em Tempo Real")]
    [Tooltip("CamposDoScript: os campos abaixo controlam a UI. RectTransformManual: voce move no Canvas e o script nao puxa de volta.")]
    public FonteDoLayout fonteDoLayout = FonteDoLayout.RectTransformManual;

    [Tooltip("Se ligado, o layout e atualizado durante o Play.")]
    public bool atualizarLayoutEmTempoReal = true;

    [Tooltip("No modo RectTransformManual, copia X/Y/tamanhos reais dos RectTransforms para estes campos.")]
    public bool sincronizarCamposComRectTransform = true;

    [Tooltip("Normalmente deixe desligado para evitar truncar valores quebrados.")]
    public bool arredondarValoresSincronizados = false;

    [Range(0, 4)]
    public int casasDecimaisSincronizacao = 2;

    [Tooltip("Posicao X/Y do painel inteiro na tela. X direita/esquerda, Y cima/baixo.")]
    public Vector2 posicaoPainel = Vector2.zero;

    public Vector2 tamanhoPainel = new Vector2(640f, 360f);

    [Tooltip("Posicao X/Y da area do texto principal dentro do painel.")]
    public Vector2 posicaoTextoPrincipal = new Vector2(0f, 35f);

    [Tooltip("Tamanho da area do texto principal.")]
    public Vector2 tamanhoAreaTextoPrincipal = new Vector2(500f, 145f);

    [Tooltip("Campo antigo mantido por compatibilidade. O layout novo usa Posicao/Tamanho do texto principal.")]
    public Vector2 margemTexto = new Vector2(70f, 95f);

    [Tooltip("Posicao X/Y do botao Confirmar dentro do painel.")]
    public Vector2 posicaoBotaoConfirmar = new Vector2(0f, -105f);

    public Vector2 tamanhoBotaoConfirmar = new Vector2(230f, 70f);

    [Tooltip("Posicao X/Y do texto dentro do botao Confirmar.")]
    public Vector2 posicaoTextoBotaoConfirmar = Vector2.zero;

    public Vector2 tamanhoTextoBotaoConfirmar = new Vector2(230f, 70f);

    [Tooltip("Posicao X/Y do botao Fechar dentro do painel.")]
    public Vector2 posicaoBotaoFechar = new Vector2(285f, 135f);

    public Vector2 tamanhoBotaoFechar = new Vector2(56f, 56f);

    public int tamanhoFonteTexto = 27;
    public int tamanhoFonteBotao = 24;

    [Header("Cores")]
    public Color corTexto = Color.white;
    public Color corTextoBotao = Color.white;
    public Color corFundoFallback = new Color(0f, 0f, 0f, 0.88f);

    [Header("Debug")]
    public bool logarEventos;

    public static bool ExistePainelAberto { get; private set; }

    public bool PainelAberto => painelRaiz != null && painelRaiz.activeSelf;

    private GameObject painelRaiz;
    private BuyableLandAreaMarker terrenoAtual;
    private Action<BuyableLandAreaMarker> aoConfirmarCompra;
    private Action aoFecharPainel;
    private string ultimoTextoMontado;

    private void Awake()
    {
        InicializarInterface();
        OcultarSemCallback();
    }

    private void Update()
    {
        if (!atualizarLayoutEmTempoReal)
            return;

        if (painelRaiz == null)
            InicializarInterface();

        ResolverInterfaceManual();
        AplicarOuSincronizarLayout();
        AplicarVisualCompleto();
    }

    private void OnValidate()
    {
        tamanhoPainel = GarantirTamanhoMinimo(tamanhoPainel, 10f);
        tamanhoAreaTextoPrincipal = GarantirTamanhoMinimo(tamanhoAreaTextoPrincipal, 10f);
        tamanhoBotaoConfirmar = GarantirTamanhoMinimo(tamanhoBotaoConfirmar, 10f);
        tamanhoTextoBotaoConfirmar = GarantirTamanhoMinimo(tamanhoTextoBotaoConfirmar, 10f);
        tamanhoBotaoFechar = GarantirTamanhoMinimo(tamanhoBotaoFechar, 10f);
        tamanhoFonteTexto = Mathf.Max(1, tamanhoFonteTexto);
        tamanhoFonteBotao = Mathf.Max(1, tamanhoFonteBotao);

        if (!Application.isPlaying)
            return;

        InicializarInterface();
        AplicarOuSincronizarLayout();
        AplicarVisualCompleto();
        AtualizarTextoConfirmacao();
    }

    private void OnDisable()
    {
        if (PainelAberto)
            OcultarSemCallback();
    }

    public void Mostrar(BuyableLandAreaMarker terreno, Action<BuyableLandAreaMarker> aoConfirmar, Action aoFechar)
    {
        InicializarInterface();

        terrenoAtual = terreno;
        aoConfirmarCompra = aoConfirmar;
        aoFecharPainel = aoFechar;

        AplicarOuSincronizarLayout();
        AplicarVisualCompleto();
        AtualizarTextoConfirmacao();

        if (painelRaiz != null)
            painelRaiz.SetActive(true);

        ExistePainelAberto = true;

        if (logarEventos)
            Debug.Log("[BuyScenePurchaseConfirmationPanel] Abriu painel de confirmacao.");
    }

    public void MostrarMensagemErro(string mensagem)
    {
        if (textoPrincipal == null)
            return;

        textoPrincipal.text = mensagem;
        ultimoTextoMontado = mensagem;
    }

    public void FecharPeloBotao()
    {
        OcultarSemCallback();

        if (aoFecharPainel != null)
            aoFecharPainel.Invoke();

        LimparCallbacks();

        if (logarEventos)
            Debug.Log("[BuyScenePurchaseConfirmationPanel] Fechou painel pelo botao close.");
    }

    public void OcultarSemCallback()
    {
        if (painelRaiz != null)
            painelRaiz.SetActive(false);

        ExistePainelAberto = false;
    }

    public void LimparCallbacks()
    {
        terrenoAtual = null;
        aoConfirmarCompra = null;
        aoFecharPainel = null;
    }

    private void ConfirmarCompra()
    {
        if (aoConfirmarCompra != null)
            aoConfirmarCompra.Invoke(terrenoAtual);
    }

    private void InicializarInterface()
    {
        ResolverInterfaceManual();

        if (painelRect == null && criarInterfaceAutomaticamente)
            CriarInterfaceAutomatica();

        ResolverInterfaceManual();
        ConfigurarEventosDosBotoes();
        AplicarOuSincronizarLayout();
        AplicarVisualCompleto();
    }

    private void ResolverInterfaceManual()
    {
        if (painelRect == null && painelRaiz != null)
            painelRect = painelRaiz.GetComponent<RectTransform>();

        if (painelRect != null)
        {
            painelRaiz = painelRect.gameObject;

            if (imagemPainel == null)
                imagemPainel = painelRect.GetComponent<RawImage>();
        }

        if (textoPrincipalRect != null && textoPrincipal == null)
            textoPrincipal = textoPrincipalRect.GetComponent<Text>();

        if (textoPrincipal != null && textoPrincipalRect == null)
            textoPrincipalRect = textoPrincipal.GetComponent<RectTransform>();

        if (botaoConfirmarRect != null)
        {
            if (imagemBotaoConfirmar == null)
                imagemBotaoConfirmar = botaoConfirmarRect.GetComponent<RawImage>();

            if (botaoConfirmar == null)
                botaoConfirmar = botaoConfirmarRect.GetComponent<BuySceneUIImageButton>();
        }

        if (botaoConfirmar != null && botaoConfirmarRect == null)
            botaoConfirmarRect = botaoConfirmar.GetComponent<RectTransform>();

        if (imagemBotaoConfirmar != null && botaoConfirmarRect == null)
            botaoConfirmarRect = imagemBotaoConfirmar.GetComponent<RectTransform>();

        if (textoBotaoConfirmarRect != null && textoConfirmar == null)
            textoConfirmar = textoBotaoConfirmarRect.GetComponent<Text>();

        if (textoConfirmar != null && textoBotaoConfirmarRect == null)
            textoBotaoConfirmarRect = textoConfirmar.GetComponent<RectTransform>();

        if (botaoFecharRect != null)
        {
            if (imagemBotaoFechar == null)
                imagemBotaoFechar = botaoFecharRect.GetComponent<RawImage>();

            if (botaoFechar == null)
                botaoFechar = botaoFecharRect.GetComponent<BuySceneUIImageButton>();
        }

        if (botaoFechar != null && botaoFecharRect == null)
            botaoFecharRect = botaoFechar.GetComponent<RectTransform>();

        if (imagemBotaoFechar != null && botaoFecharRect == null)
            botaoFecharRect = imagemBotaoFechar.GetComponent<RectTransform>();
    }

    private void CriarInterfaceAutomatica()
    {
        if (painelRect != null)
            return;

        GarantirCanvas();
        GarantirEventSystem();

        GameObject painelObjeto = new GameObject("BuyScene_Painel_Confirmacao_Compra", typeof(RectTransform), typeof(RawImage), typeof(CanvasGroup));
        painelObjeto.transform.SetParent(canvasAlvo.transform, false);

        painelRaiz = painelObjeto;
        painelRect = painelObjeto.GetComponent<RectTransform>();
        imagemPainel = painelObjeto.GetComponent<RawImage>();
        imagemPainel.raycastTarget = true;

        CriarTextoPrincipal(painelObjeto.transform);
        CriarBotaoConfirmar(painelObjeto.transform);
        CriarBotaoFechar(painelObjeto.transform);
    }

    private void GarantirCanvas()
    {
        if (canvasAlvo != null)
            return;

        canvasAlvo = FindObjectOfType<Canvas>();

        if (canvasAlvo != null)
            return;

        GameObject objetoCanvas = new GameObject("Canvas_BuyScene_Auto", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasAlvo = objetoCanvas.GetComponent<Canvas>();
        canvasAlvo.renderMode = RenderMode.ScreenSpaceOverlay;
        canvasAlvo.sortingOrder = 100;

        CanvasScaler scaler = objetoCanvas.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
    }

    private void GarantirEventSystem()
    {
        if (FindObjectOfType<EventSystem>() != null)
            return;

        new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
    }

    private void CriarTextoPrincipal(Transform pai)
    {
        GameObject objetoTexto = new GameObject("Texto_Confirmacao", typeof(RectTransform), typeof(Text));
        objetoTexto.transform.SetParent(pai, false);

        textoPrincipalRect = objetoTexto.GetComponent<RectTransform>();
        textoPrincipal = objetoTexto.GetComponent<Text>();
        textoPrincipal.raycastTarget = false;
        textoPrincipal.horizontalOverflow = HorizontalWrapMode.Wrap;
        textoPrincipal.verticalOverflow = VerticalWrapMode.Overflow;
    }

    private void CriarBotaoConfirmar(Transform pai)
    {
        GameObject objetoBotao = new GameObject("Botao_Confirmar", typeof(RectTransform), typeof(RawImage), typeof(BuySceneUIImageButton));
        objetoBotao.transform.SetParent(pai, false);

        botaoConfirmarRect = objetoBotao.GetComponent<RectTransform>();
        imagemBotaoConfirmar = objetoBotao.GetComponent<RawImage>();
        imagemBotaoConfirmar.raycastTarget = true;
        botaoConfirmar = objetoBotao.GetComponent<BuySceneUIImageButton>();

        GameObject objetoTextoBotao = new GameObject("Texto_Botao_Confirmar", typeof(RectTransform), typeof(Text));
        objetoTextoBotao.transform.SetParent(objetoBotao.transform, false);

        textoBotaoConfirmarRect = objetoTextoBotao.GetComponent<RectTransform>();
        textoConfirmar = objetoTextoBotao.GetComponent<Text>();
        textoConfirmar.raycastTarget = false;
    }

    private void CriarBotaoFechar(Transform pai)
    {
        GameObject objetoBotao = new GameObject("Botao_Fechar", typeof(RectTransform), typeof(RawImage), typeof(BuySceneUIImageButton));
        objetoBotao.transform.SetParent(pai, false);

        botaoFecharRect = objetoBotao.GetComponent<RectTransform>();
        imagemBotaoFechar = objetoBotao.GetComponent<RawImage>();
        imagemBotaoFechar.raycastTarget = true;
        botaoFechar = objetoBotao.GetComponent<BuySceneUIImageButton>();
    }

    private void ConfigurarEventosDosBotoes()
    {
        if (botaoConfirmar != null)
        {
            botaoConfirmar.Clique -= ConfirmarCompra;
            botaoConfirmar.Clique += ConfirmarCompra;
        }

        if (botaoFechar != null)
        {
            botaoFechar.Clique -= FecharPeloBotao;
            botaoFechar.Clique += FecharPeloBotao;
        }
    }

    private void AplicarOuSincronizarLayout()
    {
        if (fonteDoLayout == FonteDoLayout.RectTransformManual)
        {
            SincronizarCamposPelosRectTransforms();
            return;
        }

        AplicarLayoutCompleto();
    }

    private void AplicarLayoutCompleto()
    {
        AplicarRectCentralizado(painelRect, posicaoPainel, tamanhoPainel);
        AplicarRectCentralizado(textoPrincipalRect, posicaoTextoPrincipal, tamanhoAreaTextoPrincipal);
        AplicarRectCentralizado(botaoConfirmarRect, posicaoBotaoConfirmar, tamanhoBotaoConfirmar);
        AplicarRectCentralizado(textoBotaoConfirmarRect, posicaoTextoBotaoConfirmar, tamanhoTextoBotaoConfirmar);
        AplicarRectCentralizado(botaoFecharRect, posicaoBotaoFechar, tamanhoBotaoFechar);
    }

    private void SincronizarCamposPelosRectTransforms()
    {
        if (!sincronizarCamposComRectTransform)
            return;

        posicaoPainel = LerPosicaoDoRect(painelRect, posicaoPainel);
        tamanhoPainel = LerTamanhoDoRect(painelRect, tamanhoPainel);

        posicaoTextoPrincipal = LerPosicaoDoRect(textoPrincipalRect, posicaoTextoPrincipal);
        tamanhoAreaTextoPrincipal = LerTamanhoDoRect(textoPrincipalRect, tamanhoAreaTextoPrincipal);

        posicaoBotaoConfirmar = LerPosicaoDoRect(botaoConfirmarRect, posicaoBotaoConfirmar);
        tamanhoBotaoConfirmar = LerTamanhoDoRect(botaoConfirmarRect, tamanhoBotaoConfirmar);

        posicaoTextoBotaoConfirmar = LerPosicaoDoRect(textoBotaoConfirmarRect, posicaoTextoBotaoConfirmar);
        tamanhoTextoBotaoConfirmar = LerTamanhoDoRect(textoBotaoConfirmarRect, tamanhoTextoBotaoConfirmar);

        posicaoBotaoFechar = LerPosicaoDoRect(botaoFecharRect, posicaoBotaoFechar);
        tamanhoBotaoFechar = LerTamanhoDoRect(botaoFecharRect, tamanhoBotaoFechar);
    }

    private Vector2 LerPosicaoDoRect(RectTransform rect, Vector2 valorAtual)
    {
        if (rect == null)
            return valorAtual;

        return NormalizarValor(rect.anchoredPosition);
    }

    private Vector2 LerTamanhoDoRect(RectTransform rect, Vector2 valorAtual)
    {
        if (rect == null)
            return valorAtual;

        return NormalizarValor(rect.sizeDelta);
    }

    private Vector2 NormalizarValor(Vector2 valor)
    {
        if (!arredondarValoresSincronizados)
            return valor;

        valor.x = Arredondar(valor.x);
        valor.y = Arredondar(valor.y);
        return valor;
    }

    private float Arredondar(float valor)
    {
        float fator = Mathf.Pow(10f, casasDecimaisSincronizacao);
        return Mathf.Round(valor * fator) / fator;
    }

    private void AplicarVisualCompleto()
    {
        if (imagemPainel != null)
        {
            imagemPainel.texture = painelWarning;
            imagemPainel.color = painelWarning != null ? Color.white : corFundoFallback;
            imagemPainel.raycastTarget = true;
        }

        if (imagemBotaoConfirmar != null)
        {
            imagemBotaoConfirmar.texture = buttonOff;
            imagemBotaoConfirmar.color = buttonOff != null ? Color.white : new Color(0.15f, 0.45f, 0.15f, 1f);
            imagemBotaoConfirmar.raycastTarget = true;
        }

        if (botaoConfirmar != null)
            botaoConfirmar.Configurar(buttonOff, buttonOn);

        if (imagemBotaoFechar != null)
        {
            imagemBotaoFechar.texture = closeOff;
            imagemBotaoFechar.color = closeOff != null ? Color.white : new Color(0.6f, 0.1f, 0.1f, 1f);
            imagemBotaoFechar.raycastTarget = true;
        }

        if (botaoFechar != null)
            botaoFechar.Configurar(closeOff, closeOn);

        if (textoPrincipal != null)
        {
            textoPrincipal.font = ObterFontePadrao();
            textoPrincipal.fontSize = tamanhoFonteTexto;
            textoPrincipal.color = corTexto;
            textoPrincipal.alignment = TextAnchor.MiddleCenter;
            textoPrincipal.horizontalOverflow = HorizontalWrapMode.Wrap;
            textoPrincipal.verticalOverflow = VerticalWrapMode.Overflow;
            textoPrincipal.raycastTarget = false;
        }

        if (textoConfirmar != null)
        {
            textoConfirmar.font = ObterFontePadrao();
            textoConfirmar.fontSize = tamanhoFonteBotao;
            textoConfirmar.color = corTextoBotao;
            textoConfirmar.alignment = TextAnchor.MiddleCenter;
            textoConfirmar.raycastTarget = false;
            textoConfirmar.text = textoBotaoConfirmar;
        }
    }

    private void AplicarRectCentralizado(RectTransform rect, Vector2 posicao, Vector2 tamanho)
    {
        if (rect == null)
            return;

        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = posicao;
        rect.sizeDelta = tamanho;
    }

    private void AtualizarTextoConfirmacao()
    {
        if (textoPrincipal == null)
            return;

        string textoFinal = textoConfirmacao;

        if (terrenoAtual != null)
        {
            if (exibirNomeDoTerreno)
                textoFinal += "\n\n" + terrenoAtual.nomeDoTerreno;

            if (exibirPrecoDoTerreno)
                textoFinal += "\nPreço: " + terrenoAtual.precoGold.ToString("N0") + " Gold";
        }

        ultimoTextoMontado = textoFinal;
        textoPrincipal.text = textoFinal;

        if (textoConfirmar != null)
            textoConfirmar.text = textoBotaoConfirmar;
    }

    private Font ObterFontePadrao()
    {
        Font fonte = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        if (fonte == null)
            fonte = Resources.GetBuiltinResource<Font>("Arial.ttf");

        return fonte;
    }

    private Vector2 GarantirTamanhoMinimo(Vector2 tamanho, float minimo)
    {
        tamanho.x = Mathf.Max(minimo, tamanho.x);
        tamanho.y = Mathf.Max(minimo, tamanho.y);
        return tamanho;
    }
}
