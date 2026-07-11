using System;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Menu principal do jogador.
///
/// Exibe nome, gold, energia e empresas diretamente da fonte de dados atual.
/// Não serializa referências para objetos DontDestroyOnLoad e bloqueia o input do
/// gameplay de forma balanceada enquanto o menu está aberto.
/// </summary>
[DefaultExecutionOrder(10000)]
[DisallowMultipleComponent]
public class MiniMarketMenuController : MonoBehaviour
{
    public enum ModoOcultarPainel
    {
        CanvasGroup,
        SetActive
    }

    [Header("Painel")]
    public GameObject painelMenu;
    public KeyCode teclaMenu = KeyCode.Tab;
    public bool iniciarFechado = true;
    public bool atualizarEmTempoReal = true;
    public ModoOcultarPainel modoOcultarPainel = ModoOcultarPainel.CanvasGroup;
    public bool criarCanvasGroupAutomaticamente = true;

    [Header("Textos do Menu")]
    public Text textoNome;
    public Text textoGold;
    public Text textoStamina;
    public Text textoEmpresas;
    public bool incluirRotulosNosTextos = true;

    [Header("Botões")]
    public Button botaoClose;
    public Button botaoGemasGratis;
    public bool usarCliqueManualDeSeguranca = true;

    [Header("Dados da cena")]
    public CameraRelativeMovement movimento;
    public string nomeTemporario = "Player";

    // Compatibilidade com cenas antigas. Não são serializados para impedir referência
    // de SampleScene para objetos runtime em DontDestroyOnLoad.
    [NonSerialized] public MiniMarketPlayerProfile perfil;
    [NonSerialized] public PlayerGold playerGold;
    [NonSerialized] public Component componenteStaminaOuMovimento;

    [Header("Energia / Stamina")]
    public string energiaNaoEncontradaTexto = "--%";
    public string rotuloEnergia = "Energia";
    public bool gemasGratisRecarregaEnergia = true;
    [Min(0.1f)] public float intervaloAtualizacaoAberto = 0.25f;

    [Header("Cursor")]
    public bool desbloquearCursorQuandoMenuAberto = true;
    public bool manterCursorLivreEnquantoAberto = true;
    public bool travarCursorAoFechar = true;

    [Header("Debug")]
    public bool logarEventos;

    private readonly CultureInfo culturaBR = new CultureInfo("pt-BR");
    private MiniMarketPlayerDatabase database;
    private CanvasGroup canvasGroupMenu;
    private bool menuAberto;
    private bool inputBlockApplied;
    private bool subscriptionsApplied;
    private float nextRefresh;
    private int ultimoFrameCliqueManual = -100;

    public bool MenuAberto => menuAberto;

    private void Awake()
    {
        if (painelMenu == null)
            painelMenu = gameObject;

        ResolveCanvasGroup();
        ResolveReferences(true);
        ConnectButtons();
    }

    private void Start()
    {
        ResolveReferences(true);
        Subscribe();

        if (iniciarFechado)
            FecharMenu(false);
        else
            AbrirMenu(false);

        AtualizarTextos();
    }

    private void OnEnable()
    {
        ResolveReferences(true);
        Subscribe();
    }

