using UnityEngine;

/// <summary>
/// MiniMarket Camera V2 - terceira pessoa estilo GTA.
/// A Camera usada por este script deve estar no mesmo GameObject do Camera3Person.
/// O script se auto-corrige para impedir que a referência aponte para a Main Camera antiga.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(UnityEngine.Camera))]
[DefaultExecutionOrder(20000)]
public class Camera3Person : MonoBehaviour
{
    [Header("Referências")]
    public UnityEngine.Camera camera3Person;
    public Transform target;
    public Transform lookAtOverride;

    [Header("Autoridade da câmera")]
    [Tooltip("Mantém camera3Person apontando para a Camera deste mesmo GameObject.")]
    public bool forcarCameraNoMesmoGameObject = true;

    [Tooltip("Corrige posição, referência e câmera ativa durante os primeiros segundos do Play.")]
    public bool autorrepararInicio = true;

    [Min(0.5f)] public float duracaoAutorreparoInicial = 4f;
    [Min(0.05f)] public float intervaloAutorreparoInicial = 0.15f;
    [Min(0.1f)] public float toleranciaPosicaoInvalida = 1.25f;

    [Header("Ativação")]
    public bool cameraAtiva = true;
    public bool controlarCameraComponent = true;
    public bool travarCursorAoAtivar = true;
    public bool aceitarInputMouse = true;
    public bool usarUnscaledTime = false;
    public bool posicionarImediatamenteAoAtivar = true;

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
    [Min(0f)] public float delayAutoAlinhar = 2f;
    [Min(0f)] public float velocidadeAutoAlinhar = 4f;
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
    private UnityEngine.Camera cameraLocal;
    private Vector3 velocidadePosicao;
    private Vector3 ultimaTargetPos;
    private bool possuiUltimaTarget;
    private float yaw;
    private float pitch;
    private float distanciaAtual;
    private float distanciaDesejadaAtual;
    private float distanciaSeguraAtual;
    private float tempoSemMouse;
    private float tempoAtivacao;
    private float proximoAutorreparo;
    private bool inicializado;
    private bool colisaoNoFrame;
    private int reparosIniciais;
    private string ultimoColliderColisao = string.Empty;

    public float YawAtual => yaw;
    public float PitchAtual => pitch;
    public float DistanciaAtual => distanciaAtual;
    public float DistanciaSeguraAtual => distanciaSeguraAtual;
    public float FovAtual => camera3Person != null ? camera3Person.fieldOfView : 0f;
    public bool ColisaoNoFrame => colisaoNoFrame;
    public string UltimoColliderColisao => ultimoColliderColisao;
    public bool InputMouseAtivo => cameraAtiva && aceitarInputMouse && !CameraV2MenuInputBlocker.MenuAberto;
    public bool ZoomAtivo => InputMouseAtivo && usarZoom && zoomEnquantoSeguraBotao && Input.GetMouseButton(botaoZoomMira);
    public UnityEngine.Camera UnityCamera => camera3Person;
    public bool ReferenciaCameraEhLocal => camera3Person != null && camera3Person.gameObject == gameObject;
    public int ReparosIniciais => reparosIniciais;

    private Transform CameraTransform => camera3Person != null ? camera3Person.transform : transform;

    private void Reset()
    {
        cameraLocal = GetComponent<UnityEngine.Camera>();
        camera3Person = cameraLocal;
    }

    private void Awake()
    {
        tempoAtivacao = Time.unscaledTime;
        ResolverReferencias(true);
        InicializarEstado();

        if (cameraAtiva && posicionarImediatamenteAoAtivar)
            ForcarAtualizacaoImediata();
    }

    private void Start()
    {
        ResolverReferencias(true);

        if (cameraAtiva && posicionarImediatamenteAoAtivar)
            ForcarAtualizacaoImediata();
    }

    private void OnEnable()
    {
        tempoAtivacao = Time.unscaledTime;
        proximoAutorreparo = 0f;
        ResolverReferencias(true);
        InicializarEstado();
        AplicarAtivacaoCamera();

        if (cameraAtiva && posicionarImediatamenteAoAtivar)
            ForcarAtualizacaoImediata();
    }

