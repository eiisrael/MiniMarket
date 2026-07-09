using UnityEngine;

/// <summary>
/// Camera principal do personagem.
///
/// Ajuste atual:
/// - Remove o efeito de jumping na primeira pessoa/mira.
/// - Mantem o mesmo yaw/pitch quando entra/sai da primeira pessoa.
/// - Usa a mesma sensibilidade da Main Camera, salvo se voce desligar a opcao no Inspector.
/// - Evita que a rotacao do personagem realimente a posicao da camera e cause tranco lateral.
/// - Mantem zoom/mira por distancia ajustavel em tempo real.
/// </summary>
public class CameraGTAFollowHardcore : MonoBehaviour
{
    [Header("Alvo")]
    public Transform target;

    [Header("Distância / Enquadramento")]
    public Vector3 targetOffset = new Vector3(0f, 1.45f, 0f);
    public float distance = 4.2f;
    public float height = 1.2f;

    [Header("Mouse")]
    public float mouseSensitivityX = 180f;
    public float mouseSensitivityY = 120f;
    public bool invertY = false;

    [Header("Sensibilidade da Mira")]
    [Tooltip("Recomendado desligado: primeira pessoa/mira usa a mesma sensibilidade da Main Camera.")]
    public bool usarSensibilidadeSeparadaNaMira = false;

    [Range(0, 2)]
    public int botaoSensibilidadeMira = 1;

    public float mouseSensitivityXMira = 180f;
    public float mouseSensitivityYMira = 120f;

    [Header("Suavização do Mouse")]
    public bool suavizarMouse = true;

    [Min(0f)]
    public float mouseSmoothTimeTerceiraPessoa = 0.015f;

    [Min(0f)]
    public float mouseSmoothTimePrimeiraPessoa = 0.015f;

    [Tooltip("Forca a primeira pessoa a usar a mesma sensibilidade e suavizacao da Main Camera.")]
    public bool usarMesmaSensibilidadeDaMainCameraNaPrimeiraPessoa = true;

    [Min(0f)]
    public float mouseDeadZone = 0.001f;

    [Header("Limites Verticais")]
    public float minPitch = -25f;
    public float maxPitch = 55f;

    [Header("Suavização Terceira Pessoa")]
    public float positionSmoothTime = 0.06f;
    public float rotationSmoothTime = 0.04f;

    [Header("Auto Alinhar Atrás do Personagem")]
    public bool autoAlignBehindPlayer = true;
    public float autoAlignDelay = 1.2f;
    public float autoAlignSpeed = 70f;

    [Header("Proteção Contra Reset de Ângulo")]
    public bool protegerContraResetDeAngulo = true;
    public bool bloquearAutoAlignDuranteMira = true;

    [Range(5f, 180f)]
    public float anguloMaximoAutoAlignSemReset = 35f;

    [Min(0.01f)]
    public float autoAlignSmoothTime = 0.35f;

    [Header("Primeira Pessoa")]
    public bool permitirPrimeiraPessoa = true;
    public Transform pontoPrimeiraPessoa;
    public Vector3 offsetPrimeiraPessoa = new Vector3(0f, 1.68f, 0.32f);
    public Vector3 ajusteLocalPontoPrimeiraPessoa = new Vector3(0f, 0f, 0.12f);

    [Header("Primeira Pessoa Anti-Jumping")]
    [Tooltip("Mantem exatamente o angulo atual da camera ao entrar/sair da primeira pessoa. Remove o snap/recenter.")]
    public bool preservarAnguloAtualNaTransicao = true;

    [Tooltip("Usa uma posicao de primeira pessoa calculada em mundo, sem depender da rotacao local do ponto filho. Evita o pulo lateral ao olhar para os lados.")]
    public bool usarPosicaoPrimeiraPessoaEstavel = true;

    [Tooltip("Quando ligado, a camera nao rotaciona o corpo no mesmo frame usando transform do alvo para calcular a posicao. Reduz jumping/tontura.")]
    public bool evitarRealimentacaoRotacaoPersonagemCamera = true;

    [Tooltip("Sincroniza o corpo com a camera apenas de forma suave, sem puxar a camera.")]
    public bool sincronizarCorpoSuaveNaPrimeiraPessoa = true;

    [Min(0.1f)]
    public float velocidadeSincronizarCorpoPrimeiraPessoa = 8f;

