using UnityEngine;

/// <summary>
/// Ajusta automaticamente o CameraRealtimeAnomalyLogger para não pausar por movimento normal de mouse/orbit.
///
/// O log mostrou dois cenários diferentes:
/// - problema real: distância câmera->alvo muda por colisão/anti-parede;
/// - falso positivo: órbita normal da câmera por mouse enquanto W/S/A/D está pressionado.
///
/// Este tuner mantém o logger ativo, mas aumenta limites de velocidade/aceleração/rotação
/// para ele não travar o Play por movimento normal do jogador.
/// </summary>
[DefaultExecutionOrder(33100)]
public class MiniMarketCameraDiagnosticsTuner : MonoBehaviour
{
    public bool aplicarAutomaticamente = true;
    public bool procurarAutomaticamente = true;
    public float intervaloBusca = 1f;

    [Header("Limites seguros anti falso positivo")]
    public float limiteSaltoPosicaoFrame = 2.2f;
    public float limiteVelocidadeCamera = 120f;
    public float limiteAceleracaoCamera = 2200f;
    public float limiteSaltoRotacaoFrame = 45f;
    public float limiteVelocidadeAngular = 1800f;
    public float limiteSaltoFovFrame = 6f;

    [Tooltip("Continua sensível para snap real de colisão/distância.")]
    public float limiteSaltoDistanciaAlvo = 0.85f;

    public float toleranciaAposTrocaPrimeiraPessoa = 0.45f;

    [Header("Pausa")]
    [Tooltip("No Editor, EditorApplication.isPaused já pausa. Não usar TimeScale 0 evita congelar Time.time e confundir os logs.")]
    public bool usarTimeScaleZeroAoPausar = false;

    [Header("Amostras")]
    public float intervaloAmostraLeve = 0.35f;
    public float intervaloFlush = 1.2f;

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

    private void LateUpdate()
    {
        if (!aplicarAutomaticamente)
            return;

        if (procurarAutomaticamente && Time.unscaledTime >= proximaBusca)
        {
            proximaBusca = Time.unscaledTime + Mathf.Max(0.25f, intervaloBusca);
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
        logger.usarTimeScaleZeroAoPausar = usarTimeScaleZeroAoPausar;
        logger.intervaloAmostraLeve = intervaloAmostraLeve;
        logger.intervaloFlush = intervaloFlush;

        if (!aplicouUmaVez)
        {
            aplicouUmaVez = true;
            MiniMarketUpgradeLogger.Log("Camera", "Camera diagnostics tuner aplicado", "Logger ajustado para não pausar por órbita normal/mouse e continuar pegando snap real de distância/colisão.", "camera-diagnostics-tuner", 3f);
        }
    }
}
