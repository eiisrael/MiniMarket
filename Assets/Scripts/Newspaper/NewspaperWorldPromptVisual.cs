using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Prompt circular compacto para as interações do sistema de jornal.
///
/// Versão 2.0:
/// - círculo real, sem painel quadrado;
/// - progresso radial;
/// - anel segmentado giratório;
/// - brilho, pulsação, flutuação e partículas orbitais;
/// - tema infantil/gamer compatível com o HUD dourado, rosa e verde;
/// - parâmetros editáveis em tempo real no Inspector durante o Play Mode.
/// </summary>
[DisallowMultipleComponent]
public sealed class NewspaperWorldPromptVisual : MonoBehaviour
{
    [Header("Referências Runtime")]
    public Canvas worldCanvas;
    public CanvasGroup canvasGroup;
    public RectTransform rootRect;
    public RectTransform visualRoot;
    public RectTransform rotatingRing;

    public Image glowImage;
    public Image outerRingImage;
    public Image rotatingRingImage;
    public Image progressImage;
    public Image innerAccentRingImage;
    public Image centerDisc;
    public Image orbitMarker;
    public Image orbitMarkerSecondary;
    public Image orbitMarkerTertiary;

    public TextMeshProUGUI centerText;
    public TextMeshProUGUI instructionText;

    [Header("Posição e Escala")]
    [Tooltip("Escala final do prompt em World Space. Pode ser alterada em tempo real.")]
    [Min(0.0001f)] public float worldScale = 0.0027f;

    [Tooltip("Posição local do prompt em relação ao objeto pai.")]
    public Vector3 localOffset = new Vector3(0f, 1.35f, 0f);

    [Min(48f)] public float circleDiameter = 108f;
    [Min(1f)] public float progressThickness = 12f;
    [Min(1f)] public float innerAccentThickness = 8f;
    [Min(4f)] public float orbitMarkerSize = 12f;

    [Header("Texto")]
    public Vector2 instructionOffset = new Vector2(0f, 78f);
    public Vector2 instructionSize = new Vector2(290f, 40f);
    [Min(8f)] public float instructionFontSize = 20f;
    [Min(8f)] public float centerFontSize = 38f;
    [Range(0f, 1f)] public float instructionOutlineWidth = 0.18f;
    [Range(0f, 1f)] public float centerOutlineWidth = 0.22f;

    [Header("Animação")]
    public bool faceCamera = true;

    [Tooltip("Mantido com este nome para compatibilidade com os controladores existentes.")]
    [Min(0f)] public float rotationDegreesPerSecond = 42f;

    [Min(0f)] public float floatingAmplitude = 0.035f;
    [Min(0f)] public float floatingSpeed = 2.15f;
    [Range(0f, 0.25f)] public float pulseAmount = 0.055f;
    [Min(0f)] public float pulseSpeed = 3.2f;
    [Range(0f, 0.5f)] public float glowPulseAmount = 0.16f;
    [Min(0f)] public float glowPulseSpeed = 3.8f;

    [Header("Tema Infantil / Gamer")]
    public Color glowColor = new Color32(255, 220, 84, 68);
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

