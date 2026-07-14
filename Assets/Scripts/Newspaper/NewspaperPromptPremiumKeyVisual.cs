using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Camada visual premium para a tecla de interação do jornal.
///
/// Funciona sobre o NewspaperWorldPromptVisual existente, sem alterar a lógica de pegar
/// ou colocar. A hierarquia criada é persistente, selecionável e editável no Inspector.
/// As animações atuam somente em wrappers próprios, preservando os transforms dos
/// elementos visuais que o usuário edita.
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
public sealed class NewspaperPromptPremiumKeyVisual : MonoBehaviour
{
    private const int CurrentSchemaVersion = 1;

    [Header("Ativação")]
    public bool visualEnabled = true;
    public bool previewInEditMode = true;
    public bool animateInPlayMode = true;
    public bool animateInEditMode;

    [Tooltip("Desliga a imagem quadrada antiga do centro e usa o novo círculo procedural.")]
    public bool disableLegacyCenterDisc = true;

    [Header("Edição pelo Inspector")]
    [Tooltip("Marcado: posição, rotação, escala e tamanho dos filhos vêm dos próprios RectTransforms.")]
    public bool useChildTransformsAsSource = true;

    [Tooltip("Marcado: cores e transparências vêm dos componentes gráficos filhos.")]
    public bool useChildColorsAsSource = true;

    [Tooltip("Marcado: fonte, tamanho, cor e contorno do E vêm do TextMeshPro existente.")]
    public bool useCenterTextStyleAsSource = true;

    [Header("Dimensões padrão")]
    [Min(36f)] public float keyDiameter = 62f;
    [Min(40f)] public float glowDiameter = 86f;
    [Min(1f)] public float outerRingThickness = 7f;
    [Min(1f)] public float accentRingThickness = 4f;
    [Min(2f)] public float sparkleSize = 7f;
    [Min(1f)] public float sparkleOrbitRadius = 39f;

    [Header("Cores padrão")]
    public Color glowColor = new Color32(69, 211, 255, 80);
    public Color outerRingColor = new Color32(255, 214, 70, 255);
    public Color accentRingColor = new Color32(255, 94, 178, 255);
    public Color centerColor = new Color32(25, 34, 54, 250);
    public Color highlightColor = new Color32(255, 255, 255, 46);
    public Color sparklePrimaryColor = new Color32(255, 238, 104, 255);
    public Color sparkleSecondaryColor = new Color32(88, 225, 255, 255);
    public Color sparkleTertiaryColor = new Color32(255, 110, 190, 255);
    public Color centerTextColor = Color.white;
    public Color centerTextOutlineColor = new Color32(5, 9, 18, 255);

    [Header("Animação do brilho")]
    [Range(0f, 0.35f)] public float glowScalePulse = 0.10f;
    [Range(0f, 0.75f)] public float glowAlphaPulse = 0.28f;
    [Min(0f)] public float glowPulseSpeed = 3.1f;

    [Header("Animação dos anéis")]
    public bool rotateOrbitEffects = true;
    [Min(0f)] public float orbitRotationSpeed = 42f;
    [Range(0f, 0.35f)] public float accentAlphaPulse = 0.16f;
    [Min(0f)] public float accentPulseSpeed = 4.2f;

    [Header("Animação das partículas")]
    public bool animateSparkles = true;
    [Range(0f, 0.8f)] public float sparkleAlphaPulse = 0.42f;
    [Min(0f)] public float sparklePulseSpeed = 5.2f;

    [Header("Texto central padrão")]
    [Min(8f)] public float centerFontSize = 30f;
    [Range(0f, 1f)] public float centerOutlineWidth = 0.20f;

    [Header("Referências editáveis")]
    public NewspaperWorldPromptVisual worldPrompt;
    public RectTransform circularPrompt;
    public RectTransform premiumRoot;
    public RectTransform glowMotion;
    public RectTransform orbitMotion;
    public RectTransform staticLayer;

