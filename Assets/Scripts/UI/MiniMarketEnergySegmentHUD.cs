using System;
using System.Collections.Generic;
using Object = UnityEngine.Object;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// HUD de stamina/energia segmentada.
///
/// Atualiza texto, barra principal e barras individuais. A busca automática parte do
/// container visual mais próximo, portanto funciona mesmo quando o componente está no
/// Text e as imagens são irmãs no Canvas.
/// </summary>
[DisallowMultipleComponent]
public class MiniMarketEnergySegmentHUD : MonoBehaviour
{
    [Header("Referências")]
    public Text textoEnergia;
    public Image barraEnergia;
    public Image iconeEnergia;
    public Image[] barrasSegmentos;
    public CameraRelativeMovement movimento;

    [Header("Detecção automática")]
    public bool autoDetectarBarras = true;
    public bool preencherBarraPrincipalComEnergiaTotal = true;
    public bool inverterOrdemSegmentos;

    [Header("Sprites opcionais")]
    public Sprite energiaAltaSprite;
    public Sprite energiaMediaSprite;
    public Sprite energiaBaixaSprite;

    [Header("Cores")]
    public Color corAlta = new Color(0.15f, 0.9f, 0.25f, 1f);
    public Color corMedia = new Color(1f, 0.78f, 0.08f, 1f);
    public Color corBaixa = new Color(1f, 0.18f, 0.08f, 1f);
    [Range(0f, 1f)] public float limiteMedio = 0.55f;
    [Range(0f, 1f)] public float limiteBaixo = 0.25f;

    [Header("Texto")]
    [Min(1)] public int barrasMaximasFallback = 5;
    public string formatoTexto = "{0}/{1}";
    public bool mostrarPercentualDaBarra;
    public string formatoComPercentual = "{0}/{1}  {2}%";

    [Header("Animação visual")]
    public bool animarPreenchimento = true;
    [Min(0.1f)] public float velocidadePreenchimento = 12f;

    [Header("Atualização")]
    [Min(0.25f)] public float intervaloBuscaReferencias = 1f;
    [Min(0.1f)] public float intervaloVerificacaoSeguranca = 0.25f;
    public bool atualizarMesmoDesativado;

    [Header("Debug")]
    public bool logarSeNaoEncontrarPlayer;

    private MiniMarketPlayerDatabase database;
    private float proximaBusca;
    private float proximaVerificacao;
    private int ultimoAtual = -1;
    private int ultimoMaximo = -1;
    private int ultimoPercentual = -1;
    private float ultimoFill = -1f;
    private float alvoBarraPrincipal = 1f;
    private float[] alvosSegmentos = Array.Empty<float>();
    private bool inscritoMovimento;
    private bool inscritoBanco;

    private void Awake()
    {
        ResolveReferences(true);
        Subscribe();
        Refresh(true);
    }

