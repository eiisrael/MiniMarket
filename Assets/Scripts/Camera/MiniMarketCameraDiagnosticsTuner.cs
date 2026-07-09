using UnityEngine;

/// <summary>
/// Ajusta automaticamente o CameraRealtimeAnomalyLogger para não pausar por movimento normal de mouse/orbit.
///
/// O log anterior mostrou que uma parte dos incidentes era causada por mudança normal de órbita da câmera
/// enquanto havia Mouse Raw diferente de zero. O detector continua ativo, mas com limites mais adequados
/// para capturar snap real de distância/colisão, não movimento normal do jogador.
/// </summary>
[DefaultExecutionOrder(33100)]
public class MiniMarketCameraDiagnosticsTuner : MonoBehaviour
{
    public bool aplicarAutomaticamente = true;
    public bool procurarAutomaticamente = true;
    public float intervaloBusca = 1f;

    [Header("Novos limites seguros")]
    public float limiteSaltoPosicaoFrame = 1.35f;
    public float limiteVelocidadeCamera = 45f;
    public float limiteAceleracaoCamera = 500f;
    public float limiteSaltoRotacaoFrame = 28f;
    public float limiteVelocidadeAngular = 720f;
    public float limiteSaltoFovFrame = 5f;
    public float limiteSaltoDistanciaAlvo = 0.55f;
    public float toleranciaAposTrocaPrimeiraPessoa = 0.32f;

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
        logger.intervaloAmostraLeve = intervaloAmostraLeve;
        logger.intervaloFlush = intervaloFlush;

        if (!aplicouUmaVez)
        {
            aplicouUmaVez = true;
            MiniMarketUpgradeLogger.Log("Camera", "Camera diagnostics tuner aplicado", "Logger ajustado para ignorar movimento normal de órbita por mouse e focar em snap real de distância/colisão.", "camera-diagnostics-tuner", 3f);
        }
    }
}
