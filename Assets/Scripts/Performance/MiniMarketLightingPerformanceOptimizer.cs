using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Otimiza luzes pontuais sem desligar as próprias luzes.
/// Mantém sombras da Directional Light e limita sombras de Point/Spot Lights,
/// evitando o spam de "Reduced additional punctual light shadows resolution".
/// </summary>
[DefaultExecutionOrder(-44000)]
[DisallowMultipleComponent]
public class MiniMarketLightingPerformanceOptimizer : MonoBehaviour
{
    public static MiniMarketLightingPerformanceOptimizer Instance { get; private set; }

    [Header("Ativação")]
    public bool ativo = true;

    [Header("Sombras pontuais")]
    [Min(0)] public int maxLuzesPontuaisComSombraDesktop = 1;
    [Min(0)] public int maxLuzesPontuaisComSombraMobile = 0;
    public bool manterSombrasDirectionalLight = true;
    public bool considerarSomenteLuzesAtivas = true;

    [Header("Qualidade mobile")]
    public bool aplicarLimitesMobile = true;
    [Range(1, 8)] public int pixelLightCountMobile = 2;
    [Min(5f)] public float shadowDistanceMobile = 30f;

    [Header("Busca")]
    [Min(1f)] public float intervaloRevalidacao = 5f;

    [Header("Debug")]
    public bool logarResultado;

    private Light[] luzes = new Light[0];
    private Transform referenciaDistancia;
    private float proximaRevalidacao;
    private int luzesPontuaisEncontradas;
    private int sombrasPontuaisMantidas;
    private int sombrasPontuaisDesligadas;

    public int LuzesPontuaisEncontradas => luzesPontuaisEncontradas;
    public int SombrasPontuaisMantidas => sombrasPontuaisMantidas;
    public int SombrasPontuaisDesligadas => sombrasPontuaisDesligadas;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetarStatics()
    {
        Instance = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void CriarAutomaticamente()
    {
        if (Instance != null)
            return;

        GameObject go = new GameObject("MiniMarket_LightingPerformanceOptimizer");
        DontDestroyOnLoad(go);
        go.AddComponent<MiniMarketLightingPerformanceOptimizer>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        SceneManager.sceneLoaded += AoCarregarCena;
        AplicarPerfilMobile();
        RevalidarAgora();
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= AoCarregarCena;

        if (Instance == this)
            Instance = null;
    }

    private void Update()
    {
        if (!ativo || Time.unscaledTime < proximaRevalidacao)
            return;

        proximaRevalidacao = Time.unscaledTime + Mathf.Max(1f, intervaloRevalidacao);
        RevalidarAgora();
    }

    private void AoCarregarCena(Scene cena, LoadSceneMode modo)
    {
        proximaRevalidacao = 0f;
        referenciaDistancia = null;
        AplicarPerfilMobile();
    }

    [ContextMenu("Performance/Revalidar luzes agora")]
    public void RevalidarAgora()
    {
        if (!ativo)
            return;

        ResolverReferencia();
        luzes = Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        int limite = Application.isMobilePlatform
            ? Mathf.Max(0, maxLuzesPontuaisComSombraMobile)
            : Mathf.Max(0, maxLuzesPontuaisComSombraDesktop);

        luzesPontuaisEncontradas = 0;
        sombrasPontuaisMantidas = 0;
        sombrasPontuaisDesligadas = 0;

        // Primeiro identifica as melhores luzes pontuais para manter sombra.
        Light[] melhores = limite > 0 ? new Light[limite] : new Light[0];
        float[] melhoresScores = limite > 0 ? new float[limite] : new float[0];

        for (int i = 0; i < luzes.Length; i++)
        {
            Light luz = luzes[i];
            if (!EhLuzPontualValida(luz))
                continue;

            luzesPontuaisEncontradas++;

            if (limite <= 0)
                continue;

            float score = CalcularScore(luz);
            InserirMelhor(luz, score, melhores, melhoresScores);
        }

        for (int i = 0; i < luzes.Length; i++)
        {
            Light luz = luzes[i];
            if (luz == null)
                continue;

            if (luz.type == LightType.Directional)
            {
                if (!manterSombrasDirectionalLight && luz.shadows != LightShadows.None)
                    luz.shadows = LightShadows.None;

                continue;
            }

            if (!EhLuzPontualValida(luz))
                continue;

            bool manter = Contem(melhores, luz);
            if (manter)
            {
                sombrasPontuaisMantidas++;
            }
            else if (luz.shadows != LightShadows.None)
            {
                luz.shadows = LightShadows.None;
                sombrasPontuaisDesligadas++;
            }
        }

        if (logarResultado)
        {
            Debug.Log(
                "[MiniMarketLightingPerformance] Pontuais=" + luzesPontuaisEncontradas +
                ", sombras mantidas=" + sombrasPontuaisMantidas +
                ", sombras desligadas=" + sombrasPontuaisDesligadas
            );
        }
    }

    private void AplicarPerfilMobile()
    {
        if (!Application.isMobilePlatform || !aplicarLimitesMobile)
            return;

        QualitySettings.pixelLightCount = Mathf.Clamp(pixelLightCountMobile, 1, 8);
        QualitySettings.shadowDistance = Mathf.Min(QualitySettings.shadowDistance, Mathf.Max(5f, shadowDistanceMobile));
    }

    private void ResolverReferencia()
    {
        if (referenciaDistancia != null)
            return;

        PlayerMove player = Object.FindFirstObjectByType<PlayerMove>(FindObjectsInactive.Include);
        if (player != null)
        {
            referenciaDistancia = player.transform;
            return;
        }

        CameraV2Controller controller = Object.FindFirstObjectByType<CameraV2Controller>(FindObjectsInactive.Include);
        if (controller != null)
            referenciaDistancia = controller.CameraAtivaTransform;
    }

    private bool EhLuzPontualValida(Light luz)
    {
        if (luz == null)
            return false;

        if (luz.type != LightType.Point && luz.type != LightType.Spot)
            return false;

        if (considerarSomenteLuzesAtivas && (!luz.enabled || !luz.gameObject.activeInHierarchy))
            return false;

        return true;
    }

    private float CalcularScore(Light luz)
    {
        float distancia = 0f;

        if (referenciaDistancia != null)
            distancia = Vector3.Distance(luz.transform.position, referenciaDistancia.position);

        float sombraAtual = luz.shadows != LightShadows.None ? 10f : 0f;
        return sombraAtual + Mathf.Max(0f, luz.intensity) * 2f + Mathf.Max(0f, luz.range) * 0.05f - distancia * 0.05f;
    }

    private void InserirMelhor(Light luz, float score, Light[] melhores, float[] scores)
    {
        for (int i = 0; i < melhores.Length; i++)
        {
            if (melhores[i] != null && scores[i] >= score)
                continue;

            for (int j = melhores.Length - 1; j > i; j--)
            {
                melhores[j] = melhores[j - 1];
                scores[j] = scores[j - 1];
            }

            melhores[i] = luz;
            scores[i] = score;
            return;
        }
    }

    private bool Contem(Light[] array, Light luz)
    {
        for (int i = 0; i < array.Length; i++)
        {
            if (array[i] == luz)
                return true;
        }

        return false;
    }
}
