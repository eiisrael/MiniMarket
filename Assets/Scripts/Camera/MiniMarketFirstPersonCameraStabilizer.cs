using System.Reflection;
using UnityEngine;

/// <summary>
/// Estabilizador automático da câmera do MiniMarket.
///
/// Mantém a Main Camera estável, sem auto-align/recenter, e força a primeira pessoa real
/// quando o botão direito estiver ativo pelo MiniMarketCameraPerspectiveSwitcher.
/// </summary>
[DefaultExecutionOrder(32000)]
public class MiniMarketFirstPersonCameraStabilizer : MonoBehaviour
{
    public CameraGTAFollowHardcore cameraGTA;

    [Header("Auto")]
    public bool procurarCameraAutomaticamente = true;
    public bool aplicarConfiguracaoAutomaticamente = true;

    [Header("Main Camera - Anti Pulso")]
    public bool desativarAutoAlignDaCamera = true;
    public bool manterCameraLivreSemRecenter = true;

    [Header("Primeira Pessoa - Anti Jumping")]
    public bool zerarInerciaMouseQuandoParar = true;
    public bool permitirCorpoAcompanharCamera = true;
    public bool usarMesmaSensibilidadeDaCameraNormal = true;
    [Min(0f)] public float mouseSmoothTimePrimeiraPessoa = 0f;
    [Min(0f)] public float deadzoneParadaMouse = 0.0015f;
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
        if (!procurarCameraAutomaticamente || cameraGTA != null)
            return;

        Camera main = Camera.main;
        if (main != null)
            cameraGTA = main.GetComponent<CameraGTAFollowHardcore>();

        if (cameraGTA == null)
            cameraGTA = Object.FindFirstObjectByType<CameraGTAFollowHardcore>(FindObjectsInactive.Include);

        if (cameraGTA != null)
        {
            PrepararReflection();
            MiniMarketUpgradeLogger.Log("Camera", "CameraGTAFollowHardcore encontrada", "Estabilizador conectado a " + cameraGTA.gameObject.name, "camera-found", 10f);
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

        cameraGTA.usarZoomMiraPorDistancia = false;
        cameraGTA.aplicarColisaoNoZoomMira = false;
        cameraGTA.preservarAnguloAtualNaTransicao = true;
        cameraGTA.alinharComFrenteDoPersonagemAoEntrarNaMira = false;
        cameraGTA.corrigirPitchAoEntrarNaMira = false;
        cameraGTA.usarPosicaoPrimeiraPessoaEstavel = true;
        cameraGTA.evitarRealimentacaoRotacaoPersonagemCamera = true;
        cameraGTA.rotacionarPersonagemNaPrimeiraPessoa = permitirCorpoAcompanharCamera;
        cameraGTA.sincronizarCorpoSuaveNaPrimeiraPessoa = permitirCorpoAcompanharCamera;
        cameraGTA.mouseSmoothTimePrimeiraPessoa = mouseSmoothTimePrimeiraPessoa;

        if (usarMesmaSensibilidadeDaCameraNormal)
        {
            cameraGTA.usarMesmaSensibilidadeDaMainCameraNaPrimeiraPessoa = true;
            cameraGTA.usarSensibilidadeSeparadaNaMira = false;
            cameraGTA.mouseSensitivityXMira = cameraGTA.mouseSensitivityX;
            cameraGTA.mouseSensitivityYMira = cameraGTA.mouseSensitivityY;
        }

        if (logarEventos && Time.unscaledTime - ultimoLogTempo > 10f)
        {
            ultimoLogTempo = Time.unscaledTime;
            MiniMarketUpgradeLogger.Log("Camera", "Estabilizacao aplicada", "Auto-align desligado; primeira pessoa real; smooth FP = " + mouseSmoothTimePrimeiraPessoa.ToString("0.###"), "camera-config", 10f);
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
