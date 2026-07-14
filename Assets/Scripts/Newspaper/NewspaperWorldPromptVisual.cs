using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Prompt world-space reutilizável para as interações do jornal.
/// Cria círculo, anel giratório, preenchimento radial e textos sem assets externos.
/// </summary>
[DisallowMultipleComponent]
public sealed class NewspaperWorldPromptVisual : MonoBehaviour
{
    [Header("Runtime")]
    public Canvas worldCanvas;
    public CanvasGroup canvasGroup;
    public RectTransform rotatingRing;
    public Image rotatingRingImage;
    public Image orbitMarker;
    public Image progressImage;
    public Image centerDisc;
    public TextMeshProUGUI centerText;
    public TextMeshProUGUI instructionText;

    [Header("Comportamento")]
    public bool faceCamera = true;
    [Min(0f)] public float rotationDegreesPerSecond = 32f;

    private Camera cachedCamera;

    private static Sprite discSprite;
    private static Sprite ringSprite;
    private static Texture2D discTexture;
    private static Texture2D ringTexture;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        discSprite = null;
        ringSprite = null;
        discTexture = null;
        ringTexture = null;
    }

    public static NewspaperWorldPromptVisual Create(
        Transform parent,
        string objectName,
        Vector3 localOffset,
        float worldScale,
        int sortingOrder = 260)
    {
        GameObject root = new GameObject(objectName, typeof(RectTransform));
        root.transform.SetParent(parent, false);
        root.transform.localPosition = localOffset;
        root.transform.localRotation = Quaternion.identity;
        root.transform.localScale = Vector3.one * Mathf.Max(0.0005f, worldScale);

        RectTransform rootRect = root.GetComponent<RectTransform>();
        rootRect.sizeDelta = new Vector2(220f, 230f);

        NewspaperWorldPromptVisual visual = root.AddComponent<NewspaperWorldPromptVisual>();
        visual.Build(sortingOrder);
        visual.SetVisible(false);
        return visual;
    }

    private void Build(int sortingOrder)
    {
        EnsureSprites();

        worldCanvas = gameObject.AddComponent<Canvas>();
        worldCanvas.renderMode = RenderMode.WorldSpace;
        worldCanvas.overrideSorting = true;
        worldCanvas.sortingOrder = sortingOrder;

        CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.dynamicPixelsPerUnit = 12f;
        scaler.referencePixelsPerUnit = 100f;

        gameObject.AddComponent<GraphicRaycaster>();
        canvasGroup = gameObject.AddComponent<CanvasGroup>();
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;

        RectTransform root = transform as RectTransform;

        instructionText = CreateText("Instruction", root, 25f, FontStyles.Bold);
        RectTransform instructionRect = instructionText.rectTransform;
        instructionRect.anchorMin = new Vector2(0.5f, 0.5f);
        instructionRect.anchorMax = new Vector2(0.5f, 0.5f);
        instructionRect.pivot = new Vector2(0.5f, 0.5f);
        instructionRect.anchoredPosition = new Vector2(0f, 82f);
        instructionRect.sizeDelta = new Vector2(420f, 58f);
        instructionText.alignment = TextAlignmentOptions.Center;
        instructionText.color = new Color32(246, 249, 255, 255);
        instructionText.outlineColor = new Color32(5, 8, 14, 235);
        instructionText.outlineWidth = 0.22f;

        GameObject circleRootObject = CreateUiObject("CircleRoot", root);
        RectTransform circleRoot = circleRootObject.GetComponent<RectTransform>();
        circleRoot.anchorMin = new Vector2(0.5f, 0.5f);
        circleRoot.anchorMax = new Vector2(0.5f, 0.5f);
        circleRoot.pivot = new Vector2(0.5f, 0.5f);
        circleRoot.anchoredPosition = new Vector2(0f, -5f);
        circleRoot.sizeDelta = new Vector2(126f, 126f);

        GameObject discObject = CreateUiObject("CenterDisc", circleRoot);
        RectTransform discRect = discObject.GetComponent<RectTransform>();
        Stretch(discRect, new Vector2(13f, 13f), new Vector2(-13f, -13f));
        centerDisc = discObject.AddComponent<Image>();
        centerDisc.sprite = discSprite;
        centerDisc.color = new Color32(8, 12, 22, 225);
        centerDisc.raycastTarget = false;

        GameObject rotatingObject = CreateUiObject("RotatingRing", circleRoot);
        rotatingRing = rotatingObject.GetComponent<RectTransform>();
        Stretch(rotatingRing, Vector2.zero, Vector2.zero);
        rotatingRingImage = rotatingObject.AddComponent<Image>();
        rotatingRingImage.sprite = ringSprite;
        rotatingRingImage.color = new Color32(255, 255, 255, 245);
        rotatingRingImage.raycastTarget = false;

        GameObject markerObject = CreateUiObject("OrbitMarker", rotatingRing);
        RectTransform markerRect = markerObject.GetComponent<RectTransform>();
        markerRect.anchorMin = new Vector2(0.5f, 1f);
        markerRect.anchorMax = new Vector2(0.5f, 1f);
        markerRect.pivot = new Vector2(0.5f, 0.5f);
        markerRect.anchoredPosition = new Vector2(0f, -3f);
        markerRect.sizeDelta = new Vector2(15f, 15f);
        orbitMarker = markerObject.AddComponent<Image>();
        orbitMarker.sprite = discSprite;
        orbitMarker.color = new Color32(255, 255, 255, 255);
        orbitMarker.raycastTarget = false;

        GameObject progressObject = CreateUiObject("ProgressRing", circleRoot);
        RectTransform progressRect = progressObject.GetComponent<RectTransform>();
        Stretch(progressRect, new Vector2(6f, 6f), new Vector2(-6f, -6f));
        progressImage = progressObject.AddComponent<Image>();
        progressImage.sprite = ringSprite;
        progressImage.type = Image.Type.Filled;
        progressImage.fillMethod = Image.FillMethod.Radial360;
        progressImage.fillOrigin = 2;
        progressImage.fillClockwise = true;
        progressImage.fillAmount = 0f;
        progressImage.color = new Color32(68, 230, 125, 255);
        progressImage.raycastTarget = false;

        centerText = CreateText("CenterText", circleRoot, 43f, FontStyles.Bold);
        Stretch(centerText.rectTransform, new Vector2(15f, 15f), new Vector2(-15f, -15f));
        centerText.alignment = TextAlignmentOptions.Center;
        centerText.color = Color.white;
        centerText.outlineColor = new Color32(0, 0, 0, 230);
        centerText.outlineWidth = 0.2f;
        centerText.text = "E";
    }

    private void LateUpdate()
    {
        if (canvasGroup == null || canvasGroup.alpha <= 0.001f)
            return;

        if (rotatingRing != null && rotationDegreesPerSecond != 0f)
        {
            rotatingRing.Rotate(
                0f,
                0f,
                -rotationDegreesPerSecond * Time.unscaledDeltaTime,
                Space.Self
            );
        }

        if (!faceCamera)
            return;

        if (cachedCamera == null || !cachedCamera.isActiveAndEnabled)
            cachedCamera = Camera.main;

        if (cachedCamera == null)
            return;

        Vector3 direction = transform.position - cachedCamera.transform.position;
        if (direction.sqrMagnitude > 0.0001f)
        {
            transform.rotation = Quaternion.LookRotation(
                direction.normalized,
                cachedCamera.transform.up
            );
        }
    }

    public void SetVisible(bool visible)
    {
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
        if (centerText != null)
            centerText.text = string.IsNullOrEmpty(center) ? "E" : center;

        if (instructionText != null)
            instructionText.text = instruction ?? string.Empty;

        if (rotatingRingImage != null)
            rotatingRingImage.color = accentColor;

        if (orbitMarker != null)
            orbitMarker.color = accentColor;

        if (progressImage != null)
        {
            progressImage.enabled = showProgress;
            progressImage.fillAmount = Mathf.Clamp01(progress01);
            progressImage.color = accentColor;
        }
    }

    public void SetLocalOffset(Vector3 offset)
    {
        transform.localPosition = offset;
    }

    public void SetWorldScale(float scale)
    {
        transform.localScale = Vector3.one * Mathf.Max(0.0005f, scale);
    }

    private static GameObject CreateUiObject(string name, Transform parent)
    {
        GameObject value = new GameObject(name, typeof(RectTransform));
        value.transform.SetParent(parent, false);
        return value;
    }

    private static TextMeshProUGUI CreateText(
        string name,
        Transform parent,
        float fontSize,
        FontStyles style)
    {
        GameObject value = CreateUiObject(name, parent);
        TextMeshProUGUI text = value.AddComponent<TextMeshProUGUI>();
        text.fontSize = fontSize;
        text.fontStyle = style;
        text.enableWordWrapping = false;
        text.raycastTarget = false;
        return text;
    }

    private static void Stretch(RectTransform rect, Vector2 minOffset, Vector2 maxOffset)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = minOffset;
        rect.offsetMax = maxOffset;
    }

    private static void EnsureSprites()
    {
        if (discSprite == null)
        {
            discTexture = CreateCircleTexture(128, 0f, 0.96f);
            discTexture.name = "NewspaperPrompt_Disc_Runtime";
            discTexture.hideFlags = HideFlags.HideAndDontSave;
            discSprite = Sprite.Create(
                discTexture,
                new Rect(0f, 0f, discTexture.width, discTexture.height),
                new Vector2(0.5f, 0.5f),
                100f
            );
            discSprite.name = "NewspaperPrompt_Disc_Runtime";
            discSprite.hideFlags = HideFlags.HideAndDontSave;
        }

        if (ringSprite == null)
        {
            ringTexture = CreateCircleTexture(128, 0.77f, 0.97f);
            ringTexture.name = "NewspaperPrompt_Ring_Runtime";
            ringTexture.hideFlags = HideFlags.HideAndDontSave;
            ringSprite = Sprite.Create(
                ringTexture,
                new Rect(0f, 0f, ringTexture.width, ringTexture.height),
                new Vector2(0.5f, 0.5f),
                100f
            );
            ringSprite.name = "NewspaperPrompt_Ring_Runtime";
            ringSprite.hideFlags = HideFlags.HideAndDontSave;
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
}