    public NewspaperPromptShapeGraphic glowBack;
    public NewspaperPromptShapeGraphic outerRing;
    public NewspaperPromptShapeGraphic accentRing;
    public NewspaperPromptShapeGraphic premiumCenterDisc;
    public NewspaperPromptShapeGraphic centerHighlight;
    public NewspaperPromptShapeGraphic sparkleTop;
    public NewspaperPromptShapeGraphic sparkleLeft;
    public NewspaperPromptShapeGraphic sparkleRight;

    public TextMeshProUGUI centerText;
    public Image legacyCenterDisc;

    [SerializeField, HideInInspector] private int schemaVersion;

    private Vector3 glowMotionBaseScale = Vector3.one;
    private Quaternion orbitMotionBaseRotation = Quaternion.identity;
    private Color glowBaseColor;
    private Color accentBaseColor;
    private Color sparkleTopBaseColor;
    private Color sparkleLeftBaseColor;
    private Color sparkleRightBaseColor;
    private bool animationBaseCaptured;
    private bool animationWasRunning;

    private void Awake()
    {
        ResolveReferences();

        if (Application.isPlaying && premiumRoot == null)
            EnsureEditableHierarchy(false);

        ApplyStaticSettings();
        CaptureAnimationBase();
    }

    private void OnEnable()
    {
        ResolveReferences();

        if (Application.isPlaying && premiumRoot == null)
            EnsureEditableHierarchy(false);

        ApplyStaticSettings();
        CaptureAnimationBase();
        ApplyVisibility();
    }

    private void OnDisable()
    {
        ResetAnimation();
        animationWasRunning = false;
    }

    private void OnValidate()
    {
        ClampValues();
        ResolveReferences();

        if (premiumRoot != null)
        {
            ApplyStaticSettings();
            CaptureAnimationBase();
        }
    }

    private void LateUpdate()
    {
        if (premiumRoot == null)
            return;

        ApplyVisibility();

        bool shouldAnimate = visualEnabled &&
                             (Application.isPlaying ? animateInPlayMode : animateInEditMode);

        if (shouldAnimate)
        {
            if (!animationWasRunning)
            {
                CaptureAnimationBase();
                animationWasRunning = true;
            }

            AnimateVisual();
        }
        else if (animationWasRunning)
        {
            ResetAnimation();
            animationWasRunning = false;
            CaptureAnimationBase();
        }
    }

