using System.Reflection;
using UnityEngine;

/// <summary>
/// Correção runtime segura para o modo de stamina segmentada 5/5.
///
/// Objetivo:
/// - Manter o funcionamento antigo da corrida: o personagem continua correndo enquanto houver energia.
/// - No modo segmentado, ignorar o limite minimo antigo da barra ativa para que ela possa chegar ate 0.
/// - Quando a barra chega em 0, o PlayerMove consome 1 segmento e recarrega a barra para 100%.
/// - So bloquear corrida quando os segmentos chegarem em 0/5.
///
/// Este script se cria automaticamente ao iniciar a cena. Nao precisa arrastar no Inspector.
/// </summary>
[DefaultExecutionOrder(-5000)]
public class MiniMarketSegmentedStaminaRuntimeGuard : MonoBehaviour
{
    [Header("Busca")]
    public bool procurarPlayerAutomaticamente = true;
    public PlayerMove playerMove;

    [Header("Ajuste Segmentado")]
    [Tooltip("Valor aplicado ao limite minimo de corrida enquanto o modo 5/5 estiver ativo. Negativo para permitir a barra chegar em 0 antes de bloquear.")]
    public float limiteMinimoSegmentado = -0.01f;

    [Tooltip("Enquanto ainda houver cargas, remove o bloqueio antigo de cansaço.")]
    public bool destravarEnquantoHouverSegmentos = true;

    [Tooltip("Se a stamina ativa estiver zerada mas ainda houver segmentos, deixa o PlayerMove consumir/decrementar no frame seguinte.")]
    public bool manterConsumoContinuo = true;

    [Header("Performance")]
    [Min(0.02f)] public float intervaloBusca = 0.5f;

    [Header("Debug")]
    public bool logarEventos;

    private static MiniMarketSegmentedStaminaRuntimeGuard instancia;
    private float proximaBusca;
    private float limiteMinimoOriginal;
    private bool limiteOriginalCapturado;
    private FieldInfo campoUsarStaminaSegmentada;
    private FieldInfo campoCorridaBloqueada;

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
            RestaurarLimiteOriginalSeNecessario();
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
        campoUsarStaminaSegmentada = null;
        campoCorridaBloqueada = null;

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

        // A trava antiga era: se staminaAtual <= staminaMinimaParaCorrer, para de correr.
        // No modo 5/5, isso impede a barra de chegar em zero e de consumir o próximo segmento.
        // Por isso deixamos o limite levemente negativo apenas enquanto o modo segmentado está ativo.
        if (playerMove.staminaMinimaParaCorrer > limiteMinimoSegmentado)
            playerMove.staminaMinimaParaCorrer = limiteMinimoSegmentado;

        if (!destravarEnquantoHouverSegmentos)
            return;

        if (playerMove.StaminaSegmentosAtuais <= 0)
            return;

        DefinirCorridaBloqueada(false);
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
