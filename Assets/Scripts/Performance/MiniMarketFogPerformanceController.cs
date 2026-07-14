using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// Controla a distância de visão do jogo.
///
/// Não aplica neblina exponencial sobre a cidade. Em vez disso, cria um fade linear
/// somente no fundo do campo de visão, escondendo o corte do far clip da câmera e
/// reduzindo a renderização distante sem alterar a leitura dos objetos próximos.
/// </summary>
[DefaultExecutionOrder(30500)]
[DisallowMultipleComponent]
public sealed class MiniMarketFogPerformanceController : MonoBehaviour
{
    public enum FogPreset
    {
        Suave = 0,
        Media = 1,
        Forte = 2
    }

    public static MiniMarketFogPerformanceController Instance { get; private set; }

    private const string PlayerPrefsKey = "MiniMarket.ViewDistancePreset";

    [Header("Estado")]
    public FogPreset presetAtual = FogPreset.Media;
    public bool salvarPreferencia = true;
    public bool usarFadeDeProfundidade = true;

    [Header("Cor do limite de visão")]
    public bool sincronizarCorComAmbiente = true;
    public Color corManual = new Color32(135, 151, 168, 255);
    [Min(0.1f)] public float velocidadeCor = 3.5f;

    [Header("Suave - maior distância")]
    [Min(20f)] public float inicioSuave = 360f;
    [Min(30f)] public float fimSuave = 430f;

    [Header("Média - recomendada")]
    [Min(20f)] public float inicioMedio = 215f;
    [Min(30f)] public float fimMedio = 270f;

    [Header("Forte - melhor desempenho")]
    [Min(20f)] public float inicioForte = 130f;
    [Min(30f)] public float fimForte = 175f;

    [Header("Câmeras")]
    [Min(1f)] public float margemAposFade = 8f;
    [Min(0.1f)] public float cameraRefreshInterval = 0.5f;

    private readonly Dictionary<Camera, float> originalFarClipByCamera =
        new Dictionary<Camera, float>();
    private readonly List<Camera> camerasToRemove = new List<Camera>();

    private RuntimeDiagnosticsPanel diagnosticsPanel;
    private FieldInfo diagnosticsVisibleField;
    private float nextCameraRefresh;
    private Color currentFogColor;
    private bool colorInitialized;

    private GUIStyle panelStyle;
    private GUIStyle titleStyle;
    private GUIStyle subtitleStyle;
    private GUIStyle labelStyle;
    private GUIStyle activeButtonStyle;
    private GUIStyle buttonStyle;
    private Texture2D panelTexture;
    private Texture2D activeTexture;
    private Texture2D buttonTexture;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Instance = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        MiniMarketFogPerformanceController existing =
            Object.FindAnyObjectByType<MiniMarketFogPerformanceController>(
                FindObjectsInactive.Include
            );

        if (existing != null)
        {
            Instance = existing;
            return;
        }

        GameObject host = new GameObject("[MiniMarket] View Distance");
        DontDestroyOnLoad(host);
        Instance = host.AddComponent<MiniMarketFogPerformanceController>();
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

        int saved = PlayerPrefs.GetInt(PlayerPrefsKey, (int)FogPreset.Media);
        presetAtual = (FogPreset)Mathf.Clamp(saved, 0, 2);

