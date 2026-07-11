using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>
/// Minimapa autônomo para Desktop e Mobile.
/// Cria câmera ortográfica com RenderTexture, UI circular, ponto do jogador e zoom.
/// Não depende dos scripts antigos removidos durante a organização do projeto.
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(24000)]
public sealed class RuntimeMiniMap : MonoBehaviour
{
    public static RuntimeMiniMap Instance { get; private set; }

    [Header("Alvo")]
    public Transform target;
    [Min(0.25f)] public float targetSearchInterval = 1f;

    [Header("Visual")]
    [Min(96f)] public float desktopSize = 220f;
    [Min(96f)] public float mobileSize = 170f;
    public Vector2 margin = new Vector2(22f, 22f);
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

    [Header("Input")]
    public KeyCode toggleKey = KeyCode.M;
    public bool startOpen = true;

    private Camera mapCamera;
    private RenderTexture renderTexture;
    private Canvas canvas;
    private RectTransform rootRect;
    private RawImage mapImage;
    private Image playerDot;
    private Button zoomInButton;
    private Button zoomOutButton;
    private Texture2D circleTexture;
    private Sprite circleSprite;
    private float nextTargetSearch;
    private bool isOpen;

    public bool IsOpen => isOpen;
    public Camera MapCamera => mapCamera;

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
        DontDestroyOnLoad(gameObject);
        SceneManager.sceneLoaded += HandleSceneLoaded;

        isOpen = startOpen;
        DisableLegacyMiniMaps();
        BuildCamera();
        BuildUI();
        ResolveTarget(true);
        SetOpen(isOpen);
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;

        if (renderTexture != null)
        {
            renderTexture.Release();
            Destroy(renderTexture);
        }

        if (circleSprite != null)
            Destroy(circleSprite);
        if (circleTexture != null)
            Destroy(circleTexture);

        if (Instance == this)
            Instance = null;
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        nextTargetSearch = 0f;
        ResolveTarget(true);
        DisableLegacyMiniMaps();
        SetOpen(isOpen);
    }

    private void Update()
    {
        if (Input.GetKeyDown(toggleKey))
            Toggle();

        if (target == null && Time.unscaledTime >= nextTargetSearch)
            ResolveTarget(false);
    }

    private void LateUpdate()
    {
        if (mapCamera == null)
            BuildCamera();
        if (rootRect == null)
            BuildUI();

        if (target == null)
            return;

        Vector3 position = target.position + Vector3.up * cameraHeight;
        mapCamera.transform.position = position;

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

    private void BuildCamera()
    {
        if (mapCamera != null)
            return;

        int resolution = Application.isMobilePlatform ? 256 : 512;
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
        canvas.sortingOrder = 80;

        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        canvasObject.AddComponent<GraphicRaycaster>();

        float size = Application.isMobilePlatform ? mobileSize : desktopSize;

        GameObject root = new GameObject("MiniMap", typeof(RectTransform));
        root.transform.SetParent(canvasObject.transform, false);
        rootRect = root.GetComponent<RectTransform>();
        rootRect.anchorMin = new Vector2(0f, 1f);
        rootRect.anchorMax = new Vector2(0f, 1f);
        rootRect.pivot = new Vector2(0f, 1f);
        rootRect.anchoredPosition = new Vector2(margin.x, -margin.y);
        rootRect.sizeDelta = new Vector2(size + 46f, size);

        GameObject borderObject = CreateImage("Border", rootRect, circleSprite, borderColor);
        RectTransform borderRect = borderObject.GetComponent<RectTransform>();
        borderRect.anchorMin = new Vector2(0f, 0f);
        borderRect.anchorMax = new Vector2(0f, 1f);
        borderRect.pivot = new Vector2(0f, 0.5f);
        borderRect.sizeDelta = new Vector2(size, 0f);
        borderRect.anchoredPosition = Vector2.zero;
        borderObject.GetComponent<Image>().raycastTarget = false;

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

        zoomInButton = CreateButton("ZoomIn", rootRect, "+", new Vector2(size + 8f, -size * 0.3f));
        zoomOutButton = CreateButton("ZoomOut", rootRect, "−", new Vector2(size + 8f, -size * 0.62f));
        zoomInButton.onClick.AddListener(ZoomIn);
        zoomOutButton.onClick.AddListener(ZoomOut);

        SetOpen(isOpen);
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

    private Button CreateButton(string name, Transform parent, string label, Vector2 position)
    {
        GameObject buttonObject = CreateImage(name, parent, circleSprite, buttonColor);
        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = position;
        rect.sizeDelta = new Vector2(38f, 38f);

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

    private void OnValidate()
    {
        desktopSize = Mathf.Max(96f, desktopSize);
        mobileSize = Mathf.Max(96f, mobileSize);
        cameraHeight = Mathf.Max(10f, cameraHeight);
        minimumZoom = Mathf.Max(2f, minimumZoom);
        maximumZoom = Mathf.Max(minimumZoom + 1f, maximumZoom);
        zoom = Mathf.Clamp(zoom, minimumZoom, maximumZoom);
        zoomStep = Mathf.Max(0.5f, zoomStep);
    }
}
