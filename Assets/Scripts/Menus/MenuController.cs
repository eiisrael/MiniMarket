using System;
using System.Globalization;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Controla o painel de menu do MiniMarket.
/// - TAB abre/fecha por padrao.
/// - Exibe Nome, Gold, Energia/Stamina em porcentagem e Empresas.
/// - Botao Gemas Gratis restaura Energia/Stamina para 100%.
/// - Botao Close fecha o painel.
/// - Mantem cursor livre quando o menu esta aberto, mesmo se outro script tentar travar o mouse.
/// </summary>
[DefaultExecutionOrder(10000)]
[DisallowMultipleComponent]
public class MenuController : MonoBehaviour
{
    public enum ModoOcultarPainel
    {
        CanvasGroup,
        SetActive
    }

    [Header("Painel")]
    [Tooltip("Arraste aqui o GameObject Menu. Se vazio, usa este GameObject.")]
    public GameObject painelMenu;

    [Tooltip("Tecla que abre e fecha o menu.")]
    public KeyCode teclaMenu = KeyCode.Tab;

    [Tooltip("Comecar com menu fechado.")]
    public bool iniciarFechado = true;

    [Tooltip("Atualiza os textos em tempo real enquanto o painel estiver aberto.")]
    public bool atualizarEmTempoReal = true;

    [Tooltip("CanvasGroup e mais seguro se este script estiver no proprio Menu.")]
    public ModoOcultarPainel modoOcultarPainel = ModoOcultarPainel.CanvasGroup;

    [Tooltip("Se usar CanvasGroup, cria automaticamente quando nao existir.")]
    public bool criarCanvasGroupAutomaticamente = true;

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

    [Tooltip("Captura clique nos botoes manualmente tambem, caso o Button.onClick nao dispare por conflito de cursor/camera.")]
    public bool usarCliqueManualDeSeguranca = true;

    [Header("Dados")]
    public PlayerProfile perfil;
    public PlayerGold playerGold;

    [Tooltip("Arraste aqui o SCRIPT que possui stamina/energia. Nao arraste o Transform. Se errar, o script tenta corrigir sozinho.")]
    public Component componenteStaminaOuMovimento;

    public string nomeTemporario = "Player";

    [Header("Energia / Stamina")]
    [Tooltip("Quando ligado, procura automaticamente um script com campos/metodos de stamina ou energia.")]
    public bool procurarComponenteStaminaAutomaticamente = true;

    [Tooltip("Texto exibido quando nao encontrar o script/valor de energia.")]
    public string energiaNaoEncontradaTexto = "--%";

    [Tooltip("Texto base antes da porcentagem.")]
    public string rotuloEnergia = "Energia";

    [Tooltip("Ao clicar em Gemas Gratis, tenta restaurar Energia/Stamina para o maximo.")]
    public bool gemasGratisRecarregaEnergia = true;

    [Header("Cursor")]
    public bool desbloquearCursorQuandoMenuAberto = true;
    public bool manterCursorLivreEnquantoAberto = true;
    public bool travarCursorAoFechar = true;

    [Header("Debug")]
    public bool logarEventos = true;
    public bool logarComponenteEnergiaEncontrado = true;

    private bool menuAberto;
    private CanvasGroup canvasGroupMenu;
    private readonly CultureInfo culturaBR = new CultureInfo("pt-BR");
    private int ultimoFrameCliqueManual = -100;

    private static readonly string[] nomesPercentualEnergia =
    {
        "PercentualStamina", "percentualStamina",
        "StaminaPercentual", "staminaPercentual",
        "StaminaNormalizada", "staminaNormalizada",
        "PercentualEnergia", "percentualEnergia",
        "EnergiaPercentual", "energiaPercentual",
        "EnergiaNormalizada", "energiaNormalizada"
    };

    private static readonly string[] nomesEnergiaAtual =
    {
        "StaminaAtual", "staminaAtual",
        "CurrentStamina", "currentStamina",
        "Stamina", "stamina",
        "EnergiaAtual", "energiaAtual",
        "CurrentEnergy", "currentEnergy",
        "Energia", "energia",
        "energy", "Energy"
    };

