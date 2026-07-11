using System;
using UnityEngine;

/// <summary>
/// Fachada compatível para nome e empresas do jogador.
/// Os dados reais pertencem ao MiniMarketPlayerDatabase.
/// </summary>
[DisallowMultipleComponent]
public class MiniMarketPlayerProfile : MonoBehaviour
{
    public static MiniMarketPlayerProfile Instance { get; private set; }

    [Header("Dados padrão")]
    public string nomePadrao = "Player";

    [Header("Banco de Dados")]
    public bool usarBancoDeDados = true;

    [Header("Debug")]
    public bool logarAlteracoes;

    private MiniMarketPlayerDatabase banco;
    private bool subscribed;

    public event Action OnDadosAlterados;

    public string NomePersonagem
    {
        get
        {
            ResolverBanco();
            return usarBancoDeDados && banco != null
                ? banco.NomePersonagem
                : (string.IsNullOrWhiteSpace(nomePadrao) ? "Player" : nomePadrao);
        }
        set
        {
            ResolverBanco();
            string novoNome = string.IsNullOrWhiteSpace(value) ? nomePadrao : value.Trim();

            if (usarBancoDeDados && banco != null)
                banco.DefinirNome(novoNome);

            DispararAlteracao();
        }
    }

    public int EmpresasCompradas
    {
        get
        {
            ResolverBanco();
            return usarBancoDeDados && banco != null ? banco.EmpresasCompradas : 0;
        }
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (transform.parent != null)
            transform.SetParent(null);

        DontDestroyOnLoad(gameObject);
        ResolverBanco();

        if (usarBancoDeDados && banco != null && string.IsNullOrWhiteSpace(banco.NomePersonagem))
            banco.DefinirNome(nomePadrao);
    }

    private void OnEnable()
    {
        if (Instance == null)
            Instance = this;

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

    public static MiniMarketPlayerProfile ObterOuCriar()
    {
        if (Instance != null)
            return Instance;

        if (!Application.isPlaying)
            return null;

        MiniMarketPlayerProfile encontrado =
            UnityEngine.Object.FindAnyObjectByType<MiniMarketPlayerProfile>(FindObjectsInactive.Include);

        if (encontrado != null)
        {
            Instance = encontrado;
            return encontrado;
        }

        GameObject go = new GameObject("MiniMarket_PlayerProfile");
        return go.AddComponent<MiniMarketPlayerProfile>();
    }

    public bool EmpresaJaComprada(string empresaId)
    {
        ResolverBanco();
        return usarBancoDeDados && banco != null && banco.EmpresaJaComprada(empresaId);
    }

    public bool RegistrarEmpresaComprada(string empresaId)
    {
        ResolverBanco();
        if (!usarBancoDeDados || banco == null)
            return false;

        bool registrou = banco.RegistrarEmpresaComprada(empresaId);
        DispararAlteracao();

        if (logarAlteracoes)
        {
            Debug.Log(
                "[PlayerProfile] " +
                (registrou ? "Empresa registrada: " : "Empresa já registrada: ") +
                empresaId,
                this
            );
        }

        return registrou;
    }

    public void DefinirEmpresasCompradasParaTeste(int quantidade)
    {
        ResolverBanco();
        if (banco == null)
            return;

        banco.ResetarEmpresasCompradas();
        quantidade = Mathf.Max(0, quantidade);

        for (int i = 0; i < quantidade; i++)
            banco.RegistrarEmpresaComprada("TESTE_EMPRESA_" + i);

        DispararAlteracao();
    }

    [ContextMenu("Resetar Perfil Completo")]
    public void ResetarPerfilCompleto()
    {
        ResolverBanco();
        if (banco != null)
            banco.ResetarBancoLocal();
        DispararAlteracao();
    }

    [ContextMenu("Resetar Empresas Compradas")]
    public void ResetarEmpresasCompradas()
    {
        ResolverBanco();
        if (banco != null)
            banco.ResetarEmpresasCompradas();
        DispararAlteracao();
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
        DispararAlteracao();
    }

    private void DispararAlteracao()
    {
        OnDadosAlterados?.Invoke();
    }
}