    private void OnDisable()
    {
        if (controlarCameraComponent && camera3Person != null && camera3Person.enabled)
            camera3Person.enabled = false;
    }

    private void LateUpdate()
    {
        ResolverReferencias(false);
        AplicarAtivacaoCamera();

        if (!cameraAtiva || target == null || camera3Person == null)
            return;

        float dt = DeltaTimeSeguro();
        LerMouse(dt);
        AtualizarAutoAlinhar(dt);
        AtualizarCamera(dt);
        ExecutarAutorreparoInicial();
    }

    public void SetAtiva(bool ativa)
    {
        bool estavaAtiva = cameraAtiva;
        cameraAtiva = ativa;
        AplicarAtivacaoCamera();

        if (ativa && (!estavaAtiva || posicionarImediatamenteAoAtivar))
        {
            tempoAtivacao = Time.unscaledTime;
            proximoAutorreparo = 0f;
            ForcarAtualizacaoImediata();
        }
    }

    public void DefinirTarget(Transform novoTarget, bool resetarAngulo = false)
    {
        target = novoTarget;
        possuiUltimaTarget = false;

        if (resetarAngulo)
            ReinicializarEstado();

        if (cameraAtiva && posicionarImediatamenteAoAtivar)
            ForcarAtualizacaoImediata();
    }

    public void DefinirAngulos(float novoYaw, float novoPitch)
    {
        yaw = novoYaw;
        pitch = Mathf.Clamp(novoPitch, minPitch, maxPitch);
    }

    public void ForcarAtualizacaoImediata()
    {
        ResolverReferencias(true);
        InicializarEstado();

        if (!cameraAtiva || camera3Person == null || target == null)
            return;

        CalcularPoseDesejada(false, out Vector3 posicaoFinal, out Quaternion rotacaoFinal, out float distanciaCalculada);

        distanciaDesejadaAtual = Mathf.Max(0.5f, distancia);
        distanciaSeguraAtual = distanciaCalculada;
        distanciaAtual = distanciaCalculada;
        velocidadePosicao = Vector3.zero;

        CameraTransform.SetPositionAndRotation(posicaoFinal, rotacaoFinal);
        camera3Person.fieldOfView = fovNormal;

        ultimaTargetPos = target.position;
        possuiUltimaTarget = true;
    }

    private void ResolverReferencias(bool forcar)
    {
        if (cameraLocal == null || forcar)
            cameraLocal = GetComponent<UnityEngine.Camera>();

        if (forcarCameraNoMesmoGameObject && cameraLocal != null)
        {
            if (camera3Person != cameraLocal)
            {
                camera3Person = cameraLocal;
                reparosIniciais++;
            }
        }
        else if (camera3Person == null)
        {
            camera3Person = cameraLocal;
        }
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
        yaw = Mathf.Abs(yawInicial) > 0.001f ? yawInicial : transform.eulerAngles.y;
        pitch = Mathf.Clamp(pitchInicial, minPitch, maxPitch);
        distanciaAtual = Mathf.Max(0.5f, distancia);
        distanciaDesejadaAtual = distanciaAtual;
        distanciaSeguraAtual = distanciaAtual;
        velocidadePosicao = Vector3.zero;

        if (target != null)
        {
            ultimaTargetPos = target.position;
            possuiUltimaTarget = true;
        }
    }

    private void AplicarAtivacaoCamera()
    {
        if (controlarCameraComponent && camera3Person != null && camera3Person.enabled != cameraAtiva)
            camera3Person.enabled = cameraAtiva;

        if (!cameraAtiva || !travarCursorAoAtivar || CameraV2MenuInputBlocker.MenuAberto)
            return;

        if (Cursor.lockState != CursorLockMode.Locked)
            Cursor.lockState = CursorLockMode.Locked;

        if (Cursor.visible)
            Cursor.visible = false;
    }