    private Camera cachedCamera;
    private Vector3 baseLocalPosition;
    private bool built;

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
        int sortingOrder = 260)
    {
        GameObject root = new GameObject(objectName, typeof(RectTransform));
        root.transform.SetParent(parent, false);

        NewspaperWorldPromptVisual visual = root.AddComponent<NewspaperWorldPromptVisual>();
        visual.localOffset = requestedLocalOffset;
        visual.worldScale = NormalizeLegacyScale(requestedWorldScale);
        visual.Build(sortingOrder);
        visual.SetVisible(false);
        return visual;
    }

    private void Awake()
    {
        ClampInspectorValues();
        EnsureBuilt();
        ApplyInspectorSettings();
    }

    private void OnEnable()
    {
        ClampInspectorValues();
        EnsureBuilt();
        ApplyInspectorSettings();
    }

    private void OnValidate()
    {
        ClampInspectorValues();

        if (Application.isPlaying && isActiveAndEnabled)
        {
            EnsureBuilt();
            ApplyInspectorSettings();
        }
    }

    private void LateUpdate()
    {
        if (!built)
            return;

        // Atualização proposital em cada frame para permitir edição visual em tempo real.
        ApplyInspectorSettings();
        AnimatePrompt();
        FaceActiveCamera();
    }

    private void EnsureBuilt()
    {
        if (built)
            return;

        if (visualRoot != null && worldCanvas != null)
        {
            built = true;
            baseLocalPosition = localOffset;
            return;
        }

        Build(260);
    }

    private void Build(int sortingOrder)
    {
        EnsureSprites();

        worldCanvas = GetComponent<Canvas>();
        if (worldCanvas == null)
            worldCanvas = gameObject.AddComponent<Canvas>();

        worldCanvas.renderMode = RenderMode.WorldSpace;
        worldCanvas.overrideSorting = true;
        worldCanvas.sortingOrder = sortingOrder;

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

        rootRect = transform as RectTransform;
        rootRect.sizeDelta = new Vector2(320f, 210f);

        visualRoot = CreateRect("CircularPrompt", rootRect);
        visualRoot.anchorMin = new Vector2(0.5f, 0.5f);
        visualRoot.anchorMax = new Vector2(0.5f, 0.5f);
        visualRoot.pivot = new Vector2(0.5f, 0.5f);
        visualRoot.anchoredPosition = Vector2.zero;

        instructionText = CreateText("Instruction", visualRoot, instructionFontSize, FontStyles.Bold);
        instructionText.text = "Segure E para pegar";
        instructionText.alignment = TextAlignmentOptions.Center;

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

        built = true;
        baseLocalPosition = localOffset;
        ApplyInspectorSettings();
    }

    private void ApplyInspectorSettings()
    {
        if (!built)
            return;

        ClampInspectorValues();
        baseLocalPosition = localOffset;

        transform.localScale = Vector3.one * worldScale;

        if (rootRect != null)
            rootRect.sizeDelta = new Vector2(320f, 210f);

        if (visualRoot != null)
            visualRoot.sizeDelta = new Vector2(320f, 210f);

        ConfigureCenteredCircle(glowImage != null ? glowImage.rectTransform : null, circleDiameter * 1.32f);
        ConfigureCenteredCircle(outerRingImage != null ? outerRingImage.rectTransform : null, circleDiameter);
        ConfigureCenteredCircle(rotatingRing, circleDiameter - 8f);
        ConfigureCenteredCircle(progressImage != null ? progressImage.rectTransform : null, circleDiameter - 7f);
        ConfigureCenteredCircle(
            innerAccentRingImage != null ? innerAccentRingImage.rectTransform : null,
            circleDiameter - progressThickness * 2f - 7f
        );
        ConfigureCenteredCircle(
            centerDisc != null ? centerDisc.rectTransform : null,
            circleDiameter - progressThickness * 2f - innerAccentThickness * 2f - 13f
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
            float centerSize = Mathf.Max(30f, circleDiameter * 0.48f);
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
        float time = Time.unscaledTime;

        transform.localPosition = baseLocalPosition +
                                  Vector3.up * (Mathf.Sin(time * floatingSpeed) * floatingAmplitude);

        if (rotatingRing != null)
        {
            rotatingRing.localRotation = Quaternion.Euler(
                0f,
                0f,
                -time * rotationDegreesPerSecond
            );
        }

        if (visualRoot != null)
        {
            float pulse = 1f + Mathf.Sin(time * pulseSpeed) * pulseAmount;
            visualRoot.localScale = Vector3.one * pulse;
        }

        if (glowImage != null)
        {
            Color animatedGlow = glowColor;
            float pulse = 1f + Mathf.Sin(time * glowPulseSpeed) * glowPulseAmount;
            animatedGlow.a = Mathf.Clamp01(glowColor.a * pulse);
            glowImage.color = animatedGlow;
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

        transform.rotation = Quaternion.LookRotation(
            direction.normalized,
            cachedCamera.transform.up
        );
    }

    public void SetVisible(bool visible)
    {
        EnsureBuilt();

        if (canvasGroup == null)
            return;

        canvasGroup.alpha = visible ? 1f : 0f;
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
        EnsureBuilt();

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
        baseLocalPosition = offset;
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
        progressThickness = Mathf.Clamp(progressThickness, 1f, circleDiameter * 0.22f);
        innerAccentThickness = Mathf.Clamp(innerAccentThickness, 1f, circleDiameter * 0.18f);
        orbitMarkerSize = Mathf.Clamp(orbitMarkerSize, 4f, circleDiameter * 0.22f);
        instructionFontSize = Mathf.Max(8f, instructionFontSize);
        centerFontSize = Mathf.Max(8f, centerFontSize);
        currentProgress = Mathf.Clamp01(currentProgress);
    }

    private void ConfigureOrbitMarker(RectTransform marker, float angleDegrees)
    {
        if (marker == null || rotatingRing == null)
            return;

        marker.anchorMin = new Vector2(0.5f, 0.5f);
        marker.anchorMax = new Vector2(0.5f, 0.5f);
        marker.pivot = new Vector2(0.5f, 0.5f);
        marker.sizeDelta = Vector2.one * orbitMarkerSize;

        float radius = Mathf.Max(8f, (circleDiameter - 8f) * 0.5f - orbitMarkerSize * 0.25f);
        float radians = angleDegrees * Mathf.Deg2Rad;
        marker.anchoredPosition = new Vector2(
            Mathf.Sin(radians) * radius,
            Mathf.Cos(radians) * radius
        );
    }

    private static void ConfigureCenteredCircle(RectTransform rect, float diameter)
    {
        if (rect == null)
            return;

        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = Vector2.one * Mathf.Max(1f, diameter);
    }

    private static float NormalizeLegacyScale(float scale)
    {
        scale = Mathf.Max(0.0001f, scale);

        // As versões anteriores usavam aproximadamente 0.008, deixando o prompt gigante.
        // Valores antigos são convertidos automaticamente para o novo padrão compacto.
        return scale > 0.0045f ? scale * 0.34f : scale;
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

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static void EnsureSprites()
    {
        if (discSprite == null)
        {
            discTexture = CreateCircleTexture(128, 0f, 0.98f);
            discTexture.name = "NewspaperPromptV2_Disc";
            discTexture.hideFlags = HideFlags.HideAndDontSave;

            discSprite = Sprite.Create(
                discTexture,
                new Rect(0f, 0f, discTexture.width, discTexture.height),
                new Vector2(0.5f, 0.5f),
                100f
            );
            discSprite.name = "NewspaperPromptV2_Disc";
            discSprite.hideFlags = HideFlags.HideAndDontSave;
        }

        if (ringSprite == null)
        {
            ringTexture = CreateCircleTexture(128, 0.76f, 0.98f);
            ringTexture.name = "NewspaperPromptV2_Ring";
            ringTexture.hideFlags = HideFlags.HideAndDontSave;

            ringSprite = Sprite.Create(
                ringTexture,
                new Rect(0f, 0f, ringTexture.width, ringTexture.height),
                new Vector2(0.5f, 0.5f),
                100f
            );
            ringSprite.name = "NewspaperPromptV2_Ring";
            ringSprite.hideFlags = HideFlags.HideAndDontSave;
        }

        if (segmentedRingSprite == null)
        {
            segmentedRingTexture = CreateSegmentedRingTexture(128, 0.76f, 0.98f, 10, 0.28f);
            segmentedRingTexture.name = "NewspaperPromptV2_SegmentedRing";
            segmentedRingTexture.hideFlags = HideFlags.HideAndDontSave;

            segmentedRingSprite = Sprite.Create(
                segmentedRingTexture,
                new Rect(0f, 0f, segmentedRingTexture.width, segmentedRingTexture.height),
                new Vector2(0.5f, 0.5f),
                100f
            );
            segmentedRingSprite.name = "NewspaperPromptV2_SegmentedRing";
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
                float outerAlpha = 1f - Mathf.SmoothStep(
                    outerRadius - 0.025f,
                    outerRadius + 0.01f,
                    distance
                );
                float innerAlpha = innerRadius <= 0f
                    ? 1f
                    : Mathf.SmoothStep(
                        innerRadius - 0.018f,
                        innerRadius + 0.026f,
                        distance
                    );

                byte alpha = (byte)Mathf.RoundToInt(
                    Mathf.Clamp01(outerAlpha * innerAlpha) * 255f
                );
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
        float gapFraction)
    {
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false, true);
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;

        Color32[] pixels = new Color32[size * size];
        float center = (size - 1) * 0.5f;
        float radius = size * 0.5f;
        segmentCount = Mathf.Max(2, segmentCount);
        gapFraction = Mathf.Clamp01(gapFraction);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = (x - center) / radius;
                float dy = (y - center) / radius;
                float distance = Mathf.Sqrt(dx * dx + dy * dy);

                float outerAlpha = 1f - Mathf.SmoothStep(
                    outerRadius - 0.025f,
                    outerRadius + 0.01f,
                    distance
                );
                float innerAlpha = Mathf.SmoothStep(
                    innerRadius - 0.018f,
                    innerRadius + 0.026f,
                    distance
                );

                float angle = Mathf.Atan2(dy, dx) / (Mathf.PI * 2f) + 0.5f;
                float segmentPosition = Mathf.Repeat(angle * segmentCount, 1f);
                float segmentAlpha = segmentPosition <= 1f - gapFraction ? 1f : 0f;

                byte alpha = (byte)Mathf.RoundToInt(
                    Mathf.Clamp01(outerAlpha * innerAlpha * segmentAlpha) * 255f
                );
                pixels[y * size + x] = new Color32(255, 255, 255, alpha);
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply(false, true);
        return texture;
    }
}
