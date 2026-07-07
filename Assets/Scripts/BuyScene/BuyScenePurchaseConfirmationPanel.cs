using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Painel de confirmacao de compra da BuyScene.
/// Ele cria a UI automaticamente em runtime usando RawImage + Texture2D.
/// Assim os PNGs painel_warning/button_on/button_off/close_on/close_off funcionam
/// mesmo se estiverem importados como Default Texture.
/// </summary>
public class BuyScenePurchaseConfirmationPanel : MonoBehaviour
{
    [Header("Canvas")]
    [Tooltip("Canvas onde o painel sera criado. Se vazio, o script cria um Canvas automatico.")]
    public Canvas canvasAlvo;

    [Tooltip("Cria a hierarquia da janela automaticamente ao iniciar.")]
    public bool criarInterfaceAutomaticamente = true;

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

    [Header("Layout")]
    public Vector2 tamanhoPainel = new Vector2(640f, 360f);
    public Vector2 tamanhoBotaoConfirmar = new Vector2(230f, 70f);
    public Vector2 posicaoBotaoConfirmar = new Vector2(0f, -105f);
    public Vector2 tamanhoBotaoFechar = new Vector2(56f, 56f);
    public Vector2 posicaoBotaoFechar = new Vector2(285f, 135f);
    public Vector2 margemTexto = new Vector2(70f, 95f);
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
    private RawImage imagemPainel;
    private Text textoPrincipal;
    private Text textoConfirmar;
    private BuySceneUIImageButton botaoConfirmar;
    private BuySceneUIImageButton botaoFechar;

    private BuyableLandAreaMarker terrenoAtual;
    private Action<BuyableLandAreaMarker> aoConfirmarCompra;
    private Action aoFecharPainel;

    private void Awake()
    {
        if (criarInterfaceAutomaticamente)
            CriarInterfaceSeNecessario();

        OcultarSemCallback();
    }

    private void OnDisable()
    {
        if (PainelAberto)
            OcultarSemCallback();
    }

    public void Mostrar(BuyableLandAreaMarker terreno, Action<BuyableLandAreaMarker> aoConfirmar, Action aoFechar)
    {
        CriarInterfaceSeNecessario();

        terrenoAtual = terreno;
        aoConfirmarCompra = aoConfirmar;
        aoFecharPainel = aoFechar;

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

    private void ConfirmarCompra()
    {
        if (aoConfirmarCompra != null)
            aoConfirmarCompra.Invoke(terrenoAtual);
    }

    public void LimparCallbacks()
    {
        terrenoAtual = null;
        aoConfirmarCompra = null;
        aoFecharPainel = null;
    }

    private void CriarInterfaceSeNecessario()
    {
        if (painelRaiz != null)
            return;

        GarantirCanvas();
        GarantirEventSystem();

        painelRaiz = new GameObject("BuyScene_Painel_Confirmacao_Compra", typeof(RectTransform), typeof(CanvasGroup));
        painelRaiz.transform.SetParent(canvasAlvo.transform, false);

        RectTransform painelRect = painelRaiz.GetComponent<RectTransform>();
        painelRect.anchorMin = new Vector2(0.5f, 0.5f);
        painelRect.anchorMax = new Vector2(0.5f, 0.5f);
        painelRect.pivot = new Vector2(0.5f, 0.5f);
        painelRect.anchoredPosition = Vector2.zero;
        painelRect.sizeDelta = tamanhoPainel;

        imagemPainel = painelRaiz.AddComponent<RawImage>();
        imagemPainel.texture = painelWarning;
        imagemPainel.color = painelWarning != null ? Color.white : corFundoFallback;
        imagemPainel.raycastTarget = true;

        CriarTextoPrincipal(painelRaiz.transform);
        CriarBotaoConfirmar(painelRaiz.transform);
        CriarBotaoFechar(painelRaiz.transform);
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

        RectTransform rect = objetoTexto.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.offsetMin = new Vector2(margemTexto.x, margemTexto.y);
        rect.offsetMax = new Vector2(-margemTexto.x, -margemTexto.y);

        textoPrincipal = objetoTexto.GetComponent<Text>();
        textoPrincipal.font = ObterFontePadrao();
        textoPrincipal.fontSize = tamanhoFonteTexto;
        textoPrincipal.color = corTexto;
        textoPrincipal.alignment = TextAnchor.MiddleCenter;
        textoPrincipal.raycastTarget = false;
        textoPrincipal.horizontalOverflow = HorizontalWrapMode.Wrap;
        textoPrincipal.verticalOverflow = VerticalWrapMode.Overflow;
    }

    private void CriarBotaoConfirmar(Transform pai)
    {
        GameObject objetoBotao = new GameObject("Botao_Confirmar", typeof(RectTransform), typeof(RawImage), typeof(BuySceneUIImageButton));
        objetoBotao.transform.SetParent(pai, false);

        RectTransform rect = objetoBotao.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = posicaoBotaoConfirmar;
        rect.sizeDelta = tamanhoBotaoConfirmar;

        RawImage imagemBotao = objetoBotao.GetComponent<RawImage>();
        imagemBotao.texture = buttonOff;
        imagemBotao.color = buttonOff != null ? Color.white : new Color(0.15f, 0.45f, 0.15f, 1f);
        imagemBotao.raycastTarget = true;

        botaoConfirmar = objetoBotao.GetComponent<BuySceneUIImageButton>();
        botaoConfirmar.Configurar(buttonOff, buttonOn);
        botaoConfirmar.Clique += ConfirmarCompra;

        GameObject objetoTextoBotao = new GameObject("Texto_Botao_Confirmar", typeof(RectTransform), typeof(Text));
        objetoTextoBotao.transform.SetParent(objetoBotao.transform, false);

        RectTransform textoRect = objetoTextoBotao.GetComponent<RectTransform>();
        textoRect.anchorMin = Vector2.zero;
        textoRect.anchorMax = Vector2.one;
        textoRect.offsetMin = Vector2.zero;
        textoRect.offsetMax = Vector2.zero;

        textoConfirmar = objetoTextoBotao.GetComponent<Text>();
        textoConfirmar.font = ObterFontePadrao();
        textoConfirmar.fontSize = tamanhoFonteBotao;
        textoConfirmar.color = corTextoBotao;
        textoConfirmar.alignment = TextAnchor.MiddleCenter;
        textoConfirmar.raycastTarget = false;
        textoConfirmar.text = textoBotaoConfirmar;
    }

    private void CriarBotaoFechar(Transform pai)
    {
        GameObject objetoBotao = new GameObject("Botao_Fechar", typeof(RectTransform), typeof(RawImage), typeof(BuySceneUIImageButton));
        objetoBotao.transform.SetParent(pai, false);

        RectTransform rect = objetoBotao.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = posicaoBotaoFechar;
        rect.sizeDelta = tamanhoBotaoFechar;

        RawImage imagemBotao = objetoBotao.GetComponent<RawImage>();
        imagemBotao.texture = closeOff;
        imagemBotao.color = closeOff != null ? Color.white : new Color(0.6f, 0.1f, 0.1f, 1f);
        imagemBotao.raycastTarget = true;

        botaoFechar = objetoBotao.GetComponent<BuySceneUIImageButton>();
        botaoFechar.Configurar(closeOff, closeOn);
        botaoFechar.Clique += FecharPeloBotao;
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
}
