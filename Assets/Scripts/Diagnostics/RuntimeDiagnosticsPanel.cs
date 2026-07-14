using System;
using UnityEngine;
using UnityEngine.Profiling;
using Object = UnityEngine.Object;

/// <summary>
/// Central técnica F10 do MiniMarket.
/// Interface gamer, rolável e organizada por desempenho, jogador, mundo e interação.
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(30000)]
public sealed class RuntimeDiagnosticsPanel : MonoBehaviour
{
    public static RuntimeDiagnosticsPanel Instance { get; private set; }

    [Header("Atalho")]
    public KeyCode toggleKey = KeyCode.F10;
    public bool showOnStart;

    [Header("Atualização")]
    [Min(0.05f)] public float dataRefreshInterval = 0.20f;
    [Min(0.5f)] public float objectRefreshInterval = 2f;

    [Header("Dimensões")]
    public int width = 820;
    public int height = 690;
    public int margin = 18;

    private bool visible;
    private float smoothedDelta;
    private float nextDataRefresh;
    private float nextObjectRefresh;
    private Vector2 scrollPosition;

    private float fps;
    private float frameMs;
    private long managedMemory;
    private long allocatedMemory;
    private long reservedMemory;

    private int objectCount;
    private int rendererCount;
    private int colliderCount;
    private int rigidbodyCount;
    private int cameraCount;
    private int activeCameraCount;
    private int listenerCount;

    private MiniMarketPlayerDatabase database;
    private PlayerCameraController cameraController;
    private CameraRelativeMovement movement;
    private GetItemController getItemController;
    private InteractionFocusController interactionController;
    private BuySceneCameraModeController purchaseController;
    private RuntimeMiniMap miniMap;

    private GUIStyle titleStyle;
    private GUIStyle subtitleStyle;
    private GUIStyle sectionStyle;
    private GUIStyle labelStyle;
    private GUIStyle valueStyle;
    private GUIStyle badgeStyle;
    private GUIStyle closeButtonStyle;
    private GUIStyle cardStyle;

    private Texture2D panelTexture;
    private Texture2D headerTexture;
    private Texture2D cardTexture;
    private Texture2D darkTexture;
    private Texture2D goldTexture;
    private Texture2D greenTexture;
    private Texture2D yellowTexture;
    private Texture2D redTexture;
    private Texture2D blueTexture;
    private Texture2D pinkTexture;

    private static readonly Color PanelColor = new Color32(10, 15, 27, 248);
    private static readonly Color HeaderColor = new Color32(22, 29, 48, 255);
    private static readonly Color CardColor = new Color32(27, 36, 57, 248);
    private static readonly Color DarkColor = new Color32(6, 10, 19, 245);
    private static readonly Color GoldColor = new Color32(255, 190, 48, 255);
    private static readonly Color GreenColor = new Color32(75, 229, 119, 255);
    private static readonly Color YellowColor = new Color32(245, 196, 55, 255);
    private static readonly Color RedColor = new Color32(236, 75, 83, 255);
    private static readonly Color BlueColor = new Color32(67, 178, 255, 255);
    private static readonly Color PinkColor = new Color32(255, 75, 148, 255);
    private static readonly Color TextColor = new Color32(241, 246, 255, 255);
    private static readonly Color MutedColor = new Color32(166, 181, 211, 255);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Instance = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void CreateAfterSceneLoad()
    {
        RuntimeDiagnosticsPanel existing =
            Object.FindAnyObjectByType<RuntimeDiagnosticsPanel>(FindObjectsInactive.Include);

        if (existing != null)
        {
            Instance = existing;
            return;
        }

        GameObject host = new GameObject("[MiniMarket] Runtime Diagnostics");
        DontDestroyOnLoad(host);
        Instance = host.AddComponent<RuntimeDiagnosticsPanel>();
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
        RefreshReferences();
        RefreshData();
    }

    private void OnDestroy()
    {
        DestroyTexture(ref panelTexture);
        DestroyTexture(ref headerTexture);
        DestroyTexture(ref cardTexture);
        DestroyTexture(ref darkTexture);
        DestroyTexture(ref goldTexture);
        DestroyTexture(ref greenTexture);
        DestroyTexture(ref yellowTexture);
        DestroyTexture(ref redTexture);
        DestroyTexture(ref blueTexture);
        DestroyTexture(ref pinkTexture);

        if (Instance == this)
            Instance = null;
    }

