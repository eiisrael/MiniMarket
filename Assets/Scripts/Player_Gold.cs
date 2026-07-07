using System;
using UnityEngine;

public class PlayerGold : MonoBehaviour
{
    public static PlayerGold Instance { get; private set; }

    [Header("Gold Global")]
    [Tooltip("Gold inicial do personagem ao começar o jogo.")]
    [Min(0)]
    public int goldInicial;

    [SerializeField]
    private int goldAtual;

    [Header("Configuração")]
    [Tooltip("Se ativado, mantém o gold global entre cenas enquanto o jogo estiver aberto.")]
    public bool manterGoldEntreCenas = true;

    private static bool goldGlobalInicializado;
    private static int goldGlobal;

    public static int GoldGlobal => goldGlobal;
    public int GoldAtual => goldGlobal;

    public event Action<int> OnGoldAlterado;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            if (manterGoldEntreCenas)
            {
                Destroy(gameObject);
                return;
            }
        }

        Instance = this;

        if (manterGoldEntreCenas)
            DontDestroyOnLoad(gameObject);

        if (!goldGlobalInicializado)
        {
            goldGlobal = Mathf.Max(0, goldInicial);
            goldGlobalInicializado = true;
        }

        goldAtual = goldGlobal;
    }

    private void Start()
    {
        NotificarGoldAlterado();
    }

    public void AdicionarGold(int quantidade)
    {
        if (quantidade <= 0)
            return;

        goldGlobal += quantidade;
        goldAtual = goldGlobal;

        NotificarGoldAlterado();
    }

    public bool RemoverGold(int quantidade)
    {
        if (quantidade <= 0)
            return true;

        if (goldGlobal < quantidade)
            return false;

        goldGlobal -= quantidade;
        goldAtual = goldGlobal;

        NotificarGoldAlterado();

        return true;
    }

    public bool TemGoldSuficiente(int quantidade)
    {
        return goldGlobal >= quantidade;
    }

    public void DefinirGold(int novoValor)
    {
        goldGlobal = Mathf.Max(0, novoValor);
        goldAtual = goldGlobal;

        NotificarGoldAlterado();
    }

    public void ZerarGold()
    {
        goldGlobal = 0;
        goldAtual = goldGlobal;

        NotificarGoldAlterado();
    }

    public string GoldFormatado()
    {
        return goldGlobal.ToString("N0");
    }

    private void NotificarGoldAlterado()
    {
        goldAtual = goldGlobal;

        if (OnGoldAlterado != null)
            OnGoldAlterado.Invoke(goldGlobal);
    }

    private void OnValidate()
    {
        goldInicial = Mathf.Max(0, goldInicial);
        goldAtual = Mathf.Max(0, goldAtual);
    }
}