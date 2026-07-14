using System.Globalization;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Interface profissional do menu do jogador.
///
/// O MiniMarketMenuController antigo continua responsável por TAB, cursor e bloqueio
/// de input, porém todo o visual é criado como uma camada independente no Canvas.
/// Isso evita conflitos de escala, posição e textos com o painel legado da cena.
/// </summary>
[DefaultExecutionOrder(13000)]
[DisallowMultipleComponent]
public sealed class MiniMarketProfessionalMenuLayout : MonoBehaviour
{
    public static MiniMarketProfessionalMenuLayout Instance { get; private set; }

    [Header("Referências")]
    public MiniMarketMenuController menuController;
    public MiniMarketEnergyQuarterRefill energyAuthority;
    public PlayerCameraController playerCamera;
    public CameraRelativeMovement movement;
    public GetItemController getItemController;
    public InteractionFocusController interactionController;

    [Header("Layout")]
    public Vector2 panelSize = new Vector2(880f, 720f);
    [Min(0.03f)] public float liveUpdateInterval = 0.08f;

    [Header("Tema")]
    public Color backdropColor = new Color32(4, 7, 15, 190);
    public Color panelColor = new Color32(14, 20, 34, 252);
    public Color headerColor = new Color32(24, 31, 52, 255);
    public Color cardColor = new Color32(27, 36, 57, 248);
    public Color accentGold = new Color32(255, 190, 48, 255);
    public Color accentPink = new Color32(255, 73, 146, 255);
    public Color accentBlue = new Color32(65, 180, 255, 255);
    public Color accentGreen = new Color32(74, 230, 118, 255);
    public Color textColor = new Color32(242, 246, 255, 255);
    public Color mutedTextColor = new Color32(170, 184, 212, 255);

    private readonly CultureInfo culture = new CultureInfo("pt-BR");

    private Canvas targetCanvas;
    private CanvasGroup legacyCanvasGroup;
    private GameObject uiRoot;
    private RectTransform mainPanel;
    private Text profileText;
    private Text energyText;
    private Text gameplayText;
    private Text controlsText;
    private Text liveFooterText;
    private Image energyFill;
    private Text energyPercentText;
    private Button energyButton;
    private Font uiFont;
    private float nextRefresh;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Instance = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        MiniMarketProfessionalMenuLayout existing =
            Object.FindAnyObjectByType<MiniMarketProfessionalMenuLayout>(
                FindObjectsInactive.Include
            );

        if (existing != null)
        {
            Instance = existing;
            return;
        }

