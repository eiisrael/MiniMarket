using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Logger global leve para registrar alteracoes, correcoes e eventos importantes do MiniMarket.
///
/// Arquivo principal no Editor:
/// - UpgradeLog.htm na raiz do projeto.
///
/// Arquivo fallback em runtime/build:
/// - Application.persistentDataPath/UpgradeLog.htm
///
/// Anti-spam:
/// - entradas passam por fila;
/// - flush por intervalo;
/// - rate limit por chave;
/// - captura warnings/erros do Unity sem loop infinito.
/// </summary>
[DefaultExecutionOrder(-20000)]
public class MiniMarketUpgradeLogger : MonoBehaviour
{
    [Header("Arquivo")]
    public string nomeArquivo = "UpgradeLog.htm";
    public bool escreverNaRaizDoProjetoNoEditor = true;
    public bool escreverTambemNoPersistentDataPath = true;

    [Header("Performance / Anti Spam")]
    [Min(0.25f)] public float intervaloFlush = 2f;
    [Min(1)] public int maxEntradasPorFlush = 30;
    [Min(10)] public int maxEntradasEmMemoria = 300;
    public bool capturarWarningsErrosUnity = true;
    public bool ignorarLogsNormaisUnity = true;

    [Header("Debug")]
    public bool logarCaminhoNoConsole;

    private static MiniMarketUpgradeLogger instancia;
    private static readonly Queue<LogEntry> fila = new Queue<LogEntry>(128);
    private static readonly Dictionary<string, float> ultimoLogPorChave = new Dictionary<string, float>(128);
    private static readonly object sync = new object();

    private float proximoFlush;
    private string caminhoProjeto;
    private string caminhoPersistent;
    private bool inicializado;
    private bool processandoLogUnity;

    private struct LogEntry
    {
        public DateTime time;
        public string category;
        public string title;
        public string details;
        public LogType type;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void CriarAutomaticamente()
    {
        if (instancia != null)
            return;

        GameObject go = new GameObject("MiniMarket_UpgradeLogger");
        DontDestroyOnLoad(go);
        instancia = go.AddComponent<MiniMarketUpgradeLogger>();
    }

    public static void Log(string category, string title, string details = "", string rateLimitKey = "", float minIntervalSeconds = 0f, LogType type = LogType.Log)
    {
        float now = Time.unscaledTime;

        if (!string.IsNullOrEmpty(rateLimitKey) && minIntervalSeconds > 0f)
        {
            lock (sync)
            {
                if (ultimoLogPorChave.TryGetValue(rateLimitKey, out float last) && now - last < minIntervalSeconds)
                    return;

                ultimoLogPorChave[rateLimitKey] = now;
            }
        }

        LogEntry entry = new LogEntry
        {
            time = DateTime.Now,
            category = string.IsNullOrWhiteSpace(category) ? "Geral" : category,
            title = string.IsNullOrWhiteSpace(title) ? "Evento" : title,
            details = details ?? string.Empty,
            type = type
        };

        lock (sync)
        {
            while (fila.Count > 500)
                fila.Dequeue();

            fila.Enqueue(entry);
        }
    }

    public static void LogChange(string title, string details)
    {
        Log("Upgrade", title, details, title, 0.25f, LogType.Log);
    }

    private void Awake()
    {
        if (instancia != null && instancia != this)
        {
            Destroy(gameObject);
            return;
        }

        instancia = this;
        DontDestroyOnLoad(gameObject);
        Inicializar();
    }

    private void OnEnable()
    {
        if (capturarWarningsErrosUnity)
            Application.logMessageReceived += AoLogUnity;
    }

    private void OnDisable()
    {
        Application.logMessageReceived -= AoLogUnity;
        Flush(true);
    }

    private void OnApplicationQuit()
    {
        Log("Sistema", "Aplicacao finalizada", "Flush final do UpgradeLog.", "app-quit", 0f);
        Flush(true);
    }

