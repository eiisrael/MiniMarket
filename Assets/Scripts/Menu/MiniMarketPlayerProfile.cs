using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Dados permanentes simples do jogador enquanto ainda nao existe banco de dados.
/// Salva nome temporario e empresas compradas usando PlayerPrefs.
/// </summary>
[DisallowMultipleComponent]
public class MiniMarketPlayerProfile : MonoBehaviour
{
    public static MiniMarketPlayerProfile Instance { get; private set; }

    private const string KeyPlayerName = "MiniMarket.Player.Name";
    private const string KeyOwnedCompanies = "MiniMarket.Player.OwnedCompanies";

    [Header("Dados Temporarios")]
    [Tooltip("Nome temporario ate existir banco de dados/sistema de personagem.")]
    public string nomePadrao = "Player";

    [Header("Debug")]
    public bool logarAlteracoes = true;

    private readonly HashSet<string> empresasCompradas = new HashSet<string>();

    public event Action OnDadosAlterados;

    public string NomePersonagem
    {
        get
        {
            string nome = PlayerPrefs.GetString(KeyPlayerName, nomePadrao);
            return string.IsNullOrWhiteSpace(nome) ? nomePadrao : nome;
        }
        set
        {
            string novoNome = string.IsNullOrWhiteSpace(value) ? nomePadrao : value.Trim();
            PlayerPrefs.SetString(KeyPlayerName, novoNome);
            PlayerPrefs.Save();
            DispararAlteracao();
        }
    }

    public int EmpresasCompradas => empresasCompradas.Count;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        CarregarEmpresasCompradas();

        if (!PlayerPrefs.HasKey(KeyPlayerName))
        {
            PlayerPrefs.SetString(KeyPlayerName, nomePadrao);
            PlayerPrefs.Save();
        }
    }

    private void OnEnable()
    {
        if (Instance == null)
            Instance = this;
    }

    public static MiniMarketPlayerProfile ObterOuCriar()
    {
        if (Instance != null)
            return Instance;

        MiniMarketPlayerProfile encontrado = FindObjectOfType<MiniMarketPlayerProfile>();
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
        empresaId = NormalizarEmpresaId(empresaId);
        return !string.IsNullOrEmpty(empresaId) && empresasCompradas.Contains(empresaId);
    }

    public bool RegistrarEmpresaComprada(string empresaId)
    {
        empresaId = NormalizarEmpresaId(empresaId);

        if (string.IsNullOrEmpty(empresaId))
            return false;

        if (empresasCompradas.Contains(empresaId))
        {
            if (logarAlteracoes)
                Debug.Log("[MiniMarketPlayerProfile] Empresa ja registrada: " + empresaId);

            DispararAlteracao();
            return false;
        }

        empresasCompradas.Add(empresaId);
        SalvarEmpresasCompradas();
        DispararAlteracao();

        if (logarAlteracoes)
            Debug.Log("[MiniMarketPlayerProfile] Empresa comprada registrada: " + empresaId + " | Total: " + EmpresasCompradas);

        return true;
    }

    public void DefinirEmpresasCompradasParaTeste(int quantidade)
    {
        quantidade = Mathf.Max(0, quantidade);
        empresasCompradas.Clear();

        for (int i = 0; i < quantidade; i++)
            empresasCompradas.Add("TESTE_EMPRESA_" + i);

        SalvarEmpresasCompradas();
        DispararAlteracao();
    }

    [ContextMenu("Resetar Empresas Compradas")]
    public void ResetarEmpresasCompradas()
    {
        empresasCompradas.Clear();
        SalvarEmpresasCompradas();
        DispararAlteracao();
    }

    [ContextMenu("Resetar Perfil Completo")]
    public void ResetarPerfilCompleto()
    {
        empresasCompradas.Clear();
        PlayerPrefs.DeleteKey(KeyPlayerName);
        PlayerPrefs.DeleteKey(KeyOwnedCompanies);
        PlayerPrefs.Save();
        DispararAlteracao();
    }

    private void CarregarEmpresasCompradas()
    {
        empresasCompradas.Clear();

        string dados = PlayerPrefs.GetString(KeyOwnedCompanies, string.Empty);
        if (string.IsNullOrWhiteSpace(dados))
            return;

        string[] partes = dados.Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries);

        for (int i = 0; i < partes.Length; i++)
        {
            string id = NormalizarEmpresaId(partes[i]);
            if (!string.IsNullOrEmpty(id))
                empresasCompradas.Add(id);
        }
    }

    private void SalvarEmpresasCompradas()
    {
        string dados = string.Join("|", empresasCompradas);
        PlayerPrefs.SetString(KeyOwnedCompanies, dados);
        PlayerPrefs.Save();
    }

    private string NormalizarEmpresaId(string empresaId)
    {
        if (string.IsNullOrWhiteSpace(empresaId))
            return string.Empty;

        return empresaId.Trim().ToUpperInvariant();
    }

    private void DispararAlteracao()
    {
        OnDadosAlterados?.Invoke();
    }
}
