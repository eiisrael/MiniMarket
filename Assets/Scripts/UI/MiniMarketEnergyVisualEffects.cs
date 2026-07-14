using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>
/// Camada visual complementar do HUD de energia.
/// Mantém o texto em porcentagem, troca o raio verde/amarelo/vermelho,
/// suaviza as mudanças de cor da progress bar e pulsa o ícone durante a corrida.
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(26000)]
public sealed class MiniMarketEnergyVisualEffects : MonoBehaviour
{
    [Header("Referências")]
    public MiniMarketEnergyProgressBar progressBar;
    public CameraRelativeMovement movimento;
    public Text textoPorcentagem;
    public Image iconeRaio;
    public Image preenchimento;

    [Header("Sprites do raio")]
    public Sprite raioVerde;
    public Sprite raioAmarelo;
    public Sprite raioVermelho;

    [Header("Faixas")]
    [Range(0f, 1f)] public float inicioVerde = 0.61f;
    [Range(0f, 1f)] public float inicioVermelho = 0.25f;

    [Header("Degradê da progress bar")]
    public Color corVerde = new Color(0.16f, 1f, 0.24f, 1f);
    public Color corAmarela = new Color(1f, 0.78f, 0.04f, 1f);
    public Color corVermelha = new Color(1f, 0.12f, 0.05f, 1f);

    [Tooltip("Largura da transição suave ao redor de 25% e 61%.")]
    [Range(0.01f, 0.20f)] public float larguraTransicao = 0.07f;

    [Header("Pulsação do raio ao correr")]
    public bool pulsarAoCorrer = true;
    public bool detectarShiftComoFallback = true;
    [Range(0.01f, 0.40f)] public float intensidadePulsacao = 0.14f;
    [Min(20f)] public float batimentosPorMinuto = 118f;
    [Range(0f, 1f)] public float segundoBatimento = 0.55f;
    [Min(1f)] public float retornoEscala = 12f;

    [Header("Atualização")]
    [Min(0.1f)] public float intervaloBusca = 0.5f;

    private MiniMarketPlayerDatabase database;
    private Vector3 escalaOriginal = Vector3.one;
    private Image iconeRegistrado;
    private float fasePulso;
    private float proximaBusca;
    private int ultimoPercentual = -1;
    private int ultimoEstado = -1;

    private void Awake()
    {
        ResolverReferencias(true);
        RegistrarEscalaOriginal();
        AplicarVisual(true);
    }

    private void OnEnable()
    {
        ResolverReferencias(true);
        RegistrarEscalaOriginal();
        AplicarVisual(true);
    }

    private void OnDisable()
    {
        RestaurarEscalaImediatamente();
    }

    private void LateUpdate()
    {
        if (Time.unscaledTime >= proximaBusca &&
            (movimento == null || textoPorcentagem == null || iconeRaio == null || preenchimento == null))
        {
            ResolverReferencias(false);
            RegistrarEscalaOriginal();
        }

        AplicarVisual(false);
        AtualizarPulsacao();
    }

    [ContextMenu("Energia Visual/Rebuscar referências")]
    public void RebuscarReferencias()
    {
        ResolverReferencias(true);
        RegistrarEscalaOriginal();
        ultimoPercentual = -1;
        ultimoEstado = -1;
        AplicarVisual(true);
    }

    private void ResolverReferencias(bool forcar)
    {
        if (progressBar == null)
            progressBar = GetComponent<MiniMarketEnergyProgressBar>();
        if (progressBar == null)
            progressBar = GetComponentInParent<MiniMarketEnergyProgressBar>();

        if ((forcar || movimento == null) && progressBar != null)
            movimento = progressBar.movimento;

        if (forcar || movimento == null)
        {
            movimento = Object.FindAnyObjectByType<CameraRelativeMovement>(
                FindObjectsInactive.Include
            );
        }

        if (database == null)
            database = MiniMarketPlayerDatabase.Instance;

        if (progressBar != null)
        {
            if (textoPorcentagem == null)
                textoPorcentagem = progressBar.textoQuantidade;
            if (iconeRaio == null)
                iconeRaio = progressBar.iconeEnergia;
            if (preenchimento == null)
                preenchimento = progressBar.preenchimentoVerde;

            if (raioVerde == null)
                raioVerde = progressBar.energiaVerdeSprite;
            if (raioAmarelo == null)
                raioAmarelo = progressBar.energiaAmarelaSprite;
            if (raioVermelho == null)
                raioVermelho = progressBar.energiaVermelhaSprite;
        }

        if (iconeRaio == null)
            iconeRaio = EncontrarRaioNaHierarquia();

        if (textoPorcentagem == null)
            textoPorcentagem = EncontrarTextoNaHierarquia();

        proximaBusca = Time.unscaledTime + Mathf.Max(0.1f, intervaloBusca);
    }

