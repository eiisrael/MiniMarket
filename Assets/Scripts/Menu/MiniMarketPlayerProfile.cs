using System;
using UnityEngine;

/// <summary>
/// Fachada de perfil do jogador.
/// Mantem compatibilidade com os scripts existentes, mas agora le/escreve no MiniMarketPlayerDatabase.
/// </summary>
[DisallowMultipleComponent]
public class MiniMarketPlayerProfile : MonoBehaviour
{
    public static MiniMarketPlayerProfile Instance { get; private set; }

    [Header("Dados Temporarios")]
    [Tooltip("Nome temporario ate existir login/conta online.")]
    public string nomePadrao = "Player";

    [Header("Banco de Dados")]
    public bool usarBancoDeDados = true;

    [Header("Debug")]
    public bool logarAlteracoes = true;

    private MiniMarketPlayerDatabase banco;

    public event Action OnDadosAlterados;

    public string NomePersonagem
    {
        get
        {
            ResolverBanco();

            if (usarBancoDeDados && banco != null)
                return banco.NomePersonagem;

            return string.IsNullOrWhiteSpace(nomePadrao) ? "Player" : nomePadrao;
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

        if (banco != null)
            banco.OnDatabaseChanged += AoBancoAlterado;
    }

    private void OnDisable()
    {
        if (banco != null)
            banco.OnDatabaseChanged -= AoBancoAlterado;
    }

    public static MiniMarketPlayerProfile ObterOuCriar()
    {
        if (Instance != null)
            return Instance;

        MiniMarketPlayerProfile encontrado = FindObjectOfType<MiniMarketPlayerProfile>(true);
        if (encontrado != null)
        {
            Instance = encontrado;
            return Instance;
        }

        GameObject go = new GameObject("MiniMarket_PlayerProfile");
        Instance = go.AddComponent<MiniMarketPlayerProfile>();
        return Instance;
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
            string msg = registrou ? "Empresa comprada registrada: " : "Empresa ja registrada: ";
            Debug.Log("[MiniMarketPlayerProfile] " + msg + empresaId + " | Total: " + EmpresasCompradas);
        }

        return registrou;
    }

    public void DefinirEmpresasCompradasParaTeste(int quantidade)
    {
        ResolverBanco();

        if (banco == null)
            return;

        banco.ResetarBancoLocal();

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
            banco.ResetarBancoLocal();

        DispararAlteracao();
    }

    private void ResolverBanco()
    {
        if (!usarBancoDeDados)
            return;

        if (banco == null)
            banco = MiniMarketPlayerDatabase.ObterOuCriar();
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
