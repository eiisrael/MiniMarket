using System.Reflection;
using UnityEngine;

/// <summary>
/// Correção runtime segura para o modo de stamina segmentada 5/5.
///
/// Regra final:
/// - 5/5 até 1/5: cada barra esvazia normalmente; ao zerar, recarrega 100% e decrementa.
/// - 0/5 com barra parcialmente carregada: Shift funciona e gasta a barra na mesma velocidade.
/// - 0/5 com barra zerada: corrida desativa.
/// - Visualmente o HUD continua mostrando 0/5 durante a barra final temporária.
/// </summary>
[DefaultExecutionOrder(-5000)]
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

    [Tooltip("Quando a barra final 0/5 zerar segurando Shift, a corrida fica desativada até soltar Shift.")]
    public bool exigirSoltarShiftAposZerarFinal = true;

    [Header("Performance")]
    [Min(0.02f)] public float intervaloBusca = 0.5f;

    [Header("Debug")]
    public bool logarEventos;

    public static bool ForcarHudZeroNoSegmentoFantasma { get; private set; }

    private static MiniMarketSegmentedStaminaRuntimeGuard instancia;

    private float proximaBusca;
    private float limiteMinimoOriginal;
    private bool limiteOriginalCapturado;

    private bool segmentoFantasmaAtivo;
    private bool bloqueadoAteSoltarShift;

    private FieldInfo campoUsarStaminaSegmentada;
    private FieldInfo campoCorridaBloqueada;
    private FieldInfo campoTravarNoZeroEnquantoShift;
    private FieldInfo campoStaminaSegmentosAtuais;
    private FieldInfo campoStaminaAtual;

    private bool valorOriginalTravarNoZero;
    private bool valorOriginalTravarNoZeroCapturado;

    private const float EpsilonStamina = 0.001f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void CriarAutomaticamente()
    {
        if (instancia != null)
            return;

        GameObject go = new GameObject("MiniMarket_SegmentedStaminaRuntimeGuard");
        DontDestroyOnLoad(go);
        instancia = go.AddComponent<MiniMarketSegmentedStaminaRuntimeGuard>();
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

    private void Update()
    {
        ResolverPlayer(false);

        if (playerMove == null)
            return;

        bool modoSegmentadoAtivo = ModoSegmentadoAtivo();

        if (!limiteOriginalCapturado)
        {
            limiteMinimoOriginal = playerMove.staminaMinimaParaCorrer;
            limiteOriginalCapturado = true;
        }

        if (modoSegmentadoAtivo)
        {
            AplicarCorrecaoSegmentada();
        }
        else
        {
            ForcarHudZeroNoSegmentoFantasma = false;
            segmentoFantasmaAtivo = false;
            bloqueadoAteSoltarShift = false;
            RestaurarLimiteOriginalSeNecessario();
            RestaurarTravaZeroOriginalSeNecessario();
        }
    }

    private void ResolverPlayer(bool forcar)
    {
        if (!procurarPlayerAutomaticamente)
            return;

        if (playerMove != null && !forcar)
            return;

        if (!forcar && Time.unscaledTime < proximaBusca)
            return;

        proximaBusca = Time.unscaledTime + intervaloBusca;

        PlayerMove encontrado = FindObjectOfType<PlayerMove>(true);
        if (encontrado == null || encontrado == playerMove)
            return;

        playerMove = encontrado;

        limiteOriginalCapturado = false;
        valorOriginalTravarNoZeroCapturado = false;
        segmentoFantasmaAtivo = false;
        bloqueadoAteSoltarShift = false;
        ForcarHudZeroNoSegmentoFantasma = false;

        campoUsarStaminaSegmentada = null;
        campoCorridaBloqueada = null;
        campoTravarNoZeroEnquantoShift = null;
        campoStaminaSegmentosAtuais = null;
        campoStaminaAtual = null;

        if (logarEventos)
            Debug.Log("[MiniMarketSegmentedStaminaRuntimeGuard] PlayerMove encontrado: " + playerMove.gameObject.name);
    }

    private bool ModoSegmentadoAtivo()
    {
        if (playerMove == null)
            return false;

        if (campoUsarStaminaSegmentada == null)
        {
            campoUsarStaminaSegmentada = typeof(PlayerMove).GetField(
                "usarStaminaSegmentada",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic
            );
        }

        if (campoUsarStaminaSegmentada == null || campoUsarStaminaSegmentada.FieldType != typeof(bool))
            return false;

        return (bool)campoUsarStaminaSegmentada.GetValue(playerMove);
    }

    private void AplicarCorrecaoSegmentada()
    {
        if (playerMove == null)
            return;

        if (playerMove.staminaMinimaParaCorrer > limiteMinimoSegmentado)
            playerMove.staminaMinimaParaCorrer = limiteMinimoSegmentado;

        if (impedirDrenoInstantaneoNoZero)
            DesativarTravaInstantaneaNoZero();

        if (permitirCorrerComBarraParcialNoZero)
            AtualizarSegmentoFantasmaDoZero();

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

    private void AtualizarSegmentoFantasmaDoZero()
    {
        bool segurandoShift = Input.GetKey(playerMove.runKey);
        float staminaAtual = playerMove.StaminaAtual;
        float staminaMaxima = Mathf.Max(1f, playerMove.StaminaMaxima);
        int segmentos = LerSegmentosInternos();

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
                ForcarHudZeroNoSegmentoFantasma = false;
                DefinirCorridaBloqueada(true);
                return;
            }
        }

        // Caso principal: 0/5, mas a barra já carregou um pouco.
        // Internamente colocamos 1 segmento temporário para o PlayerMove permitir correr.
        // O HUD continua mostrando 0/5.
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
        segmentos = LerSegmentosInternos();

        // A barra final zerou: agora realmente desativa a corrida.
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

        // Se parou de correr e a barra final encheu completamente, vira 1/5 de verdade.
        if (!segurandoShift && staminaAtual >= staminaMaxima - 0.05f)
        {
            segmentoFantasmaAtivo = false;
            ForcarHudZeroNoSegmentoFantasma = false;
            DefinirSegmentosInternos(1);
            DefinirCorridaBloqueada(false);
            return;
        }

        // Mantém o segmento interno temporário enquanto a barra parcial 0/5 está sendo usada.
        if (segmentos < 1)
            DefinirSegmentosInternos(1);

        ForcarHudZeroNoSegmentoFantasma = true;
        DefinirCorridaBloqueada(false);
    }

    private int LerSegmentosInternos()
    {
        if (playerMove == null)
            return 0;

        if (campoStaminaSegmentosAtuais == null)
        {
            campoStaminaSegmentosAtuais = typeof(PlayerMove).GetField(
                "staminaSegmentosAtuais",
                BindingFlags.Instance | BindingFlags.NonPublic
            );
        }

        if (campoStaminaSegmentosAtuais == null || campoStaminaSegmentosAtuais.FieldType != typeof(int))
            return playerMove.StaminaSegmentosAtuais;

        return (int)campoStaminaSegmentosAtuais.GetValue(playerMove);
    }

    private void DefinirSegmentosInternos(int valor)
    {
        if (playerMove == null)
            return;

        if (campoStaminaSegmentosAtuais == null)
        {
            campoStaminaSegmentosAtuais = typeof(PlayerMove).GetField(
                "staminaSegmentosAtuais",
                BindingFlags.Instance | BindingFlags.NonPublic
            );
        }

        if (campoStaminaSegmentosAtuais == null || campoStaminaSegmentosAtuais.FieldType != typeof(int))
            return;

        campoStaminaSegmentosAtuais.SetValue(
            playerMove,
            Mathf.Clamp(valor, 0, Mathf.Max(1, playerMove.StaminaSegmentosMaximos))
        );
    }

    private void DefinirStaminaAtualInterna(float valor)
    {
        if (playerMove == null)
            return;

        if (campoStaminaAtual == null)
        {
            campoStaminaAtual = typeof(PlayerMove).GetField(
                "staminaAtual",
                BindingFlags.Instance | BindingFlags.NonPublic
            );
        }

        if (campoStaminaAtual == null || campoStaminaAtual.FieldType != typeof(float))
            return;

        campoStaminaAtual.SetValue(playerMove, Mathf.Clamp(valor, 0f, playerMove.StaminaMaxima));
    }

    private void DesativarTravaInstantaneaNoZero()
    {
        if (playerMove == null)
            return;

        if (campoTravarNoZeroEnquantoShift == null)
        {
            campoTravarNoZeroEnquantoShift = typeof(PlayerMove).GetField(
                "travarNoZeroEnquantoShiftPressionado",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic
            );
        }

        if (campoTravarNoZeroEnquantoShift == null || campoTravarNoZeroEnquantoShift.FieldType != typeof(bool))
            return;

        if (!valorOriginalTravarNoZeroCapturado)
        {
            valorOriginalTravarNoZero = (bool)campoTravarNoZeroEnquantoShift.GetValue(playerMove);
            valorOriginalTravarNoZeroCapturado = true;
        }

        if ((bool)campoTravarNoZeroEnquantoShift.GetValue(playerMove))
            campoTravarNoZeroEnquantoShift.SetValue(playerMove, false);
    }

    private void RestaurarTravaZeroOriginalSeNecessario()
    {
        if (playerMove == null || !valorOriginalTravarNoZeroCapturado || campoTravarNoZeroEnquantoShift == null)
            return;

        campoTravarNoZeroEnquantoShift.SetValue(playerMove, valorOriginalTravarNoZero);
    }

    private void RestaurarLimiteOriginalSeNecessario()
    {
        if (playerMove == null || !limiteOriginalCapturado)
            return;

        if (playerMove.staminaMinimaParaCorrer < 0f)
            playerMove.staminaMinimaParaCorrer = Mathf.Max(0f, limiteMinimoOriginal);
    }

    private void DefinirCorridaBloqueada(bool bloqueada)
    {
        if (playerMove == null)
            return;

        if (campoCorridaBloqueada == null)
        {
            campoCorridaBloqueada = typeof(PlayerMove).GetField(
                "corridaBloqueadaPorStamina",
                BindingFlags.Instance | BindingFlags.NonPublic
            );
        }

        if (campoCorridaBloqueada == null || campoCorridaBloqueada.FieldType != typeof(bool))
            return;

        campoCorridaBloqueada.SetValue(playerMove, bloqueada);
    }
}