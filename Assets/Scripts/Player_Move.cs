using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerMoveHardcore2 : MonoBehaviour
{
    [Header("Referências")]
    public Transform cameraTransform;
    public Animator animator;

    [Tooltip("Objeto visual do personagem. Use CharacterVisual, Armature ou o mesh principal.")]
    public Transform characterVisual;

    [Header("Movimento")]
    public float walkSpeed = 2.5f;
    public float runSpeed = 5.5f;

    [Tooltip("Quanto menor, mais rápido o personagem responde.")]
    public float movementSmoothTime = 0.12f;

    [Tooltip("Quanto menor, mais rápido ele para ao soltar as teclas.")]
    public float stopSmoothTime = 0.08f;

    [Range(0f, 1f)]
    public float airControl = 0.45f;

    [Header("Delay / Peso do Movimento")]
    public float movementDelay = 0.08f;
    public float movementWeight = 1.0f;

    [Header("Rotação")]
    public bool usarRotacaoPeloMouse = true;
    public bool rotateTowardsMovement = false;
    public float rotationSmoothTime = 0.42f;
    public float maxTurnSpeed = 360f;

    [Header("Pulo")]
    public float jumpHeight = 1.35f;

    [Tooltip("Tempo em que o comando de pulo fica guardado antes de tocar o chão.")]
    public float jumpBufferTime = 0.10f;

    [Tooltip("Pequena tolerância para pular logo após sair da borda.")]
    public float coyoteTime = 0.08f;

    [Header("Anti Pulo Infinito")]
    [Tooltip("Delay real entre um pulo e outro. 0.8 = 0.8 segundos.")]
    public float jumpCooldown = 0.8f;

    [Tooltip("Tempo em que o chão é ignorado logo após pular.")]
    public float groundLockAfterJump = 0.25f;

    [Tooltip("Tempo mínimo que precisa ficar no ar antes de poder aterrissar novamente.")]
    public float minimumAirTimeBeforeLanding = 0.20f;

    public bool requireJumpRelease = true;

    [Tooltip("Tempo máximo segurando espaço para sustentar um pouco o pulo.")]
    public float jumpHoldTime = 0.12f;

    [Tooltip("Força extra enquanto segura espaço no começo do pulo.")]
    public float jumpHoldForce = 4.5f;

    [Header("Gravidade")]
    public float gravity = -25f;
    public float fallGravityMultiplier = 1.85f;
    public float lowJumpGravityMultiplier = 2.2f;
    public float groundedForce = -4f;
    public float groundSnapForce = -6f;
    public float terminalVelocity = -55f;

    [Header("Checagem de Chão")]
    public Transform groundCheck;

    [Tooltip("Raio da checagem de chão.")]
    public float groundCheckRadius = 0.18f;

    [Tooltip("Distância para procurar chão abaixo do personagem.")]
    public float groundCheckDistance = 0.35f;

    [Tooltip("Use uma Layer chamada Ground e marque somente chão, rua, calçada e terreno.")]
    public LayerMask groundLayers = ~0;

    [Tooltip("Normal mínima para considerar chão. 0.65 evita considerar paredes como chão.")]
    [Range(0f, 1f)]
    public float minGroundNormalY = 0.65f;

    [Header("Teclas")]
    public KeyCode runKey = KeyCode.LeftShift;
    public KeyCode jumpKey = KeyCode.Space;

    [Header("Configurações")]
    public bool cameraRelativeMovement = true;
    public bool lockCursorOnPlay = false;

    private CharacterController controller;

    private Vector3 currentHorizontalVelocity;
    private Vector3 horizontalVelocityRef;
    private Vector3 desiredMoveDirection;

    private float verticalVelocity;
    private float currentSpeed;
    private float targetSpeed;
    private float speedVelocityRef;
    private float rotationVelocityRef;

    private float jumpBufferCounter;
    private float coyoteCounter;
    private float jumpHoldCounter;
    private float movementDelayCounter;

    private float nextAllowedJumpTime;
    private float groundLockCounter;
    private float airTimeCounter;

    private bool jumpReleased = true;
    private bool jumpConsumed;
    private bool isGrounded;
    private bool wasGrounded;
    private bool isWalking;
    private bool isRunning;
    private bool isJumping;
    private bool hasMovementInput;

    private Vector2 rawInput;
    private Vector2 smoothInput;
    private Vector2 smoothInputVelocity;

    private readonly HashSet<string> animatorParams = new HashSet<string>();

    private void Awake()
    {
        controller = GetComponent<CharacterController>();

        if (animator == null)
            animator = GetComponentInChildren<Animator>();

        if (cameraTransform == null && Camera.main != null)
            cameraTransform = Camera.main.transform;

        if (animator != null)
        {
            animator.applyRootMotion = false;

            foreach (AnimatorControllerParameter parameter in animator.parameters)
            {
                animatorParams.Add(parameter.name);
            }
        }

        if (lockCursorOnPlay)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    private void Update()
    {
        ReadInput();
        UpdateJumpInput();
        CheckGround();
        CalculateMovement();
        CalculateRotation();
        CalculateJumpAndGravity();
        ApplyMovement();
        UpdateAnimator();
    }

    private void ReadInput()
    {
        float x = 0f;
        float z = 0f;

        if (Input.GetKey(KeyCode.A)) x -= 1f;
        if (Input.GetKey(KeyCode.D)) x += 1f;
        if (Input.GetKey(KeyCode.W)) z += 1f;
        if (Input.GetKey(KeyCode.S)) z -= 1f;

        rawInput = new Vector2(x, z);

        if (rawInput.magnitude > 1f)
            rawInput.Normalize();

        hasMovementInput = rawInput.magnitude > 0.1f;

        float smoothTime = hasMovementInput ? movementSmoothTime : stopSmoothTime;

        smoothInput = Vector2.SmoothDamp(
            smoothInput,
            rawInput,
            ref smoothInputVelocity,
            smoothTime
        );

        if (smoothInput.magnitude < 0.01f)
            smoothInput = Vector2.zero;
    }

    private void UpdateJumpInput()
    {
        if (Input.GetKeyUp(jumpKey))
            jumpReleased = true;

        if (Input.GetKeyDown(jumpKey))
        {
            if (!requireJumpRelease || jumpReleased)
            {
                jumpBufferCounter = jumpBufferTime;
                jumpReleased = false;
            }
        }
        else
        {
            jumpBufferCounter -= Time.deltaTime;
        }

        if (jumpBufferCounter < 0f)
            jumpBufferCounter = 0f;
    }

private void CheckGround()
{
    wasGrounded = isGrounded;

    bool controllerGrounded = controller.isGrounded;

    if (groundLockCounter > 0f)
    {
        groundLockCounter -= Time.deltaTime;
        isGrounded = false;
        coyoteCounter = 0f;
        airTimeCounter += Time.deltaTime;
        return;
    }

    if (controllerGrounded && verticalVelocity <= 0f)
    {
        isGrounded = true;
        coyoteCounter = coyoteTime;
        airTimeCounter = 0f;
        jumpConsumed = false;
        isJumping = false;

        if (verticalVelocity < 0f)
            verticalVelocity = groundedForce;

        return;
    }

    isGrounded = false;
    airTimeCounter += Time.deltaTime;

    if (wasGrounded && !jumpConsumed)
        coyoteCounter = coyoteTime;
    else
        coyoteCounter -= Time.deltaTime;
}
    private bool DetectGround()
    {
        Vector3 origin;

        if (groundCheck != null)
            origin = groundCheck.position + Vector3.up * 0.15f;
        else
            origin = transform.position + Vector3.up * 0.25f;

        RaycastHit[] hits = Physics.SphereCastAll(
            origin,
            groundCheckRadius,
            Vector3.down,
            groundCheckDistance,
            groundLayers,
            QueryTriggerInteraction.Ignore
        );

        for (int i = 0; i < hits.Length; i++)
        {
            RaycastHit hit = hits[i];

            if (hit.collider == null)
                continue;

            if (hit.collider.transform == transform || hit.collider.transform.IsChildOf(transform))
                continue;

            if (hit.normal.y < minGroundNormalY)
                continue;

            return true;
        }

        return false;
    }

    private void CalculateMovement()
    {
        if (hasMovementInput)
            movementDelayCounter += Time.deltaTime;
        else
            movementDelayCounter = 0f;

        float delayFactor = Mathf.Clamp01(
            movementDelayCounter / Mathf.Max(0.01f, movementDelay)
        );

        isRunning = hasMovementInput && Input.GetKey(runKey);
        isWalking = hasMovementInput;

        targetSpeed = isRunning ? runSpeed : walkSpeed;
        targetSpeed *= smoothInput.magnitude;
        targetSpeed *= delayFactor;

        currentSpeed = Mathf.SmoothDamp(
            currentSpeed,
            targetSpeed,
            ref speedVelocityRef,
            hasMovementInput ? movementSmoothTime : stopSmoothTime
        );

        desiredMoveDirection = GetCameraRelativeDirection(smoothInput);

        Vector3 targetVelocity = desiredMoveDirection * currentSpeed;

        float smoothTime = (hasMovementInput ? movementSmoothTime : stopSmoothTime) * movementWeight;

        if (isGrounded)
        {
            currentHorizontalVelocity = Vector3.SmoothDamp(
                currentHorizontalVelocity,
                targetVelocity,
                ref horizontalVelocityRef,
                smoothTime
            );
        }
        else
        {
            currentHorizontalVelocity = Vector3.Lerp(
                currentHorizontalVelocity,
                targetVelocity,
                airControl * Time.deltaTime * 5f
            );
        }
    }

    private Vector3 GetCameraRelativeDirection(Vector2 input)
    {
        Vector3 direction;

        if (cameraRelativeMovement && cameraTransform != null)
        {
            Vector3 cameraForward = cameraTransform.forward;
            Vector3 cameraRight = cameraTransform.right;

            cameraForward.y = 0f;
            cameraRight.y = 0f;

            cameraForward.Normalize();
            cameraRight.Normalize();

            direction = cameraForward * input.y + cameraRight * input.x;
        }
        else
        {
            direction = new Vector3(input.x, 0f, input.y);
        }

        if (direction.sqrMagnitude > 1f)
            direction.Normalize();

        return direction;
    }

private void CalculateRotation()
{
    if (!hasMovementInput)
        return;

    if (desiredMoveDirection.sqrMagnitude < 0.001f)
        return;

    Transform visualTarget = characterVisual != null ? characterVisual : transform;

    float targetAngle = Mathf.Atan2(
        desiredMoveDirection.x,
        desiredMoveDirection.z
    ) * Mathf.Rad2Deg;

    float currentAngle = visualTarget.eulerAngles.y;

    float smoothAngle = Mathf.SmoothDampAngle(
        currentAngle,
        targetAngle,
        ref rotationVelocityRef,
        rotationSmoothTime
    );

    float limitedAngle = Mathf.MoveTowardsAngle(
        currentAngle,
        smoothAngle,
        maxTurnSpeed * Time.deltaTime
    );

    visualTarget.rotation = Quaternion.Euler(0f, limitedAngle, 0f);
}
private void CalculateJumpAndGravity()
{
    bool canJump =
        jumpBufferCounter > 0f &&
        (isGrounded || coyoteCounter > 0f) &&
        !jumpConsumed &&
        Time.time >= nextAllowedJumpTime;

    if (canJump)
    {
        verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);

        nextAllowedJumpTime = Time.time + jumpCooldown;

        jumpBufferCounter = 0f;
        coyoteCounter = 0f;
        jumpHoldCounter = jumpHoldTime;
        groundLockCounter = groundLockAfterJump;
        airTimeCounter = 0f;

        isGrounded = false;
        isJumping = true;
        jumpConsumed = true;

        SetAnimatorTrigger("Jump");
    }

    bool holdingJump = Input.GetKey(jumpKey);

    if (isJumping && holdingJump && jumpHoldCounter > 0f && verticalVelocity > 0f)
    {
        verticalVelocity += jumpHoldForce * Time.deltaTime;
        jumpHoldCounter -= Time.deltaTime;
    }
    else
    {
        jumpHoldCounter = 0f;
    }

    float gravityMultiplier = 1f;

    if (verticalVelocity < 0f)
        gravityMultiplier = fallGravityMultiplier;
    else if (verticalVelocity > 0f && !holdingJump)
        gravityMultiplier = lowJumpGravityMultiplier;

    verticalVelocity += gravity * gravityMultiplier * Time.deltaTime;

    if (verticalVelocity < terminalVelocity)
        verticalVelocity = terminalVelocity;

    if (isGrounded && verticalVelocity < 0f)
        verticalVelocity = groundedForce;
}
    private void ApplyMovement()
    {
        Vector3 finalVelocity = currentHorizontalVelocity;
        finalVelocity.y = verticalVelocity;

        controller.Move(finalVelocity * Time.deltaTime);
    }

    private void UpdateAnimator()
    {
        if (animator == null)
            return;

        float normalizedSpeed = Mathf.InverseLerp(
            0f,
            runSpeed,
            currentHorizontalVelocity.magnitude
        );

        SetAnimatorFloat("Speed", normalizedSpeed);
        SetAnimatorBool("Walking", isWalking);
        SetAnimatorBool("Running", isRunning);
        SetAnimatorBool("IsRunning", isRunning);
        SetAnimatorBool("IsGrounded", isGrounded);
        SetAnimatorBool("IsJumping", isJumping);
        SetAnimatorFloat("VerticalVelocity", verticalVelocity);

        SetAnimatorFloat("MoveX", smoothInput.x);
        SetAnimatorFloat("MoveY", smoothInput.y);

        SetAnimatorBool("StrafeLeft", smoothInput.x < -0.25f);
        SetAnimatorBool("StrafeRight", smoothInput.x > 0.25f);
        SetAnimatorBool("Backpedal", smoothInput.y < -0.25f);
    }

    private void SetAnimatorFloat(string parameterName, float value)
    {
        if (animatorParams.Contains(parameterName))
            animator.SetFloat(parameterName, value);
    }

    private void SetAnimatorBool(string parameterName, bool value)
    {
        if (animatorParams.Contains(parameterName))
            animator.SetBool(parameterName, value);
    }

    private void SetAnimatorTrigger(string parameterName)
    {
        if (animatorParams.Contains(parameterName))
            animator.SetTrigger(parameterName);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;

        Vector3 origin;

        if (groundCheck != null)
            origin = groundCheck.position + Vector3.up * 0.15f;
        else
            origin = transform.position + Vector3.up * 0.25f;

        Gizmos.DrawWireSphere(origin, groundCheckRadius);
        Gizmos.DrawLine(origin, origin + Vector3.down * groundCheckDistance);
        Gizmos.DrawWireSphere(origin + Vector3.down * groundCheckDistance, groundCheckRadius);
    }
}