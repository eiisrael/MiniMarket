using System;
using System.Globalization;
using System.Reflection;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Controla o painel de menu do MiniMarket.
/// - ESC abre/fecha.
/// - Exibe nome temporario, gold, stamina em porcentagem e empresas compradas.
/// - Botao Gemas Gratis restaura stamina para 100%.
/// - Botao Close fecha o painel.
/// </summary>
[DisallowMultipleComponent]
public class MiniMarketMenuController : MonoBehaviour
{
    [Header("Painel")]
    [Tooltip("Arraste aqui o GameObject Menu. Se vazio, usa este GameObject.")]
    public GameObject painelMenu;

    [Tooltip("ESC abre e fecha o menu.")]
    public KeyCode teclaMenu = KeyCode.Escape;

    [Tooltip("Comecar com menu fechado.")]
    public bool iniciarFechado = true;

    [Tooltip("Atualiza os textos em tempo real enquanto o painel estiver aberto.")]
    public bool atualizarEmTempoReal = true;

    [Header("Textos do Menu")]
    public Text textoNome;
    public Text textoGold;
    public Text textoStamina;
    public Text textoEmpresas;

    [Tooltip("Se ligado, escreve 'Nome:', 'Gold:', etc junto com o valor no mesmo Text.")]
    public bool incluirRotulosNosTextos = true;

    [Header("Botoes")]
    public Button botaoClose;
    public Button botaoGemasGratis;

    [Header("Dados")]
    [Tooltip("Perfil permanente temporario. Se vazio, o script cria um automaticamente.")]
    public MiniMarketPlayerProfile perfil;

    [Tooltip("PlayerGold da cena. Se vazio, tenta encontrar automaticamente.")]
    public PlayerGold playerGold;

    [Tooltip("Script do player que possui stamina. Pode arrastar PlayerMoveHardcore2 ou outro script de stamina.")]
    public Component componenteStaminaOuMovimento;

    [Tooltip("Nome temporario ate criarmos banco de dados/personagem.")]
    public string nomeTemporario = "Player";

    [Header("Stamina - Leitura Automatica")]
    [Tooltip("Quando ligado, procura automaticamente um script com campos/metodos de stamina.")]
    public bool procurarComponenteStaminaAutomaticamente = true;

    [Tooltip("Valor exibido se nao encontrar stamina.")]
    public string staminaNaoEncontradaTexto = "--%";

    [Header("Cursor")]
    [Tooltip("Libera o cursor quando abre o menu para clicar nos botoes.")]
    public bool desbloquearCursorQuandoMenuAberto = true;

    [Tooltip("Trava o cursor novamente ao fechar o menu.")]
    public bool travarCursorAoFechar = true;

    [Header("Debug")]
    public bool logarEventos = true;

    private bool menuAberto;
    private readonly CultureInfo culturaBR = new CultureInfo("pt-BR");

    private void Awake()
    {
        if (painelMenu == null)
            painelMenu = gameObject;

        ResolverReferencias();
        ConectarBotoes();
    }

    private void Start()
    {
        ResolverReferencias();

        if (perfil != null && string.IsNullOrWhiteSpace(perfil.NomePersonagem))
            perfil.NomePersonagem = nomeTemporario;

        if (iniciarFechado)
            FecharMenu(false);
        else
            AbrirMenu(false);

        AtualizarTextos();
    }

    private void OnEnable()
    {
        if (perfil != null)
            perfil.OnDadosAlterados += AtualizarTextos;
    }

    private void OnDisable()
    {
        if (perfil != null)
            perfil.OnDadosAlterados -= AtualizarTextos;
    }

    private void Update()
    {
        if (Input.GetKeyDown(teclaMenu))
            AlternarMenu();

        if (menuAberto && atualizarEmTempoReal)
            AtualizarTextos();
    }

    public void AlternarMenu()
    {
        if (menuAberto)
            FecharMenu();
        else
            AbrirMenu();
    }

    public void AbrirMenu()
    {
        AbrirMenu(true);
    }

    public void FecharMenu()
    {
        FecharMenu(true);
    }

