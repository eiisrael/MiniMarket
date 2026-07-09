using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Mini mapa circular do MiniMarket.
///
/// Recursos:
/// - Fica no canto superior esquerdo real da tela.
/// - Renderiza uma camera ortografica de cima para baixo seguindo o jogador.
/// - Mostra um ponto no centro indicando o jogador.
/// - Possui botoes laterais + e - para aumentar/diminuir o zoom.
/// - Abre/fecha com M.
/// - Atualiza tamanho, margem, cores, zoom e botoes em tempo real no Inspector.
/// - Usa Canvas proprio por padrao para nao herdar posicao/escala do GoldHUD.
/// </summary>
[DefaultExecutionOrder(25000)]
[DisallowMultipleComponent]
public class MiniMarketMiniMapController : MonoBehaviour
{
    [Header("Alvo")]
    [Tooltip("Jogador que o minimapa deve seguir. Se vazio, procura PlayerMove automaticamente.")]
    public Transform alvo;

    [Tooltip("Se ligado, procura PlayerMove automaticamente quando Alvo estiver vazio.")]
    public bool procurarPlayerAutomaticamente = true;

    [Min(0.1f)]
    public float intervaloBuscaPlayer = 1f;

    [Header("Abrir / Fechar")]
    public KeyCode teclaAbrirFechar = KeyCode.M;
    public bool iniciarAberto = true;

    [Tooltip("Estado atual do minimapa. Pode alterar em tempo real no Inspector.")]
    public bool minimapaAberto = true;

    [Header("Camera do MiniMapa")]
    [Tooltip("Camera interna do minimapa. Se vazio, o script cria automaticamente.")]
    public Camera cameraMiniMapa;

    [Tooltip("Altura da camera acima do jogador.")]
    public float alturaCamera = 70f;

    [Tooltip("Rotaciona o mapa junto com o jogador. Se desligado, o norte do mapa fica fixo.")]
    public bool girarMapaComJogador = false;

    [Tooltip("Layers exibidas no minimapa. Por padrao mostra tudo.")]
    public LayerMask camadasVisiveis = ~0;

    [Tooltip("Cor de fundo da camera do minimapa.")]
    public Color corFundoCamera = new Color(0.035f, 0.045f, 0.055f, 1f);

    [Header("Zoom")]
    [Tooltip("Quanto menor, mais aproximado. Pode ajustar em tempo real.")]
    [Min(2f)] public float zoomAtual = 22f;

    [Min(2f)] public float zoomMinimo = 8f;
    [Min(3f)] public float zoomMaximo = 60f;

    [Tooltip("Quanto cada clique no + ou - altera o zoom.")]
    [Min(0.5f)] public float passoZoom = 4f;

    [Tooltip("Velocidade para suavizar a mudanca de zoom.")]
    [Min(0.1f)] public float velocidadeSuavizacaoZoom = 12f;

    [Header("Canvas / Camada")]
    [Tooltip("Recomendado ligado: cria/usa um Canvas proprio, evitando o erro de ficar preso no GoldHUD ou em outro painel.")]
    public bool usarCanvasProprioSempre = true;

    [Tooltip("Canvas onde o minimapa sera criado. Se usarCanvasProprioSempre estiver ligado, este campo sera substituido pelo Canvas proprio.")]
    public Canvas canvasUI;

    public int canvasSortingOrder = 80;

    [Header("UI - Layout")]
    [Tooltip("Atualiza tamanho, posicao e cores em tempo real no Inspector durante o Play.")]
    public bool atualizarLayoutEmTempoReal = true;

    [Tooltip("Tamanho do minimapa em pixels.")]
    [Min(64f)] public float tamanhoMiniMapa = 240f;

    [Tooltip("Margem do canto superior esquerdo.")]
    public Vector2 margemSuperiorEsquerda = new Vector2(24f, 24f);

    [Tooltip("Tamanho do ponto do jogador no centro do mapa.")]
    [Min(4f)] public float tamanhoPontoJogador = 16f;

