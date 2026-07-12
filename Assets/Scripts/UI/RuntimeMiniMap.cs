using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>
/// Minimapa autônomo para Desktop e Mobile.
/// Pode existir como componente salvo na cena para edição completa pelo Inspector.
/// Se não existir, uma instância runtime é criada automaticamente.
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(24000)]
public sealed class RuntimeMiniMap : MonoBehaviour
{
    public enum MiniMapAnchor
    {
        TopLeft,
        TopRight,
        BottomLeft,
        BottomRight
    }

    public static RuntimeMiniMap Instance { get; private set; }

    [Header("Alvo")]
    public Transform target;
    [Min(0.25f)] public float targetSearchInterval = 1f;

    [Header("Layout editável")]
    public MiniMapAnchor anchor = MiniMapAnchor.TopLeft;
    [Min(96f)] public float desktopSize = 220f;
    [Min(96f)] public float mobileSize = 170f;
    public Vector2 margin = new Vector2(22f, 22f);
    [Min(24f)] public float zoomButtonSize = 38f;
    [Min(0f)] public float zoomButtonGap = 8f;
    public bool showZoomButtons = true;
    public bool showPlayerDot = true;
    public int canvasSortingOrder = 80;

    [Header("Cores")]
    public Color borderColor = new Color(0f, 0f, 0f, 0.72f);
    public Color playerColor = new Color(1f, 0.16f, 0.08f, 1f);
    public Color buttonColor = new Color(0.05f, 0.07f, 0.09f, 0.9f);
    public Color cameraBackground = new Color(0.035f, 0.045f, 0.055f, 1f);

    [Header("Câmera")]
    [Min(10f)] public float cameraHeight = 70f;
    [Min(2f)] public float zoom = 22f;
    [Min(2f)] public float minimumZoom = 8f;
    [Min(3f)] public float maximumZoom = 60f;
    [Min(0.5f)] public float zoomStep = 4f;
    public bool rotateWithPlayer;
    public LayerMask visibleLayers = ~0;
    [Range(128, 1024)] public int desktopTextureResolution = 512;
    [Range(128, 512)] public int mobileTextureResolution = 256;

    [Header("Input")]
    public KeyCode toggleKey = KeyCode.M;
    public bool startOpen = true;

    [Header("Referências runtime (somente leitura prática)")]
    [SerializeField] private Camera mapCamera;
    [SerializeField] private Canvas canvas;
    [SerializeField] private RectTransform rootRect;
    [SerializeField] private RawImage mapImage;
    [SerializeField] private Image playerDot;
    [SerializeField] private Button zoomInButton;
    [SerializeField] private Button zoomOutButton;

    private RenderTexture renderTexture;
    private Image borderImage;
    private RectTransform borderRect;
    private Texture2D circleTexture;
    private Sprite circleSprite;
    private float nextTargetSearch;
    private bool isOpen;

    public bool IsOpen => isOpen;
    public Camera MapCamera => mapCamera;
    public RenderTexture MapTexture => renderTexture;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Instance = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void CreateAfterSceneLoad()
    {
        RuntimeMiniMap existing = Object.FindAnyObjectByType<RuntimeMiniMap>(FindObjectsInactive.Include);
        if (existing != null)
        {
            Instance = existing;
            existing.ResolveTarget(true);
            existing.EnsureBuilt();
            existing.SetOpen(existing.isOpen);
            return;
        }

        GameObject host = new GameObject("RuntimeMiniMap");
        DontDestroyOnLoad(host);
        host.AddComponent<RuntimeMiniMap>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (transform.parent == null)
            DontDestroyOnLoad(gameObject);

        SceneManager.sceneLoaded += HandleSceneLoaded;

        isOpen = startOpen;
        DisableLegacyMiniMaps();
        EnsureBuilt();
        ResolveTarget(true);
        SetOpen(isOpen);
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        ReleaseGeneratedResources();

        if (Instance == this)
            Instance = null;
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        nextTargetSearch = 0f;
        ResolveTarget(true);
        DisableLegacyMiniMaps();
        EnsureBuilt();
        ApplyInspectorSettings();
        SetOpen(isOpen);
    }

