using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

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
/// - captura warnings/erros do Unity sem loop infinito;
/// - nao chama AssetDatabase.Refresh em loop para evitar travadas no Editor.
/// </summary>
[DefaultExecutionOrder(-20000)]
public class MiniMarketUpgradeLogger : MonoBehaviour
{
    [Header("Arquivo")]
    public string nomeArquivo = "UpgradeLog.htm";
    public bool escreverNaRaizDoProjetoNoEditor = true;
    public bool escreverTambemNoPersistentDataPath = true;

    [Header("Performance / Anti Spam")]
    [Min(0.5f)] public float intervaloFlush = 3f;
    [Min(1)] public int maxEntradasPorFlush = 25;
    [Min(10)] public int maxFila = 500;
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

        Log("Sistema", "UpgradeLogger iniciado", "Projeto: " + (string.IsNullOrEmpty(caminhoProjeto) ? "N/A" : caminhoProjeto) + "\nRuntime: " + caminhoPersistent, "logger-start", 2f);

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

        if ((type == LogType.Exception || type == LogType.Error) && !string.IsNullOrEmpty(stackTrace))
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
            while (fila.Count > maxFila)
                fila.Dequeue();

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
        return @"<!doctype html>
<html lang=""pt-BR"">
<head>
<meta charset=""utf-8""><meta name=""viewport"" content=""width=device-width, initial-scale=1"">
<title>MiniMarket Upgrade Log</title>
<style>
:root{--bg:#070b14;--panel:#101827;--panel2:#172033;--line:#2e3b55;--txt:#e8eefc;--muted:#9fb0cf;--yellow:#ffd34d;--green:#36d399;--cyan:#43d9ff;--purple:#b892ff;--red:#ff5d68;--orange:#ffb454}*{box-sizing:border-box}body{margin:0;background:radial-gradient(circle at top left,#1a2b45 0,#070b14 42%,#05070c 100%);color:var(--txt);font-family:Segoe UI,Arial,sans-serif}header{padding:30px 34px;border-bottom:1px solid var(--line);background:linear-gradient(135deg,rgba(16,24,39,.96),rgba(9,13,24,.94));position:sticky;top:0;z-index:2;backdrop-filter:blur(10px)}h1{margin:0;color:var(--yellow);font-size:30px;letter-spacing:.3px}.subtitle{margin-top:8px;color:var(--muted);font-size:14px}.dashboard{display:grid;grid-template-columns:repeat(auto-fit,minmax(190px,1fr));gap:14px;padding:22px 34px 0}.card{background:linear-gradient(180deg,var(--panel),#0b1220);border:1px solid var(--line);border-radius:16px;padding:16px 18px;box-shadow:0 14px 28px rgba(0,0,0,.28)}.card b{display:block;font-size:13px;color:var(--muted);text-transform:uppercase;letter-spacing:.08em}.card span{display:block;margin-top:8px;font-size:21px;color:#fff}main{padding:22px 34px 90px}.entry{position:relative;background:linear-gradient(180deg,var(--panel2),var(--panel));border:1px solid var(--line);border-left:6px solid var(--cyan);border-radius:16px;margin:0 0 16px;padding:17px 20px;box-shadow:0 12px 26px rgba(0,0,0,.26)}.entry:before{content:"";position:absolute;left:-10px;top:22px;width:14px;height:14px;border-radius:50%;background:var(--cyan);box-shadow:0 0 18px var(--cyan)}.entry.warn{border-left-color:var(--orange)}.entry.warn:before{background:var(--orange);box-shadow:0 0 18px var(--orange)}.entry.error{border-left-color:var(--red)}.entry.error:before{background:var(--red);box-shadow:0 0 18px var(--red)}.entry.upgrade{border-left-color:var(--green)}.entry.upgrade:before{background:var(--green);box-shadow:0 0 18px var(--green)}.entry.camera{border-left-color:var(--purple)}.entry.camera:before{background:var(--purple);box-shadow:0 0 18px var(--purple)}.entry.grabber{border-left-color:var(--cyan)}.entry.log{border-left-color:var(--yellow)}.entry.log:before{background:var(--yellow);box-shadow:0 0 18px var(--yellow)}.meta{display:flex;gap:9px;flex-wrap:wrap;margin-bottom:9px}.tag{font-size:12px;border:1px solid #3b4a67;background:#0a1020;color:#c8d5f2;border-radius:999px;padding:4px 10px}.title{font-size:18px;font-weight:800;color:white;margin-bottom:8px}.details{white-space:pre-wrap;line-height:1.52;color:#d9e2f6;font-size:14px}code{color:var(--yellow)}.hint{margin:18px 34px 0;padding:14px 18px;border:1px solid #365475;border-radius:14px;background:rgba(67,217,255,.08);color:#cfeeff}
</style>
</head>
<body>
<header><h1>MiniMarket Upgrade Log</h1><div class=""subtitle"">Registro profissional de alterações, diagnósticos, correções, warnings e eventos importantes do projeto.</div></header>
<section class=""dashboard""><div class=""card""><b>Status</b><span>Ativo</span></div><div class=""card""><b>Anti-spam</b><span>Rate limit</span></div><div class=""card""><b>Arquivo</b><span>UpgradeLog.htm</span></div><div class=""card""><b>Uso</b><span>Diagnóstico</span></div></section>
<div class=""hint"">Abra este arquivo no navegador para revisar o histórico. O logger grava em lote para evitar lag e não usa Refresh do AssetDatabase em loop.</div>
<main>
<!-- LOG_ENTRIES -->
</main>
</body>
</html>
";
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
        if (c.Contains("log")) return "log";
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