    private void AbrirMenu(bool logar)
    {
        ResolverReferenciasLeves();

        menuAberto = true;

        if (painelMenu != null)
            painelMenu.SetActive(true);

        AtualizarTextos();

        if (desbloquearCursorQuandoMenuAberto)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        if (logar && logarEventos)
            Debug.Log("[MiniMarketMenuController] Menu aberto.");
    }

    private void FecharMenu(bool logar)
    {
        menuAberto = false;

        if (painelMenu != null)
            painelMenu.SetActive(false);

        if (travarCursorAoFechar)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        if (logar && logarEventos)
            Debug.Log("[MiniMarketMenuController] Menu fechado.");
    }

    public void AtualizarTextos()
    {
        ResolverReferenciasLeves();

        AtualizarTextoNome();
        AtualizarTextoGold();
        AtualizarTextoStamina();
        AtualizarTextoEmpresas();
    }

    public void RecarregarStaminaComGemasGratis()
    {
        ResolverReferenciasLeves();

        bool sucesso = TentarRestaurarStaminaParaCemPorCento();
        AtualizarTextos();

        if (logarEventos)
        {
            if (sucesso)
                Debug.Log("[MiniMarketMenuController] Gemas Gratis: stamina restaurada para 100%.");
            else
                Debug.LogWarning("[MiniMarketMenuController] Gemas Gratis: nao encontrei campos/metodos de stamina para restaurar.");
        }
    }

    private void AtualizarTextoNome()
    {
        if (textoNome == null)
            return;

        string nome = perfil != null ? perfil.NomePersonagem : nomeTemporario;
        if (string.IsNullOrWhiteSpace(nome))
            nome = nomeTemporario;

        textoNome.text = incluirRotulosNosTextos ? "Nome: " + nome : nome;
    }

    private void AtualizarTextoGold()
    {
        if (textoGold == null)
            return;

        string goldTexto = FormatarNumeroInteiro(LerGoldAtual());
        textoGold.text = incluirRotulosNosTextos ? "Gold: " + goldTexto : goldTexto;
    }

    private void AtualizarTextoStamina()
    {
        if (textoStamina == null)
            return;

        float porcentagem = LerPorcentagemStamina();

        string valor = porcentagem >= 0f
            ? Mathf.RoundToInt(porcentagem).ToString(culturaBR) + "%"
            : staminaNaoEncontradaTexto;

        textoStamina.text = incluirRotulosNosTextos ? "Recarregando... " + valor : valor;
    }

    private void AtualizarTextoEmpresas()
    {
        if (textoEmpresas == null)
            return;

        int quantidade = perfil != null ? perfil.EmpresasCompradas : 0;
        textoEmpresas.text = incluirRotulosNosTextos ? "Empresas: " + quantidade : quantidade.ToString(culturaBR);
    }

    private long LerGoldAtual()
    {
        if (playerGold == null)
            return 0;

        object valor = LerMembroNumerico(
            playerGold,
            "GoldGlobal",
            "goldGlobal",
            "GoldAtual",
            "goldAtual",
            "Gold",
            "gold",
            "goldInicial"
        );

        return ConverterParaLong(valor);
    }

    private float LerPorcentagemStamina()
    {
        if (componenteStaminaOuMovimento == null)
            return -1f;

        object percentualDireto = LerMembroNumerico(
            componenteStaminaOuMovimento,
            "PercentualStamina",
            "percentualStamina",
            "StaminaPercentual",
            "staminaPercentual",
            "StaminaNormalizada",
            "staminaNormalizada"
        );

        if (percentualDireto != null)
        {
            float valor = ConverterParaFloat(percentualDireto);
            if (valor <= 1.01f)
                valor *= 100f;

            return Mathf.Clamp(valor, 0f, 100f);
        }

        object atual = LerMembroNumerico(
            componenteStaminaOuMovimento,
            "StaminaAtual",
            "staminaAtual",
            "CurrentStamina",
            "currentStamina",
            "Stamina",
            "stamina"
        );

        object maxima = LerMembroNumerico(
            componenteStaminaOuMovimento,
            "StaminaMaxima",
            "staminaMaxima",
            "MaxStamina",
            "maxStamina",
            "StaminaMax",
            "staminaMax",
            "staminaTotal"
        );

        if (atual == null || maxima == null)
            return -1f;

        float atualFloat = ConverterParaFloat(atual);
        float maximaFloat = ConverterParaFloat(maxima);

        if (maximaFloat <= 0.001f)
            return -1f;

        return Mathf.Clamp01(atualFloat / maximaFloat) * 100f;
    }

