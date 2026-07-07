using UnityEngine;
using UnityEngine.UI;

public class StaminaHUDHardcore : MonoBehaviour
{
    [Header("Referências")]
    public PlayerMoveHardcore2 player;

    [Tooltip("Arraste aqui o RectTransform do objeto Energy/BarraStamina.")]
    public RectTransform barraStaminaRect;

    [Tooltip("Arraste aqui o Image da barra verde/amarela/vermelha de stamina.")]
    public Image barraStaminaImagem;

    [Tooltip("Arraste aqui o Image do ícone do HUD. No seu caso: StaminaHUD -> Image.")]
    public Image iconeEnergiaImagem;

    [Tooltip("Opcional: CanvasGroup do StaminaHUD.")]
    public CanvasGroup canvasGroup;

    [Tooltip("Opcional: texto de porcentagem.")]
    public Text textoStamina;

    [Header("Sprites do Ícone")]
    public Sprite greenEnergySprite;
    public Sprite yellowEnergySprite;
    public Sprite redEnergySprite;

    [Header("Configuração")]
    public bool procurarPlayerAutomaticamente = true;
    public bool mostrarTexto = true;

    [Tooltip("Troca o ícone energy.png automaticamente por green/yellow/red.")]
    public bool trocarIconePorEstado = true;

    [Tooltip("Se ligado, o ícone também recebe a cor do estado. Se desligado, usa a cor original do PNG.")]
    public bool tingirIconeComCorDoEstado = false;

    [Header("Animação")]
    [Min(0.1f)]
    public float velocidadeBarra = 10f;

    [Min(0.1f)]
    public float velocidadeFade = 8f;

    public bool reduzirAlphaQuandoCheia = false;

    [Range(0f, 1f)]
    public float alphaQuandoCheia = 0.35f;

    [Range(0f, 1f)]
    public float alphaNormal = 1f;

    [Header("Cores")]
    public bool usarCoresPorEstado = true;

    [Range(0f, 1f)]
    public float limiteStaminaBaixa = 0.35f;

    [Range(0f, 1f)]
    public float limiteStaminaCritica = 0.15f;

    public Color corNormal = new Color(0.1f, 1f, 0.45f, 1f);
    public Color corBaixa = new Color(1f, 0.8f, 0.1f, 1f);
    public Color corCritica = new Color(1f, 0.15f, 0.08f, 1f);

    private float staminaVisual = 1f;

    private float larguraOriginal;
    private float alturaOriginal;
    private float posXOriginal;
    private float pivotXOriginal;

    private bool inicializado;

    private enum EstadoStamina
    {
        Normal,
        Baixa,
        Critica
    }

    private EstadoStamina estadoAtual = EstadoStamina.Normal;

    private void Awake()
    {
        Inicializar();
    }

    private void OnEnable()
    {
        Inicializar();
    }

    private void Inicializar()
    {
        if (inicializado)
            return;

        if (player == null && procurarPlayerAutomaticamente)
            player = FindObjectOfType<PlayerMoveHardcore2>();

        if (canvasGroup == null)
            canvasGroup = GetComponent<CanvasGroup>();

        if (barraStaminaImagem == null && barraStaminaRect != null)
            barraStaminaImagem = barraStaminaRect.GetComponent<Image>();

        if (iconeEnergiaImagem == null)
        {
            Transform imagemFilha = transform.Find("Image");

            if (imagemFilha != null)
                iconeEnergiaImagem = imagemFilha.GetComponent<Image>();
        }

        if (barraStaminaRect != null)
        {
            larguraOriginal = barraStaminaRect.sizeDelta.x;
            alturaOriginal = barraStaminaRect.sizeDelta.y;
            posXOriginal = barraStaminaRect.anchoredPosition.x;
            pivotXOriginal = barraStaminaRect.pivot.x;
        }

        inicializado = true;
    }

    private void Update()
    {
        if (player == null)
        {
            if (procurarPlayerAutomaticamente)
                player = FindObjectOfType<PlayerMoveHardcore2>();

            return;
        }

        float staminaAlvo = player.StaminaPercentual01;

        float suavizacao = CalcularSuavizacao(
            velocidadeBarra,
            Time.deltaTime
        );

        staminaVisual = Mathf.Lerp(
            staminaVisual,
            staminaAlvo,
            suavizacao
        );

        AtualizarTamanhoDaBarra();
        AtualizarEstadoVisual(staminaAlvo);
        AtualizarTexto(staminaAlvo);
        AtualizarFade(staminaAlvo);
    }

