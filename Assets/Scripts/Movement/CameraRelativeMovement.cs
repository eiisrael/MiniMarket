using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Movimentação do personagem baseada na direção horizontal da câmera.
///
/// Inclui aceleração suave, corrida, pulo, gravidade, Animator compatível,
/// input externo para mobile e energia segmentada persistida no banco local.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(CharacterController))]
[DefaultExecutionOrder(100)]
public sealed class CameraRelativeMovement : MonoBehaviour
{
    [Header("Referências")]
    public Transform cameraTransform;
    public PlayerCameraController playerCamera;

    [HideInInspector]
    public CameraModeController cameraMode;

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

    [Header("Stamina / Energia")]
    public bool useStamina = true;
    [Min(1f)] public float maxStamina = 100f;
    [SerializeField, Min(0f)] private float currentStamina = 100f;
    [Min(0f)] public float runDrainPerSecond = 15f;
    [Min(0f)] public float recoveryPerSecond = 11f;
    [Min(0f)] public float recoveryDelay = 0.8f;
    [Min(0f)] public float minimumStaminaToStartRunning = 5f;

    [Header("Energia segmentada")]
    public bool useSegmentedEnergy = true;
    [Min(1)] public int maxEnergySegments = 5;
    [SerializeField, Min(0)] private int currentEnergySegments = 5;
    [SerializeField, Min(0f)] private float energyRechargeReserve;
    public bool refillBarWhenSegmentIsConsumed = true;
    public bool rechargeAdditionalSegments = true;

    [Header("Persistência")]
    public bool usePlayerDatabase = true;
    [Min(0.05f)] public float minimumDatabaseDifference = 0.25f;
    [Min(0.25f)] public float databaseSyncInterval = 0.75f;

    [Header("Animator - parâmetros principais")]
    public string speedParameter = "Speed";
    public string groundedParameter = "Grounded";
    public string verticalSpeedParameter = "VerticalSpeed";
    [Min(0f)] public float animatorDampTime = 0.08f;
    public bool normalizedSpeedForAnimator = true;
    public bool disableAnimatorRootMotion = true;
    public bool keepAnimatorEnabled = true;

    [Header("Animator - compatibilidade automática")]
    public bool autoDetectCommonAnimatorParameters = true;
    public bool useStateFallbackWhenNoParameters = true;
    public string idleStateName = "Idle";
    public string walkStateName = "Walk";
    public string runStateName = "Run";
    public string jumpStateName = "Jump";
    [Min(0f)] public float stateTransitionDuration = 0.12f;
    public bool logAnimatorDiagnostics;

    [Header("Input externo / Mobile")]
    public bool useLegacyInput = true;

    private CharacterController controller;
    private Vector2 externalMoveInput;
    private bool externalRun;
    private bool externalJump;
    private Vector3 horizontalVelocity;
    private float verticalVelocity;
    private float lastRunTime = -100f;
    private bool running;
    private bool jumpedThisFrame;

    private MiniMarketPlayerDatabase database;
    private bool applyingDatabaseState;
    private bool staminaDirty;
    private float nextDatabaseSync;
    private float lastSavedStamina;
    private float lastSavedReserve;
    private int lastSavedSegments;
    private int lastSavedMaxSegments;

    private RuntimeAnimatorController cachedAnimatorController;
    private readonly HashSet<int> floatParameters = new HashSet<int>();
    private readonly HashSet<int> boolParameters = new HashSet<int>();
    private readonly HashSet<int> triggerParameters = new HashSet<int>();

    private int speedHash;
    private int groundedHash;
    private int verticalSpeedHash;
    private bool hasSpeedParameter;
    private bool hasGroundedParameter;
    private bool hasVerticalSpeedParameter;
    private bool hasAnyLocomotionParameter;

    private int idleStateHash;
    private int walkStateHash;
    private int runStateHash;
    private int jumpStateHash;
    private int currentFallbackStateHash;

    private bool loggedMissingAnimator;
    private bool loggedNoAnimationMapping;

