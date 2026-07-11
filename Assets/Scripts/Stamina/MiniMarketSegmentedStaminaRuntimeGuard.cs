using System.Reflection;
using UnityEngine;

/// <summary>
/// Guarda leve para a stamina segmentada 5/5.
///
/// Otimizacao:
/// - procura o PlayerMove apenas quando necessario;
/// - prepara FieldInfo uma unica vez;
/// - usa propriedades publicas para leitura;
/// - escreve campos privados somente em mudancas reais de estado;
/// - nao gera strings, logs ou alocacoes no loop normal.
/// </summary>
[DefaultExecutionOrder(-5000)]
[DisallowMultipleComponent]
public class MiniMarketSegmentedStaminaRuntimeGuard : MonoBehaviour
{
    [Header("Busca")]
    public bool procurarPlayerAutomaticamente = true;
    public PlayerMove playerMove;

    [Header("Ajuste Segmentado")]
    public float limiteMinimoSegmentado = -0.01f;
    public bool destravarEnquantoHouverSegmentos = true;
    public bool impedirDrenoInstantaneoNoZero = true;

    [Header("Barra Final 0/5")]
    [Tooltip("Permite correr no 0/5 se a barra tiver energia parcial. Visualmente continua 0/5.")]
    public bool permitirCorrerComBarraParcialNoZero = true;

    [Tooltip("Quando a barra final 0/5 zerar segurando Shift, a corrida fica desativada ate soltar Shift.")]
    public bool exigirSoltarShiftAposZerarFinal = true;

    [Header("Performance")]
    [Min(0.25f)] public float intervaloBusca = 1f;
    [Min(0.25f)] public float intervaloReforcoConfiguracao = 1f;

    [Header("Debug")]
    public bool logarEventos;

    public static bool ForcarHudZeroNoSegmentoFantasma { get; private set; }
    public bool SegmentoFantasmaAtivo => segmentoFantasmaAtivo;
    public bool BloqueadoAteSoltarShift => bloqueadoAteSoltarShift;

    private static MiniMarketSegmentedStaminaRuntimeGuard instancia;

    private float proximaBusca;
    private float proximoReforcoConfiguracao;
    private float limiteMinimoOriginal;
    private bool limiteOriginalCapturado;
    private bool segmentoFantasmaAtivo;
    private bool bloqueadoAteSoltarShift;

    private FieldInfo campoCorridaBloqueada;
    private FieldInfo campoStaminaSegmentosAtuais;
    private FieldInfo campoStaminaAtual;

    private bool valorOriginalTravarNoZero;
    private bool valorOriginalTravarNoZeroCapturado;