    /// <summary>
    /// Cria ou repara a hierarquia persistente. Retorna true apenas quando algo
    /// serializável foi criado ou atualizado, permitindo ao instalador marcar a cena
    /// como modificada somente uma vez.
    /// </summary>
    public bool EnsureEditableHierarchy(bool forceApplyDefaults)
    {
        ResolveReferences();

        if (worldPrompt == null)
            worldPrompt = GetComponent<NewspaperWorldPromptVisual>();

        if (circularPrompt == null)
            circularPrompt = transform.Find("CircularPrompt") as RectTransform;

        if (circularPrompt == null)
            return false;

        bool changed = false;

        premiumRoot = GetOrCreateRect("PremiumKeyVisual", circularPrompt, ref changed);
        glowMotion = GetOrCreateRect("GlowMotion", premiumRoot, ref changed);
        orbitMotion = GetOrCreateRect("OrbitMotion", premiumRoot, ref changed);
        staticLayer = GetOrCreateRect("StaticLayer", premiumRoot, ref changed);

        glowBack = GetOrCreateShape(
            "DynamicGlow",
            glowMotion,
            NewspaperPromptShapeGraphic.ShapeMode.FilledCircle,
            ref changed
        );
        outerRing = GetOrCreateShape(
            "OuterRing",
            orbitMotion,
            NewspaperPromptShapeGraphic.ShapeMode.Ring,
            ref changed
        );
        accentRing = GetOrCreateShape(
            "AccentRing",
            orbitMotion,
            NewspaperPromptShapeGraphic.ShapeMode.Ring,
            ref changed
        );
        sparkleTop = GetOrCreateShape(
            "SparkleTop",
            orbitMotion,
            NewspaperPromptShapeGraphic.ShapeMode.FilledCircle,
            ref changed
        );
        sparkleLeft = GetOrCreateShape(
            "SparkleLeft",
            orbitMotion,
            NewspaperPromptShapeGraphic.ShapeMode.FilledCircle,
            ref changed
        );
        sparkleRight = GetOrCreateShape(
            "SparkleRight",
            orbitMotion,
            NewspaperPromptShapeGraphic.ShapeMode.FilledCircle,
            ref changed
        );
        premiumCenterDisc = GetOrCreateShape(
            "CenterCircle",
            staticLayer,
            NewspaperPromptShapeGraphic.ShapeMode.FilledCircle,
            ref changed
        );
        centerHighlight = GetOrCreateShape(
            "CenterHighlight",
            staticLayer,
            NewspaperPromptShapeGraphic.ShapeMode.FilledCircle,
            ref changed
        );

        ResolveReferences();

        bool needsSchemaUpgrade = schemaVersion < CurrentSchemaVersion;
        if (needsSchemaUpgrade || forceApplyDefaults)
        {
            ApplyDefaultLayout();
            ApplyDefaultColors();
            ApplyDefaultTextStyle();

            if (worldPrompt != null)
            {
                worldPrompt.useCircularPromptTransformAsSource = true;
                worldPrompt.useGeneratedChildTransformsAsSource = true;
                worldPrompt.useGeneratedGraphicStylesAsSource = true;
                worldPrompt.useInstructionTransformAsSource = true;
                worldPrompt.useInstructionGraphicAsSource = true;
            }

            schemaVersion = CurrentSchemaVersion;
            changed = true;
        }

        ApplyStaticSettings();
        CaptureAnimationBase();
        return changed;
    }

    [ContextMenu("Jornal/Aplicar visual premium padrão")]
    public void ApplyPremiumDefaults()
    {
        EnsureEditableHierarchy(true);
    }

    [ContextMenu("Jornal/Reparar referências do visual premium")]
    public void RepairReferences()
    {
        ResolveReferences();
        ApplyStaticSettings();
        CaptureAnimationBase();
    }

    private void ResolveReferences()
    {
        if (worldPrompt == null)
            worldPrompt = GetComponent<NewspaperWorldPromptVisual>();

        if (circularPrompt == null)
            circularPrompt = transform.Find("CircularPrompt") as RectTransform;

        if (circularPrompt == null)
            return;

        premiumRoot = circularPrompt.Find("PremiumKeyVisual") as RectTransform;

        if (premiumRoot != null)
        {
            glowMotion = premiumRoot.Find("GlowMotion") as RectTransform;
            orbitMotion = premiumRoot.Find("OrbitMotion") as RectTransform;
            staticLayer = premiumRoot.Find("StaticLayer") as RectTransform;
        }

        glowBack = FindShape(glowMotion, "DynamicGlow");
        outerRing = FindShape(orbitMotion, "OuterRing");
        accentRing = FindShape(orbitMotion, "AccentRing");
        sparkleTop = FindShape(orbitMotion, "SparkleTop");
        sparkleLeft = FindShape(orbitMotion, "SparkleLeft");
        sparkleRight = FindShape(orbitMotion, "SparkleRight");
        premiumCenterDisc = FindShape(staticLayer, "CenterCircle");
        centerHighlight = FindShape(staticLayer, "CenterHighlight");

        Transform textTransform = circularPrompt.Find("CenterText");
        centerText = textTransform != null
            ? textTransform.GetComponent<TextMeshProUGUI>()
            : null;

        Transform legacyDiscTransform = circularPrompt.Find("CenterDisc");
        legacyCenterDisc = legacyDiscTransform != null
            ? legacyDiscTransform.GetComponent<Image>()
            : null;
    }

