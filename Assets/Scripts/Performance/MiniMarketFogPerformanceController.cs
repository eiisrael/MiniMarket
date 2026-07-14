using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// Neblina de distância voltada ao cenário urbano do MiniMarket.
///
/// Além do efeito visual, limita a distância máxima da câmera principal conforme o
/// preset escolhido, reduzindo a quantidade de cenário distante renderizado.
/// Os controles aparecem ao lado do painel F10 e são aplicados em tempo real.
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

    private const string PlayerPrefsKey = "MiniMarket.FogPreset";

    [Header("Estado")]
    public FogPreset presetAtual = FogPreset.Media;
    public bool salvarPreferencia = true;

    [Header("Cor")]
    public Color fogColor = new Color32(139, 157, 175, 255);

    [Header("Suave")]
    [Min(0f)] public float densidadeSuave = 0.0028f;
    [Min(50f)] public float alcanceSuave = 430f;

    [Header("Média - recomendada")]
    [Min(0f)] public float densidadeMedia = 0.0062f;
    [Min(50f)] public float alcanceMedio = 270f;

    [Header("Forte")]
    [Min(0f)] public float densidadeForte = 0.0125f;
    [Min(50f)] public float alcanceForte = 175f;

    [Header("Atualização")]
    [Min(0.2f)] public float cameraRefreshInterval = 1f;

    // A própria referência Camera é uma chave estável durante a vida do objeto.
    // Isso evita GetInstanceID(), removido no Unity 6.7 alpha, e não depende de EntityId.
    private readonly Dictionary<Camera, float> originalFarClipByCamera =
        new Dictionary<Camera, float>();
    private readonly List<Camera> camerasToRemove = new List<Camera>();

    private RuntimeDiagnosticsPanel diagnosticsPanel;
    private FieldInfo diagnosticsVisibleField;
    private float nextCameraRefresh;

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

        GameObject host = new GameObject("[MiniMarket] Fog Performance");
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

    private void OnDestroy()
    {
        RestoreOriginalCameraDistances();
        DestroyTexture(ref panelTexture);
        DestroyTexture(ref activeTexture);
        DestroyTexture(ref buttonTexture);

        if (Instance == this)
            Instance = null;
    }

    private void Update()
    {
        ResolveDiagnosticsPanel();

        if (Time.unscaledTime < nextCameraRefresh)
            return;

        nextCameraRefresh = Time.unscaledTime + Mathf.Max(0.2f, cameraRefreshInterval);
        ApplyFogAndCameraDistance();
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

        ApplyFogAndCameraDistance();
    }

    private void ApplyFogAndCameraDistance()
    {
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogColor = fogColor;
        RenderSettings.fogDensity = CurrentDensity;

        PruneDestroyedCameraReferences();

        Camera[] cameras = Object.FindObjectsByType<Camera>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None
        );

        float requestedFarClip = CurrentFarClip;
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
            Camera cameraTarget = pair.Key;
            if (cameraTarget != null)
                cameraTarget.farClipPlane = pair.Value;
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

    private float CurrentDensity
    {
        get
        {
            switch (presetAtual)
            {
                case FogPreset.Suave:
                    return Mathf.Max(0f, densidadeSuave);
                case FogPreset.Forte:
                    return Mathf.Max(0f, densidadeForte);
                default:
                    return Mathf.Max(0f, densidadeMedia);
            }
        }
    }

    private float CurrentFarClip
    {
        get
        {
            switch (presetAtual)
            {
                case FogPreset.Suave:
                    return Mathf.Max(50f, alcanceSuave);
                case FogPreset.Forte:
                    return Mathf.Max(50f, alcanceForte);
                default:
                    return Mathf.Max(50f, alcanceMedio);
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

        const float panelWidth = 300f;
        const float panelHeight = 238f;
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

        Rect panelRect = new Rect(x, y, panelWidth, panelHeight);
        GUI.Box(panelRect, GUIContent.none, panelStyle);

        GUI.Label(
            new Rect(x + 18f, y + 14f, panelWidth - 36f, 30f),
            "NEBLINA E DESEMPENHO",
            titleStyle
        );
        GUI.Label(
            new Rect(x + 18f, y + 44f, panelWidth - 36f, 40f),
            "Ajuste em tempo real • sem reiniciar",
            subtitleStyle
        );

        GUI.Label(
            new Rect(x + 18f, y + 82f, panelWidth - 36f, 23f),
            "Atual: " + PresetLabel +
            "  •  Densidade " + CurrentDensity.ToString("0.0000"),
            labelStyle
        );
        GUI.Label(
            new Rect(x + 18f, y + 105f, panelWidth - 36f, 23f),
            "Distância renderizada: até " + CurrentFarClip.ToString("0") + " m",
            labelStyle
        );

        float buttonY = y + 142f;
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
            new Rect(x + 18f, y + 196f, panelWidth - 36f, 28f),
            presetAtual == FogPreset.Media
                ? "MÉDIA é o equilíbrio recomendado para a cidade."
                : "Menor alcance pode reduzir o custo de renderização.",
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
        densidadeSuave = Mathf.Max(0f, densidadeSuave);
        densidadeMedia = Mathf.Max(0f, densidadeMedia);
        densidadeForte = Mathf.Max(0f, densidadeForte);
        alcanceSuave = Mathf.Max(50f, alcanceSuave);
        alcanceMedio = Mathf.Max(50f, alcanceMedio);
        alcanceForte = Mathf.Max(50f, alcanceForte);
        cameraRefreshInterval = Mathf.Max(0.2f, cameraRefreshInterval);
    }
}