    private static readonly int WalkingHash = Animator.StringToHash("Walking");
    private static readonly int IsWalkingHash = Animator.StringToHash("IsWalking");
    private static readonly int RunningHash = Animator.StringToHash("Running");
    private static readonly int IsRunningHash = Animator.StringToHash("IsRunning");
    private static readonly int IsGroundedHash = Animator.StringToHash("IsGrounded");
    private static readonly int JumpingHash = Animator.StringToHash("Jumping");
    private static readonly int IsJumpingHash = Animator.StringToHash("IsJumping");
    private static readonly int JumpHash = Animator.StringToHash("Jump");
    private static readonly int VerticalVelocityHash = Animator.StringToHash("VerticalVelocity");
    private static readonly int MoveXHash = Animator.StringToHash("MoveX");
    private static readonly int MoveYHash = Animator.StringToHash("MoveY");
    private static readonly int InputXHash = Animator.StringToHash("InputX");
    private static readonly int InputYHash = Animator.StringToHash("InputY");
    private static readonly int MoveSpeedHash = Animator.StringToHash("MoveSpeed");
    private static readonly int VelocityHash = Animator.StringToHash("Velocity");
    private static readonly int StaminaHash = Animator.StringToHash("Stamina");
    private static readonly int IsTiredHash = Animator.StringToHash("IsTired");

    public event Action OnStaminaChanged;

    public Vector3 Velocity => horizontalVelocity + Vector3.up * verticalVelocity;
    public float CurrentSpeed => horizontalVelocity.magnitude;
    public bool IsRunning => running;
    public bool IsGrounded => controller != null && controller.isGrounded;
    public float CurrentStamina => currentStamina;
    public float MaxStamina => maxStamina;
    public float Stamina01 => maxStamina > 0.001f ? Mathf.Clamp01(currentStamina / maxStamina) : 0f;

    public float StaminaAtual => CurrentStamina;
    public float StaminaMaxima => MaxStamina;
    public float StaminaPercentual01 => Stamina01;
    public bool EstaCorrendo => IsRunning;
    public bool EstaGastandoStamina => IsRunning && useStamina;
    public bool EstaCansado => useStamina && currentEnergySegments <= 0 && currentStamina <= 0.01f;
    public int StaminaSegmentosMaximos => useSegmentedEnergy ? Mathf.Max(1, maxEnergySegments) : 1;
    public int StaminaSegmentosAtuais => useSegmentedEnergy
        ? Mathf.Clamp(currentEnergySegments, 0, StaminaSegmentosMaximos)
        : (currentStamina > 0.001f ? 1 : 0);
    public string StaminaSegmentadaTexto => StaminaSegmentosAtuais + "/" + StaminaSegmentosMaximos;
    public float StaminaRecargaReserva => energyRechargeReserve;

    public float EnergiaPercentual01
    {
        get
        {
            if (!useSegmentedEnergy)
                return Stamina01;

            float barrasCompletas = Mathf.Max(0, StaminaSegmentosAtuais - 1);
            float barraAtual = StaminaSegmentosAtuais > 0 ? Stamina01 : 0f;
            return Mathf.Clamp01((barrasCompletas + barraAtual) / StaminaSegmentosMaximos);
        }
    }

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        maxStamina = Mathf.Max(1f, maxStamina);
        maxEnergySegments = Mathf.Max(1, maxEnergySegments);
        currentStamina = Mathf.Clamp(currentStamina, 0f, maxStamina);
        currentEnergySegments = Mathf.Clamp(currentEnergySegments, 0, maxEnergySegments);
        energyRechargeReserve = Mathf.Clamp(energyRechargeReserve, 0f, maxStamina);