    [Header("Mira / Zoom Profissional")]
    [Tooltip("Use somente se quiser forcar a mira para a frente do personagem. Recomendado desligado para evitar snap.")]
    public bool alinharComFrenteDoPersonagemAoEntrarNaMira = false;

    [Tooltip("Use somente se quiser forcar pitch reto. Recomendado desligado para manter a direcao atual.")]
    public bool corrigirPitchAoEntrarNaMira = false;

    [Range(-30f, 30f)]
    public float pitchInicialAoEntrarNaMira = 0f;

    [Tooltip("Usa uma camera de mira por distancia. Mais estavel para terceira pessoa.")]
    public bool usarZoomMiraPorDistancia = true;

    [Min(0.15f)]
    public float distanciaZoomMira = 1.25f;

    public float alturaZoomMira = 1.58f;
    public float deslocamentoLateralZoomMira = 0f;

    [Min(0.5f)]
    public float distanciaOlharFrenteZoomMira = 8f;

    public bool aplicarColisaoNoZoomMira = true;

    [Min(0.1f)]
    public float velocidadeEntradaZoomMira = 2.2f;

    [Header("FOV da Mira / Zoom")]
    public bool ajustarFovNaMira = true;
    public bool capturarFovNormalNoStart = true;

    [Min(1f)]
    public float fovNormal = 60f;

    [Range(25f, 75f)]
    public float fovMira = 50f;

    [Min(0.1f)]
    public float velocidadeFovMira = 8f;

    [Header("Transição Anti-Enjoo")]
    [Min(0.1f)]
    public float velocidadeEntradaPrimeiraPessoa = 4.2f;

    [Min(0.1f)]
    public float velocidadeSaidaPrimeiraPessoa = 1.6f;

    [Min(0.01f)]
    public float positionSmoothTimePrimeiraPessoa = 0.06f;

    [Min(0.01f)]
    public float rotationSmoothTimePrimeiraPessoa = 0.04f;

    [Min(0.01f)]
    public float positionSmoothTimeSaidaPrimeiraPessoa = 0.18f;

    [Min(0.01f)]
    public float rotationSmoothTimeSaidaPrimeiraPessoa = 0.12f;

    [Header("Rotação do Personagem na Mira")]
    public bool rotacionarPersonagemNaPrimeiraPessoa = true;

    [Min(0.1f)]
    public float velocidadeRotacaoPersonagemPrimeiraPessoa = 10f;

    [Header("Anti Atravessar Cabeça")]
    public bool ocultarRenderersNaPrimeiraPessoa = true;
    public bool autoEncontrarRenderersDoTarget = true;

    [Range(0f, 1f)]
    public float ocultarRenderersQuandoBlendMaiorQue = 0.08f;

    public Renderer[] renderersParaOcultar;

    [Header("Colisão da Câmera - Anti Parede Seguro")]
    public bool usarColisaoCamera = true;
    public LayerMask cameraCollisionLayers = ~0;

    [Min(0.01f)]
    public float cameraCollisionRadius = 0.30f;

    [Min(0f)]
    public float cameraCollisionOffset = 0.18f;

    [Min(0.05f)]
    public float cameraMinDistanceFromFocus = 0.75f;

    public bool usarAntiGrudarExtra = true;

    [Range(3, 14)]
    public int passosBuscaAntiGrudar = 8;

    public bool corrigirPosicaoFinalSuavizada = true;

    [Header("Debug")]
    public bool desenharDebugColisaoCamera = false;

    [Header("Cursor")]
    public bool lockCursorOnStart = true;
    public KeyCode unlockCursorKey = KeyCode.Escape;
    public KeyCode lockCursorKey = KeyCode.Mouse0;

    private Camera cameraComponente;

    private float yaw;
    private float pitch = 15f;
    private float timeWithoutMouse;
    private float yawAutoAlignVelocity;

    private Vector3 positionVelocity;
    private Quaternion rotationVelocity;
    private Vector2 mouseSuavizado;
    private Vector2 mouseSmoothVelocity;
    private Vector3 lastTargetPosition;

    private bool primeiraPessoaSolicitada;
    private float primeiraPessoaBlend;

    private readonly RaycastHit[] cameraHits = new RaycastHit[32];
    private readonly Collider[] cameraOverlaps = new Collider[32];

