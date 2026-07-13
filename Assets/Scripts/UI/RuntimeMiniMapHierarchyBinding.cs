using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Liga a hierarquia persistente/editável do minimapa ao RuntimeMiniMap.
/// A câmera, Canvas e imagens permanecem salvos na cena; apenas a RenderTexture
/// continua sendo criada em runtime, pois é um recurso temporário de GPU.
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
[DefaultExecutionOrder(-50000)]
public sealed class RuntimeMiniMapHierarchyBinding : MonoBehaviour
{
    [Header("Controlador")]
    public RuntimeMiniMap miniMap;

    [Header("Hierarquia persistente")]
    public Camera mapCamera;
    public Canvas canvas;
    public RectTransform rootRect;
    public RawImage mapImage;
    public Image borderImage;
    public RectTransform borderRect;
    public Image playerDot;
    public Button zoomInButton;
    public Button zoomOutButton;

    [Header("Preview no Editor")]
    public bool aplicarPreviewNoEditor = true;

    private RenderTexture ownedTexture;

    private static readonly BindingFlags PrivateInstance =
        BindingFlags.Instance | BindingFlags.NonPublic;

    private static FieldInfo mapCameraField;
    private static FieldInfo canvasField;
    private static FieldInfo rootRectField;
    private static FieldInfo mapImageField;
    private static FieldInfo playerDotField;
    private static FieldInfo zoomInButtonField;
    private static FieldInfo zoomOutButtonField;
    private static FieldInfo renderTextureField;
    private static FieldInfo borderImageField;
    private static FieldInfo borderRectField;

    private void Awake()
    {
        ResolverControlador();
        InjetarReferencias(Application.isPlaying);
    }

    private void OnEnable()
    {
        ResolverControlador();
        InjetarReferencias(Application.isPlaying);
    }

    private void Start()
    {
        if (!Application.isPlaying)
            return;

        InjetarReferencias(true);
        VincularBotoes();
    }

    private void OnDisable()
    {
        DesvincularBotoes();
    }

    private void OnDestroy()
    {
        DesvincularBotoes();
        LiberarTextura();
    }

    [ContextMenu("MiniMap/Aplicar hierarquia persistente")]
    public void AplicarHierarquiaPersistente()
    {
        ResolverControlador();
        InjetarReferencias(Application.isPlaying);

        if (miniMap != null)
            miniMap.ApplyInspectorSettings();
    }

    private void ResolverControlador()
    {
        if (miniMap == null)
            miniMap = GetComponent<RuntimeMiniMap>();

        if (miniMap == null)
            miniMap = GetComponentInParent<RuntimeMiniMap>();
    }

    private void InjetarReferencias(bool criarTexturaRuntime)
    {
        if (miniMap == null)
            return;

        GarantirReflection();

        Definir(mapCameraField, miniMap, mapCamera);
        Definir(canvasField, miniMap, canvas);
        Definir(rootRectField, miniMap, rootRect);
        Definir(mapImageField, miniMap, mapImage);
        Definir(playerDotField, miniMap, playerDot);
        Definir(zoomInButtonField, miniMap, zoomInButton);
        Definir(zoomOutButtonField, miniMap, zoomOutButton);
        Definir(borderImageField, miniMap, borderImage);
        Definir(borderRectField, miniMap, borderRect);

        if (mapCamera != null)
        {
            mapCamera.orthographic = true;
            mapCamera.clearFlags = CameraClearFlags.SolidColor;
            mapCamera.backgroundColor = miniMap.cameraBackground;
            mapCamera.cullingMask = miniMap.visibleLayers;
            mapCamera.orthographicSize = miniMap.zoom;
            mapCamera.depth = -100f;
            mapCamera.allowHDR = false;
            mapCamera.allowMSAA = false;
            mapCamera.useOcclusionCulling = false;

            AudioListener listener = mapCamera.GetComponent<AudioListener>();
            if (listener != null)
                listener.enabled = false;
        }

        if (canvas != null)
            canvas.sortingOrder = miniMap.canvasSortingOrder;

        if (criarTexturaRuntime)
            GarantirTexturaRuntime();
        else if (mapCamera != null)
            mapCamera.enabled = false;

        if (!Application.isPlaying && aplicarPreviewNoEditor)
        {
            if (borderImage != null)
                borderImage.color = miniMap.borderColor;
            if (playerDot != null)
                playerDot.color = miniMap.playerColor;
            if (zoomInButton != null && zoomInButton.targetGraphic != null)
                zoomInButton.targetGraphic.color = miniMap.buttonColor;
            if (zoomOutButton != null && zoomOutButton.targetGraphic != null)
                zoomOutButton.targetGraphic.color = miniMap.buttonColor;
        }
    }