    private static readonly string[] nomesEnergiaMaxima =
    {
        "StaminaMaxima", "staminaMaxima",
        "MaxStamina", "maxStamina",
        "StaminaMax", "staminaMax",
        "staminaTotal", "StaminaTotal",
        "EnergiaMaxima", "energiaMaxima",
        "MaxEnergia", "maxEnergia",
        "EnergiaMax", "energiaMax",
        "energiaTotal", "EnergiaTotal",
        "MaxEnergy", "maxEnergy"
    };

    private static readonly string[] metodosRestaurarEnergia =
    {
        "RecarregarStaminaCompleta",
        "RecarregarStaminaTotal",
        "RestaurarStaminaCompleta",
        "RestaurarStaminaTotal",
        "RestaurarTodaStamina",
        "RecuperarStaminaTotal",
        "EncherStamina",
        "ResetarStamina",
        "RecarregarEnergiaCompleta",
        "RecarregarEnergiaTotal",
        "RestaurarEnergiaCompleta",
        "RestaurarEnergiaTotal",
        "RestaurarTodaEnergia",
        "RecuperarEnergiaTotal",
        "EncherEnergia",
        "ResetarEnergia"
    };

    private void Awake()
    {
        if (painelMenu == null)
            painelMenu = gameObject;

        ResolverCanvasGroup();
        ResolverReferencias();
        ConectarBotoes();
    }

    private void Start()
    {
        ResolverCanvasGroup();
        ResolverReferencias();
        ConectarBotoes();

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
        if (perfil == null)
            perfil = PlayerProfile.ObterOuCriar();

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

        if (!menuAberto)
            return;

        if (manterCursorLivreEnquantoAberto)
            LiberarCursor();

        if (usarCliqueManualDeSeguranca)
            VerificarCliqueManualNosBotoes();

        if (atualizarEmTempoReal)
            AtualizarTextos();
    }

    private void LateUpdate()
    {
        if (menuAberto && manterCursorLivreEnquantoAberto)
            LiberarCursor();
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
        ResolverCanvasGroup();
        ResolverReferenciasLeves();
        ConectarBotoes();

        menuAberto = true;
        AplicarVisibilidadePainel(true);
        AtualizarTextos();

        if (desbloquearCursorQuandoMenuAberto)
            LiberarCursor();

        if (logar && logarEventos)
            Debug.Log("[MenuController] Menu aberto pela tecla: " + teclaMenu);
    }

    private void FecharMenu(bool logar)
    {
        menuAberto = false;
        AplicarVisibilidadePainel(false);

        if (travarCursorAoFechar)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        if (logar && logarEventos)
            Debug.Log("[MenuController] Menu fechado.");
    }

    private void AplicarVisibilidadePainel(bool visivel)
    {
        if (painelMenu == null)
            return;

        if (modoOcultarPainel == ModoOcultarPainel.SetActive)
        {
            painelMenu.SetActive(visivel);
            return;
        }

        ResolverCanvasGroup();

        if (!painelMenu.activeSelf)
            painelMenu.SetActive(true);

        if (canvasGroupMenu != null)
        {
            canvasGroupMenu.alpha = visivel ? 1f : 0f;
            canvasGroupMenu.interactable = visivel;
            canvasGroupMenu.blocksRaycasts = visivel;
        }
        else
        {
            painelMenu.SetActive(visivel);
        }
    }

    private void ResolverCanvasGroup()
    {
        if (painelMenu == null)
            return;

        if (canvasGroupMenu != null)
            return;

        canvasGroupMenu = painelMenu.GetComponent<CanvasGroup>();

        if (canvasGroupMenu == null && criarCanvasGroupAutomaticamente)
            canvasGroupMenu = painelMenu.AddComponent<CanvasGroup>();
    }

    private void VerificarCliqueManualNosBotoes()
    {
        if (!Input.GetMouseButtonDown(0))
            return;

        if (Time.frameCount == ultimoFrameCliqueManual)
            return;

        if (PonteiroSobreBotao(botaoClose))
        {
            ultimoFrameCliqueManual = Time.frameCount;
            FecharMenu();
            return;
        }

        if (PonteiroSobreBotao(botaoGemasGratis))
        {
            ultimoFrameCliqueManual = Time.frameCount;
            RecarregarEnergiaComGemasGratis();
        }
    }