    private void Update()
    {
        if (Input.GetKeyDown(toggleKey))
        {
            visible = !visible;
            nextDataRefresh = 0f;
            nextObjectRefresh = 0f;

            if (visible)
            {
                RefreshObjectCounts();
                RefreshData();
            }
        }

        float currentDelta = Mathf.Max(Time.unscaledDeltaTime, 0.0001f);
        smoothedDelta += (currentDelta - smoothedDelta) * 0.08f;

        if (!visible)
            return;

        if (Time.unscaledTime >= nextObjectRefresh)
        {
            RefreshObjectCounts();
            nextObjectRefresh = Time.unscaledTime + Mathf.Max(0.5f, objectRefreshInterval);
        }

        if (Time.unscaledTime >= nextDataRefresh)
        {
            RefreshData();
            nextDataRefresh = Time.unscaledTime + Mathf.Max(0.05f, dataRefreshInterval);
        }
    }

    private void RefreshData()
    {
        fps = 1f / Mathf.Max(smoothedDelta, 0.0001f);
        frameMs = smoothedDelta * 1000f;
        managedMemory = GC.GetTotalMemory(false);
        allocatedMemory = Profiler.GetTotalAllocatedMemoryLong();
        reservedMemory = Profiler.GetTotalReservedMemoryLong();
        RefreshReferences();
    }

    private void RefreshReferences()
    {
        cameraController = Object.FindAnyObjectByType<PlayerCameraController>(
            FindObjectsInactive.Include
        );

        movement = cameraController != null && cameraController.movement != null
            ? cameraController.movement
            : Object.FindAnyObjectByType<CameraRelativeMovement>(FindObjectsInactive.Include);

        getItemController = cameraController != null
            ? cameraController.GetComponent<GetItemController>()
            : Object.FindAnyObjectByType<GetItemController>(FindObjectsInactive.Include);

        interactionController = cameraController != null
            ? cameraController.GetComponent<InteractionFocusController>()
            : Object.FindAnyObjectByType<InteractionFocusController>(FindObjectsInactive.Include);

        database = MiniMarketPlayerDatabase.Instance;
        purchaseController = Object.FindAnyObjectByType<BuySceneCameraModeController>(
            FindObjectsInactive.Include
        );
        miniMap = RuntimeMiniMap.Instance;
    }