    private void Update()
    {
        if (toggleKey != KeyCode.None && Input.GetKeyDown(toggleKey))
            Toggle();

        if (target == null && Time.unscaledTime >= nextTargetSearch)
            ResolveTarget(false);
    }

    private void LateUpdate()
    {
        EnsureBuilt();

        if (target == null || mapCamera == null)
            return;

        mapCamera.transform.position = target.position + Vector3.up * cameraHeight;
        float yaw = rotateWithPlayer ? target.eulerAngles.y : 0f;
        mapCamera.transform.rotation = Quaternion.Euler(90f, yaw, 0f);
        mapCamera.orthographicSize = Mathf.Clamp(zoom, minimumZoom, maximumZoom);
        mapCamera.cullingMask = visibleLayers;
        mapCamera.enabled = isOpen;

        if (rootRect != null && rootRect.gameObject.activeSelf != isOpen)
            rootRect.gameObject.SetActive(isOpen);
    }

    public void Toggle()
    {
        SetOpen(!isOpen);
    }

    public void SetOpen(bool open)
    {
        isOpen = open;

        if (rootRect != null)
            rootRect.gameObject.SetActive(open);
        if (mapCamera != null)
            mapCamera.enabled = open;
    }

    public void ZoomIn()
    {
        zoom = Mathf.Clamp(zoom - zoomStep, minimumZoom, maximumZoom);
    }

    public void ZoomOut()
    {
        zoom = Mathf.Clamp(zoom + zoomStep, minimumZoom, maximumZoom);
    }

    [ContextMenu("MiniMap/Aplicar configurações do Inspector")]
    public void ApplyInspectorSettings()
    {
        ValidateSettings();

        if (mapCamera != null)
        {
            mapCamera.backgroundColor = cameraBackground;
            mapCamera.cullingMask = visibleLayers;
            mapCamera.orthographicSize = zoom;
        }

        if (canvas != null)
            canvas.sortingOrder = canvasSortingOrder;

        ApplyLayout();

        if (borderImage != null)
            borderImage.color = borderColor;
        if (playerDot != null)
        {
            playerDot.color = playerColor;
            playerDot.gameObject.SetActive(showPlayerDot);
        }
        if (zoomInButton != null)
        {
            zoomInButton.gameObject.SetActive(showZoomButtons);
            Image image = zoomInButton.targetGraphic as Image;
            if (image != null) image.color = buttonColor;
        }
        if (zoomOutButton != null)
        {
            zoomOutButton.gameObject.SetActive(showZoomButtons);
            Image image = zoomOutButton.targetGraphic as Image;
            if (image != null) image.color = buttonColor;
        }
    }

    [ContextMenu("MiniMap/Recriar câmera e visual runtime")]
    public void RebuildRuntimeVisuals()
    {
        if (!Application.isPlaying)
        {
            Debug.Log("[RuntimeMiniMap] As configurações estão salvas. A recriação visual acontece ao entrar no Play Mode.", this);
            return;
        }

        DestroyRuntimeChildren();
        ReleaseGeneratedResources();
        EnsureBuilt();
        SetOpen(isOpen);
    }

