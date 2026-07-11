using UnityEngine;

/// <summary>
/// Controlador definitivo da câmera do jogador.
/// Uma única Camera e um único AudioListener atendem primeira e terceira pessoa.
/// ThirdPersonCamera e FirstPersonCamera apenas calculam poses; este componente é
/// o único que escreve posição, rotação e FOV.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Camera))]
[RequireComponent(typeof(ThirdPersonCamera))]
[RequireComponent(typeof(FirstPersonCamera))]
[DefaultExecutionOrder(1000)]
public sealed class PlayerCameraController : MonoBehaviour
{
    public enum ViewMode
    {
        ThirdPerson,
        FirstPerson
    }

    [Header("Referências")]
    public Camera gameCamera;
    public ThirdPersonCamera thirdPerson;
    public FirstPersonCamera firstPerson;
    public Transform player;
    public CameraRelativeMovement movement;

    [Header("Modo")]
    public ViewMode initialMode = ViewMode.ThirdPerson;
    public KeyCode toggleKey = KeyCode.V;
    public bool holdRightMouseForFirstPerson = true;
    [Range(0, 2)] public int firstPersonMouseButton = 1;

    [Header("Transição")]
    [Min(0f)] public float positionSmoothTime = 0.035f;
    [Min(0f)] public float rotationSpeed = 24f;
    [Min(0f)] public float fieldOfViewSpeed = 16f;
    public bool instantOnStart = true;
    public bool instantOnModeChange = true;

    [Header("Primeira pessoa")]
    public Renderer[] renderersHiddenInFirstPerson;

    [Header("Cursor")]
    public bool lockCursorDuringGameplay = true;

    [Header("Segurança")]
    public bool disableOtherCameras = true;
    public bool disableOtherAudioListeners = true;
    [Min(0.1f)] public float initialValidationDuration = 3f;
    [Min(0.05f)] public float initialValidationInterval = 0.25f;

    private ViewMode currentMode;
    private Vector3 positionVelocity;
    private bool previousMouseState;
    private float startTime;
    private float nextValidationTime;
    private bool cursorInitialized;

    public ViewMode CurrentMode => currentMode;
    public bool IsFirstPerson => currentMode == ViewMode.FirstPerson;
    public Transform ActiveCameraTransform => gameCamera != null ? gameCamera.transform : transform;

    private void Reset()
    {
        ResolveReferences();
    }

    private void Awake()
    {
        startTime = Time.unscaledTime;
        ResolveReferences();
        currentMode = initialMode;
        InitializeModes();
        EnforceSingleCameraAndListener();
        ForceGameplayCursor();
        ApplyRendererVisibility();

        if (instantOnStart)
            ForceImmediatePose();
    }

    private void Start()
    {
        ResolveReferences();
        EnforceSingleCameraAndListener();

        if (instantOnStart)
            ForceImmediatePose();
    }

    private void Update()
    {
        ResolveReferences();

        if (Time.unscaledTime - startTime <= initialValidationDuration &&
            Time.unscaledTime >= nextValidationTime)
        {
            nextValidationTime = Time.unscaledTime + Mathf.Max(0.05f, initialValidationInterval);
            EnforceSingleCameraAndListener();
        }

        HandleModeInput();
        ApplyCursorState();

        if (GameplayInputState.IsBlocked)
            return;

        float deltaTime = SafeDeltaTime();
        if (IsFirstPerson)
            firstPerson?.ReadInput(deltaTime);
        else
            thirdPerson?.ReadInput(deltaTime);
    }

    private void LateUpdate()
    {
        ResolveReferences();
        UpdatePose(false);

        if (movement != null && movement.cameraTransform != ActiveCameraTransform)
            movement.cameraTransform = ActiveCameraTransform;
    }

    public void SetMode(ViewMode mode)
    {
        if (currentMode == mode)
            return;

        SynchronizeAngles(mode);
        currentMode = mode;
        positionVelocity = Vector3.zero;
        ApplyRendererVisibility();

        if (instantOnModeChange)
            ForceImmediatePose();
    }

    public void ToggleMode()
    {
        SetMode(IsFirstPerson ? ViewMode.ThirdPerson : ViewMode.FirstPerson);
    }

    public void ForceImmediatePose()
    {
        UpdatePose(true);
    }

    private void HandleModeInput()
    {
        if (GameplayInputState.IsBlocked)
        {
            previousMouseState = false;
            return;
        }

        if (holdRightMouseForFirstPerson)
        {
            bool pressed = Input.GetMouseButton(firstPersonMouseButton);
            if (pressed != previousMouseState)
            {
                previousMouseState = pressed;
                SetMode(pressed ? ViewMode.FirstPerson : ViewMode.ThirdPerson);
            }
        }

        if (toggleKey != KeyCode.None && Input.GetKeyDown(toggleKey))
            ToggleMode();
    }

