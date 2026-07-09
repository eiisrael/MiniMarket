using UnityEngine;

/// <summary>
/// Estabilizador opcional de autoridade da câmera.
///
/// Mudança importante:
/// - por padrão fica em Modo Inspector Livre;
/// - não força valores continuamente;
/// - só aplica regras se você desligar Modo Inspector Livre.
///
/// Isso evita que campos do Inspector pareçam travados durante o Play.
/// </summary>
[DefaultExecutionOrder(34000)]
public class MiniMarketCameraAuthorityStabilizer : MonoBehaviour
{
    [Header("Modo Inspector")]
    [Tooltip("Ligado: não altera valores de outros scripts. Use assim para configurar tudo manualmente no Inspector.")]
    public bool modoInspectorLivre = true;

    [Header("Ativação")]
    public bool ativo = true;
    public bool aplicarContinuamente = false;
    [Min(0.1f)] public float intervaloAplicacao = 0.5f;

    [Header("Autoridade da Camera - Preset Opcional")]
    public bool garantirFrameSpikeSomenteDiagnostico = true;
    public bool impedirCollisionSmootherDeRotacionar = true;
    public bool impedirPerspectiveSwitcherDeRotacionarPersonagem = true;
    public bool impedirGrabberDeMoverCamera = true;

    [Header("Debug")]
    public bool logarAplicacao;

    private static MiniMarketCameraAuthorityStabilizer instancia;
    private float proximaAplicacao;
    private float ultimoLog;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void CriarAutomaticamente()
    {
        if (instancia != null)
            return;

        GameObject go = new GameObject("MiniMarket_CameraAuthorityStabilizer");
        DontDestroyOnLoad(go);
        instancia = go.AddComponent<MiniMarketCameraAuthorityStabilizer>();
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
        AplicarRegrasSePermitido();
    }

    private void LateUpdate()
    {
        if (!ativo || !aplicarContinuamente)
            return;

        if (Time.unscaledTime < proximaAplicacao)
            return;

        proximaAplicacao = Time.unscaledTime + intervaloAplicacao;
        AplicarRegrasSePermitido();
    }

    private void AplicarRegrasSePermitido()
    {
        if (modoInspectorLivre)
            return;

        AplicarRegras();
    }

    private void AplicarRegras()
    {
        int alteracoes = 0;

        if (garantirFrameSpikeSomenteDiagnostico)
            alteracoes += AplicarFrameSpikeGuard();

        if (impedirCollisionSmootherDeRotacionar)
            alteracoes += AplicarCollisionSmoother();

        if (impedirPerspectiveSwitcherDeRotacionarPersonagem)
            alteracoes += AplicarPerspectiveSwitcher();

        if (impedirGrabberDeMoverCamera)
            alteracoes += AplicarGrabber();

        if (logarAplicacao && alteracoes > 0 && Time.unscaledTime - ultimoLog > 3f)
        {
            ultimoLog = Time.unscaledTime;
            MiniMarketUpgradeLogger.Log("Camera", "Autoridade única aplicada", "Ajustes aplicados: " + alteracoes + ". CameraGTA mantém rotação/eixo principal.", "camera-authority", 3f);
        }
    }

    private int AplicarFrameSpikeGuard()
    {
        int count = 0;
        MiniMarketCameraFrameSpikeGuard[] guards = Object.FindObjectsByType<MiniMarketCameraFrameSpikeGuard>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < guards.Length; i++)
        {
            MiniMarketCameraFrameSpikeGuard guard = guards[i];
            if (guard == null)
                continue;

            if (!guard.somenteDiagnostico)
            {
                guard.somenteDiagnostico = true;
                count++;
            }
        }
        return count;
    }

    private int AplicarCollisionSmoother()
    {
        int count = 0;
        MiniMarketCameraCollisionSmoother[] smoothers = Object.FindObjectsByType<MiniMarketCameraCollisionSmoother>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < smoothers.Length; i++)
        {
            MiniMarketCameraCollisionSmoother smoother = smoothers[i];
            if (smoother == null)
                continue;

            if (smoother.sobrescreverRotacaoDaCamera)
            {
                smoother.sobrescreverRotacaoDaCamera = false;
                count++;
            }
        }
        return count;
    }

    private int AplicarPerspectiveSwitcher()
    {
        int count = 0;
        MiniMarketCameraPerspectiveSwitcher[] switchers = Object.FindObjectsByType<MiniMarketCameraPerspectiveSwitcher>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < switchers.Length; i++)
        {
            MiniMarketCameraPerspectiveSwitcher switcher = switchers[i];
            if (switcher == null)
                continue;

            if (switcher.rotacionarPersonagemComCamera)
            {
                switcher.rotacionarPersonagemComCamera = false;
                count++;
            }
        }
        return count;
    }

    private int AplicarGrabber()
    {
        int count = 0;
        PlayerObjectGrabberHardcore[] grabbers = Object.FindObjectsByType<PlayerObjectGrabberHardcore>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < grabbers.Length; i++)
        {
            PlayerObjectGrabberHardcore grabber = grabbers[i];
            if (grabber == null)
                continue;

            if (grabber.puxarCameraParaEsquerdaAoMirarOuSegurar)
            {
                grabber.puxarCameraParaEsquerdaAoMirarOuSegurar = false;
                count++;
            }

            if (grabber.aplicarAssistenciaCameraNaPrimeiraPessoa)
            {
                grabber.aplicarAssistenciaCameraNaPrimeiraPessoa = false;
                count++;
            }

            if (grabber.deslocamentoCameraEsquerda != 0f)
            {
                grabber.deslocamentoCameraEsquerda = 0f;
                count++;
            }
        }
        return count;
    }
}