    public bool EstaEmPrimeiraPessoa => primeiraPessoaSolicitada || primeiraPessoaBlend > 0.15f;
    public bool EstaTransicionandoPrimeiraPessoa => primeiraPessoaBlend > 0.01f && primeiraPessoaBlend < 0.99f;
    public float YawAtual => yaw;
    public float PitchAtual => pitch;
    public float DistanciaZoomMiraAtual => distanciaZoomMira;

    private void Start()
    {
        cameraComponente = GetComponent<Camera>();

        if (cameraComponente != null && capturarFovNormalNoStart)
            fovNormal = cameraComponente.fieldOfView;

        if (target == null && transform.parent != null)
            target = transform.parent;

        if (target != null)
        {
            yaw = transform.eulerAngles.y;
            if (Mathf.Abs(yaw) < 0.001f)
                yaw = target.eulerAngles.y;

            pitch = NormalizarPitch(transform.eulerAngles.x, pitch);
            lastTargetPosition = target.position;
        }

        if (
            autoEncontrarRenderersDoTarget &&
            target != null &&
            (renderersParaOcultar == null || renderersParaOcultar.Length == 0)
        )
        {
            renderersParaOcultar = target.GetComponentsInChildren<Renderer>(true);
        }

        AplicarPreferenciasSensibilidade();
        ResetarSuavizacoes();
        AplicarVisibilidadeRenderers(true);

        if (lockCursorOnStart)
            LockCursor();
    }

    private void OnDisable()
    {
        AplicarVisibilidadeRenderers(true);

        if (cameraComponente != null && ajustarFovNaMira)
            cameraComponente.fieldOfView = fovNormal;
    }

    private void LateUpdate()
    {
        HandleCursor();

        if (target == null)
            return;

        AplicarPreferenciasSensibilidade();

        if (Cursor.lockState == CursorLockMode.Locked)
            HandleMouse();

        AtualizarBlendPrimeiraPessoa();

        if (!EstaEmPrimeiraPessoa)
            HandleAutoAlign();

        UpdateCameraUnificada();
        AtualizarFovMira();
        AtualizarVisibilidadeDuranteTransicao();
    }

    public void SetPrimeiraPessoa(bool ativa)
    {
        if (!permitirPrimeiraPessoa)
            ativa = false;

        if (primeiraPessoaSolicitada == ativa)
            return;

        primeiraPessoaSolicitada = ativa;
        ResetarSuavizacoes();
        yawAutoAlignVelocity = 0f;
        timeWithoutMouse = 0f;

        if (!preservarAnguloAtualNaTransicao && primeiraPessoaSolicitada)
        {
            if (target != null && alinharComFrenteDoPersonagemAoEntrarNaMira)
                yaw = target.eulerAngles.y;

            if (corrigirPitchAoEntrarNaMira)
                pitch = Mathf.Clamp(pitchInicialAoEntrarNaMira, minPitch, maxPitch);
        }

        if (target != null)
            lastTargetPosition = target.position;
    }

    public void DefinirDistanciaZoomMira(float novaDistancia)
    {
        distanciaZoomMira = Mathf.Max(0.15f, novaDistancia);
    }

    private void HandleMouse()
    {
        float mouseXRaw = Input.GetAxisRaw("Mouse X");
        float mouseYRaw = Input.GetAxisRaw("Mouse Y");

        Vector2 mouseInput = new Vector2(mouseXRaw, mouseYRaw);

        if (mouseInput.sqrMagnitude <= mouseDeadZone * mouseDeadZone)
        {
            mouseInput = Vector2.zero;
            timeWithoutMouse += Time.deltaTime;
        }
        else
        {
            timeWithoutMouse = 0f;
            yawAutoAlignVelocity = 0f;
        }

        if (suavizarMouse)
        {
            float smoothTime = EstaEmPrimeiraPessoa
                ? mouseSmoothTimePrimeiraPessoa
                : mouseSmoothTimeTerceiraPessoa;

            mouseSuavizado = Vector2.SmoothDamp(mouseSuavizado, mouseInput, ref mouseSmoothVelocity, smoothTime);
        }
        else
        {
            mouseSuavizado = mouseInput;
        }

        bool podeUsarSensibilidadeSeparada =
            usarSensibilidadeSeparadaNaMira &&
            !usarMesmaSensibilidadeDaMainCameraNaPrimeiraPessoa &&
            Input.GetMouseButton(botaoSensibilidadeMira);

        float sensibilidadeXAtual = podeUsarSensibilidadeSeparada ? mouseSensitivityXMira : mouseSensitivityX;
        float sensibilidadeYAtual = podeUsarSensibilidadeSeparada ? mouseSensitivityYMira : mouseSensitivityY;

        yaw += mouseSuavizado.x * sensibilidadeXAtual * Time.deltaTime;

        if (invertY)
            pitch += mouseSuavizado.y * sensibilidadeYAtual * Time.deltaTime;
        else
            pitch -= mouseSuavizado.y * sensibilidadeYAtual * Time.deltaTime;

        pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
    }

