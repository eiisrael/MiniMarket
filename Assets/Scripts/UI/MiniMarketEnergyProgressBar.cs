using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>
/// Controla a barra interna de Canvas/StaminaHUD/Energy.
/// O artwork original permanece estático. O preenchimento, o texto percentual e o ícone
/// mudam de estado conforme a energia total segmentada.
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(25500)]
public sealed class MiniMarketEnergyProgressBar : MonoBehaviour
{
    private const string FillAreaName = "EnergyProgressArea";
    private const string FillName = "EnergyProgressFill";

    [Header("Referências")]
    [Tooltip("Imagem original do objeto Energy. Ela não será usada como progress bar.")]
    public Image imagemOriginal;

    [Tooltip("Área interna onde a barra será desenhada.")]
    public RectTransform areaPreenchimento;

    [Tooltip("Imagem interna que aumenta e diminui.")]
    public Image preenchimentoVerde;

    [Tooltip("Texto que mostrava 5/5. Agora mostra a porcentagem total da energia.")]
    public Text textoQuantidade;

    [Tooltip("Imagem do ícone de energia que alterna entre verde, amarelo e vermelho.")]
    public Image iconeEnergia;

    public CameraRelativeMovement movimento;

    [Header("Sprites do ícone de energia")]
    public Sprite energiaVerdeSprite;
    public Sprite energiaAmarelaSprite;
    public Sprite energiaVermelhaSprite;

    [Header("Faixas de energia")]
    [Tooltip("Verde entre 100% e 61%.")]
    [Range(0f, 1f)] public float limiteVerde = 0.61f;

    [Tooltip("Vermelho entre 25% e 0%. Entre os dois limites será amarelo.")]
    [Range(0f, 1f)] public float limiteVermelho = 0.25f;

    [Header("Cores do progress bar")]
    public Color corAlta = new Color(0.18f, 0.95f, 0.22f, 1f);
    public Color corMedia = new Color(1f, 0.78f, 0.08f, 1f);
    public Color corBaixa = new Color(1f, 0.18f, 0.08f, 1f);

    [Tooltip("Compatibilidade com cenas antigas. Representa a cor alta/verde.")]
    public Color corBarra = new Color(0.18f, 0.95f, 0.22f, 1f);

    [Header("Área da barra dentro de Energy")]
    [Tooltip("Posição normalizada do canto inferior esquerdo da barra dentro de Energy.")]
    public Vector2 ancoraMinima = new Vector2(0.22f, 0.36f);

    [Tooltip("Posição normalizada do canto superior direito da barra dentro de Energy.")]
    public Vector2 ancoraMaxima = new Vector2(0.93f, 0.64f);

    [Header("Visual")]
    [Tooltip("Quando existe Background_Ene separado, oculta somente a Image original de Energy para não duplicar a barra.")]
    public bool ocultarImagemOriginalComFundoSeparado = true;

    [Header("Texto")]
    public bool mostrarPorcentagem = true;
    public string formatoPorcentagem = "{0}%";

    [Tooltip("Compatibilidade antiga. Deve permanecer desligado quando Mostrar Porcentagem estiver ligado.")]
    public bool manterTextoSegmentado;

    [Header("Animação")]
    public bool animar = true;
    [Min(0.1f)] public float velocidade = 12f;
    [Min(0.0001f)] public float tolerancia = 0.001f;

    [Header("Comportamento")]
    [Tooltip("A barra representa a energia total dos segmentos.")]
    public bool usarEnergiaTotalSegmentada = true;

    [Tooltip("Corrige somente o visual quando o save informa segmentos disponíveis e stamina ativa zerada fora da corrida.")]
    public bool corrigirEstadoInconsistente = true;

    [Min(0.1f)] public float intervaloBusca = 0.5f;