    [Tooltip("Tamanho dos botoes + e -.")]
    [Min(18f)] public float tamanhoBotaoZoom = 36f;

    [Tooltip("Espaco entre minimapa e botoes.")]
    public float espacamentoBotoes = 10f;

    [Tooltip("Espessura visual da borda externa.")]
    [Min(0f)] public float espessuraBorda = 5f;

    [Tooltip("Resolucao interna do RenderTexture. 256 e leve; 512 fica mais nitido.")]
    public int resolucaoRenderTexture = 512;

    [Header("Cores")]
    public Color corBorda = new Color(0f, 0f, 0f, 0.58f);
    public Color corPontoJogador = new Color(1f, 0.12f, 0.08f, 1f);
    public Color corBotao = new Color(0.08f, 0.10f, 0.12f, 0.88f);
    public Color corTextoBotao = Color.white;

    [Header("Debug")]
    public bool recriarUIAgora;
    public bool logarEventos;

    private RenderTexture renderTexture;
    private RectTransform raizMiniMapa;
    private RectTransform bordaRect;
    private RectTransform mapaCircular;
    private RectTransform rawRect;
    private RectTransform pontoRect;
    private RectTransform botaoMaisRect;
    private RectTransform botaoMenosRect;
    private RectTransform textoMaisRect;
    private RectTransform textoMenosRect;

    private Image bordaImage;
    private Image maskImage;
    private RawImage imagemMapa;
    private Image pontoJogador;
    private Image botaoMaisImage;
    private Image botaoMenosImage;
    private Text textoMais;
    private Text textoMenos;
    private Button botaoMais;
    private Button botaoMenos;

    private float zoomSuavizado;
    private float proximaBusca;
    private bool iniciou;

    private Texture2D texturaCirculo;
    private Texture2D texturaPonto;
    private Texture2D texturaBotao;
    private Sprite spriteCirculo;
    private Sprite spritePonto;
    private Sprite spriteBotao;

    private const string NomeCanvasAutomatico = "MiniMarket_Minimap_Canvas";
    private const string NomeMiniMapa = "MiniMarket_Minimap";

    private void Awake()
    {
        NormalizarValores();
        minimapaAberto = iniciarAberto;
        zoomSuavizado = zoomAtual;

        ResolverAlvo(true);
        GarantirRenderTexture();
        GarantirCameraMiniMapa();
        GarantirUI();
        AplicarVisibilidade();
    }

    private void Start()
    {
        iniciou = true;
        ResolverAlvo(true);
        GarantirRenderTexture();
        GarantirCameraMiniMapa();
        GarantirUI();
        AtualizarCameraMiniMapa(true);
        AtualizarLayoutUI();
        AplicarVisibilidade();
    }

    private void Update()
    {
        if (Input.GetKeyDown(teclaAbrirFechar))
            AlternarMinimapa();
    }

    private void LateUpdate()
    {
        NormalizarValores();

        if (recriarUIAgora)
        {
            recriarUIAgora = false;
            RecriarUI();
        }

        GarantirRenderTexture();
        GarantirCameraMiniMapa();
        GarantirUI();

        ResolverAlvo(false);
        AtualizarCameraMiniMapa(false);
        AtualizarZoomSuavizado();

        if (atualizarLayoutEmTempoReal)
            AtualizarLayoutUI();

        AplicarVisibilidade();
    }

    private void OnDestroy()
    {
        if (renderTexture != null)
        {
            renderTexture.Release();
            Destroy(renderTexture);
        }

        if (cameraMiniMapa != null && cameraMiniMapa.gameObject.name == "MiniMapCamera_Auto")
            Destroy(cameraMiniMapa.gameObject);

        DestroyTextureSafe(texturaCirculo);
        DestroyTextureSafe(texturaPonto);
        DestroyTextureSafe(texturaBotao);
    }

    public void AlternarMinimapa()
    {
        minimapaAberto = !minimapaAberto;
        AplicarVisibilidade();
    }

