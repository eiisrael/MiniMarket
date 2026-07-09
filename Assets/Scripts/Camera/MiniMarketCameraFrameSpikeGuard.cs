using UnityEngine;

/// <summary>
/// Guarda final contra pulos raros de câmera causados por frame spike.
///
/// Logs v2.1.3 mostraram FPS aproximado 1~2 no momento dos incidentes.
/// Quando o frame demora demais, qualquer movimento normal do player/mouse vira um salto visual.
/// Este script roda depois da câmera e do suavizador de colisão, mas antes do logger,
/// limitando apenas o deslocamento/rotação máxima visível em frames ruins.
///
/// Ele não cria efeito mola contínuo: só atua quando o deltaTime fica alto ou quando a câmera dá
/// um salto acima do limite configurado.
/// </summary>
[DefaultExecutionOrder(32800)]
public class MiniMarketCameraFrameSpikeGuard : MonoBehaviour
{
    [Header("Referências")]
    public Camera cameraMonitorada;
    public CameraGTAFollowHardcore cameraGTA;

    [Header("Ativação")]
    public bool ativo = true;
    public bool procurarAutomaticamente = true;
    [Min(0.2f)] public float intervaloBusca = 1f;

    [Header("Detecção")]
    [Tooltip("Aciona proteção quando o frame demora mais que isso. 0.12 = abaixo de ~8 FPS.")]
    [Min(0.02f)] public float deltaTimeFrameRuim = 0.12f;

    [Tooltip("Também aciona se a câmera mudar de posição mais que isso em um frame.")]
    [Min(0.05f)] public float saltoPosicaoParaCorrigir = 0.85f;

    [Tooltip("Também aciona se a câmera girar mais que isso em um frame.")]
    [Min(1f)] public float saltoRotacaoParaCorrigir = 35f;

    [Header("Correção suave")]
    [Min(0.05f)] public float maxMovimentoPorFrameRuim = 0.45f;
    [Min(1f)] public float maxRotacaoPorFrameRuim = 18f;

    [Tooltip("Não corrige durante transição normal de primeira pessoa para não atrapalhar o botão direito.")]
    public bool ignorarDuranteTransicaoPrimeiraPessoa = true;

    [Tooltip("Ao trocar primeira pessoa/terceira pessoa, reseta baseline para não puxar a câmera de volta.")]
    public bool resetarAoTrocarPerspectiva = true;

    [Header("Debug")]
    public bool logarCorrecoes;

    private static MiniMarketCameraFrameSpikeGuard instancia;
    private Vector3 ultimaPosicao;
    private Quaternion ultimaRotacao;
    private bool possuiUltima;
    private bool ultimoEstadoPrimeiraPessoa;
    private float proximaBusca;
    private float ultimoLog;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void CriarAutomaticamente()
    {
        if (instancia != null)
            return;

        GameObject go = new GameObject("MiniMarket_CameraFrameSpikeGuard");
        DontDestroyOnLoad(go);
        instancia = go.AddComponent<MiniMarketCameraFrameSpikeGuard>();
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
        ResolverReferencias(true);
        CapturarBaseline();
    }

    private void LateUpdate()
    {
        if (!ativo)
            return;

        if (procurarAutomaticamente && Time.unscaledTime >= proximaBusca)
        {
            proximaBusca = Time.unscaledTime + intervaloBusca;
            ResolverReferencias(false);
        }

        if (cameraMonitorada == null)
            return;

        bool estadoPrimeiraPessoa = cameraGTA != null && cameraGTA.EstaEmPrimeiraPessoa;
        if (resetarAoTrocarPerspectiva && estadoPrimeiraPessoa != ultimoEstadoPrimeiraPessoa)
        {
            ultimoEstadoPrimeiraPessoa = estadoPrimeiraPessoa;
            CapturarBaseline();
            return;
        }

        if (ignorarDuranteTransicaoPrimeiraPessoa && cameraGTA != null && cameraGTA.EstaTransicionandoPrimeiraPessoa)
        {
            CapturarBaseline();
            return;
        }

        if (!possuiUltima)
        {
            CapturarBaseline();
            return;
        }

        Vector3 posAtual = cameraMonitorada.transform.position;
        Quaternion rotAtual = cameraMonitorada.transform.rotation;
        float deltaPos = Vector3.Distance(posAtual, ultimaPosicao);
        float deltaRot = Quaternion.Angle(ultimaRotacao, rotAtual);
        bool frameRuim = Time.unscaledDeltaTime >= deltaTimeFrameRuim;
        bool salto = deltaPos >= saltoPosicaoParaCorrigir || deltaRot >= saltoRotacaoParaCorrigir;

        if (frameRuim || salto)
        {
            Vector3 posCorrigida = posAtual;
            Quaternion rotCorrigida = rotAtual;

            if (deltaPos > maxMovimentoPorFrameRuim)
            {
                Vector3 direcao = posAtual - ultimaPosicao;
                if (direcao.sqrMagnitude > 0.000001f)
                    posCorrigida = ultimaPosicao + direcao.normalized * maxMovimentoPorFrameRuim;
            }

            if (deltaRot > maxRotacaoPorFrameRuim)
                rotCorrigida = Quaternion.RotateTowards(ultimaRotacao, rotAtual, maxRotacaoPorFrameRuim);

            cameraMonitorada.transform.position = posCorrigida;
            cameraMonitorada.transform.rotation = rotCorrigida;
            posAtual = posCorrigida;
            rotAtual = rotCorrigida;

            if (logarCorrecoes && Time.unscaledTime - ultimoLog > 1f)
            {
                ultimoLog = Time.unscaledTime;
                MiniMarketUpgradeLogger.Log("Camera", "Frame spike suavizado", "deltaTime=" + Time.unscaledDeltaTime.ToString("0.000") + " deltaPos=" + deltaPos.ToString("0.000") + " deltaRot=" + deltaRot.ToString("0.0"), "camera-frame-spike", 1f, LogType.Warning);
            }
        }

        ultimaPosicao = posAtual;
        ultimaRotacao = rotAtual;
        possuiUltima = true;
    }

    private void ResolverReferencias(bool forcar)
    {
        if (!forcar && cameraMonitorada != null && cameraGTA != null)
            return;

        if (cameraMonitorada == null)
            cameraMonitorada = Camera.main;

        if (cameraGTA == null && cameraMonitorada != null)
            cameraGTA = cameraMonitorada.GetComponent<CameraGTAFollowHardcore>();

        if (cameraGTA == null)
            cameraGTA = Object.FindFirstObjectByType<CameraGTAFollowHardcore>(FindObjectsInactive.Include);

        if (cameraMonitorada == null && cameraGTA != null)
            cameraMonitorada = cameraGTA.GetComponent<Camera>();
    }

    private void CapturarBaseline()
    {
        if (cameraMonitorada == null)
            return;

        ultimaPosicao = cameraMonitorada.transform.position;
        ultimaRotacao = cameraMonitorada.transform.rotation;
        possuiUltima = true;
    }
}
