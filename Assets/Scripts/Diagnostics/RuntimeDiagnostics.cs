using System;
using System.Text;
using UnityEngine;
using UnityEngine.Profiling;

/// <summary>
/// Painel leve de diagnostico runtime.
/// F10 mostra/oculta FPS, memoria, CPU/GPU e contagens.
///
/// Ajuste atual:
/// - remove consultas pesadas frequentes enquanto o painel está aberto;
/// - usa FindObjectsByType sem ordenação e com intervalo maior;
/// - evita GC/strings por frame;
/// - não deve causar micro travadas ao mover a câmera.
/// </summary>
[DefaultExecutionOrder(30000)]
public class RuntimeDiagnostics : MonoBehaviour
{
    [Header("Input")]
    public KeyCode teclaAlternar = KeyCode.F10;
    public bool mostrarAoIniciar = false;

    [Header("Atualizacao")]
    [Tooltip("Intervalo para atualizar texto leve de FPS/memoria.")]
    [Min(0.1f)] public float intervaloAtualizacaoTexto = 0.35f;

    [Tooltip("Intervalo para atualizar contagem pesada de objetos.")]
    [Min(0.5f)] public float intervaloAtualizacaoObjetos = 3f;

    [Tooltip("Conta objetos somente em intervalo longo. Desligue se quiser painel ultra leve.")]
    public bool contarObjetosQuandoVisivel = true;

    [Header("Visual")]
    public int largura = 500;
    public int altura = 330;
    public int margem = 12;
    public int tamanhoFonte = 14;

    private bool visivel;
    private float deltaTimeSuavizado;
    private float proximaAtualizacaoTexto;
    private float proximaAtualizacaoObjetos;
    private string textoCache = string.Empty;
    private GUIStyle estiloJanela;
    private GUIStyle estiloTexto;
    private readonly StringBuilder sb = new StringBuilder(2048);

    private int objetosCena;
    private int renderersCena;
    private int collidersCena;
    private int rigidbodiesCena;
    private int scriptsCena;
    private int camerasCena;
    private int lightsCena;

    private void Awake()
    {
        visivel = mostrarAoIniciar;
        deltaTimeSuavizado = Time.unscaledDeltaTime;
    }

    private void Update()
    {
        if (Input.GetKeyDown(teclaAlternar))
        {
            visivel = !visivel;
            proximaAtualizacaoTexto = 0f;
            proximaAtualizacaoObjetos = 0f;

            UpgradeLogger.Log("Diagnostics", visivel ? "F10 aberto" : "F10 fechado", "Painel de diagnostico runtime alternado.", "diag-toggle", 0.5f);
        }

        deltaTimeSuavizado += (Time.unscaledDeltaTime - deltaTimeSuavizado) * 0.08f;

        if (!visivel)
            return;

        if (contarObjetosQuandoVisivel && Time.unscaledTime >= proximaAtualizacaoObjetos)
        {
            AtualizarDadosPesados();
            proximaAtualizacaoObjetos = Time.unscaledTime + intervaloAtualizacaoObjetos;
        }

        if (Time.unscaledTime >= proximaAtualizacaoTexto)
        {
            MontarTexto();
            proximaAtualizacaoTexto = Time.unscaledTime + intervaloAtualizacaoTexto;
        }
    }

    private void AtualizarDadosPesados()
    {
        objetosCena = UnityEngine.Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;
        renderersCena = UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;
        collidersCena = UnityEngine.Object.FindObjectsByType<Collider>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;
        rigidbodiesCena = UnityEngine.Object.FindObjectsByType<Rigidbody>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;
        scriptsCena = UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;
        camerasCena = UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;
        lightsCena = UnityEngine.Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;
    }