    private bool TentarRestaurarStaminaParaCemPorCento()
    {
        if (componenteStaminaOuMovimento == null)
            return false;

        bool chamouMetodo = TentarChamarMetodoSemParametro(
            componenteStaminaOuMovimento,
            "RecarregarStaminaCompleta",
            "RecarregarStaminaTotal",
            "RestaurarStaminaCompleta",
            "RestaurarStaminaTotal",
            "RestaurarTodaStamina",
            "RecuperarStaminaTotal",
            "EncherStamina",
            "ResetarStamina"
        );

        object maxima = LerMembroNumerico(
            componenteStaminaOuMovimento,
            "StaminaMaxima",
            "staminaMaxima",
            "MaxStamina",
            "maxStamina",
            "StaminaMax",
            "staminaMax",
            "staminaTotal"
        );

        if (maxima != null)
        {
            bool setou = TentarDefinirMembroNumerico(
                componenteStaminaOuMovimento,
                ConverterParaFloat(maxima),
                "StaminaAtual",
                "staminaAtual",
                "CurrentStamina",
                "currentStamina",
                "Stamina",
                "stamina"
            );

            return chamouMetodo || setou;
        }

        bool setouPercentual = TentarDefinirMembroNumerico(
            componenteStaminaOuMovimento,
            1f,
            "PercentualStamina",
            "percentualStamina",
            "StaminaPercentual",
            "staminaPercentual",
            "StaminaNormalizada",
            "staminaNormalizada"
        );

        return chamouMetodo || setouPercentual;
    }

    private void ResolverReferencias()
    {
        if (perfil == null)
            perfil = MiniMarketPlayerProfile.ObterOuCriar();

        if (playerGold == null)
            playerGold = PlayerGold.Instance != null ? PlayerGold.Instance : FindObjectOfType<PlayerGold>();

        ResolverComponenteStamina();
    }

    private void ResolverReferenciasLeves()
    {
        if (perfil == null)
            perfil = MiniMarketPlayerProfile.ObterOuCriar();

        if (playerGold == null)
            playerGold = PlayerGold.Instance;

        if (componenteStaminaOuMovimento == null)
            ResolverComponenteStamina();
    }

    private void ResolverComponenteStamina()
    {
        if (!procurarComponenteStaminaAutomaticamente || componenteStaminaOuMovimento != null)
            return;

        MonoBehaviour[] scripts = FindObjectsOfType<MonoBehaviour>(true);

        for (int i = 0; i < scripts.Length; i++)
        {
            MonoBehaviour script = scripts[i];
            if (script == null)
                continue;

            Type tipo = script.GetType();
            string nomeTipo = tipo.Name;

            if (nomeTipo.Contains("PlayerMoveHardcore") || nomeTipo.Contains("Stamina"))
            {
                if (PossuiAlgumMembro(tipo, "staminaAtual", "StaminaAtual", "stamina", "Stamina", "currentStamina", "CurrentStamina"))
                {
                    componenteStaminaOuMovimento = script;
                    return;
                }
            }
        }
    }

    private void ConectarBotoes()
    {
        if (botaoClose != null)
        {
            botaoClose.onClick.RemoveListener(FecharMenu);
            botaoClose.onClick.AddListener(FecharMenu);
        }

        if (botaoGemasGratis != null)
        {
            botaoGemasGratis.onClick.RemoveListener(RecarregarStaminaComGemasGratis);
            botaoGemasGratis.onClick.AddListener(RecarregarStaminaComGemasGratis);
        }
    }

