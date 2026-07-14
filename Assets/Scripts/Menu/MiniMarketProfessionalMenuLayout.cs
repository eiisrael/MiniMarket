using System.Globalization;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Transforma o menu antigo em uma central gamer maior, organizada e rolável.
/// A montagem acontece em runtime para preservar a cena original e reutilizar os
/// botões/sprites já configurados no projeto.
/// </summary>
[DefaultExecutionOrder(10950)]
[DisallowMultipleComponent]
public sealed class MiniMarketProfessionalMenuLayout : MonoBehaviour
{
    [Header("Referências")]
    public MiniMarketMenuController menuController;
    public PlayerCameraController playerCamera;
    public CameraRelativeMovement movement;
    public GetItemController getItemController;
    public InteractionFocusController interactionController;

    [Header("Dimensões")]
    public Vector2 panelSize = new Vector2(780f, 690f);
    [Min(0.05f)] public float liveUpdateInterval = 0.10f;

    [Header("Tema")]
    public Color panelColor = new Color32(16, 20, 34, 250);
    public Color cardColor = new Color32(29, 36, 56, 245);
    public Color accentColor = new Color32(255, 190, 46, 255);
    public Color secondaryAccentColor = new Color32(255, 72, 146, 255);
    public Color textColor = new Color32(240, 244, 255, 255);
    public Color mutedTextColor = new Color32(176, 189, 215, 255);

    private static MiniMarketProfessionalMenuLayout instance;
    private readonly CultureInfo culture = new CultureInfo("pt-BR");

    private RectTransform builtForPanel;
    private GameObject layoutRoot;
    private Text profileBody;
    private Text energyBody;
    private Text gameplayBody;
    private Text controlsBody;
    private Text footerStatus;
    private Font uiFont;
    private float nextRefresh;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (instance != null)
            return;

        MiniMarketProfessionalMenuLayout existing =
            Object.FindAnyObjectByType<MiniMarketProfessionalMenuLayout>(
                FindObjectsInactive.Include
            );

        if (existing != null)
        {
            instance = existing;
            return;
        }