        ResolveDiagnosticsPanel();
        ApplyPreset(presetAtual, false);
    }

    private void LateUpdate()
    {
        ResolveDiagnosticsPanel();
        ApplyLinearViewFade();

        if (Time.unscaledTime >= nextCameraRefresh)
        {
            nextCameraRefresh = Time.unscaledTime + Mathf.Max(0.1f, cameraRefreshInterval);
            ApplyCameraDistance();
        }
    }

    private void OnDestroy()
    {
        RestoreOriginalCameraDistances();
        DestroyTexture(ref panelTexture);
        DestroyTexture(ref activeTexture);
        DestroyTexture(ref buttonTexture);

        if (Instance == this)
            Instance = null;
    }

    public void ApplySoft()
    {
        ApplyPreset(FogPreset.Suave, true);
    }

    public void ApplyMedium()
    {
        ApplyPreset(FogPreset.Media, true);
    }

    public void ApplyStrong()
    {
        ApplyPreset(FogPreset.Forte, true);
    }

    public void ApplyPreset(FogPreset preset, bool persist)
    {
        presetAtual = preset;

        if (persist && salvarPreferencia)
        {
            PlayerPrefs.SetInt(PlayerPrefsKey, (int)presetAtual);
            PlayerPrefs.Save();
        }

        ApplyLinearViewFade();
        ApplyCameraDistance();
    }

    private void ApplyLinearViewFade()
    {
        if (!usarFadeDeProfundidade)
        {
            RenderSettings.fog = false;
            RestoreOriginalCameraDistances();
            return;
        }

        float start = CurrentStartDistance;
        float end = CurrentEndDistance;

        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogStartDistance = start;
        RenderSettings.fogEndDistance = end;
        RenderSettings.fogDensity = 0f;

        Color targetColor = ResolveTargetFogColor();
        if (!colorInitialized)
        {
            currentFogColor = targetColor;
            colorInitialized = true;
        }
        else
        {
            float delta = Mathf.Clamp(Time.unscaledDeltaTime, 0.0001f, 0.05f);
            float blend = 1f - Mathf.Exp(-Mathf.Max(0.1f, velocidadeCor) * delta);
            currentFogColor = Color.Lerp(currentFogColor, targetColor, blend);
        }

        RenderSettings.fogColor = currentFogColor;
    }

    private Color ResolveTargetFogColor()
    {
        if (!sincronizarCorComAmbiente)
            return corManual;

        Color sky = RenderSettings.ambientSkyColor;
        Color equator = RenderSettings.ambientEquatorColor;
        Color ambient = Color.Lerp(equator, sky, 0.68f);
        ambient.a = 1f;
        return ambient;
    }

    private void ApplyCameraDistance()
    {
        if (!usarFadeDeProfundidade)
            return;

        PruneDestroyedCameraReferences();

        Camera[] cameras = Object.FindObjectsByType<Camera>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None
        );

        float requestedFarClip = CurrentEndDistance + Mathf.Max(1f, margemAposFade);

        for (int i = 0; i < cameras.Length; i++)
        {
            Camera cameraTarget = cameras[i];
            if (!ShouldControlCamera(cameraTarget))
                continue;

            if (!originalFarClipByCamera.TryGetValue(cameraTarget, out float original))
            {
                original = cameraTarget.farClipPlane;
                originalFarClipByCamera.Add(cameraTarget, original);
            }

            cameraTarget.farClipPlane = Mathf.Max(
                cameraTarget.nearClipPlane + 10f,
                Mathf.Min(original, requestedFarClip)
            );
        }
    }

    private void PruneDestroyedCameraReferences()
    {
        if (originalFarClipByCamera.Count == 0)
            return;

        camerasToRemove.Clear();
        foreach (KeyValuePair<Camera, float> pair in originalFarClipByCamera)
        {
            if (pair.Key == null)
                camerasToRemove.Add(pair.Key);
        }

        for (int i = 0; i < camerasToRemove.Count; i++)
            originalFarClipByCamera.Remove(camerasToRemove[i]);

        camerasToRemove.Clear();
    }

    private void RestoreOriginalCameraDistances()
    {
        foreach (KeyValuePair<Camera, float> pair in originalFarClipByCamera)
        {
            if (pair.Key != null)
                pair.Key.farClipPlane = pair.Value;
        }

        originalFarClipByCamera.Clear();
        camerasToRemove.Clear();
    }

    private static bool ShouldControlCamera(Camera cameraTarget)
    {
        if (cameraTarget == null || !cameraTarget.enabled || !cameraTarget.gameObject.activeInHierarchy)
            return false;
        if (cameraTarget.orthographic || cameraTarget.cameraType != CameraType.Game)
            return false;
        if (cameraTarget.targetTexture != null)
            return false;

        string compactName = cameraTarget.name.ToLowerInvariant()
            .Replace("_", string.Empty)
            .Replace("-", string.Empty)
            .Replace(" ", string.Empty);

        return !compactName.Contains("minimap") &&
               !compactName.Contains("mapcamera") &&
               !compactName.Contains("preview") &&
               !compactName.Contains("uicamera");
    }

    private float CurrentStartDistance
    {
        get
        {
            switch (presetAtual)
            {
                case FogPreset.Suave:
                    return Mathf.Max(20f, Mathf.Min(inicioSuave, fimSuave - 10f));
                case FogPreset.Forte:
                    return Mathf.Max(20f, Mathf.Min(inicioForte, fimForte - 10f));
                default:
                    return Mathf.Max(20f, Mathf.Min(inicioMedio, fimMedio - 10f));
            }
        }
    }

    private float CurrentEndDistance
    {
        get
        {
            switch (presetAtual)
            {
                case FogPreset.Suave:
                    return Mathf.Max(30f, fimSuave);
                case FogPreset.Forte:
                    return Mathf.Max(30f, fimForte);
                default:
                    return Mathf.Max(30f, fimMedio);
            }
        }
    }

    private void ResolveDiagnosticsPanel()
    {
        if (diagnosticsPanel == null)
        {
            diagnosticsPanel = RuntimeDiagnosticsPanel.Instance;
            if (diagnosticsPanel == null)
            {
                diagnosticsPanel = Object.FindAnyObjectByType<RuntimeDiagnosticsPanel>(
                    FindObjectsInactive.Include
                );
            }
        }

        if (diagnosticsVisibleField == null)
        {
            diagnosticsVisibleField = typeof(RuntimeDiagnosticsPanel).GetField(
                "visible",
                BindingFlags.Instance | BindingFlags.NonPublic
            );
        }
    }

    private bool IsDiagnosticsVisible()
    {
        if (diagnosticsPanel == null || diagnosticsVisibleField == null)
            return false;

        object value = diagnosticsVisibleField.GetValue(diagnosticsPanel);
        return value is bool && (bool)value;
    }

    private void OnGUI()
    {
        if (!IsDiagnosticsVisible())
            return;

        EnsureGuiResources();
        GUI.depth = -11000;

        float diagnosticsMargin = diagnosticsPanel != null ? diagnosticsPanel.margin : 18f;
        float diagnosticsWidth = diagnosticsPanel != null ? diagnosticsPanel.width : 820f;
        float diagnosticsHeight = diagnosticsPanel != null ? diagnosticsPanel.height : 690f;

        const float panelWidth = 320f;
        const float panelHeight = 252f;
        float x = diagnosticsMargin + diagnosticsWidth + 12f;
        float y = diagnosticsMargin;

        if (x + panelWidth > Screen.width - diagnosticsMargin)
        {
            x = Mathf.Max(
                diagnosticsMargin + 12f,
                diagnosticsMargin + diagnosticsWidth - panelWidth - 18f
            );
            y = Mathf.Max(
                diagnosticsMargin + 100f,
                diagnosticsMargin + diagnosticsHeight - panelHeight - 18f
            );
        }

        GUI.Box(new Rect(x, y, panelWidth, panelHeight), GUIContent.none, panelStyle);

        GUI.Label(
            new Rect(x + 18f, y + 14f, panelWidth - 36f, 30f),
            "DISTÂNCIA DE VISÃO",
            titleStyle
        );
        GUI.Label(
            new Rect(x + 18f, y + 44f, panelWidth - 36f, 40f),
            "Fade linear somente no fundo • sem neblina próxima",
            subtitleStyle
        );

        GUI.Label(
            new Rect(x + 18f, y + 84f, panelWidth - 36f, 23f),
            "Preset: " + PresetLabel,
            labelStyle
        );
        GUI.Label(
            new Rect(x + 18f, y + 108f, panelWidth - 36f, 23f),
            "Fade: " + CurrentStartDistance.ToString("0") + " m → " +
            CurrentEndDistance.ToString("0") + " m",
            labelStyle
        );

        float buttonY = y + 148f;
        const float gap = 8f;
        float buttonWidth = (panelWidth - 36f - gap * 2f) / 3f;

        if (GUI.Button(
                new Rect(x + 18f, buttonY, buttonWidth, 46f),
                "SUAVE",
                presetAtual == FogPreset.Suave ? activeButtonStyle : buttonStyle))
        {
            ApplySoft();
        }

        if (GUI.Button(
                new Rect(x + 18f + buttonWidth + gap, buttonY, buttonWidth, 46f),
                "MÉDIA",
                presetAtual == FogPreset.Media ? activeButtonStyle : buttonStyle))
        {
            ApplyMedium();
        }

        if (GUI.Button(
                new Rect(x + 18f + (buttonWidth + gap) * 2f, buttonY, buttonWidth, 46f),
                "FORTE",
                presetAtual == FogPreset.Forte ? activeButtonStyle : buttonStyle))
        {
            ApplyStrong();
        }

        GUI.Label(
            new Rect(x + 18f, y + 204f, panelWidth - 36f, 34f),
            "MÉDIA mantém a cidade nítida e reduz o cenário distante.",
            subtitleStyle
        );
    }

    private string PresetLabel
    {
        get
        {
            switch (presetAtual)
            {
                case FogPreset.Suave:
                    return "SUAVE";
                case FogPreset.Forte:
                    return "FORTE";
                default:
                    return "MÉDIA";
            }
        }
    }

    private void EnsureGuiResources()
    {
        if (panelTexture == null)
            panelTexture = CreateTexture(new Color32(15, 22, 37, 250));
        if (activeTexture == null)
            activeTexture = CreateTexture(new Color32(226, 168, 43, 255));
        if (buttonTexture == null)
            buttonTexture = CreateTexture(new Color32(38, 49, 72, 255));

        if (panelStyle == null)
        {
            panelStyle = new GUIStyle(GUI.skin.box);
            panelStyle.normal.background = panelTexture;
            panelStyle.border = new RectOffset(2, 2, 2, 2);
        }

        if (titleStyle == null)
        {
            titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 18,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft
            };
            titleStyle.normal.textColor = new Color32(255, 196, 62, 255);
        }

        if (subtitleStyle == null)
        {
            subtitleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                alignment = TextAnchor.UpperLeft,
                wordWrap = true
            };
            subtitleStyle.normal.textColor = new Color32(170, 185, 211, 255);
        }

        if (labelStyle == null)
        {
            labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                alignment = TextAnchor.MiddleLeft
            };
            labelStyle.normal.textColor = new Color32(238, 244, 255, 255);
        }

        if (buttonStyle == null)
        {
            buttonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            buttonStyle.normal.background = buttonTexture;
            buttonStyle.hover.background = buttonTexture;
            buttonStyle.normal.textColor = Color.white;
            buttonStyle.hover.textColor = Color.white;
        }

        if (activeButtonStyle == null)
        {
            activeButtonStyle = new GUIStyle(buttonStyle);
            activeButtonStyle.normal.background = activeTexture;
            activeButtonStyle.hover.background = activeTexture;
            activeButtonStyle.normal.textColor = new Color32(22, 25, 33, 255);
            activeButtonStyle.hover.textColor = new Color32(22, 25, 33, 255);
        }
    }

    private static Texture2D CreateTexture(Color color)
    {
        Texture2D texture = new Texture2D(1, 1, TextureFormat.RGBA32, false)
        {
            hideFlags = HideFlags.HideAndDontSave,
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp
        };
        texture.SetPixel(0, 0, color);
        texture.Apply(false, true);
        return texture;
    }

    private static void DestroyTexture(ref Texture2D texture)
    {
        if (texture != null)
            Destroy(texture);
        texture = null;
    }

    private void OnValidate()
    {
        inicioSuave = Mathf.Max(20f, inicioSuave);
        fimSuave = Mathf.Max(inicioSuave + 10f, fimSuave);
        inicioMedio = Mathf.Max(20f, inicioMedio);
        fimMedio = Mathf.Max(inicioMedio + 10f, fimMedio);
        inicioForte = Mathf.Max(20f, inicioForte);
        fimForte = Mathf.Max(inicioForte + 10f, fimForte);
        margemAposFade = Mathf.Max(1f, margemAposFade);
        cameraRefreshInterval = Mathf.Max(0.1f, cameraRefreshInterval);
        velocidadeCor = Mathf.Max(0.1f, velocidadeCor);
    }
}
