using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>
/// Controla a barra verde interna de Canvas/StaminaHUD/Energy.
/// O artwork original do objeto Energy permanece estático; somente o preenchimento
/// interno aumenta e diminui conforme a energia segmentada.
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

    [Tooltip("Área interna onde a barra verde será desenhada.")]
    public RectTransform areaPreenchimento;

    [Tooltip("Imagem verde criada dentro da área de preenchimento.")]
    public Image preenchimentoVerde;

    public Text textoQuantidade;
    public CameraRelativeMovement movimento;

    [Header("Área da barra dentro de Energy")]
    [Tooltip("Posição normalizada do canto inferior esquerdo da barra dentro de Energy.")]
    public Vector2 ancoraMinima = new Vector2(0.22f, 0.36f);

    [Tooltip("Posição normalizada do canto superior direito da barra dentro de Energy.")]
    public Vector2 ancoraMaxima = new Vector2(0.93f, 0.64f);

    [Header("Visual")]
    public Color corBarra = new Color(0.18f, 0.95f, 0.22f, 1f);

    [Tooltip("Quando existe Background_Ene separado, oculta somente a Image original de Energy para não duplicar a barra.")]
    public bool ocultarImagemOriginalComFundoSeparado = true;

    [Header("Animação")]
    public bool animar = true;
    [Min(0.1f)] public float velocidade = 12f;
    [Min(0.0001f)] public float tolerancia = 0.001f;

    [Header("Comportamento")]
    [Tooltip("A barra representa a energia total dos segmentos, respeitando 5/5, 4/5 etc.")]
    public bool usarEnergiaTotalSegmentada = true;

    [Tooltip("Corrige somente o visual quando o save informa segmentos disponíveis e stamina ativa zerada fora da corrida.")]
    public bool corrigirEstadoInconsistente = true;

    public bool manterTextoSegmentado = true;
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
            (movimento == null || imagemOriginal == null || database == null))
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
        AtualizarAlvo(true);
    }

    [ContextMenu("Energia/Recriar barra verde interna")]
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
        preenchimentoVerde.color = corBarra;
        preenchimentoVerde.raycastTarget = false;
        preenchimentoVerde.preserveAspect = false;

        AplicarValorVisual(valorVisual);
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
            hudLegado.textoEnergia = textoQuantidade;
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
                nome == "energytext" || nome == "staminatext")
            {
                return texto;
            }

            if (fallback == null && PareceContador(texto.text))
                fallback = texto;
        }

        return fallback;
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

        if (manterTextoSegmentado && textoQuantidade != null)
            textoQuantidade.text = atual + "/" + maximo;

        if (forcar)
            AplicarValorVisual(valorVisual);
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

        AplicarValorVisual(valorVisual);
    }

    private void AplicarValorVisual(float valor)
    {
        if (preenchimentoVerde == null)
            return;

        RectTransform rect = preenchimentoVerde.rectTransform;
        float normalizado = Mathf.Clamp01(valor);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = new Vector2(normalizado, 1f);
        rect.pivot = new Vector2(0f, 0.5f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        preenchimentoVerde.color = corBarra;
        preenchimentoVerde.enabled = normalizado > 0.0001f;
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
        velocidade = Mathf.Max(0.1f, velocidade);
        tolerancia = Mathf.Max(0.0001f, tolerancia);
        intervaloBusca = Mathf.Max(0.1f, intervaloBusca);
    }
}
