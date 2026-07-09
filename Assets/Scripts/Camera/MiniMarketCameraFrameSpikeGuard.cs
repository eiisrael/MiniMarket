using UnityEngine;

/// <summary>
/// Diagnóstico leve de frame spike da câmera.
///
/// Antes este script corrigia transform.position/transform.rotation da Main Camera depois da CameraGTA.
/// Isso podia deixar a rotação visual diferente do yaw/pitch interno e gerar o efeito de "pulo",
/// "encaixe" ou câmera indo para trás do personagem e depois voltando.
///
/// Agora ele NÃO altera mais a câmera. Apenas detecta e registra, quando habilitado.
/// A regra passa a ser: CameraGTAFollowHardcore é a autoridade de rotação/eixo da câmera;
/// outros scripts não devem sobrescrever rotação final.
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

    [Header("Modo Seguro")]
    [Tooltip("Mantido ligado. Nunca altera position/rotation da câmera, apenas registra diagnóstico.")]
    public bool somenteDiagnostico = true;

    [Header("Detecção")]
    [Min(0.02f)] public float deltaTimeFrameRuim = 0.12f;
    [Min(0.05f)] public float saltoPosicaoParaRegistrar = 1.4f;
    [Min(1f)] public float saltoRotacaoParaRegistrar = 55f;

    [Header("Transição")]
    public bool ignorarDuranteTransicaoPrimeiraPessoa = true;
    public bool resetarAoTrocarPerspectiva = true;

    [Header("Debug")]
    public bool logarCorrecoes = false;

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
        bool salto = deltaPos >= saltoPosicaoParaRegistrar || deltaRot >= saltoRotacaoParaRegistrar;

        if ((frameRuim || salto) && logarCorrecoes && Time.unscaledTime - ultimoLog > 1f)
        {
            ultimoLog = Time.unscaledTime;
            MiniMarketUpgradeLogger.Log(
                "Camera",
                "Frame spike detectado sem corrigir câmera",
                "deltaTime=" + Time.unscaledDeltaTime.ToString("0.000") +
                " deltaPos=" + deltaPos.ToString("0.000") +
                " deltaRot=" + deltaRot.ToString("0.0") +
                " | Modo seguro: nenhuma alteração em transform.position/rotation.",
                "camera-frame-spike-diagnostic",
                1f,
                LogType.Warning
            );
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
