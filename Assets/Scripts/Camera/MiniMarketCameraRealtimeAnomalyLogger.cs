using System;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Log exclusivo em tempo real para diagnosticar camera.
///
/// O que faz:
/// - monitora Main Camera em LateUpdate;
/// - registra amostras leves em CameraRealtimeLog.htm;
/// - detecta variações bruscas de posição, rotação, FOV e distância do alvo;
/// - quando detecta uma mudança brusca, salva o log imediatamente, pausa o jogo e mostra uma MessageBox detalhada no Editor.
///
/// Arquivos:
/// - CameraRealtimeLog.htm na raiz do projeto, no Editor;
/// - Application.persistentDataPath/CameraRealtimeLog.htm como fallback/runtime.
///
/// Segurança/performance:
/// - não usa Find em loop curto;
/// - não usa AssetDatabase.Refresh;
/// - limita spam por cooldown;
/// - salva incidente imediatamente.
/// </summary>
[DefaultExecutionOrder(33000)]
public class MiniMarketCameraRealtimeAnomalyLogger : MonoBehaviour
{
    [Header("Camera")]
    public Camera cameraMonitorada;
    public CameraGTAFollowHardcore cameraGTA;
    public Transform alvo;
    public bool procurarAutomaticamente = true;
    [Min(0.2f)] public float intervaloBuscaCamera = 1f;

    [Header("Arquivo")]
    public string nomeArquivo = "CameraRealtimeLog.htm";
    public bool escreverNaRaizDoProjetoNoEditor = true;
    public bool escreverNoPersistentDataPath = true;

    [Header("Amostras em Tempo Real")]
    public bool registrarAmostrasLeves = true;
    [Min(0.05f)] public float intervaloAmostraLeve = 0.25f;
    [Min(1)] public int maxAmostrasPorFlush = 12;
    [Min(0.2f)] public float intervaloFlush = 1f;

    [Header("Detecção - Mudança Brusca")]
    public bool detectarMudancaBrusca = true;

    [Tooltip("Distância máxima que a câmera pode saltar em um frame antes de pausar.")]
    [Min(0.01f)] public float limiteSaltoPosicaoFrame = 0.75f;

    [Tooltip("Velocidade brusca da câmera em metros/segundo.")]
    [Min(0.1f)] public float limiteVelocidadeCamera = 18f;

    [Tooltip("Aceleração brusca da câmera em metros/segundo².")]
    [Min(1f)] public float limiteAceleracaoCamera = 140f;

    [Tooltip("Graus máximos que a câmera pode girar em um frame antes de pausar.")]
    [Min(0.1f)] public float limiteSaltoRotacaoFrame = 14f;

    [Tooltip("Velocidade angular brusca em graus/segundo.")]
    [Min(1f)] public float limiteVelocidadeAngular = 220f;

    [Tooltip("Mudança brusca no FOV em um frame.")]
    [Min(0.1f)] public float limiteSaltoFovFrame = 4f;

    [Tooltip("Mudança brusca na distância entre câmera e alvo em um frame.")]
    [Min(0.05f)] public float limiteSaltoDistanciaAlvo = 0.75f;

    [Tooltip("Ignora a detecção durante a transição normal de primeira pessoa para não pausar falsamente.")]
    public bool ignorarDuranteTransicaoPrimeiraPessoa = true;

    [Tooltip("Tempo de tolerância logo após entrar/sair da primeira pessoa.")]
    [Min(0f)] public float toleranciaAposTrocaPrimeiraPessoa = 0.18f;

    [Header("Pausar / MessageBox")]
    public bool pausarAoDetectar = true;
    public bool mostrarMessageBoxNoEditor = true;
    public bool usarTimeScaleZeroAoPausar = true;
    [Min(0.1f)] public float cooldownEntreIncidentes = 1.5f;

    [Header("Debug")]
    public bool logarNoConsole = true;

    private static MiniMarketCameraRealtimeAnomalyLogger instancia;

    private string caminhoProjeto;
    private string caminhoPersistent;
    private bool inicializado;

