using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Prompt circular persistente do sistema de jornal.
///
/// O layout é aplicado somente na criação/reparo. Durante o jogo, o componente altera
/// apenas estado, visibilidade, billboard e animações explicitamente habilitadas.
/// RectTransforms, cores, transparências e tamanhos ajustados no Inspector não são
/// reescritos a cada frame.
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
public sealed class NewspaperWorldPromptVisual : MonoBehaviour
{
    [Header("Visualização")]
    public bool previewInEditMode = true;
    public bool animateInEditMode;
    public bool faceCamera;

    [Tooltip("Mantém o billboard vertical e impede que o prompt fique apontado para o céu/chão.")]
    public bool keepBillboardUpright = true;

    [Tooltip("Correção adicional aplicada depois do billboard.")]
    public Vector3 billboardEulerOffset;

    [Range(0f, 1f)] public float visibleOpacity = 1f;
    public bool showInstructionText = true;
    public int sortingOrder = 260;

    [Header("Controle do Transform Raiz")]
    [Tooltip("Marcado: posição, escala e tamanho do objeto raiz vêm diretamente do Inspector.")]
    public bool useRootTransformAsSource = true;

    [Tooltip("Usado apenas quando Use Root Transform As Source está desmarcado ou na criação inicial.")]
    [Min(0.0001f)] public float worldScale = 0.0023f;

    [Tooltip("Usado apenas quando Use Root Transform As Source está desmarcado ou na criação inicial.")]
    public Vector3 localOffset = new Vector3(0f, 0.72f, 0f);

    [Header("CircularPrompt Editável")]
    [Tooltip("Marcado: posição, rotação, escala e tamanho de CircularPrompt vêm do próprio RectTransform.")]
    public bool useCircularPromptTransformAsSource = true;

    [Tooltip("Marcado: RectTransforms dos anéis, disco, brilhos e marcadores não são reposicionados pelo script.")]
    public bool useGeneratedChildTransformsAsSource = true;

    [Tooltip("Marcado: cores, transparências, fonte e estilos dos filhos vêm dos componentes da Hierarchy.")]
    public bool useGeneratedGraphicStylesAsSource = true;

    [Tooltip("Mantém as imagens conhecidas usando máscaras realmente circulares, sem alterar Transform ou cor.")]
    public bool enforceCircularSprites = true;

    [Min(48f)] public float circleDiameter = 86f;
    [Min(1f)] public float progressThickness = 10f;
    [Min(1f)] public float innerAccentThickness = 6f;
    [Min(4f)] public float orbitMarkerSize = 9f;

    [Header("Instruction Editável")]
    [Tooltip("Marcado: posição, rotação, escala, âncoras e tamanho de Instruction vêm do RectTransform dele.")]
    public bool useInstructionTransformAsSource = true;

    [Tooltip("Marcado: fonte, tamanho, cor, alinhamento e contorno vêm do TextMeshPro de Instruction.")]
    public bool useInstructionGraphicAsSource = true;

    public Vector2 instructionOffset = new Vector2(0f, 52f);
    public Vector2 instructionSize = new Vector2(220f, 30f);
    [Min(8f)] public float instructionFontSize = 15f;
    [Min(8f)] public float centerFontSize = 30f;
    [Range(0f, 1f)] public float instructionOutlineWidth = 0.16f;
    [Range(0f, 1f)] public float centerOutlineWidth = 0.20f;

    [Header("Animação")]
    [Min(0f)] public float rotationDegreesPerSecond = 38f;

    [Tooltip("Desmarcado por padrão para preservar a posição editada de CircularPrompt.")]
    public bool animateCircularPromptPosition;

    [Tooltip("Desmarcado por padrão para preservar a escala editada de CircularPrompt.")]
    public bool animateCircularPromptScale;

    [Tooltip("Amplitude em unidades do mundo, aplicada relativamente à posição editada.")]
    [Min(0f)] public float floatingAmplitude = 0.015f;

    [Min(0f)] public float floatingSpeed = 2.1f;
    [Range(0f, 0.25f)] public float pulseAmount = 0.035f;
    [Min(0f)] public float pulseSpeed = 3f;
    [Range(0f, 0.5f)] public float glowPulseAmount = 0.12f;
    [Min(0f)] public float glowPulseSpeed = 3.4f;

