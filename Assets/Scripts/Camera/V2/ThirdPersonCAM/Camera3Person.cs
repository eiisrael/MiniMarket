using UnityEngine;

/// <summary>
/// MiniMarket Camera V2 - Third Person CAM.
///
/// Use no GameObject: ThirdPersonCAM.
/// Objetivo: câmera em terceira pessoa estilo GTA, fluida, segura e totalmente editável no Inspector em runtime.
///
/// Regras do V2:
/// - Este script é a autoridade da terceira pessoa.
/// - Não depende do CameraGTAFollowHardcore antigo.
/// - Não força valores do Inspector em loop.
/// - Evita pulo visual, snap agressivo, colisão no chão e câmera entrando no personagem.
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(20000)]
public class Camera3Person : MonoBehaviour
{
    [Header("Referências")]
    public UnityEngine.Camera camera3Person;
    public Transform target;
    public Transform lookAtOverride;

    [Header("Ativação")]
    public bool cameraAtiva = true;
    public bool controlarCameraComponent = true;
    public bool travarCursorAoAtivar = true;
    public bool aceitarInputMouse = true;
    public bool usarUnscaledTime = false;

    [Header("Input")]
    public string mouseXAxis = "Mouse X";
    public string mouseYAxis = "Mouse Y";
    [Range(0, 2)] public int botaoZoomMira = 1;
    public bool zoomEnquantoSeguraBotao = true;
    public bool inverterY = false;
    [Min(0f)] public float deadZoneMouse = 0.0008f;

    [Header("Sensibilidade")]
    [Min(1f)] public float sensibilidadeX = 180f;
    [Min(1f)] public float sensibilidadeY = 130f;
    [Min(0f)] public float multiplicadorMouseNoZoom = 0.82f;

    [Header("Órbita GTA")]
    public Vector3 targetOffset = new Vector3(0f, 1.45f, 0f);
    [Min(0.5f)] public float distancia = 4.25f;
    [Min(0f)] public float alturaExtra = 0.15f;
    public float offsetLateral = 0.45f;
    public float offsetLateralZoom = 0.18f;
    [Range(-89f, 89f)] public float minPitch = -35f;
    [Range(-89f, 89f)] public float maxPitch = 62f;
    public float yawInicial = 0f;
    public float pitchInicial = 18f;

    [Header("Suavização")]
    public bool usarSuavizacao = true;
    [Min(0f)] public float tempoSuavizacaoPosicao = 0.045f;
    [Min(0f)] public float velocidadeRotacao = 22f;
    [Min(0f)] public float maxDeltaPosicaoPorFrame = 1.4f;
    public bool resetarSuavizacaoAoTeleportar = true;
    [Min(0.1f)] public float distanciaTeleportReset = 5.5f;

    [Header("Auto Alinhar estilo GTA")]
    public bool autoAlinharAtrasDoPersonagem = false;
    [Min(0f)] public float delayAutoAlinhar = 2.0f;
    [Min(0f)] public float velocidadeAutoAlinhar = 4.0f;
    [Min(0f)] public float velocidadeMinimaParaAutoAlinhar = 0.08f;
    public bool bloquearAutoAlinharComMouse = true;
    public bool bloquearAutoAlinharNoZoom = true;

    [Header("Zoom / Mira")]
    public bool usarZoom = true;
    [Min(0.5f)] public float distanciaZoom = 2.85f;
    public float offsetLateralMira = 0.68f;
    [Range(1f, 179f)] public float fovNormal = 60f;
    [Range(1f, 179f)] public float fovZoom = 48f;
    [Min(0f)] public float velocidadeFov = 14f;
    [Min(0f)] public float velocidadeDistanciaZoom = 10f;

