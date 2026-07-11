using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Perfil de renderização seguro para Desktop e Mobile.
///
/// Não troca materiais nem pipeline. Ajusta apenas propriedades globais e, quando URP
/// estiver ativo, usa reflexão para configurar renderScale/MSAA sem criar dependência
/// rígida de versão do pacote Universal RP.
/// </summary>
[DefaultExecutionOrder(-48000)]
[DisallowMultipleComponent]
public sealed class PlatformRenderProfile : MonoBehaviour
{
    public enum ForcedProfile
    {
        Automatic,
        Desktop,
        Mobile,
        LowEndMobile
    }

    public static PlatformRenderProfile Instance { get; private set; }

    [Header("Seleção")]
    public ForcedProfile forcedProfile = ForcedProfile.Automatic;
    public bool applyAutomatically = true;

    [Header("Desktop")]
    [Range(30, 240)] public int desktopTargetFps = 60;
    public bool respectDesktopVSync = true;

    [Header("Mobile")]
    [Range(30, 120)] public int mobileTargetFps = 60;
    [Range(0.5f, 1f)] public float mobileRenderScale = 0.85f;
    [Range(0.5f, 1f)] public float lowEndMobileRenderScale = 0.70f;
    [Range(0, 4)] public int mobilePixelLights = 2;
    [Min(5f)] public float mobileShadowDistance = 28f;
    [Min(5f)] public float lowEndShadowDistance = 16f;
    [Range(0, 4)] public int mobileMsaaSamples = 2;
    public bool disableRealtimeReflectionsOnMobile = true;
    public bool disableSoftParticlesOnMobile = true;
    public bool disableVSyncOnMobile = true;

    [Header("Detecção de aparelho fraco")]
    [Min(512)] public int lowEndRamThresholdMb = 3500;
    [Min(128)] public int lowEndVramThresholdMb = 1200;

    [Header("Comum")]
    public bool useLowBackgroundLoadingPriority = true;
    public bool enableCollisionCallbackReuse = true;
    public bool neverSleepOnMobile = true;
    public bool logAppliedProfile;

    public ForcedProfile ActiveProfile { get; private set; }
    public bool IsMobileProfile => ActiveProfile == ForcedProfile.Mobile || ActiveProfile == ForcedProfile.LowEndMobile;
    public float ActiveRenderScale { get; private set; } = 1f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Instance = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void CreateAutomatically()
    {
        if (Instance != null)
            return;

        GameObject go = new GameObject("PlatformRenderProfile");
        go.AddComponent<PlatformRenderProfile>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (transform.parent != null)
            transform.SetParent(null);

        DontDestroyOnLoad(gameObject);

        if (applyAutomatically)
            ApplyProfile();
    }

    private void OnApplicationFocus(bool focused)
    {
        if (focused && applyAutomatically)
            ApplyProfile();
    }

    [ContextMenu("Rendering/Aplicar perfil agora")]
    public void ApplyProfile()
    {
        ActiveProfile = ResolveProfile();

        if (useLowBackgroundLoadingPriority)
            Application.backgroundLoadingPriority = ThreadPriority.Low;

#if UNITY_2020_2_OR_NEWER
        if (enableCollisionCallbackReuse)
            Physics.reuseCollisionCallbacks = true;
#endif

        if (IsMobileProfile)
            ApplyMobileProfile(ActiveProfile == ForcedProfile.LowEndMobile);
        else
            ApplyDesktopProfile();

        if (logAppliedProfile)
        {
            Debug.Log(
                "[PlatformRenderProfile] Perfil=" + ActiveProfile +
                ", FPS=" + Application.targetFrameRate +
                ", RenderScale=" + ActiveRenderScale.ToString("0.00") +
                ", RAM=" + SystemInfo.systemMemorySize + "MB" +
                ", VRAM=" + SystemInfo.graphicsMemorySize + "MB",
                this
            );
        }
    }

    private ForcedProfile ResolveProfile()
    {
        if (forcedProfile != ForcedProfile.Automatic)
            return forcedProfile;

        if (!Application.isMobilePlatform)
            return ForcedProfile.Desktop;

        bool lowRam = SystemInfo.systemMemorySize > 0 &&
                      SystemInfo.systemMemorySize <= lowEndRamThresholdMb;
        bool lowVram = SystemInfo.graphicsMemorySize > 0 &&
                       SystemInfo.graphicsMemorySize <= lowEndVramThresholdMb;

        return lowRam || lowVram
            ? ForcedProfile.LowEndMobile
            : ForcedProfile.Mobile;
    }