    private void MontarTexto()
    {
        float fps = deltaTimeSuavizado > 0.0001f ? 1f / deltaTimeSuavizado : 0f;
        float ms = deltaTimeSuavizado * 1000f;

        long monoMem = GC.GetTotalMemory(false);
        long allocated = Profiler.GetTotalAllocatedMemoryLong();
        long reserved = Profiler.GetTotalReservedMemoryLong();
        long unused = Profiler.GetTotalUnusedReservedMemoryLong();

        MiniMarketPlayerDatabase banco = MiniMarketPlayerDatabase.Instance;

        sb.Length = 0;
        sb.AppendLine("MINIMARKET DIAGNOSTICS  (F10 fecha)");
        sb.AppendLine("--------------------------------------------------");
        sb.AppendLine("FPS: " + fps.ToString("0") + " | Frame: " + ms.ToString("0.00") + " ms");
        sb.AppendLine("VSync: " + QualitySettings.vSyncCount + " | Target FPS: " + Application.targetFrameRate);
        sb.AppendLine("Tempo: " + Time.time.ToString("0.0") + "s | TimeScale: " + Time.timeScale.ToString("0.00"));
        sb.AppendLine();

        sb.AppendLine("CPU: " + SystemInfo.processorType);
        sb.AppendLine("CPU Threads: " + SystemInfo.processorCount + " | Freq: " + SystemInfo.processorFrequency + " MHz");
        sb.AppendLine("GPU: " + SystemInfo.graphicsDeviceName);
        sb.AppendLine("GPU API: " + SystemInfo.graphicsDeviceType + " | VRAM: " + SystemInfo.graphicsMemorySize + " MB");
        sb.AppendLine("RAM Sistema: " + SystemInfo.systemMemorySize + " MB");
        sb.AppendLine();

        sb.AppendLine("Mono/GC: " + FormatBytes(monoMem));
        sb.AppendLine("Unity Allocated: " + FormatBytes(allocated));
        sb.AppendLine("Unity Reserved: " + FormatBytes(reserved));
        sb.AppendLine("Unity Unused: " + FormatBytes(unused));
        sb.AppendLine();

        if (contarObjetosQuandoVisivel)
        {
            sb.AppendLine("Objetos: " + objetosCena + " | Renderers: " + renderersCena + " | Colliders: " + collidersCena);
            sb.AppendLine("Rigidbodies: " + rigidbodiesCena + " | Scripts: " + scriptsCena + " | Cameras: " + camerasCena + " | Lights: " + lightsCena);
        }
        else
        {
            sb.AppendLine("Contagem de objetos: desligada para desempenho");
        }

        sb.AppendLine();

        if (banco != null)
        {
            sb.AppendLine("Banco: OK | Save pendente: " + banco.SalvamentoPendente);
            sb.AppendLine("Gold: " + banco.GoldAtual + " | Stamina: " + banco.StaminaAtual.ToString("0.0") + "/" + banco.StaminaMaxima.ToString("0.0"));
            sb.AppendLine("Empresas: " + banco.EmpresasCompradas);
        }
        else
        {
            sb.AppendLine("Banco: nao encontrado na cena ainda");
        }

        textoCache = sb.ToString();
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
        if (!visivel)
            return;

        CriarEstilosSeNecessario();
        Rect rect = new Rect(margem, margem, largura, altura);
        GUI.Box(rect, GUIContent.none, estiloJanela);
        GUI.Label(new Rect(margem + 12, margem + 10, largura - 24, altura - 20), textoCache, estiloTexto);
    }

    private void CriarEstilosSeNecessario()
    {
        if (estiloJanela == null)
        {
            estiloJanela = new GUIStyle(GUI.skin.box);
            estiloJanela.normal.background = Texture2D.grayTexture;
        }

        if (estiloTexto == null)
        {
            estiloTexto = new GUIStyle(GUI.skin.label);
            estiloTexto.fontSize = tamanhoFonte;
            estiloTexto.normal.textColor = Color.white;
            estiloTexto.alignment = TextAnchor.UpperLeft;
            estiloTexto.wordWrap = false;
        }
    }
}