    private bool PossuiAlgumMembro(Type tipo, params string[] nomes)
    {
        for (int i = 0; i < nomes.Length; i++)
        {
            if (tipo.GetField(nomes[i], BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic) != null)
                return true;

            if (tipo.GetProperty(nomes[i], BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic) != null)
                return true;

            if (tipo.GetMethod(nomes[i], BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic) != null)
                return true;
        }

        return false;
    }

    private object LerMembroNumerico(object alvo, params string[] nomes)
    {
        if (alvo == null)
            return null;

        Type tipo = alvo.GetType();
        BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        for (int i = 0; i < nomes.Length; i++)
        {
            FieldInfo field = tipo.GetField(nomes[i], flags);
            if (field != null && EhTipoNumerico(field.FieldType))
                return field.GetValue(alvo);

            PropertyInfo prop = tipo.GetProperty(nomes[i], flags);
            if (prop != null && prop.CanRead && EhTipoNumerico(prop.PropertyType))
                return prop.GetValue(alvo, null);

            MethodInfo method = tipo.GetMethod(nomes[i], flags, null, Type.EmptyTypes, null);
            if (method != null && EhTipoNumerico(method.ReturnType))
                return method.Invoke(alvo, null);
        }

        return null;
    }

    private bool TentarDefinirMembroNumerico(object alvo, float valor, params string[] nomes)
    {
        if (alvo == null)
            return false;

        Type tipo = alvo.GetType();
        BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        for (int i = 0; i < nomes.Length; i++)
        {
            FieldInfo field = tipo.GetField(nomes[i], flags);
            if (field != null && EhTipoNumerico(field.FieldType))
            {
                field.SetValue(alvo, ConverterParaTipo(valor, field.FieldType));
                return true;
            }

            PropertyInfo prop = tipo.GetProperty(nomes[i], flags);
            if (prop != null && prop.CanWrite && EhTipoNumerico(prop.PropertyType))
            {
                prop.SetValue(alvo, ConverterParaTipo(valor, prop.PropertyType), null);
                return true;
            }
        }

        return false;
    }

    private bool TentarChamarMetodoSemParametro(object alvo, params string[] nomes)
    {
        if (alvo == null)
            return false;

        Type tipo = alvo.GetType();
        BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        for (int i = 0; i < nomes.Length; i++)
        {
            MethodInfo method = tipo.GetMethod(nomes[i], flags, null, Type.EmptyTypes, null);
            if (method == null)
                continue;

            method.Invoke(alvo, null);
            return true;
        }

        return false;
    }

    private bool EhTipoNumerico(Type tipo)
    {
        return tipo == typeof(int) ||
               tipo == typeof(long) ||
               tipo == typeof(float) ||
               tipo == typeof(double) ||
               tipo == typeof(decimal) ||
               tipo == typeof(short) ||
               tipo == typeof(uint) ||
               tipo == typeof(ulong);
    }

    private object ConverterParaTipo(float valor, Type tipo)
    {
        if (tipo == typeof(int)) return Mathf.RoundToInt(valor);
        if (tipo == typeof(long)) return (long)Mathf.RoundToInt(valor);
        if (tipo == typeof(double)) return (double)valor;
        if (tipo == typeof(decimal)) return (decimal)valor;
        if (tipo == typeof(short)) return (short)Mathf.RoundToInt(valor);
        if (tipo == typeof(uint)) return (uint)Mathf.Max(0, Mathf.RoundToInt(valor));
        if (tipo == typeof(ulong)) return (ulong)Mathf.Max(0, Mathf.RoundToInt(valor));
        return valor;
    }

    private float ConverterParaFloat(object valor)
    {
        if (valor == null)
            return 0f;

        try
        {
            return Convert.ToSingle(valor, CultureInfo.InvariantCulture);
        }
        catch
        {
            return 0f;
        }
    }

    private long ConverterParaLong(object valor)
    {
        if (valor == null)
            return 0;

        try
        {
            return Convert.ToInt64(valor, CultureInfo.InvariantCulture);
        }
        catch
        {
            return 0;
        }
    }

    private string FormatarNumeroInteiro(long valor)
    {
        return valor.ToString("N0", culturaBR);
    }
}