    private const float EpsilonStamina = 0.001f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetarStatics()
    {
        instancia = null;
        ForcarHudZeroNoSegmentoFantasma = false;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void CriarAutomaticamente()
    {
        if (instancia != null)
            return;

        GameObject go = new GameObject("MiniMarket_SegmentedStaminaRuntimeGuard");
        DontDestroyOnLoad(go);
        go.AddComponent<MiniMarketSegmentedStaminaRuntimeGuard>();
    }

    private void Awake()
    {
        if (instancia != null && instancia != this)
        {
            Destroy(gameObject);
            return;
        }

        instancia = this;
        DontDestroyOnLoad(gameObject);
        ResolverPlayer(true);
    }

    private void OnDisable()
    {
        RestaurarConfiguracaoOriginal();
        ForcarHudZeroNoSegmentoFantasma = false;
    }

    private void Update()
    {
        ResolverPlayer(false);

        if (playerMove == null)
            return;

        if (!playerMove.usarStamina || !playerMove.usarStaminaSegmentada)
        {
            LimparEstadoSegmentoFantasma();
            RestaurarConfiguracaoOriginal();
            return;
        }

        if (Time.unscaledTime >= proximoReforcoConfiguracao)
        {
            proximoReforcoConfiguracao = Time.unscaledTime + Mathf.Max(0.25f, intervaloReforcoConfiguracao);
            AplicarConfiguracaoSegmentada();
        }

        if (permitirCorrerComBarraParcialNoZero)
            AtualizarSegmentoFantasmaDoZero();
        else
            LimparEstadoSegmentoFantasma();

        if (!destravarEnquantoHouverSegmentos)
            return;

        if (bloqueadoAteSoltarShift)
        {
            DefinirCorridaBloqueada(true);
            return;
        }

        if (playerMove.StaminaSegmentosAtuais > 0 || segmentoFantasmaAtivo)
            DefinirCorridaBloqueada(false);
    }

    private void ResolverPlayer(bool forcar)
    {
        if (!procurarPlayerAutomaticamente && playerMove == null)
            return;

        if (!forcar && playerMove != null)
            return;

        if (!forcar && Time.unscaledTime < proximaBusca)
            return;

        proximaBusca = Time.unscaledTime + Mathf.Max(0.25f, intervaloBusca);
        PlayerMove encontrado = Object.FindFirstObjectByType<PlayerMove>(FindObjectsInactive.Include);

        if (encontrado == null || encontrado == playerMove)
            return;

        RestaurarConfiguracaoOriginal();
        playerMove = encontrado;
        PrepararCamposPrivados();
        CapturarConfiguracaoOriginal();
        AplicarConfiguracaoSegmentada();

        segmentoFantasmaAtivo = false;
        bloqueadoAteSoltarShift = false;
        ForcarHudZeroNoSegmentoFantasma = false;

        if (logarEventos)
            Debug.Log("[MiniMarketSegmentedStaminaRuntimeGuard] PlayerMove encontrado e configurado: " + playerMove.gameObject.name);
    }

    private void PrepararCamposPrivados()
    {
        System.Type tipo = typeof(PlayerMove);
        BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        campoCorridaBloqueada = tipo.GetField("corridaBloqueadaPorStamina", flags);
        campoStaminaSegmentosAtuais = tipo.GetField("staminaSegmentosAtuais", flags);
        campoStaminaAtual = tipo.GetField("staminaAtual", flags);
    }

    private void CapturarConfiguracaoOriginal()
    {
        if (playerMove == null)
            return;

        limiteMinimoOriginal = playerMove.staminaMinimaParaCorrer;
        limiteOriginalCapturado = true;
        valorOriginalTravarNoZero = playerMove.travarNoZeroEnquantoShiftPressionado;
        valorOriginalTravarNoZeroCapturado = true;
    }

    private void AplicarConfiguracaoSegmentada()
    {
        if (playerMove == null)
            return;

        if (!limiteOriginalCapturado)
            CapturarConfiguracaoOriginal();

        if (playerMove.staminaMinimaParaCorrer > limiteMinimoSegmentado)
            playerMove.staminaMinimaParaCorrer = limiteMinimoSegmentado;

        if (impedirDrenoInstantaneoNoZero && playerMove.travarNoZeroEnquantoShiftPressionado)
            playerMove.travarNoZeroEnquantoShiftPressionado = false;
    }

    private void AtualizarSegmentoFantasmaDoZero()
    {
        bool segurandoShift = Input.GetKey(playerMove.runKey);
        float staminaAtual = playerMove.StaminaAtual;
        float staminaMaxima = Mathf.Max(1f, playerMove.StaminaMaxima);
        int segmentos = playerMove.StaminaSegmentosAtuais;

        if (bloqueadoAteSoltarShift)
        {
            if (!segurandoShift)
            {
                bloqueadoAteSoltarShift = false;
                DefinirCorridaBloqueada(false);
            }
            else
            {
                DefinirSegmentosInternos(0);
                DefinirStaminaAtualInterna(0f);
                segmentoFantasmaAtivo = false;
                ForcarHudZeroNoSegmentoFantasma = false;
                DefinirCorridaBloqueada(true);
                return;
            }
        }

        if (segmentos <= 0 && staminaAtual > EpsilonStamina)
        {
            DefinirSegmentosInternos(1);
            segmentoFantasmaAtivo = true;
            ForcarHudZeroNoSegmentoFantasma = true;
            DefinirCorridaBloqueada(false);
            return;
        }

        if (!segmentoFantasmaAtivo)
        {
            ForcarHudZeroNoSegmentoFantasma = false;
            return;
        }

        staminaAtual = playerMove.StaminaAtual;

        if (staminaAtual <= EpsilonStamina)
        {
            DefinirSegmentosInternos(0);
            segmentoFantasmaAtivo = false;
            ForcarHudZeroNoSegmentoFantasma = false;
            DefinirCorridaBloqueada(true);

            if (exigirSoltarShiftAposZerarFinal && segurandoShift)
                bloqueadoAteSoltarShift = true;

            return;
        }

        if (!segurandoShift && staminaAtual >= staminaMaxima - 0.05f)
        {
            segmentoFantasmaAtivo = false;
            ForcarHudZeroNoSegmentoFantasma = false;
            DefinirSegmentosInternos(1);
            DefinirCorridaBloqueada(false);
            return;
        }

        if (playerMove.StaminaSegmentosAtuais < 1)
            DefinirSegmentosInternos(1);

        ForcarHudZeroNoSegmentoFantasma = true;
        DefinirCorridaBloqueada(false);
    }

    private void LimparEstadoSegmentoFantasma()
    {
        segmentoFantasmaAtivo = false;
        bloqueadoAteSoltarShift = false;
        ForcarHudZeroNoSegmentoFantasma = false;
    }

    private void DefinirSegmentosInternos(int valor)
    {
        if (playerMove == null || campoStaminaSegmentosAtuais == null)
            return;

        int limitado = Mathf.Clamp(valor, 0, Mathf.Max(1, playerMove.StaminaSegmentosMaximos));
        if (playerMove.StaminaSegmentosAtuais != limitado)
            campoStaminaSegmentosAtuais.SetValue(playerMove, limitado);
    }

    private void DefinirStaminaAtualInterna(float valor)
    {
        if (playerMove == null || campoStaminaAtual == null)
            return;

        float limitado = Mathf.Clamp(valor, 0f, playerMove.StaminaMaxima);
        if (Mathf.Abs(playerMove.StaminaAtual - limitado) > EpsilonStamina)
            campoStaminaAtual.SetValue(playerMove, limitado);
    }

    private void DefinirCorridaBloqueada(bool bloqueada)
    {
        if (playerMove == null || campoCorridaBloqueada == null)
            return;

        if (playerMove.EstaCansado != bloqueada)
            campoCorridaBloqueada.SetValue(playerMove, bloqueada);
    }

    private void RestaurarConfiguracaoOriginal()
    {
        if (playerMove == null)
            return;

        if (limiteOriginalCapturado)
            playerMove.staminaMinimaParaCorrer = Mathf.Max(0f, limiteMinimoOriginal);

        if (valorOriginalTravarNoZeroCapturado)
            playerMove.travarNoZeroEnquantoShiftPressionado = valorOriginalTravarNoZero;

        limiteOriginalCapturado = false;
        valorOriginalTravarNoZeroCapturado = false;
    }
}
