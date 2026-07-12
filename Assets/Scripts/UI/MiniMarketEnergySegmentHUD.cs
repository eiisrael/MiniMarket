using System;
using System.Collections.Generic;
using Object = UnityEngine.Object;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// HUD de stamina/energia segmentada.
///
/// Atualiza texto, barra principal e barras individuais. A barra principal mostra por
/// padrão a carga ativa (100% -> 0%); as barras menores mostram o total segmentado.
/// Quando a cena não possui barras menores, o componente pode criá-las abaixo da barra
/// principal sem alterar o layout original.
/// </summary>
[DisallowMultipleComponent]
public class MiniMarketEnergySegmentHUD : MonoBehaviour
{
    public enum PrimaryBarDisplayMode
    {
        ActiveSegment,
        TotalEnergy,
        Auto
    }

    [Header("Referências")]
    public Text textoEnergia;
    public Image barraEnergia;
    public Image iconeEnergia;
    public Image[] barrasSegmentos;
    public CameraRelativeMovement movimento;

    [Header("Detecção automática")]
    public bool autoDetectarBarras = true;

    [Tooltip("Compatibilidade antiga. É usado somente quando Modo da barra principal = Auto.")]
    public bool preencherBarraPrincipalComEnergiaTotal = true;

    public PrimaryBarDisplayMode modoBarraPrincipal = PrimaryBarDisplayMode.ActiveSegment;
    public bool inverterOrdemSegmentos;

    [Header("Barras segmentadas automáticas")]
    public bool criarBarrasSegmentadasQuandoAusentes = true;
    [Min(2f)] public float alturaBarrasAutomaticas = 8f;
    [Min(0f)] public float espacoEntreBarras = 2f;
    public float deslocamentoVerticalBarras = -8f;
    public Color corFundoSegmento = new Color(0.05f, 0.08f, 0.05f, 0.72f);

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
    [Min(0.0001f)] public float toleranciaVisual = 0.001f;

    [Header("Atualização")]
    [Min(0.25f)] public float intervaloBuscaReferencias = 1f;
    [Min(0.1f)] public float intervaloVerificacaoSeguranca = 0.25f;
    public bool atualizarMesmoDesativado;

    [Header("Debug")]
    public bool logarSeNaoEncontrarPlayer;

    private const string AutoContainerName = "EnergySegments_Auto";
    private const string AutoFillName = "SegmentFill";

    private MiniMarketPlayerDatabase database;
    private RectTransform autoSegmentContainer;
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

    [ContextMenu("HUD/Recriar barras segmentadas automáticas")]
    public void RecriarBarrasSegmentadasAutomaticas()
    {
        DestroyAutomaticSegmentContainer();
        barrasSegmentos = null;
        ResolveUIReferences();
        int maximo = movimento != null
            ? movimento.StaminaSegmentosMaximos
            : database != null ? database.EnergiaSegmentosMaximos : barrasMaximasFallback;
        EnsureSegmentBars(Mathf.Max(1, maximo));
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
        reserva01 = Mathf.Clamp01(reserva01);

        EnsureSegmentBars(maximo);

        float unidades = CalcularUnidadesEnergia(atual, maximo, barraAtual01, reserva01);
        float total01 = Mathf.Clamp01(unidades / maximo);
        int percentual = Mathf.RoundToInt(total01 * 100f);

        bool changed = force ||
                       atual != ultimoAtual ||
                       maximo != ultimoMaximo ||
                       percentual != ultimoPercentual ||
                       Mathf.Abs(barraAtual01 - ultimoFill) > 0.0005f;

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

        alvoBarraPrincipal = ResolvePrimaryBarTarget(barraAtual01, total01);
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

    private float ResolvePrimaryBarTarget(float active01, float total01)
    {
        switch (modoBarraPrincipal)
        {
            case PrimaryBarDisplayMode.TotalEnergy:
                return total01;
            case PrimaryBarDisplayMode.Auto:
                return preencherBarraPrincipalComEnergiaTotal && CountValidSegmentBars() > 0
                    ? total01
                    : active01;
            default:
                return active01;
        }
    }

    private float CalcularUnidadesEnergia(int atual, int maximo, float barraAtual01, float reserva01)
    {
        if (atual <= 0)
            return Mathf.Clamp01(barraAtual01);

        float unidades = Mathf.Max(0, atual - 1) + barraAtual01;

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
            barraEnergia.fillAmount = MoveFill(barraEnergia.fillAmount, alvoBarraPrincipal, t);
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
            image.fillAmount = MoveFill(image.fillAmount, alvosSegmentos[i], t);
            image.color = color;
        }
    }