    private void AtualizarTamanhoDaBarra()
    {
        if (barraStaminaRect == null)
            return;

        float novaLargura = larguraOriginal * Mathf.Clamp01(staminaVisual);

        barraStaminaRect.SetSizeWithCurrentAnchors(
            RectTransform.Axis.Horizontal,
            novaLargura
        );

        barraStaminaRect.SetSizeWithCurrentAnchors(
            RectTransform.Axis.Vertical,
            alturaOriginal
        );

        float esquerdaOriginal = posXOriginal - larguraOriginal * pivotXOriginal;
        float novoPosX = esquerdaOriginal + novaLargura * pivotXOriginal;

        Vector2 pos = barraStaminaRect.anchoredPosition;
        pos.x = novoPosX;
        barraStaminaRect.anchoredPosition = pos;
    }

    private void AtualizarEstadoVisual(float staminaReal)
    {
        EstadoStamina novoEstado = ObterEstadoStamina(staminaReal);

        if (novoEstado == estadoAtual)
        {
            AtualizarCor(novoEstado);
            return;
        }

        estadoAtual = novoEstado;

        AtualizarCor(novoEstado);
        AtualizarIcone(novoEstado);
    }

    private EstadoStamina ObterEstadoStamina(float staminaReal)
    {
        if (staminaReal <= limiteStaminaCritica)
            return EstadoStamina.Critica;

        if (staminaReal <= limiteStaminaBaixa)
            return EstadoStamina.Baixa;

        return EstadoStamina.Normal;
    }

    private void AtualizarCor(EstadoStamina estado)
    {
        if (!usarCoresPorEstado)
            return;

        Color corFinal = corNormal;

        if (estado == EstadoStamina.Critica)
            corFinal = corCritica;
        else if (estado == EstadoStamina.Baixa)
            corFinal = corBaixa;
        else
            corFinal = corNormal;

        if (barraStaminaImagem != null)
            barraStaminaImagem.color = corFinal;

        if (iconeEnergiaImagem != null)
        {
            if (tingirIconeComCorDoEstado)
                iconeEnergiaImagem.color = corFinal;
            else
                iconeEnergiaImagem.color = Color.white;
        }
    }

    private void AtualizarIcone(EstadoStamina estado)
    {
        if (!trocarIconePorEstado)
            return;

        if (iconeEnergiaImagem == null)
            return;

        if (estado == EstadoStamina.Critica)
        {
            if (redEnergySprite != null)
                iconeEnergiaImagem.sprite = redEnergySprite;
        }
        else if (estado == EstadoStamina.Baixa)
        {
            if (yellowEnergySprite != null)
                iconeEnergiaImagem.sprite = yellowEnergySprite;
        }
        else
        {
            if (greenEnergySprite != null)
                iconeEnergiaImagem.sprite = greenEnergySprite;
        }
    }

    private void AtualizarTexto(float staminaReal)
    {
        if (textoStamina == null)
            return;

        textoStamina.enabled = mostrarTexto;

        if (!mostrarTexto)
            return;

        int percentual = Mathf.RoundToInt(staminaReal * 100f);
        textoStamina.text = percentual.ToString() + "%";
    }

    private void AtualizarFade(float staminaReal)
    {
        if (canvasGroup == null)
            return;

        bool staminaCheia =
            staminaReal >= 0.995f &&
            !player.EstaGastandoStamina &&
            !player.EstaCansado;

        float alphaAlvo = alphaNormal;

        if (reduzirAlphaQuandoCheia && staminaCheia)
            alphaAlvo = alphaQuandoCheia;

        float suavizacao = CalcularSuavizacao(
            velocidadeFade,
            Time.deltaTime
        );

        canvasGroup.alpha = Mathf.Lerp(
            canvasGroup.alpha,
            alphaAlvo,
            suavizacao
        );
    }

    private float CalcularSuavizacao(float velocidade, float deltaTime)
    {
        return 1f - Mathf.Exp(-velocidade * deltaTime);
    }
}