    public void AbrirMinimapa()
    {
        minimapaAberto = true;
        AplicarVisibilidade();
    }

    public void FecharMinimapa()
    {
        minimapaAberto = false;
        AplicarVisibilidade();
    }

    public void AumentarZoom()
    {
        zoomAtual = Mathf.Clamp(zoomAtual - passoZoom, zoomMinimo, zoomMaximo);
    }

    public void DiminuirZoom()
    {
        zoomAtual = Mathf.Clamp(zoomAtual + passoZoom, zoomMinimo, zoomMaximo);
    }

    public void DefinirZoom(float novoZoom)
    {
        zoomAtual = Mathf.Clamp(novoZoom, zoomMinimo, zoomMaximo);
    }

    private void ResolverAlvo(bool forcar)
    {
        if (!procurarPlayerAutomaticamente)
            return;

        if (alvo != null && !forcar)
            return;

        if (!forcar && Time.unscaledTime < proximaBusca)
            return;

        proximaBusca = Time.unscaledTime + intervaloBuscaPlayer;

        PlayerMove playerMove = FindObjectOfType<PlayerMove>(true);
        if (playerMove != null)
        {
            alvo = playerMove.transform;
            return;
        }

        GameObject playerPorTag = GameObject.FindGameObjectWithTag("Player");
        if (playerPorTag != null)
            alvo = playerPorTag.transform;
    }

    private void GarantirRenderTexture()
    {
        resolucaoRenderTexture = Mathf.Clamp(resolucaoRenderTexture, 128, 2048);

        if (renderTexture != null && renderTexture.width == resolucaoRenderTexture && renderTexture.height == resolucaoRenderTexture)
            return;

        if (renderTexture != null)
        {
            renderTexture.Release();
            Destroy(renderTexture);
        }

        renderTexture = new RenderTexture(resolucaoRenderTexture, resolucaoRenderTexture, 16, RenderTextureFormat.ARGB32);
        renderTexture.name = "MiniMarket_Minimap_RenderTexture";
        renderTexture.Create();

        if (imagemMapa != null)
            imagemMapa.texture = renderTexture;

        if (cameraMiniMapa != null)
            cameraMiniMapa.targetTexture = renderTexture;
    }

    private void GarantirCameraMiniMapa()
    {
        if (cameraMiniMapa == null)
        {
            GameObject cameraGo = new GameObject("MiniMapCamera_Auto");
            cameraGo.transform.SetParent(transform, false);
            cameraMiniMapa = cameraGo.AddComponent<Camera>();
        }

        cameraMiniMapa.orthographic = true;
        cameraMiniMapa.orthographicSize = zoomSuavizado;
        cameraMiniMapa.clearFlags = CameraClearFlags.SolidColor;
        cameraMiniMapa.backgroundColor = corFundoCamera;
        cameraMiniMapa.cullingMask = camadasVisiveis;
        cameraMiniMapa.depth = -50f;
        cameraMiniMapa.allowHDR = false;
        cameraMiniMapa.allowMSAA = false;
        cameraMiniMapa.useOcclusionCulling = false;
        cameraMiniMapa.targetTexture = renderTexture;
        cameraMiniMapa.enabled = minimapaAberto;

        AudioListener listener = cameraMiniMapa.GetComponent<AudioListener>();
        if (listener != null)
            Destroy(listener);
    }

