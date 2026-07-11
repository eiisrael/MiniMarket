using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

/// <summary>
/// Logger global leve para registrar alteracoes, correcoes e eventos importantes do MiniMarket.
///
/// UpgradeLog.htm agora possui:
/// - filtros por hoje/semana/mes/periodo customizado;
/// - areas por categoria: Camera, Objeto/Grabber, Movimento, UI, Fisica, Runtime, Unity etc;
/// - selecao individual de logs;
/// - limpar selecionados, limpar visiveis, apagar tudo, restaurar e baixar HTML limpo;
/// - limpeza nao bloqueante via localStorage no navegador, sem travar o Unity.
/// </summary>
[DefaultExecutionOrder(-20000)]
public class UpgradeLogger : MonoBehaviour
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

    private static UpgradeLogger instancia;
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
        instancia = go.AddComponent<UpgradeLogger>();
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
            Debug.Log("[UpgradeLogger] " + (string.IsNullOrEmpty(caminhoProjeto) ? caminhoPersistent : caminhoProjeto));
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
<html lang='pt-BR'>
<head>
<meta charset='utf-8'><meta name='viewport' content='width=device-width, initial-scale=1'>
<title>MiniMarket Upgrade Log</title>
<style>
:root{--bg:#060a12;--panel:#101827;--panel2:#172033;--line:#2e3b55;--txt:#e8eefc;--muted:#9fb0cf;--yellow:#ffd34d;--green:#36d399;--cyan:#43d9ff;--purple:#b892ff;--red:#ff5d68;--orange:#ffb454;--blue:#60a5fa}*{box-sizing:border-box}body{margin:0;background:radial-gradient(circle at top left,#1a2b45 0,#070b14 42%,#05070c 100%);color:var(--txt);font-family:Segoe UI,Arial,sans-serif}header{padding:26px 34px 18px;border-bottom:1px solid var(--line);background:linear-gradient(135deg,rgba(16,24,39,.97),rgba(9,13,24,.96));position:sticky;top:0;z-index:20;backdrop-filter:blur(10px)}h1{margin:0;color:var(--yellow);font-size:30px}.subtitle{margin-top:8px;color:var(--muted);font-size:14px}.dashboard{display:grid;grid-template-columns:repeat(auto-fit,minmax(170px,1fr));gap:12px;margin-top:18px}.card{background:linear-gradient(180deg,var(--panel),#0b1220);border:1px solid var(--line);border-radius:16px;padding:14px 16px;box-shadow:0 14px 28px rgba(0,0,0,.25)}.card b{display:block;font-size:12px;color:var(--muted);text-transform:uppercase;letter-spacing:.08em}.card span{display:block;margin-top:7px;font-size:21px;color:#fff}.toolbar{padding:18px 34px;border-bottom:1px solid var(--line);background:rgba(6,10,18,.72);position:sticky;top:146px;z-index:15;backdrop-filter:blur(10px)}.row{display:flex;gap:10px;flex-wrap:wrap;align-items:center}.field{background:#08111f;border:1px solid #334155;color:#e8eefc;border-radius:12px;padding:10px 12px;min-height:40px}.search{min-width:280px;flex:1}button,.chip{border:1px solid #3b4a67;background:#0a1020;color:#dbeafe;border-radius:999px;padding:9px 13px;cursor:pointer}button:hover,.chip:hover{border-color:var(--cyan)}button.danger{border-color:#7f1d1d;background:#2b1014;color:#ffd5d9}.chip.active{background:#17324c;border-color:var(--cyan);color:white}.hint{margin:14px 34px 0;padding:12px 16px;border:1px solid #365475;border-radius:14px;background:rgba(67,217,255,.08);color:#cfeeff}.areas{padding:16px 34px 0}.areas h2{font-size:16px;margin:0 0 10px;color:#fff}.group-title{margin:18px 0 10px;color:#ffd34d;font-size:15px;text-transform:uppercase;letter-spacing:.08em}.entry{position:relative;background:linear-gradient(180deg,var(--panel2),var(--panel));border:1px solid var(--line);border-left:6px solid var(--cyan);border-radius:16px;margin:0 0 16px;padding:17px 20px 17px 54px;box-shadow:0 12px 26px rgba(0,0,0,.26)}.entry.hidden-log{display:none!important}.entry.filtered-out{display:none!important}.entry:before{content:'';position:absolute;left:-10px;top:22px;width:14px;height:14px;border-radius:50%;background:var(--cyan);box-shadow:0 0 18px var(--cyan)}.pick{position:absolute;left:18px;top:20px;transform:scale(1.25)}.entry.warn{border-left-color:var(--orange)}.entry.warn:before{background:var(--orange);box-shadow:0 0 18px var(--orange)}.entry.error{border-left-color:var(--red)}.entry.error:before{background:var(--red);box-shadow:0 0 18px var(--red)}.entry.upgrade{border-left-color:var(--green)}.entry.upgrade:before{background:var(--green);box-shadow:0 0 18px var(--green)}.entry.camera{border-left-color:var(--purple)}.entry.camera:before{background:var(--purple);box-shadow:0 0 18px var(--purple)}.entry.grabber,.entry.object{border-left-color:var(--cyan)}.entry.movement{border-left-color:var(--blue)}.entry.physics{border-left-color:#22c55e}.entry.ui,.entry.menu{border-left-color:#f472b6}.entry.runtime,.entry.system{border-left-color:var(--yellow)}.meta{display:flex;gap:9px;flex-wrap:wrap;margin-bottom:9px}.tag{font-size:12px;border:1px solid #3b4a67;background:#0a1020;color:#c8d5f2;border-radius:999px;padding:4px 10px}.title{font-size:18px;font-weight:800;color:white;margin-bottom:8px}.details{white-space:pre-wrap;line-height:1.52;color:#d9e2f6;font-size:14px}main{padding:18px 34px 90px}.empty{display:none;margin:22px 0;padding:22px;border:1px dashed #475569;border-radius:16px;color:#cbd5e1;text-align:center}.sep{height:1px;background:#263348;margin:12px 0}.small{font-size:12px;color:var(--muted)}
</style>
</head>
<body>
<header><h1>MiniMarket Upgrade Log</h1><div class='subtitle'>Registro profissional com filtros por data/semana/mês, áreas por tipo, seleção e limpeza de logs.</div><section class='dashboard'><div class='card'><b>Total</b><span id='statTotal'>0</span></div><div class='card'><b>Visíveis</b><span id='statVisible'>0</span></div><div class='card'><b>Ocultos</b><span id='statHidden'>0</span></div><div class='card'><b>Selecionados</b><span id='statSelected'>0</span></div></section></header>
<section class='toolbar'>
<div class='row'><input id='searchBox' class='field search' placeholder='Buscar por título, detalhe, categoria...'><select id='periodFilter' class='field'><option value='all'>Todos</option><option value='today'>Hoje</option><option value='week'>Últimos 7 dias</option><option value='month'>Este mês</option><option value='custom'>Período manual</option></select><input id='dateStart' class='field' type='date'><input id='dateEnd' class='field' type='date'><button id='selectVisible'>Selecionar visíveis</button><button id='clearSelection'>Desmarcar</button></div>
<div class='row' style='margin-top:10px'><button class='danger' id='deleteSelected'>Limpar selecionados</button><button class='danger' id='deleteVisible'>Limpar visíveis</button><button class='danger' id='deleteAll'>Apagar tudo</button><button id='restoreHidden'>Restaurar ocultos</button><button id='downloadClean'>Baixar HTML limpo</button></div>
<div class='small' style='margin-top:8px'>A limpeza oculta os logs no navegador via localStorage. Para substituir o arquivo físico, use “Baixar HTML limpo” e salve por cima do UpgradeLog.htm.</div>
</section>
<div class='hint'>Use as áreas abaixo para separar Camera, Objeto/Grabber, Movimento, Física, UI/Menu, Runtime/Sistema, Unity/Warnings e outros.</div>
<section class='areas'><h2>Áreas de log</h2><div id='areaButtons' class='row'></div><div class='sep'></div><h2>Tipos/severidade</h2><div id='typeButtons' class='row'></div></section>
<main id='entries'>
<!-- LOG_ENTRIES -->
<div id='emptyState' class='empty'>Nenhum log encontrado para o filtro atual.</div>
</main>
<script>
(function(){
const LS='minimarket.upgradelog.hidden.v3';
const state={area:'all',type:'all',hidden:new Set(JSON.parse(localStorage.getItem(LS)||'[]'))};
const q=s=>document.querySelector(s); const qa=s=>Array.from(document.querySelectorAll(s));
function slug(s){return (s||'geral').toString().normalize('NFD').replace(/[\u0300-\u036f]/g,'').toLowerCase().replace(/[^a-z0-9]+/g,'-').replace(/^-|-$/g,'')||'geral'}
function parseDate(s){if(!s)return null; const d=new Date(s.replace(' ','T')); return isNaN(d.getTime())?null:d}
function labelArea(c){const k=slug(c); if(k.includes('camera'))return 'Camera'; if(k.includes('grab')||k.includes('item')||k.includes('objeto'))return 'Objeto/Grabber'; if(k.includes('move')||k.includes('mov'))return 'Movimentação'; if(k.includes('fis')||k.includes('phys'))return 'Física'; if(k.includes('ui')||k.includes('menu')||k.includes('hud'))return 'UI/Menu/HUD'; if(k.includes('runtime')||k.includes('sistema')||k.includes('system'))return 'Runtime/Sistema'; if(k.includes('unity')||k.includes('warning')||k.includes('error'))return 'Unity'; return c||'Geral'}
function initEntries(){qa('.entry').forEach((e,i)=>{const tags=qa.call?[]:[]; const spans=Array.from(e.querySelectorAll('.tag')).map(x=>x.textContent.trim()); const date=e.dataset.date||spans[0]||''; const category=e.dataset.category||spans[1]||'Geral'; const type=e.dataset.type||spans[2]||'Log'; e.dataset.date=date; e.dataset.category=category; e.dataset.area=labelArea(category); e.dataset.type=type; e.dataset.logId=e.dataset.logId||slug(date+'-'+category+'-'+type+'-'+(e.querySelector('.title')?.textContent||'log')+'-'+i); if(!e.querySelector('.pick')){const cb=document.createElement('input'); cb.type='checkbox'; cb.className='pick'; cb.title='Selecionar log'; e.prepend(cb); cb.addEventListener('change',render)}})}
function buildChips(){const areas=['all',...new Set(qa('.entry').map(e=>e.dataset.area))]; const types=['all',...new Set(qa('.entry').map(e=>e.dataset.type))]; makeButtons('#areaButtons',areas,'area'); makeButtons('#typeButtons',types,'type')}
function makeButtons(sel,items,key){const wrap=q(sel); wrap.innerHTML=''; items.forEach(v=>{const b=document.createElement('button'); b.className='chip'+(state[key]===v?' active':''); b.textContent=v==='all'?'Todos':v; b.onclick=()=>{state[key]=v; buildChips(); render()}; wrap.appendChild(b)})}
function inPeriod(e){const mode=q('#periodFilter').value; if(mode==='all')return true; const d=parseDate(e.dataset.date); if(!d)return true; const now=new Date(); if(mode==='today')return d.toDateString()===now.toDateString(); if(mode==='week')return now-d<=7*24*60*60*1000; if(mode==='month')return d.getFullYear()===now.getFullYear()&&d.getMonth()===now.getMonth(); if(mode==='custom'){const s=q('#dateStart').value?new Date(q('#dateStart').value+'T00:00:00'):null; const en=q('#dateEnd').value?new Date(q('#dateEnd').value+'T23:59:59'):null; return (!s||d>=s)&&(!en||d<=en)} return true}
function matches(e){const text=(e.textContent||'').toLowerCase(); const search=q('#searchBox').value.trim().toLowerCase(); if(search&& !text.includes(search))return false; if(state.area!=='all'&&e.dataset.area!==state.area)return false; if(state.type!=='all'&&e.dataset.type!==state.type)return false; return inPeriod(e)}
function render(){let total=0,visible=0,hidden=0,selected=0; qa('.entry').forEach(e=>{total++; const isHidden=state.hidden.has(e.dataset.logId); const show=!isHidden&&matches(e); e.classList.toggle('hidden-log',isHidden); e.classList.toggle('filtered-out',!isHidden&&!show); if(show)visible++; if(isHidden)hidden++; if(e.querySelector('.pick')?.checked)selected++}); q('#statTotal').textContent=total; q('#statVisible').textContent=visible; q('#statHidden').textContent=hidden; q('#statSelected').textContent=selected; q('#emptyState').style.display=visible?'none':'block'}
function saveHidden(){localStorage.setItem(LS,JSON.stringify([...state.hidden]))}
function hideEntries(list){list.forEach(e=>{state.hidden.add(e.dataset.logId); const cb=e.querySelector('.pick'); if(cb)cb.checked=false}); saveHidden(); render()}
q('#searchBox').oninput=render; q('#periodFilter').onchange=render; q('#dateStart').onchange=render; q('#dateEnd').onchange=render;
q('#selectVisible').onclick=()=>{qa('.entry').forEach(e=>{if(!e.classList.contains('hidden-log')&&!e.classList.contains('filtered-out')){const cb=e.querySelector('.pick'); if(cb)cb.checked=true}});render()};
q('#clearSelection').onclick=()=>{qa('.pick').forEach(c=>c.checked=false);render()};
q('#deleteSelected').onclick=()=>hideEntries(qa('.entry').filter(e=>e.querySelector('.pick')?.checked));
q('#deleteVisible').onclick=()=>{if(confirm('Limpar todos os logs visíveis no filtro atual?'))hideEntries(qa('.entry').filter(e=>!e.classList.contains('hidden-log')&&!e.classList.contains('filtered-out')))};
q('#deleteAll').onclick=()=>{if(confirm('Apagar/ocultar todos os logs deste navegador?'))hideEntries(qa('.entry'))};
q('#restoreHidden').onclick=()=>{state.hidden.clear();saveHidden();render()};
q('#downloadClean').onclick=()=>{const clone=document.documentElement.cloneNode(true); const ids=new Set([...state.hidden]); clone.querySelectorAll('.entry').forEach(e=>{if(ids.has(e.dataset.logId)||e.classList.contains('filtered-out'))e.remove(); else e.querySelector('.pick')?.remove()}); const blob=new Blob(['<!doctype html>\n'+clone.outerHTML],{type:'text/html'}); const a=document.createElement('a'); a.href=URL.createObjectURL(blob); a.download='UpgradeLog_limpo.htm'; a.click(); setTimeout(()=>URL.revokeObjectURL(a.href),1000)};
initEntries(); buildChips(); render();
})();
</script>
</body>
</html>";
    }

    private string MontarHtmlEntradas(List<LogEntry> entries)
    {
        StringBuilder sb = new StringBuilder(entries.Count * 720);

        for (int i = 0; i < entries.Count; i++)
        {
            LogEntry e = entries[i];
            string css = "entry " + CssClass(e);
            string date = e.time.ToString("yyyy-MM-dd HH:mm:ss");
            string id = e.time.ToString("yyyyMMddHHmmssfff") + "-" + SafeId(e.category) + "-" + SafeId(e.title) + "-" + i.ToString();

            sb.Append("<section class=\"").Append(css).Append("\" data-log-id=\"").Append(Html(id)).Append("\" data-date=\"").Append(Html(date)).Append("\" data-category=\"").Append(Html(e.category)).Append("\" data-type=\"").Append(Html(e.type.ToString())).Append("\">\n");
            sb.Append("<div class=\"meta\"><span class=\"tag\">").Append(Html(date)).Append("</span><span class=\"tag\">").Append(Html(e.category)).Append("</span><span class=\"tag\">").Append(Html(e.type.ToString())).Append("</span></div>\n");
            sb.Append("<div class=\"title\">").Append(Html(e.title)).Append("</div>\n");
            if (!string.IsNullOrWhiteSpace(e.details))
                sb.Append("<div class=\"details\">").Append(Html(e.details)).Append("</div>\n");
            sb.Append("</section>\n");
        }

        return sb.ToString();
    }

    private string CssClass(LogEntry e)
    {
        string severity = string.Empty;
        if (e.type == LogType.Error || e.type == LogType.Exception)
            severity = "error ";
        else if (e.type == LogType.Warning)
            severity = "warn ";

        return severity + CategoriaCss(e.category);
    }

    private string CategoriaCss(string category)
    {
        string c = (category ?? string.Empty).ToLowerInvariant();
        if (c.Contains("camera")) return "camera";
        if (c.Contains("grab") || c.Contains("item") || c.Contains("objeto")) return "grabber object";
        if (c.Contains("move") || c.Contains("mov")) return "movement";
        if (c.Contains("fis") || c.Contains("phys")) return "physics";
        if (c.Contains("ui") || c.Contains("menu") || c.Contains("hud")) return "ui menu";
        if (c.Contains("runtime") || c.Contains("sistema") || c.Contains("system")) return "runtime system";
        if (c.Contains("unity")) return "unity";
        if (c.Contains("upgrade")) return "upgrade";
        if (c.Contains("log")) return "log";
        return "general";
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

    private string SafeId(string s)
    {
        if (string.IsNullOrEmpty(s))
            return "log";

        StringBuilder sb = new StringBuilder(s.Length);
        for (int i = 0; i < s.Length; i++)
        {
            char ch = char.ToLowerInvariant(s[i]);
            if ((ch >= 'a' && ch <= 'z') || (ch >= '0' && ch <= '9'))
                sb.Append(ch);
            else if (sb.Length == 0 || sb[sb.Length - 1] != '-')
                sb.Append('-');
        }

        return sb.Length == 0 ? "log" : sb.ToString().Trim('-');
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
