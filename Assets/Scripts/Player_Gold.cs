using System;
using UnityEngine;

/// <summary>
/// Fachada de gold do jogador. O valor persistente pertence ao banco de dados.
/// Mantém a API antiga para os sistemas de compra e HUD.
/// </summary>
[DisallowMultipleComponent]
public class PlayerGold : MonoBehaviour
{
    public static PlayerGold Instance { get; private set; }

    [Header("Gold Global")]
    [Min(0)] public int goldInicial = 20000;
    [SerializeField, Min(0)] private int goldAtual;

    [Header("Banco de Dados")]
    public bool usarBancoDeDados = true;
    public bool manterGoldEntreCenas = true;

    [Header("Inspector em Tempo Real")]
    public bool permitirEditarGoldAtualNoInspector = true;
    public bool sincronizarGoldAtualComInicialNoEditor = true;

    [Header("Debug")]
    public bool logarAlteracoes;

    private static bool goldGlobalInicializado;
    private static int goldGlobal;

    private int ultimoGoldSincronizado;
    private MiniMarketPlayerDatabase banco;
    private bool subscribed;

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
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (manterGoldEntreCenas)
        {
            if (transform.parent != null)
                transform.SetParent(null);
            DontDestroyOnLoad(gameObject);
        }

        InicializarGold();
        SincronizarInspectorComGoldGlobal();
    }

    private void Start()
    {
        NotificarGoldAlterado(false);
    }

    private void OnEnable()
    {
        ResolverBanco();
        Subscribe();
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    private void OnDestroy()
    {
        Unsubscribe();
        if (Instance == this)
            Instance = null;
    }

    private void Update()
    {
        if (!Application.isPlaying || !permitirEditarGoldAtualNoInspector)
            return;

        goldAtual = Mathf.Max(0, goldAtual);
        if (goldAtual != ultimoGoldSincronizado)
            DefinirGold(goldAtual);
    }

    private void InicializarGold()
    {
        if (usarBancoDeDados)
        {
            ResolverBanco();
            if (banco != null)
            {
                banco.GarantirGoldInicial(goldInicial);
                goldGlobal = banco.GoldAtual;
                goldGlobalInicializado = true;
                return;
            }
        }

        if (!goldGlobalInicializado)
        {
            goldGlobal = Mathf.Max(0, goldInicial);
            goldGlobalInicializado = true;
        }
    }

    private void ResolverBanco()
    {
        if (!usarBancoDeDados || banco != null || !Application.isPlaying)
            return;

        banco = MiniMarketPlayerDatabase.ObterOuCriar();
    }

    private void Subscribe()
    {
        if (subscribed || banco == null)
            return;

        banco.OnDatabaseChanged += AoBancoAlterado;
        subscribed = true;
    }

    private void Unsubscribe()
    {
        if (!subscribed || banco == null)
            return;

        banco.OnDatabaseChanged -= AoBancoAlterado;
        subscribed = false;
    }

    private void AoBancoAlterado(MiniMarketPlayerDatabase.MiniMarketPlayerData dados)
    {
        if (dados == null)
            return;

        int novoValor = Mathf.Max(0, dados.gold);
        if (goldGlobal == novoValor && goldAtual == novoValor)
            return;

        goldGlobal = novoValor;
        SincronizarInspectorComGoldGlobal();
        OnGoldAlterado?.Invoke(goldGlobal);
    }

    public void AdicionarGold(int quantidade)
    {
        if (quantidade <= 0)
            return;

        ResolverBanco();
        goldGlobal = usarBancoDeDados && banco != null
            ? banco.AdicionarGold(quantidade)
            : goldGlobal + quantidade;

        NotificarGoldAlterado(false);
    }

    public bool RemoverGold(int quantidade)
    {
        if (quantidade <= 0)
            return true;

        ResolverBanco();
        bool sucesso;

        if (usarBancoDeDados && banco != null)
        {
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
            NotificarGoldAlterado(false);

        return sucesso;
    }

    public bool TemGoldSuficiente(int quantidade)
    {
        ResolverBanco();
        return usarBancoDeDados && banco != null
            ? banco.TemGoldSuficiente(quantidade)
            : goldGlobal >= Mathf.Max(0, quantidade);
    }

    public void DefinirGold(int novoValor)
    {
        goldGlobal = Mathf.Max(0, novoValor);
        ResolverBanco();

        if (usarBancoDeDados && banco != null)
            goldGlobal = banco.DefinirGold(goldGlobal);

        NotificarGoldAlterado(false);
    }

    public void DefinirGoldInicial(int novoValor)
    {
        goldInicial = Mathf.Max(0, novoValor);
    }

    public void ZerarGold() => DefinirGold(0);
    public void ResetarParaGoldInicial() => DefinirGold(goldInicial);
    public string GoldFormatado() => goldGlobal.ToString("N0");

    private void NotificarGoldAlterado(bool escreverBanco)
    {
        goldGlobal = Mathf.Max(0, goldGlobal);
        SincronizarInspectorComGoldGlobal();

        if (escreverBanco && usarBancoDeDados)
        {
            ResolverBanco();
            if (banco != null)
                goldGlobal = banco.DefinirGold(goldGlobal);
        }

        if (logarAlteracoes)
            Debug.Log("[PlayerGold] Gold atualizado: " + goldGlobal, this);

        OnGoldAlterado?.Invoke(goldGlobal);
    }

    private void SincronizarInspectorComGoldGlobal()
    {
        goldAtual = goldGlobal;
        ultimoGoldSincronizado = goldGlobal;
    }

    [ContextMenu("Gold/Resetar para Gold Inicial")]
    private void ContextResetarParaGoldInicial() => ResetarParaGoldInicial();

    [ContextMenu("Gold/Zerar Gold")]
    private void ContextZerarGold() => ZerarGold();

    [ContextMenu("Gold/Adicionar 1000")]
    private void ContextAdicionar1000() => AdicionarGold(1000);

    [ContextMenu("Gold/Adicionar 20000")]
    private void ContextAdicionar20000() => AdicionarGold(20000);

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