    private Vector3 ultimaPosicao;
    private Quaternion ultimaRotacao;
    private float ultimoFov;
    private float ultimaDistanciaAlvo;
    private float ultimaVelocidade;
    private bool possuiUltimaAmostra;
    private bool ultimoEstadoPrimeiraPessoa;
    private float ultimoTempoTrocaPrimeiraPessoa;
    private float ultimoIncidente;
    private float proximaBuscaCamera;
    private float proximaAmostraLeve;
    private float proximoFlush;

    private readonly StringBuilder filaHtml = new StringBuilder(8192);
    private readonly StringBuilder mensagem = new StringBuilder(2048);
    private int amostrasFila;
    private bool dialogoPendente;
    private string tituloDialogoPendente;
    private string mensagemDialogoPendente;

    private FieldInfo campoYaw;
    private FieldInfo campoPitch;
    private FieldInfo campoPrimeiraPessoaBlend;
    private FieldInfo campoPositionVelocity;
    private FieldInfo campoMouseSuavizado;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void CriarAutomaticamente()
    {
        if (instancia != null)
            return;

        GameObject go = new GameObject("MiniMarket_CameraRealtimeAnomalyLogger");
        DontDestroyOnLoad(go);
        instancia = go.AddComponent<MiniMarketCameraRealtimeAnomalyLogger>();
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
        InicializarArquivo();
        ResolverCamera(true);
        EscreverEvento("Sistema", "Camera realtime logger iniciado", "Monitoramento em tempo real da câmera iniciado. Incidentes pausam o Play e registram detalhes.", "system");
        Flush(true);
    }

    private void OnApplicationQuit()
    {
        EscreverEvento("Sistema", "Aplicação finalizada", "Flush final do CameraRealtimeLog.", "system");
        Flush(true);
    }

    private void LateUpdate()
    {
        if (!Application.isPlaying)
            return;

        InicializarArquivo();

        if (procurarAutomaticamente && Time.unscaledTime >= proximaBuscaCamera)
        {
            proximaBuscaCamera = Time.unscaledTime + intervaloBuscaCamera;
            ResolverCamera(false);
        }

        if (cameraMonitorada == null)
            return;

        AtualizarEstadoPrimeiraPessoa();
        AnalisarCamera();

        if (registrarAmostrasLeves && Time.unscaledTime >= proximaAmostraLeve)
        {
            proximaAmostraLeve = Time.unscaledTime + intervaloAmostraLeve;
            RegistrarAmostraLeve();
        }

        if (Time.unscaledTime >= proximoFlush || amostrasFila >= maxAmostrasPorFlush)
        {
            proximoFlush = Time.unscaledTime + intervaloFlush;
            Flush(false);
        }

        MostrarDialogoPendenteSeNecessario();
    }