    private void GarantirTexturaRuntime()
    {
        if (miniMap == null || mapCamera == null || mapImage == null)
            return;

        int desejada = Application.isMobilePlatform
            ? miniMap.mobileTextureResolution
            : miniMap.desktopTextureResolution;
        desejada = Mathf.Clamp(desejada, 128, 1024);

        if (ownedTexture != null &&
            (ownedTexture.width != desejada || ownedTexture.height != desejada))
        {
            LiberarTextura();
        }

        if (ownedTexture == null)
        {
            ownedTexture = new RenderTexture(
                desejada,
                desejada,
                16,
                RenderTextureFormat.ARGB32)
            {
                name = "RuntimeMiniMapTexture",
                useMipMap = false,
                autoGenerateMips = false
            };
            ownedTexture.Create();
        }

        mapCamera.targetTexture = ownedTexture;
        mapImage.texture = ownedTexture;
        Definir(renderTextureField, miniMap, ownedTexture);
    }

    private void VincularBotoes()
    {
        if (miniMap == null)
            return;

        if (zoomInButton != null)
        {
            zoomInButton.onClick.RemoveListener(miniMap.ZoomIn);
            zoomInButton.onClick.AddListener(miniMap.ZoomIn);
        }

        if (zoomOutButton != null)
        {
            zoomOutButton.onClick.RemoveListener(miniMap.ZoomOut);
            zoomOutButton.onClick.AddListener(miniMap.ZoomOut);
        }
    }

    private void DesvincularBotoes()
    {
        if (miniMap == null)
            return;

        if (zoomInButton != null)
            zoomInButton.onClick.RemoveListener(miniMap.ZoomIn);
        if (zoomOutButton != null)
            zoomOutButton.onClick.RemoveListener(miniMap.ZoomOut);
    }

    private void LiberarTextura()
    {
        if (mapCamera != null && mapCamera.targetTexture == ownedTexture)
            mapCamera.targetTexture = null;
        if (mapImage != null && mapImage.texture == ownedTexture)
            mapImage.texture = null;

        if (ownedTexture == null)
            return;

        ownedTexture.Release();

        if (Application.isPlaying)
            Destroy(ownedTexture);
        else
            DestroyImmediate(ownedTexture);

        ownedTexture = null;

        if (miniMap != null)
            Definir(renderTextureField, miniMap, null);
    }

    private static void GarantirReflection()
    {
        if (mapCameraField != null)
            return;

        System.Type type = typeof(RuntimeMiniMap);
        mapCameraField = type.GetField("mapCamera", PrivateInstance);
        canvasField = type.GetField("canvas", PrivateInstance);
        rootRectField = type.GetField("rootRect", PrivateInstance);
        mapImageField = type.GetField("mapImage", PrivateInstance);
        playerDotField = type.GetField("playerDot", PrivateInstance);
        zoomInButtonField = type.GetField("zoomInButton", PrivateInstance);
        zoomOutButtonField = type.GetField("zoomOutButton", PrivateInstance);
        renderTextureField = type.GetField("renderTexture", PrivateInstance);
        borderImageField = type.GetField("borderImage", PrivateInstance);
        borderRectField = type.GetField("borderRect", PrivateInstance);
    }

    private static void Definir(FieldInfo field, object target, object value)
    {
        if (field != null && target != null)
            field.SetValue(target, value);
    }

    private void OnValidate()
    {
        ResolverControlador();
        InjetarReferencias(false);
    }
}
