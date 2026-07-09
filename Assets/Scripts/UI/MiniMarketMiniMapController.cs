using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Mini mapa circular do MiniMarket.
///
/// Recursos:
/// - Fica no canto superior esquerdo.
/// - Renderiza uma camera ortografica de cima para baixo seguindo o jogador.
/// - Mostra um ponto no centro indicando o jogador.
/// - Possui botoes laterais + e - para aumentar/diminuir o zoom.
/// - Cria a UI automaticamente em runtime, sem precisar montar o Canvas manualmente.
///
/// Uso recomendado:
/// - Crie um GameObject vazio: MiniMarket_MiniMap
/// - Adicione este script.
/// - Se quiser, arraste o Character 01 no campo Alvo.
/// - Se deixar vazio, ele procura PlayerMove automaticamente.
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

    [Header("UI")]
    [Tooltip("Canvas onde o minimapa sera criado. Se vazio, usa um Canvas existente ou cria um novo.")]
    public Canvas canvasUI;

    [Tooltip("Tamanho do minimapa em pixels.")]
    [Min(64f)] public float tamanhoMiniMapa = 168f;

    [Tooltip("Margem do canto superior esquerdo.")]
    public Vector2 margemSuperiorEsquerda = new Vector2(18f, 18f);

    [Tooltip("Tamanho do ponto do jogador no centro do mapa.")]
    [Min(4f)] public float tamanhoPontoJogador = 14f;

    [Tooltip("Tamanho dos botoes + e -.")]
    [Min(18f)] public float tamanhoBotaoZoom = 32f;

    [Tooltip("Espaco entre minimapa e botoes.")]
    public float espacamentoBotoes = 8f;

    [Tooltip("Resolucao interna do RenderTexture. 256 e leve; 512 fica mais nitido.")]
    public int resolucaoRenderTexture = 512;

    [Header("Cores")]
    public Color corBorda = new Color(0f, 0f, 0f, 0.55f);
    public Color corPontoJogador = new Color(1f, 0.12f, 0.08f, 1f);
    public Color corBotao = new Color(0.08f, 0.10f, 0.12f, 0.88f);
    public Color corTextoBotao = Color.white;

    [Header("Debug")]
    public bool recriarUIAgora;
    public bool logarEventos;

    private RenderTexture renderTexture;
    private RectTransform raizMiniMapa;
    private RectTransform mapaCircular;
    private RawImage imagemMapa;
    private Image pontoJogador;
    private Button botaoMais;
    private Button botaoMenos;
    private float zoomSuavizado;
    private float proximaBusca;

    private Texture2D texturaCirculo;
    private Texture2D texturaPonto;
    private Texture2D texturaBotao;
    private Sprite spriteCirculo;
    private Sprite spritePonto;
    private Sprite spriteBotao;

    private const string NomeCanvasAutomatico = "MiniMarket_UI_AutoCanvas";
    private const string NomeMiniMapa = "MiniMarket_Minimap";

    private void Awake()
    {
        zoomAtual = Mathf.Clamp(zoomAtual, zoomMinimo, zoomMaximo);
        zoomSuavizado = zoomAtual;

        ResolverAlvo(true);
        GarantirRenderTexture();
        GarantirCameraMiniMapa();
        GarantirUI();
    }

    private void Start()
    {
        ResolverAlvo(true);
        GarantirRenderTexture();
        GarantirCameraMiniMapa();
        GarantirUI();
        AtualizarCameraMiniMapa(true);
    }

    private void LateUpdate()
    {
        if (recriarUIAgora)
        {
            recriarUIAgora = false;
            RecriarUI();
        }

        ResolverAlvo(false);
        AtualizarCameraMiniMapa(false);
        AtualizarZoomSuavizado();
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

    public void AumentarZoom()
    {
        // Aumentar zoom = aproximar, entao reduz orthographicSize.
        zoomAtual = Mathf.Clamp(zoomAtual - passoZoom, zoomMinimo, zoomMaximo);
    }

    public void DiminuirZoom()
    {
        // Diminuir zoom = afastar, entao aumenta orthographicSize.
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

        AudioListener listener = cameraMiniMapa.GetComponent<AudioListener>();
        if (listener != null)
            Destroy(listener);
    }

    private void GarantirUI()
    {
        GarantirCanvas();
        GarantirSprites();

        if (raizMiniMapa != null)
            return;

        GameObject raizGo = new GameObject(NomeMiniMapa, typeof(RectTransform));
        raizGo.transform.SetParent(canvasUI.transform, false);
        raizMiniMapa = raizGo.GetComponent<RectTransform>();
        raizMiniMapa.anchorMin = new Vector2(0f, 1f);
        raizMiniMapa.anchorMax = new Vector2(0f, 1f);
        raizMiniMapa.pivot = new Vector2(0f, 1f);
        raizMiniMapa.anchoredPosition = new Vector2(margemSuperiorEsquerda.x, -margemSuperiorEsquerda.y);
        raizMiniMapa.sizeDelta = new Vector2(tamanhoMiniMapa + tamanhoBotaoZoom + espacamentoBotoes, tamanhoMiniMapa);

        GameObject bordaGo = new GameObject("Borda_Redonda", typeof(RectTransform), typeof(Image));
        bordaGo.transform.SetParent(raizMiniMapa, false);
        RectTransform bordaRect = bordaGo.GetComponent<RectTransform>();
        bordaRect.anchorMin = new Vector2(0f, 1f);
        bordaRect.anchorMax = new Vector2(0f, 1f);
        bordaRect.pivot = new Vector2(0f, 1f);
        bordaRect.anchoredPosition = Vector2.zero;
        bordaRect.sizeDelta = new Vector2(tamanhoMiniMapa, tamanhoMiniMapa);
        Image bordaImage = bordaGo.GetComponent<Image>();
        bordaImage.sprite = spriteCirculo;
        bordaImage.color = corBorda;

        GameObject maskGo = new GameObject("Mapa_Circular_Mask", typeof(RectTransform), typeof(Image), typeof(Mask));
        maskGo.transform.SetParent(raizMiniMapa, false);
        mapaCircular = maskGo.GetComponent<RectTransform>();
        mapaCircular.anchorMin = new Vector2(0f, 1f);
        mapaCircular.anchorMax = new Vector2(0f, 1f);
        mapaCircular.pivot = new Vector2(0f, 1f);
        mapaCircular.anchoredPosition = new Vector2(4f, -4f);
        mapaCircular.sizeDelta = new Vector2(tamanhoMiniMapa - 8f, tamanhoMiniMapa - 8f);

        Image maskImage = maskGo.GetComponent<Image>();
        maskImage.sprite = spriteCirculo;
        maskImage.color = Color.white;

        Mask mask = maskGo.GetComponent<Mask>();
        mask.showMaskGraphic = false;

        GameObject rawGo = new GameObject("Imagem_Render_Minimap", typeof(RectTransform), typeof(RawImage));
        rawGo.transform.SetParent(mapaCircular, false);
        RectTransform rawRect = rawGo.GetComponent<RectTransform>();
        rawRect.anchorMin = Vector2.zero;
        rawRect.anchorMax = Vector2.one;
        rawRect.offsetMin = Vector2.zero;
        rawRect.offsetMax = Vector2.zero;
        imagemMapa = rawGo.GetComponent<RawImage>();
        imagemMapa.texture = renderTexture;
        imagemMapa.color = Color.white;

        GameObject pontoGo = new GameObject("Ponto_Jogador", typeof(RectTransform), typeof(Image));
        pontoGo.transform.SetParent(raizMiniMapa, false);
        RectTransform pontoRect = pontoGo.GetComponent<RectTransform>();
        pontoRect.anchorMin = new Vector2(0f, 1f);
        pontoRect.anchorMax = new Vector2(0f, 1f);
        pontoRect.pivot = new Vector2(0.5f, 0.5f);
        pontoRect.anchoredPosition = new Vector2(tamanhoMiniMapa * 0.5f, -tamanhoMiniMapa * 0.5f);
        pontoRect.sizeDelta = new Vector2(tamanhoPontoJogador, tamanhoPontoJogador);
        pontoJogador = pontoGo.GetComponent<Image>();
        pontoJogador.sprite = spritePonto;
        pontoJogador.color = corPontoJogador;

        float xBotoes = tamanhoMiniMapa + espacamentoBotoes;
        botaoMais = CriarBotaoZoom("Botao_Zoom_Mais", "+", new Vector2(xBotoes, -tamanhoMiniMapa * 0.5f + tamanhoBotaoZoom * 0.6f));
        botaoMenos = CriarBotaoZoom("Botao_Zoom_Menos", "-", new Vector2(xBotoes, -tamanhoMiniMapa * 0.5f - tamanhoBotaoZoom * 0.6f));

        botaoMais.onClick.AddListener(AumentarZoom);
        botaoMenos.onClick.AddListener(DiminuirZoom);

        GarantirEventSystem();

        if (logarEventos)
            Debug.Log("[MiniMarketMiniMapController] Mini mapa criado automaticamente.");
    }

    private Button CriarBotaoZoom(string nome, string texto, Vector2 anchoredPosition)
    {
        GameObject botaoGo = new GameObject(nome, typeof(RectTransform), typeof(Image), typeof(Button));
        botaoGo.transform.SetParent(raizMiniMapa, false);

        RectTransform rect = botaoGo.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = new Vector2(tamanhoBotaoZoom, tamanhoBotaoZoom);

        Image image = botaoGo.GetComponent<Image>();
        image.sprite = spriteBotao;
        image.color = corBotao;

        Button button = botaoGo.GetComponent<Button>();
        button.targetGraphic = image;

        ColorBlock colors = button.colors;
        colors.normalColor = corBotao;
        colors.highlightedColor = new Color(corBotao.r + 0.12f, corBotao.g + 0.12f, corBotao.b + 0.12f, corBotao.a);
        colors.pressedColor = new Color(corBotao.r * 0.75f, corBotao.g * 0.75f, corBotao.b * 0.75f, corBotao.a);
        colors.selectedColor = colors.highlightedColor;
        button.colors = colors;

        GameObject textoGo = new GameObject("Text", typeof(RectTransform), typeof(Text));
        textoGo.transform.SetParent(botaoGo.transform, false);
        RectTransform textoRect = textoGo.GetComponent<RectTransform>();
        textoRect.anchorMin = Vector2.zero;
        textoRect.anchorMax = Vector2.one;
        textoRect.offsetMin = Vector2.zero;
        textoRect.offsetMax = Vector2.zero;

        Text text = textoGo.GetComponent<Text>();
        text.text = texto;
        text.alignment = TextAnchor.MiddleCenter;
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = Mathf.RoundToInt(tamanhoBotaoZoom * 0.72f);
        text.color = corTextoBotao;
        text.raycastTarget = false;

        return button;
    }

    private void GarantirCanvas()
    {
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

        GameObject canvasGo = new GameObject(NomeCanvasAutomatico, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasUI = canvasGo.GetComponent<Canvas>();
        canvasUI.renderMode = RenderMode.ScreenSpaceOverlay;
        canvasUI.sortingOrder = 50;

        CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
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
        if (cameraMiniMapa == null)
            return;

        if (alvo == null)
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

    private void RecriarUI()
    {
        if (raizMiniMapa != null)
            Destroy(raizMiniMapa.gameObject);

        raizMiniMapa = null;
        mapaCircular = null;
        imagemMapa = null;
        pontoJogador = null;
        botaoMais = null;
        botaoMenos = null;

        GarantirRenderTexture();
        GarantirUI();
    }

    private void DestroyTextureSafe(Texture2D texture)
    {
        if (texture != null)
            Destroy(texture);
    }

    private void OnValidate()
    {
        tamanhoMiniMapa = Mathf.Max(64f, tamanhoMiniMapa);
        tamanhoPontoJogador = Mathf.Max(4f, tamanhoPontoJogador);
        tamanhoBotaoZoom = Mathf.Max(18f, tamanhoBotaoZoom);
        resolucaoRenderTexture = Mathf.Clamp(resolucaoRenderTexture, 128, 2048);

        zoomMinimo = Mathf.Max(2f, zoomMinimo);
        zoomMaximo = Mathf.Max(zoomMinimo + 1f, zoomMaximo);
        zoomAtual = Mathf.Clamp(zoomAtual, zoomMinimo, zoomMaximo);
        passoZoom = Mathf.Max(0.5f, passoZoom);
    }
}