    [Header("Colisão / Anti Parede Seguro")]
    public bool usarColisao = true;
    public LayerMask layersColisao = ~0;
    [Min(0.02f)] public float raioColisao = 0.28f;
    [Min(0f)] public float margemParede = 0.18f;
    [Min(0.3f)] public float distanciaMinima = 1.25f;
    [Min(0.5f)] public float distanciaMinimaVisual = 2.05f;
    public bool ignorarTriggers = true;
    public bool ignorarProprioTarget = true;
    public bool ignorarGrabbables = true;
    public bool ignorarChaoSuperficiesHorizontais = true;
    [Range(0f, 1f)] public float normalYChao = 0.45f;
    [Min(0f)] public float ignorarHitMuitoPertoDoAlvo = 0.75f;

    [Header("Proteção Visual")]
    public bool limitarMudancaDistanciaPorFrame = true;
    [Min(0.01f)] public float maxDeltaDistanciaPorFrame = 0.18f;
    public bool preservarRotacaoDaOrbita = true;
    public bool desenharDebugColisao = false;

    [Header("Runtime / Debug")]
    public bool logarEventos = false;

    private readonly RaycastHit[] hits = new RaycastHit[32];
    private Vector3 velocidadePosicao;
    private Vector3 ultimaTargetPos;
    private bool possuiUltimaTarget;
    private float yaw;
    private float pitch;
    private float distanciaAtual;
    private float distanciaDesejadaAtual;
    private float tempoSemMouse;
    private bool inicializado;

    public float YawAtual => yaw;
    public float PitchAtual => pitch;
    public bool ZoomAtivo => usarZoom && zoomEnquantoSeguraBotao && Input.GetMouseButton(botaoZoomMira);
    public UnityEngine.Camera UnityCamera => camera3Person;

    private void Reset()
    {
        camera3Person = GetComponent<UnityEngine.Camera>();
        if (camera3Person == null)
            camera3Person = UnityEngine.Camera.main;
    }

    private void Awake()
    {
        ResolverReferencias();
        InicializarEstado();
    }

    private void OnEnable()
    {
        ResolverReferencias();
        AplicarAtivacaoCamera();
        InicializarEstado();
    }

    private void OnDisable()
    {
        if (controlarCameraComponent && camera3Person != null)
            camera3Person.enabled = false;
    }

    private void LateUpdate()
    {
        if (!cameraAtiva)
        {
            AplicarAtivacaoCamera();
            return;
        }

        ResolverReferencias();
        AplicarAtivacaoCamera();

        if (target == null || camera3Person == null)
            return;

        float dt = DeltaTimeSeguro();
        LerMouse(dt);
        AtualizarAutoAlinhar(dt);
        AtualizarCamera(dt);
    }

    public void DefinirTarget(Transform novoTarget, bool resetarAngulo = false)
    {
        target = novoTarget;
        possuiUltimaTarget = false;
        if (resetarAngulo)
            ReinicializarEstado();
    }

    public void DefinirAngulos(float novoYaw, float novoPitch)
    {
        yaw = novoYaw;
        pitch = Mathf.Clamp(novoPitch, minPitch, maxPitch);
    }

    private void ResolverReferencias()
    {
        if (camera3Person == null)
            camera3Person = GetComponent<UnityEngine.Camera>();

        if (camera3Person == null)
            camera3Person = UnityEngine.Camera.main;
    }

    private void InicializarEstado()
    {
        if (inicializado)
            return;

        ReinicializarEstado();
        inicializado = true;
    }

    private void ReinicializarEstado()
    {
        yaw = yawInicial != 0f ? yawInicial : transform.eulerAngles.y;
        pitch = Mathf.Clamp(pitchInicial, minPitch, maxPitch);
        distanciaAtual = distancia;
        distanciaDesejadaAtual = distancia;
        velocidadePosicao = Vector3.zero;

        if (target != null)
        {
            ultimaTargetPos = target.position;
            possuiUltimaTarget = true;
        }
    }

