using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Mostrador de energia segmentada no HUD.
/// Exemplo: 5/5, 4/5, 3/5, 2/5, 1/5, 0/5.
///
/// Nao altera a logica principal da stamina. Apenas converte a stamina atual/maxima
/// do PlayerMove em barras visuais para o HUD.
/// </summary>
[DisallowMultipleComponent]
public class MiniMarketEnergySegmentHUD : MonoBehaviour
{
    [Header("Referencias")]
    [Tooltip("Texto que mostrara 5/5, 4/5, etc.")]
    public Text textoEnergia;

    [Tooltip("PlayerMove do personagem. Se vazio, encontra automaticamente.")]
    public PlayerMove playerMove;

    [Header("Configuracao")]
    [Tooltip("Quantidade maxima de barras de energia.")]
    [Min(1)] public int barrasMaximas = 5;

    [Tooltip("Formato visual do texto. Use {0} para atual e {1} para maximo.")]
    public string formatoTexto = "{0}/{1}";

    [Tooltip("Quando ligado, 1% de energia ja mostra 1/5. Somente zero real mostra 0/5.")]
    public bool usarCeilParaNaoSumirBarraAntesDeZerar = true;

    [Tooltip("Atualiza mesmo se o texto estiver desativado. Normalmente deixe desligado.")]
    public bool atualizarMesmoDesativado = false;

    [Header("Atualizacao")]
    [Min(0.02f)] public float intervaloAtualizacao = 0.08f;

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

        int atual = CalcularBarrasAtuais();
        int maximo = Mathf.Max(1, barrasMaximas);

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
            Debug.LogWarning("[MiniMarketEnergySegmentHUD] PlayerMove nao encontrado para calcular energia 5/5.");
    }

    private int CalcularBarrasAtuais()
    {
        barrasMaximas = Mathf.Max(1, barrasMaximas);

        float atual = 0f;
        float maximo = 1f;

        if (playerMove != null)
        {
            atual = Mathf.Max(0f, playerMove.StaminaAtual);
            maximo = Mathf.Max(0.001f, playerMove.StaminaMaxima);
        }
        else
        {
            MiniMarketPlayerDatabase banco = MiniMarketPlayerDatabase.Instance;
            if (banco != null)
            {
                atual = Mathf.Max(0f, banco.StaminaAtual);
                maximo = Mathf.Max(0.001f, banco.StaminaMaxima);
            }
        }

        float normalizado = Mathf.Clamp01(atual / maximo);
        float valor = normalizado * barrasMaximas;

        int barras;

        if (usarCeilParaNaoSumirBarraAntesDeZerar)
            barras = normalizado <= 0.0001f ? 0 : Mathf.CeilToInt(valor);
        else
            barras = Mathf.RoundToInt(valor);

        return Mathf.Clamp(barras, 0, barrasMaximas);
    }
}