    private void ApplyStaticSettings()
    {
        ClampValues();

        if (premiumRoot == null)
            return;

        if (!useChildTransformsAsSource)
            ApplyDefaultLayout();

        if (!useChildColorsAsSource)
            ApplyDefaultColors();

        if (!useCenterTextStyleAsSource)
            ApplyDefaultTextStyle();

        if (legacyCenterDisc != null)
            legacyCenterDisc.enabled = !disableLegacyCenterDisc;

        SetRaycastDisabled(glowBack);
        SetRaycastDisabled(outerRing);
        SetRaycastDisabled(accentRing);
        SetRaycastDisabled(premiumCenterDisc);
        SetRaycastDisabled(centerHighlight);
        SetRaycastDisabled(sparkleTop);
        SetRaycastDisabled(sparkleLeft);
        SetRaycastDisabled(sparkleRight);

        if (centerText != null)
        {
            centerText.raycastTarget = false;
            centerText.textWrappingMode = TextWrappingModes.NoWrap;
        }

        KeepTextAbovePremiumVisual();
    }

    private void ApplyDefaultLayout()
    {
        if (premiumRoot == null)
            return;

        ConfigureCenteredRect(premiumRoot, glowDiameter);
        Stretch(glowMotion);
        Stretch(orbitMotion);
        Stretch(staticLayer);

        ConfigureCenteredRect(
            glowBack != null ? glowBack.rectTransform : null,
            glowDiameter
        );
        ConfigureCenteredRect(
            outerRing != null ? outerRing.rectTransform : null,
            keyDiameter + 16f
        );
        ConfigureCenteredRect(
            accentRing != null ? accentRing.rectTransform : null,
            keyDiameter + 7f
        );
        ConfigureCenteredRect(
            premiumCenterDisc != null ? premiumCenterDisc.rectTransform : null,
            keyDiameter
        );

        if (centerHighlight != null)
        {
            ConfigureCenteredRect(centerHighlight.rectTransform, keyDiameter * 0.72f);
            centerHighlight.rectTransform.anchoredPosition = new Vector2(
                -keyDiameter * 0.10f,
                keyDiameter * 0.13f
            );
        }

        ConfigureSparkle(
            sparkleTop != null ? sparkleTop.rectTransform : null,
            90f
        );
        ConfigureSparkle(
            sparkleLeft != null ? sparkleLeft.rectTransform : null,
            210f
        );
        ConfigureSparkle(
            sparkleRight != null ? sparkleRight.rectTransform : null,
            330f
        );

        if (outerRing != null)
        {
            outerRing.shape = NewspaperPromptShapeGraphic.ShapeMode.Ring;
            outerRing.ringThickness = Mathf.Clamp01(
                outerRingThickness / Mathf.Max(1f, (keyDiameter + 16f) * 0.5f)
            );
        }

        if (accentRing != null)
        {
            accentRing.shape = NewspaperPromptShapeGraphic.ShapeMode.Ring;
            accentRing.ringThickness = Mathf.Clamp01(
                accentRingThickness / Mathf.Max(1f, (keyDiameter + 7f) * 0.5f)
            );
        }

        KeepTextAbovePremiumVisual();
    }

    private void ApplyDefaultColors()
    {
        if (glowBack != null) glowBack.color = glowColor;
        if (outerRing != null) outerRing.color = outerRingColor;
        if (accentRing != null) accentRing.color = accentRingColor;
        if (premiumCenterDisc != null) premiumCenterDisc.color = centerColor;
        if (centerHighlight != null) centerHighlight.color = highlightColor;
        if (sparkleTop != null) sparkleTop.color = sparklePrimaryColor;
        if (sparkleLeft != null) sparkleLeft.color = sparkleSecondaryColor;
        if (sparkleRight != null) sparkleRight.color = sparkleTertiaryColor;
    }

    private void ApplyDefaultTextStyle()
    {
        if (centerText == null)
            return;

        centerText.fontSize = centerFontSize;
        centerText.color = centerTextColor;
        centerText.outlineColor = centerTextOutlineColor;
        centerText.outlineWidth = centerOutlineWidth;
        centerText.alignment = TextAlignmentOptions.Center;
        centerText.fontStyle = FontStyles.Bold;

        RectTransform textRect = centerText.rectTransform;
        ConfigureCenteredRect(textRect, keyDiameter * 0.72f);
    }