    private void ExecutarAutorreparoInicial()
    {
        if (!autorrepararInicio || Time.unscaledTime - tempoAtivacao > duracaoAutorreparoInicial)
            return;

        if (Time.unscaledTime < proximoAutorreparo)
            return;

        proximoAutorreparo = Time.unscaledTime + Mathf.Max(0.05f, intervaloAutorreparoInicial);
        ResolverReferencias(true);

        if (camera3Person == null || target == null)
            return;

        CalcularPoseDesejada(ZoomAtivo, out Vector3 posicaoEsperada, out _, out _);
        float erro = Vector3.Distance(CameraTransform.position, posicaoEsperada);

        bool referenciaErrada = !ReferenciaCameraEhLocal;
        bool cameraDesligada = !camera3Person.enabled;
        bool transformInvalido = float.IsNaN(CameraTransform.position.x) || float.IsInfinity(CameraTransform.position.x);
        bool muitoFora = erro > Mathf.Max(0.1f, toleranciaPosicaoInvalida);

        if (!referenciaErrada && !cameraDesligada && !transformInvalido && !muitoFora)
            return;

        reparosIniciais++;
        camera3Person.enabled = true;
        ForcarAtualizacaoImediata();

        if (logarEventos)
            Debug.Log("[Camera3Person] Autorreparo inicial aplicado. Erro de posição: " + erro.ToString("0.00") + "m");
    }

    private void LerMouse(float dt)
    {
        if (!InputMouseAtivo || Cursor.lockState != CursorLockMode.Locked)
        {
            tempoSemMouse += dt;
            return;
        }

        float mouseX = Input.GetAxisRaw(mouseXAxis);
        float mouseY = Input.GetAxisRaw(mouseYAxis);
        float magnitudeQuadrada = mouseX * mouseX + mouseY * mouseY;

        if (magnitudeQuadrada <= deadZoneMouse * deadZoneMouse)
        {
            tempoSemMouse += dt;
            return;
        }

        tempoSemMouse = 0f;
        float mult = ZoomAtivo ? Mathf.Max(0.01f, multiplicadorMouseNoZoom) : 1f;
        yaw += mouseX * sensibilidadeX * mult * dt;
        pitch += (inverterY ? mouseY : -mouseY) * sensibilidadeY * mult * dt;
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

        float limite = velocidadeMinimaParaAutoAlinhar * Mathf.Max(dt, 0.0001f);
        if (delta.sqrMagnitude < limite * limite)
            return;

        yaw = Mathf.LerpAngle(yaw, target.eulerAngles.y, Suavizacao(velocidadeAutoAlinhar, dt));
    }

    private void AtualizarCamera(float dt)
    {
        bool zoom = ZoomAtivo;
        CalcularPoseDesejada(zoom, out Vector3 posicaoFinal, out Quaternion rotacaoFinal, out float distanciaCalculada);

        distanciaSeguraAtual = distanciaCalculada;

        if (limitarMudancaDistanciaPorFrame)
            distanciaAtual = Mathf.MoveTowards(distanciaAtual, distanciaSeguraAtual, maxDeltaDistanciaPorFrame);
        else
            distanciaAtual = distanciaSeguraAtual;

        Vector3 foco = ObterFoco();
        Vector3 direcao = (posicaoFinal - foco).normalized;
        posicaoFinal = foco + direcao * distanciaAtual;

        Transform camTransform = CameraTransform;

        if (resetarSuavizacaoAoTeleportar && Vector3.Distance(camTransform.position, posicaoFinal) > distanciaTeleportReset)
        {
            velocidadePosicao = Vector3.zero;
            camTransform.SetPositionAndRotation(posicaoFinal, rotacaoFinal);
        }
        else
        {
            if (usarSuavizacao && tempoSuavizacaoPosicao > 0.0001f)
            {
                Vector3 novaPos = Vector3.SmoothDamp(camTransform.position, posicaoFinal, ref velocidadePosicao, tempoSuavizacaoPosicao, Mathf.Infinity, dt);
                if (maxDeltaPosicaoPorFrame > 0f)
                    novaPos = Vector3.MoveTowards(camTransform.position, novaPos, maxDeltaPosicaoPorFrame);
                camTransform.position = novaPos;
            }
            else
            {
                camTransform.position = posicaoFinal;
                velocidadePosicao = Vector3.zero;
            }

            camTransform.rotation = velocidadeRotacao <= 0.0001f
                ? rotacaoFinal
                : Quaternion.Slerp(camTransform.rotation, rotacaoFinal, Suavizacao(velocidadeRotacao, dt));
        }

        AtualizarFov(dt, zoom);

        if (desenharDebugColisao)
        {
            Debug.DrawLine(foco, posicaoFinal, Color.green);
        }
    }

