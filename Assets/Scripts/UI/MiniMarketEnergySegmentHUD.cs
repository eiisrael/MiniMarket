using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Mostrador de energia segmentada no HUD.
/// Exemplo: 5/5, 4/5, 3/5, 2/5, 1/5, 0/5.
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
    [Min(0.02f)] public float intervaloAtualizacao = 0.08f;
    public bool atualizarMesmoDesativado = false;

    [Header("Debug")]
    public bool logarSeNaoEncontrarPlayer = false;

    private float proximaAtualizacao;
    private int ultimoAtual = -1;
    private int ultimoMaximo = -1;

    private void Awake()
    {
        ResolverReferencias();
        AtualizarTexto(true);
    }

    private void OnEnable()
    {
        ResolverReferencias();
        AtualizarTexto(true);
    }

    private void Update()
    {
        if (!atualizarMesmoDesativado && textoEnergia != null && !textoEnergia.gameObject.activeInHierarchy)
            return;

        if (Time.unscaledTime < proximaAtualizacao)
            return;

        proximaAtualizacao = Time.unscaledTime + intervaloAtualizacao;
        AtualizarTexto(false);
    }

    public void AtualizarTexto(bool forcar)
    {
        ResolverReferencias();

        if (textoEnergia == null)
            return;

        int atual = CalcularSegmentosAtuais();
        int maximo = CalcularSegmentosMaximos();

        if (!forcar && atual == ultimoAtual && maximo == ultimoMaximo)
            return;

        ultimoAtual = atual;
        ultimoMaximo = maximo;

        textoEnergia.text = string.Format(formatoTexto, atual, maximo);
    }

    private void ResolverReferencias()
    {
        if (textoEnergia == null)
            textoEnergia = GetComponent<Text>();

        if (playerMove == null)
            playerMove = FindObjectOfType<PlayerMove>(true);

        if (playerMove == null && logarSeNaoEncontrarPlayer)
            Debug.LogWarning("[MiniMarketEnergySegmentHUD] PlayerMove nao encontrado para mostrar energia 5/5.");
    }

    private int CalcularSegmentosAtuais()
    {
        if (MiniMarketSegmentedStaminaRuntimeGuard.ForcarHudZeroNoSegmentoFantasma)
            return 0;

        if (playerMove != null)
            return Mathf.Clamp(playerMove.StaminaSegmentosAtuais, 0, Mathf.Max(1, playerMove.StaminaSegmentosMaximos));

        return Mathf.Clamp(barrasMaximasFallback, 0, Mathf.Max(1, barrasMaximasFallback));
    }

    private int CalcularSegmentosMaximos()
    {
        if (playerMove != null)
            return Mathf.Max(1, playerMove.StaminaSegmentosMaximos);

        return Mathf.Max(1, barrasMaximasFallback);
    }
}