    private void RefreshObjectCounts()
    {
        objectCount = Object.FindObjectsByType<GameObject>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None
        ).Length;
        rendererCount = Object.FindObjectsByType<Renderer>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None
        ).Length;
        colliderCount = Object.FindObjectsByType<Collider>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None
        ).Length;
        rigidbodyCount = Object.FindObjectsByType<Rigidbody>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None
        ).Length;

        Camera[] cameras = Object.FindObjectsByType<Camera>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None
        );
        cameraCount = cameras.Length;
        activeCameraCount = 0;

        for (int i = 0; i < cameras.Length; i++)
        {
            Camera target = cameras[i];
            if (target != null && target.enabled && target.gameObject.activeInHierarchy)
                activeCameraCount++;
        }

        AudioListener[] listeners = Object.FindObjectsByType<AudioListener>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None
        );
        listenerCount = 0;

        for (int i = 0; i < listeners.Length; i++)
        {
            AudioListener target = listeners[i];
            if (target != null && target.enabled && target.gameObject.activeInHierarchy)
                listenerCount++;
        }
    }

    private void OnGUI()
    {
        if (!visible)
            return;

        EnsureStylesAndTextures();
        GUI.depth = -10000;

        float safeWidth = Mathf.Max(520f, Mathf.Min(width, Screen.width - margin * 2f));
        float safeHeight = Mathf.Max(420f, Mathf.Min(height, Screen.height - margin * 2f));
        Rect panelRect = new Rect(margin, margin, safeWidth, safeHeight);

        GUI.DrawTexture(panelRect, panelTexture, ScaleMode.StretchToFill);
        DrawHeader(panelRect);

        Rect contentArea = new Rect(
            panelRect.x + 18f,
            panelRect.y + 103f,
            panelRect.width - 36f,
            panelRect.height - 121f
        );

        GUILayout.BeginArea(contentArea);
        scrollPosition = GUILayout.BeginScrollView(
            scrollPosition,
            false,
            true,
            GUIStyle.none,
            GUI.skin.verticalScrollbar
        );

        DrawPerformanceSection();
        DrawPlayerSection();
        DrawWorldSection();
        DrawInteractionSection();
        DrawSystemSection();
        GUILayout.Space(8f);

        GUILayout.EndScrollView();
        GUILayout.EndArea();
    }

    private void DrawHeader(Rect panelRect)
    {
        Rect header = new Rect(panelRect.x, panelRect.y, panelRect.width, 88f);
        GUI.DrawTexture(header, headerTexture, ScaleMode.StretchToFill);
        GUI.DrawTexture(new Rect(header.x, header.y, header.width, 6f), goldTexture);

        GUI.Label(
            new Rect(header.x + 24f, header.y + 15f, header.width - 220f, 38f),
            "MINIMARKET • CENTRAL DE DIAGNÓSTICO",
            titleStyle
        );
        GUI.Label(
            new Rect(header.x + 26f, header.y + 54f, header.width - 220f, 24f),
            "RUNTIME AO VIVO  •  F10 PARA FECHAR",
            subtitleStyle
        );

        Color statusColor = GetFpsColor();
        GUI.DrawTexture(
            new Rect(header.x + header.width - 174f, header.y + 23f, 92f, 38f),
            GetTexture(statusColor)
        );
        GUI.Label(
            new Rect(header.x + header.width - 174f, header.y + 23f, 92f, 38f),
            fps.ToString("0") + " FPS",
            badgeStyle
        );

        if (GUI.Button(
                new Rect(header.x + header.width - 62f, header.y + 20f, 42f, 42f),
                "×",
                closeButtonStyle))
        {
            visible = false;
        }
    }

    private void DrawPerformanceSection()
    {
        BeginSection("DESEMPENHO", GoldColor);
        DrawRow("FPS atual", fps.ToString("0.0"), GetFpsColor());
        DrawRow("Tempo de frame", frameMs.ToString("0.00") + " ms", GetFrameColor());
        DrawRow("Meta / VSync", Application.targetFrameRate + " FPS  •  VSync " + QualitySettings.vSyncCount, TextColor);
        DrawRow("Memória gerenciada", FormatBytes(managedMemory), TextColor);
        DrawRow("Unity alocada", FormatBytes(allocatedMemory), TextColor);
        DrawRow("Unity reservada", FormatBytes(reservedMemory), MutedColor);
        EndSection();
    }

    private void DrawPlayerSection()
    {
        BeginSection("JOGADOR E ENERGIA", GreenColor);

        float energy01 = movement != null
            ? Mathf.Clamp01(movement.EnergiaPercentual01)
            : (database != null ? Mathf.Clamp01(database.EnergiaPercentual01) : 0f);
        int energyPercent = Mathf.RoundToInt(energy01 * 100f);

        string cameraMode = cameraController == null
            ? "NÃO ENCONTRADA"
            : (cameraController.IsFirstPerson ? "PRIMEIRA PESSOA" : "TERCEIRA PESSOA");
        string movementState = movement == null
            ? "NÃO ENCONTRADO"
            : (movement.IsRunning ? "CORRENDO" : (movement.CurrentSpeed > 0.05f ? "ANDANDO" : "PARADO"));

        DrawRow("Câmera", cameraMode, cameraController != null ? BlueColor : RedColor);
        DrawRow("Movimento", movementState, movement != null ? TextColor : RedColor);
        DrawRow("Velocidade", movement != null ? movement.CurrentSpeed.ToString("0.00") + " m/s" : "--", TextColor);
        DrawRow("Cargas", movement != null ? movement.StaminaSegmentadaTexto : "--/--", TextColor);
        DrawProgressBar(energy01, GetEnergyColor(energyPercent), energyPercent + "% ENERGIA TOTAL");
        EndSection();
    }

    private void DrawWorldSection()
    {
        BeginSection("MUNDO E CENA", BlueColor);
        DrawRow("Objetos / Renderers", objectCount + "  /  " + rendererCount, TextColor);
        DrawRow("Colliders / Rigidbodies", colliderCount + "  /  " + rigidbodyCount, TextColor);
        DrawRow("Câmeras", cameraCount + " total  •  " + activeCameraCount + " ativas", activeCameraCount == 1 ? GreenColor : YellowColor);
        DrawRow("AudioListeners", listenerCount + " ativo(s)", listenerCount == 1 ? GreenColor : YellowColor);
        DrawRow("Minimapa", miniMap != null ? (miniMap.IsOpen ? "ABERTO" : "FECHADO") : "NÃO ENCONTRADO", miniMap != null ? TextColor : RedColor);
        EndSection();
    }

    private void DrawInteractionSection()
    {
        BeginSection("INTERAÇÃO E GAMEPLAY", PinkColor);

        string selected = getItemController != null && getItemController.SelectedItem != null
            ? getItemController.SelectedItem.name
            : "Nenhum";
        string held = getItemController != null && getItemController.HeldItem != null
            ? getItemController.HeldItem.name
            : "Nenhum";
        string focused = interactionController != null && interactionController.FocusedObject != null
            ? interactionController.FocusedObject.displayName
            : "Nenhum";
        string purchase = purchaseController == null
            ? "Controlador não encontrado"
            : (purchaseController.ModoCompraAtivo ? "MODO COMPRA ATIVO" : "Pronto");

        DrawRow("Item selecionado", selected, selected == "Nenhum" ? MutedColor : BlueColor);
        DrawRow("Item segurado", held, held == "Nenhum" ? MutedColor : GreenColor);
        DrawRow("Alvo interativo", focused, focused == "Nenhum" ? MutedColor : GoldColor);
        DrawRow("Compra de terreno", purchase, purchaseController != null ? TextColor : YellowColor);
        DrawRow("Input bloqueado", GameplayInputState.IsBlocked ? "SIM" : "NÃO", GameplayInputState.IsBlocked ? YellowColor : GreenColor);
        EndSection();
    }

    private void DrawSystemSection()
    {
        BeginSection("SISTEMA E BANCO", GoldColor);

        string quality = QualitySettings.names.Length > 0
            ? QualitySettings.names[Mathf.Clamp(QualitySettings.GetQualityLevel(), 0, QualitySettings.names.Length - 1)]
            : "--";

        DrawRow("Plataforma / Qualidade", Application.platform + "  •  " + quality, TextColor);
        DrawRow("CPU", SystemInfo.processorType + "  •  " + SystemInfo.processorCount + " threads", TextColor);
        DrawRow("GPU", SystemInfo.graphicsDeviceName + "  •  " + SystemInfo.graphicsMemorySize + " MB", TextColor);
        DrawRow("RAM do sistema", SystemInfo.systemMemorySize + " MB", TextColor);
        DrawRow("Banco local", database != null ? "CONECTADO" : "NÃO ENCONTRADO", database != null ? GreenColor : RedColor);
        DrawRow("Salvamento pendente", database != null ? (database.SalvamentoPendente ? "SIM" : "NÃO") : "--", database != null && database.SalvamentoPendente ? YellowColor : GreenColor);
        EndSection();
    }

    private void BeginSection(string title, Color accent)
    {
        GUILayout.BeginVertical(cardStyle, GUILayout.ExpandWidth(true));
        Color previous = GUI.color;
        GUI.color = accent;
        GUILayout.Label(title, sectionStyle, GUILayout.Height(28f));
        GUI.color = previous;
        GUILayout.Space(3f);
    }

    private static void EndSection()
    {
        GUILayout.Space(7f);
        GUILayout.EndVertical();
        GUILayout.Space(12f);
    }

    private void DrawRow(string label, string value, Color valueColor)
    {
        GUILayout.BeginHorizontal(GUILayout.Height(25f));
        GUILayout.Label(label, labelStyle, GUILayout.Width(255f));

        Color previous = GUI.color;
        GUI.color = valueColor;
        GUILayout.Label(value, valueStyle, GUILayout.ExpandWidth(true));
        GUI.color = previous;

        GUILayout.EndHorizontal();
    }

    private void DrawProgressBar(float value, Color color, string label)
    {
        GUILayout.Space(5f);
        Rect rect = GUILayoutUtility.GetRect(120f, 32f, GUILayout.ExpandWidth(true));
        GUI.DrawTexture(rect, darkTexture, ScaleMode.StretchToFill);

        Rect inner = new Rect(rect.x + 4f, rect.y + 4f, rect.width - 8f, rect.height - 8f);
        Rect fill = new Rect(inner.x, inner.y, inner.width * Mathf.Clamp01(value), inner.height);
        GUI.DrawTexture(fill, GetTexture(color), ScaleMode.StretchToFill);
        GUI.Label(rect, label, badgeStyle);
    }

    private void EnsureStylesAndTextures()
    {
        if (panelTexture == null)
        {
            panelTexture = CreateTexture(PanelColor);
            headerTexture = CreateTexture(HeaderColor);
            cardTexture = CreateTexture(CardColor);
            darkTexture = CreateTexture(DarkColor);
            goldTexture = CreateTexture(GoldColor);
            greenTexture = CreateTexture(GreenColor);
            yellowTexture = CreateTexture(YellowColor);
            redTexture = CreateTexture(RedColor);
            blueTexture = CreateTexture(BlueColor);
            pinkTexture = CreateTexture(PinkColor);
        }

        if (titleStyle != null)
            return;

        titleStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 23,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleLeft
        };
        titleStyle.normal.textColor = TextColor;

        subtitleStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 12,
            alignment = TextAnchor.MiddleLeft
        };
        subtitleStyle.normal.textColor = MutedColor;

        sectionStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 17,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleLeft
        };
        sectionStyle.normal.textColor = Color.white;

        labelStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 14,
            alignment = TextAnchor.MiddleLeft
        };
        labelStyle.normal.textColor = MutedColor;

        valueStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 14,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleRight,
            clipping = TextClipping.Clip
        };
        valueStyle.normal.textColor = Color.white;

        badgeStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 14,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };
        badgeStyle.normal.textColor = Color.white;

        closeButtonStyle = new GUIStyle(GUI.skin.button)
        {
            fontSize = 28,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };
        closeButtonStyle.normal.textColor = Color.white;
        closeButtonStyle.hover.textColor = GoldColor;
        closeButtonStyle.active.textColor = RedColor;

        cardStyle = new GUIStyle(GUI.skin.box)
        {
            padding = new RectOffset(18, 18, 12, 12),
            margin = new RectOffset(0, 6, 0, 0)
        };
        cardStyle.normal.background = cardTexture;
    }

    private Color GetFpsColor()
    {
        if (fps >= 55f) return GreenColor;
        if (fps >= 30f) return YellowColor;
        return RedColor;
    }

    private Color GetFrameColor()
    {
        if (frameMs <= 18f) return GreenColor;
        if (frameMs <= 34f) return YellowColor;
        return RedColor;
    }

    private static Color GetEnergyColor(int percent)
    {
        if (percent > 60) return GreenColor;
        if (percent > 25) return YellowColor;
        return RedColor;
    }

    private Texture2D GetTexture(Color color)
    {
        if (Approximately(color, GreenColor)) return greenTexture;
        if (Approximately(color, YellowColor)) return yellowTexture;
        if (Approximately(color, RedColor)) return redTexture;
        if (Approximately(color, BlueColor)) return blueTexture;
        if (Approximately(color, PinkColor)) return pinkTexture;
        return goldTexture;
    }

    private static bool Approximately(Color a, Color b)
    {
        return Mathf.Abs(a.r - b.r) < 0.01f &&
               Mathf.Abs(a.g - b.g) < 0.01f &&
               Mathf.Abs(a.b - b.b) < 0.01f;
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
            Object.Destroy(texture);
        texture = null;
    }

    private static string FormatBytes(long bytes)
    {
        const double kb = 1024.0;
        const double mb = kb * 1024.0;
        const double gb = mb * 1024.0;

        if (bytes >= gb) return (bytes / gb).ToString("0.00") + " GB";
        if (bytes >= mb) return (bytes / mb).ToString("0.00") + " MB";
        if (bytes >= kb) return (bytes / kb).ToString("0.00") + " KB";
        return bytes + " B";
    }
}