        GameObject host = new GameObject("[MiniMarket] Player Hub UI");
        DontDestroyOnLoad(host);
        Instance = host.AddComponent<MiniMarketProfessionalMenuLayout>();
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
        ResolveReferences(true);
    }

    private void OnDestroy()
    {
        RestoreLegacyPanel();

        if (Instance == this)
            Instance = null;
    }

    private void Update()
    {
        ResolveReferences(false);
        EnsureUi();
        SyncVisibility();

        if (uiRoot == null || menuController == null || !menuController.MenuAberto)
            return;

        if (Time.unscaledTime >= nextRefresh)
        {
            nextRefresh = Time.unscaledTime + Mathf.Max(0.03f, liveUpdateInterval);
            RefreshLiveData();
        }
    }

    private void LateUpdate()
    {
        if (menuController == null)
            return;

        // O painel antigo permanece funcional como controlador, mas nunca concorre visualmente.
        HideLegacyPanel();

        if (uiRoot != null && menuController.MenuAberto)
            uiRoot.transform.SetAsLastSibling();
    }

    private void ResolveReferences(bool force)
    {
        if (force || menuController == null || !menuController.gameObject.scene.IsValid())
        {
            menuController = Object.FindAnyObjectByType<MiniMarketMenuController>(
                FindObjectsInactive.Include
            );
        }

        if (force || energyAuthority == null)
            energyAuthority = MiniMarketEnergyQuarterRefill.Instance;

        if (energyAuthority == null)
        {
            energyAuthority = Object.FindAnyObjectByType<MiniMarketEnergyQuarterRefill>(
                FindObjectsInactive.Include
            );
        }

        if (force || playerCamera == null || !playerCamera.gameObject.scene.IsValid())
        {
            playerCamera = Object.FindAnyObjectByType<PlayerCameraController>(
                FindObjectsInactive.Include
            );
        }

        movement = energyAuthority != null && energyAuthority.ActiveMovement != null
            ? energyAuthority.ActiveMovement
            : (playerCamera != null ? playerCamera.movement : movement);

        if (getItemController == null && playerCamera != null)
            getItemController = playerCamera.GetComponent<GetItemController>();

        if (interactionController == null && playerCamera != null)
            interactionController = playerCamera.GetComponent<InteractionFocusController>();

        if (menuController != null)
        {
            Canvas canvas = menuController.GetComponentInParent<Canvas>(true);
            if (canvas != null && canvas != targetCanvas)
            {
                targetCanvas = canvas;
                DestroyCurrentUi();
            }

            if (menuController.painelMenu != null && legacyCanvasGroup == null)
            {
                legacyCanvasGroup = menuController.painelMenu.GetComponent<CanvasGroup>();
                if (legacyCanvasGroup == null)
                    legacyCanvasGroup = menuController.painelMenu.AddComponent<CanvasGroup>();
            }
        }
    }

    private void EnsureUi()
    {
        if (uiRoot != null || targetCanvas == null || menuController == null)
            return;

        uiFont = ResolveFont();
        BuildUi();
        RefreshLiveData();
        SyncVisibility();
    }

    private void BuildUi()
    {
        uiRoot = CreateUiObject("MiniMarketPlayerHub", targetCanvas.transform);
        RectTransform root = uiRoot.GetComponent<RectTransform>();
        Stretch(root, Vector2.zero, Vector2.zero);

        Image backdrop = uiRoot.AddComponent<Image>();
        backdrop.color = backdropColor;
        backdrop.raycastTarget = true;

        mainPanel = CreateUiObject("MainPanel", root).GetComponent<RectTransform>();
        mainPanel.anchorMin = new Vector2(0.5f, 0.5f);
        mainPanel.anchorMax = new Vector2(0.5f, 0.5f);
        mainPanel.pivot = new Vector2(0.5f, 0.5f);
        mainPanel.anchoredPosition = Vector2.zero;
        mainPanel.sizeDelta = panelSize;

        Image panelImage = mainPanel.gameObject.AddComponent<Image>();
        panelImage.color = panelColor;

        Outline panelOutline = mainPanel.gameObject.AddComponent<Outline>();
        panelOutline.effectColor = new Color32(255, 190, 48, 210);
        panelOutline.effectDistance = new Vector2(3f, -3f);

        BuildHeader(mainPanel);
        BuildContent(mainPanel);
        BuildFooter(mainPanel);
    }

    private void BuildHeader(RectTransform parent)
    {
        RectTransform header = CreateUiObject("Header", parent).GetComponent<RectTransform>();
        header.anchorMin = new Vector2(0f, 1f);
        header.anchorMax = new Vector2(1f, 1f);
        header.pivot = new Vector2(0.5f, 1f);
        header.anchoredPosition = Vector2.zero;
        header.sizeDelta = new Vector2(0f, 105f);

        Image headerImage = header.gameObject.AddComponent<Image>();
        headerImage.color = headerColor;

        RectTransform accent = CreateUiObject("TopAccent", header).GetComponent<RectTransform>();
        accent.anchorMin = new Vector2(0f, 1f);
        accent.anchorMax = new Vector2(1f, 1f);
        accent.pivot = new Vector2(0.5f, 1f);
        accent.anchoredPosition = Vector2.zero;
        accent.sizeDelta = new Vector2(0f, 7f);
        accent.gameObject.AddComponent<Image>().color = accentGold;

        Text title = CreateText("Title", header, 34, FontStyle.Bold, textColor);
        title.text = "CENTRAL DO JOGADOR";
        title.alignment = TextAnchor.MiddleLeft;
        SetTopRect(title.rectTransform, 30f, -18f, -100f, -58f);

        Text subtitle = CreateText("Subtitle", header, 16, FontStyle.Normal, mutedTextColor);
        subtitle.text = "PERFIL  •  ENERGIA  •  ECONOMIA  •  INTERAÇÃO";
        subtitle.alignment = TextAnchor.MiddleLeft;
        SetTopRect(subtitle.rectTransform, 32f, -62f, -100f, -92f);

        Button closeButton = CreateButton(
            "CloseButton",
            header,
            "×",
            new Color32(221, 72, 82, 255),
            32
        );
        RectTransform closeRect = closeButton.GetComponent<RectTransform>();
        closeRect.anchorMin = new Vector2(1f, 0.5f);
        closeRect.anchorMax = new Vector2(1f, 0.5f);
        closeRect.pivot = new Vector2(1f, 0.5f);
        closeRect.anchoredPosition = new Vector2(-22f, -2f);
        closeRect.sizeDelta = new Vector2(62f, 62f);
        closeButton.onClick.AddListener(() => menuController?.FecharMenu());
    }

    private void BuildContent(RectTransform parent)
    {
        RectTransform scrollRoot = CreateUiObject("ScrollRoot", parent).GetComponent<RectTransform>();
        scrollRoot.anchorMin = Vector2.zero;
        scrollRoot.anchorMax = Vector2.one;
        scrollRoot.offsetMin = new Vector2(26f, 108f);
        scrollRoot.offsetMax = new Vector2(-26f, -118f);

        ScrollRect scroll = scrollRoot.gameObject.AddComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Elastic;
        scroll.elasticity = 0.08f;
        scroll.inertia = true;
        scroll.scrollSensitivity = 38f;

        RectTransform viewport = CreateUiObject("Viewport", scrollRoot).GetComponent<RectTransform>();
        viewport.anchorMin = Vector2.zero;
        viewport.anchorMax = Vector2.one;
        viewport.offsetMin = Vector2.zero;
        viewport.offsetMax = new Vector2(-22f, 0f);
        Image viewportImage = viewport.gameObject.AddComponent<Image>();
        viewportImage.color = new Color32(8, 12, 23, 185);
        viewport.gameObject.AddComponent<RectMask2D>();

        RectTransform content = CreateUiObject("Content", viewport).GetComponent<RectTransform>();
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(0.5f, 1f);
        content.anchoredPosition = Vector2.zero;
        content.sizeDelta = Vector2.zero;

        VerticalLayoutGroup layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(14, 14, 14, 18);
        layout.spacing = 14f;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        ContentSizeFitter fitter = content.gameObject.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        profileText = CreateCard(content, "PERFIL E ECONOMIA", accentPink, 150f);
        energyText = CreateEnergyCard(content, 205f);
        gameplayText = CreateCard(content, "STATUS DE GAMEPLAY", accentBlue, 205f);
        controlsText = CreateCard(content, "CONTROLES", accentGold, 220f);
        controlsText.text =
            "BOTÃO DIREITO  • alternar primeira/terceira pessoa\n" +
            "BOTÃO ESQUERDO • interagir, pegar e fechar a mão\n" +
            "E              • interagir com portas e objetos\n" +
            "F              • arremessar objeto segurado\n" +
            "TAB            • abrir/fechar a central\n" +
            "F10            • diagnóstico técnico";

        RectTransform scrollbarRect = CreateUiObject("Scrollbar", scrollRoot).GetComponent<RectTransform>();
        scrollbarRect.anchorMin = new Vector2(1f, 0f);
        scrollbarRect.anchorMax = new Vector2(1f, 1f);
        scrollbarRect.pivot = new Vector2(1f, 0.5f);
        scrollbarRect.sizeDelta = new Vector2(15f, 0f);
        scrollbarRect.anchoredPosition = Vector2.zero;
        scrollbarRect.gameObject.AddComponent<Image>().color = new Color32(8, 12, 22, 230);

        RectTransform sliding = CreateUiObject("SlidingArea", scrollbarRect).GetComponent<RectTransform>();
        Stretch(sliding, new Vector2(3f, 3f), new Vector2(-3f, -3f));
        RectTransform handle = CreateUiObject("Handle", sliding).GetComponent<RectTransform>();
        Stretch(handle, Vector2.zero, Vector2.zero);
        Image handleImage = handle.gameObject.AddComponent<Image>();
        handleImage.color = accentGold;

        Scrollbar scrollbar = scrollbarRect.gameObject.AddComponent<Scrollbar>();
        scrollbar.handleRect = handle;
        scrollbar.targetGraphic = handleImage;
        scrollbar.direction = Scrollbar.Direction.BottomToTop;

        scroll.viewport = viewport;
        scroll.content = content;
        scroll.verticalScrollbar = scrollbar;
        scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
    }

    private Text CreateEnergyCard(RectTransform parent, float height)
    {
        RectTransform card = CreateCardRoot(parent, "EnergyCard", "ENERGIA EM TEMPO REAL", accentGreen, height);

        energyText = CreateText("EnergyBody", card, 18, FontStyle.Normal, textColor);
        energyText.alignment = TextAnchor.UpperLeft;
        energyText.lineSpacing = 1.15f;
        SetTopRect(energyText.rectTransform, 24f, -52f, -210f, -126f);

        RectTransform barBackground = CreateUiObject("EnergyBarBackground", card).GetComponent<RectTransform>();
        barBackground.anchorMin = new Vector2(0f, 0f);
        barBackground.anchorMax = new Vector2(1f, 0f);
        barBackground.pivot = new Vector2(0.5f, 0f);
        barBackground.offsetMin = new Vector2(24f, 20f);
        barBackground.offsetMax = new Vector2(-210f, 52f);
        barBackground.gameObject.AddComponent<Image>().color = new Color32(8, 12, 22, 255);

        RectTransform fill = CreateUiObject("EnergyFill", barBackground).GetComponent<RectTransform>();
        fill.anchorMin = Vector2.zero;
        fill.anchorMax = Vector2.one;
        fill.offsetMin = new Vector2(4f, 4f);
        fill.offsetMax = new Vector2(-4f, -4f);
        energyFill = fill.gameObject.AddComponent<Image>();
        energyFill.type = Image.Type.Filled;
        energyFill.fillMethod = Image.FillMethod.Horizontal;
        energyFill.fillOrigin = 0;
        energyFill.color = accentGreen;

        energyPercentText = CreateText("EnergyPercent", barBackground, 18, FontStyle.Bold, textColor);
        energyPercentText.alignment = TextAnchor.MiddleCenter;
        Stretch(energyPercentText.rectTransform, Vector2.zero, Vector2.zero);

        energyButton = CreateButton(
            "EnergyButton",
            card,
            "RECUPERAR\n+25%",
            new Color32(48, 166, 82, 255),
            18
        );
        RectTransform buttonRect = energyButton.GetComponent<RectTransform>();
        buttonRect.anchorMin = new Vector2(1f, 0.5f);
        buttonRect.anchorMax = new Vector2(1f, 0.5f);
        buttonRect.pivot = new Vector2(1f, 0.5f);
        buttonRect.anchoredPosition = new Vector2(-24f, -10f);
        buttonRect.sizeDelta = new Vector2(166f, 82f);
        energyButton.onClick.AddListener(HandleEnergyClick);

        return energyText;
    }

    private void BuildFooter(RectTransform parent)
    {
        RectTransform footer = CreateUiObject("Footer", parent).GetComponent<RectTransform>();
        footer.anchorMin = new Vector2(0f, 0f);
        footer.anchorMax = new Vector2(1f, 0f);
        footer.pivot = new Vector2(0.5f, 0f);
        footer.anchoredPosition = Vector2.zero;
        footer.sizeDelta = new Vector2(0f, 92f);
        footer.gameObject.AddComponent<Image>().color = headerColor;

        liveFooterText = CreateText("LiveFooter", footer, 17, FontStyle.Normal, mutedTextColor);
        liveFooterText.alignment = TextAnchor.MiddleLeft;
        SetBottomRect(liveFooterText.rectTransform, 28f, 16f, -28f, 74f);
    }

    private Text CreateCard(
        RectTransform parent,
        string title,
        Color accent,
        float height)
    {
        RectTransform card = CreateCardRoot(parent, title.Replace(" ", string.Empty), title, accent, height);
        Text body = CreateText("Body", card, 19, FontStyle.Normal, textColor);
        body.alignment = TextAnchor.UpperLeft;
        body.lineSpacing = 1.18f;
        body.horizontalOverflow = HorizontalWrapMode.Wrap;
        body.verticalOverflow = VerticalWrapMode.Overflow;
        SetTopRect(body.rectTransform, 24f, -52f, -22f, -12f);
        return body;
    }

    private RectTransform CreateCardRoot(
        RectTransform parent,
        string objectName,
        string title,
        Color accent,
        float height)
    {
        RectTransform card = CreateUiObject(objectName, parent).GetComponent<RectTransform>();
        card.gameObject.AddComponent<Image>().color = cardColor;

        Outline outline = card.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(accent.r, accent.g, accent.b, 0.5f);
        outline.effectDistance = new Vector2(2f, -2f);

        LayoutElement element = card.gameObject.AddComponent<LayoutElement>();
        element.minHeight = height;
        element.preferredHeight = height;
        element.flexibleWidth = 1f;

        RectTransform accentBar = CreateUiObject("Accent", card).GetComponent<RectTransform>();
        accentBar.anchorMin = new Vector2(0f, 0f);
        accentBar.anchorMax = new Vector2(0f, 1f);
        accentBar.pivot = new Vector2(0f, 0.5f);
        accentBar.anchoredPosition = Vector2.zero;
        accentBar.sizeDelta = new Vector2(7f, 0f);
        accentBar.gameObject.AddComponent<Image>().color = accent;

        Text titleText = CreateText("Title", card, 21, FontStyle.Bold, accent);
        titleText.text = title;
        titleText.alignment = TextAnchor.MiddleLeft;
        SetTopRect(titleText.rectTransform, 24f, -10f, -20f, -44f);
        return card;
    }

    private void HandleEnergyClick()
    {
        ResolveReferences(true);
        energyAuthority?.AddTwentyFivePercent();
        RefreshLiveData();
    }

    private void RefreshLiveData()
    {
        ResolveReferences(false);

        MiniMarketPlayerDatabase database = MiniMarketPlayerDatabase.Instance;
        if (database == null && Application.isPlaying)
            database = MiniMarketPlayerDatabase.ObterOuCriar();

        string playerName = database != null ? database.NomePersonagem : "Player";
        if (string.IsNullOrWhiteSpace(playerName))
            playerName = "Player";

        long gold = database != null ? database.GoldAtual : 0L;
        int companies = database != null ? database.EmpresasCompradas : 0;

        if (profileText != null)
        {
            profileText.text =
                "Jogador             " + playerName + "\n" +
                "Gold                " + gold.ToString("N0", culture) + "\n" +
                "Empresas adquiridas " + companies.ToString(culture);
        }

        float energy01 = energyAuthority != null
            ? energyAuthority.CurrentEnergy01
            : (movement != null ? Mathf.Clamp01(movement.EnergiaPercentual01) : 0f);
        int energyPercent = Mathf.RoundToInt(energy01 * 100f);

        if (energyFill != null)
        {
            energyFill.fillAmount = energy01;
            energyFill.color = energyPercent > 60
                ? accentGreen
                : (energyPercent > 25 ? accentGold : new Color32(235, 72, 72, 255));
        }

        if (energyPercentText != null)
            energyPercentText.text = energyPercent + "%";

        if (energyText != null)
        {
            string segments = movement != null
                ? movement.StaminaSegmentosAtuais + "/" + movement.StaminaSegmentosMaximos
                : "--/--";
            string state = movement == null
                ? "Indisponível"
                : (movement.EstaCansado
                    ? "ESGOTADO"
                    : (movement.IsRunning ? "CONSUMINDO" : "ESTÁVEL"));

            energyText.text =
                "Energia total  " + energyPercent + "%\n" +
                "Cargas         " + segments + "\n" +
                "Estado         " + state;
        }

        string cameraMode = playerCamera != null && playerCamera.IsFirstPerson
            ? "PRIMEIRA PESSOA / MIRA"
            : "TERCEIRA PESSOA";
        string held = getItemController != null && getItemController.HeldItem != null
            ? getItemController.HeldItem.name
            : "Nenhum";
        string selected = getItemController != null && getItemController.SelectedItem != null
            ? getItemController.SelectedItem.name
            : "Nenhum";
        string focused = interactionController != null && interactionController.FocusedObject != null
            ? interactionController.FocusedObject.displayName
            : "Nenhum";
        float speed = movement != null ? movement.CurrentSpeed : 0f;

        if (gameplayText != null)
        {
            gameplayText.text =
                "Câmera          " + cameraMode + "\n" +
                "Velocidade      " + speed.ToString("0.0", culture) + " m/s\n" +
                "Item selecionado " + selected + "\n" +
                "Item segurado    " + held + "\n" +
                "Alvo interativo  " + focused;
        }

        if (liveFooterText != null)
        {
            liveFooterText.text =
                "● DADOS AO VIVO     Energia " + energyPercent +
                "%     Banco " + (database != null ? "CONECTADO" : "INDISPONÍVEL") +
                "     F10: diagnóstico";
        }
    }

    private void SyncVisibility()
    {
        if (uiRoot == null || menuController == null)
            return;

        bool visible = menuController.MenuAberto;
        if (uiRoot.activeSelf != visible)
            uiRoot.SetActive(visible);

        HideLegacyPanel();
    }

    private void HideLegacyPanel()
    {
        if (legacyCanvasGroup == null)
            return;

        legacyCanvasGroup.alpha = 0f;
        legacyCanvasGroup.interactable = false;
        legacyCanvasGroup.blocksRaycasts = false;
    }

    private void RestoreLegacyPanel()
    {
        if (legacyCanvasGroup == null)
            return;

        bool visible = menuController != null && menuController.MenuAberto;
        legacyCanvasGroup.alpha = visible ? 1f : 0f;
        legacyCanvasGroup.interactable = visible;
        legacyCanvasGroup.blocksRaycasts = visible;
    }

    private void DestroyCurrentUi()
    {
        if (uiRoot != null)
            Destroy(uiRoot);

        uiRoot = null;
        mainPanel = null;
    }

    private Font ResolveFont()
    {
        if (menuController != null)
        {
            if (menuController.textoNome != null && menuController.textoNome.font != null)
                return menuController.textoNome.font;
            if (menuController.textoGold != null && menuController.textoGold.font != null)
                return menuController.textoGold.font;
        }

        return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
    }

    private Button CreateButton(
        string objectName,
        RectTransform parent,
        string label,
        Color color,
        int fontSize)
    {
        RectTransform rect = CreateUiObject(objectName, parent).GetComponent<RectTransform>();
        Image image = rect.gameObject.AddComponent<Image>();
        image.color = color;

        Button button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = image;

        ColorBlock colors = button.colors;
        colors.normalColor = color;
        colors.highlightedColor = Color.Lerp(color, Color.white, 0.18f);
        colors.pressedColor = Color.Lerp(color, Color.black, 0.22f);
        colors.selectedColor = colors.highlightedColor;
        button.colors = colors;

        Outline outline = rect.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color32(0, 0, 0, 170);
        outline.effectDistance = new Vector2(2f, -2f);

        Text text = CreateText("Label", rect, fontSize, FontStyle.Bold, Color.white);
        text.text = label;
        text.alignment = TextAnchor.MiddleCenter;
        Stretch(text.rectTransform, new Vector2(5f, 5f), new Vector2(-5f, -5f));
        return button;
    }

    private Text CreateText(
        string objectName,
        RectTransform parent,
        int fontSize,
        FontStyle style,
        Color color)
    {
        RectTransform rect = CreateUiObject(objectName, parent).GetComponent<RectTransform>();
        Text text = rect.gameObject.AddComponent<Text>();
        text.font = uiFont;
        text.fontSize = fontSize;
        text.fontStyle = style;
        text.color = color;
        text.supportRichText = true;
        text.raycastTarget = false;
        return text;
    }

    private static GameObject CreateUiObject(string objectName, Transform parent)
    {
        GameObject result = new GameObject(objectName, typeof(RectTransform));
        RectTransform rect = result.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.localScale = Vector3.one;
        return result;
    }

    private static void Stretch(RectTransform rect, Vector2 min, Vector2 max)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = min;
        rect.offsetMax = max;
    }

    private static void SetTopRect(
        RectTransform rect,
        float left,
        float top,
        float right,
        float bottom)
    {
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.offsetMin = new Vector2(left, bottom);
        rect.offsetMax = new Vector2(right, top);
    }

    private static void SetBottomRect(
        RectTransform rect,
        float left,
        float bottom,
        float right,
        float top)
    {
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(1f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.offsetMin = new Vector2(left, bottom);
        rect.offsetMax = new Vector2(right, top);
    }
}