    private void KeepTextAbovePremiumVisual()
    {
        if (premiumRoot == null || centerText == null)
            return;

        int textIndex = centerText.transform.GetSiblingIndex();
        premiumRoot.SetSiblingIndex(Mathf.Max(0, textIndex));
        centerText.transform.SetAsLastSibling();
    }

    private void ApplyVisibility()
    {
        if (premiumRoot == null)
            return;

        bool shouldShow = visualEnabled && (Application.isPlaying || previewInEditMode);
        if (premiumRoot.gameObject.activeSelf != shouldShow)
            premiumRoot.gameObject.SetActive(shouldShow);
    }

    private void CaptureAnimationBase()
    {
        if (glowMotion != null)
            glowMotionBaseScale = glowMotion.localScale;

        if (orbitMotion != null)
            orbitMotionBaseRotation = orbitMotion.localRotation;

        glowBaseColor = glowBack != null ? glowBack.color : glowColor;
        accentBaseColor = accentRing != null ? accentRing.color : accentRingColor;
        sparkleTopBaseColor = sparkleTop != null ? sparkleTop.color : sparklePrimaryColor;
        sparkleLeftBaseColor = sparkleLeft != null ? sparkleLeft.color : sparkleSecondaryColor;
        sparkleRightBaseColor = sparkleRight != null ? sparkleRight.color : sparkleTertiaryColor;
        animationBaseCaptured = true;
    }

    private void AnimateVisual()
    {
        if (!animationBaseCaptured)
            CaptureAnimationBase();

        float time = Time.realtimeSinceStartup;

        if (glowMotion != null)
        {
            float pulse = 1f + Mathf.Sin(time * glowPulseSpeed) * glowScalePulse;
            glowMotion.localScale = glowMotionBaseScale * pulse;
        }

        if (glowBack != null)
        {
            float multiplier = 1f + Mathf.Sin(time * glowPulseSpeed) * glowAlphaPulse;
            glowBack.color = WithAlphaMultiplier(glowBaseColor, multiplier);
        }

        if (orbitMotion != null && rotateOrbitEffects)
        {
            orbitMotion.localRotation = orbitMotionBaseRotation *
                                        Quaternion.Euler(0f, 0f, -time * orbitRotationSpeed);
        }

        if (accentRing != null)
        {
            float multiplier = 1f + Mathf.Sin(time * accentPulseSpeed) * accentAlphaPulse;
            accentRing.color = WithAlphaMultiplier(accentBaseColor, multiplier);
        }

        if (animateSparkles)
        {
            AnimateSparkle(sparkleTop, sparkleTopBaseColor, time, 0f);
            AnimateSparkle(sparkleLeft, sparkleLeftBaseColor, time, 2.0943952f);
            AnimateSparkle(sparkleRight, sparkleRightBaseColor, time, 4.1887903f);
        }
    }

    private void AnimateSparkle(
        NewspaperPromptShapeGraphic sparkle,
        Color baseColor,
        float time,
        float phase)
    {
        if (sparkle == null)
            return;

        float wave = Mathf.Sin(time * sparklePulseSpeed + phase);
        float multiplier = 1f + wave * sparkleAlphaPulse;
        sparkle.color = WithAlphaMultiplier(baseColor, multiplier);
    }

    private void ResetAnimation()
    {
        if (!animationBaseCaptured)
            return;

        if (glowMotion != null)
            glowMotion.localScale = glowMotionBaseScale;

        if (orbitMotion != null)
            orbitMotion.localRotation = orbitMotionBaseRotation;

        if (glowBack != null) glowBack.color = glowBaseColor;
        if (accentRing != null) accentRing.color = accentBaseColor;
        if (sparkleTop != null) sparkleTop.color = sparkleTopBaseColor;
        if (sparkleLeft != null) sparkleLeft.color = sparkleLeftBaseColor;
        if (sparkleRight != null) sparkleRight.color = sparkleRightBaseColor;
    }