    private void ApplyDesktopProfile()
    {
        ActiveRenderScale = 1f;
        Application.targetFrameRate = Mathf.Clamp(desktopTargetFps, 30, 240);

        if (!respectDesktopVSync)
            QualitySettings.vSyncCount = 0;

        ScalableBufferManager.ResizeBuffers(1f, 1f);
        TryConfigureRenderPipeline(1f, Mathf.Max(1, QualitySettings.antiAliasing));
    }

    private void ApplyMobileProfile(bool lowEnd)
    {
        if (disableVSyncOnMobile)
            QualitySettings.vSyncCount = 0;

        Application.targetFrameRate = Mathf.Clamp(
            lowEnd ? Mathf.Min(30, mobileTargetFps) : mobileTargetFps,
            30,
            120
        );

        ActiveRenderScale = Mathf.Clamp(
            lowEnd ? lowEndMobileRenderScale : mobileRenderScale,
            0.5f,
            1f
        );

        QualitySettings.pixelLightCount = Mathf.Clamp(mobilePixelLights, 0, 4);
        QualitySettings.shadowDistance = Mathf.Min(
            QualitySettings.shadowDistance,
            lowEnd ? lowEndShadowDistance : mobileShadowDistance
        );
        QualitySettings.antiAliasing = NormalizeMsaa(mobileMsaaSamples);
        QualitySettings.anisotropicFiltering = lowEnd
            ? AnisotropicFiltering.Disable
            : AnisotropicFiltering.Enable;
        QualitySettings.lodBias = lowEnd ? 0.65f : 0.85f;
        QualitySettings.maximumLODLevel = lowEnd ? 1 : 0;
        QualitySettings.skinWeights = lowEnd ? SkinWeights.TwoBones : SkinWeights.FourBones;

        if (disableRealtimeReflectionsOnMobile)
            QualitySettings.realtimeReflectionProbes = false;
        if (disableSoftParticlesOnMobile)
            QualitySettings.softParticles = false;

        if (neverSleepOnMobile)
            Screen.sleepTimeout = SleepTimeout.NeverSleep;

        ScalableBufferManager.ResizeBuffers(ActiveRenderScale, ActiveRenderScale);
        TryConfigureRenderPipeline(ActiveRenderScale, NormalizeMsaa(mobileMsaaSamples));
    }

    private void TryConfigureRenderPipeline(float renderScale, int msaaSamples)
    {
        RenderPipelineAsset asset = GraphicsSettings.currentRenderPipeline;
        if (asset == null)
            return;

        SetPropertyIfWritable(asset, "renderScale", renderScale);
        SetPropertyIfWritable(asset, "msaaSampleCount", msaaSamples);

        if (IsMobileProfile)
        {
            SetPropertyIfWritable(asset, "supportsHDR", false);
            SetPropertyIfWritable(asset, "shadowDistance", QualitySettings.shadowDistance);
        }
    }

    private void SetPropertyIfWritable(object target, string propertyName, object value)
    {
        if (target == null)
            return;

        PropertyInfo property = target.GetType().GetProperty(
            propertyName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic
        );

        if (property == null || !property.CanWrite)
            return;

        try
        {
            object converted = value;
            if (value != null && property.PropertyType != value.GetType())
                converted = System.Convert.ChangeType(value, property.PropertyType);

            property.SetValue(target, converted, null);
        }
        catch
        {
            // A versão do pipeline pode expor a propriedade apenas para leitura.
        }
    }

    private int NormalizeMsaa(int requested)
    {
        if (requested >= 4)
            return 4;
        if (requested >= 2)
            return 2;
        return 0;
    }

    private void OnValidate()
    {
        desktopTargetFps = Mathf.Clamp(desktopTargetFps, 30, 240);
        mobileTargetFps = Mathf.Clamp(mobileTargetFps, 30, 120);
        mobileRenderScale = Mathf.Clamp(mobileRenderScale, 0.5f, 1f);
        lowEndMobileRenderScale = Mathf.Clamp(lowEndMobileRenderScale, 0.5f, mobileRenderScale);
        mobileShadowDistance = Mathf.Max(5f, mobileShadowDistance);
        lowEndShadowDistance = Mathf.Clamp(lowEndShadowDistance, 5f, mobileShadowDistance);
    }
}
