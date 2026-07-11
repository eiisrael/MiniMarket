using System;
using System.Text;
using UnityEngine;
using UnityEngine.Profiling;
using Object = UnityEngine.Object;

/// <summary>
/// Painel F10 independente da antiga Camera V2. É criado automaticamente e mostra
/// desempenho, banco, energia, câmera, compra e minimapa sem depender da cena.
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(30000)]
public sealed class RuntimeDiagnosticsPanel : MonoBehaviour
{
    public static RuntimeDiagnosticsPanel Instance { get; private set; }

    public KeyCode toggleKey = KeyCode.F10;
    public bool showOnStart;
    [Min(0.1f)] public float textRefreshInterval = 0.35f;
    [Min(0.5f)] public float objectRefreshInterval = 2f;
    public int width = 620;
    public int height = 500;
    public int margin = 12;
    public int fontSize = 14;

    private readonly StringBuilder builder = new StringBuilder(4096);
    private bool visible;
    private float smoothedDelta;
    private float nextTextRefresh;
    private float nextObjectRefresh;
    private string cachedText = string.Empty;
    private GUIStyle windowStyle;
    private GUIStyle textStyle;

    private int objectCount;
    private int rendererCount;
    private int colliderCount;
    private int rigidbodyCount;
    private int cameraCount;
    private int activeCameraCount;
    private int listenerCount;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Instance = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void CreateAfterSceneLoad()
    {
        RuntimeDiagnosticsPanel existing = Object.FindAnyObjectByType<RuntimeDiagnosticsPanel>(FindObjectsInactive.Include);
        if (existing != null)
        {
            Instance = existing;
            return;
        }

        GameObject host = new GameObject("RuntimeDiagnosticsPanel");
        DontDestroyOnLoad(host);
        host.AddComponent<RuntimeDiagnosticsPanel>();
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
        visible = showOnStart;
        smoothedDelta = Mathf.Max(Time.unscaledDeltaTime, 0.0001f);
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void Update()
    {
        if (Input.GetKeyDown(toggleKey))
        {
            visible = !visible;
            nextTextRefresh = 0f;
            nextObjectRefresh = 0f;
        }

        smoothedDelta += (Mathf.Max(Time.unscaledDeltaTime, 0.0001f) - smoothedDelta) * 0.08f;

        if (!visible)
            return;

        if (Time.unscaledTime >= nextObjectRefresh)
        {
            RefreshObjectCounts();
            nextObjectRefresh = Time.unscaledTime + Mathf.Max(0.5f, objectRefreshInterval);
        }

        if (Time.unscaledTime >= nextTextRefresh)
        {
            BuildText();
            nextTextRefresh = Time.unscaledTime + Mathf.Max(0.1f, textRefreshInterval);
        }
    }

    private void RefreshObjectCounts()
    {
        objectCount = Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;
        rendererCount = Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;
        colliderCount = Object.FindObjectsByType<Collider>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;
        rigidbodyCount = Object.FindObjectsByType<Rigidbody>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;

        Camera[] cameras = Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        cameraCount = cameras.Length;
        activeCameraCount = 0;
        for (int i = 0; i < cameras.Length; i++)
        {
            if (cameras[i] != null && cameras[i].enabled && cameras[i].gameObject.activeInHierarchy)
                activeCameraCount++;
        }

        AudioListener[] listeners = Object.FindObjectsByType<AudioListener>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        listenerCount = 0;
        for (int i = 0; i < listeners.Length; i++)
        {
            if (listeners[i] != null && listeners[i].enabled && listeners[i].gameObject.activeInHierarchy)
                listenerCount++;
        }
    }

    private void BuildText()
    {
        float fps = 1f / Mathf.Max(smoothedDelta, 0.0001f);
        float frameMs = smoothedDelta * 1000f;
        long managed = GC.GetTotalMemory(false);
        long allocated = Profiler.GetTotalAllocatedMemoryLong();

        MiniMarketPlayerDatabase database = MiniMarketPlayerDatabase.Instance;
        CameraRelativeMovement movement = Object.FindAnyObjectByType<CameraRelativeMovement>(FindObjectsInactive.Include);
        PlayerCameraController cameraController = Object.FindAnyObjectByType<PlayerCameraController>(FindObjectsInactive.Include);
        BuySceneCameraModeController purchase = Object.FindAnyObjectByType<BuySceneCameraModeController>(FindObjectsInactive.Include);
        RuntimeMiniMap miniMap = RuntimeMiniMap.Instance;

        builder.Length = 0;
        builder.AppendLine("MINIMARKET RUNTIME DIAGNOSTICS  —  F10 fecha");
        builder.AppendLine("────────────────────────────────────────────────────────");
        builder.AppendLine("FPS: " + fps.ToString("0.0") + " | Frame: " + frameMs.ToString("0.00") + " ms");
        builder.AppendLine("Target FPS: " + Application.targetFrameRate + " | VSync: " + QualitySettings.vSyncCount);
        builder.AppendLine("Plataforma: " + Application.platform + " | Qualidade: " + QualitySettings.names[QualitySettings.GetQualityLevel()]);
        builder.AppendLine("CPU: " + SystemInfo.processorType + " | Threads: " + SystemInfo.processorCount);
        builder.AppendLine("GPU: " + SystemInfo.graphicsDeviceName + " | VRAM: " + SystemInfo.graphicsMemorySize + " MB");
        builder.AppendLine("RAM: " + SystemInfo.systemMemorySize + " MB | GC: " + FormatBytes(managed) + " | Unity: " + FormatBytes(allocated));
        builder.AppendLine();

        builder.AppendLine("Objetos: " + objectCount + " | Renderers: " + rendererCount + " | Colliders: " + colliderCount + " | Rigidbodies: " + rigidbodyCount);
        builder.AppendLine("Câmeras: " + cameraCount + " | Ativas: " + activeCameraCount + " | AudioListeners ativos: " + listenerCount);
        builder.AppendLine();

        if (cameraController != null)
        {
            builder.AppendLine("Câmera jogador: " + cameraController.CurrentMode +
                               " | Controle externo: " + cameraController.ExternalPoseControl +
                               " | Camera enabled: " + (cameraController.gameCamera != null && cameraController.gameCamera.enabled));
        }
        else
        {
            builder.AppendLine("Câmera jogador: NÃO ENCONTRADA");
        }

        if (movement != null)
        {
            builder.AppendLine("Energia runtime: " + movement.StaminaSegmentadaTexto +
                               " | Barra: " + (movement.StaminaPercentual01 * 100f).ToString("0.0") + "%" +
                               " | Reserva: " + movement.StaminaRecargaReserva.ToString("0.0"));
            builder.AppendLine("Movimento: " + (movement.IsRunning ? "CORRENDO" : movement.CurrentSpeed > 0.05f ? "ANDANDO" : "PARADO") +
                               " | Velocidade: " + movement.CurrentSpeed.ToString("0.00"));
        }
        else
        {
            builder.AppendLine("Movimento/Energia: NÃO ENCONTRADO");
        }

        if (database != null)
        {
            builder.AppendLine("Banco: OK | Save pendente: " + database.SalvamentoPendente);
            builder.AppendLine("Nome: " + database.NomePersonagem + " | Gold: " + database.GoldAtual + " | Empresas: " + database.EmpresasCompradas);
            builder.AppendLine("Energia banco: " + database.EnergiaSegmentosAtuais + "/" + database.EnergiaSegmentosMaximos +
                               " | Stamina: " + database.StaminaAtual.ToString("0.0") + "/" + database.StaminaMaxima.ToString("0.0"));
        }
        else
        {
            builder.AppendLine("Banco: NÃO ENCONTRADO");
        }

        builder.AppendLine();
        builder.AppendLine("Compra de terreno: " + (purchase != null ? (purchase.ModoCompraAtivo ? "MODO COMPRA ATIVO" : "pronto") : "controlador não encontrado"));
        builder.AppendLine("Minimapa: " + (miniMap != null ? (miniMap.IsOpen ? "aberto" : "fechado") : "não encontrado"));
        builder.AppendLine("Input bloqueado: " + GameplayInputState.IsBlocked);

        cachedText = builder.ToString();
    }

    private string FormatBytes(long bytes)
    {
        const double kb = 1024.0;
        const double mb = kb * 1024.0;
        const double gb = mb * 1024.0;

        if (bytes >= gb) return (bytes / gb).ToString("0.00") + " GB";
        if (bytes >= mb) return (bytes / mb).ToString("0.00") + " MB";
        if (bytes >= kb) return (bytes / kb).ToString("0.00") + " KB";
        return bytes + " B";
    }

    private void OnGUI()
    {
        if (!visible)
            return;

        EnsureStyles();
        Rect rect = new Rect(margin, margin, width, height);
        GUI.Box(rect, GUIContent.none, windowStyle);
        GUI.Label(new Rect(margin + 12, margin + 10, width - 24, height - 20), cachedText, textStyle);
    }

    private void EnsureStyles()
    {
        if (windowStyle == null)
        {
            windowStyle = new GUIStyle(GUI.skin.box);
            windowStyle.normal.background = Texture2D.grayTexture;
        }

        if (textStyle == null)
        {
            textStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = fontSize,
                alignment = TextAnchor.UpperLeft,
                wordWrap = false
            };
            textStyle.normal.textColor = Color.white;
        }
    }
}