    private void AtualizarBlendPrimeiraPessoa()
    {
        float alvo = primeiraPessoaSolicitada ? 1f : 0f;

        float velocidade = primeiraPessoaSolicitada && usarZoomMiraPorDistancia
            ? velocidadeEntradaZoomMira
            : (primeiraPessoaSolicitada ? velocidadeEntradaPrimeiraPessoa : velocidadeSaidaPrimeiraPessoa);

        primeiraPessoaBlend = Mathf.MoveTowards(primeiraPessoaBlend, alvo, velocidade * Time.deltaTime);
    }

    private void UpdateCameraUnificada()
    {
        Vector3 focusPoint = target.position + targetOffset;
        Quaternion orbitRotation = Quaternion.Euler(pitch, yaw, 0f);

        Vector3 terceiraPessoaPosition =
            focusPoint
            - orbitRotation * Vector3.forward * distance
            + Vector3.up * height;

        terceiraPessoaPosition = AplicarColisaoCameraSegura(focusPoint, terceiraPessoaPosition);

        Quaternion terceiraPessoaRotation = Quaternion.LookRotation(focusPoint - terceiraPessoaPosition, Vector3.up);

        Vector3 primeiraPessoaPosition;
        Quaternion primeiraPessoaRotation;

        if (usarZoomMiraPorDistancia)
        {
            primeiraPessoaPosition = CalcularPosicaoZoomMiraPorDistancia();
            primeiraPessoaRotation = CalcularRotacaoZoomMira(primeiraPessoaPosition);
        }
        else
        {
            primeiraPessoaPosition = CalcularPosicaoPrimeiraPessoaEstavel();
            primeiraPessoaRotation = Quaternion.Euler(pitch, yaw, 0f);
        }

        float blendSuave = SmoothStep01(primeiraPessoaBlend);

        Vector3 desiredPosition = Vector3.Lerp(terceiraPessoaPosition, primeiraPessoaPosition, blendSuave);
        Quaternion desiredRotation = Quaternion.Slerp(terceiraPessoaRotation, primeiraPessoaRotation, blendSuave);

        float smoothPositionAtual;
        float smoothRotationAtual;

        if (!primeiraPessoaSolicitada && primeiraPessoaBlend > 0.01f)
        {
            smoothPositionAtual = positionSmoothTimeSaidaPrimeiraPessoa;
            smoothRotationAtual = rotationSmoothTimeSaidaPrimeiraPessoa;
        }
        else
        {
            smoothPositionAtual = Mathf.Lerp(positionSmoothTime, positionSmoothTimePrimeiraPessoa, blendSuave);
            smoothRotationAtual = Mathf.Lerp(rotationSmoothTime, rotationSmoothTimePrimeiraPessoa, blendSuave);
        }

        Vector3 novaPosicao = Vector3.SmoothDamp(transform.position, desiredPosition, ref positionVelocity, smoothPositionAtual);

        bool deveCorrigirPosicaoFinal =
            usarColisaoCamera &&
            corrigirPosicaoFinalSuavizada &&
            (blendSuave < 0.95f || (usarZoomMiraPorDistancia && aplicarColisaoNoZoomMira));

        if (deveCorrigirPosicaoFinal)
            novaPosicao = AplicarColisaoCameraSegura(focusPoint, novaPosicao);

        transform.position = novaPosicao;
        transform.rotation = SmoothDampQuaternion(transform.rotation, desiredRotation, ref rotationVelocity, smoothRotationAtual);

        SincronizarCorpoComCameraSemJumping();
        lastTargetPosition = target.position;
    }

