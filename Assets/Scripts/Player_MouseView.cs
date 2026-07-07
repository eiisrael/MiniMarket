using UnityEngine;

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
    [Tooltip("Ativa uma sensibilidade diferente enquanto o botão de mira estiver pressionado.")]
    public bool usarSensibilidadeSeparadaNaMira = true;

    [Tooltip("Botão usado para aplicar a sensibilidade da mira. 0 = esquerdo, 1 = direito, 2 = meio.")]
    [Range(0, 2)]
    public int botaoSensibilidadeMira = 1;

    [Tooltip("Sensibilidade horizontal enquanto segura o botão de mira.")]
    public float mouseSensitivityXMira = 75f;

    [Tooltip("Sensibilidade vertical enquanto segura o botão de mira.")]
    public float mouseSensitivityYMira = 55f;

    [Header("Suavização do Mouse")]
    public bool suavizarMouse = true;

    [Min(0f)]
    public float mouseSmoothTimeTerceiraPessoa = 0.015f;

    [Min(0f)]
    public float mouseSmoothTimePrimeiraPessoa = 0.03f;

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

    [Header("Primeira Pessoa")]
    public bool permitirPrimeiraPessoa = true;

    [Tooltip("Crie um Empty um pouco na frente dos olhos do personagem e arraste aqui.")]
    public Transform pontoPrimeiraPessoa;

    [Tooltip("Usado caso o pontoPrimeiraPessoa esteja vazio. Coloque o Z um pouco para frente da cabeça.")]
    public Vector3 offsetPrimeiraPessoa = new Vector3(0f, 1.68f, 0.32f);

    [Tooltip("Ajuste extra local quando usar pontoPrimeiraPessoa. Use Z positivo se ainda atravessar a cabeça.")]
    public Vector3 ajusteLocalPontoPrimeiraPessoa = new Vector3(0f, 0f, 0.12f);

    [Header("Transição Anti-Enjoo")]
    [Tooltip("Velocidade para ENTRAR na primeira pessoa. Menor = mais lento.")]
    [Min(0.1f)]
    public float velocidadeEntradaPrimeiraPessoa = 4.2f;

    [Tooltip("Velocidade para SAIR da primeira pessoa. Menor = volta mais lenta e confortável.")]
    [Min(0.1f)]
    public float velocidadeSaidaPrimeiraPessoa = 1.6f;

    [Tooltip("Suavização da posição enquanto entra na primeira pessoa.")]
    [Min(0.01f)]
    public float positionSmoothTimePrimeiraPessoa = 0.08f;

    [Tooltip("Suavização da rotação enquanto entra na primeira pessoa.")]
    [Min(0.01f)]
    public float rotationSmoothTimePrimeiraPessoa = 0.045f;

    [Tooltip("Suavização da posição enquanto sai da primeira pessoa. Aumente se a volta ainda causar tontura.")]
    [Min(0.01f)]
    public float positionSmoothTimeSaidaPrimeiraPessoa = 0.24f;

    [Tooltip("Suavização da rotação enquanto sai da primeira pessoa. Aumente se a volta ainda causar tontura.")]
    [Min(0.01f)]
    public float rotationSmoothTimeSaidaPrimeiraPessoa = 0.16f;

    [Header("Rotação do Personagem na Mira")]
    public bool rotacionarPersonagemNaPrimeiraPessoa = true;

    [Min(0.1f)]
    public float velocidadeRotacaoPersonagemPrimeiraPessoa = 10f;

    [Header("Anti Atravessar Cabeça")]
    [Tooltip("Oculta o personagem durante a transição para evitar ver a câmera atravessando cabeça/corpo.")]
    public bool ocultarRenderersNaPrimeiraPessoa = true;

    [Tooltip("Se estiver vazio, o script tenta pegar automaticamente os renderers dentro do target.")]
    public bool autoEncontrarRenderersDoTarget = true;

    [Range(0f, 1f)]
    public float ocultarRenderersQuandoBlendMaiorQue = 0.08f;

    public Renderer[] renderersParaOcultar;

    [Header("Colisão da Câmera")]
    public bool usarColisaoCamera = true;

    [Tooltip("Use uma layer que tenha cenário, parede, chão, objetos. Evite colocar o Player aqui.")]
    public LayerMask cameraCollisionLayers = ~0;

    [Min(0.01f)]
    public float cameraCollisionRadius = 0.22f;

    [Min(0f)]
    public float cameraCollisionOffset = 0.08f;

    [Header("Cursor")]
    public bool lockCursorOnStart = true;
    public KeyCode unlockCursorKey = KeyCode.Escape;
    public KeyCode lockCursorKey = KeyCode.Mouse0;

    private float yaw;
    private float pitch = 15f;
    private float timeWithoutMouse;

    private Vector3 positionVelocity;
    private Quaternion rotationVelocity;

    private Vector3 lastTargetPosition;

    private Vector2 mouseSuavizado;
    private Vector2 mouseSmoothVelocity;

    private bool primeiraPessoaSolicitada;
    private float primeiraPessoaBlend;

    public bool EstaEmPrimeiraPessoa => primeiraPessoaSolicitada || primeiraPessoaBlend > 0.15f;
    public bool EstaTransicionandoPrimeiraPessoa => primeiraPessoaBlend > 0.01f && primeiraPessoaBlend < 0.99f;

    public float YawAtual => yaw;
    public float PitchAtual => pitch;

    private void Start()
    {
        if (target == null && transform.parent != null)
            target = transform.parent;

        if (target != null)
        {
            yaw = target.eulerAngles.y;
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

        ResetarSuavizacoes();
        AplicarVisibilidadeRenderers(true);

        if (lockCursorOnStart)
            LockCursor();
    }

    private void OnDisable()
    {
        AplicarVisibilidadeRenderers(true);
    }

    private void LateUpdate()
    {
        HandleCursor();

        if (target == null)
            return;

        if (Cursor.lockState == CursorLockMode.Locked)
            HandleMouse();

        AtualizarBlendPrimeiraPessoa();

        if (!EstaEmPrimeiraPessoa)
            HandleAutoAlign();

        UpdateCameraUnificada();

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

        if (primeiraPessoaSolicitada)
            timeWithoutMouse = 0f;

        if (target != null)
            lastTargetPosition = target.position;
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
        }

        if (suavizarMouse)
        {
            float smoothTime = EstaEmPrimeiraPessoa
                ? mouseSmoothTimePrimeiraPessoa
                : mouseSmoothTimeTerceiraPessoa;

            mouseSuavizado = Vector2.SmoothDamp(
                mouseSuavizado,
                mouseInput,
                ref mouseSmoothVelocity,
                smoothTime
            );
        }
        else
        {
            mouseSuavizado = mouseInput;
        }

        bool usandoSensibilidadeMira =
            usarSensibilidadeSeparadaNaMira &&
            Input.GetMouseButton(botaoSensibilidadeMira);

        float sensibilidadeXAtual = usandoSensibilidadeMira
            ? mouseSensitivityXMira
            : mouseSensitivityX;

        float sensibilidadeYAtual = usandoSensibilidadeMira
            ? mouseSensitivityYMira
            : mouseSensitivityY;

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

        float velocidade = primeiraPessoaSolicitada
            ? velocidadeEntradaPrimeiraPessoa
            : velocidadeSaidaPrimeiraPessoa;

        primeiraPessoaBlend = Mathf.MoveTowards(
            primeiraPessoaBlend,
            alvo,
            velocidade * Time.deltaTime
        );
    }

    private void UpdateCameraUnificada()
    {
        Vector3 focusPoint = target.position + targetOffset;

        Quaternion orbitRotation = Quaternion.Euler(pitch, yaw, 0f);

        Vector3 terceiraPessoaPosition =
            focusPoint
            - orbitRotation * Vector3.forward * distance
            + Vector3.up * height;

        terceiraPessoaPosition = AplicarColisaoCamera(
            focusPoint,
            terceiraPessoaPosition
        );

        Quaternion terceiraPessoaRotation = Quaternion.LookRotation(
            focusPoint - terceiraPessoaPosition,
            Vector3.up
        );

        Vector3 primeiraPessoaPosition = CalcularPosicaoPrimeiraPessoa();
        Quaternion primeiraPessoaRotation = Quaternion.Euler(pitch, yaw, 0f);

        float blendSuave = SmoothStep01(primeiraPessoaBlend);

        Vector3 desiredPosition = Vector3.Lerp(
            terceiraPessoaPosition,
            primeiraPessoaPosition,
            blendSuave
        );

        Quaternion desiredRotation = Quaternion.Slerp(
            terceiraPessoaRotation,
            primeiraPessoaRotation,
            blendSuave
        );

        float smoothPositionAtual;
        float smoothRotationAtual;

        if (!primeiraPessoaSolicitada && primeiraPessoaBlend > 0.01f)
        {
            smoothPositionAtual = positionSmoothTimeSaidaPrimeiraPessoa;
            smoothRotationAtual = rotationSmoothTimeSaidaPrimeiraPessoa;
        }
        else
        {
            smoothPositionAtual = Mathf.Lerp(
                positionSmoothTime,
                positionSmoothTimePrimeiraPessoa,
                blendSuave
            );

            smoothRotationAtual = Mathf.Lerp(
                rotationSmoothTime,
                rotationSmoothTimePrimeiraPessoa,
                blendSuave
            );
        }

        transform.position = Vector3.SmoothDamp(
            transform.position,
            desiredPosition,
            ref positionVelocity,
            smoothPositionAtual
        );

        transform.rotation = SmoothDampQuaternion(
            transform.rotation,
            desiredRotation,
            ref rotationVelocity,
            smoothRotationAtual
        );

        if (rotacionarPersonagemNaPrimeiraPessoa && target != null && EstaEmPrimeiraPessoa)
        {
            Quaternion rotacaoAlvoDoPersonagem = Quaternion.Euler(0f, yaw, 0f);

            float suavizacaoRotacaoPlayer = CalcularSuavizacao(
                velocidadeRotacaoPersonagemPrimeiraPessoa,
                Time.deltaTime
            );

            target.rotation = Quaternion.Slerp(
                target.rotation,
                rotacaoAlvoDoPersonagem,
                suavizacaoRotacaoPlayer
            );
        }

        lastTargetPosition = target.position;
    }

    private Vector3 CalcularPosicaoPrimeiraPessoa()
    {
        if (pontoPrimeiraPessoa != null)
            return pontoPrimeiraPessoa.TransformPoint(ajusteLocalPontoPrimeiraPessoa);

        return target.TransformPoint(offsetPrimeiraPessoa);
    }

    private Vector3 AplicarColisaoCamera(Vector3 focusPoint, Vector3 desiredPosition)
    {
        if (!usarColisaoCamera)
            return desiredPosition;

        Vector3 direction = desiredPosition - focusPoint;
        float targetDistance = direction.magnitude;

        if (targetDistance <= 0.001f)
            return desiredPosition;

        direction /= targetDistance;

        RaycastHit[] hits = Physics.SphereCastAll(
            focusPoint,
            cameraCollisionRadius,
            direction,
            targetDistance,
            cameraCollisionLayers,
            QueryTriggerInteraction.Ignore
        );

        float menorDistancia = targetDistance;
        bool encontrouBloqueio = false;

        for (int i = 0; i < hits.Length; i++)
        {
            RaycastHit hit = hits[i];

            if (hit.collider == null)
                continue;

            if (target != null)
            {
                Transform hitTransform = hit.collider.transform;

                if (hitTransform == target || hitTransform.IsChildOf(target))
                    continue;
            }

            if (hit.distance < menorDistancia)
            {
                menorDistancia = hit.distance;
                encontrouBloqueio = true;
            }
        }

        if (!encontrouBloqueio)
            return desiredPosition;

        menorDistancia = Mathf.Max(0f, menorDistancia - cameraCollisionOffset);

        return focusPoint + direction * menorDistancia;
    }

    private void HandleAutoAlign()
    {
        if (!autoAlignBehindPlayer)
            return;

        if (timeWithoutMouse < autoAlignDelay)
            return;

        if (!IsTargetMoving())
            return;

        float targetYaw = target.eulerAngles.y;

        yaw = Mathf.MoveTowardsAngle(
            yaw,
            targetYaw,
            autoAlignSpeed * Time.deltaTime
        );
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
        if (!ocultarRenderersNaPrimeiraPessoa)
            return;

        if (renderersParaOcultar == null)
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
    }

    private Quaternion SmoothDampQuaternion(
        Quaternion current,
        Quaternion targetRotation,
        ref Quaternion velocity,
        float smoothTime
    )
    {
        if (Time.deltaTime < Mathf.Epsilon)
            return current;

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

    private float CalcularSuavizacao(float velocidade, float deltaTime)
    {
        return 1f - Mathf.Exp(-velocidade * deltaTime);
    }

    private float SmoothStep01(float value)
    {
        value = Mathf.Clamp01(value);
        return value * value * (3f - 2f * value);
    }
}