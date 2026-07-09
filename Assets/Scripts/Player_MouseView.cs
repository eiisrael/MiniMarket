using UnityEngine;

/// <summary>
/// Camera principal do personagem, estável para terceira pessoa e primeira pessoa.
///
/// Ajuste atual:
/// - Botão direito entra em primeira pessoa via SetPrimeiraPessoa(true), soltar volta para terceira pessoa.
/// - Sem auto-align/recenter automático: remove pulsação/reset de eixo.
/// - Remove efeito mola/estabilização da camera ao apertar S/A/D repetidamente.
/// - Primeira pessoa usa posição estável baseada no corpo, não no balanço da cabeça/Empty.
/// - Oculta renderers do personagem em primeira pessoa para não ver cabeça/corpo por dentro.
/// - Colisão da terceira pessoa ignora o próprio player e objetos seguráveis, evitando snap/travadas.
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
    public bool usarSensibilidadeSeparadaNaMira = false;
    [Range(0, 2)] public int botaoSensibilidadeMira = 1;
    public float mouseSensitivityXMira = 180f;
    public float mouseSensitivityYMira = 120f;

    [Header("Suavização do Mouse")]
    public bool suavizarMouse = true;
    [Min(0f)] public float mouseSmoothTimeTerceiraPessoa = 0.006f;
    [Min(0f)] public float mouseSmoothTimePrimeiraPessoa = 0f;
    public bool usarMesmaSensibilidadeDaMainCameraNaPrimeiraPessoa = true;
    [Min(0f)] public float mouseDeadZone = 0.001f;

    [Header("Limites Verticais")]
    public float minPitch = -45f;
    public float maxPitch = 65f;

    [Header("Suavização Terceira Pessoa")]
    [Tooltip("Recomendado 0 para remover o efeito mola/tremor ao apertar S/A/D.")]
    [Min(0f)] public float positionSmoothTime = 0f;
    [Tooltip("Recomendado 0 para camera responder fixa, sem oscilação.")]
    [Min(0f)] public float rotationSmoothTime = 0f;

    [Header("Anti Tremor de Movimento")]
    [Tooltip("Remove o efeito de estabilização/mola da camera quando não está transicionando.")]
    public bool removerEfeitoMolaDaCamera = true;

    [Tooltip("Na camera normal e na primeira pessoa, aplica posição/rotação direta para não tremer com S/A/D.")]
    public bool usarSeguimentoDiretoQuandoNaoEstaTransicionando = true;

    [Tooltip("Ignora micro variações verticais do alvo, evitando tremor por animação/CharacterController.")]
    [Min(0f)] public float ignorarTremorVerticalMenorQue = 0.01f;

    [Tooltip("Na primeira pessoa, não usa o balanço real do Empty da cabeça; usa altura estável a partir do corpo.")]
    public bool estabilizarPontoPrimeiraPessoaContraAnimacao = true;

    [Header("Auto Alinhar Atrás do Personagem")]
    public bool autoAlignBehindPlayer = false;
    public float autoAlignDelay = 999f;
    public float autoAlignSpeed = 0f;

    [Header("Proteção Contra Reset de Ângulo")]
    public bool protegerContraResetDeAngulo = true;
    public bool bloquearAutoAlignDuranteMira = true;
    [Range(5f, 180f)] public float anguloMaximoAutoAlignSemReset = 180f;
    [Min(0.01f)] public float autoAlignSmoothTime = 0.35f;

    [Header("Primeira Pessoa")]
    public bool permitirPrimeiraPessoa = true;
    public Transform pontoPrimeiraPessoa;
    public Vector3 offsetPrimeiraPessoa = new Vector3(0f, 1.68f, 0.18f);
    public Vector3 ajusteLocalPontoPrimeiraPessoa = new Vector3(0f, 0f, 0.05f);

    [Header("Primeira Pessoa Anti-Jumping")]
    public bool preservarAnguloAtualNaTransicao = true;
    public bool usarPosicaoPrimeiraPessoaEstavel = true;
    public bool evitarRealimentacaoRotacaoPersonagemCamera = true;
    public bool sincronizarCorpoSuaveNaPrimeiraPessoa = true;
    [Min(0.1f)] public float velocidadeSincronizarCorpoPrimeiraPessoa = 18f;

    [Header("Mira / Zoom Profissional")]
    public bool alinharComFrenteDoPersonagemAoEntrarNaMira = false;
    public bool corrigirPitchAoEntrarNaMira = false;
    [Range(-30f, 30f)] public float pitchInicialAoEntrarNaMira = 0f;

    [Tooltip("Mantido por compatibilidade. Recomendado desligado: botão direito agora usa primeira pessoa real.")]
    public bool usarZoomMiraPorDistancia = false;
    [Min(0.15f)] public float distanciaZoomMira = 1.25f;
    public float alturaZoomMira = 1.58f;
    public float deslocamentoLateralZoomMira = 0f;
    [Min(0.5f)] public float distanciaOlharFrenteZoomMira = 8f;
    public bool aplicarColisaoNoZoomMira = false;
    [Min(0.1f)] public float velocidadeEntradaZoomMira = 8f;

    [Header("FOV da Mira / Zoom")]
    public bool ajustarFovNaMira = true;
    public bool capturarFovNormalNoStart = true;
    [Min(1f)] public float fovNormal = 60f;
    [Range(25f, 75f)] public float fovMira = 58f;
    [Min(0.1f)] public float velocidadeFovMira = 12f;

    [Header("Transição Anti-Enjoo")]
    [Min(0.1f)] public float velocidadeEntradaPrimeiraPessoa = 14f;
    [Min(0.1f)] public float velocidadeSaidaPrimeiraPessoa = 14f;
    [Min(0f)] public float positionSmoothTimePrimeiraPessoa = 0f;
    [Min(0f)] public float rotationSmoothTimePrimeiraPessoa = 0f;
    [Min(0f)] public float positionSmoothTimeSaidaPrimeiraPessoa = 0.02f;
    [Min(0f)] public float rotationSmoothTimeSaidaPrimeiraPessoa = 0.015f;

    [Header("Rotação do Personagem na Mira")]
    public bool rotacionarPersonagemNaPrimeiraPessoa = true;
    [Min(0.1f)] public float velocidadeRotacaoPersonagemPrimeiraPessoa = 18f;

    [Header("Anti Atravessar Cabeça")]
    public bool ocultarRenderersNaPrimeiraPessoa = true;
    public bool autoEncontrarRenderersDoTarget = true;
    [Range(0f, 1f)] public float ocultarRenderersQuandoBlendMaiorQue = 0.08f;
    public Renderer[] renderersParaOcultar;

    [Header("Colisão da Câmera - Anti Parede Seguro")]
    public bool usarColisaoCamera = true;
    public LayerMask cameraCollisionLayers = ~0;
    [Min(0.01f)] public float cameraCollisionRadius = 0.28f;
    [Min(0f)] public float cameraCollisionOffset = 0.18f;
    [Min(0.05f)] public float cameraMinDistanceFromFocus = 0.85f;
    public bool usarAntiGrudarExtra = true;
    [Range(3, 14)] public int passosBuscaAntiGrudar = 8;
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
    private Vector3 ultimoFocusPointEstavel;
    private bool possuiFocusPointEstavel;

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
            ultimoFocusPointEstavel = target.position + targetOffset;
            possuiFocusPointEstavel = true;
        }

        if (autoEncontrarRenderersDoTarget && target != null && (renderersParaOcultar == null || renderersParaOcultar.Length == 0))
            renderersParaOcultar = target.GetComponentsInChildren<Renderer>(true);

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
        possuiFocusPointEstavel = false;

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
            float smoothTime = EstaEmPrimeiraPessoa ? mouseSmoothTimePrimeiraPessoa : mouseSmoothTimeTerceiraPessoa;
            if (smoothTime <= 0.0001f)
            {
                mouseSuavizado = mouseInput;
                mouseSmoothVelocity = Vector2.zero;
            }
            else
            {
                mouseSuavizado = Vector2.SmoothDamp(mouseSuavizado, mouseInput, ref mouseSmoothVelocity, smoothTime);
            }
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
        pitch += (invertY ? mouseSuavizado.y : -mouseSuavizado.y) * sensibilidadeYAtual * Time.deltaTime;
        pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
    }

    private void AtualizarBlendPrimeiraPessoa()
    {
        float alvo = primeiraPessoaSolicitada ? 1f : 0f;
        float velocidade = primeiraPessoaSolicitada ? velocidadeEntradaPrimeiraPessoa : velocidadeSaidaPrimeiraPessoa;
        primeiraPessoaBlend = Mathf.MoveTowards(primeiraPessoaBlend, alvo, velocidade * Time.deltaTime);
    }

    private void UpdateCameraUnificada()
    {
        Vector3 focusPoint = CalcularFocusPointEstavel();
        Quaternion orbitRotation = Quaternion.Euler(pitch, yaw, 0f);

        Vector3 terceiraPessoaPosition = focusPoint - orbitRotation * Vector3.forward * distance + Vector3.up * height;
        terceiraPessoaPosition = AplicarColisaoCameraSegura(focusPoint, terceiraPessoaPosition);
        Quaternion terceiraPessoaRotation = Quaternion.LookRotation(focusPoint - terceiraPessoaPosition, Vector3.up);

        Vector3 primeiraPessoaPosition = CalcularPosicaoPrimeiraPessoaEstavel();
        Quaternion primeiraPessoaRotation = Quaternion.Euler(pitch, yaw, 0f);

        float blendSuave = SmoothStep01(primeiraPessoaBlend);
        Vector3 desiredPosition = Vector3.Lerp(terceiraPessoaPosition, primeiraPessoaPosition, blendSuave);
        Quaternion desiredRotation = Quaternion.Slerp(terceiraPessoaRotation, primeiraPessoaRotation, blendSuave);

        bool cameraEmEstadoPuro = blendSuave <= 0.01f || blendSuave >= 0.98f;
        bool usarDireto = removerEfeitoMolaDaCamera && usarSeguimentoDiretoQuandoNaoEstaTransicionando && cameraEmEstadoPuro;

        if (usarDireto)
        {
            transform.position = desiredPosition;
            transform.rotation = desiredRotation;
            positionVelocity = Vector3.zero;
            rotationVelocity = new Quaternion(0f, 0f, 0f, 0f);
        }
        else
        {
            float smoothPositionAtual = primeiraPessoaSolicitada ? positionSmoothTimePrimeiraPessoa : positionSmoothTimeSaidaPrimeiraPessoa;
            float smoothRotationAtual = primeiraPessoaSolicitada ? rotationSmoothTimePrimeiraPessoa : rotationSmoothTimeSaidaPrimeiraPessoa;

            if (primeiraPessoaBlend <= 0.01f)
            {
                smoothPositionAtual = positionSmoothTime;
                smoothRotationAtual = rotationSmoothTime;
            }

            Vector3 novaPosicao = smoothPositionAtual <= 0.0001f
                ? desiredPosition
                : Vector3.SmoothDamp(transform.position, desiredPosition, ref positionVelocity, smoothPositionAtual);

            if (usarColisaoCamera && corrigirPosicaoFinalSuavizada && blendSuave < 0.20f)
                novaPosicao = AplicarColisaoCameraSegura(focusPoint, novaPosicao);

            transform.position = novaPosicao;
            transform.rotation = smoothRotationAtual <= 0.0001f
                ? desiredRotation
                : Quaternion.Slerp(transform.rotation, desiredRotation, CalcularSuavizacao(1f / Mathf.Max(0.0001f, smoothRotationAtual), Time.deltaTime));
        }

        SincronizarCorpoComCameraSemJumping();
        lastTargetPosition = target.position;
    }

    private Vector3 CalcularFocusPointEstavel()
    {
        Vector3 atual = target.position + targetOffset;

        if (!removerEfeitoMolaDaCamera)
            return atual;

        if (!possuiFocusPointEstavel)
        {
            ultimoFocusPointEstavel = atual;
            possuiFocusPointEstavel = true;
            return atual;
        }

        if (Mathf.Abs(atual.y - ultimoFocusPointEstavel.y) <= ignorarTremorVerticalMenorQue)
            atual.y = ultimoFocusPointEstavel.y;

        ultimoFocusPointEstavel = atual;
        return atual;
    }

    private Vector3 CalcularPosicaoPrimeiraPessoaEstavel()
    {
        if (!usarPosicaoPrimeiraPessoaEstavel && pontoPrimeiraPessoa != null)
            return pontoPrimeiraPessoa.TransformPoint(ajusteLocalPontoPrimeiraPessoa);

        Quaternion yawRotation = Quaternion.Euler(0f, yaw, 0f);

        if (pontoPrimeiraPessoa != null && estabilizarPontoPrimeiraPessoaContraAnimacao)
        {
            float alturaOlhos = pontoPrimeiraPessoa.position.y - target.position.y;
            alturaOlhos = Mathf.Max(0.2f, alturaOlhos);
            Vector3 offsetPlanoEstavel = new Vector3(ajusteLocalPontoPrimeiraPessoa.x, 0f, ajusteLocalPontoPrimeiraPessoa.z);
            return target.position + Vector3.up * alturaOlhos + yawRotation * offsetPlanoEstavel;
        }

        if (pontoPrimeiraPessoa != null)
            return pontoPrimeiraPessoa.position + yawRotation * ajusteLocalPontoPrimeiraPessoa;

        Vector3 offsetPlano = new Vector3(offsetPrimeiraPessoa.x, 0f, offsetPrimeiraPessoa.z);
        return target.position + Vector3.up * offsetPrimeiraPessoa.y + yawRotation * offsetPlano;
    }

    private void SincronizarCorpoComCameraSemJumping()
    {
        if (!rotacionarPersonagemNaPrimeiraPessoa || target == null || !EstaEmPrimeiraPessoa)
            return;

        Vector3 forward = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
        if (forward.sqrMagnitude <= 0.0001f)
            return;

        Quaternion rotacaoAlvoDoPersonagem = Quaternion.LookRotation(forward.normalized, Vector3.up);
        float velocidade = sincronizarCorpoSuaveNaPrimeiraPessoa ? velocidadeSincronizarCorpoPrimeiraPessoa : velocidadeRotacaoPersonagemPrimeiraPessoa;
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
        float menor = Mathf.Min(cameraMinDistanceFromFocus, distanciaMaxima);
        float maior = distanciaMaxima;
        Vector3 melhorPosicao = focusPoint + direction * menor;

        for (int i = 0; i < passosBuscaAntiGrudar; i++)
        {
            float meio = (menor + maior) * 0.5f;
            Vector3 teste = focusPoint + direction * meio;

            if (CameraEstaSobreposta(teste))
                maior = meio;
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

        if (target != null && (colTransform == target || colTransform.IsChildOf(target)))
            return true;

        if (colTransform == transform || colTransform.IsChildOf(transform))
            return true;

        if (col.GetComponentInParent<GrabbableObjectHardcore>() != null)
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

        yaw = Mathf.SmoothDampAngle(yaw, targetYaw, ref yawAutoAlignVelocity, autoAlignSmoothTime, autoAlignSpeed);
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

    private void AplicarPreferenciasSensibilidade()
    {
        if (!usarMesmaSensibilidadeDaMainCameraNaPrimeiraPessoa)
            return;

        mouseSensitivityXMira = mouseSensitivityX;
        mouseSensitivityYMira = mouseSensitivityY;
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
        positionSmoothTime = Mathf.Max(0f, positionSmoothTime);
        rotationSmoothTime = Mathf.Max(0f, rotationSmoothTime);
        positionSmoothTimePrimeiraPessoa = Mathf.Max(0f, positionSmoothTimePrimeiraPessoa);
        rotationSmoothTimePrimeiraPessoa = Mathf.Max(0f, rotationSmoothTimePrimeiraPessoa);
        ignorarTremorVerticalMenorQue = Mathf.Max(0f, ignorarTremorVerticalMenorQue);
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
    }
}
