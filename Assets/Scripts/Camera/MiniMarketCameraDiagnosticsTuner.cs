using UnityEngine;

/// <summary>
/// Ajusta automaticamente o CameraRealtimeAnomalyLogger antes dele analisar a câmera.
///
/// Diagnóstico dos logs v2.1.2:
/// - os travamentos restantes eram pausas falsas do logger;
/// - em primeira pessoa, RMB=True e Mouse Raw alto indicam movimento normal do jogador;
/// - o logger estava interpretando esse giro normal como salto brusco de rotação.
///
/// Correção:
/// - rotação/velocidade/aceleração deixam de pausar o Play por movimento normal;
/// - o logger continua sensível para o que realmente importa: snap de distância câmera->alvo,
///   FOV anormal e colisão/anti-parede.
/// </summary>
[DefaultExecutionOrder(32900)]
public class MiniMarketCameraDiagnosticsTuner : MonoBehaviour
{
    public bool aplicarAutomaticamente = true;
    public bool procurarAutomaticamente = true;
    public float intervaloBusca = 0.25f;

    [Header("Limites anti falso positivo")]
    [Tooltip("Alto de propósito: movimento normal do player/camera não deve pausar o Play.")]
    public float limiteSaltoPosicaoFrame = 8f;

    [Tooltip("Alto de propósito: velocidade normal da câmera em órbita/primeira pessoa não é bug.")]
    public float limiteVelocidadeCamera = 9999f;

    [Tooltip("Alto de propósito: evita pausa falsa por aceleração calculada em frames de FPS baixo.")]
    public float limiteAceleracaoCamera = 999999f;

    [Tooltip("Alto de propósito: olhar com o mouse em primeira pessoa pode girar muitos graus em um frame.")]
    public float limiteSaltoRotacaoFrame = 179f;

    [Tooltip("Alto de propósito: velocidade angular por input do mouse não é travamento.")]
    public float limiteVelocidadeAngular = 999999f;

    [Tooltip("Mantém detecção de mudança brusca de FOV.")]
    public float limiteSaltoFovFrame = 8f;

    [Header("Detecção real que deve continuar ativa")]
    [Tooltip("Continua sensível para snap real de colisão/distância. Se passar disso, é provável anti-parede/colisão.")]
    public float limiteSaltoDistanciaAlvo = 0.95f;

    public float toleranciaAposTrocaPrimeiraPessoa = 0.65f;

    [Header("Pausa")]
    [Tooltip("No Editor, EditorApplication.isPaused já pausa. Não usar TimeScale 0 evita congelar Time.time e confundir logs.")]
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
        logger.usarTimeScaleZeroAoPausar = usarTimeScaleZeroAoPausar;
        logger.intervaloAmostraLeve = intervaloAmostraLeve;
        logger.intervaloFlush = intervaloFlush;

        if (!aplicouUmaVez)
        {
            aplicouUmaVez = true;
            MiniMarketUpgradeLogger.Log("Camera", "Camera diagnostics tuner aplicado", "Pausas falsas por rotação/mouse normal foram bloqueadas. O logger continua detectando snap real de distância/FOV/colisão.", "camera-diagnostics-tuner-v2", 3f);
        }
    }
}
