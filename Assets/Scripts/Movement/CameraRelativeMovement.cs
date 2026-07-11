using UnityEngine;

/// <summary>
/// Movimentação baseada na direção horizontal da câmera.
/// Usa CharacterController, aceleração suave, corrida, pulo, gravidade e rotação estável.
/// Não processa input enquanto menu/pausa bloqueiam o gameplay.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(CharacterController))]
[DefaultExecutionOrder(100)]
public sealed class CameraRelativeMovement : MonoBehaviour
{
    [Header("Referências")]
    public Transform cameraTransform;
    public PlayerCameraController playerCamera;
    public Animator animator;

    [Header("Velocidade")]
    [Min(0f)] public float walkSpeed = 3.3f;
    [Min(0f)] public float runSpeed = 6.2f;
    [Min(0f)] public float acceleration = 18f;
    [Min(0f)] public float deceleration = 24f;
    [Range(0f, 1f)] public float airControl = 0.45f;

    [Header("Rotação")]
    [Min(0f)] public float rotationSpeed = 14f;
    public bool faceMovementDirection = true;
    public bool faceCameraForwardInFirstPerson = true;

    [Header("Pulo e gravidade")]
    [Min(0f)] public float jumpHeight = 1.15f;
    [Min(0f)] public float gravity = 24f;
    [Min(0f)] public float terminalVelocity = 45f;
    public float groundedVerticalVelocity = -2f;
    public bool allowJump = true;

    [Header("Corrida")]
    public KeyCode runKey = KeyCode.LeftShift;
    public bool allowRun = true;
    [Range(0f, 1f)] public float minimumInputToRun = 0.65f;

    [Header("Stamina")]
    public bool useStamina = true;
    [Min(1f)] public float maxStamina = 100f;
    [Min(0f)] public float runDrainPerSecond = 15f;
    [Min(0f)] public float recoveryPerSecond = 11f;
    [Min(0f)] public float recoveryDelay = 0.8f;
    [Min(0f)] public float minimumStaminaToStartRunning = 5f;

    [Header("Animator")]
    public string speedParameter = "Speed";
    public string groundedParameter = "Grounded";
    public string verticalSpeedParameter = "VerticalSpeed";
    [Min(0f)] public float animatorDampTime = 0.08f;

    [Header("Input externo / Mobile")]
    public bool useLegacyInput = true;

    private CharacterController controller;
    private Vector2 externalMoveInput;
    private bool externalRun;
    private bool externalJump;
    private Vector3 horizontalVelocity;
    private float verticalVelocity;
    private float currentStamina;
    private float lastRunTime = -100f;
    private bool running;

    private int speedHash;
    private int groundedHash;
    private int verticalSpeedHash;
    private bool hasSpeedParameter;
    private bool hasGroundedParameter;
    private bool hasVerticalSpeedParameter;

    public Vector3 Velocity => horizontalVelocity + Vector3.up * verticalVelocity;
    public float CurrentSpeed => horizontalVelocity.magnitude;
    public bool IsRunning => running;
    public bool IsGrounded => controller != null && controller.isGrounded;
    public float CurrentStamina => currentStamina;
    public float MaxStamina => maxStamina;
    public float Stamina01 => maxStamina > 0.001f ? Mathf.Clamp01(currentStamina / maxStamina) : 0f;

    // Compatibilidade para HUD/Menu que leem nomes antigos por reflexão.
    public float StaminaAtual => CurrentStamina;
    public float StaminaMaxima => MaxStamina;
    public float StaminaPercentual01 => Stamina01;
    public bool EstaCorrendo => IsRunning;
    public bool EstaGastandoStamina => IsRunning && useStamina;
    public bool EstaCansado => useStamina && currentStamina <= 0.01f;
    public int StaminaSegmentosMaximos => 5;
    public int StaminaSegmentosAtuais => Mathf.Clamp(Mathf.CeilToInt(Stamina01 * StaminaSegmentosMaximos), 0, StaminaSegmentosMaximos);
    public string StaminaSegmentadaTexto => StaminaSegmentosAtuais + "/" + StaminaSegmentosMaximos;
    public float StaminaRecargaReserva => 0f;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        currentStamina = Mathf.Max(1f, maxStamina);

        if (animator == null)
            animator = GetComponentInChildren<Animator>(true);

