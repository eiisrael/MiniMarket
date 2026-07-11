using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// HUD leve de energia segmentada.
/// Atualiza em intervalo controlado, nao procura PlayerMove a cada tick e so altera o Text quando o valor muda.
/// </summary>
[DisallowMultipleComponent]
public class MiniMarketEnergySegmentHUD : MonoBehaviour
{
    [Header("Referencias")]
    public Text textoEnergia;
    public PlayerMove playerMove;

    [Header("Configuracao")]
    [Min(1)] public int barrasMaximasFallback = 5;
    public string formatoTexto = "{0}/{1}";

    [Header("Atualizacao")]
    [Tooltip("0.20 e fluido visualmente e muito mais leve que atualizar a cada frame.")]
    [Min(0.05f)] public float intervaloAtualizacao = 0.20f;

    [Min(0.25f)] public float intervaloBuscaPlayer = 1f;
    public bool atualizarMesmoDesativado = false;

    [Header("Debug")]
    public bool logarSeNaoEncontrarPlayer = false;

    private float proximaAtualizacao;
    private float proximaBusca;
    private int ultimoAtual = -1;
    private int ultimoMaximo = -1;
    private bool ultimoFantasma;

    private void Awake()
    {
        ResolverReferencias(true);
        AtualizarTexto(true);
    }

    private void OnEnable()
    {
        ResolverReferencias(true);
        ultimoAtual = -1;
        ultimoMaximo = -1;
        AtualizarTexto(true);
    }

    private void Update()
    {
        if (!atualizarMesmoDesativado && textoEnergia != null && !textoEnergia.gameObject.activeInHierarchy)
            return;

        if (playerMove == null && Time.unscaledTime >= proximaBusca)
            ResolverReferencias(false);

        if (Time.unscaledTime < proximaAtualizacao)
            return;

        proximaAtualizacao = Time.unscaledTime + Mathf.Max(0.05f, intervaloAtualizacao);
        AtualizarTexto(false);
    }

    public void AtualizarTexto(bool forcar)
    {
        if (textoEnergia == null)
            textoEnergia = GetComponent<Text>();

        if (textoEnergia == null)
            return;

        bool fantasma = MiniMarketSegmentedStaminaRuntimeGuard.ForcarHudZeroNoSegmentoFantasma;
        int atual = fantasma ? 0 : CalcularSegmentosAtuais();
        int maximo = CalcularSegmentosMaximos();

        if (!forcar && atual == ultimoAtual && maximo == ultimoMaximo && fantasma == ultimoFantasma)
            return;

        ultimoAtual = atual;
        ultimoMaximo = maximo;
        ultimoFantasma = fantasma;
        textoEnergia.text = string.Format(formatoTexto, atual, maximo);
    }

    private void ResolverReferencias(bool forcar)
    {
        if (textoEnergia == null)
            textoEnergia = GetComponent<Text>();

        if (!forcar && playerMove != null)
            return;

        proximaBusca = Time.unscaledTime + Mathf.Max(0.25f, intervaloBuscaPlayer);
        playerMove = Object.FindFirstObjectByType<PlayerMove>(FindObjectsInactive.Include);

        if (playerMove == null && logarSeNaoEncontrarPlayer)
            Debug.LogWarning("[MiniMarketEnergySegmentHUD] PlayerMove nao encontrado para mostrar energia.");
    }

    private int CalcularSegmentosAtuais()
    {
        if (playerMove != null)
            return Mathf.Clamp(playerMove.StaminaSegmentosAtuais, 0, Mathf.Max(1, playerMove.StaminaSegmentosMaximos));

        return Mathf.Clamp(barrasMaximasFallback, 0, Mathf.Max(1, barrasMaximasFallback));
    }

    private int CalcularSegmentosMaximos()
    {
        return playerMove != null
            ? Mathf.Max(1, playerMove.StaminaSegmentosMaximos)
            : Mathf.Max(1, barrasMaximasFallback);
    }
}