    private float MoveFill(float current, float target, float t)
    {
        float value = Mathf.Lerp(current, target, t);
        return Mathf.Abs(value - target) <= toleranciaVisual ? target : value;
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

    private void EnsureSegmentBars(int desiredCount)
    {
        desiredCount = Mathf.Max(1, desiredCount);

        if (CountValidSegmentBars() > 0)
            return;

        if (!criarBarrasSegmentadasQuandoAusentes || barraEnergia == null)
            return;

        RectTransform parent = barraEnergia.rectTransform.parent as RectTransform;
        if (parent == null)
            return;

        Transform existing = parent.Find(AutoContainerName);
        if (existing != null)
            autoSegmentContainer = existing as RectTransform;

        if (autoSegmentContainer == null)
            BuildAutomaticSegmentContainer(parent);

        if (autoSegmentContainer == null)
            return;

        List<Image> fills = CollectAutomaticFills();
        if (fills.Count != desiredCount)
        {
            ClearChildren(autoSegmentContainer);
            fills.Clear();

            for (int i = 0; i < desiredCount; i++)
                fills.Add(CreateAutomaticSegment(i));
        }

        barrasSegmentos = fills.ToArray();
    }

    private void BuildAutomaticSegmentContainer(RectTransform parent)
    {
        GameObject containerObject = new GameObject(AutoContainerName, typeof(RectTransform));
        containerObject.transform.SetParent(parent, false);
        autoSegmentContainer = containerObject.GetComponent<RectTransform>();

        RectTransform source = barraEnergia.rectTransform;
        autoSegmentContainer.anchorMin = source.anchorMin;
        autoSegmentContainer.anchorMax = source.anchorMax;
        autoSegmentContainer.pivot = source.pivot;
        autoSegmentContainer.anchoredPosition = source.anchoredPosition +
                                                  new Vector2(0f, deslocamentoVerticalBarras - Mathf.Max(0f, source.rect.height * 0.5f));
        autoSegmentContainer.sizeDelta = new Vector2(source.sizeDelta.x, alturaBarrasAutomaticas);
        autoSegmentContainer.SetSiblingIndex(Mathf.Min(parent.childCount - 1, source.GetSiblingIndex() + 1));

        HorizontalLayoutGroup layout = containerObject.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = espacoEntreBarras;
        layout.padding = new RectOffset(0, 0, 0, 0);
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = true;
    }

    private Image CreateAutomaticSegment(int index)
    {
        GameObject cell = new GameObject("Segment_" + (index + 1), typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        cell.transform.SetParent(autoSegmentContainer, false);
        Image background = cell.GetComponent<Image>();
        background.sprite = barraEnergia != null ? barraEnergia.sprite : null;
        background.color = corFundoSegmento;
        background.raycastTarget = false;
        LayoutElement layout = cell.GetComponent<LayoutElement>();
        layout.flexibleWidth = 1f;
        layout.minHeight = alturaBarrasAutomaticas;

        GameObject fillObject = new GameObject(AutoFillName, typeof(RectTransform), typeof(Image));
        fillObject.transform.SetParent(cell.transform, false);
        RectTransform fillRect = fillObject.GetComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;

        Image fill = fillObject.GetComponent<Image>();
        fill.sprite = barraEnergia != null ? barraEnergia.sprite : null;
        fill.raycastTarget = false;
        ConfigureFilledImage(fill);
        fill.fillAmount = 1f;
        return fill;
    }

    private List<Image> CollectAutomaticFills()
    {
        List<Image> result = new List<Image>();
        if (autoSegmentContainer == null)
            return result;

        Image[] images = autoSegmentContainer.GetComponentsInChildren<Image>(true);
        for (int i = 0; i < images.Length; i++)
        {
            if (images[i] != null && images[i].name == AutoFillName)
                result.Add(images[i]);
        }
        return result;
    }

    private void ClearChildren(Transform root)
    {
        if (root == null)
            return;

        for (int i = root.childCount - 1; i >= 0; i--)
        {
            GameObject child = root.GetChild(i).gameObject;
            if (Application.isPlaying)
                Destroy(child);
            else
                DestroyImmediate(child);
        }
    }

    private void DestroyAutomaticSegmentContainer()
    {
        if (autoSegmentContainer == null && barraEnergia != null && barraEnergia.transform.parent != null)
            autoSegmentContainer = barraEnergia.transform.parent.Find(AutoContainerName) as RectTransform;

        if (autoSegmentContainer == null)
            return;

        GameObject target = autoSegmentContainer.gameObject;
        autoSegmentContainer = null;
        if (Application.isPlaying)
            Destroy(target);
        else
            DestroyImmediate(target);
    }

    private int CountValidSegmentBars()
    {
        if (barrasSegmentos == null)
            return 0;

        int count = 0;
        for (int i = 0; i < barrasSegmentos.Length; i++)
        {
            if (barrasSegmentos[i] != null && barrasSegmentos[i] != barraEnergia)
                count++;
        }
        return count;
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

        List<Image> primaryCandidates = new List<Image>();
        List<Image> segmentCandidates = new List<Image>();

        for (int i = 0; i < images.Length; i++)
        {
            Image image = images[i];
            if (image == null || image == iconeEnergia)
                continue;

            string lower = image.name.ToLowerInvariant();
            if (ContainsAny(lower, "fundo", "background", "moldura", "frame", "icone", "icon", "mask", "border"))
                continue;

            bool explicitSegment = ContainsAny(lower, "segment", "carga_", "bar_1", "bar_2", "bar_3", "bar_4", "bar_5");
            bool primary = ContainsAny(lower, "progress", "fill", "barraenergia", "barra_energia", "staminafill", "energyfill");

            if (explicitSegment && image != barraEnergia)
                segmentCandidates.Add(image);
            else if (primary || ContainsAny(lower, "barra", "bar", "stamina", "energia", "energy"))
                primaryCandidates.Add(image);
        }

        primaryCandidates.Sort((a, b) => CompareImageAreaDescending(a, b));
        segmentCandidates.Sort((a, b) => string.CompareOrdinal(GetHierarchyPath(a.transform), GetHierarchyPath(b.transform)));

        if (barraEnergia == null && primaryCandidates.Count > 0)
            barraEnergia = primaryCandidates[0];

        if ((barrasSegmentos == null || CountValidSegmentBars() == 0) && segmentCandidates.Count > 0)
            barrasSegmentos = segmentCandidates.ToArray();

        if (barraEnergia != null && barrasSegmentos != null)
        {
            List<Image> clean = new List<Image>();
            for (int i = 0; i < barrasSegmentos.Length; i++)
            {
                Image image = barrasSegmentos[i];
                if (image != null && image != barraEnergia && !clean.Contains(image))
                    clean.Add(image);
            }
            barrasSegmentos = clean.ToArray();
        }
    }

    private int CompareImageAreaDescending(Image a, Image b)
    {
        float areaA = a != null ? Mathf.Abs(a.rectTransform.rect.width * a.rectTransform.rect.height) : 0f;
        float areaB = b != null ? Mathf.Abs(b.rectTransform.rect.width * b.rectTransform.rect.height) : 0f;
        return areaB.CompareTo(areaA);
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
        toleranciaVisual = Mathf.Max(0.0001f, toleranciaVisual);
        alturaBarrasAutomaticas = Mathf.Max(2f, alturaBarrasAutomaticas);
        espacoEntreBarras = Mathf.Max(0f, espacoEntreBarras);
    }
}