    [Header("Tema Infantil / Gamer")]
    public Color glowColor = new Color32(255, 220, 84, 90);
    public Color outerRingColor = new Color32(255, 205, 65, 255);
    public Color innerAccentColor = new Color32(255, 102, 171, 255);
    public Color centerDiscColor = new Color32(22, 29, 45, 244);
    public Color centerTextColor = Color.white;
    public Color instructionTextColor = new Color32(255, 248, 218, 255);
    public Color textOutlineColor = new Color32(10, 12, 20, 245);

    [Header("Migração segura da Put Area")]
    [Tooltip("Repara uma única vez escala minúscula e inclinação 3D deixadas pelas versões anteriores.")]
    public bool repairLegacyPlacementPrompt = true;

    [Min(0.0001f)] public float minimumPlacePromptScale = 0.006f;

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

    [SerializeField, HideInInspector] private bool legacyPlacementRepairApplied;

    private Camera cachedCamera;
    private float nextCameraResolveTime;
    private bool built;
    private bool animationBaseCaptured;
    private Vector2 visualBaseAnchoredPosition;
    private Vector3 visualBaseScale = Vector3.one;
    private Quaternion rotatingRingBaseRotation = Quaternion.identity;
    private Color glowBaseColor = Color.white;

    private static Sprite discSprite;
    private static Sprite ringSprite;
    private static Sprite segmentedRingSprite;
    private static Texture2D discTexture;
    private static Texture2D ringTexture;
    private static Texture2D segmentedRingTexture;

    private sealed class HierarchySnapshot
    {
        public readonly Dictionary<string, RectSnapshot> rects =
            new Dictionary<string, RectSnapshot>();
        public readonly Dictionary<string, GraphicSnapshot> graphics =
            new Dictionary<string, GraphicSnapshot>();
    }

    private struct RectSnapshot
    {
        public Vector2 anchorMin;
        public Vector2 anchorMax;
        public Vector2 pivot;
        public Vector3 anchoredPosition3D;
        public Vector2 sizeDelta;
        public Vector3 localEulerAngles;
        public Vector3 localScale;
    }

