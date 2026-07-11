using UnityEngine;
using UnityEngine.UI;

public class GoldHUD : MonoBehaviour
{
    [Header("Referências")]
    public PlayerGold playerGold;

    [Tooltip("Texto que mostra o gold. Pode ser UI > Legacy > Text.")]
    public Text textoGold;

    [Tooltip("Opcional: CanvasGroup do GoldHUD.")]
    public CanvasGroup canvasGroup;

    [Header("Configuração")]
    public bool procurarPlayerAutomaticamente = true;

    [Tooltip("Prefixo antes do número. Exemplo: Gold: ou vazio.")]
    public string prefixo = "M$: ";

    [Header("Animação")]
    [Tooltip("Anima o número subindo/descendo até o valor real.")]
    public bool animarNumero = true;

    [Min(0.1f)]
    public float velocidadeAnimacaoNumero = 12f;

    [Min(0.1f)]
    public float velocidadeFade = 8f;

    [Range(0f, 1f)]
    public float alphaNormal = 1f;

    private float goldVisual;
    private int goldReal;

    private void Awake()
    {
        if (playerGold == null && procurarPlayerAutomaticamente)
            playerGold = FindObjectOfType<PlayerGold>();

        if (canvasGroup == null)
            canvasGroup = GetComponent<CanvasGroup>();

        if (playerGold != null)
        {
            goldReal = playerGold.GoldAtual;
            goldVisual = goldReal;
        }

        AtualizarTextoInstantaneo();
    }

    private void OnEnable()
    {
        TentarRegistrarEvento();
    }

    private void OnDisable()
    {
        if (playerGold != null)
            playerGold.OnGoldAlterado -= ReceberGoldAlterado;
    }

    private void Update()
    {
        if (playerGold == null)
        {
            if (procurarPlayerAutomaticamente)
            {
                playerGold = FindObjectOfType<PlayerGold>();
                TentarRegistrarEvento();
            }

            return;
        }

        if (animarNumero)
        {
            float suavizacao = CalcularSuavizacao(
                velocidadeAnimacaoNumero,
                Time.deltaTime
            );

            goldVisual = Mathf.Lerp(
                goldVisual,
                goldReal,
                suavizacao
            );

            if (Mathf.Abs(goldVisual - goldReal) < 0.5f)
                goldVisual = goldReal;

            AtualizarTexto(Mathf.RoundToInt(goldVisual));
        }
        else
        {
            AtualizarTexto(goldReal);
        }

        AtualizarFade();
    }

    private void TentarRegistrarEvento()
    {
        if (playerGold == null)
            return;

        playerGold.OnGoldAlterado -= ReceberGoldAlterado;
        playerGold.OnGoldAlterado += ReceberGoldAlterado;

        goldReal = playerGold.GoldAtual;
        goldVisual = goldReal;

        AtualizarTextoInstantaneo();
    }

    private void ReceberGoldAlterado(int novoGold)
    {
        goldReal = novoGold;

        if (!animarNumero)
            goldVisual = goldReal;
    }

    private void AtualizarTextoInstantaneo()
    {
        if (textoGold == null)
            return;

        AtualizarTexto(goldReal);
    }

    private void AtualizarTexto(int valor)
    {
        if (textoGold == null)
            return;

        textoGold.text = prefixo + valor.ToString("N0");
    }

    private void AtualizarFade()
    {
        if (canvasGroup == null)
            return;

        float suavizacao = CalcularSuavizacao(
            velocidadeFade,
            Time.deltaTime
        );

        canvasGroup.alpha = Mathf.Lerp(
            canvasGroup.alpha,
            alphaNormal,
            suavizacao
        );
    }

    private float CalcularSuavizacao(float velocidade, float deltaTime)
    {
        return 1f - Mathf.Exp(-velocidade * deltaTime);
    }
}