using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// HUD de stamina/energia segmentada.
///
/// A atualização principal é dirigida por eventos do movimento e do banco; existe apenas
/// uma verificação lenta de segurança para cenas carregadas depois. Funciona em desktop
/// e mobile sem trabalho de reflexão nem busca por frame.
/// </summary>
[DisallowMultipleComponent]
public class MiniMarketEnergySegmentHUD : MonoBehaviour
{
    [Header("Referências")]
    public Text textoEnergia;
    public Image barraEnergia;
    public Image iconeEnergia;
    public CameraRelativeMovement movimento;

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

    public void Refresh(bool force)
    {
        ResolveUIReferences();

        int atual;
        int maximo;
        float barra01;
        float total01;

        if (movimento != null)
        {
            atual = movimento.StaminaSegmentosAtuais;
            maximo = movimento.StaminaSegmentosMaximos;
            barra01 = movimento.StaminaPercentual01;
            total01 = movimento.EnergiaPercentual01;
        }
        else if (database != null)
        {
            atual = database.EnergiaSegmentosAtuais;
            maximo = database.EnergiaSegmentosMaximos;
            barra01 = database.ObterPercentualStamina01();
            total01 = database.EnergiaPercentual01;
        }
        else
        {
            maximo = Mathf.Max(1, barrasMaximasFallback);
            atual = maximo;
            barra01 = 1f;
            total01 = 1f;
        }

        maximo = Mathf.Max(1, maximo);
        atual = Mathf.Clamp(atual, 0, maximo);
        barra01 = Mathf.Clamp01(barra01);
        total01 = Mathf.Clamp01(total01);
        int percentual = Mathf.RoundToInt(total01 * 100f);

        bool changed = force ||
                       atual != ultimoAtual ||
                       maximo != ultimoMaximo ||
                       percentual != ultimoPercentual ||
                       Mathf.Abs(barra01 - ultimoFill) > 0.002f;

        if (!changed)
            return;

        ultimoAtual = atual;
        ultimoMaximo = maximo;
        ultimoPercentual = percentual;
        ultimoFill = barra01;

        if (textoEnergia != null)
        {
            textoEnergia.text = mostrarPercentualDaBarra
                ? string.Format(formatoComPercentual, atual, maximo, percentual)
                : string.Format(formatoTexto, atual, maximo);
        }

        if (barraEnergia != null)
        {
            barraEnergia.type = Image.Type.Filled;
            barraEnergia.fillMethod = Image.FillMethod.Horizontal;
            barraEnergia.fillAmount = barra01;
            barraEnergia.color = ColorFor(total01);
        }

        if (iconeEnergia != null)
        {
            iconeEnergia.color = ColorFor(total01);
            Sprite sprite = SpriteFor(total01);
            if (sprite != null && iconeEnergia.sprite != sprite)
                iconeEnergia.sprite = sprite;
        }
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

        if (barraEnergia == null)
        {
            Image[] images = GetComponentsInChildren<Image>(true);
            for (int i = 0; i < images.Length; i++)
            {
                Image image = images[i];
                if (image == null || image == iconeEnergia)
                    continue;

                string lower = image.name.ToLowerInvariant();
                if (lower.Contains("barra") || lower.Contains("fill") || lower.Contains("stamina"))
                {
                    barraEnergia = image;
                    break;
                }
            }
        }
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
    }
}