    private bool PonteiroSobreBotao(Button botao)
    {
        if (botao == null || !botao.gameObject.activeInHierarchy)
            return false;

        RectTransform rect = botao.GetComponent<RectTransform>();
        if (rect == null)
            return false;

        Canvas canvas = botao.GetComponentInParent<Canvas>();
        Camera cameraUI = null;

        if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
            cameraUI = canvas.worldCamera != null ? canvas.worldCamera : Camera.main;

        return RectTransformUtility.RectangleContainsScreenPoint(rect, Input.mousePosition, cameraUI);
    }

    public void AtualizarTextos()
    {
        ResolverReferenciasLeves();

        AtualizarTextoNome();
        AtualizarTextoGold();
        AtualizarTextoEnergia();
        AtualizarTextoEmpresas();
    }

    public void RecarregarStaminaComGemasGratis()
    {
        RecarregarEnergiaComGemasGratis();
    }

    public void RecarregarEnergiaComGemasGratis()
    {
        if (!gemasGratisRecarregaEnergia)
            return;

        ResolverReferenciasLeves();

        bool sucesso = TentarRestaurarEnergiaParaCemPorCento();
        AtualizarTextos();

        if (menuAberto && manterCursorLivreEnquantoAberto)
            LiberarCursor();

        if (logarEventos)
        {
            if (sucesso)
                Debug.Log("[MenuController] Gemas Gratis: energia/stamina restaurada para 100%.");
            else
                Debug.LogWarning("[MenuController] Gemas Gratis: nao encontrei campos/metodos de energia/stamina para restaurar.");
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

    private void AtualizarTextoEnergia()
    {
        if (textoStamina == null)
            return;

        float porcentagem = LerPorcentagemEnergia();

        string valor = porcentagem >= 0f
            ? Mathf.RoundToInt(porcentagem).ToString(culturaBR) + "%"
            : energiaNaoEncontradaTexto;

        textoStamina.text = incluirRotulosNosTextos ? rotuloEnergia + ": " + valor : valor;
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

        object valor = LerMembroNumerico(playerGold,
            "GoldGlobal", "goldGlobal",
            "GoldAtual", "goldAtual",
            "Gold", "gold",
            "goldInicial");

        return ConverterParaLong(valor);
    }

    private float LerPorcentagemEnergia()
    {
        GarantirComponenteEnergiaValido();

        if (componenteStaminaOuMovimento == null)
            return -1f;

        object percentualDireto = LerMembroNumerico(componenteStaminaOuMovimento, nomesPercentualEnergia);

        if (percentualDireto != null)
        {
            float valor = ConverterParaFloat(percentualDireto);
            if (valor <= 1.01f)
                valor *= 100f;

            return Mathf.Clamp(valor, 0f, 100f);
        }

        object atual = LerMembroNumerico(componenteStaminaOuMovimento, nomesEnergiaAtual);
        object maxima = LerMembroNumerico(componenteStaminaOuMovimento, nomesEnergiaMaxima);

        if (atual == null)
            atual = ProcurarMembroNumericoPorPalavras(componenteStaminaOuMovimento, true, "stamina", "energia", "energy");

        if (maxima == null)
            maxima = ProcurarMembroNumericoPorPalavras(componenteStaminaOuMovimento, false, "stamina", "energia", "energy");

        if (atual == null || maxima == null)
            return -1f;

        float atualFloat = ConverterParaFloat(atual);
        float maximaFloat = ConverterParaFloat(maxima);

        if (maximaFloat <= 0.001f)
            return -1f;

        return Mathf.Clamp01(atualFloat / maximaFloat) * 100f;
    }

    private bool TentarRestaurarEnergiaParaCemPorCento()
    {
        GarantirComponenteEnergiaValido();

        if (componenteStaminaOuMovimento == null)
            return false;

        bool chamouMetodo = TentarChamarMetodoSemParametro(componenteStaminaOuMovimento, metodosRestaurarEnergia);

        object maxima = LerMembroNumerico(componenteStaminaOuMovimento, nomesEnergiaMaxima);

        if (maxima == null)
            maxima = ProcurarMembroNumericoPorPalavras(componenteStaminaOuMovimento, false, "stamina", "energia", "energy");

        if (maxima != null)
        {
            bool setou = TentarDefinirMembroNumerico(componenteStaminaOuMovimento, ConverterParaFloat(maxima), nomesEnergiaAtual);

            if (!setou)
                setou = TentarDefinirMembroNumericoPorPalavras(componenteStaminaOuMovimento, ConverterParaFloat(maxima), true, "stamina", "energia", "energy");

            return chamouMetodo || setou;
        }

        bool setouPercentual = TentarDefinirMembroNumerico(componenteStaminaOuMovimento, 1f, nomesPercentualEnergia);
        return chamouMetodo || setouPercentual;
    }

    private void ResolverReferencias()
    {
        if (perfil == null)
            perfil = PlayerProfile.ObterOuCriar();

        if (playerGold == null)
            playerGold = PlayerGold.Instance != null ? PlayerGold.Instance : FindObjectOfType<PlayerGold>();

        GarantirComponenteEnergiaValido();
    }

    private void ResolverReferenciasLeves()
    {
        if (perfil == null)
            perfil = PlayerProfile.ObterOuCriar();

        if (playerGold == null)
            playerGold = PlayerGold.Instance != null ? PlayerGold.Instance : FindObjectOfType<PlayerGold>();

        GarantirComponenteEnergiaValido();
    }

    private void GarantirComponenteEnergiaValido()
    {
        if (ComponenteEnergiaEhValido(componenteStaminaOuMovimento))
            return;

        if (!procurarComponenteStaminaAutomaticamente)
            return;

        Component encontrado = EncontrarMelhorComponenteEnergia();

        if (encontrado != null)
        {
            componenteStaminaOuMovimento = encontrado;

            if (logarComponenteEnergiaEncontrado)
                Debug.Log("[MenuController] Componente de energia/stamina encontrado: " + encontrado.GetType().Name + " em " + encontrado.gameObject.name);
        }
    }

    private bool ComponenteEnergiaEhValido(Component componente)
    {
        if (componente == null)
            return false;

        if (componente is Transform)
            return false;

        Type tipo = componente.GetType();

        if (PossuiAlgumMembro(tipo, nomesPercentualEnergia))
            return true;

        if (PossuiAlgumMembro(tipo, nomesEnergiaAtual) && PossuiAlgumMembro(tipo, nomesEnergiaMaxima))
            return true;

        return TipoTemMembroComPalavras(tipo, "stamina", "energia", "energy");
    }

    private Component EncontrarMelhorComponenteEnergia()
    {
        MonoBehaviour[] scripts = FindObjectsOfType<MonoBehaviour>(true);

        Component melhor = null;
        int melhorPontuacao = -1;

        for (int i = 0; i < scripts.Length; i++)
        {
            MonoBehaviour script = scripts[i];
            if (script == null || script == this)
                continue;

            if (!ComponenteEnergiaEhValido(script))
                continue;

            int pontos = CalcularPontuacaoComponenteEnergia(script);

            if (pontos > melhorPontuacao)
            {
                melhorPontuacao = pontos;
                melhor = script;
            }
        }

        return melhor;
    }

    private int CalcularPontuacaoComponenteEnergia(Component componente)
    {
        int pontos = 0;
        string nomeTipo = componente.GetType().Name.ToLowerInvariant();
        string nomeObjeto = componente.gameObject.name.ToLowerInvariant();

        if (nomeTipo.Contains("player")) pontos += 30;
        if (nomeTipo.Contains("move")) pontos += 25;
        if (nomeTipo.Contains("stamina")) pontos += 20;
        if (nomeTipo.Contains("energia")) pontos += 20;
        if (nomeTipo.Contains("hud")) pontos -= 20;
        if (nomeObjeto.Contains("character")) pontos += 20;
        if (nomeObjeto.Contains("player")) pontos += 20;

        return pontos;
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
            botaoGemasGratis.onClick.RemoveListener(RecarregarEnergiaComGemasGratis);
            botaoGemasGratis.onClick.RemoveListener(RecarregarStaminaComGemasGratis);
            botaoGemasGratis.onClick.AddListener(RecarregarEnergiaComGemasGratis);
        }
    }

    private void LiberarCursor()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private bool PossuiAlgumMembro(Type tipo, params string[] nomes)
    {
        if (tipo == null || nomes == null)
            return false;

        BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        for (int i = 0; i < nomes.Length; i++)
        {
            if (tipo.GetField(nomes[i], flags) != null)
                return true;

            if (tipo.GetProperty(nomes[i], flags) != null)
                return true;

            if (tipo.GetMethod(nomes[i], flags) != null)
                return true;
        }

        return false;
    }

    private bool TipoTemMembroComPalavras(Type tipo, params string[] palavras)
    {
        BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        FieldInfo[] fields = tipo.GetFields(flags);
        for (int i = 0; i < fields.Length; i++)
        {
            if (EhTipoNumerico(fields[i].FieldType) && NomeContemAlgumaPalavra(fields[i].Name, palavras))
                return true;
        }

        PropertyInfo[] props = tipo.GetProperties(flags);
        for (int i = 0; i < props.Length; i++)
        {
            if (props[i].CanRead && EhTipoNumerico(props[i].PropertyType) && NomeContemAlgumaPalavra(props[i].Name, palavras))
                return true;
        }

        return false;
    }

    private object LerMembroNumerico(object alvo, params string[] nomes)
    {
        if (alvo == null || nomes == null)
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

    private object ProcurarMembroNumericoPorPalavras(object alvo, bool atual, params string[] palavras)
    {
        if (alvo == null)
            return null;

        Type tipo = alvo.GetType();
        BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        FieldInfo[] fields = tipo.GetFields(flags);
        for (int i = 0; i < fields.Length; i++)
        {
            FieldInfo field = fields[i];
            if (!EhTipoNumerico(field.FieldType))
                continue;

            if (!NomeContemAlgumaPalavra(field.Name, palavras))
                continue;

            bool pareceMaximo = NomePareceMaximo(field.Name);
            if (atual && pareceMaximo)
                continue;

            if (!atual && !pareceMaximo)
                continue;

            return field.GetValue(alvo);
        }

        PropertyInfo[] props = tipo.GetProperties(flags);
        for (int i = 0; i < props.Length; i++)
        {
            PropertyInfo prop = props[i];
            if (!prop.CanRead || !EhTipoNumerico(prop.PropertyType))
                continue;

            if (!NomeContemAlgumaPalavra(prop.Name, palavras))
                continue;

            bool pareceMaximo = NomePareceMaximo(prop.Name);
            if (atual && pareceMaximo)
                continue;

            if (!atual && !pareceMaximo)
                continue;

            return prop.GetValue(alvo, null);
        }

        return null;
    }

    private bool TentarDefinirMembroNumerico(object alvo, float valor, params string[] nomes)
    {
        if (alvo == null || nomes == null)
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

    private bool TentarDefinirMembroNumericoPorPalavras(object alvo, float valor, bool atual, params string[] palavras)
    {
        if (alvo == null)
            return false;

        Type tipo = alvo.GetType();
        BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        FieldInfo[] fields = tipo.GetFields(flags);
        for (int i = 0; i < fields.Length; i++)
        {
            FieldInfo field = fields[i];
            if (!EhTipoNumerico(field.FieldType))
                continue;

            if (!NomeContemAlgumaPalavra(field.Name, palavras))
                continue;

            bool pareceMaximo = NomePareceMaximo(field.Name);
            if (atual && pareceMaximo)
                continue;

            if (!atual && !pareceMaximo)
                continue;

            field.SetValue(alvo, ConverterParaTipo(valor, field.FieldType));
            return true;
        }

        PropertyInfo[] props = tipo.GetProperties(flags);
        for (int i = 0; i < props.Length; i++)
        {
            PropertyInfo prop = props[i];
            if (!prop.CanWrite || !EhTipoNumerico(prop.PropertyType))
                continue;

            if (!NomeContemAlgumaPalavra(prop.Name, palavras))
                continue;

            bool pareceMaximo = NomePareceMaximo(prop.Name);
            if (atual && pareceMaximo)
                continue;

            if (!atual && !pareceMaximo)
                continue;

            prop.SetValue(alvo, ConverterParaTipo(valor, prop.PropertyType), null);
            return true;
        }

        return false;
    }

    private bool TentarChamarMetodoSemParametro(object alvo, params string[] nomes)
    {
        if (alvo == null || nomes == null)
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

    private bool NomeContemAlgumaPalavra(string nome, params string[] palavras)
    {
        if (string.IsNullOrEmpty(nome))
            return false;

        string nomeLower = nome.ToLowerInvariant();

        for (int i = 0; i < palavras.Length; i++)
        {
            if (!string.IsNullOrEmpty(palavras[i]) && nomeLower.Contains(palavras[i].ToLowerInvariant()))
                return true;
        }

        return false;
    }

    private bool NomePareceMaximo(string nome)
    {
        string n = nome.ToLowerInvariant();
        return n.Contains("max") || n.Contains("maxima") || n.Contains("maximum") || n.Contains("total");
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