    private void ResolveTarget(bool force)
    {
        if (!force && target != null)
            return;

        nextTargetSearch = Time.unscaledTime + Mathf.Max(0.25f, targetSearchInterval);

        CameraRelativeMovement movement = Object.FindAnyObjectByType<CameraRelativeMovement>(FindObjectsInactive.Include);
        if (movement != null)
        {
            target = movement.transform;
            return;
        }

        try
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
                target = player.transform;
        }
        catch
        {
            // Tag inexistente não impede o minimapa de continuar buscando.
        }
    }

    private void EnsureBuilt()
    {
        if (mapCamera == null)
            BuildCamera();
        if (rootRect == null)
            BuildUI();

        ApplyInspectorSettings();
    }

    private void BuildCamera()
    {
        if (mapCamera != null)
            return;

        int configuredResolution = Application.isMobilePlatform
            ? mobileTextureResolution
            : desktopTextureResolution;
        int resolution = Mathf.Clamp(configuredResolution, 128, 1024);

        renderTexture = new RenderTexture(resolution, resolution, 16, RenderTextureFormat.ARGB32)
        {
            name = "RuntimeMiniMapTexture",
            useMipMap = false,
            autoGenerateMips = false
        };
        renderTexture.Create();

        GameObject cameraObject = new GameObject("RuntimeMiniMapCamera");
        cameraObject.transform.SetParent(transform, false);
        cameraObject.tag = "Untagged";

        mapCamera = cameraObject.AddComponent<Camera>();
        mapCamera.orthographic = true;
        mapCamera.orthographicSize = zoom;
        mapCamera.clearFlags = CameraClearFlags.SolidColor;
        mapCamera.backgroundColor = cameraBackground;
        mapCamera.cullingMask = visibleLayers;
        mapCamera.depth = -100f;
        mapCamera.allowHDR = false;
        mapCamera.allowMSAA = false;
        mapCamera.useOcclusionCulling = false;
        mapCamera.targetTexture = renderTexture;
        mapCamera.enabled = isOpen;
    }

    private void BuildUI()
    {
        if (rootRect != null)
            return;

        EnsureEventSystem();
        EnsureCircleSprite();

        GameObject canvasObject = new GameObject("RuntimeMiniMapCanvas", typeof(RectTransform));
        canvasObject.transform.SetParent(transform, false);
        canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = canvasSortingOrder;

        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        canvasObject.AddComponent<GraphicRaycaster>();

        GameObject root = new GameObject("MiniMap", typeof(RectTransform));
        root.transform.SetParent(canvasObject.transform, false);
        rootRect = root.GetComponent<RectTransform>();

        GameObject borderObject = CreateImage("Border", rootRect, circleSprite, borderColor);
        borderRect = borderObject.GetComponent<RectTransform>();
        borderImage = borderObject.GetComponent<Image>();
        borderImage.raycastTarget = false;

        GameObject maskObject = CreateImage("CircularMask", borderRect, circleSprite, Color.white);
        RectTransform maskRect = maskObject.GetComponent<RectTransform>();
        maskRect.anchorMin = Vector2.zero;
        maskRect.anchorMax = Vector2.one;
        maskRect.offsetMin = new Vector2(5f, 5f);
        maskRect.offsetMax = new Vector2(-5f, -5f);
        Mask mask = maskObject.AddComponent<Mask>();
        mask.showMaskGraphic = false;

        GameObject mapObject = new GameObject("MapImage", typeof(RectTransform), typeof(RawImage));
        mapObject.transform.SetParent(maskRect, false);
        RectTransform mapRect = mapObject.GetComponent<RectTransform>();
        mapRect.anchorMin = Vector2.zero;
        mapRect.anchorMax = Vector2.one;
        mapRect.offsetMin = Vector2.zero;
        mapRect.offsetMax = Vector2.zero;
        mapImage = mapObject.GetComponent<RawImage>();
        mapImage.texture = renderTexture;
        mapImage.raycastTarget = false;

        GameObject dotObject = CreateImage("PlayerDot", maskRect, circleSprite, playerColor);
        RectTransform dotRect = dotObject.GetComponent<RectTransform>();
        dotRect.anchorMin = new Vector2(0.5f, 0.5f);
        dotRect.anchorMax = new Vector2(0.5f, 0.5f);
        dotRect.pivot = new Vector2(0.5f, 0.5f);
        dotRect.sizeDelta = new Vector2(14f, 14f);
        dotRect.anchoredPosition = Vector2.zero;
        playerDot = dotObject.GetComponent<Image>();
        playerDot.raycastTarget = false;

        zoomInButton = CreateButton("ZoomIn", rootRect, "+");
        zoomOutButton = CreateButton("ZoomOut", rootRect, "−");
        zoomInButton.onClick.AddListener(ZoomIn);
        zoomOutButton.onClick.AddListener(ZoomOut);

        ApplyInspectorSettings();
        SetOpen(isOpen);
    }

    private void ApplyLayout()
    {
        if (rootRect == null)
            return;

        float size = Application.isMobilePlatform ? mobileSize : desktopSize;
        Vector2 anchorValue;
        Vector2 pivotValue;
        Vector2 anchoredPosition;

        switch (anchor)
        {
            case MiniMapAnchor.TopRight:
                anchorValue = pivotValue = new Vector2(1f, 1f);
                anchoredPosition = new Vector2(-margin.x, -margin.y);
                break;
            case MiniMapAnchor.BottomLeft:
                anchorValue = pivotValue = new Vector2(0f, 0f);
                anchoredPosition = new Vector2(margin.x, margin.y);
                break;
            case MiniMapAnchor.BottomRight:
                anchorValue = pivotValue = new Vector2(1f, 0f);
                anchoredPosition = new Vector2(-margin.x, margin.y);
                break;
            default:
                anchorValue = pivotValue = new Vector2(0f, 1f);
                anchoredPosition = new Vector2(margin.x, -margin.y);
                break;
        }

        rootRect.anchorMin = anchorValue;
        rootRect.anchorMax = anchorValue;
        rootRect.pivot = pivotValue;
        rootRect.anchoredPosition = anchoredPosition;
        rootRect.sizeDelta = new Vector2(size + zoomButtonSize + zoomButtonGap, size);

        if (borderRect != null)
        {
            bool rightAnchored = anchor == MiniMapAnchor.TopRight || anchor == MiniMapAnchor.BottomRight;
            borderRect.anchorMin = rightAnchored ? new Vector2(1f, 0f) : new Vector2(0f, 0f);
            borderRect.anchorMax = rightAnchored ? new Vector2(1f, 1f) : new Vector2(0f, 1f);
            borderRect.pivot = rightAnchored ? new Vector2(1f, 0.5f) : new Vector2(0f, 0.5f);
            borderRect.sizeDelta = new Vector2(size, 0f);
            borderRect.anchoredPosition = Vector2.zero;
        }

        PositionZoomButton(zoomInButton, size, 0.30f);
        PositionZoomButton(zoomOutButton, size, 0.62f);
    }

    private void PositionZoomButton(Button button, float size, float verticalRatio)
    {
        if (button == null)
            return;

        RectTransform rect = button.transform as RectTransform;
        bool rightAnchored = anchor == MiniMapAnchor.TopLeft || anchor == MiniMapAnchor.BottomLeft;
        bool topAnchored = anchor == MiniMapAnchor.TopLeft || anchor == MiniMapAnchor.TopRight;

        rect.anchorMin = new Vector2(rightAnchored ? 0f : 1f, topAnchored ? 1f : 0f);
        rect.anchorMax = rect.anchorMin;
        rect.pivot = new Vector2(rightAnchored ? 0f : 1f, topAnchored ? 1f : 0f);

        float x = rightAnchored ? size + zoomButtonGap : -(size + zoomButtonGap);
        float y = topAnchored ? -(size * verticalRatio) : size * verticalRatio;
        rect.anchoredPosition = new Vector2(x, y);
        rect.sizeDelta = new Vector2(zoomButtonSize, zoomButtonSize);
    }

    private GameObject CreateImage(string name, Transform parent, Sprite sprite, Color color)
    {
        GameObject targetObject = new GameObject(name, typeof(RectTransform), typeof(Image));
        targetObject.transform.SetParent(parent, false);
        Image image = targetObject.GetComponent<Image>();
        image.sprite = sprite;
        image.color = color;
        return targetObject;
    }

    private Button CreateButton(string name, Transform parent, string label)
    {
        GameObject buttonObject = CreateImage(name, parent, circleSprite, buttonColor);
        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(zoomButtonSize, zoomButtonSize);

        Button button = buttonObject.AddComponent<Button>();
        button.targetGraphic = buttonObject.GetComponent<Image>();

        GameObject textObject = new GameObject("Label", typeof(RectTransform), typeof(Text));
        textObject.transform.SetParent(buttonObject.transform, false);
        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        Text text = textObject.GetComponent<Text>();
        text.text = label;
        text.alignment = TextAnchor.MiddleCenter;
        text.fontSize = 24;
        text.color = Color.white;
        text.raycastTarget = false;
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        return button;
    }

    private void EnsureCircleSprite()
    {
        if (circleSprite != null)
            return;

        const int resolution = 128;
        circleTexture = new Texture2D(resolution, resolution, TextureFormat.RGBA32, false)
        {
            name = "RuntimeMiniMapCircle",
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };

        Color[] pixels = new Color[resolution * resolution];
        Vector2 center = new Vector2((resolution - 1) * 0.5f, (resolution - 1) * 0.5f);
        float radius = resolution * 0.5f - 1f;

        for (int y = 0; y < resolution; y++)
        {
            for (int x = 0; x < resolution; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), center);
                float alpha = Mathf.Clamp01(radius - distance + 1f);
                pixels[y * resolution + x] = new Color(1f, 1f, 1f, alpha);
            }
        }

        circleTexture.SetPixels(pixels);
        circleTexture.Apply(false, false);
        circleSprite = Sprite.Create(
            circleTexture,
            new Rect(0f, 0f, resolution, resolution),
            new Vector2(0.5f, 0.5f),
            100f
        );
    }

    private void EnsureEventSystem()
    {
        if (Object.FindAnyObjectByType<EventSystem>(FindObjectsInactive.Include) != null)
            return;

        new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
    }

    private void DisableLegacyMiniMaps()
    {
        MonoBehaviour[] behaviours = Object.FindObjectsByType<MonoBehaviour>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None
        );

        for (int i = 0; i < behaviours.Length; i++)
        {
            MonoBehaviour behaviour = behaviours[i];
            if (behaviour == null || behaviour == this)
                continue;

            string typeName = behaviour.GetType().Name;
            if (typeName == "MiniMarketMiniMapController" || typeName == "MiniMapController")
                behaviour.enabled = false;
        }
    }

    private void DestroyRuntimeChildren()
    {
        if (mapCamera != null)
            Destroy(mapCamera.gameObject);
        if (canvas != null)
            Destroy(canvas.gameObject);

        mapCamera = null;
        canvas = null;
        rootRect = null;
        mapImage = null;
        playerDot = null;
        zoomInButton = null;
        zoomOutButton = null;
        borderImage = null;
        borderRect = null;
    }

    private void ReleaseGeneratedResources()
    {
        if (renderTexture != null)
        {
            renderTexture.Release();
            Destroy(renderTexture);
            renderTexture = null;
        }

        if (circleSprite != null)
        {
            Destroy(circleSprite);
            circleSprite = null;
        }

        if (circleTexture != null)
        {
            Destroy(circleTexture);
            circleTexture = null;
        }
    }

    private void ValidateSettings()
    {
        desktopSize = Mathf.Max(96f, desktopSize);
        mobileSize = Mathf.Max(96f, mobileSize);
        zoomButtonSize = Mathf.Max(24f, zoomButtonSize);
        zoomButtonGap = Mathf.Max(0f, zoomButtonGap);
        cameraHeight = Mathf.Max(10f, cameraHeight);
        minimumZoom = Mathf.Max(2f, minimumZoom);
        maximumZoom = Mathf.Max(minimumZoom + 1f, maximumZoom);
        zoom = Mathf.Clamp(zoom, minimumZoom, maximumZoom);
        zoomStep = Mathf.Max(0.5f, zoomStep);
        desktopTextureResolution = Mathf.Clamp(desktopTextureResolution, 128, 1024);
        mobileTextureResolution = Mathf.Clamp(mobileTextureResolution, 128, 512);
    }

    private void OnValidate()
    {
        ValidateSettings();

        if (Application.isPlaying)
            ApplyInspectorSettings();
    }
}
