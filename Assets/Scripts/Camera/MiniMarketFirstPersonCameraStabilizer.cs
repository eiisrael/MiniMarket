using System.Reflection;
using UnityEngine;

/// <summary>
/// Estabilizador automatico da camera do MiniMarket.
///
/// Corrige dois problemas comuns:
/// - pulso/reset de eixo da Main Camera causado por auto-align;
/// - jumping/fade-out horizontal da primeira pessoa causado por inercia residual do mouse e suavizacao acumulada.
///
/// Ele se cria sozinho ao carregar a cena e aplica ajustes seguros na CameraGTAFollowHardcore.
/// </summary>
[DefaultExecutionOrder(32000)]
public class MiniMarketFirstPersonCameraStabilizer : MonoBehaviour
{
    public CameraGTAFollowHardcore cameraGTA;

    [Header("Auto")]
    public bool procurarCameraAutomaticamente = true;
    public bool aplicarConfiguracaoAutomaticamente = true;

    [Header("Main Camera - Anti Pulso")]
    [Tooltip("Desliga o auto-align que puxa a camera para tras do personagem e causa pulso/reset de eixo.")]
    public bool desativarAutoAlignDaCamera = true;

    [Tooltip("Mantem a camera livre, sem recentralizar automaticamente no corpo.")]
    public bool manterCameraLivreSemRecenter = true;

    [Header("Primeira Pessoa - Anti Jumping")]
    [Tooltip("Zera a inercia do mouse quando ele para, removendo o fade out no final do movimento.")]
    public bool zerarInerciaMouseQuandoParar = true;

    [Tooltip("Remove a rotacao do corpo causada diretamente pela camera. O corpo continua podendo virar por outros scripts/movimento.")]
    public bool desativarRotacaoDoCorpoPelaCamera = true;

    [Tooltip("Forca a mira/primeira pessoa a usar a mesma sensibilidade da camera normal.")]
    public bool usarMesmaSensibilidadeDaCameraNormal = true;

    [Tooltip("Valor aplicado ao smooth do mouse em primeira pessoa. 0 remove completamente a inercia.")]
    [Min(0f)] public float mouseSmoothTimePrimeiraPessoa = 0f;

    [Tooltip("Limite abaixo do qual considera que o mouse parou.")]
    [Min(0f)] public float deadzoneParadaMouse = 0.0015f;

    [Tooltip("Quando entrar/sair da primeira pessoa, limpa as velocidades internas de transicao para nao dar encaixe final.")]
    public bool limparVelocidadesDuranteTransicao = true;

    [Header("Debug")]
    public bool logarEventos;

    private static MiniMarketFirstPersonCameraStabilizer instancia;
    private FieldInfo campoMouseSuavizado;
    private FieldInfo campoMouseSmoothVelocity;
    private FieldInfo campoPositionVelocity;
    private FieldInfo campoRotationVelocity;
    private bool ultimoEstadoPrimeiraPessoa;
    private float ultimoLogTempo;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void CriarAutomaticamente()
    {
        if (instancia != null)
            return;

        GameObject go = new GameObject("MiniMarket_CameraStabilizer");
        DontDestroyOnLoad(go);
        instancia = go.AddComponent<MiniMarketFirstPersonCameraStabilizer>();
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
        ResolverCamera();
        PrepararReflection();
        AplicarConfiguracao();
    }

    private void LateUpdate()
    {
        ResolverCamera();

        if (cameraGTA == null)
            return;

        if (aplicarConfiguracaoAutomaticamente)
            AplicarConfiguracao();

        bool estadoPrimeiraPessoa = cameraGTA.EstaEmPrimeiraPessoa;

        if (limparVelocidadesDuranteTransicao && estadoPrimeiraPessoa != ultimoEstadoPrimeiraPessoa)
            LimparVelocidadesInternas();

        ultimoEstadoPrimeiraPessoa = estadoPrimeiraPessoa;

        if (zerarInerciaMouseQuandoParar && estadoPrimeiraPessoa)
            RemoverInerciaResidualDoMouse();
    }

    private void ResolverCamera()
    {
        if (!procurarCameraAutomaticamente)
            return;

        if (cameraGTA != null)
            return;

        Camera main = Camera.main;
        if (main != null)
            cameraGTA = main.GetComponent<CameraGTAFollowHardcore>();

        if (cameraGTA == null)
            cameraGTA = FindObjectOfType<CameraGTAFollowHardcore>(true);

        if (cameraGTA != null)
        {
            PrepararReflection();
            MiniMarketUpgradeLogger.Log("Camera", "CameraGTAFollowHardcore encontrada", "Estabilizador automatico conectado a " + cameraGTA.gameObject.name, "camera-found", 10f);
        }
    }