    private void GarantirUI()
    {
        GarantirCanvas();
        GarantirSprites();

        if (raizMiniMapa != null)
        {
            if (raizMiniMapa.parent != canvasUI.transform)
                raizMiniMapa.SetParent(canvasUI.transform, false);

            return;
        }

        GameObject raizGo = new GameObject(NomeMiniMapa, typeof(RectTransform));
        raizGo.transform.SetParent(canvasUI.transform, false);
        raizMiniMapa = raizGo.GetComponent<RectTransform>();

        GameObject bordaGo = new GameObject("Borda_Redonda", typeof(RectTransform), typeof(Image));
        bordaGo.transform.SetParent(raizMiniMapa, false);
        bordaRect = bordaGo.GetComponent<RectTransform>();
        bordaImage = bordaGo.GetComponent<Image>();
        bordaImage.sprite = spriteCirculo;
        bordaImage.raycastTarget = false;

        GameObject maskGo = new GameObject("Mapa_Circular_Mask", typeof(RectTransform), typeof(Image), typeof(Mask));
        maskGo.transform.SetParent(raizMiniMapa, false);
        mapaCircular = maskGo.GetComponent<RectTransform>();
        maskImage = maskGo.GetComponent<Image>();
        maskImage.sprite = spriteCirculo;
        maskImage.color = Color.white;

        Mask mask = maskGo.GetComponent<Mask>();
        mask.showMaskGraphic = false;

        GameObject rawGo = new GameObject("Imagem_Render_Minimap", typeof(RectTransform), typeof(RawImage));
        rawGo.transform.SetParent(mapaCircular, false);
        rawRect = rawGo.GetComponent<RectTransform>();
        imagemMapa = rawGo.GetComponent<RawImage>();
        imagemMapa.texture = renderTexture;
        imagemMapa.color = Color.white;
        imagemMapa.raycastTarget = false;

        GameObject pontoGo = new GameObject("Ponto_Jogador", typeof(RectTransform), typeof(Image));
        pontoGo.transform.SetParent(raizMiniMapa, false);
        pontoRect = pontoGo.GetComponent<RectTransform>();
        pontoJogador = pontoGo.GetComponent<Image>();
        pontoJogador.sprite = spritePonto;
        pontoJogador.raycastTarget = false;

        botaoMais = CriarBotaoZoom("Botao_Zoom_Mais", "+", out botaoMaisRect, out botaoMaisImage, out textoMaisRect, out textoMais);
        botaoMenos = CriarBotaoZoom("Botao_Zoom_Menos", "-", out botaoMenosRect, out botaoMenosImage, out textoMenosRect, out textoMenos);

        botaoMais.onClick.AddListener(AumentarZoom);
        botaoMenos.onClick.AddListener(DiminuirZoom);

        GarantirEventSystem();
        AtualizarLayoutUI();

        if (logarEventos)
            Debug.Log("[MiniMarketMiniMapController] Mini mapa criado/atualizado.");
    }

    private Button CriarBotaoZoom(string nome, string texto, out RectTransform rect, out Image image, out RectTransform textoRect, out Text text)
    {
        GameObject botaoGo = new GameObject(nome, typeof(RectTransform), typeof(Image), typeof(Button));
        botaoGo.transform.SetParent(raizMiniMapa, false);

        rect = botaoGo.GetComponent<RectTransform>();
        image = botaoGo.GetComponent<Image>();
        image.sprite = spriteBotao;

        Button button = botaoGo.GetComponent<Button>();
        button.targetGraphic = image;

        GameObject textoGo = new GameObject("Text", typeof(RectTransform), typeof(Text));
        textoGo.transform.SetParent(botaoGo.transform, false);
        textoRect = textoGo.GetComponent<RectTransform>();

        text = textoGo.GetComponent<Text>();
        text.text = texto;
        text.alignment = TextAnchor.MiddleCenter;
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.raycastTarget = false;

        return button;
    }