    private Vector3 CalcularPosicaoPrimeiraPessoaEstavel()
    {
        if (!usarPosicaoPrimeiraPessoaEstavel && pontoPrimeiraPessoa != null)
            return pontoPrimeiraPessoa.TransformPoint(ajusteLocalPontoPrimeiraPessoa);

        float altura = offsetPrimeiraPessoa.y;
        Vector3 offsetPlano = new Vector3(offsetPrimeiraPessoa.x, 0f, offsetPrimeiraPessoa.z);

        if (pontoPrimeiraPessoa != null)
        {
            altura = pontoPrimeiraPessoa.position.y - target.position.y;
            offsetPlano += new Vector3(ajusteLocalPontoPrimeiraPessoa.x, 0f, ajusteLocalPontoPrimeiraPessoa.z);
        }

        Quaternion yawRotation = Quaternion.Euler(0f, yaw, 0f);
        return target.position + Vector3.up * altura + yawRotation * offsetPlano;
    }

    private Vector3 CalcularPosicaoZoomMiraPorDistancia()
    {
        Quaternion yawRotation = Quaternion.Euler(0f, yaw, 0f);
        Vector3 focoMira = target.position + Vector3.up * alturaZoomMira;

        Vector3 posicao =
            focoMira
            - yawRotation * Vector3.forward * distanciaZoomMira
            + yawRotation * Vector3.right * deslocamentoLateralZoomMira;

        if (usarColisaoCamera && aplicarColisaoNoZoomMira)
            posicao = AplicarColisaoCameraSegura(focoMira, posicao);

        return posicao;
    }

    private Quaternion CalcularRotacaoZoomMira(Vector3 cameraPosition)
    {
        Quaternion rotacaoOlhar = Quaternion.Euler(pitch, yaw, 0f);
        Vector3 focoMira = target.position + Vector3.up * alturaZoomMira;
        Vector3 pontoOlhar = focoMira + rotacaoOlhar * Vector3.forward * distanciaOlharFrenteZoomMira;
        Vector3 direcao = pontoOlhar - cameraPosition;

        if (direcao.sqrMagnitude <= 0.0001f)
            return Quaternion.Euler(pitch, yaw, 0f);

        return Quaternion.LookRotation(direcao.normalized, Vector3.up);
    }

    private void SincronizarCorpoComCameraSemJumping()
    {
        if (!rotacionarPersonagemNaPrimeiraPessoa || target == null || !EstaEmPrimeiraPessoa)
            return;

        if (!sincronizarCorpoSuaveNaPrimeiraPessoa)
            return;

        Quaternion rotacaoAlvoDoPersonagem = Quaternion.Euler(0f, yaw, 0f);
        float velocidade = evitarRealimentacaoRotacaoPersonagemCamera
            ? velocidadeSincronizarCorpoPrimeiraPessoa
            : velocidadeRotacaoPersonagemPrimeiraPessoa;

        float suavizacaoRotacaoPlayer = CalcularSuavizacao(velocidade, Time.deltaTime);
        target.rotation = Quaternion.Slerp(target.rotation, rotacaoAlvoDoPersonagem, suavizacaoRotacaoPlayer);
    }

    private void AtualizarFovMira()
    {
        if (!ajustarFovNaMira)
            return;

        if (cameraComponente == null)
            cameraComponente = GetComponent<Camera>();

        if (cameraComponente == null)
            return;

        float alvoFov = primeiraPessoaSolicitada ? fovMira : fovNormal;
        float suavizacao = CalcularSuavizacao(velocidadeFovMira, Time.deltaTime);
        cameraComponente.fieldOfView = Mathf.Lerp(cameraComponente.fieldOfView, alvoFov, suavizacao);
    }

    private Vector3 AplicarColisaoCameraSegura(Vector3 focusPoint, Vector3 desiredPosition)
    {
        if (!usarColisaoCamera)
            return desiredPosition;

        Vector3 direction = desiredPosition - focusPoint;
        float targetDistance = direction.magnitude;

        if (targetDistance <= 0.001f)
            return desiredPosition;

        direction /= targetDistance;
        float distanciaSegura = targetDistance;

        int hitCount = Physics.SphereCastNonAlloc(
            focusPoint,
            cameraCollisionRadius,
            direction,
            cameraHits,
            targetDistance + cameraCollisionOffset,
            cameraCollisionLayers,
            QueryTriggerInteraction.Ignore
        );

        bool encontrouBloqueio = false;

        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit hit = cameraHits[i];

            if (DeveIgnorarCollider(hit.collider))
                continue;

            if (hit.distance < distanciaSegura)
            {
                distanciaSegura = hit.distance;
                encontrouBloqueio = true;
            }
        }

