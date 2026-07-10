using UnityEngine;

/// <summary>
/// Compatibilidade para scripts antigos que ainda referenciam CameraGTAFollowHardcore.
///
/// O sistema principal novo é Camera V2:
/// - Camera3Person
/// - Camera1Person
/// - CameraV2Controller
///
/// Este componente existe apenas para o projeto compilar enquanto os scripts antigos
/// ainda estão presentes na pasta Assets/Scripts. Não use este script para o setup V2.
/// </summary>
[DisallowMultipleComponent]
public class CameraGTAFollowHardcore : MonoBehaviour
{
    [Header("Legacy Target")]
    public Transform target;
    public Transform pontoPrimeiraPessoa;
    public Vector3 targetOffset = new Vector3(0f, 1.45f, 0f);
    public Vector3 offsetPrimeiraPessoa = new Vector3(0f, 1.68f, 0.18f);

    [Header("Legacy Orbit")]
    public float distance = 4.25f;
    public float height = 0.15f;
    public float minPitch = -40f;
    public float maxPitch = 60f;
    public float mouseSensitivityX = 180f;
    public float mouseSensitivityY = 130f;

    [Header("Legacy Mouse")]
    public bool suavizarMouse = false;
    public float mouseSmoothTimeTerceiraPessoa = 0f;
    public float mouseSmoothTimePrimeiraPessoa = 0f;
    public bool usarMesmaSensibilidadeDaMainCameraNaPrimeiraPessoa = true;
    public bool usarSensibilidadeSeparadaNaMira = false;
    public float mouseSensitivityXMira = 180f;
    public float mouseSensitivityYMira = 130f;

    [Header("Legacy Smooth")]
    public float positionSmoothTime = 0f;
    public float rotationSmoothTime = 0f;
    public float positionSmoothTimePrimeiraPessoa = 0f;
    public float rotationSmoothTimePrimeiraPessoa = 0f;
    public bool removerEfeitoMolaDaCamera = true;
    public bool usarSeguimentoDiretoQuandoNaoEstaTransicionando = true;
    public float ignorarTremorVerticalMenorQue = 0.01f;
    public bool estabilizarPontoPrimeiraPessoaContraAnimacao = true;

    [Header("Legacy Auto Align")]
    public bool autoAlignBehindPlayer = false;
    public float autoAlignDelay = 999f;
    public float autoAlignSpeed = 0f;
    public bool protegerContraResetDeAngulo = true;
    public float anguloMaximoAutoAlignSemReset = 180f;
    public bool bloquearAutoAlignDuranteMira = true;

    [Header("Legacy First Person")]
    public bool permitirPrimeiraPessoa = true;
    public bool preservarAnguloAtualNaTransicao = true;
    public bool alinharComFrenteDoPersonagemAoEntrarNaMira = false;
    public bool corrigirPitchAoEntrarNaMira = false;
    public bool usarPosicaoPrimeiraPessoaEstavel = true;
    public bool evitarRealimentacaoRotacaoPersonagemCamera = true;
    public bool rotacionarPersonagemNaPrimeiraPessoa = true;
    public bool sincronizarCorpoSuaveNaPrimeiraPessoa = true;
    public float velocidadeRotacaoPersonagemPrimeiraPessoa = 18f;

    [Header("Legacy Zoom")]
    public bool usarZoomMiraPorDistancia = false;
    public bool aplicarColisaoNoZoomMira = false;
    public bool ajustarFovNaMira = true;
    public bool capturarFovNormalNoStart = true;
    public float fovNormal = 60f;
    public float fovMira = 58f;
    public float velocidadeFovMira = 12f;

    [Header("Legacy Collision")]
    public bool usarColisaoCamera = false;
    public bool corrigirPosicaoFinalSuavizada = false;
    public LayerMask cameraCollisionLayers = ~0;
    public float cameraCollisionRadius = 0.28f;
    public float cameraCollisionOffset = 0.18f;
    public float cameraMinDistanceFromFocus = 0.85f;
    public bool usarAntiGrudarExtra = true;
    public int passosBuscaAntiGrudar = 8;
    public bool desenharDebugColisaoCamera = false;

    [Header("Runtime Legacy State")]
    [SerializeField] private bool primeiraPessoaAtiva;
    [SerializeField] private bool transicionandoPrimeiraPessoa;
    [SerializeField] private float yaw;
    [SerializeField] private float pitch;
    [SerializeField] private float primeiraPessoaBlend;
    [SerializeField] private Vector3 positionVelocity;
    [SerializeField] private Vector2 mouseSuavizado;
    [SerializeField] private Vector2 mouseSmoothVelocity;
    [SerializeField] private Quaternion rotationVelocity;

    public bool EstaEmPrimeiraPessoa => primeiraPessoaAtiva;
    public bool EstaTransicionandoPrimeiraPessoa => transicionandoPrimeiraPessoa;
    public float YawAtual => yaw != 0f ? yaw : transform.eulerAngles.y;
    public float PitchAtual => pitch;

    private void Awake()
    {
        yaw = transform.eulerAngles.y;
        pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
    }

    public void SetPrimeiraPessoa(bool ativa)
    {
        primeiraPessoaAtiva = ativa;
        transicionandoPrimeiraPessoa = false;
        primeiraPessoaBlend = ativa ? 1f : 0f;
    }

    public void ForcarAngulos(float novoYaw, float novoPitch)
    {
        yaw = novoYaw;
        pitch = Mathf.Clamp(novoPitch, minPitch, maxPitch);
    }
}