        GameObject runtimeObject = new GameObject("[MiniMarket] Professional Menu Layout");
        DontDestroyOnLoad(runtimeObject);
        instance = runtimeObject.AddComponent<MiniMarketProfessionalMenuLayout>();
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
        ResolveReferences();
    }

    private void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }

    private void Update()
    {
        ResolveReferences();
        EnsureLayout();

        if (layoutRoot == null || Time.unscaledTime < nextRefresh)
            return;

        nextRefresh = Time.unscaledTime + Mathf.Max(0.05f, liveUpdateInterval);
        RefreshLiveInformation();
    }

    private void ResolveReferences()
    {
        if (menuController == null)
        {
            menuController = Object.FindAnyObjectByType<MiniMarketMenuController>(
                FindObjectsInactive.Include
            );
        }

        if (playerCamera == null)
        {
            playerCamera = Object.FindAnyObjectByType<PlayerCameraController>(
                FindObjectsInactive.Include
            );
        }

        if (movement == null && playerCamera != null)
            movement = playerCamera.movement;

        if (movement == null && menuController != null)
            movement = menuController.movimento;

        if (movement == null)
        {
            CameraRelativeMovement[] movements = Object.FindObjectsByType<CameraRelativeMovement>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None
            );

            for (int i = 0; i < movements.Length; i++)
            {
                if (movements[i] != null && movements[i].isActiveAndEnabled)
                {
                    movement = movements[i];
                    break;
                }
            }
        }

        if (getItemController == null && playerCamera != null)
            getItemController = playerCamera.GetComponent<GetItemController>();

        if (getItemController == null)
        {
            getItemController = Object.FindAnyObjectByType<GetItemController>(
                FindObjectsInactive.Include
            );
        }

        if (interactionController == null && playerCamera != null)
            interactionController = playerCamera.GetComponent<InteractionFocusController>();

        if (interactionController == null)
        {
            interactionController = Object.FindAnyObjectByType<InteractionFocusController>(
                FindObjectsInactive.Include
            );
        }

        if (menuController != null && movement != null)
            menuController.movimento = movement;
    }

    private void EnsureLayout()
    {
        if (menuController == null || menuController.painelMenu == null)
            return;

        RectTransform panel = menuController.painelMenu.GetComponent<RectTransform>();
        if (panel == null)
            return;

        if (builtForPanel == panel && layoutRoot != null)
            return;

        if (layoutRoot != null)
            Destroy(layoutRoot);

        builtForPanel = panel;
        BuildLayout(panel);
        RefreshLiveInformation();
    }

    private void BuildLayout(RectTransform panel)
    {
        panel.anchorMin = new Vector2(0.5f, 0.5f);
        panel.anchorMax = new Vector2(0.5f, 0.5f);
        panel.pivot = new Vector2(0.5f, 0.5f);
        panel.anchoredPosition = Vector2.zero;
        panel.sizeDelta = panelSize;
        panel.localScale = Vector3.one;

        menuController.atualizarEmTempoReal = true;
        menuController.intervaloAtualizacaoAberto = Mathf.Min(
            menuController.intervaloAtualizacaoAberto,
            liveUpdateInterval
        );
        menuController.usarCliqueManualDeSeguranca = false;

        uiFont = ResolveFont();
        HideLegacyTexts();

        layoutRoot = CreateUiObject("ProfessionalLayout", panel);
        RectTransform rootRect = layoutRoot.GetComponent<RectTransform>();
        Stretch(rootRect, Vector2.zero, Vector2.zero);

        Image background = layoutRoot.AddComponent<Image>();
        background.color = panelColor;
        background.raycastTarget = true;

        Outline outline = layoutRoot.AddComponent<Outline>();
        outline.effectColor = new Color32(255, 190, 46, 190);
        outline.effectDistance = new Vector2(3f, -3f);

        CreateAccentBar(rootRect);
        RectTransform header = CreateHeader(rootRect);
        CreateScrollArea(rootRect);
        RectTransform footer = CreateFooter(rootRect);
        RepositionExistingButtons(header, footer);
    }

    private void HideLegacyTexts()
    {
        SetLegacyTextVisible(menuController.textoNome, false);
        SetLegacyTextVisible(menuController.textoGold, false);
        SetLegacyTextVisible(menuController.textoStamina, false);
        SetLegacyTextVisible(menuController.textoEmpresas, false);
    }

    private static void SetLegacyTextVisible(Text text, bool visible)
    {
        if (text != null)
            text.gameObject.SetActive(visible);
    }

    private void CreateAccentBar(RectTransform parent)
    {
        GameObject bar = CreateUiObject("AccentBar", parent);
        RectTransform rect = bar.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(0f, 7f);

        Image image = bar.AddComponent<Image>();
        image.color = accentColor;
        image.raycastTarget = false;
    }

    private RectTransform CreateHeader(RectTransform parent)
    {
        GameObject headerObject = CreateUiObject("Header", parent);
        RectTransform header = headerObject.GetComponent<RectTransform>();
        header.anchorMin = new Vector2(0f, 1f);
        header.anchorMax = new Vector2(1f, 1f);
        header.pivot = new Vector2(0.5f, 1f);
        header.anchoredPosition = new Vector2(0f, -7f);
        header.sizeDelta = new Vector2(0f, 92f);

        Image image = headerObject.AddComponent<Image>();
        image.color = new Color32(23, 29, 48, 255);
        image.raycastTarget = false;

        Text title = CreateText("Title", header, 34, FontStyle.Bold, textColor);
        title.text = "CENTRAL DO JOGADOR";
        title.alignment = TextAnchor.MiddleLeft;
        SetRect(title.rectTransform, new Vector2(28f, -12f), new Vector2(-95f, -54f));

        Text subtitle = CreateText("Subtitle", header, 17, FontStyle.Normal, mutedTextColor);
        subtitle.text = "PERFIL • ECONOMIA • ENERGIA • INTERAÇÃO";
        subtitle.alignment = TextAnchor.MiddleLeft;
        SetRect(subtitle.rectTransform, new Vector2(30f, -52f), new Vector2(-95f, -82f));

        return header;
    }

    private void CreateScrollArea(RectTransform parent)
    {
        GameObject scrollObject = CreateUiObject("InformationScroll", parent);
        RectTransform scrollRectTransform = scrollObject.GetComponent<RectTransform>();
        scrollRectTransform.anchorMin = Vector2.zero;
        scrollRectTransform.anchorMax = Vector2.one;
        scrollRectTransform.offsetMin = new Vector2(24f, 102f);
        scrollRectTransform.offsetMax = new Vector2(-24f, -108f);

        ScrollRect scroll = scrollObject.AddComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Elastic;
        scroll.elasticity = 0.08f;
        scroll.inertia = true;
        scroll.decelerationRate = 0.12f;
        scroll.scrollSensitivity = 34f;

        GameObject viewportObject = CreateUiObject("Viewport", scrollRectTransform);
        RectTransform viewport = viewportObject.GetComponent<RectTransform>();
        viewport.anchorMin = Vector2.zero;
        viewport.anchorMax = Vector2.one;
        viewport.offsetMin = Vector2.zero;
        viewport.offsetMax = new Vector2(-22f, 0f);

        Image viewportImage = viewportObject.AddComponent<Image>();
        viewportImage.color = new Color32(12, 16, 28, 160);
        viewportImage.raycastTarget = true;
        viewportObject.AddComponent<RectMask2D>();

        GameObject contentObject = CreateUiObject("Content", viewport);
        RectTransform content = contentObject.GetComponent<RectTransform>();
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(0.5f, 1f);
        content.anchoredPosition = Vector2.zero;
        content.sizeDelta = Vector2.zero;

        VerticalLayoutGroup vertical = contentObject.AddComponent<VerticalLayoutGroup>();
        vertical.padding = new RectOffset(12, 12, 12, 18);
        vertical.spacing = 12f;
        vertical.childAlignment = TextAnchor.UpperCenter;
        vertical.childControlWidth = true;
        vertical.childControlHeight = true;
        vertical.childForceExpandWidth = true;
        vertical.childForceExpandHeight = false;

        ContentSizeFitter fitter = contentObject.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        profileBody = CreateCard(
            content,
            "PERFIL E ECONOMIA",
            "Carregando dados do jogador...",
            142f,
            secondaryAccentColor
        );

        energyBody = CreateCard(
            content,
            "ENERGIA EM TEMPO REAL",
            "Sincronizando energia...",
            162f,
            new Color32(81, 232, 112, 255)
        );

        gameplayBody = CreateCard(
            content,
            "STATUS DE GAMEPLAY",
            "Lendo câmera e interações...",
            172f,
            new Color32(75, 178, 255, 255)
        );

        controlsBody = CreateCard(
            content,
            "CONTROLES RÁPIDOS",
            "Clique direito: alternar primeira/terceira pessoa\n" +
            "Clique esquerdo: interagir, pegar ou fechar a mão\n" +
            "E: interagir com portas e objetos\n" +
            "F: arremessar objeto segurado\n" +
            "TAB: abrir ou fechar esta central",
            214f,
            accentColor
        );

        GameObject scrollbarObject = CreateUiObject("Scrollbar", scrollRectTransform);
        RectTransform scrollbarRect = scrollbarObject.GetComponent<RectTransform>();
        scrollbarRect.anchorMin = new Vector2(1f, 0f);
        scrollbarRect.anchorMax = new Vector2(1f, 1f);
        scrollbarRect.pivot = new Vector2(1f, 0.5f);
        scrollbarRect.anchoredPosition = Vector2.zero;
        scrollbarRect.sizeDelta = new Vector2(16f, 0f);

        Image scrollbarBackground = scrollbarObject.AddComponent<Image>();
        scrollbarBackground.color = new Color32(8, 12, 22, 220);

        GameObject slidingAreaObject = CreateUiObject("Sliding Area", scrollbarRect);
        RectTransform slidingArea = slidingAreaObject.GetComponent<RectTransform>();
        Stretch(slidingArea, new Vector2(3f, 3f), new Vector2(-3f, -3f));

        GameObject handleObject = CreateUiObject("Handle", slidingArea);
        RectTransform handle = handleObject.GetComponent<RectTransform>();
        Stretch(handle, Vector2.zero, Vector2.zero);

        Image handleImage = handleObject.AddComponent<Image>();
        handleImage.color = accentColor;

        Scrollbar scrollbar = scrollbarObject.AddComponent<Scrollbar>();
        scrollbar.handleRect = handle;
        scrollbar.targetGraphic = handleImage;
        scrollbar.direction = Scrollbar.Direction.BottomToTop;
        scrollbar.size = 0.25f;

        scroll.content = content;
        scroll.viewport = viewport;
        scroll.verticalScrollbar = scrollbar;
        scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
    }

    private RectTransform CreateFooter(RectTransform parent)
    {
        GameObject footerObject = CreateUiObject("Footer", parent);
        RectTransform footer = footerObject.GetComponent<RectTransform>();
        footer.anchorMin = new Vector2(0f, 0f);
        footer.anchorMax = new Vector2(1f, 0f);
        footer.pivot = new Vector2(0.5f, 0f);
        footer.anchoredPosition = Vector2.zero;
        footer.sizeDelta = new Vector2(0f, 96f);

        Image image = footerObject.AddComponent<Image>();
        image.color = new Color32(23, 29, 48, 255);
        image.raycastTarget = false;

        footerStatus = CreateText("FooterStatus", footer, 18, FontStyle.Normal, mutedTextColor);
        footerStatus.alignment = TextAnchor.MiddleLeft;
        SetRect(footerStatus.rectTransform, new Vector2(26f, 12f), new Vector2(-280f, -12f));

        return footer;
    }

    private Text CreateCard(
        RectTransform parent,
        string title,
        string initialBody,
        float height,
        Color accent)
    {
        GameObject cardObject = CreateUiObject(title.Replace(" ", "") + "Card", parent);
        Image cardImage = cardObject.AddComponent<Image>();
        cardImage.color = cardColor;

        Outline cardOutline = cardObject.AddComponent<Outline>();
        cardOutline.effectColor = new Color(accent.r, accent.g, accent.b, 0.55f);
        cardOutline.effectDistance = new Vector2(2f, -2f);

        LayoutElement element = cardObject.AddComponent<LayoutElement>();
        element.preferredHeight = height;
        element.minHeight = height;
        element.flexibleWidth = 1f;

        RectTransform card = cardObject.GetComponent<RectTransform>();

        GameObject accentObject = CreateUiObject("Accent", card);
        RectTransform accentRect = accentObject.GetComponent<RectTransform>();
        accentRect.anchorMin = new Vector2(0f, 0f);
        accentRect.anchorMax = new Vector2(0f, 1f);
        accentRect.pivot = new Vector2(0f, 0.5f);
        accentRect.anchoredPosition = Vector2.zero;
        accentRect.sizeDelta = new Vector2(6f, 0f);
        Image accentImage = accentObject.AddComponent<Image>();
        accentImage.color = accent;
        accentImage.raycastTarget = false;

        Text titleText = CreateText("CardTitle", card, 21, FontStyle.Bold, accent);
        titleText.text = title;
        titleText.alignment = TextAnchor.MiddleLeft;
        SetRect(titleText.rectTransform, new Vector2(22f, -10f), new Vector2(-18f, -44f));

        Text body = CreateText("CardBody", card, 19, FontStyle.Normal, textColor);
        body.text = initialBody;
        body.alignment = TextAnchor.UpperLeft;
        body.lineSpacing = 1.18f;
        body.horizontalOverflow = HorizontalWrapMode.Wrap;
        body.verticalOverflow = VerticalWrapMode.Overflow;
        SetRect(body.rectTransform, new Vector2(22f, -48f), new Vector2(-18f, -10f));

        return body;
    }

    private void RepositionExistingButtons(RectTransform header, RectTransform footer)
    {
        if (menuController.botaoClose != null)
        {
            RectTransform close = menuController.botaoClose.GetComponent<RectTransform>();
            close.SetParent(header, false);
            close.anchorMin = new Vector2(1f, 0.5f);
            close.anchorMax = new Vector2(1f, 0.5f);
            close.pivot = new Vector2(1f, 0.5f);
            close.anchoredPosition = new Vector2(-18f, 0f);
            close.sizeDelta = new Vector2(62f, 62f);
            close.localScale = Vector3.one;
            close.SetAsLastSibling();
        }

        if (menuController.botaoGemasGratis != null)
        {
            RectTransform energyButton = menuController.botaoGemasGratis.GetComponent<RectTransform>();
            energyButton.SetParent(footer, false);
            energyButton.anchorMin = new Vector2(1f, 0.5f);
            energyButton.anchorMax = new Vector2(1f, 0.5f);
            energyButton.pivot = new Vector2(1f, 0.5f);
            energyButton.anchoredPosition = new Vector2(-24f, 0f);
            energyButton.sizeDelta = new Vector2(238f, 68f);
            energyButton.localScale = Vector3.one;
            energyButton.SetAsLastSibling();

            Text label = energyButton.GetComponentInChildren<Text>(true);
            if (label != null)
            {
                label.text = "ENERGIA +25%";
                label.fontSize = 19;
                label.fontStyle = FontStyle.Bold;
                label.alignment = TextAnchor.MiddleCenter;
            }
        }
    }

    private void RefreshLiveInformation()
    {
        ResolveReferences();

        MiniMarketPlayerDatabase database = MiniMarketPlayerDatabase.Instance;
        if (database == null && Application.isPlaying)
            database = MiniMarketPlayerDatabase.ObterOuCriar();

        string playerName = database != null
            ? database.NomePersonagem
            : (menuController != null ? menuController.nomeTemporario : "Player");

        if (string.IsNullOrWhiteSpace(playerName))
            playerName = "Player";

        long gold = database != null ? database.GoldAtual : 0L;
        int companies = database != null ? database.EmpresasCompradas : 0;

        if (profileBody != null)
        {
            profileBody.text =
                "Jogador:  " + playerName + "\n" +
                "Gold:  " + gold.ToString("N0", culture) + "\n" +
                "Empresas adquiridas:  " + companies.ToString(culture);
        }

        float energy01 = movement != null
            ? Mathf.Clamp01(movement.EnergiaPercentual01)
            : (database != null ? Mathf.Clamp01(database.EnergiaPercentual01) : 0f);

        int energyPercent = Mathf.RoundToInt(energy01 * 100f);
        string segments = movement != null
            ? movement.StaminaSegmentosAtuais + "/" + movement.StaminaSegmentosMaximos
            : "--/--";
        int activeBarPercent = movement != null
            ? Mathf.RoundToInt(movement.Stamina01 * 100f)
            : 0;

        string energyState = "Estável";
        if (movement != null)
        {
            if (movement.EstaCansado)
                energyState = "Esgotado";
            else if (movement.IsRunning)
                energyState = "Consumindo em corrida";
            else if (energyPercent < 100)
                energyState = "Recuperando";
        }

        if (energyBody != null)
        {
            energyBody.text =
                "Energia total:  " + energyPercent + "%\n" +
                "Cargas disponíveis:  " + segments + "\n" +
                "Barra ativa:  " + activeBarPercent + "%\n" +
                "Estado:  " + energyState;
        }

        string cameraMode = playerCamera != null && playerCamera.IsFirstPerson
            ? "Primeira pessoa / Mira"
            : "Terceira pessoa";
        string heldName = getItemController != null && getItemController.HeldItem != null
            ? getItemController.HeldItem.name
            : "Nenhum";
        string selectedName = getItemController != null && getItemController.SelectedItem != null
            ? getItemController.SelectedItem.name
            : "Nenhum";
        string interactionName = interactionController != null &&
                                 interactionController.FocusedObject != null
            ? interactionController.FocusedObject.displayName
            : "Nenhum";
        float speed = movement != null ? movement.CurrentSpeed : 0f;

        if (gameplayBody != null)
        {
            gameplayBody.text =
                "Câmera:  " + cameraMode + "\n" +
                "Velocidade:  " + speed.ToString("0.0", culture) + " m/s\n" +
                "Item selecionado:  " + selectedName + "\n" +
                "Item segurado:  " + heldName + "\n" +
                "Alvo interativo:  " + interactionName;
        }

        if (footerStatus != null)
        {
            footerStatus.text =
                "DADOS AO VIVO  •  Energia " + energyPercent +
                "%  •  Cada clique recupera 25%";
        }

        if (controlsBody != null && string.IsNullOrWhiteSpace(controlsBody.text))
        {
            controlsBody.text =
                "Clique direito: alternar primeira/terceira pessoa\n" +
                "Clique esquerdo: interagir, pegar ou fechar a mão\n" +
                "E: interagir com portas e objetos\n" +
                "F: arremessar objeto segurado\n" +
                "TAB: abrir ou fechar esta central";
        }
    }

    private Font ResolveFont()
    {
        if (menuController != null)
        {
            if (menuController.textoNome != null && menuController.textoNome.font != null)
                return menuController.textoNome.font;
            if (menuController.textoGold != null && menuController.textoGold.font != null)
                return menuController.textoGold.font;
            if (menuController.textoStamina != null && menuController.textoStamina.font != null)
                return menuController.textoStamina.font;
        }

        return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
    }

    private Text CreateText(
        string objectName,
        RectTransform parent,
        int fontSize,
        FontStyle fontStyle,
        Color color)
    {
        GameObject textObject = CreateUiObject(objectName, parent);
        Text text = textObject.AddComponent<Text>();
        text.font = uiFont;
        text.fontSize = fontSize;
        text.fontStyle = fontStyle;
        text.color = color;
        text.supportRichText = true;
        text.raycastTarget = false;
        return text;
    }

    private static GameObject CreateUiObject(string objectName, RectTransform parent)
    {
        GameObject result = new GameObject(objectName, typeof(RectTransform));
        RectTransform rect = result.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.localScale = Vector3.one;
        return result;
    }

    private static void Stretch(RectTransform rect, Vector2 offsetMin, Vector2 offsetMax)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
    }

    private static void SetRect(RectTransform rect, Vector2 topLeft, Vector2 bottomRight)
    {
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.offsetMin = new Vector2(topLeft.x, bottomRight.y);
        rect.offsetMax = new Vector2(bottomRight.x, topLeft.y);
    }
}