    private struct GraphicSnapshot
    {
        public bool enabled;
        public Color color;
        public bool raycastTarget;
        public string text;
        public float fontSize;
        public FontStyles fontStyle;
        public TextAlignmentOptions alignment;
        public Color outlineColor;
        public float outlineWidth;
    }

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
        visual.worldScale = ClampScale(requestedWorldScale);
        visual.sortingOrder = requestedSortingOrder;
        visual.ApplyCompatibilityRootTransform();
        visual.EnsurePersistentVisual();
        visual.SetVisible(false);
        return visual;
    }

    private void Awake()
    {
        ResolveExistingReferences();

        if (Application.isPlaying)
            EnsurePersistentVisual();
        else if (built)
            ApplyStaticInspectorSettings();

        RepairLegacyPlacementPromptOnce();
        CaptureAnimationBase();
    }

    private void OnEnable()
    {
        ResolveExistingReferences();

        if (Application.isPlaying)
            EnsurePersistentVisual();
        else if (built)
            ApplyStaticInspectorSettings();

        RepairLegacyPlacementPromptOnce();
        CaptureAnimationBase();

        if (!Application.isPlaying && previewInEditMode && built)
            ApplyCanvasVisibility(true);
    }

    private void OnValidate()
    {
        ClampInspectorValues();
        ResolveExistingReferences();
        RepairLegacyPlacementPromptOnce();

        if (built)
        {
            CaptureAnimationBase();
            ApplyStaticInspectorSettings();
        }
    }

    private void LateUpdate()
    {
        if (!built)
            return;

        bool shouldAnimate = Application.isPlaying || animateInEditMode;
        if (shouldAnimate)
            AnimatePrompt();
        else
            ResetInnerAnimation();

        if (faceCamera && Application.isPlaying)
            FaceActiveCamera();
    }

    [ContextMenu("Jornal/Reparar visual circular")]
    public void EnsurePersistentVisual()
    {
        ResolveExistingReferences();

        if (!built)
        {
            BuildMissingHierarchy(sortingOrder);
            ResolveExistingReferences();
        }

        EnsureGeneratedSprites();
        CaptureAnimationBase();
        ApplyStaticInspectorSettings();

        if (!Application.isPlaying && previewInEditMode)
            ApplyCanvasVisibility(true);
    }

    [ContextMenu("Jornal/Reconstruir visual circular preservando edições")]
    public void RebuildVisual()
    {
        HierarchySnapshot snapshot = CaptureHierarchySnapshot();
        ClearGeneratedChildren();
        ResetGeneratedReferences();
        BuildMissingHierarchy(sortingOrder);
        ResolveExistingReferences();
        RestoreHierarchySnapshot(snapshot);
        EnsureGeneratedSprites();
        CaptureAnimationBase();
        ApplyStaticInspectorSettings();

        if (!Application.isPlaying && previewInEditMode)
            ApplyCanvasVisibility(true);
    }

    [ContextMenu("Jornal/Aplicar layout padrão aos filhos")]
    public void ApplyDefaultChildLayout()
    {
        ResolveExistingReferences();
        ApplyCircularPromptDefaults();
        ApplyGeneratedChildTransformDefaults();
        ApplyInstructionDefaults();
        CaptureAnimationBase();
    }

    private void BuildMissingHierarchy(int sortOrderOverride)
    {
        EnsureSprites();

        rootRect = transform as RectTransform;
        if (rootRect == null)
            return;

        worldCanvas = GetComponent<Canvas>();
        if (worldCanvas == null)
            worldCanvas = gameObject.AddComponent<Canvas>();

        CanvasScaler scaler = GetComponent<CanvasScaler>();
        if (scaler == null)
            scaler = gameObject.AddComponent<CanvasScaler>();

        GraphicRaycaster raycaster = GetComponent<GraphicRaycaster>();
        if (raycaster == null)
            raycaster = gameObject.AddComponent<GraphicRaycaster>();

        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = gameObject.AddComponent<CanvasGroup>();

        worldCanvas.renderMode = RenderMode.WorldSpace;
        worldCanvas.overrideSorting = true;
        worldCanvas.sortingOrder = sortOrderOverride;
        scaler.dynamicPixelsPerUnit = 12f;
        scaler.referencePixelsPerUnit = 100f;
        raycaster.enabled = false;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;

        if (rootRect.sizeDelta.sqrMagnitude < 1f)
            rootRect.sizeDelta = new Vector2(220f, 125f);

        visualRoot = GetOrCreateRect("CircularPrompt", rootRect);
        glowImage = GetOrCreateImage("SoftGlow", visualRoot, discSprite, glowColor);
        outerRingImage = GetOrCreateImage("GoldenOuterRing", visualRoot, ringSprite, outerRingColor);
        rotatingRing = GetOrCreateRect("RotatingSegmentedRing", visualRoot);
        rotatingRingImage = GetOrCreateImage("Segments", rotatingRing, segmentedRingSprite, currentAccentColor);
        orbitMarker = GetOrCreateImage("OrbitSparkTop", rotatingRing, discSprite, currentAccentColor);
        orbitMarkerSecondary = GetOrCreateImage("OrbitSparkLeft", rotatingRing, discSprite, innerAccentColor);
        orbitMarkerTertiary = GetOrCreateImage("OrbitSparkRight", rotatingRing, discSprite, outerRingColor);
        progressImage = GetOrCreateImage("CircularProgress", visualRoot, ringSprite, currentAccentColor);
        innerAccentRingImage = GetOrCreateImage("PinkInnerAccent", visualRoot, ringSprite, innerAccentColor);
        centerDisc = GetOrCreateImage("CenterDisc", visualRoot, discSprite, centerDiscColor);
        centerText = GetOrCreateText("CenterText", visualRoot, centerFontSize, FontStyles.Bold);
        instructionText = GetOrCreateText("Instruction", rootRect, instructionFontSize, FontStyles.Bold);

        if (centerText != null && string.IsNullOrEmpty(centerText.text))
            centerText.text = "E";
        if (instructionText != null && string.IsNullOrEmpty(instructionText.text))
            instructionText.text = "Segure E para pegar";

        if (centerText != null && centerText.color == Color.white)
            centerText.color = centerTextColor;
        if (instructionText != null && instructionText.color == Color.white)
            instructionText.color = instructionTextColor;

        if (progressImage != null)
        {
            progressImage.type = Image.Type.Filled;
            progressImage.fillMethod = Image.FillMethod.Radial360;
            progressImage.fillOrigin = 2;
            progressImage.fillClockwise = true;
            progressImage.fillAmount = currentProgress;
        }

        ApplyCircularPromptDefaults();
        ApplyGeneratedChildTransformDefaults();
        ApplyInstructionDefaults();
        built = true;
    }

    private void ResolveExistingReferences()
    {
        rootRect = transform as RectTransform;
        worldCanvas = GetComponent<Canvas>();
        canvasGroup = GetComponent<CanvasGroup>();
        visualRoot = transform.Find("CircularPrompt") as RectTransform;

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

    private void ApplyStaticInspectorSettings()
    {
        if (!built)
            return;

        ClampInspectorValues();

        if (!useRootTransformAsSource)
            ApplyCompatibilityRootTransform();
        else
            SyncRootCompatibilityFields();

        if (worldCanvas != null)
        {
            worldCanvas.renderMode = RenderMode.WorldSpace;
            worldCanvas.overrideSorting = true;
            worldCanvas.sortingOrder = sortingOrder;
        }

        if (canvasGroup != null)
        {
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }

        if (!useCircularPromptTransformAsSource)
            ApplyCircularPromptDefaults();

        if (!useGeneratedChildTransformsAsSource)
            ApplyGeneratedChildTransformDefaults();

        if (!useInstructionTransformAsSource)
            ApplyInstructionDefaults();
        else
            SyncInstructionCompatibilityFields();

        ApplyGraphicSettings();
        ApplyRuntimeState();
        EnsureGeneratedSprites();
    }

    private void ApplyGraphicSettings()
    {
        if (!useGeneratedGraphicStylesAsSource)
        {
            if (glowImage != null) glowImage.color = glowColor;
            if (outerRingImage != null) outerRingImage.color = outerRingColor;
            if (rotatingRingImage != null) rotatingRingImage.color = currentAccentColor;
            if (progressImage != null) progressImage.color = currentAccentColor;
            if (innerAccentRingImage != null) innerAccentRingImage.color = innerAccentColor;
            if (centerDisc != null) centerDisc.color = centerDiscColor;
            if (orbitMarker != null) orbitMarker.color = currentAccentColor;
            if (orbitMarkerSecondary != null) orbitMarkerSecondary.color = innerAccentColor;
            if (orbitMarkerTertiary != null) orbitMarkerTertiary.color = outerRingColor;
        }

        if (centerText != null)
        {
            if (!useGeneratedGraphicStylesAsSource)
            {
                centerText.fontSize = centerFontSize;
                centerText.color = centerTextColor;
                centerText.outlineColor = textOutlineColor;
                centerText.outlineWidth = centerOutlineWidth;
                centerText.alignment = TextAlignmentOptions.Center;
            }

            centerText.textWrappingMode = TextWrappingModes.NoWrap;
            centerText.raycastTarget = false;
        }

        if (instructionText != null)
        {
            if (!useInstructionGraphicAsSource)
            {
                instructionText.fontSize = instructionFontSize;
                instructionText.color = instructionTextColor;
                instructionText.outlineColor = textOutlineColor;
                instructionText.outlineWidth = instructionOutlineWidth;
                instructionText.alignment = TextAlignmentOptions.Center;
            }

            instructionText.textWrappingMode = TextWrappingModes.NoWrap;
            instructionText.raycastTarget = false;
            instructionText.gameObject.SetActive(showInstructionText);
        }

        SetRaycastDisabled(glowImage);
        SetRaycastDisabled(outerRingImage);
        SetRaycastDisabled(rotatingRingImage);
        SetRaycastDisabled(progressImage);
        SetRaycastDisabled(innerAccentRingImage);
        SetRaycastDisabled(centerDisc);
        SetRaycastDisabled(orbitMarker);
        SetRaycastDisabled(orbitMarkerSecondary);
        SetRaycastDisabled(orbitMarkerTertiary);
    }

    private void ApplyRuntimeState()
    {
        if (progressImage != null)
        {
            progressImage.enabled = progressVisible;
            progressImage.fillAmount = currentProgress;
        }
    }

    private void ApplyCompatibilityRootTransform()
    {
        transform.localPosition = localOffset;
        worldScale = ClampScale(worldScale);
        transform.localScale = Vector3.one * worldScale;
    }

    private void SyncRootCompatibilityFields()
    {
        localOffset = transform.localPosition;
        Vector3 scale = transform.localScale;
        worldScale = ClampScale(
            (Mathf.Abs(scale.x) + Mathf.Abs(scale.y) + Mathf.Abs(scale.z)) / 3f
        );
    }

    private void SyncInstructionCompatibilityFields()
    {
        if (instructionText == null)
            return;

        RectTransform rect = instructionText.rectTransform;
        instructionOffset = rect.anchoredPosition;
        instructionSize = rect.sizeDelta;

        if (useInstructionGraphicAsSource)
        {
            instructionFontSize = Mathf.Max(1f, instructionText.fontSize);
            instructionTextColor = instructionText.color;
            instructionOutlineWidth = Mathf.Clamp01(instructionText.outlineWidth);
            textOutlineColor = instructionText.outlineColor;
        }
    }

    private void ApplyCircularPromptDefaults()
    {
        if (visualRoot == null)
            return;

        visualRoot.anchorMin = new Vector2(0.5f, 0.5f);
        visualRoot.anchorMax = new Vector2(0.5f, 0.5f);
        visualRoot.pivot = new Vector2(0.5f, 0.5f);
        visualRoot.anchoredPosition = Vector2.zero;
        visualRoot.sizeDelta = new Vector2(circleDiameter, circleDiameter);
        visualRoot.localEulerAngles = Vector3.zero;
        visualRoot.localScale = Vector3.one;
    }

    private void ApplyGeneratedChildTransformDefaults()
    {
        ConfigureCenteredCircle(glowImage != null ? glowImage.rectTransform : null, circleDiameter * 1.28f);
        ConfigureCenteredCircle(outerRingImage != null ? outerRingImage.rectTransform : null, circleDiameter);
        ConfigureCenteredCircle(rotatingRing, circleDiameter - 7f);

        if (rotatingRingImage != null)
            Stretch(rotatingRingImage.rectTransform);

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

        if (centerText != null)
        {
            float centerSize = Mathf.Max(24f, circleDiameter * 0.5f);
            ConfigureCenteredCircle(centerText.rectTransform, centerSize);
        }
    }

    private void ApplyInstructionDefaults()
    {
        if (instructionText == null)
            return;

        RectTransform rect = instructionText.rectTransform;
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = instructionOffset;
        rect.sizeDelta = instructionSize;
        rect.localEulerAngles = Vector3.zero;
        rect.localScale = Vector3.one;

        if (!useInstructionGraphicAsSource)
        {
            instructionText.fontSize = instructionFontSize;
            instructionText.color = instructionTextColor;
            instructionText.outlineColor = textOutlineColor;
            instructionText.outlineWidth = instructionOutlineWidth;
            instructionText.alignment = TextAlignmentOptions.Center;
        }
    }

    private void RepairLegacyPlacementPromptOnce()
    {
        if (!repairLegacyPlacementPrompt || legacyPlacementRepairApplied || !IsPlacementPrompt())
            return;

        bool changed = false;
        Vector3 scale = transform.localScale;
        float averageScale = (Mathf.Abs(scale.x) + Mathf.Abs(scale.y) + Mathf.Abs(scale.z)) / 3f;

        if (averageScale < minimumPlacePromptScale)
        {
            float repairedScale = Mathf.Max(minimumPlacePromptScale, 0.006f);
            transform.localScale = Vector3.one * repairedScale;
            worldScale = repairedScale;
            changed = true;
        }

        if (visualRoot != null)
        {
            Vector3 euler = visualRoot.localEulerAngles;
            float tiltX = Mathf.Abs(Mathf.DeltaAngle(0f, euler.x));
            float tiltY = Mathf.Abs(Mathf.DeltaAngle(0f, euler.y));

            if (tiltX > 1f || tiltY > 1f)
            {
                visualRoot.localEulerAngles = new Vector3(0f, 0f, euler.z);
                changed = true;
            }

            if (visualRoot.localScale.sqrMagnitude < 0.03f)
            {
                visualRoot.localScale = Vector3.one;
                changed = true;
            }
        }

        faceCamera = true;
        keepBillboardUpright = true;
        legacyPlacementRepairApplied = true;

        if (changed)
            CaptureAnimationBase();
    }

    private bool IsPlacementPrompt()
    {
        Transform current = transform;

        while (current != null)
        {
            string value = current.name;
            if (value.IndexOf("PlacePrompt", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                value.IndexOf("Put_Area", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                value.IndexOf("Jornal_Place", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }

            current = current.parent;
        }

        return false;
    }

    private void CaptureAnimationBase()
    {
        if (visualRoot != null)
        {
            visualBaseAnchoredPosition = visualRoot.anchoredPosition;
            visualBaseScale = visualRoot.localScale;
        }

        if (rotatingRing != null)
            rotatingRingBaseRotation = rotatingRing.localRotation;

        if (glowImage != null)
            glowBaseColor = glowImage.color;

        animationBaseCaptured = true;
    }

    private void AnimatePrompt()
    {
        if (!animationBaseCaptured)
            CaptureAnimationBase();

        float time = Time.realtimeSinceStartup;

        if (visualRoot != null)
        {
            if (animateCircularPromptPosition)
            {
                float scaleY = Mathf.Max(0.0001f, Mathf.Abs(transform.lossyScale.y));
                float uiAmplitude = floatingAmplitude / scaleY;
                visualRoot.anchoredPosition = visualBaseAnchoredPosition +
                                              Vector2.up * (Mathf.Sin(time * floatingSpeed) * uiAmplitude);
            }

            if (animateCircularPromptScale)
            {
                float pulse = 1f + Mathf.Sin(time * pulseSpeed) * pulseAmount;
                visualRoot.localScale = visualBaseScale * pulse;
            }
        }

        if (rotatingRing != null && rotationDegreesPerSecond > 0f)
        {
            rotatingRing.localRotation = rotatingRingBaseRotation *
                                         Quaternion.Euler(0f, 0f, -time * rotationDegreesPerSecond);
        }

        if (glowImage != null && glowPulseAmount > 0f)
        {
            Color animatedGlow = glowBaseColor;
            float pulse = 1f + Mathf.Sin(time * glowPulseSpeed) * glowPulseAmount;
            animatedGlow.a = Mathf.Clamp01(glowBaseColor.a * pulse);
            glowImage.color = animatedGlow;
        }
    }

    private void ResetInnerAnimation()
    {
        if (!animationBaseCaptured)
            CaptureAnimationBase();

        if (visualRoot != null)
        {
            if (animateCircularPromptPosition)
                visualRoot.anchoredPosition = visualBaseAnchoredPosition;
            if (animateCircularPromptScale)
                visualRoot.localScale = visualBaseScale;
        }

        if (rotatingRing != null && rotationDegreesPerSecond > 0f)
            rotatingRing.localRotation = rotatingRingBaseRotation;

        if (glowImage != null && glowPulseAmount > 0f)
            glowImage.color = glowBaseColor;
    }

    private void FaceActiveCamera()
    {
        ResolveActiveCamera();
        if (cachedCamera == null)
            return;

        Vector3 direction = transform.position - cachedCamera.transform.position;

        if (keepBillboardUpright)
            direction = Vector3.ProjectOnPlane(direction, Vector3.up);

        if (direction.sqrMagnitude <= 0.0001f)
        {
            direction = keepBillboardUpright
                ? Vector3.ProjectOnPlane(cachedCamera.transform.forward, Vector3.up)
                : cachedCamera.transform.forward;
        }

        if (direction.sqrMagnitude <= 0.0001f)
            return;

        Vector3 up = keepBillboardUpright ? Vector3.up : cachedCamera.transform.up;
        transform.rotation = Quaternion.LookRotation(direction.normalized, up) *
                             Quaternion.Euler(billboardEulerOffset);
    }

    private void ResolveActiveCamera()
    {
        if (cachedCamera != null && cachedCamera.isActiveAndEnabled)
            return;

        if (Time.unscaledTime < nextCameraResolveTime)
            return;

        nextCameraResolveTime = Time.unscaledTime + 0.5f;

        PlayerCameraController controller =
            UnityEngine.Object.FindAnyObjectByType<PlayerCameraController>(FindObjectsInactive.Include);

        if (controller != null && controller.gameCamera != null &&
            controller.gameCamera.isActiveAndEnabled)
        {
            cachedCamera = controller.gameCamera;
            return;
        }

        cachedCamera = Camera.main;
    }

    public void SetVisible(bool visible)
    {
        ResolveExistingReferences();

        if (!built && Application.isPlaying)
            EnsurePersistentVisual();

        bool finalVisible = visible || (!Application.isPlaying && previewInEditMode);
        ApplyCanvasVisibility(finalVisible);
    }

    private void ApplyCanvasVisibility(bool visible)
    {
        if (canvasGroup == null)
            return;

        canvasGroup.alpha = visible ? visibleOpacity : 0f;
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

        if (progressImage != null)
        {
            progressImage.enabled = progressVisible;
            progressImage.fillAmount = currentProgress;

            if (!useGeneratedGraphicStylesAsSource)
                progressImage.color = currentAccentColor;
        }

        if (!useGeneratedGraphicStylesAsSource)
        {
            if (rotatingRingImage != null) rotatingRingImage.color = currentAccentColor;
            if (orbitMarker != null) orbitMarker.color = currentAccentColor;
        }
    }

    public void SetLocalOffset(Vector3 offset)
    {
        localOffset = offset;
        transform.localPosition = offset;
    }

    public void SetWorldScale(float scale)
    {
        worldScale = ClampScale(scale);
        transform.localScale = Vector3.one * worldScale;
    }

    private HierarchySnapshot CaptureHierarchySnapshot()
    {
        HierarchySnapshot snapshot = new HierarchySnapshot();
        RectTransform[] rects = GetComponentsInChildren<RectTransform>(true);

        for (int i = 0; i < rects.Length; i++)
        {
            RectTransform rect = rects[i];
            if (rect == null || rect == rootRect)
                continue;

            string path = GetRelativePath(rect.transform);
            snapshot.rects[path] = new RectSnapshot
            {
                anchorMin = rect.anchorMin,
                anchorMax = rect.anchorMax,
                pivot = rect.pivot,
                anchoredPosition3D = rect.anchoredPosition3D,
                sizeDelta = rect.sizeDelta,
                localEulerAngles = rect.localEulerAngles,
                localScale = rect.localScale
            };

            Graphic graphic = rect.GetComponent<Graphic>();
            if (graphic == null)
                continue;

            GraphicSnapshot value = new GraphicSnapshot
            {
                enabled = graphic.enabled,
                color = graphic.color,
                raycastTarget = graphic.raycastTarget
            };

            TMP_Text text = graphic as TMP_Text;
            if (text != null)
            {
                value.text = text.text;
                value.fontSize = text.fontSize;
                value.fontStyle = text.fontStyle;
                value.alignment = text.alignment;
                value.outlineColor = text.outlineColor;
                value.outlineWidth = text.outlineWidth;
            }

            snapshot.graphics[path] = value;
        }

        return snapshot;
    }

    private void RestoreHierarchySnapshot(HierarchySnapshot snapshot)
    {
        if (snapshot == null)
            return;

        foreach (KeyValuePair<string, RectSnapshot> pair in snapshot.rects)
        {
            Transform value = transform.Find(pair.Key);
            RectTransform rect = value as RectTransform;
            if (rect == null)
                continue;

            RectSnapshot state = pair.Value;
            rect.anchorMin = state.anchorMin;
            rect.anchorMax = state.anchorMax;
            rect.pivot = state.pivot;
            rect.anchoredPosition3D = state.anchoredPosition3D;
            rect.sizeDelta = state.sizeDelta;
            rect.localEulerAngles = state.localEulerAngles;
            rect.localScale = state.localScale;
        }

        foreach (KeyValuePair<string, GraphicSnapshot> pair in snapshot.graphics)
        {
            Transform value = transform.Find(pair.Key);
            Graphic graphic = value != null ? value.GetComponent<Graphic>() : null;
            if (graphic == null)
                continue;

            GraphicSnapshot state = pair.Value;
            graphic.enabled = state.enabled;
            graphic.color = state.color;
            graphic.raycastTarget = state.raycastTarget;

            TMP_Text text = graphic as TMP_Text;
            if (text == null)
                continue;

            text.text = state.text;
            text.fontSize = state.fontSize;
            text.fontStyle = state.fontStyle;
            text.alignment = state.alignment;
            text.outlineColor = state.outlineColor;
            text.outlineWidth = state.outlineWidth;
        }
    }

    private string GetRelativePath(Transform target)
    {
        if (target == null || target == transform)
            return string.Empty;

        string path = target.name;
        Transform current = target.parent;

        while (current != null && current != transform)
        {
            path = current.name + "/" + path;
            current = current.parent;
        }

        return path;
    }

    private void ClearGeneratedChildren()
    {
        DestroyChildIfPresent("CircularPrompt");
        DestroyChildIfPresent("Instruction");
    }

    private void DestroyChildIfPresent(string childName)
    {
        Transform child = transform.Find(childName);
        if (child == null)
            return;

        if (Application.isPlaying)
            Destroy(child.gameObject);
        else
            DestroyImmediate(child.gameObject);
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
        animationBaseCaptured = false;
    }

    private void EnsureGeneratedSprites()
    {
        if (!enforceCircularSprites)
            return;

        EnsureSprites();
        AssignSprite(glowImage, discSprite);
        AssignSprite(outerRingImage, ringSprite);
        AssignSprite(rotatingRingImage, segmentedRingSprite);
        AssignSprite(progressImage, ringSprite);
        AssignSprite(innerAccentRingImage, ringSprite);
        AssignSprite(centerDisc, discSprite);
        AssignSprite(orbitMarker, discSprite);
        AssignSprite(orbitMarkerSecondary, discSprite);
        AssignSprite(orbitMarkerTertiary, discSprite);
    }

    private static void AssignSprite(Image image, Sprite sprite)
    {
        if (image == null || sprite == null)
            return;

        image.sprite = sprite;
        image.raycastTarget = false;
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

    private static RectTransform GetOrCreateRect(string objectName, Transform parent)
    {
        Transform existing = parent != null ? parent.Find(objectName) : null;
        RectTransform existingRect = existing as RectTransform;
        if (existingRect != null)
            return existingRect;

        GameObject value = new GameObject(objectName, typeof(RectTransform));
        value.transform.SetParent(parent, false);
        return value.GetComponent<RectTransform>();
    }

    private static Image GetOrCreateImage(
        string objectName,
        Transform parent,
        Sprite sprite,
        Color defaultColor)
    {
        RectTransform rect = GetOrCreateRect(objectName, parent);
        Image image = rect.GetComponent<Image>();
        bool created = image == null;

        if (created)
            image = rect.gameObject.AddComponent<Image>();

        image.sprite = sprite;
        image.raycastTarget = false;

        if (created)
            image.color = defaultColor;

        return image;
    }

    private static TextMeshProUGUI GetOrCreateText(
        string objectName,
        Transform parent,
        float fontSize,
        FontStyles style)
    {
        RectTransform rect = GetOrCreateRect(objectName, parent);
        TextMeshProUGUI text = rect.GetComponent<TextMeshProUGUI>();
        bool created = text == null;

        if (created)
            text = rect.gameObject.AddComponent<TextMeshProUGUI>();

        if (created || text.fontSize <= 0f)
            text.fontSize = fontSize;
        if (created || text.fontStyle == FontStyles.Normal)
            text.fontStyle = style;

        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.raycastTarget = false;
        return text;
    }

    private static void SetRaycastDisabled(Graphic graphic)
    {
        if (graphic != null)
            graphic.raycastTarget = false;
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
        rect.localEulerAngles = Vector3.zero;
        rect.localScale = Vector3.one;
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

    private void ClampInspectorValues()
    {
        worldScale = ClampScale(worldScale);
        visibleOpacity = Mathf.Clamp01(visibleOpacity);
        circleDiameter = Mathf.Max(48f, circleDiameter);
        progressThickness = Mathf.Clamp(progressThickness, 1f, circleDiameter * 0.25f);
        innerAccentThickness = Mathf.Clamp(innerAccentThickness, 1f, circleDiameter * 0.2f);
        orbitMarkerSize = Mathf.Clamp(orbitMarkerSize, 4f, circleDiameter * 0.25f);
        instructionFontSize = Mathf.Max(1f, instructionFontSize);
        centerFontSize = Mathf.Max(1f, centerFontSize);
        rotationDegreesPerSecond = Mathf.Max(0f, rotationDegreesPerSecond);
        floatingAmplitude = Mathf.Max(0f, floatingAmplitude);
        floatingSpeed = Mathf.Max(0f, floatingSpeed);
        pulseSpeed = Mathf.Max(0f, pulseSpeed);
        glowPulseSpeed = Mathf.Max(0f, glowPulseSpeed);
        minimumPlacePromptScale = ClampScale(minimumPlacePromptScale);
    }

    private static float ClampScale(float value)
    {
        return Mathf.Clamp(value, 0.0001f, 0.05f);
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
            discSprite.name = "NewspaperPrompt_DiscSprite";
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
            ringSprite.name = "NewspaperPrompt_RingSprite";
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
            segmentedRingSprite.name = "NewspaperPrompt_SegmentedRingSprite";
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
                        innerRadius - 0.015f,
                        innerRadius + 0.025f,
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

                float outerAlpha = 1f - Mathf.SmoothStep(
                    outerRadius - 0.025f,
                    outerRadius + 0.01f,
                    distance
                );
                float innerAlpha = Mathf.SmoothStep(
                    innerRadius - 0.015f,
                    innerRadius + 0.025f,
                    distance
                );
                float alpha01 = segmentVisible
                    ? Mathf.Clamp01(outerAlpha * innerAlpha)
                    : 0f;
                byte alpha = (byte)Mathf.RoundToInt(alpha01 * 255f);
                pixels[y * size + x] = new Color32(255, 255, 255, alpha);
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply(false, true);
        return texture;
    }
}