    private void AplicarConfiguracao()
    {
        if (cameraGTA == null)
            return;

        if (desativarAutoAlignDaCamera)
        {
            cameraGTA.autoAlignBehindPlayer = false;
            cameraGTA.autoAlignDelay = 999f;
            cameraGTA.autoAlignSpeed = 0f;
        }

        if (manterCameraLivreSemRecenter)
        {
            cameraGTA.protegerContraResetDeAngulo = true;
            cameraGTA.anguloMaximoAutoAlignSemReset = 180f;
            cameraGTA.bloquearAutoAlignDuranteMira = true;
        }

        cameraGTA.preservarAnguloAtualNaTransicao = true;
        cameraGTA.alinharComFrenteDoPersonagemAoEntrarNaMira = false;
        cameraGTA.corrigirPitchAoEntrarNaMira = false;
        cameraGTA.usarPosicaoPrimeiraPessoaEstavel = true;
        cameraGTA.evitarRealimentacaoRotacaoPersonagemCamera = true;

        if (desativarRotacaoDoCorpoPelaCamera)
        {
            cameraGTA.rotacionarPersonagemNaPrimeiraPessoa = false;
            cameraGTA.sincronizarCorpoSuaveNaPrimeiraPessoa = false;
        }

        if (usarMesmaSensibilidadeDaCameraNormal)
        {
            cameraGTA.usarMesmaSensibilidadeDaMainCameraNaPrimeiraPessoa = true;
            cameraGTA.usarSensibilidadeSeparadaNaMira = false;
            cameraGTA.mouseSensitivityXMira = cameraGTA.mouseSensitivityX;
            cameraGTA.mouseSensitivityYMira = cameraGTA.mouseSensitivityY;
        }

        cameraGTA.mouseSmoothTimePrimeiraPessoa = mouseSmoothTimePrimeiraPessoa;

        if (logarEventos && Time.unscaledTime - ultimoLogTempo > 10f)
        {
            ultimoLogTempo = Time.unscaledTime;
            MiniMarketUpgradeLogger.Log("Camera", "Configuracao anti-jumping aplicada", "Auto-align desligado; primeira pessoa sem recenter/pitch snap; smooth FP = " + mouseSmoothTimePrimeiraPessoa.ToString("0.###"), "camera-config", 10f);
        }
    }

    private void PrepararReflection()
    {
        if (cameraGTA == null)
            return;

        System.Type tipo = typeof(CameraGTAFollowHardcore);
        campoMouseSuavizado = tipo.GetField("mouseSuavizado", BindingFlags.Instance | BindingFlags.NonPublic);
        campoMouseSmoothVelocity = tipo.GetField("mouseSmoothVelocity", BindingFlags.Instance | BindingFlags.NonPublic);
        campoPositionVelocity = tipo.GetField("positionVelocity", BindingFlags.Instance | BindingFlags.NonPublic);
        campoRotationVelocity = tipo.GetField("rotationVelocity", BindingFlags.Instance | BindingFlags.NonPublic);
    }

    private void RemoverInerciaResidualDoMouse()
    {
        float mx = Input.GetAxisRaw("Mouse X");
        float my = Input.GetAxisRaw("Mouse Y");

        if ((mx * mx + my * my) > deadzoneParadaMouse * deadzoneParadaMouse)
            return;

        if (campoMouseSuavizado != null)
            campoMouseSuavizado.SetValue(cameraGTA, Vector2.zero);

        if (campoMouseSmoothVelocity != null)
            campoMouseSmoothVelocity.SetValue(cameraGTA, Vector2.zero);

        if (campoRotationVelocity != null)
            campoRotationVelocity.SetValue(cameraGTA, new Quaternion(0f, 0f, 0f, 0f));
    }

    private void LimparVelocidadesInternas()
    {
        if (campoMouseSuavizado != null)
            campoMouseSuavizado.SetValue(cameraGTA, Vector2.zero);

        if (campoMouseSmoothVelocity != null)
            campoMouseSmoothVelocity.SetValue(cameraGTA, Vector2.zero);

        if (campoPositionVelocity != null)
            campoPositionVelocity.SetValue(cameraGTA, Vector3.zero);

        if (campoRotationVelocity != null)
            campoRotationVelocity.SetValue(cameraGTA, new Quaternion(0f, 0f, 0f, 0f));
    }
}
