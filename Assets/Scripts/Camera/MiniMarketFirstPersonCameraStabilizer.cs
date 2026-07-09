using System.Reflection;
using UnityEngine;

/// <summary>
/// Estabilizador automatico para remover o efeito de "jumping / fade out" da camera em primeira pessoa.
///
/// Ele nao substitui a CameraGTAFollowHardcore. Apenas corrige os pontos que causam tontura:
/// - remove inercia residual do mouse na primeira pessoa;
/// - impede snap/recenter ao entrar na mira;
/// - deixa a sensibilidade igual a camera principal;
/// - evita que a rotacao do corpo puxe a camera depois que o mouse para.
///
/// Cria-se automaticamente ao carregar a cena.
/// </summary>
[DefaultExecutionOrder(32000)]
public class MiniMarketFirstPersonCameraStabilizer : MonoBehaviour
{
    public CameraGTAFollowHardcore cameraGTA;

    [Header("Auto")]
    public bool procurarCameraAutomaticamente = true;
    public bool aplicarConfiguracaoAutomaticamente = true;

    [Header("Anti Jumping")]
    [Tooltip("Zera a inercia do mouse quando ele para, removendo o fade out no final do movimento.")]
    public bool zerarInerciaMouseQuandoParar = true;

    [Tooltip("Remove a rotacao do corpo causada diretamente pelo mouse na primeira pessoa. O corpo ainda pode virar pela movimentacao normal.")]
    public bool desativarRotacaoDoCorpoPelaCamera = true;

    [Tooltip("Forca a mira/primeira pessoa a usar a mesma sensibilidade da camera normal.")]
    public bool usarMesmaSensibilidadeDaCameraNormal = true;

    [Tooltip("Valor aplicado ao smooth do mouse em primeira pessoa. 0 remove completamente a inercia.")]
    [Min(0f)] public float mouseSmoothTimePrimeiraPessoa = 0f;

    [Tooltip("Limite abaixo do qual considera que o mouse parou.")]
    [Min(0f)] public float deadzoneParadaMouse = 0.0015f;

    [Header("Debug")]
    public bool logarEventos;

    private static MiniMarketFirstPersonCameraStabilizer instancia;
    private FieldInfo campoMouseSuavizado;
    private FieldInfo campoMouseSmoothVelocity;
    private FieldInfo campoPositionVelocity;
    private FieldInfo campoRotationVelocity;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void CriarAutomaticamente()
    {
        if (instancia != null)
            return;

        GameObject go = new GameObject("MiniMarket_FirstPersonCameraStabilizer");
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

        if (zerarInerciaMouseQuandoParar && cameraGTA.EstaEmPrimeiraPessoa)
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
            PrepararReflection();
    }

    private void AplicarConfiguracao()
    {
        if (cameraGTA == null)
            return;

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

        // Na primeira pessoa, velocidade de transicao residual pode parecer um pequeno encaixe.
        if (campoPositionVelocity != null)
            campoPositionVelocity.SetValue(cameraGTA, Vector3.zero);

        if (campoRotationVelocity != null)
            campoRotationVelocity.SetValue(cameraGTA, new Quaternion(0f, 0f, 0f, 0f));
    }
}
