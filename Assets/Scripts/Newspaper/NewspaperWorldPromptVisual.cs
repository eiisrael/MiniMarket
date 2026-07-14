using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Prompt circular persistente do sistema de jornal.
///
/// O RectTransform raiz pertence ao usuário: posição, rotação, escala, largura e
/// altura podem ser alteradas diretamente no Inspector sem serem sobrescritas.
/// As animações acontecem somente dentro do filho CircularPrompt.
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
public sealed class NewspaperWorldPromptVisual : MonoBehaviour
{
    [Header("Visualização")]
    public bool previewInEditMode = true;
    public bool animateInEditMode;
    public bool faceCamera = true;
    public bool showInstructionText = true;
    public int sortingOrder = 260;

    [Header("Controle do Transform")]
    [Tooltip("Marcado: o RectTransform raiz é a fonte oficial de posição e escala. Assim ele pode ser editado livremente no Inspector.")]
    public bool useRootTransformAsSource = true;

    [Tooltip("Espelho da escala do Transform. Mantido para compatibilidade com os controladores existentes.")]
    [Min(0.0001f)] public float worldScale = 0.0023f;

    [Tooltip("Espelho da posição local do Transform. Mantido para compatibilidade com os controladores existentes.")]
    public Vector3 localOffset = new Vector3(0f, 0.72f, 0f);

    [Header("Círculo")]
    [Min(48f)] public float circleDiameter = 86f;
    [Min(1f)] public float progressThickness = 10f;
    [Min(1f)] public float innerAccentThickness = 6f;
    [Min(4f)] public float orbitMarkerSize = 9f;

    [Header("Texto")]
    public Vector2 instructionOffset = new Vector2(0f, 52f);
    public Vector2 instructionSize = new Vector2(220f, 30f);
    [Min(8f)] public float instructionFontSize = 15f;
    [Min(8f)] public float centerFontSize = 30f;
    [Range(0f, 1f)] public float instructionOutlineWidth = 0.16f;
    [Range(0f, 1f)] public float centerOutlineWidth = 0.20f;

    [Header("Animação")]
    [Min(0f)] public float rotationDegreesPerSecond = 38f;

    [Tooltip("Amplitude em unidades do mundo. A raiz não é movimentada; o deslocamento é aplicado apenas ao círculo interno.")]
    [Min(0f)] public float floatingAmplitude = 0.015f;

    [Min(0f)] public float floatingSpeed = 2.1f;
    [Range(0f, 0.25f)] public float pulseAmount = 0.035f;
    [Min(0f)] public float pulseSpeed = 3f;
    [Range(0f, 0.5f)] public float glowPulseAmount = 0.12f;
    [Min(0f)] public float glowPulseSpeed = 3.4f;

    [Header("Tema Infantil / Gamer")]
    public Color glowColor = new Color32(255, 220, 84, 56);
    public Color outerRingColor = new Color32(255, 205, 65, 255);
    public Color innerAccentColor = new Color32(255, 102, 171, 255);
    public Color centerDiscColor = new Color32(22, 29, 45, 244);
    public Color centerTextColor = Color.white;
    public Color instructionTextColor = new Color32(255, 248, 218, 255);
    public Color textOutlineColor = new Color32(10, 12, 20, 245);

    [Header("Estado Atual")]
    [SerializeField] private Color currentAccentColor = new Color32(76, 235, 124, 255);
    [SerializeField, Range(0f, 1f)] private float currentProgress;
    [SerializeField] private bool progressVisible;

    [Header("Referências Geradas")]
    [HideInInspector] public Canvas worldCanvas;
    [HideInInspector] public CanvasGroup canvasGroup;
    [HideInInspector] public RectTransform rootRect;
    [HideInInspector] public RectTransform visualRoot;
    [HideInInspector] public RectTransform rotatingRing;
    [HideInInspector] public Image glowImage;
    [HideInInspector] public Image outerRingImage;
    [HideInInspector] public Image rotatingRingImage;
    [HideInInspector] public Image progressImage;
    [HideInInspector] public Image innerAccentRingImage;
    [HideInInspector] public Image centerDisc;
    [HideInInspector] public Image orbitMarker;
    [HideInInspector] public Image orbitMarkerSecondary;
    [HideInInspector] public Image orbitMarkerTertiary;
    [HideInInspector] public TextMeshProUGUI centerText;
    [HideInInspector] public TextMeshProUGUI instructionText;