    private MiniMarketPlayerDatabase database;
    private MiniMarketEnergySegmentHUD hudLegado;
    private CameraRelativeMovement movimentoInscrito;
    private MiniMarketPlayerDatabase databaseInscrito;
    private float valorAlvo = 1f;
    private float valorVisual = 1f;
    private float proximaBusca;
    private int ultimoAtual = -1;
    private int ultimoMaximo = -1;
    private float ultimoAlvo = -1f;
    private int ultimoPercentualExibido = -1;
    private int ultimoEstadoVisual = -1;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void InstalarAutomaticamente()
    {
        MiniMarketEnergyProgressBar existente =
            Object.FindAnyObjectByType<MiniMarketEnergyProgressBar>(FindObjectsInactive.Include);

        if (existente != null)
        {
            existente.gameObject.SetActive(true);
            existente.enabled = true;
            existente.RebuscarTudo();
            return;
        }

        Image energy = EncontrarImagemEnergy();
        if (energy == null)
        {
            Debug.LogWarning(
                "[EnergyProgressBar] Canvas/StaminaHUD/Energy não foi encontrado. " +
                "Execute Tools > MiniMarket > Criar ou Reparar Barra de Energia."
            );
            return;
        }

        MiniMarketEnergyProgressBar controlador =
            energy.GetComponent<MiniMarketEnergyProgressBar>();

        if (controlador == null)
            controlador = energy.gameObject.AddComponent<MiniMarketEnergyProgressBar>();

        controlador.imagemOriginal = energy;
        controlador.RebuscarTudo();
    }

    private static Image EncontrarImagemEnergy()
    {
        Image[] imagens = Object.FindObjectsByType<Image>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None
        );

        Image melhor = null;
        int melhorPontuacao = int.MinValue;

        for (int i = 0; i < imagens.Length; i++)
        {
            Image imagem = imagens[i];
            if (imagem == null || imagem.GetComponentInParent<Canvas>() == null)
                continue;

            string nome = Normalizar(imagem.name);
            if (nome != "energy" && nome != "energia")
                continue;

            int pontuacao = nome == "energy" ? 1000 : 700;
            Transform pai = imagem.transform.parent;

            while (pai != null)
            {
                string nomePai = Normalizar(pai.name);
                if (nomePai == "staminahud")
                {
                    pontuacao += 1000;
                    break;
                }

                if (nomePai.Contains("stamina"))
                    pontuacao += 100;

                if (pai.GetComponent<Canvas>() != null)
                    break;

                pai = pai.parent;
            }

            if (pontuacao > melhorPontuacao)
            {
                melhorPontuacao = pontuacao;
                melhor = imagem;
            }
        }