    private void ConfigureSparkle(RectTransform rect, float angleDegrees)
    {
        if (rect == null)
            return;

        float radians = angleDegrees * Mathf.Deg2Rad;
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = Vector2.one * sparkleSize;
        rect.anchoredPosition = new Vector2(
            Mathf.Cos(radians),
            Mathf.Sin(radians)
        ) * sparkleOrbitRadius;
        rect.localEulerAngles = Vector3.zero;
        rect.localScale = Vector3.one;
    }

    private static RectTransform GetOrCreateRect(
        string objectName,
        Transform parent,
        ref bool changed)
    {
        Transform existing = parent != null ? parent.Find(objectName) : null;
        RectTransform existingRect = existing as RectTransform;

        if (existingRect != null)
            return existingRect;

        GameObject created = new GameObject(objectName, typeof(RectTransform));
        created.transform.SetParent(parent, false);
        changed = true;
        return created.GetComponent<RectTransform>();
    }

    private static NewspaperPromptShapeGraphic GetOrCreateShape(
        string objectName,
        Transform parent,
        NewspaperPromptShapeGraphic.ShapeMode shape,
        ref bool changed)
    {
        RectTransform rect = GetOrCreateRect(objectName, parent, ref changed);
        NewspaperPromptShapeGraphic graphic = rect.GetComponent<NewspaperPromptShapeGraphic>();

        if (graphic == null)
        {
            graphic = rect.gameObject.AddComponent<NewspaperPromptShapeGraphic>();
            changed = true;
        }

        graphic.shape = shape;
        graphic.raycastTarget = false;
        return graphic;
    }

    private static NewspaperPromptShapeGraphic FindShape(
        Transform parent,
        string childName)
    {
        if (parent == null)
            return null;

        Transform child = parent.Find(childName);
        return child != null
            ? child.GetComponent<NewspaperPromptShapeGraphic>()
            : null;
    }

    private static void ConfigureCenteredRect(RectTransform rect, float size)
    {
        if (rect == null)
            return;

        float safeSize = Mathf.Max(1f, size);
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = Vector2.one * safeSize;
        rect.localEulerAngles = Vector3.zero;
        rect.localScale = Vector3.one;
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
        rect.localEulerAngles = Vector3.zero;
        rect.localScale = Vector3.one;
    }

    private static void SetRaycastDisabled(Graphic graphic)
    {
        if (graphic != null)
            graphic.raycastTarget = false;
    }

    private static Color WithAlphaMultiplier(Color value, float multiplier)
    {
        value.a = Mathf.Clamp01(value.a * Mathf.Max(0f, multiplier));
        return value;
    }

    private void ClampValues()
    {
        keyDiameter = Mathf.Max(36f, keyDiameter);
        glowDiameter = Mathf.Max(keyDiameter + 8f, glowDiameter);
        outerRingThickness = Mathf.Clamp(outerRingThickness, 1f, keyDiameter * 0.25f);
        accentRingThickness = Mathf.Clamp(accentRingThickness, 1f, keyDiameter * 0.20f);
        sparkleSize = Mathf.Clamp(sparkleSize, 2f, keyDiameter * 0.30f);
        sparkleOrbitRadius = Mathf.Max(keyDiameter * 0.5f, sparkleOrbitRadius);
        glowPulseSpeed = Mathf.Max(0f, glowPulseSpeed);
        orbitRotationSpeed = Mathf.Max(0f, orbitRotationSpeed);
        accentPulseSpeed = Mathf.Max(0f, accentPulseSpeed);
        sparklePulseSpeed = Mathf.Max(0f, sparklePulseSpeed);
        centerFontSize = Mathf.Max(8f, centerFontSize);
        centerOutlineWidth = Mathf.Clamp01(centerOutlineWidth);
    }
}