        CacheAnimatorParameters();
    }

    private void Start()
    {
        ResolveCamera();
    }

    private void Update()
    {
        ResolveCamera();

        float deltaTime = Mathf.Clamp(Time.deltaTime, 0f, 0.05f);
        if (deltaTime <= 0f)
            return;

        bool inputBlocked = GameplayInputState.IsBlocked;
        Vector2 input = inputBlocked ? Vector2.zero : ReadMoveInput();

        if (inputBlocked)
        {
            externalJump = false;
            externalRun = false;
        }

        Vector3 desiredDirection = CalculateCameraRelativeDirection(input);
        float inputMagnitude = Mathf.Clamp01(input.magnitude);
        bool wasRunning = running;
        bool wantsRun = allowRun && inputMagnitude >= minimumInputToRun && ReadRunInput();
        running = wantsRun && CanRun(wasRunning);

        UpdateStamina(deltaTime);

        float targetSpeed = inputMagnitude * (running ? runSpeed : walkSpeed);
        Vector3 targetVelocity = desiredDirection * targetSpeed;
        float responsiveness = targetSpeed > horizontalVelocity.magnitude ? acceleration : deceleration;
        float control = controller.isGrounded ? 1f : airControl;

        horizontalVelocity = Vector3.MoveTowards(
            horizontalVelocity,
            targetVelocity,
            Mathf.Max(0f, responsiveness * control) * deltaTime
        );

        UpdateVerticalVelocity(deltaTime, inputBlocked);
        UpdateRotation(desiredDirection, inputMagnitude, deltaTime);

        CollisionFlags flags = controller.Move(
            (horizontalVelocity + Vector3.up * verticalVelocity) * deltaTime
        );

        if ((flags & CollisionFlags.Above) != 0 && verticalVelocity > 0f)
            verticalVelocity = 0f;

        UpdateAnimator(deltaTime);
        externalJump = false;
    }

    public void SetMoveInput(Vector2 input)
    {
        externalMoveInput = Vector2.ClampMagnitude(input, 1f);
    }

    public void SetRunInput(bool pressed)
    {
        externalRun = pressed;
    }

    public void RequestJump()
    {
        externalJump = true;
    }

    public void RestoreStaminaFull()
    {
        currentStamina = Mathf.Max(1f, maxStamina);
    }

    public void RestaurarStaminaCompleta() => RestoreStaminaFull();
    public void RecarregarStaminaCompleta() => RestoreStaminaFull();

    private Vector2 ReadMoveInput()
    {
        if (!useLegacyInput)
            return externalMoveInput;

        Vector2 keyboard = new Vector2(
            Input.GetAxisRaw("Horizontal"),
            Input.GetAxisRaw("Vertical")
        );

        return Vector2.ClampMagnitude(keyboard + externalMoveInput, 1f);
    }

    private bool ReadRunInput()
    {
        return externalRun || (useLegacyInput && Input.GetKey(runKey));
    }

    private bool ReadJumpInput()
    {
        return externalJump || (useLegacyInput && Input.GetButtonDown("Jump"));
    }

    private Vector3 CalculateCameraRelativeDirection(Vector2 input)
    {
        if (input.sqrMagnitude <= 0.0001f)
            return Vector3.zero;

        Transform reference = cameraTransform;
        Vector3 forward = reference != null ? reference.forward : transform.forward;
        Vector3 right = reference != null ? reference.right : transform.right;
        forward.y = 0f;
        right.y = 0f;

        if (forward.sqrMagnitude <= 0.0001f)
            forward = transform.forward;
        if (right.sqrMagnitude <= 0.0001f)
            right = transform.right;

        forward.Normalize();
        right.Normalize();

        Vector3 direction = forward * input.y + right * input.x;
        return direction.sqrMagnitude > 1f ? direction.normalized : direction;
    }

    private void UpdateVerticalVelocity(float deltaTime, bool inputBlocked)
    {
        if (controller.isGrounded)
        {
            if (verticalVelocity < 0f)
                verticalVelocity = groundedVerticalVelocity;

            if (!inputBlocked && allowJump && ReadJumpInput())
                verticalVelocity = Mathf.Sqrt(Mathf.Max(0f, jumpHeight * 2f * gravity));
        }
        else
        {
            verticalVelocity = Mathf.Max(
                verticalVelocity - gravity * deltaTime,
                -Mathf.Abs(terminalVelocity)
            );
        }
    }

    private void UpdateRotation(Vector3 desiredDirection, float inputMagnitude, float deltaTime)
    {
        Vector3 facingDirection = desiredDirection;
        bool firstPerson = playerCamera != null && playerCamera.IsFirstPerson;

        if (firstPerson && faceCameraForwardInFirstPerson && cameraTransform != null)
        {
            facingDirection = cameraTransform.forward;
            facingDirection.y = 0f;
        }
        else if (!faceMovementDirection || inputMagnitude <= 0.01f)
        {
            return;
        }

        if (facingDirection.sqrMagnitude <= 0.0001f)
            return;

        Quaternion targetRotation = Quaternion.LookRotation(facingDirection.normalized, Vector3.up);
        transform.rotation = rotationSpeed <= 0.001f
            ? targetRotation
            : Quaternion.Slerp(
                transform.rotation,
                targetRotation,
                1f - Mathf.Exp(-rotationSpeed * deltaTime)
            );
    }

    private bool CanRun(bool wasRunning)
    {
        if (!useStamina)
            return true;

        return wasRunning ? currentStamina > 0.01f : currentStamina >= minimumStaminaToStartRunning;
    }

    private void UpdateStamina(float deltaTime)
    {
        maxStamina = Mathf.Max(1f, maxStamina);

        if (!useStamina)
        {
            currentStamina = maxStamina;
            return;
        }

        if (running && horizontalVelocity.sqrMagnitude > 0.05f)
        {
            currentStamina = Mathf.Max(0f, currentStamina - runDrainPerSecond * deltaTime);
            lastRunTime = Time.time;

            if (currentStamina <= 0.001f)
                running = false;
        }
        else if (Time.time - lastRunTime >= recoveryDelay)
        {
            currentStamina = Mathf.Min(maxStamina, currentStamina + recoveryPerSecond * deltaTime);
        }
    }

    private void CacheAnimatorParameters()
    {
        hasSpeedParameter = false;
        hasGroundedParameter = false;
        hasVerticalSpeedParameter = false;

        if (animator == null)
            return;

        speedHash = Animator.StringToHash(speedParameter ?? string.Empty);
        groundedHash = Animator.StringToHash(groundedParameter ?? string.Empty);
        verticalSpeedHash = Animator.StringToHash(verticalSpeedParameter ?? string.Empty);

        AnimatorControllerParameter[] parameters = animator.parameters;
        for (int i = 0; i < parameters.Length; i++)
        {
            AnimatorControllerParameter parameter = parameters[i];

            if (parameter.nameHash == speedHash && parameter.type == AnimatorControllerParameterType.Float)
                hasSpeedParameter = true;
            else if (parameter.nameHash == groundedHash && parameter.type == AnimatorControllerParameterType.Bool)
                hasGroundedParameter = true;
            else if (parameter.nameHash == verticalSpeedHash && parameter.type == AnimatorControllerParameterType.Float)
                hasVerticalSpeedParameter = true;
        }
    }

    private void UpdateAnimator(float deltaTime)
    {
        if (animator == null)
            return;

        if (hasSpeedParameter)
            animator.SetFloat(speedHash, CurrentSpeed, animatorDampTime, deltaTime);

        if (hasGroundedParameter)
            animator.SetBool(groundedHash, controller.isGrounded);

        if (hasVerticalSpeedParameter)
            animator.SetFloat(verticalSpeedHash, verticalVelocity);
    }

    private void ResolveCamera()
    {
        if (playerCamera == null)
            playerCamera = Object.FindAnyObjectByType<PlayerCameraController>(FindObjectsInactive.Include);

        if (cameraTransform == null && playerCamera != null)
            cameraTransform = playerCamera.ActiveCameraTransform;

        if (cameraTransform == null && Camera.main != null)
            cameraTransform = Camera.main.transform;
    }

    private void OnValidate()
    {
        walkSpeed = Mathf.Max(0f, walkSpeed);
        runSpeed = Mathf.Max(walkSpeed, runSpeed);
        maxStamina = Mathf.Max(1f, maxStamina);
        currentStamina = Mathf.Clamp(currentStamina, 0f, maxStamina);
    }
}