        return melhor;
    }

    private void Awake()
    {
        ResolverReferencias(true);
        GarantirEstruturaVisual();
        TransferirAutoridadeDoHudAntigo();
        AtualizarAlvo(true);
    }

    private void OnEnable()
    {
        ResolverReferencias(true);
        GarantirEstruturaVisual();
        TransferirAutoridadeDoHudAntigo();
        InscreverEventos();
        AtualizarAlvo(true);
    }

    private void OnDisable()
    {
        DesinscreverEventos();
    }

    private void Update()
    {
        if (Time.unscaledTime >= proximaBusca &&
            (movimento == null || imagemOriginal == null || database == null || iconeEnergia == null))
        {
            ResolverReferencias(false);
            GarantirEstruturaVisual();
            TransferirAutoridadeDoHudAntigo();
            InscreverEventos();
        }

        AtualizarAlvo(false);
        AnimarPreenchimento();
    }

    [ContextMenu("Energia/Rebuscar tudo")]
    public void RebuscarTudo()
    {
        DesinscreverEventos();
        ResolverReferencias(true);
        GarantirEstruturaVisual();
        TransferirAutoridadeDoHudAntigo();
        InscreverEventos();

        ultimoAtual = -1;
        ultimoMaximo = -1;
        ultimoAlvo = -1f;
        ultimoPercentualExibido = -1;
        ultimoEstadoVisual = -1;
        AtualizarAlvo(true);
    }

    [ContextMenu("Energia/Recriar barra interna")]
    public void RecriarBarraInterna()
    {
        if (areaPreenchimento != null)
        {
            GameObject alvo = areaPreenchimento.gameObject;
            areaPreenchimento = null;
            preenchimentoVerde = null;

            if (Application.isPlaying)
                Destroy(alvo);
            else
                DestroyImmediate(alvo);
        }

        GarantirEstruturaVisual();
        AtualizarAlvo(true);
    }

    private void ResolverReferencias(bool forcar)
    {
        if (imagemOriginal == null)
            imagemOriginal = GetComponent<Image>();

        if (imagemOriginal == null)
            imagemOriginal = EncontrarImagemEnergy();

        if (forcar || movimento == null)
        {
            movimento = Object.FindAnyObjectByType<CameraRelativeMovement>(
                FindObjectsInactive.Include
            );
        }

        if (forcar || database == null)
        {
            database = MiniMarketPlayerDatabase.Instance;
            if (database == null && Application.isPlaying)
                database = MiniMarketPlayerDatabase.ObterOuCriar();
        }

        if (textoQuantidade == null && imagemOriginal != null)
            textoQuantidade = EncontrarTextoQuantidade(imagemOriginal.transform.parent);

        if (iconeEnergia == null && imagemOriginal != null)
            iconeEnergia = EncontrarIconeEnergia(imagemOriginal.transform.parent, imagemOriginal);

        if (hudLegado == null && imagemOriginal != null)
        {
            hudLegado = imagemOriginal.GetComponentInParent<MiniMarketEnergySegmentHUD>();
            if (hudLegado == null && imagemOriginal.transform.parent != null)
            {
                hudLegado = imagemOriginal.transform.parent
                    .GetComponentInChildren<MiniMarketEnergySegmentHUD>(true);
            }
        }

        proximaBusca = Time.unscaledTime + Mathf.Max(0.1f, intervaloBusca);
    }

    private void GarantirEstruturaVisual()
    {
        if (imagemOriginal == null)
            return;

        imagemOriginal.raycastTarget = false;
        imagemOriginal.type = Image.Type.Simple;
        imagemOriginal.fillAmount = 1f;

        bool fundoSeparado = TemFundoSeparado();
        imagemOriginal.enabled = !(ocultarImagemOriginalComFundoSeparado && fundoSeparado);

        Transform areaExistente = imagemOriginal.transform.Find(FillAreaName);
        if (areaPreenchimento == null && areaExistente != null)
            areaPreenchimento = areaExistente as RectTransform;

        if (areaPreenchimento == null)
        {
            GameObject area = new GameObject(FillAreaName, typeof(RectTransform));
            area.transform.SetParent(imagemOriginal.transform, false);
            areaPreenchimento = area.GetComponent<RectTransform>();
        }

        areaPreenchimento.anchorMin = ancoraMinima;
        areaPreenchimento.anchorMax = ancoraMaxima;
        areaPreenchimento.pivot = new Vector2(0f, 0.5f);
        areaPreenchimento.offsetMin = Vector2.zero;
        areaPreenchimento.offsetMax = Vector2.zero;
        areaPreenchimento.localScale = Vector3.one;

        Transform fillExistente = areaPreenchimento.Find(FillName);
        if (preenchimentoVerde == null && fillExistente != null)
            preenchimentoVerde = fillExistente.GetComponent<Image>();

        if (preenchimentoVerde == null)
        {
            GameObject fill = new GameObject(
                FillName,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image)
            );
            fill.transform.SetParent(areaPreenchimento, false);
            preenchimentoVerde = fill.GetComponent<Image>();
        }

        preenchimentoVerde.sprite = null;
        preenchimentoVerde.type = Image.Type.Simple;
        preenchimentoVerde.raycastTarget = false;
        preenchimentoVerde.preserveAspect = false;

        AplicarValorVisual(valorVisual, true);
    }

    private bool TemFundoSeparado()
    {
        if (imagemOriginal == null || imagemOriginal.transform.parent == null)
            return false;

        Transform pai = imagemOriginal.transform.parent;
        for (int i = 0; i < pai.childCount; i++)
        {
            Transform filho = pai.GetChild(i);
            if (filho == null || filho == imagemOriginal.transform)
                continue;

            string nome = Normalizar(filho.name);
            if (nome.Contains("backgroundene") ||
                nome.Contains("backgroundenergy") ||
                nome.Contains("fundoene") ||
                nome.Contains("fundoenergia"))
            {
                return filho.GetComponent<Image>() != null;
            }
        }

        return false;
    }

    private void TransferirAutoridadeDoHudAntigo()
    {
        if (hudLegado == null)
            return;

        hudLegado.autoDetectarBarras = false;
        hudLegado.criarBarrasSegmentadasQuandoAusentes = false;

        if (hudLegado.barraEnergia == imagemOriginal ||
            hudLegado.barraEnergia == preenchimentoVerde)
        {
            hudLegado.barraEnergia = null;
        }

        if (textoQuantidade != null)
            hudLegado.textoEnergia = null;
    }

    private static Text EncontrarTextoQuantidade(Transform raiz)
    {
        if (raiz == null)
            return null;

        Text[] textos = raiz.GetComponentsInChildren<Text>(true);
        Text fallback = null;

        for (int i = 0; i < textos.Length; i++)
        {
            Text texto = textos[i];
            if (texto == null)
                continue;

            string nome = Normalizar(texto.name);
            if (nome == "txtqtd" || nome == "quantidade" ||
                nome == "energytext" || nome == "staminatext" ||
                nome == "percentual" || nome == "porcentagem")
            {
                return texto;
            }

            if (fallback == null && (PareceContador(texto.text) || ParecePorcentagem(texto.text)))
                fallback = texto;
        }

        return fallback;
    }

    private static Image EncontrarIconeEnergia(Transform raiz, Image energyPrincipal)
    {
        if (raiz == null)
            return null;

        Image[] imagens = raiz.GetComponentsInChildren<Image>(true);
        Image melhor = null;
        int melhorPontuacao = int.MinValue;

        for (int i = 0; i < imagens.Length; i++)
        {
            Image imagem = imagens[i];
            if (imagem == null || imagem == energyPrincipal)
                continue;
            if (imagem.transform.IsChildOf(energyPrincipal.transform))
                continue;

            string nome = Normalizar(imagem.name);
            if (nome.Contains("background") || nome.Contains("fundo") ||
                nome.Contains("progress") || nome.Contains("fill"))
                continue;

            int pontuacao = 0;
            if (nome.Contains("iconeenergia") || nome.Contains("energyicon")) pontuacao += 1000;
            if (nome.Contains("icone") || nome.Contains("icon")) pontuacao += 500;
            if (nome == "image") pontuacao += 350;
            if (imagem.sprite != null && Normalizar(imagem.sprite.name).Contains("energy")) pontuacao += 700;

            if (pontuacao > melhorPontuacao)
            {
                melhorPontuacao = pontuacao;
                melhor = imagem;
            }
        }

        return melhorPontuacao > 0 ? melhor : null;
    }

    private void AtualizarAlvo(bool forcar)
    {
        int atual;
        int maximo;
        float stamina01;
        bool correndo;

        if (movimento != null)
        {
            atual = movimento.StaminaSegmentosAtuais;
            maximo = movimento.StaminaSegmentosMaximos;
            stamina01 = movimento.StaminaPercentual01;
            correndo = movimento.EstaCorrendo;
        }
        else if (database != null)
        {
            atual = database.EnergiaSegmentosAtuais;
            maximo = database.EnergiaSegmentosMaximos;
            stamina01 = database.ObterPercentualStamina01();
            correndo = false;
        }
        else
        {
            atual = 5;
            maximo = 5;
            stamina01 = 1f;
            correndo = false;
        }

        maximo = Mathf.Max(1, maximo);
        atual = Mathf.Clamp(atual, 0, maximo);
        stamina01 = Mathf.Clamp01(stamina01);

        if (corrigirEstadoInconsistente && atual > 0 && stamina01 <= 0.001f && !correndo)
            stamina01 = 1f;

        float novoAlvo;
        if (!usarEnergiaTotalSegmentada)
        {
            novoAlvo = stamina01;
        }
        else
        {
            float unidades = atual <= 0
                ? 0f
                : Mathf.Max(0, atual - 1) + stamina01;
            novoAlvo = Mathf.Clamp01(unidades / maximo);
        }

        bool mudou = forcar ||
                     atual != ultimoAtual ||
                     maximo != ultimoMaximo ||
                     Mathf.Abs(novoAlvo - ultimoAlvo) > 0.0005f;

        if (!mudou)
            return;

        ultimoAtual = atual;
        ultimoMaximo = maximo;
        ultimoAlvo = novoAlvo;
        valorAlvo = novoAlvo;

        if (forcar)
            valorVisual = valorAlvo;

        if (manterTextoSegmentado && !mostrarPorcentagem && textoQuantidade != null)
            textoQuantidade.text = atual + "/" + maximo;

        if (forcar)
            AplicarValorVisual(valorVisual, true);
    }

    private void AnimarPreenchimento()
    {
        if (preenchimentoVerde == null)
            return;

        if (!animar)
        {
            valorVisual = valorAlvo;
        }
        else
        {
            float t = 1f - Mathf.Exp(
                -Mathf.Max(0.1f, velocidade) * Time.unscaledDeltaTime
            );

            valorVisual = Mathf.Lerp(valorVisual, valorAlvo, t);
            if (Mathf.Abs(valorVisual - valorAlvo) <= tolerancia)
                valorVisual = valorAlvo;
        }

        AplicarValorVisual(valorVisual, false);
    }

    private void AplicarValorVisual(float valor, bool forcarEstado)
    {
        if (preenchimentoVerde == null)
            return;

        float normalizado = Mathf.Clamp01(valor);
        RectTransform rect = preenchimentoVerde.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = new Vector2(normalizado, 1f);
        rect.pivot = new Vector2(0f, 0.5f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        preenchimentoVerde.enabled = normalizado > 0.0001f;

        AtualizarTextoPercentual(normalizado);
        AtualizarEstadoVisual(normalizado, forcarEstado);
    }

    private void AtualizarTextoPercentual(float normalizado)
    {
        if (!mostrarPorcentagem || textoQuantidade == null)
            return;

        int percentual = Mathf.Clamp(Mathf.RoundToInt(normalizado * 100f), 0, 100);
        if (percentual == ultimoPercentualExibido)
            return;

        ultimoPercentualExibido = percentual;
        textoQuantidade.text = string.IsNullOrWhiteSpace(formatoPorcentagem)
            ? percentual + "%"
            : string.Format(formatoPorcentagem, percentual);
    }

    private void AtualizarEstadoVisual(float normalizado, bool forcar)
    {
        int estado = normalizado >= limiteVerde ? 2 : normalizado <= limiteVermelho ? 0 : 1;
        if (!forcar && estado == ultimoEstadoVisual)
            return;

        ultimoEstadoVisual = estado;

        Color cor;
        Sprite sprite;
        if (estado == 2)
        {
            cor = corAlta;
            sprite = energiaVerdeSprite;
        }
        else if (estado == 1)
        {
            cor = corMedia;
            sprite = energiaAmarelaSprite;
        }
        else
        {
            cor = corBaixa;
            sprite = energiaVermelhaSprite;
        }

        preenchimentoVerde.color = cor;

        if (iconeEnergia != null && sprite != null)
        {
            iconeEnergia.sprite = sprite;
            iconeEnergia.color = Color.white;
            iconeEnergia.preserveAspect = true;
        }
    }

    private void InscreverEventos()
    {
        if (movimentoInscrito != movimento)
        {
            if (movimentoInscrito != null)
                movimentoInscrito.OnStaminaChanged -= AoAlterarStamina;

            movimentoInscrito = movimento;
            if (movimentoInscrito != null)
                movimentoInscrito.OnStaminaChanged += AoAlterarStamina;
        }

        if (databaseInscrito != database)
        {
            if (databaseInscrito != null)
                databaseInscrito.OnDatabaseChanged -= AoAlterarBanco;

            databaseInscrito = database;
            if (databaseInscrito != null)
                databaseInscrito.OnDatabaseChanged += AoAlterarBanco;
        }
    }

    private void DesinscreverEventos()
    {
        if (movimentoInscrito != null)
            movimentoInscrito.OnStaminaChanged -= AoAlterarStamina;

        if (databaseInscrito != null)
            databaseInscrito.OnDatabaseChanged -= AoAlterarBanco;

        movimentoInscrito = null;
        databaseInscrito = null;
    }

    private void AoAlterarStamina()
    {
        AtualizarAlvo(false);
    }

    private void AoAlterarBanco(MiniMarketPlayerDatabase.MiniMarketPlayerData dados)
    {
        AtualizarAlvo(false);
    }

    private static bool PareceContador(string valor)
    {
        if (string.IsNullOrWhiteSpace(valor))
            return false;

        int indice = valor.IndexOf('/');
        return indice > 0 &&
               indice < valor.Length - 1 &&
               char.IsDigit(valor[indice - 1]) &&
               char.IsDigit(valor[indice + 1]);
    }

    private static bool ParecePorcentagem(string valor)
    {
        return !string.IsNullOrWhiteSpace(valor) && valor.Contains("%");
    }

    private static string Normalizar(string valor)
    {
        if (string.IsNullOrWhiteSpace(valor))
            return string.Empty;

        return valor.Trim()
            .ToLowerInvariant()
            .Replace("_", string.Empty)
            .Replace("-", string.Empty)
            .Replace(" ", string.Empty)
            .Replace("á", "a")
            .Replace("ã", "a")
            .Replace("â", "a")
            .Replace("é", "e")
            .Replace("ê", "e")
            .Replace("í", "i")
            .Replace("ó", "o")
            .Replace("ô", "o")
            .Replace("õ", "o")
            .Replace("ú", "u")
            .Replace("ç", "c");
    }

    private void OnValidate()
    {
        ancoraMinima.x = Mathf.Clamp01(ancoraMinima.x);
        ancoraMinima.y = Mathf.Clamp01(ancoraMinima.y);
        ancoraMaxima.x = Mathf.Clamp(ancoraMaxima.x, ancoraMinima.x, 1f);
        ancoraMaxima.y = Mathf.Clamp(ancoraMaxima.y, ancoraMinima.y, 1f);
        limiteVerde = Mathf.Clamp01(limiteVerde);
        limiteVermelho = Mathf.Clamp(limiteVermelho, 0f, limiteVerde);
        velocidade = Mathf.Max(0.1f, velocidade);
        tolerancia = Mathf.Max(0.0001f, tolerancia);
        intervaloBusca = Mathf.Max(0.1f, intervaloBusca);

        corBarra = corAlta;

        if (!Application.isPlaying && preenchimentoVerde != null)
        {
            preenchimentoVerde.color = corAlta;
            if (iconeEnergia != null && energiaVerdeSprite != null)
                iconeEnergia.sprite = energiaVerdeSprite;
        }
    }
}