    private void OnDisable()
    {
        Unsubscribe();
        ReleaseInputBlock();
        menuAberto = false;
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

        if (atualizarEmTempoReal && Time.unscaledTime >= nextRefresh)
        {
            nextRefresh = Time.unscaledTime + Mathf.Max(0.1f, intervaloAtualizacaoAberto);
            AtualizarTextos();
        }
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

    private void AbrirMenu(bool writeLog)
    {
        ResolveReferences(false);
        ResolveCanvasGroup();
        ConnectButtons();

        menuAberto = true;
        ApplyPanelVisibility(true);
        ApplyInputBlock();
        AtualizarTextos();

        if (desbloquearCursorQuandoMenuAberto)
            LiberarCursor();

        if (writeLog && logarEventos)
            Debug.Log("[MenuController] Menu aberto.", this);
    }

    private void FecharMenu(bool writeLog)
    {
        menuAberto = false;
        ApplyPanelVisibility(false);
        ReleaseInputBlock();

        if (travarCursorAoFechar)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        if (writeLog && logarEventos)
            Debug.Log("[MenuController] Menu fechado.", this);
    }

    private void ApplyInputBlock()
    {
        if (inputBlockApplied)
            return;

        GameplayInputState.PushBlock();
        inputBlockApplied = true;
    }

    private void ReleaseInputBlock()
    {
        if (!inputBlockApplied)
            return;

        GameplayInputState.PopBlock();
        inputBlockApplied = false;
    }

    private void ApplyPanelVisibility(bool visible)
    {
        if (painelMenu == null)
            return;

        if (modoOcultarPainel == ModoOcultarPainel.SetActive)
        {
            painelMenu.SetActive(visible);
            return;
        }

        ResolveCanvasGroup();

        if (!painelMenu.activeSelf)
            painelMenu.SetActive(true);

        if (canvasGroupMenu != null)
        {
            canvasGroupMenu.alpha = visible ? 1f : 0f;
            canvasGroupMenu.interactable = visible;
            canvasGroupMenu.blocksRaycasts = visible;
        }
        else
        {
            painelMenu.SetActive(visible);
        }
    }

    private void ResolveCanvasGroup()
    {
        if (painelMenu == null || canvasGroupMenu != null)
            return;

        canvasGroupMenu = painelMenu.GetComponent<CanvasGroup>();
        if (canvasGroupMenu == null && criarCanvasGroupAutomaticamente)
            canvasGroupMenu = painelMenu.AddComponent<CanvasGroup>();
    }

    public void AtualizarTextos()
    {
        ResolveReferences(false);

        string nome = database != null ? database.NomePersonagem : nomeTemporario;
        if (string.IsNullOrWhiteSpace(nome))
            nome = nomeTemporario;

        long gold = database != null
            ? database.GoldAtual
            : (playerGold != null ? playerGold.GoldAtual : 0);

        int empresas = database != null
            ? database.EmpresasCompradas
            : (perfil != null ? perfil.EmpresasCompradas : 0);

        float energia01 = -1f;
        if (movimento != null)
            energia01 = movimento.EnergiaPercentual01;
        else if (database != null)
            energia01 = database.EnergiaPercentual01;

        if (textoNome != null)
            textoNome.text = incluirRotulosNosTextos ? "Nome: " + nome : nome;

        if (textoGold != null)
        {
            string valor = gold.ToString("N0", culturaBR);
            textoGold.text = incluirRotulosNosTextos ? "Gold: " + valor : valor;
        }

        if (textoEmpresas != null)
        {
            string valor = empresas.ToString(culturaBR);
            textoEmpresas.text = incluirRotulosNosTextos ? "Empresas: " + valor : valor;
        }

        if (textoStamina != null)
        {
            string valor = energia01 >= 0f
                ? Mathf.RoundToInt(Mathf.Clamp01(energia01) * 100f).ToString(culturaBR) + "%"
                : energiaNaoEncontradaTexto;

            textoStamina.text = incluirRotulosNosTextos
                ? rotuloEnergia + ": " + valor
                : valor;
        }
    }

    public void RecarregarStaminaComGemasGratis()
    {
        RecarregarEnergiaComGemasGratis();
    }

    public void RecarregarEnergiaComGemasGratis()
    {
        if (!gemasGratisRecarregaEnergia)
            return;

        ResolveReferences(false);

        if (movimento != null)
            movimento.RestoreStaminaFull();
        else if (database != null)
            database.RestaurarEnergiaCompleta();

        AtualizarTextos();

        if (menuAberto && manterCursorLivreEnquantoAberto)
            LiberarCursor();

        if (logarEventos)
            Debug.Log("[MenuController] Energia restaurada.", this);
    }

    private void ResolveReferences(bool force)
    {
        if (force || movimento == null)
            movimento = UnityEngine.Object.FindAnyObjectByType<CameraRelativeMovement>(FindObjectsInactive.Include);

        if (force || database == null)
        {
            database = MiniMarketPlayerDatabase.Instance;
            if (database == null && Application.isPlaying)
                database = MiniMarketPlayerDatabase.ObterOuCriar();
        }

        if (force || playerGold == null)
            playerGold = PlayerGold.Instance != null
                ? PlayerGold.Instance
                : UnityEngine.Object.FindAnyObjectByType<PlayerGold>(FindObjectsInactive.Include);

        if (force || perfil == null)
            perfil = MiniMarketPlayerProfile.Instance;

        componenteStaminaOuMovimento = movimento;
    }

    private void Subscribe()
    {
        if (subscriptionsApplied)
            return;

        if (database != null)
            database.OnDatabaseChanged += HandleDatabaseChanged;
        if (movimento != null)
            movimento.OnStaminaChanged += AtualizarTextos;
        if (playerGold != null)
            playerGold.OnGoldAlterado += HandleGoldChanged;
        if (perfil != null)
            perfil.OnDadosAlterados += AtualizarTextos;

        subscriptionsApplied = true;
    }

    private void Unsubscribe()
    {
        if (!subscriptionsApplied)
            return;

        if (database != null)
            database.OnDatabaseChanged -= HandleDatabaseChanged;
        if (movimento != null)
            movimento.OnStaminaChanged -= AtualizarTextos;
        if (playerGold != null)
            playerGold.OnGoldAlterado -= HandleGoldChanged;
        if (perfil != null)
            perfil.OnDadosAlterados -= AtualizarTextos;

        subscriptionsApplied = false;
    }

    private void HandleDatabaseChanged(MiniMarketPlayerDatabase.MiniMarketPlayerData data)
    {
        AtualizarTextos();
    }

    private void HandleGoldChanged(int value)
    {
        AtualizarTextos();
    }

    private void ConnectButtons()
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

    private void VerificarCliqueManualNosBotoes()
    {
        if (!Input.GetMouseButtonDown(0) || Time.frameCount == ultimoFrameCliqueManual)
            return;

        if (PointerOverButton(botaoClose))
        {
            ultimoFrameCliqueManual = Time.frameCount;
            FecharMenu();
            return;
        }

        if (PointerOverButton(botaoGemasGratis))
        {
            ultimoFrameCliqueManual = Time.frameCount;
            RecarregarEnergiaComGemasGratis();
        }
    }

    private bool PointerOverButton(Button button)
    {
        if (button == null || !button.gameObject.activeInHierarchy)
            return false;

        RectTransform rect = button.GetComponent<RectTransform>();
        if (rect == null)
            return false;

        Canvas canvas = button.GetComponentInParent<Canvas>();
        Camera uiCamera = null;

        if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
            uiCamera = canvas.worldCamera != null ? canvas.worldCamera : Camera.main;

        return RectTransformUtility.RectangleContainsScreenPoint(rect, Input.mousePosition, uiCamera);
    }

    private void LiberarCursor()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void OnValidate()
    {
        intervaloAtualizacaoAberto = Mathf.Max(0.1f, intervaloAtualizacaoAberto);
    }
}