    private void InicializarArquivo()
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
    }

    private void ResolverCamera(bool forcar)
    {
        if (!forcar && cameraMonitorada != null && cameraGTA != null)
            return;

        if (cameraMonitorada == null)
            cameraMonitorada = Camera.main;

        if (cameraMonitorada == null)
            cameraMonitorada = UnityEngine.Object.FindFirstObjectByType<Camera>(FindObjectsInactive.Include);

        if (cameraMonitorada != null && cameraGTA == null)
            cameraGTA = cameraMonitorada.GetComponent<CameraGTAFollowHardcore>();

        if (cameraGTA == null)
            cameraGTA = UnityEngine.Object.FindFirstObjectByType<CameraGTAFollowHardcore>(FindObjectsInactive.Include);

        if (cameraGTA != null)
        {
            if (cameraMonitorada == null)
                cameraMonitorada = cameraGTA.GetComponent<Camera>();

            if (alvo == null)
                alvo = cameraGTA.target;

            PrepararReflection();
        }

        if (cameraMonitorada != null && !possuiUltimaAmostra)
            CapturarBaseline();
    }

    private void PrepararReflection()
    {
        if (cameraGTA == null)
            return;

        Type tipo = typeof(CameraGTAFollowHardcore);
        campoYaw = tipo.GetField("yaw", BindingFlags.Instance | BindingFlags.NonPublic);
        campoPitch = tipo.GetField("pitch", BindingFlags.Instance | BindingFlags.NonPublic);
        campoPrimeiraPessoaBlend = tipo.GetField("primeiraPessoaBlend", BindingFlags.Instance | BindingFlags.NonPublic);
        campoPositionVelocity = tipo.GetField("positionVelocity", BindingFlags.Instance | BindingFlags.NonPublic);
        campoMouseSuavizado = tipo.GetField("mouseSuavizado", BindingFlags.Instance | BindingFlags.NonPublic);
    }

    private void CapturarBaseline()
    {
        ultimaPosicao = cameraMonitorada.transform.position;
        ultimaRotacao = cameraMonitorada.transform.rotation;
        ultimoFov = cameraMonitorada.fieldOfView;
        ultimaDistanciaAlvo = CalcularDistanciaAlvo(ultimaPosicao);
        ultimaVelocidade = 0f;
        possuiUltimaAmostra = true;
    }

    private void AtualizarEstadoPrimeiraPessoa()
    {
        if (cameraGTA == null)
            return;

        bool estado = cameraGTA.EstaEmPrimeiraPessoa;
        if (estado != ultimoEstadoPrimeiraPessoa)
        {
            ultimoEstadoPrimeiraPessoa = estado;
            ultimoTempoTrocaPrimeiraPessoa = Time.unscaledTime;
            EscreverEvento("Camera", estado ? "Entrou em primeira pessoa" : "Saiu da primeira pessoa", MontarResumoEstadoCamera(), "camera");
        }
    }

    private void AnalisarCamera()
    {
        if (!detectarMudancaBrusca || cameraMonitorada == null)
            return;

        if (!possuiUltimaAmostra)
        {
            CapturarBaseline();
            return;
        }

        float dt = Mathf.Max(Time.unscaledDeltaTime, 0.0001f);
        Vector3 pos = cameraMonitorada.transform.position;
        Quaternion rot = cameraMonitorada.transform.rotation;
        float fov = cameraMonitorada.fieldOfView;
        float distanciaAlvo = CalcularDistanciaAlvo(pos);

        float deltaPos = Vector3.Distance(pos, ultimaPosicao);
        float velocidade = deltaPos / dt;
        float aceleracao = Mathf.Abs(velocidade - ultimaVelocidade) / dt;
        float deltaRot = Quaternion.Angle(ultimaRotacao, rot);
        float velocidadeAngular = deltaRot / dt;
        float deltaFov = Mathf.Abs(fov - ultimoFov);
        float deltaDistanciaAlvo = Mathf.Abs(distanciaAlvo - ultimaDistanciaAlvo);

        bool dentroToleranciaPrimeiraPessoa = ignorarDuranteTransicaoPrimeiraPessoa && cameraGTA != null &&
            (cameraGTA.EstaTransicionandoPrimeiraPessoa || Time.unscaledTime - ultimoTempoTrocaPrimeiraPessoa <= toleranciaAposTrocaPrimeiraPessoa);

        bool incidente = false;
        string causa = string.Empty;

        if (!dentroToleranciaPrimeiraPessoa)
        {
            if (deltaPos >= limiteSaltoPosicaoFrame)
            {
                incidente = true;
                causa = "Salto brusco de posição da câmera em um único frame";
            }
            else if (velocidade >= limiteVelocidadeCamera)
            {
                incidente = true;
                causa = "Velocidade brusca da câmera acima do limite";
            }
            else if (aceleracao >= limiteAceleracaoCamera)
            {
                incidente = true;
                causa = "Aceleração brusca da câmera acima do limite";
            }
            else if (deltaRot >= limiteSaltoRotacaoFrame)
            {
                incidente = true;
                causa = "Salto brusco de rotação da câmera em um único frame";
            }
            else if (velocidadeAngular >= limiteVelocidadeAngular)
            {
                incidente = true;
                causa = "Velocidade angular brusca da câmera acima do limite";
            }
            else if (deltaFov >= limiteSaltoFovFrame)
            {
                incidente = true;
                causa = "Mudança brusca de FOV";
            }
            else if (deltaDistanciaAlvo >= limiteSaltoDistanciaAlvo)
            {
                incidente = true;
                causa = "Mudança brusca na distância entre câmera e alvo";
            }
        }

        if (incidente && Time.unscaledTime - ultimoIncidente >= cooldownEntreIncidentes)
        {
            ultimoIncidente = Time.unscaledTime;
            RegistrarIncidente(causa, deltaPos, velocidade, aceleracao, deltaRot, velocidadeAngular, deltaFov, deltaDistanciaAlvo, dt);
        }

        ultimaPosicao = pos;
        ultimaRotacao = rot;
        ultimoFov = fov;
        ultimaDistanciaAlvo = distanciaAlvo;
        ultimaVelocidade = velocidade;
    }

    private void RegistrarAmostraLeve()
    {
        string detalhes = MontarResumoEstadoCamera();
        EscreverEvento("Sample", "Amostra câmera", detalhes, "sample");
    }

    private void RegistrarIncidente(string causa, float deltaPos, float velocidade, float aceleracao, float deltaRot, float velocidadeAngular, float deltaFov, float deltaDistanciaAlvo, float dt)
    {
        string inferencia = InferirCausaProvavel(causa, deltaPos, deltaRot, deltaDistanciaAlvo);

        mensagem.Length = 0;
        mensagem.AppendLine("O Play foi pausado por mudança brusca detectada na câmera.");
        mensagem.AppendLine();
        mensagem.AppendLine("Causa técnica detectada: " + causa);
        mensagem.AppendLine("Causa provável: " + inferencia);
        mensagem.AppendLine();
        mensagem.AppendLine("Métricas do frame:");
        mensagem.AppendLine("Delta Time: " + dt.ToString("0.0000") + "s");
        mensagem.AppendLine("Delta Posição: " + deltaPos.ToString("0.000") + " m");
        mensagem.AppendLine("Velocidade Câmera: " + velocidade.ToString("0.00") + " m/s");
        mensagem.AppendLine("Aceleração Câmera: " + aceleracao.ToString("0.00") + " m/s²");
        mensagem.AppendLine("Delta Rotação: " + deltaRot.ToString("0.00") + "°");
        mensagem.AppendLine("Velocidade Angular: " + velocidadeAngular.ToString("0.00") + " °/s");
        mensagem.AppendLine("Delta FOV: " + deltaFov.ToString("0.00"));
        mensagem.AppendLine("Delta Distância Alvo: " + deltaDistanciaAlvo.ToString("0.000") + " m");
        mensagem.AppendLine();
        mensagem.AppendLine(MontarResumoEstadoCamera());
        mensagem.AppendLine();
        mensagem.AppendLine("Arquivo salvo: " + ObterCaminhoPreferencial());

        EscreverEvento("INCIDENTE", "MUDANÇA BRUSCA DETECTADA - PLAY PAUSADO", mensagem.ToString(), "incident");
        Flush(true);

        if (logarNoConsole)
            Debug.LogWarning("[CameraRealtimeLog] Mudança brusca detectada. Play pausado. " + causa + " | " + inferencia);

        if (pausarAoDetectar)
            PausarJogoComDialogo("MiniMarket Camera Realtime Log", mensagem.ToString());
    }

    private string InferirCausaProvavel(string causa, float deltaPos, float deltaRot, float deltaDistanciaAlvo)
    {
        bool inputMovimento = Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.D);
        bool inputMovimentoLateralOuTras = Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.D);
        bool botaoDireito = Input.GetMouseButton(1);
        bool primeiraPessoa = cameraGTA != null && cameraGTA.EstaEmPrimeiraPessoa;
        bool transicionando = cameraGTA != null && cameraGTA.EstaTransicionandoPrimeiraPessoa;
        bool pertoParede = CameraSobrepostaOuPertoDeParede();
        bool mouseParado = Mathf.Abs(Input.GetAxisRaw("Mouse X")) < 0.001f && Mathf.Abs(Input.GetAxisRaw("Mouse Y")) < 0.001f;

        if (transicionando)
            return "Transição de primeira pessoa/terceira pessoa ainda ativa. Verificar velocidade de transição e troca pelo botão direito.";

        if (pertoParede && deltaPos > 0.05f)
            return "Colisão/anti-parede da câmera ou objeto próximo empurrando a câmera. Verificar Camera Collision Layers, radius e objetos seguráveis.";

        if (inputMovimentoLateralOuTras && deltaPos > 0.03f)
            return "Movimento do personagem com A/S/D gerou deslocamento abrupto no ponto de foco. Verificar suavização/seguimento do alvo e animação do CharacterController.";

        if (primeiraPessoa && botaoDireito && deltaRot > 0.1f && mouseParado)
            return "Possível rotação residual/recenter na primeira pessoa mesmo com mouse parado. Verificar scripts que alteram rotação do player/camera após LateUpdate.";

        if (inputMovimento)
            return "Movimento do personagem ativo no frame. Verificar PlayerMove, rotação do corpo e ponto de foco da câmera.";

        if (causa.Contains("FOV"))
            return "Alteração de FOV por zoom/mira/transição. Verificar velocidadeFovMira e estados de primeira pessoa.";

        return "Causa não conclusiva. Use os valores de yaw/pitch/blend/posição no log para identificar o script que alterou a câmera.";
    }

    private bool CameraSobrepostaOuPertoDeParede()
    {
        if (cameraMonitorada == null)
            return false;

        int count = Physics.OverlapSphereNonAlloc(cameraMonitorada.transform.position, 0.35f, _overlaps, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
        {
            Collider col = _overlaps[i];
            if (col == null || col.isTrigger)
                continue;

            if (alvo != null && (col.transform == alvo || col.transform.IsChildOf(alvo)))
                continue;

            if (col.GetComponentInParent<GrabbableObjectHardcore>() != null)
                continue;

            return true;
        }

        return false;
    }

    private readonly Collider[] _overlaps = new Collider[16];

    private float CalcularDistanciaAlvo(Vector3 cameraPos)
    {
        if (alvo == null)
        {
            if (cameraGTA != null)
                alvo = cameraGTA.target;
        }

        if (alvo == null)
            return 0f;

        return Vector3.Distance(cameraPos, alvo.position);
    }

    private string MontarResumoEstadoCamera()
    {
        if (cameraMonitorada == null)
            return "Camera: null";

        Transform cam = cameraMonitorada.transform;
        Vector3 pos = cam.position;
        Vector3 euler = cam.eulerAngles;

        mensagem.Length = 0;
        mensagem.AppendLine("Frame: " + Time.frameCount + " | Time: " + Time.time.ToString("0.000") + " | Unscaled: " + Time.unscaledTime.ToString("0.000"));
        mensagem.AppendLine("Camera: " + cameraMonitorada.name);
        mensagem.AppendLine("Pos: " + Vec(pos) + " | Rot Euler: " + Vec(euler) + " | FOV: " + cameraMonitorada.fieldOfView.ToString("0.00"));
        mensagem.AppendLine("Alvo: " + (alvo != null ? alvo.name : "null") + " | Distância Alvo: " + CalcularDistanciaAlvo(pos).ToString("0.000"));
        mensagem.AppendLine("Input: W=" + Input.GetKey(KeyCode.W) + " A=" + Input.GetKey(KeyCode.A) + " S=" + Input.GetKey(KeyCode.S) + " D=" + Input.GetKey(KeyCode.D) + " RMB=" + Input.GetMouseButton(1) + " LMB=" + Input.GetMouseButton(0));
        mensagem.AppendLine("Mouse Raw: X=" + Input.GetAxisRaw("Mouse X").ToString("0.000") + " Y=" + Input.GetAxisRaw("Mouse Y").ToString("0.000"));

        if (cameraGTA != null)
        {
            mensagem.AppendLine("CameraGTA: FP=" + cameraGTA.EstaEmPrimeiraPessoa + " | Transição=" + cameraGTA.EstaTransicionandoPrimeiraPessoa + " | Yaw=" + ValorFloat(campoYaw) + " | Pitch=" + ValorFloat(campoPitch) + " | Blend=" + ValorFloat(campoPrimeiraPessoaBlend));
            mensagem.AppendLine("Smooth: Pos=" + cameraGTA.positionSmoothTime.ToString("0.###") + " Rot=" + cameraGTA.rotationSmoothTime.ToString("0.###") + " MouseFP=" + cameraGTA.mouseSmoothTimePrimeiraPessoa.ToString("0.###") + " AutoAlign=" + cameraGTA.autoAlignBehindPlayer);
            mensagem.AppendLine("VelocityInterna=" + ValorVector3(campoPositionVelocity) + " | MouseSuavizado=" + ValorVector2(campoMouseSuavizado));
        }

        mensagem.AppendLine("FPS aprox: " + (Time.unscaledDeltaTime > 0.0001f ? (1f / Time.unscaledDeltaTime).ToString("0") : "N/A"));
        return mensagem.ToString();
    }

    private string ValorFloat(FieldInfo field)
    {
        if (cameraGTA == null || field == null)
            return "N/A";

        object valor = field.GetValue(cameraGTA);
        if (valor is float f)
            return f.ToString("0.000");

        return "N/A";
    }

    private string ValorVector3(FieldInfo field)
    {
        if (cameraGTA == null || field == null)
            return "N/A";

        object valor = field.GetValue(cameraGTA);
        if (valor is Vector3 v)
            return Vec(v);

        return "N/A";
    }

    private string ValorVector2(FieldInfo field)
    {
        if (cameraGTA == null || field == null)
            return "N/A";

        object valor = field.GetValue(cameraGTA);
        if (valor is Vector2 v)
            return "(" + v.x.ToString("0.000") + ", " + v.y.ToString("0.000") + ")";

        return "N/A";
    }

    private string Vec(Vector3 v)
    {
        return "(" + v.x.ToString("0.000") + ", " + v.y.ToString("0.000") + ", " + v.z.ToString("0.000") + ")";
    }

    private void PausarJogoComDialogo(string titulo, string texto)
    {
        if (usarTimeScaleZeroAoPausar)
            Time.timeScale = 0f;

#if UNITY_EDITOR
        EditorApplication.isPaused = true;
#endif

        tituloDialogoPendente = titulo;
        mensagemDialogoPendente = texto;
        dialogoPendente = true;
    }

    private void MostrarDialogoPendenteSeNecessario()
    {
        if (!dialogoPendente)
            return;

        dialogoPendente = false;

#if UNITY_EDITOR
        if (mostrarMessageBoxNoEditor)
        {
            string titulo = tituloDialogoPendente;
            string texto = mensagemDialogoPendente;
            EditorApplication.delayCall += () =>
            {
                EditorUtility.DisplayDialog(titulo, texto, "OK");
            };
        }
#else
        Debug.LogWarning(mensagemDialogoPendente);
#endif
    }

    private void EscreverEvento(string categoria, string titulo, string detalhes, string css)
    {
        InicializarArquivo();

        filaHtml.Append("<section class=\"entry ").Append(Html(css)).Append("\">\n");
        filaHtml.Append("<div class=\"meta\"><span>").Append(Html(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"))).Append("</span><span>").Append(Html(categoria)).Append("</span><span>frame ").Append(Time.frameCount).Append("</span></div>\n");
        filaHtml.Append("<h2>").Append(Html(titulo)).Append("</h2>\n");
        filaHtml.Append("<pre>").Append(Html(detalhes)).Append("</pre>\n");
        filaHtml.Append("</section>\n");
        amostrasFila++;
    }

    private void Flush(bool forcar)
    {
        if (!inicializado || filaHtml.Length == 0)
            return;

        string html = filaHtml.ToString();
        filaHtml.Length = 0;
        amostrasFila = 0;

#if UNITY_EDITOR
        if (escreverNaRaizDoProjetoNoEditor && !string.IsNullOrEmpty(caminhoProjeto))
            AcrescentarNoArquivo(caminhoProjeto, html);
#endif

        if (escreverNoPersistentDataPath)
            AcrescentarNoArquivo(caminhoPersistent, html);
    }

    private string ObterCaminhoPreferencial()
    {
#if UNITY_EDITOR
        if (escreverNaRaizDoProjetoNoEditor && !string.IsNullOrEmpty(caminhoProjeto))
            return caminhoProjeto;
#endif
        return caminhoPersistent;
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

    private void AcrescentarNoArquivo(string path, string htmlEntradas)
    {
        GarantirArquivo(path);
        string conteudo = File.ReadAllText(path, Encoding.UTF8);
        string marcador = "<!-- CAMERA_LOG_ENTRIES -->";
        int idx = conteudo.IndexOf(marcador, StringComparison.Ordinal);

        if (idx >= 0)
            conteudo = conteudo.Insert(idx + marcador.Length, "\n" + htmlEntradas);
        else
            conteudo += "\n" + htmlEntradas;

        File.WriteAllText(path, conteudo, Encoding.UTF8);
    }

    private string CriarHtmlBase()
    {
        return @"<!doctype html>
<html lang=""pt-BR""><head><meta charset=""utf-8""><meta name=""viewport"" content=""width=device-width,initial-scale=1""><title>MiniMarket Camera Realtime Log</title>
<style>
:root{--bg:#04070d;--panel:#0f172a;--panel2:#111c31;--line:#30415f;--txt:#e5eefc;--muted:#9fb1d1;--danger:#ff4f5e;--ok:#3df2a4;--cam:#7dd3fc;--warn:#fbbf24}*{box-sizing:border-box}body{margin:0;background:radial-gradient(circle at 10% 0,#122842,#04070d 45%,#02040a);color:var(--txt);font-family:Segoe UI,Arial,sans-serif}header{position:sticky;top:0;z-index:10;padding:24px 30px;background:linear-gradient(135deg,rgba(15,23,42,.98),rgba(2,6,23,.96));border-bottom:1px solid var(--line);box-shadow:0 12px 30px rgba(0,0,0,.35)}h1{margin:0;color:var(--cam);font-size:28px}.subtitle{margin-top:7px;color:var(--muted);font-size:14px}.grid{display:grid;grid-template-columns:repeat(auto-fit,minmax(180px,1fr));gap:12px;padding:18px 30px 0}.card{background:linear-gradient(180deg,var(--panel),#07101f);border:1px solid var(--line);border-radius:14px;padding:14px 16px}.card b{display:block;color:var(--muted);font-size:12px;text-transform:uppercase;letter-spacing:.08em}.card span{display:block;margin-top:7px;font-size:20px;color:#fff}main{padding:20px 30px 80px}.entry{background:linear-gradient(180deg,var(--panel2),var(--panel));border:1px solid var(--line);border-left:6px solid var(--cam);border-radius:14px;margin-bottom:14px;padding:14px 16px;box-shadow:0 10px 24px rgba(0,0,0,.28)}.entry.incident{border-left-color:var(--danger);box-shadow:0 0 0 1px rgba(255,79,94,.25),0 10px 28px rgba(255,79,94,.12)}.entry.system{border-left-color:var(--ok)}.entry.camera{border-left-color:var(--warn)}.entry.sample{opacity:.82}.meta{display:flex;gap:8px;flex-wrap:wrap;margin-bottom:8px}.meta span{font-size:12px;color:#cbd5e1;background:#050b15;border:1px solid #334155;border-radius:999px;padding:3px 9px}h2{font-size:17px;margin:0 0 8px;color:#fff}pre{white-space:pre-wrap;margin:0;color:#dbeafe;line-height:1.45;font-size:13px}.hint{margin:18px 30px 0;padding:13px 16px;border:1px solid #24506d;border-radius:13px;background:rgba(125,211,252,.08);color:#dff6ff}
</style></head><body><header><h1>MiniMarket Camera Realtime Log</h1><div class=""subtitle"">Monitor exclusivo da câmera. Detecta saltos bruscos, pausa o jogo e registra a causa provável.</div></header><section class=""grid""><div class=""card""><b>Status</b><span>Realtime</span></div><div class=""card""><b>Incidente</b><span>Auto Pause</span></div><div class=""card""><b>Arquivo</b><span>CameraRealtimeLog.htm</span></div><div class=""card""><b>Spam</b><span>Cooldown</span></div></section><div class=""hint"">Quando houver mudança brusca, o Play será pausado e a MessageBox mostrará posição, rotação, FOV, input, estado da primeira pessoa e causa provável.</div><main><!-- CAMERA_LOG_ENTRIES --></main></body></html>";
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