        RefreshAnimatorConfiguration();
        ResolveDatabase(true);
    }

    private void OnEnable()
    {
        RefreshAnimatorConfiguration();
        ResolveDatabase(true);
        SubscribeDatabase();
    }

    private void Start()
    {
        ResolveCamera();
        RefreshAnimatorConfiguration();
        ResolveDatabase(true);
    }

    private void OnDisable()
    {
        SaveStaminaToDatabase(true);
        UnsubscribeDatabase();
    }

    private void OnApplicationPause(bool paused)
    {
        if (paused)
            SaveStaminaToDatabase(true);
    }

    private void OnApplicationQuit()
    {
        SaveStaminaToDatabase(true);
    }

    private void Update()
    {
        ResolveCamera();
        EnsureAnimatorConfigurationIsCurrent();

        float deltaTime = Mathf.Clamp(Time.deltaTime, 0f, 0.05f);
        if (deltaTime <= 0f)
            return;

        jumpedThisFrame = false;

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

        UpdateAnimator(input, deltaTime);
        TrySyncDatabase();
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
        maxStamina = Mathf.Max(1f, maxStamina);
        maxEnergySegments = Mathf.Max(1, maxEnergySegments);
        currentStamina = maxStamina;
        currentEnergySegments = useSegmentedEnergy ? maxEnergySegments : 1;
        energyRechargeReserve = 0f;
        running = false;
        MarkStaminaChanged();
        SaveStaminaToDatabase(true);
    }

    public void RestaurarStaminaCompleta() => RestoreStaminaFull();
    public void RecarregarStaminaCompleta() => RestoreStaminaFull();
    public void RestaurarEnergiaCompleta() => RestoreStaminaFull();
    public void RecarregarEnergiaCompleta() => RestoreStaminaFull();

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
            {
                verticalVelocity = Mathf.Sqrt(Mathf.Max(0f, jumpHeight * 2f * gravity));
                jumpedThisFrame = true;
            }
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

        if (EstaCansado)
            return false;

        return wasRunning
            ? currentStamina > 0.01f || StaminaSegmentosAtuais > 1
            : currentStamina >= minimumStaminaToStartRunning || StaminaSegmentosAtuais > 1;
    }

    private void UpdateStamina(float deltaTime)
    {
        maxStamina = Mathf.Max(1f, maxStamina);
        maxEnergySegments = Mathf.Max(1, maxEnergySegments);

        float previousStamina = currentStamina;
        float previousReserve = energyRechargeReserve;
        int previousSegments = currentEnergySegments;

        if (!useStamina)
        {
            currentStamina = maxStamina;
            currentEnergySegments = useSegmentedEnergy ? maxEnergySegments : 1;
            energyRechargeReserve = 0f;
        }
        else if (running && horizontalVelocity.sqrMagnitude > 0.05f)
        {
            currentStamina = Mathf.Max(0f, currentStamina - runDrainPerSecond * deltaTime);
            lastRunTime = Time.time;

            if (currentStamina <= 0.001f)
                ConsumeEnergySegment();
        }
        else if (Time.time - lastRunTime >= recoveryDelay)
        {
            RecoverEnergy(deltaTime);
        }

        currentStamina = Mathf.Clamp(currentStamina, 0f, maxStamina);
        currentEnergySegments = Mathf.Clamp(currentEnergySegments, 0, maxEnergySegments);
        energyRechargeReserve = Mathf.Clamp(energyRechargeReserve, 0f, maxStamina);

        if (Mathf.Abs(previousStamina - currentStamina) > 0.001f ||
            Mathf.Abs(previousReserve - energyRechargeReserve) > 0.001f ||
            previousSegments != currentEnergySegments)
        {
            MarkStaminaChanged();
        }
    }

    private void ConsumeEnergySegment()
    {
        if (!useSegmentedEnergy)
        {
            currentStamina = 0f;
            running = false;
            return;
        }

        if (currentEnergySegments > 1)
        {
            currentEnergySegments--;
            currentStamina = refillBarWhenSegmentIsConsumed ? maxStamina : 0f;
            return;
        }

        currentEnergySegments = 0;
        currentStamina = 0f;
        running = false;
    }

    private void RecoverEnergy(float deltaTime)
    {
        float recovery = Mathf.Max(0f, recoveryPerSecond) * deltaTime;
        if (recovery <= 0f)
            return;

        if (!useSegmentedEnergy)
        {
            currentStamina = Mathf.Min(maxStamina, currentStamina + recovery);
            return;
        }

        if (currentEnergySegments <= 0)
        {
            currentStamina = Mathf.Min(maxStamina, currentStamina + recovery);
            if (currentStamina > 0.001f)
                currentEnergySegments = 1;
            return;
        }

        if (currentStamina < maxStamina - 0.001f)
        {
            currentStamina = Mathf.Min(maxStamina, currentStamina + recovery);
            return;
        }

        if (!rechargeAdditionalSegments || currentEnergySegments >= maxEnergySegments)
        {
            energyRechargeReserve = 0f;
            return;
        }

        energyRechargeReserve += recovery;
        while (energyRechargeReserve >= maxStamina && currentEnergySegments < maxEnergySegments)
        {
            energyRechargeReserve -= maxStamina;
            currentEnergySegments++;
        }

        if (currentEnergySegments >= maxEnergySegments)
            energyRechargeReserve = 0f;
    }

    private void MarkStaminaChanged()
    {
        staminaDirty = true;
        OnStaminaChanged?.Invoke();
    }

    private void ResolveDatabase(bool loadState)
    {
        if (!usePlayerDatabase || !Application.isPlaying)
            return;

        if (database == null)
            database = MiniMarketPlayerDatabase.ObterOuCriar();

        if (database == null)
            return;

        database.GarantirStaminaInicial(currentStamina, maxStamina);

        if (loadState)
            ApplyDatabaseState(database.Dados);
    }

    private void SubscribeDatabase()
    {
        if (database == null)
            return;

        database.OnDatabaseChanged -= OnDatabaseChanged;
        database.OnDatabaseChanged += OnDatabaseChanged;
    }

    private void UnsubscribeDatabase()
    {
        if (database != null)
            database.OnDatabaseChanged -= OnDatabaseChanged;
    }

    private void OnDatabaseChanged(MiniMarketPlayerDatabase.MiniMarketPlayerData data)
    {
        if (applyingDatabaseState || data == null)
            return;

        bool databaseDiffers =
            Mathf.Abs(data.staminaAtual - currentStamina) > 0.01f ||
            Mathf.Abs(data.staminaMaxima - maxStamina) > 0.01f ||
            data.energiaSegmentosAtuais != currentEnergySegments ||
            data.energiaSegmentosMaximos != maxEnergySegments ||
            Mathf.Abs(data.energiaRecargaReserva - energyRechargeReserve) > 0.01f;

        if (databaseDiffers && !staminaDirty)
            ApplyDatabaseState(data);
    }

    private void ApplyDatabaseState(MiniMarketPlayerDatabase.MiniMarketPlayerData data)
    {
        if (data == null)
            return;

        applyingDatabaseState = true;

        maxStamina = Mathf.Max(1f, data.staminaMaxima);
        currentStamina = Mathf.Clamp(data.staminaAtual, 0f, maxStamina);
        maxEnergySegments = Mathf.Max(1, data.energiaSegmentosMaximos);
        currentEnergySegments = Mathf.Clamp(data.energiaSegmentosAtuais, 0, maxEnergySegments);
        energyRechargeReserve = Mathf.Clamp(data.energiaRecargaReserva, 0f, maxStamina);

        CaptureSavedState();
        staminaDirty = false;
        applyingDatabaseState = false;
        OnStaminaChanged?.Invoke();
    }

    private void TrySyncDatabase()
    {
        if (!staminaDirty || !usePlayerDatabase || Time.unscaledTime < nextDatabaseSync)
            return;

        bool meaningfulDifference =
            Mathf.Abs(lastSavedStamina - currentStamina) >= minimumDatabaseDifference ||
            Mathf.Abs(lastSavedReserve - energyRechargeReserve) >= minimumDatabaseDifference ||
            lastSavedSegments != currentEnergySegments ||
            lastSavedMaxSegments != maxEnergySegments;

        if (meaningfulDifference)
            SaveStaminaToDatabase(false);
    }

    private void SaveStaminaToDatabase(bool saveNow)
    {
        if (!usePlayerDatabase || !Application.isPlaying || MiniMarketPlayerDatabase.EncerrandoAplicacao)
            return;

        ResolveDatabase(false);
        if (database == null)
            return;

        applyingDatabaseState = true;
        database.DefinirEnergiaSegmentada(
            currentStamina,
            maxStamina,
            currentEnergySegments,
            maxEnergySegments,
            energyRechargeReserve,
            saveNow
        );
        applyingDatabaseState = false;

        CaptureSavedState();
        staminaDirty = false;
        nextDatabaseSync = Time.unscaledTime + Mathf.Max(0.25f, databaseSyncInterval);
    }

    private void CaptureSavedState()
    {
        lastSavedStamina = currentStamina;
        lastSavedReserve = energyRechargeReserve;
        lastSavedSegments = currentEnergySegments;
        lastSavedMaxSegments = maxEnergySegments;
    }

    public bool RefreshAnimatorConfiguration()
    {
        if (animator == null)
            animator = GetComponentInChildren<Animator>(true);

        if (animator == null)
        {
            LogAnimatorWarningOnce(
                "Animator não encontrado no personagem ou nos filhos.",
                ref loggedMissingAnimator
            );
            ClearAnimatorCache();
            return false;
        }

        if (keepAnimatorEnabled)
            animator.enabled = true;
        if (disableAnimatorRootMotion)
            animator.applyRootMotion = false;

        cachedAnimatorController = animator.runtimeAnimatorController;
        CacheAnimatorParameters();
        CacheFallbackStates();

        bool ready = cachedAnimatorController != null &&
                     (hasAnyLocomotionParameter || idleStateHash != 0 || walkStateHash != 0);

        if (!ready)
        {
            LogAnimatorWarningOnce(
                "Animator Controller sem parâmetros/estados de locomoção reconhecidos.",
                ref loggedNoAnimationMapping
            );
        }

        return ready;
    }

    private void EnsureAnimatorConfigurationIsCurrent()
    {
        if (animator == null || animator.runtimeAnimatorController != cachedAnimatorController)
            RefreshAnimatorConfiguration();
    }

    private void CacheAnimatorParameters()
    {
        floatParameters.Clear();
        boolParameters.Clear();
        triggerParameters.Clear();
        hasSpeedParameter = false;
        hasGroundedParameter = false;
        hasVerticalSpeedParameter = false;
        hasAnyLocomotionParameter = false;

        speedHash = Animator.StringToHash(speedParameter ?? string.Empty);
        groundedHash = Animator.StringToHash(groundedParameter ?? string.Empty);
        verticalSpeedHash = Animator.StringToHash(verticalSpeedParameter ?? string.Empty);

        if (animator == null || animator.runtimeAnimatorController == null)
            return;

        AnimatorControllerParameter[] parameters = animator.parameters;
        for (int i = 0; i < parameters.Length; i++)
        {
            AnimatorControllerParameter parameter = parameters[i];
            switch (parameter.type)
            {
                case AnimatorControllerParameterType.Float:
                    floatParameters.Add(parameter.nameHash);
                    break;
                case AnimatorControllerParameterType.Bool:
                    boolParameters.Add(parameter.nameHash);
                    break;
                case AnimatorControllerParameterType.Trigger:
                    triggerParameters.Add(parameter.nameHash);
                    break;
            }
        }

        hasSpeedParameter = floatParameters.Contains(speedHash);
        hasGroundedParameter = boolParameters.Contains(groundedHash);
        hasVerticalSpeedParameter = floatParameters.Contains(verticalSpeedHash);

        hasAnyLocomotionParameter =
            hasSpeedParameter ||
            floatParameters.Contains(MoveSpeedHash) ||
            floatParameters.Contains(VelocityHash) ||
            floatParameters.Contains(MoveXHash) ||
            floatParameters.Contains(MoveYHash) ||
            boolParameters.Contains(WalkingHash) ||
            boolParameters.Contains(IsWalkingHash) ||
            boolParameters.Contains(RunningHash) ||
            boolParameters.Contains(IsRunningHash);
    }

    private void CacheFallbackStates()
    {
        idleStateHash = 0;
        walkStateHash = 0;
        runStateHash = 0;
        jumpStateHash = 0;
        currentFallbackStateHash = 0;

        if (animator == null || animator.runtimeAnimatorController == null)
            return;

        idleStateHash = FindFirstStateHash(idleStateName, "Idle", "Standing", "Locomotion.Idle");
        walkStateHash = FindFirstStateHash(walkStateName, "Walk", "Walking", "Locomotion.Walk");
        runStateHash = FindFirstStateHash(runStateName, "Run", "Running", "Locomotion.Run");
        jumpStateHash = FindFirstStateHash(jumpStateName, "Jump", "Jumping", "Locomotion.Jump");
    }

    private int FindFirstStateHash(params string[] candidates)
    {
        if (animator == null || candidates == null)
            return 0;

        for (int i = 0; i < candidates.Length; i++)
        {
            string candidate = candidates[i];
            if (string.IsNullOrWhiteSpace(candidate))
                continue;

            int fullPathHash = Animator.StringToHash("Base Layer." + candidate);
            if (animator.HasState(0, fullPathHash))
                return fullPathHash;

            int directHash = Animator.StringToHash(candidate);
            if (animator.HasState(0, directHash))
                return directHash;
        }

        return 0;
    }

    private void UpdateAnimator(Vector2 input, float deltaTime)
    {
        if (animator == null || animator.runtimeAnimatorController == null)
            return;

        if (keepAnimatorEnabled && !animator.enabled)
            animator.enabled = true;

        bool grounded = controller != null && controller.isGrounded;
        bool moving = CurrentSpeed > 0.05f && input.sqrMagnitude > 0.001f;
        bool walking = moving && !running;
        bool jumping = !grounded || verticalVelocity > 0.1f;

        float normalizedSpeed = runSpeed > 0.001f
            ? Mathf.Clamp01(CurrentSpeed / runSpeed)
            : 0f;
        float speedValue = normalizedSpeedForAnimator ? normalizedSpeed : CurrentSpeed;

        if (hasSpeedParameter)
            animator.SetFloat(speedHash, speedValue, animatorDampTime, deltaTime);
        if (hasGroundedParameter)
            animator.SetBool(groundedHash, grounded);
        if (hasVerticalSpeedParameter)
            animator.SetFloat(verticalSpeedHash, verticalVelocity);

        if (autoDetectCommonAnimatorParameters)
        {
            SetFloatIfExists(MoveSpeedHash, speedValue, deltaTime);
            SetFloatIfExists(VelocityHash, speedValue, deltaTime);
            SetFloatIfExists(VerticalVelocityHash, verticalVelocity, deltaTime, false);
            SetFloatIfExists(MoveXHash, input.x, deltaTime);
            SetFloatIfExists(MoveYHash, input.y, deltaTime);
            SetFloatIfExists(InputXHash, input.x, deltaTime);
            SetFloatIfExists(InputYHash, input.y, deltaTime);
            SetFloatIfExists(StaminaHash, EnergiaPercentual01, deltaTime, false);

            SetBoolIfExists(WalkingHash, walking);
            SetBoolIfExists(IsWalkingHash, walking);
            SetBoolIfExists(RunningHash, running && moving);
            SetBoolIfExists(IsRunningHash, running && moving);
            SetBoolIfExists(IsGroundedHash, grounded);
            SetBoolIfExists(JumpingHash, jumping);
            SetBoolIfExists(IsJumpingHash, jumping);
            SetBoolIfExists(IsTiredHash, EstaCansado);
        }

        if (jumpedThisFrame)
            SetTriggerIfExists(JumpHash);

        if (useStateFallbackWhenNoParameters && !hasAnyLocomotionParameter)
            UpdateFallbackAnimatorState(moving, grounded);
    }

    private void UpdateFallbackAnimatorState(bool moving, bool grounded)
    {
        int desiredState = 0;

        if (!grounded && jumpStateHash != 0)
            desiredState = jumpStateHash;
        else if (running && moving && runStateHash != 0)
            desiredState = runStateHash;
        else if (moving && walkStateHash != 0)
            desiredState = walkStateHash;
        else if (idleStateHash != 0)
            desiredState = idleStateHash;

        if (desiredState == 0 || desiredState == currentFallbackStateHash)
            return;

        currentFallbackStateHash = desiredState;
        animator.CrossFade(desiredState, Mathf.Max(0f, stateTransitionDuration), 0);
    }

    private void SetFloatIfExists(int hash, float value, float deltaTime, bool damp = true)
    {
        if (!floatParameters.Contains(hash))
            return;

        if (damp && animatorDampTime > 0f)
            animator.SetFloat(hash, value, animatorDampTime, deltaTime);
        else
            animator.SetFloat(hash, value);
    }

    private void SetBoolIfExists(int hash, bool value)
    {
        if (boolParameters.Contains(hash))
            animator.SetBool(hash, value);
    }

    private void SetTriggerIfExists(int hash)
    {
        if (triggerParameters.Contains(hash))
            animator.SetTrigger(hash);
    }

    private void ClearAnimatorCache()
    {
        cachedAnimatorController = null;
        floatParameters.Clear();
        boolParameters.Clear();
        triggerParameters.Clear();
        hasSpeedParameter = false;
        hasGroundedParameter = false;
        hasVerticalSpeedParameter = false;
        hasAnyLocomotionParameter = false;
        idleStateHash = 0;
        walkStateHash = 0;
        runStateHash = 0;
        jumpStateHash = 0;
        currentFallbackStateHash = 0;
    }

    private void LogAnimatorWarningOnce(string message, ref bool alreadyLogged)
    {
        if (!logAnimatorDiagnostics || alreadyLogged)
            return;

        alreadyLogged = true;
        Debug.LogWarning("[CameraRelativeMovement] " + message, this);
    }

    private void ResolveCamera()
    {
        if (playerCamera == null)
            playerCamera = UnityEngine.Object.FindAnyObjectByType<PlayerCameraController>(FindObjectsInactive.Include);

        if (cameraTransform == null && playerCamera != null)
            cameraTransform = playerCamera.ActiveCameraTransform;

        if (cameraTransform == null && Camera.main != null)
            cameraTransform = Camera.main.transform;
    }

    private void OnValidate()
    {
        walkSpeed = Mathf.Max(0f, walkSpeed);
        runSpeed = Mathf.Max(walkSpeed, runSpeed);
        acceleration = Mathf.Max(0f, acceleration);
        deceleration = Mathf.Max(0f, deceleration);
        gravity = Mathf.Max(0f, gravity);
        terminalVelocity = Mathf.Max(0f, terminalVelocity);
        maxStamina = Mathf.Max(1f, maxStamina);
        currentStamina = Mathf.Clamp(currentStamina, 0f, maxStamina);
        maxEnergySegments = Mathf.Max(1, maxEnergySegments);
        currentEnergySegments = Mathf.Clamp(currentEnergySegments, 0, maxEnergySegments);
        energyRechargeReserve = Mathf.Clamp(energyRechargeReserve, 0f, maxStamina);
        minimumStaminaToStartRunning = Mathf.Clamp(minimumStaminaToStartRunning, 0f, maxStamina);
        databaseSyncInterval = Mathf.Max(0.25f, databaseSyncInterval);
        minimumDatabaseDifference = Mathf.Max(0.05f, minimumDatabaseDifference);
        animatorDampTime = Mathf.Max(0f, animatorDampTime);
        stateTransitionDuration = Mathf.Max(0f, stateTransitionDuration);

        if (!Application.isPlaying)
        {
            controller = GetComponent<CharacterController>();
            if (animator == null)
                animator = GetComponentInChildren<Animator>(true);
        }
    }
}