    private void AplicarVisual(bool forcar)
    {
        float energia01 = ObterEnergiaTotal01();
        int percentual = Mathf.Clamp(Mathf.RoundToInt(energia01 * 100f), 0, 100);

        if (textoPorcentagem != null && (forcar || percentual != ultimoPercentual || !textoPorcentagem.text.EndsWith("%")))
        {
            textoPorcentagem.text = percentual + "%";
            ultimoPercentual = percentual;
        }

        int estado = energia01 >= inicioVerde ? 2 : energia01 <= inicioVermelho ? 0 : 1;
        if (forcar || estado != ultimoEstado)
        {
            ultimoEstado = estado;
            AplicarSpriteDoRaio(estado);
        }

        if (preenchimento != null)
            preenchimento.color = CalcularCorDegrade(energia01);
    }

    private float ObterEnergiaTotal01()
    {
        int atual;
        int maximo;
        float segmento01;
        bool correndo;

        if (movimento != null)
        {
            atual = movimento.StaminaSegmentosAtuais;
            maximo = movimento.StaminaSegmentosMaximos;
            segmento01 = movimento.StaminaPercentual01;
            correndo = movimento.EstaCorrendo;
        }
        else
        {
            if (database == null)
                database = MiniMarketPlayerDatabase.Instance;

            if (database == null)
                return 1f;

            atual = database.EnergiaSegmentosAtuais;
            maximo = database.EnergiaSegmentosMaximos;
            segmento01 = database.ObterPercentualStamina01();
            correndo = false;
        }

        maximo = Mathf.Max(1, maximo);
        atual = Mathf.Clamp(atual, 0, maximo);
        segmento01 = Mathf.Clamp01(segmento01);

        if (atual > 0 && segmento01 <= 0.001f && !correndo)
            segmento01 = 1f;

        float unidades = atual <= 0 ? 0f : Mathf.Max(0, atual - 1) + segmento01;
        return Mathf.Clamp01(unidades / maximo);
    }

    private Color CalcularCorDegrade(float energia01)
    {
        float largura = Mathf.Max(0.01f, larguraTransicao);

        if (energia01 <= inicioVermelho - largura)
            return corVermelha;

        if (energia01 < inicioVermelho + largura)
        {
            float t = Mathf.InverseLerp(
                inicioVermelho - largura,
                inicioVermelho + largura,
                energia01
            );
            return Color.Lerp(corVermelha, corAmarela, Suavizar(t));
        }

        if (energia01 <= inicioVerde - largura)
            return corAmarela;

        if (energia01 < inicioVerde + largura)
        {
            float t = Mathf.InverseLerp(
                inicioVerde - largura,
                inicioVerde + largura,
                energia01
            );
            return Color.Lerp(corAmarela, corVerde, Suavizar(t));
        }

        return corVerde;
    }

    private static float Suavizar(float t)
    {
        t = Mathf.Clamp01(t);
        return t * t * (3f - 2f * t);
    }

    private void AplicarSpriteDoRaio(int estado)
    {
        if (iconeRaio == null)
            return;

        Sprite desejado = estado == 2
            ? raioVerde
            : estado == 1 ? raioAmarelo : raioVermelho;

        if (desejado != null)
            iconeRaio.sprite = desejado;

        iconeRaio.color = Color.white;
        iconeRaio.preserveAspect = true;
    }

    private void AtualizarPulsacao()
    {
        if (iconeRaio == null)
            return;

        RegistrarEscalaOriginal();

        bool correndo = movimento != null && movimento.EstaCorrendo;
        bool shift = detectarShiftComoFallback &&
                     (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift));
        bool ativo = pulsarAoCorrer && (correndo || shift);

        if (!ativo)
        {
            fasePulso = 0f;
            iconeRaio.rectTransform.localScale = Vector3.Lerp(
                iconeRaio.rectTransform.localScale,
                escalaOriginal,
                1f - Mathf.Exp(-Mathf.Max(1f, retornoEscala) * Time.unscaledDeltaTime)
            );
            return;
        }

        float ciclosPorSegundo = Mathf.Max(20f, batimentosPorMinuto) / 60f;
        fasePulso = Mathf.Repeat(fasePulso + Time.unscaledDeltaTime * ciclosPorSegundo, 1f);

