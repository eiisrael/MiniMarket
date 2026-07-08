using System;
using UnityEngine;

public class PlayerGold : MonoBehaviour
{
    public static PlayerGold Instance { get; private set; }

    [Header("Gold Global")]
    [Tooltip("Gold inicial do personagem quando nao existe banco de dados salvo ainda.")]
    [Min(0)]
    public int goldInicial = 20000;

    [SerializeField]
    [Min(0)]
    private int goldAtual;

    [Header("Banco de Dados")]
    [Tooltip("Se ligado, le e escreve o gold pelo MiniMarketPlayerDatabase.")]
    public bool usarBancoDeDados = true;

    [Tooltip("Se ativado, mantem o objeto entre cenas enquanto o jogo estiver aberto.")]
    public bool manterGoldEntreCenas = true;

    [Header("Inspector em Tempo Real")]
    [Tooltip("Permite alterar Gold Atual no Inspector durante o Play Mode e salvar no banco em tempo real.")]
    public bool permitirEditarGoldAtualNoInspector = true;

    [Tooltip("Fora do Play Mode, mantem Gold Atual igual ao Gold Inicial para facilitar configuracao.")]
    public bool sincronizarGoldAtualComInicialNoEditor = true;

    [Header("Debug")]
    public bool logarAlteracoes = false;

    private static bool goldGlobalInicializado;
    private static int goldGlobal;

    private int ultimoGoldSincronizado;
    private MiniMarketPlayerDatabase banco;

    public static int GoldGlobal => goldGlobal;
    public int GoldAtual => goldGlobal;

    public event Action<int> OnGoldAlterado;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetarStaticsAoEntrarNoPlay()
    {
        Instance = null;
        goldGlobalInicializado = false;
        goldGlobal = 0;
    }

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

        InicializarGold();
        SincronizarInspectorComGoldGlobal();
    }

    private void Start()
    {
        NotificarGoldAlterado(false);
    }

    private void Update()
    {
        if (!Application.isPlaying)
            return;

        if (!permitirEditarGoldAtualNoInspector)
            return;

        goldAtual = Mathf.Max(0, goldAtual);

        if (goldAtual != ultimoGoldSincronizado)
            DefinirGold(goldAtual);
    }

    private void OnEnable()
    {
        if (usarBancoDeDados)
        {
            banco = MiniMarketPlayerDatabase.ObterOuCriar();
            if (banco != null)
                banco.OnDatabaseChanged += AoBancoAlterado;
        }
    }

    private void OnDisable()
    {
        if (banco != null)
            banco.OnDatabaseChanged -= AoBancoAlterado;
    }

    private void InicializarGold()
    {
        if (usarBancoDeDados)
        {
            banco = MiniMarketPlayerDatabase.ObterOuCriar();
            banco.GarantirGoldInicial(goldInicial);
            goldGlobal = banco.GoldAtual;
            goldGlobalInicializado = true;
            return;
        }

        if (!goldGlobalInicializado)
        {
            goldGlobal = Mathf.Max(0, goldInicial);
            goldGlobalInicializado = true;
        }
    }

    private void AoBancoAlterado(MiniMarketPlayerDatabase.MiniMarketPlayerData dados)
    {
        if (dados == null)
            return;

        if (goldGlobal == dados.gold && goldAtual == dados.gold)
            return;

        goldGlobal = Mathf.Max(0, dados.gold);
        SincronizarInspectorComGoldGlobal();
        OnGoldAlterado?.Invoke(goldGlobal);
    }

    public void AdicionarGold(int quantidade)
    {
        if (quantidade <= 0)
            return;

        if (usarBancoDeDados)
        {
            banco = MiniMarketPlayerDatabase.ObterOuCriar();
            goldGlobal = banco.AdicionarGold(quantidade);
        }
        else
        {
            goldGlobal += quantidade;
        }

        NotificarGoldAlterado(!usarBancoDeDados);
    }

    public bool RemoverGold(int quantidade)
    {
        if (quantidade <= 0)
            return true;

        bool sucesso;

        if (usarBancoDeDados)
        {
            banco = MiniMarketPlayerDatabase.ObterOuCriar();
            sucesso = banco.RemoverGold(quantidade);
            goldGlobal = banco.GoldAtual;
        }
        else
        {
            if (goldGlobal < quantidade)
                return false;

            goldGlobal -= quantidade;
            sucesso = true;
        }

        if (sucesso)
            NotificarGoldAlterado(!usarBancoDeDados);

        return sucesso;
    }

    public bool TemGoldSuficiente(int quantidade)
    {
        if (usarBancoDeDados)
        {
            banco = MiniMarketPlayerDatabase.ObterOuCriar();
            return banco.TemGoldSuficiente(quantidade);
        }

        return goldGlobal >= quantidade;
    }

    public void DefinirGold(int novoValor)
    {
        goldGlobal = Mathf.Max(0, novoValor);

        if (usarBancoDeDados)
        {
            banco = MiniMarketPlayerDatabase.ObterOuCriar();
            goldGlobal = banco.DefinirGold(goldGlobal);
        }

        NotificarGoldAlterado(!usarBancoDeDados);
    }

    public void DefinirGoldInicial(int novoValor)
    {
        goldInicial = Mathf.Max(0, novoValor);
    }

    public void ZerarGold()
    {
        DefinirGold(0);
    }

    public void ResetarParaGoldInicial()
    {
        DefinirGold(goldInicial);
    }

    public string GoldFormatado()
    {
        return goldGlobal.ToString("N0");
    }

    private void NotificarGoldAlterado(bool salvarNoBanco)
    {
        goldGlobal = Mathf.Max(0, goldGlobal);
        SincronizarInspectorComGoldGlobal();

        if (usarBancoDeDados && salvarNoBanco)
        {
            banco = MiniMarketPlayerDatabase.ObterOuCriar();
            banco.DefinirGold(goldGlobal);
        }

        if (logarAlteracoes)
            Debug.Log("[PlayerGold] Gold atualizado: " + goldGlobal);

        OnGoldAlterado?.Invoke(goldGlobal);
    }

    private void SincronizarInspectorComGoldGlobal()
    {
        goldAtual = goldGlobal;
        ultimoGoldSincronizado = goldGlobal;
    }

    [ContextMenu("Gold/Resetar para Gold Inicial")]
    private void ContextResetarParaGoldInicial()
    {
        ResetarParaGoldInicial();
    }

    [ContextMenu("Gold/Zerar Gold")]
    private void ContextZerarGold()
    {
        ZerarGold();
    }

    [ContextMenu("Gold/Adicionar 1000")]
    private void ContextAdicionar1000()
    {
        AdicionarGold(1000);
    }

    [ContextMenu("Gold/Adicionar 20000")]
    private void ContextAdicionar20000()
    {
        AdicionarGold(20000);
    }

    private void OnValidate()
    {
        goldInicial = Mathf.Max(0, goldInicial);
        goldAtual = Mathf.Max(0, goldAtual);

        if (!Application.isPlaying && sincronizarGoldAtualComInicialNoEditor)
        {
            goldAtual = goldInicial;
            ultimoGoldSincronizado = goldAtual;
        }
    }
}
