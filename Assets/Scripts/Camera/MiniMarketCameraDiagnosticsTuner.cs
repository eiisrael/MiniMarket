using UnityEngine;

/// <summary>
/// Ajusta automaticamente o CameraRealtimeAnomalyLogger antes dele analisar a câmera.
///
/// Diagnóstico dos logs v2.1.3:
/// - Os raros "travamentos" restantes estavam associados à própria pausa/MessageBox do logger.
/// - Quando o logger pausava, a stamina parecia travar também porque o Play ficava pausado/congelado.
/// - Logs grandes no Console também alimentavam o UpgradeLog e causavam overhead.
///
/// Correção:
/// - O logger continua gravando incidente no CameraRealtimeLog.htm.
/// - Mas não pausa mais o Play automaticamente.
/// - Não abre MessageBox durante gameplay.
/// - Não escreve Warning gigante no Console.
/// - Amostras leves ficam desligadas por padrão para evitar IO constante.
/// </summary>
[DefaultExecutionOrder(32900)]
public class MiniMarketCameraDiagnosticsTuner : MonoBehaviour
{
    public bool aplicarAutomaticamente = true;
    public bool procurarAutomaticamente = true;
    public float intervaloBusca = 0.25f;

    [Header("Limites anti falso positivo")]
    public float limiteSaltoPosicaoFrame = 8f;
    public float limiteVelocidadeCamera = 9999f;
    public float limiteAceleracaoCamera = 999999f;
    public float limiteSaltoRotacaoFrame = 179f;
    public float limiteVelocidadeAngular = 999999f;
    public float limiteSaltoFovFrame = 8f;

    [Header("Detecção real que deve continuar ativa")]
    public float limiteSaltoDistanciaAlvo = 1.35f;
    public float toleranciaAposTrocaPrimeiraPessoa = 0.85f;

    [Header("Modo não bloqueante")]
    public bool pausarAoDetectar = false;
    public bool mostrarMessageBoxNoEditor = false;
    public bool usarTimeScaleZeroAoPausar = false;
    public bool logarNoConsole = false;

    [Header("Amostras / IO")]
    public bool registrarAmostrasLeves = false;
    public float intervaloAmostraLeve = 1.0f;
    public float intervaloFlush = 3.0f;

    private static MiniMarketCameraDiagnosticsTuner instancia;
    private MiniMarketCameraRealtimeAnomalyLogger logger;
    private float proximaBusca;
    private bool aplicouUmaVez;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void CriarAutomaticamente()
    {
        if (instancia != null)
            return;

        GameObject go = new GameObject("MiniMarket_CameraDiagnosticsTuner");
        DontDestroyOnLoad(go);
        instancia = go.AddComponent<MiniMarketCameraDiagnosticsTuner>();
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
        BuscarLogger();
        Aplicar();
    }

    private void Update()
    {
        if (!aplicarAutomaticamente)
            return;

        BuscarEAplicarSeNecessario();
    }

    private void LateUpdate()
    {
        if (!aplicarAutomaticamente)
            return;

        BuscarEAplicarSeNecessario();
    }

    private void BuscarEAplicarSeNecessario()
    {
        if (procurarAutomaticamente && Time.unscaledTime >= proximaBusca)
        {
            proximaBusca = Time.unscaledTime + Mathf.Max(0.1f, intervaloBusca);
            BuscarLogger();
        }

        Aplicar();
    }

    private void BuscarLogger()
    {
        if (logger == null)
            logger = Object.FindFirstObjectByType<MiniMarketCameraRealtimeAnomalyLogger>(FindObjectsInactive.Include);
    }

    private void Aplicar()
    {
        if (logger == null)
            return;

        logger.limiteSaltoPosicaoFrame = limiteSaltoPosicaoFrame;
        logger.limiteVelocidadeCamera = limiteVelocidadeCamera;
        logger.limiteAceleracaoCamera = limiteAceleracaoCamera;
        logger.limiteSaltoRotacaoFrame = limiteSaltoRotacaoFrame;
        logger.limiteVelocidadeAngular = limiteVelocidadeAngular;
        logger.limiteSaltoFovFrame = limiteSaltoFovFrame;
        logger.limiteSaltoDistanciaAlvo = limiteSaltoDistanciaAlvo;
        logger.toleranciaAposTrocaPrimeiraPessoa = toleranciaAposTrocaPrimeiraPessoa;
        logger.pausarAoDetectar = pausarAoDetectar;
        logger.mostrarMessageBoxNoEditor = mostrarMessageBoxNoEditor;
        logger.usarTimeScaleZeroAoPausar = usarTimeScaleZeroAoPausar;
        logger.logarNoConsole = logarNoConsole;
        logger.registrarAmostrasLeves = registrarAmostrasLeves;
        logger.intervaloAmostraLeve = intervaloAmostraLeve;
        logger.intervaloFlush = intervaloFlush;

        if (!aplicouUmaVez)
        {
            aplicouUmaVez = true;
            MiniMarketUpgradeLogger.Log("Camera", "Camera diagnostics tuner aplicado", "Logger em modo não bloqueante: sem pausa, sem MessageBox, sem Warning gigante e sem amostras leves constantes.", "camera-diagnostics-nonblocking", 3f);
        }
    }
}