        float batida1 = PulsoCurto(fasePulso, 0.08f, 0.12f);
        float batida2 = PulsoCurto(fasePulso, 0.30f, 0.10f) * Mathf.Clamp01(segundoBatimento);
        float pulso = Mathf.Clamp01(batida1 + batida2);
        float escala = 1f + pulso * Mathf.Max(0f, intensidadePulsacao);

        iconeRaio.rectTransform.localScale = Vector3.Scale(
            escalaOriginal,
            new Vector3(escala, escala, 1f)
        );
    }

    private static float PulsoCurto(float fase, float centro, float largura)
    {
        float distancia = Mathf.Abs(Mathf.DeltaAngle(fase * 360f, centro * 360f)) / 360f;
        float normalizado = 1f - Mathf.Clamp01(distancia / Mathf.Max(0.01f, largura));
        return normalizado * normalizado * (3f - 2f * normalizado);
    }

    private void RegistrarEscalaOriginal()
    {
        if (iconeRaio == null || iconeRegistrado == iconeRaio)
            return;

        iconeRegistrado = iconeRaio;
        escalaOriginal = iconeRaio.rectTransform.localScale;
        if (escalaOriginal.sqrMagnitude <= 0.0001f)
            escalaOriginal = Vector3.one;
    }

    private void RestaurarEscalaImediatamente()
    {
        if (iconeRaio != null)
            iconeRaio.rectTransform.localScale = escalaOriginal;
    }

    private Image EncontrarRaioNaHierarquia()
    {
        Transform raiz = progressBar != null && progressBar.transform.parent != null
            ? progressBar.transform.parent
            : transform.parent;

        if (raiz == null)
            return null;

        Image[] imagens = raiz.GetComponentsInChildren<Image>(true);
        Image fallback = null;

        for (int i = 0; i < imagens.Length; i++)
        {
            Image imagem = imagens[i];
            if (imagem == null || imagem == preenchimento)
                continue;

            string nome = Normalizar(imagem.name);
            string spriteNome = imagem.sprite != null ? Normalizar(imagem.sprite.name) : string.Empty;

            if (nome == "image" || nome.Contains("raio") || nome.Contains("energyicon") || nome.Contains("iconeenergia"))
                return imagem;

            if (fallback == null && spriteNome.Contains("energy"))
                fallback = imagem;
        }

        return fallback;
    }

    private Text EncontrarTextoNaHierarquia()
    {
        Transform raiz = progressBar != null && progressBar.transform.parent != null
            ? progressBar.transform.parent
            : transform.parent;

        if (raiz == null)
            return null;

        Text[] textos = raiz.GetComponentsInChildren<Text>(true);
        for (int i = 0; i < textos.Length; i++)
        {
            Text texto = textos[i];
            if (texto == null)
                continue;

            string nome = Normalizar(texto.name);
            if (nome == "txtqtd" || nome.Contains("percent") || nome.Contains("porcent"))
                return texto;
        }

        return null;
    }

    private static string Normalizar(string valor)
    {
        return string.IsNullOrWhiteSpace(valor)
            ? string.Empty
            : valor.Trim().ToLowerInvariant()
                .Replace("_", string.Empty)
                .Replace("-", string.Empty)
                .Replace(" ", string.Empty)
                .Replace("á", "a")
                .Replace("ã", "a")
                .Replace("ç", "c");
    }

    private void OnValidate()
    {
        inicioVerde = Mathf.Clamp01(inicioVerde);
        inicioVermelho = Mathf.Clamp(inicioVermelho, 0f, inicioVerde);
        larguraTransicao = Mathf.Clamp(larguraTransicao, 0.01f, 0.20f);
        intensidadePulsacao = Mathf.Clamp(intensidadePulsacao, 0.01f, 0.40f);
        batimentosPorMinuto = Mathf.Max(20f, batimentosPorMinuto);
        retornoEscala = Mathf.Max(1f, retornoEscala);
        intervaloBusca = Mathf.Max(0.1f, intervaloBusca);

        if (!Application.isPlaying)
        {
            ResolverReferencias(false);
            if (preenchimento != null)
                preenchimento.color = corVerde;
            if (iconeRaio != null && raioVerde != null)
            {
                iconeRaio.sprite = raioVerde;
                iconeRaio.color = Color.white;
            }
            if (textoPorcentagem != null)
                textoPorcentagem.text = "100%";
        }
    }
}