    private void UpdatePose(bool immediate)
    {
        if (gameCamera == null)
            return;

        float deltaTime = SafeDeltaTime();
        bool valid;
        Vector3 targetPosition;
        Quaternion targetRotation;
        float targetFieldOfView;

        if (IsFirstPerson)
        {
            valid = firstPerson != null && firstPerson.TryGetPose(
                deltaTime,
                out targetPosition,
                out targetRotation,
                out targetFieldOfView
            );
        }
        else
        {
            valid = thirdPerson != null && thirdPerson.TryGetPose(
                out targetPosition,
                out targetRotation,
                out targetFieldOfView
            );
        }

        if (!valid)
            return;

        Transform cameraTransform = gameCamera.transform;

        if (immediate || positionSmoothTime <= 0.0001f)
        {
            cameraTransform.SetPositionAndRotation(targetPosition, targetRotation);
            positionVelocity = Vector3.zero;
        }
        else
        {
            cameraTransform.position = Vector3.SmoothDamp(
                cameraTransform.position,
                targetPosition,
                ref positionVelocity,
                positionSmoothTime,
                Mathf.Infinity,
                deltaTime
            );

            cameraTransform.rotation = rotationSpeed <= 0.0001f
                ? targetRotation
                : Quaternion.Slerp(
                    cameraTransform.rotation,
                    targetRotation,
                    1f - Mathf.Exp(-rotationSpeed * deltaTime)
                );
        }

        gameCamera.fieldOfView = immediate || fieldOfViewSpeed <= 0.0001f
            ? targetFieldOfView
            : Mathf.Lerp(
                gameCamera.fieldOfView,
                targetFieldOfView,
                1f - Mathf.Exp(-fieldOfViewSpeed * deltaTime)
            );
    }

    private void ResolveReferences()
    {
        if (gameCamera == null)
            gameCamera = GetComponent<Camera>();

        if (thirdPerson == null)
            thirdPerson = GetComponent<ThirdPersonCamera>();

        if (firstPerson == null)
            firstPerson = GetComponent<FirstPersonCamera>();

        if (movement == null && player != null)
            movement = player.GetComponent<CameraRelativeMovement>();

        if (player == null && movement != null)
            player = movement.transform;

        if (thirdPerson != null && thirdPerson.target == null)
            thirdPerson.target = player;

        if (firstPerson != null && firstPerson.playerBody == null)
            firstPerson.playerBody = player;

        if (movement != null)
        {
            movement.cameraTransform = ActiveCameraTransform;
            movement.cameraMode = null;
        }
    }

    private void InitializeModes()
    {
        thirdPerson?.Initialize(transform);
        firstPerson?.Initialize(transform);
    }

    private void SynchronizeAngles(ViewMode destination)
    {
        if (thirdPerson == null || firstPerson == null)
            return;

        if (destination == ViewMode.FirstPerson)
            firstPerson.SetAngles(thirdPerson.Yaw, thirdPerson.Pitch);
        else
            thirdPerson.SetAngles(firstPerson.Yaw, firstPerson.Pitch);
    }

    private void ApplyRendererVisibility()
    {
        if (renderersHiddenInFirstPerson == null)
            return;

        bool visible = !IsFirstPerson;
        for (int i = 0; i < renderersHiddenInFirstPerson.Length; i++)
        {
            Renderer targetRenderer = renderersHiddenInFirstPerson[i];
            if (targetRenderer != null && targetRenderer.enabled != visible)
                targetRenderer.enabled = visible;
        }
    }

    private void ForceGameplayCursor()
    {
        if (!lockCursorDuringGameplay)
            return;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        cursorInitialized = true;
    }

    private void ApplyCursorState()
    {
        if (!lockCursorDuringGameplay)
            return;

        if (!cursorInitialized)
        {
            ForceGameplayCursor();
            return;
        }

        if (GameplayInputState.IsBlocked)
            return;

        if (Cursor.lockState != CursorLockMode.Locked)
            Cursor.lockState = CursorLockMode.Locked;

        if (Cursor.visible)
            Cursor.visible = false;
    }

    private void EnforceSingleCameraAndListener()
    {
        if (gameCamera == null)
            return;

        gameCamera.enabled = true;
        gameCamera.gameObject.tag = "MainCamera";

        AudioListener localListener = gameCamera.GetComponent<AudioListener>();
        if (localListener == null)
            localListener = gameCamera.gameObject.AddComponent<AudioListener>();
        localListener.enabled = true;

        if (disableOtherCameras)
        {
            Camera[] cameras = Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < cameras.Length; i++)
            {
                Camera otherCamera = cameras[i];
                if (otherCamera == null || otherCamera == gameCamera)
                    continue;

                otherCamera.enabled = false;
                if (otherCamera.CompareTag("MainCamera"))
                    otherCamera.gameObject.tag = "Untagged";
            }
        }

        if (disableOtherAudioListeners)
        {
            AudioListener[] listeners = Object.FindObjectsByType<AudioListener>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < listeners.Length; i++)
            {
                AudioListener listener = listeners[i];
                if (listener != null && listener != localListener)
                    listener.enabled = false;
            }
        }
    }

    private float SafeDeltaTime()
    {
        return Mathf.Clamp(Time.unscaledDeltaTime, 0.0001f, 0.05f);
    }
}