        if (encontrouBloqueio)
        {
            distanciaSegura -= cameraCollisionOffset;
            distanciaSegura = Mathf.Clamp(distanciaSegura, cameraMinDistanceFromFocus, targetDistance);
        }

        Vector3 posicaoCorrigida = focusPoint + direction * distanciaSegura;

        if (usarAntiGrudarExtra && CameraEstaSobreposta(posicaoCorrigida))
            posicaoCorrigida = EncontrarMelhorPosicaoSemGrudar(focusPoint, direction, distanciaSegura);

        if (desenharDebugColisaoCamera)
        {
            Debug.DrawLine(focusPoint, desiredPosition, Color.red);
            Debug.DrawLine(focusPoint, posicaoCorrigida, Color.green);
        }

        return posicaoCorrigida;
    }

    private Vector3 EncontrarMelhorPosicaoSemGrudar(Vector3 focusPoint, Vector3 direction, float distanciaMaxima)
    {
        float distanciaMinima = Mathf.Min(cameraMinDistanceFromFocus, distanciaMaxima);
        float menor = distanciaMinima;
        float maior = distanciaMaxima;
        Vector3 melhorPosicao = focusPoint + direction * menor;

        for (int i = 0; i < passosBuscaAntiGrudar; i++)
        {
            float meio = (menor + maior) * 0.5f;
            Vector3 teste = focusPoint + direction * meio;

            if (CameraEstaSobreposta(teste))
            {
                maior = meio;
            }
            else
            {
                melhorPosicao = teste;
                menor = meio;
            }
        }

        return melhorPosicao;
    }

    private bool CameraEstaSobreposta(Vector3 position)
    {
        int count = Physics.OverlapSphereNonAlloc(
            position,
            cameraCollisionRadius,
            cameraOverlaps,
            cameraCollisionLayers,
            QueryTriggerInteraction.Ignore
        );

        for (int i = 0; i < count; i++)
        {
            Collider col = cameraOverlaps[i];
            if (DeveIgnorarCollider(col))
                continue;

            return true;
        }

        return false;
    }

    private bool DeveIgnorarCollider(Collider col)
    {
        if (col == null || !col.enabled || col.isTrigger)
            return true;

        Transform colTransform = col.transform;

        if (target != null)
        {
            if (colTransform == target || colTransform.IsChildOf(target))
                return true;
        }

        if (colTransform == transform || colTransform.IsChildOf(transform))
            return true;

        return false;
    }

    private void HandleAutoAlign()
    {
        if (!autoAlignBehindPlayer)
            return;

        if (bloquearAutoAlignDuranteMira && Input.GetMouseButton(botaoSensibilidadeMira))
            return;

        if (timeWithoutMouse < autoAlignDelay)
            return;

        if (!IsTargetMoving())
            return;

        float targetYaw = target.eulerAngles.y;
        float diferenca = Mathf.Abs(Mathf.DeltaAngle(yaw, targetYaw));

        if (protegerContraResetDeAngulo && diferenca > anguloMaximoAutoAlignSemReset)
            return;

        if (protegerContraResetDeAngulo)
        {
            yaw = Mathf.SmoothDampAngle(yaw, targetYaw, ref yawAutoAlignVelocity, autoAlignSmoothTime, autoAlignSpeed);
        }
        else
        {
            yaw = Mathf.MoveTowardsAngle(yaw, targetYaw, autoAlignSpeed * Time.deltaTime);
        }
    }

    private bool IsTargetMoving()
    {
        float moved = Vector3.Distance(target.position, lastTargetPosition);
        lastTargetPosition = target.position;
        return moved > 0.001f;
    }

    private void AtualizarVisibilidadeDuranteTransicao()
    {
        if (!ocultarRenderersNaPrimeiraPessoa)
            return;

        bool deveMostrar = primeiraPessoaBlend <= ocultarRenderersQuandoBlendMaiorQue;
        AplicarVisibilidadeRenderers(deveMostrar);
    }

    private void AplicarVisibilidadeRenderers(bool visivel)
    {
        if (!ocultarRenderersNaPrimeiraPessoa || renderersParaOcultar == null)
            return;

        for (int i = 0; i < renderersParaOcultar.Length; i++)
        {
            if (renderersParaOcultar[i] != null)
                renderersParaOcultar[i].enabled = visivel;
        }
    }

    private void HandleCursor()
    {
        if (Input.GetKeyDown(unlockCursorKey))
            UnlockCursor();

        if (Input.GetKeyDown(lockCursorKey))
            LockCursor();
    }

    private void LockCursor()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void UnlockCursor()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void ResetarSuavizacoes()
    {
        positionVelocity = Vector3.zero;
        rotationVelocity = new Quaternion(0f, 0f, 0f, 0f);
        mouseSuavizado = Vector2.zero;
        mouseSmoothVelocity = Vector2.zero;
        yawAutoAlignVelocity = 0f;
    }

    private Quaternion SmoothDampQuaternion(Quaternion current, Quaternion targetRotation, ref Quaternion velocity, float smoothTime)
    {
        if (Time.deltaTime < Mathf.Epsilon)
            return current;

        smoothTime = Mathf.Max(0.0001f, smoothTime);

        float dot = Quaternion.Dot(current, targetRotation);
        float multi = dot > 0f ? 1f : -1f;

        targetRotation.x *= multi;
        targetRotation.y *= multi;
        targetRotation.z *= multi;
        targetRotation.w *= multi;

        Vector4 result = new Vector4(
            Mathf.SmoothDamp(current.x, targetRotation.x, ref velocity.x, smoothTime),
            Mathf.SmoothDamp(current.y, targetRotation.y, ref velocity.y, smoothTime),
            Mathf.SmoothDamp(current.z, targetRotation.z, ref velocity.z, smoothTime),
            Mathf.SmoothDamp(current.w, targetRotation.w, ref velocity.w, smoothTime)
        ).normalized;

        return new Quaternion(result.x, result.y, result.z, result.w);
    }

    private void AplicarPreferenciasSensibilidade()
    {
        if (!usarMesmaSensibilidadeDaMainCameraNaPrimeiraPessoa)
            return;

        mouseSensitivityXMira = mouseSensitivityX;
        mouseSensitivityYMira = mouseSensitivityY;
        mouseSmoothTimePrimeiraPessoa = mouseSmoothTimeTerceiraPessoa;
    }

    private float NormalizarPitch(float eulerX, float fallback)
    {
        float normalizado = eulerX;
        if (normalizado > 180f)
            normalizado -= 360f;

        if (Mathf.Abs(normalizado) < 0.001f)
            return Mathf.Clamp(fallback, minPitch, maxPitch);

        return Mathf.Clamp(normalizado, minPitch, maxPitch);
    }

    private float CalcularSuavizacao(float velocidade, float deltaTime)
    {
        return 1f - Mathf.Exp(-velocidade * deltaTime);
    }

    private float SmoothStep01(float value)
    {
        value = Mathf.Clamp01(value);
        return value * value * (3f - 2f * value);
    }

    private void OnValidate()
    {
        distance = Mathf.Max(0.1f, distance);
        distanciaZoomMira = Mathf.Max(0.15f, distanciaZoomMira);
        distanciaOlharFrenteZoomMira = Mathf.Max(0.5f, distanciaOlharFrenteZoomMira);

        cameraCollisionRadius = Mathf.Max(0.01f, cameraCollisionRadius);
        cameraCollisionOffset = Mathf.Max(0f, cameraCollisionOffset);
        cameraMinDistanceFromFocus = Mathf.Max(0.05f, cameraMinDistanceFromFocus);
        passosBuscaAntiGrudar = Mathf.Clamp(passosBuscaAntiGrudar, 3, 14);

        minPitch = Mathf.Clamp(minPitch, -89f, 89f);
        maxPitch = Mathf.Clamp(maxPitch, -89f, 89f);

        if (maxPitch < minPitch)
            maxPitch = minPitch;

        pitchInicialAoEntrarNaMira = Mathf.Clamp(pitchInicialAoEntrarNaMira, minPitch, maxPitch);
        fovNormal = Mathf.Clamp(fovNormal, 1f, 179f);
        fovMira = Mathf.Clamp(fovMira, 25f, 75f);

        if (usarMesmaSensibilidadeDaMainCameraNaPrimeiraPessoa)
        {
            mouseSensitivityXMira = mouseSensitivityX;
            mouseSensitivityYMira = mouseSensitivityY;
            mouseSmoothTimePrimeiraPessoa = mouseSmoothTimeTerceiraPessoa;
        }
    }
}