    private void OnEnable()
    {
        ResolveReferences(true);
        Subscribe();
        InvalidateCache();
        Refresh(true);
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    private void Update()
    {
        AnimateVisuals();

        if (!atualizarMesmoDesativado && !gameObject.activeInHierarchy)
            return;

        if ((movimento == null || database == null) && Time.unscaledTime >= proximaBusca)
        {
            ResolveReferences(false);
            Subscribe();
        }

        if (Time.unscaledTime < proximaVerificacao)
            return;

        proximaVerificacao = Time.unscaledTime + Mathf.Max(0.1f, intervaloVerificacaoSeguranca);
        Refresh(false);
    }

    public void AtualizarTexto(bool forcar)
    {
        Refresh(forcar);
    }

    [ContextMenu("HUD/Rebuscar barras e atualizar")]
    public void RebuscarBarrasEAtualizar()
    {
        barraEnergia = null;
        barrasSegmentos = null;
        ResolveUIReferences();
        InvalidateCache();
        Refresh(true);
    }

    public void Refresh(bool force)
    {
        ResolveUIReferences();

        int atual;
        int maximo;
        float barraAtual01;
        float reserva01;

        if (movimento != null)
        {
            atual = movimento.StaminaSegmentosAtuais;
            maximo = movimento.StaminaSegmentosMaximos;
            barraAtual01 = movimento.StaminaPercentual01;
            reserva01 = movimento.StaminaMaxima > 0.001f
                ? Mathf.Clamp01(movimento.StaminaRecargaReserva / movimento.StaminaMaxima)
                : 0f;
        }
        else if (database != null)
        {
            atual = database.EnergiaSegmentosAtuais;
            maximo = database.EnergiaSegmentosMaximos;
            barraAtual01 = database.ObterPercentualStamina01();
            reserva01 = database.StaminaMaxima > 0.001f
                ? Mathf.Clamp01(database.EnergiaRecargaReserva / database.StaminaMaxima)
                : 0f;
        }
        else
        {
            maximo = Mathf.Max(1, barrasMaximasFallback);
            atual = maximo;
            barraAtual01 = 1f;
            reserva01 = 0f;
        }

        maximo = Mathf.Max(1, maximo);
        atual = Mathf.Clamp(atual, 0, maximo);
        barraAtual01 = Mathf.Clamp01(barraAtual01);

        float unidades = CalcularUnidadesEnergia(atual, maximo, barraAtual01, reserva01);
        float total01 = Mathf.Clamp01(unidades / maximo);
        int percentual = Mathf.RoundToInt(total01 * 100f);

        bool changed = force ||
                       atual != ultimoAtual ||
                       maximo != ultimoMaximo ||
                       percentual != ultimoPercentual ||
                       Mathf.Abs(barraAtual01 - ultimoFill) > 0.002f;

        if (!changed)
            return;

        ultimoAtual = atual;
        ultimoMaximo = maximo;
        ultimoPercentual = percentual;
        ultimoFill = barraAtual01;

        if (textoEnergia != null)
        {
            textoEnergia.text = mostrarPercentualDaBarra
                ? string.Format(formatoComPercentual, atual, maximo, percentual)
                : string.Format(formatoTexto, atual, maximo);
        }

        alvoBarraPrincipal = preencherBarraPrincipalComEnergiaTotal ? total01 : barraAtual01;
        PrepararAlvosSegmentos(maximo, unidades);

        if (force || !animarPreenchimento)
            ApplyVisualsImmediately(total01);

        if (iconeEnergia != null)
        {
            iconeEnergia.color = ColorFor(total01);
            Sprite sprite = SpriteFor(total01);
            if (sprite != null && iconeEnergia.sprite != sprite)
                iconeEnergia.sprite = sprite;
        }
    }

    private float CalcularUnidadesEnergia(int atual, int maximo, float barraAtual01, float reserva01)
    {
        if (atual <= 0)
            return Mathf.Clamp01(barraAtual01);

        float unidades = Mathf.Max(0, atual - 1) + barraAtual01;

        // Quando a barra atual já está cheia, a reserva representa visualmente a próxima barra.
        if (atual < maximo && barraAtual01 >= 0.999f)
            unidades += reserva01;

        return Mathf.Clamp(unidades, 0f, maximo);
    }

    private void PrepararAlvosSegmentos(int maximo, float unidades)
    {
        if (barrasSegmentos == null || barrasSegmentos.Length == 0)
        {
            alvosSegmentos = Array.Empty<float>();
            return;
        }

        if (alvosSegmentos == null || alvosSegmentos.Length != barrasSegmentos.Length)
            alvosSegmentos = new float[barrasSegmentos.Length];

        for (int visualIndex = 0; visualIndex < barrasSegmentos.Length; visualIndex++)
        {
            int logicalIndex = inverterOrdemSegmentos
                ? barrasSegmentos.Length - 1 - visualIndex
                : visualIndex;

            alvosSegmentos[visualIndex] = logicalIndex < maximo
                ? Mathf.Clamp01(unidades - logicalIndex)
                : 0f;
        }
    }

    private void AnimateVisuals()
    {
        float t = animarPreenchimento
            ? 1f - Mathf.Exp(-Mathf.Max(0.1f, velocidadePreenchimento) * Time.unscaledDeltaTime)
            : 1f;

        float total01 = ultimoPercentual >= 0 ? ultimoPercentual / 100f : 1f;
        Color color = ColorFor(total01);

        if (barraEnergia != null)
        {
            ConfigureFilledImage(barraEnergia);
            barraEnergia.fillAmount = Mathf.Lerp(barraEnergia.fillAmount, alvoBarraPrincipal, t);
            barraEnergia.color = color;
        }

        if (barrasSegmentos == null || alvosSegmentos == null)
            return;

        int count = Mathf.Min(barrasSegmentos.Length, alvosSegmentos.Length);
        for (int i = 0; i < count; i++)
        {
            Image image = barrasSegmentos[i];
            if (image == null)
                continue;

            ConfigureFilledImage(image);
            image.fillAmount = Mathf.Lerp(image.fillAmount, alvosSegmentos[i], t);
            image.color = color;
        }
    }

    private void ApplyVisualsImmediately(float total01)
    {
        Color color = ColorFor(total01);

        if (barraEnergia != null)
        {
            ConfigureFilledImage(barraEnergia);
            barraEnergia.fillAmount = alvoBarraPrincipal;
            barraEnergia.color = color;
        }

        if (barrasSegmentos == null || alvosSegmentos == null)
            return;

        int count = Mathf.Min(barrasSegmentos.Length, alvosSegmentos.Length);
        for (int i = 0; i < count; i++)
        {
            Image image = barrasSegmentos[i];
            if (image == null)
                continue;

            ConfigureFilledImage(image);
            image.fillAmount = alvosSegmentos[i];
            image.color = color;
        }
    }

    private void ConfigureFilledImage(Image image)
    {
        if (image == null)
            return;

        image.type = Image.Type.Filled;
        image.fillMethod = Image.FillMethod.Horizontal;
        image.fillOrigin = (int)Image.OriginHorizontal.Left;
        image.fillClockwise = true;
    }

    private void ResolveReferences(bool force)
    {
        ResolveUIReferences();

        if (force || movimento == null)
            movimento = Object.FindAnyObjectByType<CameraRelativeMovement>(FindObjectsInactive.Include);

        if (force || database == null)
        {
            database = MiniMarketPlayerDatabase.Instance;
            if (database == null && Application.isPlaying)
                database = MiniMarketPlayerDatabase.ObterOuCriar();
        }

        proximaBusca = Time.unscaledTime + Mathf.Max(0.25f, intervaloBuscaReferencias);

        if (movimento == null && database == null && logarSeNaoEncontrarPlayer)
            Debug.LogWarning("[EnergyHUD] Movimento e banco do jogador não foram encontrados.", this);
    }

    private void ResolveUIReferences()
    {
        if (textoEnergia == null)
            textoEnergia = GetComponent<Text>();

        if (!autoDetectarBarras)
            return;

        Transform root = FindVisualSearchRoot();
        Image[] images = root != null
            ? root.GetComponentsInChildren<Image>(true)
            : GetComponentsInChildren<Image>(true);

        List<Image> candidates = new List<Image>();
        for (int i = 0; i < images.Length; i++)
        {
            Image image = images[i];
            if (image == null || image == iconeEnergia)
                continue;

            string lower = image.name.ToLowerInvariant();
            if (ContainsAny(lower, "fundo", "background", "moldura", "frame", "icone", "icon", "mask"))
                continue;

            if (ContainsAny(lower, "segment", "barra", "bar", "fill", "stamina", "energia", "energy"))
                candidates.Add(image);
        }

        candidates.Sort((a, b) => string.CompareOrdinal(GetHierarchyPath(a.transform), GetHierarchyPath(b.transform)));

        if (barraEnergia == null && candidates.Count == 1)
            barraEnergia = candidates[0];

        if ((barrasSegmentos == null || barrasSegmentos.Length == 0) && candidates.Count > 1)
            barrasSegmentos = candidates.ToArray();

        if (barraEnergia == null && candidates.Count > 0)
            barraEnergia = candidates[0];
    }

    private Transform FindVisualSearchRoot()
    {
        Transform current = transform;
        Transform fallback = transform.parent != null ? transform.parent : transform;

        while (current != null)
        {
            string lower = current.name.ToLowerInvariant();
            if (ContainsAny(lower, "stamina", "energia", "energy", "hud"))
                fallback = current;

            if (current.GetComponent<Canvas>() != null)
                break;

            current = current.parent;
        }

        return fallback;
    }

    private string GetHierarchyPath(Transform target)
    {
        if (target == null)
            return string.Empty;

        string path = target.GetSiblingIndex().ToString("D4") + "_" + target.name;
        Transform parent = target.parent;
        while (parent != null)
        {
            path = parent.GetSiblingIndex().ToString("D4") + "_" + parent.name + "/" + path;
            parent = parent.parent;
        }

        return path;
    }

    private bool ContainsAny(string value, params string[] terms)
    {
        for (int i = 0; i < terms.Length; i++)
        {
            if (value.Contains(terms[i]))
                return true;
        }

        return false;
    }

    private void Subscribe()
    {
        if (movimento != null && !inscritoMovimento)
        {
            movimento.OnStaminaChanged += HandleStaminaChanged;
            inscritoMovimento = true;
        }

        if (database != null && !inscritoBanco)
        {
            database.OnDatabaseChanged += HandleDatabaseChanged;
            inscritoBanco = true;
        }
    }

    private void Unsubscribe()
    {
        if (movimento != null && inscritoMovimento)
            movimento.OnStaminaChanged -= HandleStaminaChanged;

        if (database != null && inscritoBanco)
            database.OnDatabaseChanged -= HandleDatabaseChanged;

        inscritoMovimento = false;
        inscritoBanco = false;
    }

    private void HandleStaminaChanged()
    {
        Refresh(false);
    }

    private void HandleDatabaseChanged(MiniMarketPlayerDatabase.MiniMarketPlayerData data)
    {
        Refresh(false);
    }

    private Color ColorFor(float value01)
    {
        if (value01 <= limiteBaixo)
            return corBaixa;
        if (value01 <= limiteMedio)
            return corMedia;
        return corAlta;
    }

    private Sprite SpriteFor(float value01)
    {
        if (value01 <= limiteBaixo)
            return energiaBaixaSprite;
        if (value01 <= limiteMedio)
            return energiaMediaSprite;
        return energiaAltaSprite;
    }

    private void InvalidateCache()
    {
        ultimoAtual = -1;
        ultimoMaximo = -1;
        ultimoPercentual = -1;
        ultimoFill = -1f;
    }

    private void OnValidate()
    {
        barrasMaximasFallback = Mathf.Max(1, barrasMaximasFallback);
        limiteBaixo = Mathf.Clamp01(limiteBaixo);
        limiteMedio = Mathf.Clamp(limiteMedio, limiteBaixo, 1f);
        intervaloBuscaReferencias = Mathf.Max(0.25f, intervaloBuscaReferencias);
        intervaloVerificacaoSeguranca = Mathf.Max(0.1f, intervaloVerificacaoSeguranca);
        velocidadePreenchimento = Mathf.Max(0.1f, velocidadePreenchimento);
    }
}