    private void Update()
    {
        if (!inicializado)
            Inicializar();

        if (Time.unscaledTime >= proximoFlush)
        {
            proximoFlush = Time.unscaledTime + intervaloFlush;
            Flush(false);
        }
    }

    private void Inicializar()
    {
        if (inicializado)
            return;

        caminhoPersistent = Path.Combine(Application.persistentDataPath, nomeArquivo);

#if UNITY_EDITOR
        string raiz = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        caminhoProjeto = Path.Combine(raiz, nomeArquivo);
#else
        caminhoProjeto = string.Empty;
#endif

        GarantirArquivo(caminhoPersistent);

#if UNITY_EDITOR
        if (escreverNaRaizDoProjetoNoEditor)
            GarantirArquivo(caminhoProjeto);
#endif

        inicializado = true;
        proximoFlush = Time.unscaledTime + intervaloFlush;

        Log("Sistema", "UpgradeLogger iniciado", "Projeto: " + (string.IsNullOrEmpty(caminhoProjeto) ? "N/A" : caminhoProjeto) + " | Runtime: " + caminhoPersistent, "logger-start", 2f);

        if (logarCaminhoNoConsole)
            Debug.Log("[MiniMarketUpgradeLogger] " + (string.IsNullOrEmpty(caminhoProjeto) ? caminhoPersistent : caminhoProjeto));
    }

    private void AoLogUnity(string condition, string stackTrace, LogType type)
    {
        if (processandoLogUnity)
            return;

        if (type == LogType.Log && ignorarLogsNormaisUnity)
            return;

        processandoLogUnity = true;

        string titulo = type == LogType.Error || type == LogType.Exception ? "Unity Error" : "Unity Warning";
        string detalhes = condition;

        if (type == LogType.Exception && !string.IsNullOrEmpty(stackTrace))
            detalhes += "\n" + stackTrace;

        Log("Unity", titulo, detalhes, "unity-" + condition.GetHashCode(), 2f, type);
        processandoLogUnity = false;
    }

    private void Flush(bool forcar)
    {
        if (!inicializado)
            Inicializar();

        List<LogEntry> entradas = new List<LogEntry>(maxEntradasPorFlush);

        lock (sync)
        {
            int count = forcar ? fila.Count : Mathf.Min(maxEntradasPorFlush, fila.Count);
            for (int i = 0; i < count; i++)
                entradas.Add(fila.Dequeue());
        }

        if (entradas.Count == 0)
            return;

        string html = MontarHtmlEntradas(entradas);

#if UNITY_EDITOR
        if (escreverNaRaizDoProjetoNoEditor && !string.IsNullOrEmpty(caminhoProjeto))
            AcrescentarAntesDoFim(caminhoProjeto, html);
#endif

        if (escreverTambemNoPersistentDataPath)
            AcrescentarAntesDoFim(caminhoPersistent, html);

#if UNITY_EDITOR
        if (escreverNaRaizDoProjetoNoEditor && !string.IsNullOrEmpty(caminhoProjeto))
            AssetDatabase.Refresh();
#endif
    }

    private void GarantirArquivo(string path)
    {
        if (string.IsNullOrEmpty(path))
            return;

        string dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        if (File.Exists(path))
            return;

        File.WriteAllText(path, CriarHtmlBase(), Encoding.UTF8);
    }