    private Camera cachedCamera;
    private bool built;
    private bool transformBindingInitialized;

    private static Sprite discSprite;
    private static Sprite ringSprite;
    private static Sprite segmentedRingSprite;
    private static Texture2D discTexture;
    private static Texture2D ringTexture;
    private static Texture2D segmentedRingTexture;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        discSprite = null;
        ringSprite = null;
        segmentedRingSprite = null;
        discTexture = null;
        ringTexture = null;
        segmentedRingTexture = null;
    }

    public static NewspaperWorldPromptVisual Create(
        Transform parent,
        string objectName,
        Vector3 requestedLocalOffset,
        float requestedWorldScale,
        int requestedSortingOrder = 260)
    {
        GameObject root = new GameObject(objectName, typeof(RectTransform));
        root.transform.SetParent(parent, false);

        NewspaperWorldPromptVisual visual = root.AddComponent<NewspaperWorldPromptVisual>();
        visual.localOffset = requestedLocalOffset;
        visual.worldScale = NormalizeLegacyScale(requestedWorldScale);
        visual.sortingOrder = requestedSortingOrder;
        visual.ApplyCompatibilityTransform();
        visual.EnsurePersistentVisual();
        visual.SetVisible(false);
        return visual;
    }

    private void Awake()
    {
        ResolveExistingReferences();
        InitializeTransformBinding();

        if (Application.isPlaying)
            EnsurePersistentVisual();

        ApplyInspectorSettings();
    }

    private void OnEnable()
    {
        ResolveExistingReferences();
        InitializeTransformBinding();

        if (Application.isPlaying)
            EnsurePersistentVisual();

        ApplyInspectorSettings();

        if (!Application.isPlaying && previewInEditMode && canvasGroup != null)
            canvasGroup.alpha = 1f;
    }

    private void OnValidate()
    {
        ClampInspectorValues();
        ResolveExistingReferences();
        InitializeTransformBinding();

        if (built)
            ApplyInspectorSettings();
    }

    private void LateUpdate()
    {
        if (!built)
            return;

        SyncCompatibilityFieldsFromTransform();
        ApplyInspectorSettings();

        bool shouldAnimate = Application.isPlaying || animateInEditMode;
        if (shouldAnimate)
            AnimatePrompt();
        else
            ResetInnerAnimation();

        if (Application.isPlaying)
            FaceActiveCamera();
    }

    [ContextMenu("Jornal/Reparar visual circular")]
    public void EnsurePersistentVisual()
    {
        ResolveExistingReferences();
        InitializeTransformBinding();

        if (!built)
            Build(sortingOrder);

        ApplyInspectorSettings();

        if (!Application.isPlaying && previewInEditMode && canvasGroup != null)
            canvasGroup.alpha = 1f;
    }

    [ContextMenu("Jornal/Reconstruir visual circular")]
    public void RebuildVisual()
    {
        ClearGeneratedChildren();
        ResetGeneratedReferences();
        Build(sortingOrder);
        ApplyInspectorSettings();

        if (!Application.isPlaying && previewInEditMode && canvasGroup != null)
            canvasGroup.alpha = 1f;
    }

    private void InitializeTransformBinding()
    {
        if (transformBindingInitialized)
            return;

        if (useRootTransformAsSource)
        {
            Vector3 currentScale = transform.localScale;
            bool looksUninitialized =
                Mathf.Abs(currentScale.x) > 0.05f ||
                Mathf.Abs(currentScale.y) > 0.05f ||
                Mathf.Abs(currentScale.z) > 0.05f;

            if (looksUninitialized)
                ApplyCompatibilityTransform();
            else
                SyncCompatibilityFieldsFromTransform();
        }
        else
        {
            ApplyCompatibilityTransform();
        }

        transformBindingInitialized = true;
    }

    private void ApplyCompatibilityTransform()
    {
        transform.localPosition = localOffset;
        float normalizedScale = NormalizeLegacyScale(worldScale);
        worldScale = normalizedScale;
        transform.localScale = Vector3.one * normalizedScale;
    }

    private void SyncCompatibilityFieldsFromTransform()
    {
        if (!useRootTransformAsSource)
            return;

        localOffset = transform.localPosition;

        Vector3 scale = transform.localScale;
        float averageScale = (
            Mathf.Abs(scale.x) +
            Mathf.Abs(scale.y) +
            Mathf.Abs(scale.z)
        ) / 3f;

        worldScale = Mathf.Clamp(averageScale, 0.0001f, 0.02f);
    }

    private void ResolveExistingReferences()
    {
        rootRect = transform as RectTransform;
        worldCanvas = GetComponent<Canvas>();
        canvasGroup = GetComponent<CanvasGroup>();

        Transform visual = transform.Find("CircularPrompt");
        visualRoot = visual as RectTransform;

        if (visualRoot != null)
        {
            glowImage = GetImage(visualRoot, "SoftGlow");
            outerRingImage = GetImage(visualRoot, "GoldenOuterRing");
            rotatingRing = visualRoot.Find("RotatingSegmentedRing") as RectTransform;

            if (rotatingRing != null)
            {
                rotatingRingImage = GetImage(rotatingRing, "Segments");
                orbitMarker = GetImage(rotatingRing, "OrbitSparkTop");
                orbitMarkerSecondary = GetImage(rotatingRing, "OrbitSparkLeft");
                orbitMarkerTertiary = GetImage(rotatingRing, "OrbitSparkRight");
            }

            progressImage = GetImage(visualRoot, "CircularProgress");
            innerAccentRingImage = GetImage(visualRoot, "PinkInnerAccent");
            centerDisc = GetImage(visualRoot, "CenterDisc");
            centerText = GetText(visualRoot, "CenterText");
        }

        instructionText = GetText(transform, "Instruction");

        built = rootRect != null &&
                worldCanvas != null &&
                canvasGroup != null &&
                visualRoot != null &&
                rotatingRing != null &&
                glowImage != null &&
                outerRingImage != null &&
                rotatingRingImage != null &&
                progressImage != null &&
                innerAccentRingImage != null &&
                centerDisc != null &&
                centerText != null &&
                instructionText != null;
    }

    private void Build(int sortOrderOverride)
    {
        EnsureSprites();

        rootRect = transform as RectTransform;
        if (rootRect == null)
            return;

        worldCanvas = GetComponent<Canvas>();
        if (worldCanvas == null)
            worldCanvas = gameObject.AddComponent<Canvas>();

        worldCanvas.renderMode = RenderMode.WorldSpace;
        worldCanvas.overrideSorting = true;
        worldCanvas.sortingOrder = sortOrderOverride;

        CanvasScaler scaler = GetComponent<CanvasScaler>();
        if (scaler == null)
            scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.dynamicPixelsPerUnit = 12f;
        scaler.referencePixelsPerUnit = 100f;

        GraphicRaycaster raycaster = GetComponent<GraphicRaycaster>();
        if (raycaster == null)
            raycaster = gameObject.AddComponent<GraphicRaycaster>();
        raycaster.enabled = false;

        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;

        // Somente um valor inicial. Depois disso o usuário pode editar livremente.
        if (rootRect.sizeDelta.sqrMagnitude < 1f)
            rootRect.sizeDelta = new Vector2(220f, 125f);

        visualRoot = CreateRect("CircularPrompt", rootRect);
        visualRoot.anchorMin = new Vector2(0.5f, 0.5f);
        visualRoot.anchorMax = new Vector2(0.5f, 0.5f);
        visualRoot.pivot = new Vector2(0.5f, 0.5f);
        visualRoot.anchoredPosition = Vector2.zero;

        glowImage = CreateImage("SoftGlow", visualRoot, discSprite);
        outerRingImage = CreateImage("GoldenOuterRing", visualRoot, ringSprite);

        rotatingRing = CreateRect("RotatingSegmentedRing", visualRoot);
        rotatingRingImage = CreateImage("Segments", rotatingRing, segmentedRingSprite);
        Stretch(rotatingRingImage.rectTransform);

        orbitMarker = CreateImage("OrbitSparkTop", rotatingRing, discSprite);
        orbitMarkerSecondary = CreateImage("OrbitSparkLeft", rotatingRing, discSprite);
        orbitMarkerTertiary = CreateImage("OrbitSparkRight", rotatingRing, discSprite);

        progressImage = CreateImage("CircularProgress", visualRoot, ringSprite);
        progressImage.type = Image.Type.Filled;
        progressImage.fillMethod = Image.FillMethod.Radial360;
        progressImage.fillOrigin = 2;
        progressImage.fillClockwise = true;
        progressImage.fillAmount = 0f;

        innerAccentRingImage = CreateImage("PinkInnerAccent", visualRoot, ringSprite);
        centerDisc = CreateImage("CenterDisc", visualRoot, discSprite);

        centerText = CreateText("CenterText", visualRoot, centerFontSize, FontStyles.Bold);
        centerText.text = "E";
        centerText.alignment = TextAlignmentOptions.Center;

        instructionText = CreateText("Instruction", rootRect, instructionFontSize, FontStyles.Bold);
        instructionText.text = "Segure E para pegar";
        instructionText.alignment = TextAlignmentOptions.Center;

        built = true;
    }

    private void ApplyInspectorSettings()
    {
        if (!built)
            return;

        ClampInspectorValues();

        // A raiz não é alterada aqui. O RectTransform fica totalmente livre.
        if (!useRootTransformAsSource)
            ApplyCompatibilityTransform();

        if (worldCanvas != null)
            worldCanvas.sortingOrder = sortingOrder;

        if (visualRoot != null)
            visualRoot.sizeDelta = new Vector2(circleDiameter, circleDiameter);

        ConfigureCenteredCircle(glowImage != null ? glowImage.rectTransform : null, circleDiameter * 1.28f);
        ConfigureCenteredCircle(outerRingImage != null ? outerRingImage.rectTransform : null, circleDiameter);
        ConfigureCenteredCircle(rotatingRing, circleDiameter - 7f);
        ConfigureCenteredCircle(progressImage != null ? progressImage.rectTransform : null, circleDiameter - 6f);
        ConfigureCenteredCircle(
            innerAccentRingImage != null ? innerAccentRingImage.rectTransform : null,
            circleDiameter - progressThickness * 2f - 5f
        );
        ConfigureCenteredCircle(
            centerDisc != null ? centerDisc.rectTransform : null,
            circleDiameter - progressThickness * 2f - innerAccentThickness * 2f - 10f
        );

        ConfigureOrbitMarker(orbitMarker != null ? orbitMarker.rectTransform : null, 0f);
        ConfigureOrbitMarker(orbitMarkerSecondary != null ? orbitMarkerSecondary.rectTransform : null, 120f);
        ConfigureOrbitMarker(orbitMarkerTertiary != null ? orbitMarkerTertiary.rectTransform : null, 240f);

        if (instructionText != null)
        {
            RectTransform rect = instructionText.rectTransform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = instructionOffset;
            rect.sizeDelta = instructionSize;

            instructionText.gameObject.SetActive(showInstructionText);
            instructionText.fontSize = instructionFontSize;
            instructionText.color = instructionTextColor;
            instructionText.outlineColor = textOutlineColor;
            instructionText.outlineWidth = instructionOutlineWidth;
            instructionText.textWrappingMode = TextWrappingModes.NoWrap;
            instructionText.raycastTarget = false;
        }

        if (centerText != null)
        {
            RectTransform rect = centerText.rectTransform;
            float centerSize = Mathf.Max(24f, circleDiameter * 0.5f);
            ConfigureCenteredCircle(rect, centerSize);

            centerText.fontSize = centerFontSize;
            centerText.color = centerTextColor;
            centerText.outlineColor = textOutlineColor;
            centerText.outlineWidth = centerOutlineWidth;
            centerText.textWrappingMode = TextWrappingModes.NoWrap;
            centerText.raycastTarget = false;
        }

        if (glowImage != null)
            glowImage.color = glowColor;
        if (outerRingImage != null)
            outerRingImage.color = outerRingColor;
        if (rotatingRingImage != null)
            rotatingRingImage.color = currentAccentColor;
        if (progressImage != null)
        {
            progressImage.enabled = progressVisible;
            progressImage.fillAmount = currentProgress;
            progressImage.color = currentAccentColor;
        }
        if (innerAccentRingImage != null)
            innerAccentRingImage.color = innerAccentColor;
        if (centerDisc != null)
            centerDisc.color = centerDiscColor;
        if (orbitMarker != null)
            orbitMarker.color = currentAccentColor;
        if (orbitMarkerSecondary != null)
            orbitMarkerSecondary.color = innerAccentColor;
        if (orbitMarkerTertiary != null)
            orbitMarkerTertiary.color = outerRingColor;
    }

    private void AnimatePrompt()
    {
        float time = Time.realtimeSinceStartup;

        if (visualRoot != null)
        {
            float scaleY = Mathf.Max(0.0001f, Mathf.Abs(transform.localScale.y));
            float uiAmplitude = floatingAmplitude / scaleY;
            visualRoot.anchoredPosition = Vector2.up * (Mathf.Sin(time * floatingSpeed) * uiAmplitude);

            float pulse = 1f + Mathf.Sin(time * pulseSpeed) * pulseAmount;
            visualRoot.localScale = Vector3.one * pulse;
        }

        if (rotatingRing != null)
            rotatingRing.localRotation = Quaternion.Euler(0f, 0f, -time * rotationDegreesPerSecond);

        if (glowImage != null)
        {
            Color animatedGlow = glowColor;
            float pulse = 1f + Mathf.Sin(time * glowPulseSpeed) * glowPulseAmount;
            animatedGlow.a = Mathf.Clamp01(glowColor.a * pulse);
            glowImage.color = animatedGlow;
        }
    }

    private void ResetInnerAnimation()
    {
        if (visualRoot != null)
        {
            visualRoot.anchoredPosition = Vector2.zero;
            visualRoot.localScale = Vector3.one;
        }
    }

    private void FaceActiveCamera()
    {
        if (!faceCamera)
            return;

        if (cachedCamera == null || !cachedCamera.isActiveAndEnabled)
            cachedCamera = Camera.main;

        if (cachedCamera == null)
            return;

        Vector3 direction = transform.position - cachedCamera.transform.position;
        if (direction.sqrMagnitude <= 0.0001f)
            return;

        transform.rotation = Quaternion.LookRotation(direction.normalized, cachedCamera.transform.up);
    }

    public void SetVisible(bool visible)
    {
        ResolveExistingReferences();

        if (!built && Application.isPlaying)
            EnsurePersistentVisual();

        if (canvasGroup == null)
            return;

        bool finalVisible = visible || (!Application.isPlaying && previewInEditMode);
        canvasGroup.alpha = finalVisible ? 1f : 0f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;
    }

    public void SetInteractionPrompt(
        string center,
        string instruction,
        float progress01,
        bool showProgress,
        Color accentColor)
    {
        if (!built)
            EnsurePersistentVisual();

        currentAccentColor = accentColor;
        currentProgress = Mathf.Clamp01(progress01);
        progressVisible = showProgress;

        if (centerText != null)
            centerText.text = string.IsNullOrWhiteSpace(center) ? "E" : center;

        if (instructionText != null)
            instructionText.text = instruction ?? string.Empty;

        ApplyInspectorSettings();
    }

    public void SetLocalOffset(Vector3 offset)
    {
        localOffset = offset;
        transform.localPosition = offset;
    }

    public void SetWorldScale(float scale)
    {
        worldScale = NormalizeLegacyScale(scale);
        transform.localScale = Vector3.one * worldScale;
    }

    private void ClampInspectorValues()
    {
        worldScale = Mathf.Clamp(worldScale, 0.0001f, 0.02f);
        circleDiameter = Mathf.Max(48f, circleDiameter);
        progressThickness = Mathf.Clamp(progressThickness, 1f, circleDiameter * 0.25f);
        innerAccentThickness = Mathf.Clamp(innerAccentThickness, 1f, circleDiameter * 0.2f);
        orbitMarkerSize = Mathf.Clamp(orbitMarkerSize, 4f, circleDiameter * 0.25f);
        instructionFontSize = Mathf.Max(8f, instructionFontSize);
        centerFontSize = Mathf.Max(8f, centerFontSize);
        currentProgress = Mathf.Clamp01(currentProgress);
    }

    private void ConfigureOrbitMarker(RectTransform rect, float angleDegrees)
    {
        if (rect == null)
            return;

        float radius = Mathf.Max(4f, (circleDiameter - 7f) * 0.5f - 3f);
        float radians = angleDegrees * Mathf.Deg2Rad;
        Vector2 position = new Vector2(Mathf.Cos(radians), Mathf.Sin(radians)) * radius;

        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = new Vector2(orbitMarkerSize, orbitMarkerSize);
    }

    private void ClearGeneratedChildren()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Transform child = transform.GetChild(i);
            if (Application.isPlaying)
                Destroy(child.gameObject);
            else
                DestroyImmediate(child.gameObject);
        }
    }

    private void ResetGeneratedReferences()
    {
        built = false;
        visualRoot = null;
        rotatingRing = null;
        glowImage = null;
        outerRingImage = null;
        rotatingRingImage = null;
        progressImage = null;
        innerAccentRingImage = null;
        centerDisc = null;
        orbitMarker = null;
        orbitMarkerSecondary = null;
        orbitMarkerTertiary = null;
        centerText = null;
        instructionText = null;
    }

    private static Image GetImage(Transform parent, string childName)
    {
        if (parent == null)
            return null;
        Transform child = parent.Find(childName);
        return child != null ? child.GetComponent<Image>() : null;
    }

    private static TextMeshProUGUI GetText(Transform parent, string childName)
    {
        if (parent == null)
            return null;
        Transform child = parent.Find(childName);
        return child != null ? child.GetComponent<TextMeshProUGUI>() : null;
    }

    private static RectTransform CreateRect(string name, Transform parent)
    {
        GameObject value = new GameObject(name, typeof(RectTransform));
        value.transform.SetParent(parent, false);
        return value.GetComponent<RectTransform>();
    }

    private static Image CreateImage(string name, Transform parent, Sprite sprite)
    {
        RectTransform rect = CreateRect(name, parent);
        Image image = rect.gameObject.AddComponent<Image>();
        image.sprite = sprite;
        image.raycastTarget = false;
        return image;
    }

    private static TextMeshProUGUI CreateText(
        string name,
        Transform parent,
        float fontSize,
        FontStyles style)
    {
        RectTransform rect = CreateRect(name, parent);
        TextMeshProUGUI text = rect.gameObject.AddComponent<TextMeshProUGUI>();
        text.fontSize = fontSize;
        text.fontStyle = style;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.raycastTarget = false;
        return text;
    }

    private static void ConfigureCenteredCircle(RectTransform rect, float diameter)
    {
        if (rect == null)
            return;

        diameter = Mathf.Max(1f, diameter);
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(diameter, diameter);
    }

    private static void Stretch(RectTransform rect)
    {
        if (rect == null)
            return;

        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static float NormalizeLegacyScale(float value)
    {
        if (value >= 0.006f)
            return 0.0023f;
        return Mathf.Clamp(value, 0.0001f, 0.02f);
    }

    private static void EnsureSprites()
    {
        if (discSprite == null)
        {
            discTexture = CreateCircleTexture(128, 0f, 0.98f);
            discTexture.name = "NewspaperPrompt_Disc";
            discTexture.hideFlags = HideFlags.HideAndDontSave;
            discSprite = Sprite.Create(
                discTexture,
                new Rect(0f, 0f, discTexture.width, discTexture.height),
                new Vector2(0.5f, 0.5f),
                100f
            );
            discSprite.hideFlags = HideFlags.HideAndDontSave;
        }

        if (ringSprite == null)
        {
            ringTexture = CreateCircleTexture(128, 0.76f, 0.98f);
            ringTexture.name = "NewspaperPrompt_Ring";
            ringTexture.hideFlags = HideFlags.HideAndDontSave;
            ringSprite = Sprite.Create(
                ringTexture,
                new Rect(0f, 0f, ringTexture.width, ringTexture.height),
                new Vector2(0.5f, 0.5f),
                100f
            );
            ringSprite.hideFlags = HideFlags.HideAndDontSave;
        }

        if (segmentedRingSprite == null)
        {
            segmentedRingTexture = CreateSegmentedRingTexture(128, 0.75f, 0.98f, 12, 0.68f);
            segmentedRingTexture.name = "NewspaperPrompt_SegmentedRing";
            segmentedRingTexture.hideFlags = HideFlags.HideAndDontSave;
            segmentedRingSprite = Sprite.Create(
                segmentedRingTexture,
                new Rect(0f, 0f, segmentedRingTexture.width, segmentedRingTexture.height),
                new Vector2(0.5f, 0.5f),
                100f
            );
            segmentedRingSprite.hideFlags = HideFlags.HideAndDontSave;
        }
    }

    private static Texture2D CreateCircleTexture(int size, float innerRadius, float outerRadius)
    {
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false, true);
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;

        Color32[] pixels = new Color32[size * size];
        float center = (size - 1) * 0.5f;
        float radius = size * 0.5f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = (x - center) / radius;
                float dy = (y - center) / radius;
                float distance = Mathf.Sqrt(dx * dx + dy * dy);
                float outerAlpha = 1f - Mathf.SmoothStep(outerRadius - 0.025f, outerRadius + 0.01f, distance);
                float innerAlpha = innerRadius <= 0f
                    ? 1f
                    : Mathf.SmoothStep(innerRadius - 0.015f, innerRadius + 0.025f, distance);
                byte alpha = (byte)Mathf.RoundToInt(Mathf.Clamp01(outerAlpha * innerAlpha) * 255f);
                pixels[y * size + x] = new Color32(255, 255, 255, alpha);
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply(false, true);
        return texture;
    }

    private static Texture2D CreateSegmentedRingTexture(
        int size,
        float innerRadius,
        float outerRadius,
        int segmentCount,
        float segmentFill)
    {
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false, true);
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;

        Color32[] pixels = new Color32[size * size];
        float center = (size - 1) * 0.5f;
        float radius = size * 0.5f;
        float segmentAngle = Mathf.PI * 2f / Mathf.Max(1, segmentCount);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = (x - center) / radius;
                float dy = (y - center) / radius;
                float distance = Mathf.Sqrt(dx * dx + dy * dy);
                float angle = Mathf.Atan2(dy, dx) + Mathf.PI;
                float withinSegment = Mathf.Repeat(angle, segmentAngle) / segmentAngle;
                bool segmentVisible = withinSegment <= segmentFill;

                float outerAlpha = 1f - Mathf.SmoothStep(outerRadius - 0.025f, outerRadius + 0.01f, distance);
                float innerAlpha = Mathf.SmoothStep(innerRadius - 0.015f, innerRadius + 0.025f, distance);
                float alpha01 = segmentVisible ? Mathf.Clamp01(outerAlpha * innerAlpha) : 0f;
                byte alpha = (byte)Mathf.RoundToInt(alpha01 * 255f);
                pixels[y * size + x] = new Color32(255, 255, 255, alpha);
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply(false, true);
        return texture;
    }
}