    private void AtualizarLayoutUI()
    {
        if (raizMiniMapa == null)
            return;

        float tamanho = Mathf.Max(64f, tamanhoMiniMapa);
        float botao = Mathf.Max(18f, tamanhoBotaoZoom);
        float borda = Mathf.Max(0f, espessuraBorda);
        float totalWidth = tamanho + botao + espacamentoBotoes;

        raizMiniMapa.anchorMin = new Vector2(0f, 1f);
        raizMiniMapa.anchorMax = new Vector2(0f, 1f);
        raizMiniMapa.pivot = new Vector2(0f, 1f);
        raizMiniMapa.anchoredPosition = new Vector2(margemSuperiorEsquerda.x, -margemSuperiorEsquerda.y);
        raizMiniMapa.sizeDelta = new Vector2(totalWidth, tamanho);

        if (bordaRect != null)
        {
            bordaRect.anchorMin = new Vector2(0f, 1f);
            bordaRect.anchorMax = new Vector2(0f, 1f);
            bordaRect.pivot = new Vector2(0f, 1f);
            bordaRect.anchoredPosition = Vector2.zero;
            bordaRect.sizeDelta = new Vector2(tamanho, tamanho);
        }

        if (mapaCircular != null)
        {
            mapaCircular.anchorMin = new Vector2(0f, 1f);
            mapaCircular.anchorMax = new Vector2(0f, 1f);
            mapaCircular.pivot = new Vector2(0f, 1f);
            mapaCircular.anchoredPosition = new Vector2(borda, -borda);
            mapaCircular.sizeDelta = new Vector2(tamanho - borda * 2f, tamanho - borda * 2f);
        }

        if (rawRect != null)
        {
            rawRect.anchorMin = Vector2.zero;
            rawRect.anchorMax = Vector2.one;
            rawRect.offsetMin = Vector2.zero;
            rawRect.offsetMax = Vector2.zero;
        }

        if (pontoRect != null)
        {
            pontoRect.anchorMin = new Vector2(0f, 1f);
            pontoRect.anchorMax = new Vector2(0f, 1f);
            pontoRect.pivot = new Vector2(0.5f, 0.5f);
            pontoRect.anchoredPosition = new Vector2(tamanho * 0.5f, -tamanho * 0.5f);
            pontoRect.sizeDelta = new Vector2(tamanhoPontoJogador, tamanhoPontoJogador);
        }

        float xBotoes = tamanho + espacamentoBotoes + botao * 0.5f;
        AplicarLayoutBotao(botaoMaisRect, textoMaisRect, textoMais, new Vector2(xBotoes, -tamanho * 0.5f + botao * 0.65f), botao);
        AplicarLayoutBotao(botaoMenosRect, textoMenosRect, textoMenos, new Vector2(xBotoes, -tamanho * 0.5f - botao * 0.65f), botao);

        if (bordaImage != null)
            bordaImage.color = corBorda;

        if (pontoJogador != null)
            pontoJogador.color = corPontoJogador;

        AplicarCoresBotao(botaoMais, botaoMaisImage, textoMais);
        AplicarCoresBotao(botaoMenos, botaoMenosImage, textoMenos);

        if (cameraMiniMapa != null)
        {
            cameraMiniMapa.backgroundColor = corFundoCamera;
            cameraMiniMapa.cullingMask = camadasVisiveis;
        }
    }

    private void AplicarLayoutBotao(RectTransform rect, RectTransform textoRect, Text texto, Vector2 pos, float tamanho)
    {
        if (rect != null)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = pos;
            rect.sizeDelta = new Vector2(tamanho, tamanho);
        }

        if (textoRect != null)
        {
            textoRect.anchorMin = Vector2.zero;
            textoRect.anchorMax = Vector2.one;
            textoRect.offsetMin = Vector2.zero;
            textoRect.offsetMax = Vector2.zero;
        }