    private void AplicarAtivacaoCamera()
    {
        if (controlarCameraComponent && camera3Person != null)
            camera3Person.enabled = cameraAtiva;

        if (cameraAtiva && travarCursorAoAtivar)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    private void LerMouse(float dt)
    {
        if (!aceitarInputMouse || Cursor.lockState != CursorLockMode.Locked)
        {
            tempoSemMouse += dt;
            return;
        }

        float mouseX = Input.GetAxisRaw(mouseXAxis);
        float mouseY = Input.GetAxisRaw(mouseYAxis);
        Vector2 input = new Vector2(mouseX, mouseY);

        if (input.sqrMagnitude <= deadZoneMouse * deadZoneMouse)
        {
            tempoSemMouse += dt;
            return;
        }

        tempoSemMouse = 0f;
        float mult = ZoomAtivo ? Mathf.Max(0.01f, multiplicadorMouseNoZoom) : 1f;
        yaw += input.x * sensibilidadeX * mult * dt;
        pitch += (inverterY ? input.y : -input.y) * sensibilidadeY * mult * dt;
        pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
    }

    private void AtualizarAutoAlinhar(float dt)
    {
        if (!autoAlinharAtrasDoPersonagem || target == null)
            return;

        if (bloquearAutoAlinharComMouse && tempoSemMouse < delayAutoAlinhar)
            return;

        if (bloquearAutoAlinharNoZoom && ZoomAtivo)
            return;

        if (!possuiUltimaTarget)
        {
            ultimaTargetPos = target.position;
            possuiUltimaTarget = true;
            return;
        }

        Vector3 delta = target.position - ultimaTargetPos;
        delta.y = 0f;
        ultimaTargetPos = target.position;

        if (delta.magnitude < velocidadeMinimaParaAutoAlinhar * Mathf.Max(dt, 0.0001f))
            return;

        float alvoYaw = target.eulerAngles.y;
        yaw = Mathf.LerpAngle(yaw, alvoYaw, Suavizacao(velocidadeAutoAlinhar, dt));
    }

    private void AtualizarCamera(float dt)
    {
        Vector3 foco = ObterFoco();
        Quaternion orbit = Quaternion.Euler(pitch, yaw, 0f);
        bool zoom = ZoomAtivo;

        float distanciaAlvo = zoom ? distanciaZoom : distancia;
        float lateralAlvo = zoom ? offsetLateralMira : offsetLateral;

        distanciaDesejadaAtual = Mathf.Lerp(distanciaDesejadaAtual, distanciaAlvo, Suavizacao(velocidadeDistanciaZoom, dt));

        Vector3 direcaoTras = orbit * Vector3.back;
        Vector3 direcaoLado = orbit * Vector3.right;
        Vector3 posicaoIdeal = foco + direcaoTras * distanciaDesejadaAtual + direcaoLado * lateralAlvo + Vector3.up * alturaExtra;

        float distanciaNormal = Vector3.Distance(foco, posicaoIdeal);
        Vector3 direcao = distanciaNormal > 0.001f ? (posicaoIdeal - foco) / distanciaNormal : direcaoTras;
        float distanciaSegura = usarColisao ? CalcularDistanciaSegura(foco, direcao, distanciaNormal) : distanciaNormal;

        if (limitarMudancaDistanciaPorFrame)
            distanciaAtual = Mathf.MoveTowards(distanciaAtual, distanciaSegura, maxDeltaDistanciaPorFrame);
        else
            distanciaAtual = distanciaSegura;

        Vector3 posicaoFinal = foco + direcao * distanciaAtual;
        Quaternion rotacaoFinal = preservarRotacaoDaOrbita ? orbit : Quaternion.LookRotation(foco - posicaoFinal, Vector3.up);

        if (resetarSuavizacaoAoTeleportar && Vector3.Distance(transform.position, posicaoFinal) > distanciaTeleportReset)
        {
            velocidadePosicao = Vector3.zero;
            transform.position = posicaoFinal;
            transform.rotation = rotacaoFinal;
        }
        else
        {
            if (usarSuavizacao && tempoSuavizacaoPosicao > 0.0001f)
            {
                Vector3 novaPos = Vector3.SmoothDamp(transform.position, posicaoFinal, ref velocidadePosicao, tempoSuavizacaoPosicao, Mathf.Infinity, dt);

                if (maxDeltaPosicaoPorFrame > 0f && Vector3.Distance(transform.position, novaPos) > maxDeltaPosicaoPorFrame)
                    novaPos = Vector3.MoveTowards(transform.position, novaPos, maxDeltaPosicaoPorFrame);

                transform.position = novaPos;
            }
            else
            {
                transform.position = posicaoFinal;
                velocidadePosicao = Vector3.zero;
            }

            float tRot = Suavizacao(velocidadeRotacao, dt);
            transform.rotation = velocidadeRotacao <= 0.0001f ? rotacaoFinal : Quaternion.Slerp(transform.rotation, rotacaoFinal, tRot);
        }

        AtualizarFov(dt, zoom);

        if (desenharDebugColisao)
        {
            Debug.DrawLine(foco, posicaoIdeal, Color.red);
            Debug.DrawLine(foco, transform.position, Color.green);
        }
    }

    private Vector3 ObterFoco()
    {
        if (lookAtOverride != null)
            return lookAtOverride.position;

        return target.position + targetOffset;
    }

    private float CalcularDistanciaSegura(Vector3 foco, Vector3 direcao, float distanciaNormal)
    {
        float minPermitida = Mathf.Clamp(Mathf.Max(distanciaMinima, distanciaMinimaVisual), 0.05f, distanciaNormal);
        float segura = distanciaNormal;
        QueryTriggerInteraction triggerMode = ignorarTriggers ? QueryTriggerInteraction.Ignore : QueryTriggerInteraction.Collide;
        int count = Physics.SphereCastNonAlloc(foco, raioColisao, direcao, hits, distanciaNormal + margemParede, layersColisao, triggerMode);

        for (int i = 0; i < count; i++)
        {
            RaycastHit hit = hits[i];
            if (DeveIgnorarHit(hit))
                continue;

            float d = Mathf.Max(minPermitida, hit.distance - margemParede);
            if (d < segura)
                segura = d;
        }

        return Mathf.Clamp(segura, minPermitida, distanciaNormal);
    }

    private bool DeveIgnorarHit(RaycastHit hit)
    {
        if (hit.collider == null)
            return true;

        Collider col = hit.collider;
        if (!col.enabled || (col.isTrigger && ignorarTriggers))
            return true;

        if (hit.distance < ignorarHitMuitoPertoDoAlvo)
            return true;

        if (ignorarChaoSuperficiesHorizontais && hit.normal.y >= normalYChao)
            return true;

        if (ignorarProprioTarget && target != null)
        {
            Transform t = col.transform;
            if (t == target || t.IsChildOf(target))
                return true;
        }

        if (ignorarGrabbables && col.GetComponentInParent<GetItemObjectV2>() != null)
            return true;

        return false;
    }

    private void AtualizarFov(float dt, bool zoom)
    {
        if (camera3Person == null)
            return;

        float alvo = zoom ? fovZoom : fovNormal;
        camera3Person.fieldOfView = Mathf.Lerp(camera3Person.fieldOfView, alvo, Suavizacao(velocidadeFov, dt));
    }

    private float DeltaTimeSeguro()
    {
        float dt = usarUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        return Mathf.Clamp(dt, 0.0001f, 0.05f);
    }

    private float Suavizacao(float velocidade, float dt)
    {
        if (velocidade <= 0.0001f)
            return 1f;
        return 1f - Mathf.Exp(-velocidade * dt);
    }

    private void OnValidate()
    {
        if (maxPitch < minPitch)
            maxPitch = minPitch;

        distancia = Mathf.Max(0.5f, distancia);
        distanciaZoom = Mathf.Max(0.5f, distanciaZoom);
        distanciaMinima = Mathf.Max(0.3f, distanciaMinima);
        distanciaMinimaVisual = Mathf.Max(distanciaMinima, distanciaMinimaVisual);
        raioColisao = Mathf.Max(0.02f, raioColisao);
        margemParede = Mathf.Max(0f, margemParede);
    }
}
