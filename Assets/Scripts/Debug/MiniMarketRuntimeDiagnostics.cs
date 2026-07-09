using System;
using System.Text;
using UnityEngine;
using UnityEngine.Profiling;

/// <summary>
/// Painel simples de diagnostico runtime.
/// Pressione F10 para mostrar/ocultar FPS, CPU, GPU, memoria e contagem de objetos.
/// Nao depende de Canvas; usa OnGUI para funcionar imediatamente.
/// </summary>
[DefaultExecutionOrder(30000)]
public class MiniMarketRuntimeDiagnostics : MonoBehaviour
{
    [Header("Input")]
    public KeyCode teclaAlternar = KeyCode.F10;
    public bool mostrarAoIniciar = false;

    [Header("Atualizacao")]
    [Tooltip("Intervalo para atualizar textos pesados, como contagem de objetos.")]
    [Min(0.1f)] public float intervaloAtualizacao = 0.5f;

    [Tooltip("Conta objetos da cena apenas quando o painel esta visivel.")]
    public bool contarObjetosQuandoVisivel = true;

    [Header("Visual")]
    public int largura = 520;
    public int altura = 360;
    public int margem = 12;
    public int tamanhoFonte = 15;

    private bool visivel;
    private float deltaTimeSuavizado;
    private float tempoProximaAtualizacao;
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
    }

    private void Update()
    {
        if (Input.GetKeyDown(teclaAlternar))
        {
            visivel = !visivel;
            tempoProximaAtualizacao = 0f;
        }

        deltaTimeSuavizado += (Time.unscaledDeltaTime - deltaTimeSuavizado) * 0.1f;

        if (!visivel)
            return;

        if (Time.unscaledTime >= tempoProximaAtualizacao)
        {
            AtualizarDadosPesados();
            MontarTexto();
            tempoProximaAtualizacao = Time.unscaledTime + intervaloAtualizacao;
        }
    }

    private void AtualizarDadosPesados()
    {
        if (!contarObjetosQuandoVisivel)
            return;

        objetosCena = FindObjectsOfType<GameObject>(true).Length;
        renderersCena = FindObjectsOfType<Renderer>(true).Length;
        collidersCena = FindObjectsOfType<Collider>(true).Length;
        rigidbodiesCena = FindObjectsOfType<Rigidbody>(true).Length;
        scriptsCena = FindObjectsOfType<MonoBehaviour>(true).Length;
        camerasCena = FindObjectsOfType<Camera>(true).Length;
        lightsCena = FindObjectsOfType<Light>(true).Length;
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
        sb.AppendLine("CPU Cores/Threads: " + SystemInfo.processorCount + " | Freq: " + SystemInfo.processorFrequency + " MHz");
        sb.AppendLine("GPU: " + SystemInfo.graphicsDeviceName);
        sb.AppendLine("GPU API: " + SystemInfo.graphicsDeviceType + " | VRAM: " + SystemInfo.graphicsMemorySize + " MB");
        sb.AppendLine("RAM Sistema: " + SystemInfo.systemMemorySize + " MB");
        sb.AppendLine();

        sb.AppendLine("Memoria Mono/GC: " + FormatBytes(monoMem));
        sb.AppendLine("Unity Allocated: " + FormatBytes(allocated));
        sb.AppendLine("Unity Reserved: " + FormatBytes(reserved));
        sb.AppendLine("Unity Unused Reserved: " + FormatBytes(unused));
        sb.AppendLine();

        sb.AppendLine("Objetos na cena: " + objetosCena);
        sb.AppendLine("Renderers: " + renderersCena + " | Colliders: " + collidersCena + " | Rigidbodies: " + rigidbodiesCena);
        sb.AppendLine("Scripts: " + scriptsCena + " | Cameras: " + camerasCena + " | Lights: " + lightsCena);
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

        if (bytes >= gb)
            return (bytes / gb).ToString("0.00") + " GB";

        if (bytes >= mb)
            return (bytes / mb).ToString("0.00") + " MB";

        if (bytes >= kb)
            return (bytes / kb).ToString("0.00") + " KB";

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