    private string CriarHtmlBase()
    {
        return "<!doctype html>\n<html lang=\"pt-BR\">\n<head>\n<meta charset=\"utf-8\">\n<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">\n<title>MiniMarket Upgrade Log</title>\n<style>\nbody{margin:0;background:#111827;color:#e5e7eb;font-family:Segoe UI,Arial,sans-serif;}\nheader{position:sticky;top:0;background:linear-gradient(90deg,#0f172a,#1e293b);padding:22px 28px;border-bottom:1px solid #334155;z-index:2;}\nh1{margin:0;font-size:26px;color:#facc15;}\n.subtitle{margin-top:6px;color:#cbd5e1;font-size:14px;}\nmain{padding:22px 28px 80px;}\n.entry{background:#1f2937;border:1px solid #374151;border-left:5px solid #38bdf8;border-radius:12px;margin:0 0 14px;padding:16px 18px;box-shadow:0 8px 20px rgba(0,0,0,.22);}\n.entry.warn{border-left-color:#f59e0b}.entry.error{border-left-color:#ef4444}.entry.upgrade{border-left-color:#22c55e}.entry.camera{border-left-color:#a78bfa}.entry.grabber{border-left-color:#06b6d4}\n.meta{display:flex;gap:10px;flex-wrap:wrap;align-items:center;color:#94a3b8;font-size:12px;margin-bottom:8px}.tag{background:#0f172a;border:1px solid #475569;border-radius:999px;padding:3px 9px;color:#cbd5e1}.title{font-size:17px;font-weight:700;color:#fff;margin-bottom:8px}.details{white-space:pre-wrap;line-height:1.45;color:#d1d5db}code{color:#facc15}\n</style>\n</head>\n<body>\n<header><h1>MiniMarket Upgrade Log</h1><div class=\"subtitle\">Registro organizado de alteracoes, diagnosticos, correcoes e eventos importantes do projeto.</div></header>\n<main>\n<!-- LOG_ENTRIES -->\n</main>\n</body>\n</html>\n";
    }

    private string MontarHtmlEntradas(List<LogEntry> entries)
    {
        StringBuilder sb = new StringBuilder(entries.Count * 512);

        for (int i = 0; i < entries.Count; i++)
        {
            LogEntry e = entries[i];
            string css = "entry " + CssClass(e);
            sb.Append("<section class=\"").Append(css).Append("\">\n");
            sb.Append("<div class=\"meta\"><span class=\"tag\">").Append(Html(e.time.ToString("yyyy-MM-dd HH:mm:ss"))).Append("</span><span class=\"tag\">").Append(Html(e.category)).Append("</span><span class=\"tag\">").Append(Html(e.type.ToString())).Append("</span></div>\n");
            sb.Append("<div class=\"title\">").Append(Html(e.title)).Append("</div>\n");
            if (!string.IsNullOrWhiteSpace(e.details))
                sb.Append("<div class=\"details\">").Append(Html(e.details)).Append("</div>\n");
            sb.Append("</section>\n");
        }

        return sb.ToString();
    }

    private string CssClass(LogEntry e)
    {
        if (e.type == LogType.Error || e.type == LogType.Exception)
            return "error";

        if (e.type == LogType.Warning)
            return "warn";

        string c = (e.category ?? string.Empty).ToLowerInvariant();
        if (c.Contains("camera")) return "camera";
        if (c.Contains("grab") || c.Contains("item")) return "grabber";
        if (c.Contains("upgrade")) return "upgrade";
        return string.Empty;
    }

    private void AcrescentarAntesDoFim(string path, string htmlEntradas)
    {
        GarantirArquivo(path);

        string conteudo = File.ReadAllText(path, Encoding.UTF8);
        string marcador = "<!-- LOG_ENTRIES -->";
        int idx = conteudo.IndexOf(marcador, StringComparison.Ordinal);

        if (idx >= 0)
            conteudo = conteudo.Insert(idx + marcador.Length, "\n" + htmlEntradas);
        else
            conteudo += "\n" + htmlEntradas;

        File.WriteAllText(path, conteudo, Encoding.UTF8);
    }

    private string Html(string s)
    {
        if (string.IsNullOrEmpty(s))
            return string.Empty;

        return s.Replace("&", "&amp;")
                .Replace("<", "&lt;")
                .Replace(">", "&gt;")
                .Replace("\"", "&quot;")
                .Replace("'", "&#39;");
    }
}