        if (texto != null)
        {
            texto.fontSize = Mathf.RoundToInt(tamanho * 0.72f);
            texto.color = corTextoBotao;
        }
    }

    private void AplicarCoresBotao(Button button, Image image, Text text)
    {
        if (image != null)
            image.color = corBotao;

        if (text != null)
            text.color = corTextoBotao;

        if (button == null)
            return;

        ColorBlock colors = button.colors;
        colors.normalColor = corBotao;
        colors.highlightedColor = new Color(Mathf.Clamp01(corBotao.r + 0.12f), Mathf.Clamp01(corBotao.g + 0.12f), Mathf.Clamp01(corBotao.b + 0.12f), corBotao.a);
        colors.pressedColor = new Color(corBotao.r * 0.75f, corBotao.g * 0.75f, corBotao.b * 0.75f, corBotao.a);
        colors.selectedColor = colors.highlightedColor;
        button.colors = colors;
    }

    private void GarantirCanvas()
    {
        if (usarCanvasProprioSempre)
        {
            Canvas proprio = EncontrarCanvasProprio();
            if (proprio == null)
                proprio = CriarCanvasProprio();

            canvasUI = proprio;
            ConfigurarCanvasProprio(canvasUI);
            return;
        }

        if (canvasUI != null)
            return;

        Canvas[] canvasExistentes = FindObjectsOfType<Canvas>(true);
        for (int i = 0; i < canvasExistentes.Length; i++)
        {
            Canvas canvas = canvasExistentes[i];
            if (canvas != null && canvas.renderMode == RenderMode.ScreenSpaceOverlay)
            {
                canvasUI = canvas;
                return;
            }
        }

        canvasUI = CriarCanvasProprio();
    }

    private Canvas EncontrarCanvasProprio()
    {
        GameObject existente = GameObject.Find(NomeCanvasAutomatico);
        if (existente == null)
            return null;

        return existente.GetComponent<Canvas>();
    }

    private Canvas CriarCanvasProprio()
    {
        GameObject canvasGo = new GameObject(NomeCanvasAutomatico, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Canvas canvas = canvasGo.GetComponent<Canvas>();
        ConfigurarCanvasProprio(canvas);
        return canvas;
    }

    private void ConfigurarCanvasProprio(Canvas canvas)
    {
        if (canvas == null)
            return;

        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = canvasSortingOrder;

        CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
        if (scaler != null)
        {
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
        }
    }

    private void GarantirEventSystem()
    {
        if (EventSystem.current != null)
            return;

        GameObject eventSystemGo = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        eventSystemGo.transform.SetParent(null, false);
    }

    private void GarantirSprites()
    {
        if (spriteCirculo == null)
        {
            texturaCirculo = CriarTexturaCircular(128, Color.white, true);
            spriteCirculo = Sprite.Create(texturaCirculo, new Rect(0, 0, texturaCirculo.width, texturaCirculo.height), new Vector2(0.5f, 0.5f), 100f);
            spriteCirculo.name = "Sprite_Circulo_Minimap_Runtime";
        }

        if (spritePonto == null)
        {
            texturaPonto = CriarTexturaCircular(64, Color.white, true);
            spritePonto = Sprite.Create(texturaPonto, new Rect(0, 0, texturaPonto.width, texturaPonto.height), new Vector2(0.5f, 0.5f), 100f);
            spritePonto.name = "Sprite_Ponto_Player_Minimap_Runtime";
        }

        if (spriteBotao == null)
        {
            texturaBotao = CriarTexturaCircular(64, Color.white, true);
            spriteBotao = Sprite.Create(texturaBotao, new Rect(0, 0, texturaBotao.width, texturaBotao.height), new Vector2(0.5f, 0.5f), 100f);
            spriteBotao.name = "Sprite_Botao_Zoom_Minimap_Runtime";
        }
    }

    private Texture2D CriarTexturaCircular(int tamanho, Color cor, bool bordaSuave)
    {
        Texture2D textura = new Texture2D(tamanho, tamanho, TextureFormat.ARGB32, false);
        textura.name = "Texture_Circle_Minimap_Runtime";
        textura.wrapMode = TextureWrapMode.Clamp;
        textura.filterMode = FilterMode.Bilinear;

        float centro = (tamanho - 1) * 0.5f;
        float raio = tamanho * 0.5f - 1f;
        float borda = bordaSuave ? 2f : 0.5f;

        for (int y = 0; y < tamanho; y++)
        {
            for (int x = 0; x < tamanho; x++)
            {
                float dx = x - centro;
                float dy = y - centro;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                float alpha = 1f - Mathf.Clamp01((dist - raio + borda) / borda);
                Color pixel = cor;
                pixel.a *= alpha;
                textura.SetPixel(x, y, pixel);
            }
        }

        textura.Apply(false, true);
        return textura;
    }

    private void AtualizarCameraMiniMapa(bool instantaneo)
    {
        if (cameraMiniMapa == null || alvo == null)
            return;

        Vector3 posicaoAlvo = alvo.position;
        cameraMiniMapa.transform.position = new Vector3(posicaoAlvo.x, posicaoAlvo.y + alturaCamera, posicaoAlvo.z);

        float yaw = girarMapaComJogador ? alvo.eulerAngles.y : 0f;
        cameraMiniMapa.transform.rotation = Quaternion.Euler(90f, yaw, 0f);

        if (instantaneo)
            cameraMiniMapa.orthographicSize = zoomAtual;
    }

    private void AtualizarZoomSuavizado()
    {
        if (cameraMiniMapa == null)
            return;

        zoomAtual = Mathf.Clamp(zoomAtual, zoomMinimo, zoomMaximo);
        float t = 1f - Mathf.Exp(-velocidadeSuavizacaoZoom * Time.unscaledDeltaTime);
        zoomSuavizado = Mathf.Lerp(zoomSuavizado, zoomAtual, t);
        cameraMiniMapa.orthographicSize = zoomSuavizado;
    }

    private void AplicarVisibilidade()
    {
        if (raizMiniMapa != null && raizMiniMapa.gameObject.activeSelf != minimapaAberto)
            raizMiniMapa.gameObject.SetActive(minimapaAberto);

        if (cameraMiniMapa != null && cameraMiniMapa.enabled != minimapaAberto)
            cameraMiniMapa.enabled = minimapaAberto;
    }

    private void RecriarUI()
    {
        if (raizMiniMapa != null)
            Destroy(raizMiniMapa.gameObject);

        raizMiniMapa = null;
        bordaRect = null;
        mapaCircular = null;
        rawRect = null;
        pontoRect = null;
        botaoMaisRect = null;
        botaoMenosRect = null;
        textoMaisRect = null;
        textoMenosRect = null;
        bordaImage = null;
        maskImage = null;
        imagemMapa = null;
        pontoJogador = null;
        botaoMaisImage = null;
        botaoMenosImage = null;
        textoMais = null;
        textoMenos = null;
        botaoMais = null;
        botaoMenos = null;

        GarantirRenderTexture();
        GarantirUI();
        AplicarVisibilidade();
    }

    private void DestroyTextureSafe(Texture2D texture)
    {
        if (texture != null)
            Destroy(texture);
    }

    private void NormalizarValores()
    {
        tamanhoMiniMapa = Mathf.Max(64f, tamanhoMiniMapa);
        tamanhoPontoJogador = Mathf.Max(4f, tamanhoPontoJogador);
        tamanhoBotaoZoom = Mathf.Max(18f, tamanhoBotaoZoom);
        resolucaoRenderTexture = Mathf.Clamp(resolucaoRenderTexture, 128, 2048);

        zoomMinimo = Mathf.Max(2f, zoomMinimo);
        zoomMaximo = Mathf.Max(zoomMinimo + 1f, zoomMaximo);
        zoomAtual = Mathf.Clamp(zoomAtual, zoomMinimo, zoomMaximo);
        passoZoom = Mathf.Max(0.5f, passoZoom);
        velocidadeSuavizacaoZoom = Mathf.Max(0.1f, velocidadeSuavizacaoZoom);
        espessuraBorda = Mathf.Max(0f, espessuraBorda);
        intervaloBuscaPlayer = Mathf.Max(0.1f, intervaloBuscaPlayer);
    }

    private void OnValidate()
    {
        NormalizarValores();

        if (!Application.isPlaying || !iniciou)
            return;

        if (atualizarLayoutEmTempoReal)
            AtualizarLayoutUI();

        AplicarVisibilidade();
    }
}