    private void CalcularPoseDesejada(bool zoom, out Vector3 posicaoFinal, out Quaternion rotacaoFinal, out float distanciaCalculada)
    {
        Vector3 foco = ObterFoco();
        Quaternion orbit = Quaternion.Euler(pitch, yaw, 0f);
        float distanciaAlvo = zoom ? distanciaZoom : distancia;
        float lateralAlvo = zoom ? offsetLateralMira : offsetLateral;

        distanciaDesejadaAtual = Mathf.Lerp(
            distanciaDesejadaAtual,
            distanciaAlvo,
            Suavizacao(velocidadeDistanciaZoom, DeltaTimeSeguro())
        );

        Vector3 posicaoIdeal = foco +
                               orbit * Vector3.back * distanciaDesejadaAtual +
                               orbit * Vector3.right * lateralAlvo +
                               Vector3.up * alturaExtra;

        Vector3 vetor = posicaoIdeal - foco;
        float distanciaNormal = vetor.magnitude;

        if (distanciaNormal <= 0.001f)
        {
            distanciaCalculada = Mathf.Max(0.5f, distanciaAlvo);
            posicaoFinal = foco + Vector3.back * distanciaCalculada;
            rotacaoFinal = orbit;
            return;
        }

        Vector3 direcao = vetor / distanciaNormal;
        distanciaCalculada = usarColisao
            ? CalcularDistanciaSegura(foco, direcao, distanciaNormal)
            : distanciaNormal;

        posicaoFinal = foco + direcao * distanciaCalculada;
        rotacaoFinal = preservarRotacaoDaOrbita
            ? orbit
            : Quaternion.LookRotation(foco - posicaoFinal, Vector3.up);
    }

    private Vector3 ObterFoco()
    {
        return lookAtOverride != null ? lookAtOverride.position : target.position + targetOffset;
    }

    private float CalcularDistanciaSegura(Vector3 foco, Vector3 direcao, float distanciaNormal)
    {
        float minima = Mathf.Clamp(Mathf.Max(distanciaMinima, distanciaMinimaVisual), 0.05f, distanciaNormal);
        float segura = distanciaNormal;
        colisaoNoFrame = false;
        ultimoColliderColisao = string.Empty;

        QueryTriggerInteraction modoTrigger = ignorarTriggers ? QueryTriggerInteraction.Ignore : QueryTriggerInteraction.Collide;
        int count = Physics.SphereCastNonAlloc(foco, raioColisao, direcao, hits, distanciaNormal + margemParede, layersColisao, modoTrigger);

        for (int i = 0; i < count; i++)
        {
            RaycastHit hit = hits[i];
            if (DeveIgnorarHit(hit))
                continue;

            float d = Mathf.Max(minima, hit.distance - margemParede);
            if (d < segura)
            {
                segura = d;
                colisaoNoFrame = true;
                ultimoColliderColisao = hit.collider != null ? hit.collider.name : string.Empty;
            }
        }

        return Mathf.Clamp(segura, minima, distanciaNormal);
    }

    private bool DeveIgnorarHit(RaycastHit hit)
    {
        Collider col = hit.collider;
        if (col == null || !col.enabled || (col.isTrigger && ignorarTriggers))
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
        if (Mathf.Abs(camera3Person.fieldOfView - alvo) <= 0.01f)
        {
            camera3Person.fieldOfView = alvo;
            return;
        }

        camera3Person.fieldOfView = Mathf.Lerp(camera3Person.fieldOfView, alvo, Suavizacao(velocidadeFov, dt));
    }

    private float DeltaTimeSeguro()
    {
        float dt = usarUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        return Mathf.Clamp(dt, 0.0001f, 0.05f);
    }

    private float Suavizacao(float velocidade, float dt)
    {
        return velocidade <= 0.0001f ? 1f : 1f - Mathf.Exp(-velocidade * dt);
    }

    private void OnValidate()
    {
        cameraLocal = GetComponent<UnityEngine.Camera>();

        if (forcarCameraNoMesmoGameObject && cameraLocal != null)
            camera3Person = cameraLocal;

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
