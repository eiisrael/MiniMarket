using UnityEngine;

/// <summary>
/// Autoridade única da câmera do jogador.
/// Usa uma única Camera/AudioListener e alterna entre os modos calculados por
/// ThirdPersonCamera e FirstPersonCamera, eliminando conflitos entre câmeras.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Camera))]
[RequireComponent(typeof(ThirdPersonCamera))]
[RequireComponent(typeof(FirstPersonCamera))]
[DefaultExecutionOrder(1000)]
public sealed class CameraModeController : MonoBehaviour
{
    public enum CameraMode
    {
        ThirdPerson,
        FirstPerson
    }

    [Header("Referências")]
    public Camera gameCamera;
    public ThirdPersonCamera thirdPerson;
    public FirstPersonCamera firstPerson;
    public Transform player;
    public PlayerMove playerMovement;

    [Header("Modo")]
    public CameraMode initialMode = CameraMode.ThirdPerson;
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
    public bool disableOtherMainCameras = true;
    public bool disableOtherAudioListeners = true;

    private CameraMode currentMode;
    private Vector3 positionVelocity;
    private bool previousMouseState;
    private bool initialized;

    public CameraMode CurrentMode => currentMode;
    public bool IsFirstPerson => currentMode == CameraMode.FirstPerson;
    public Transform ActiveCameraTransform => gameCamera != null ? gameCamera.transform : transform;

    private void Reset()
    {
        ResolveReferences();
    }

    private void Awake()
    {
        ResolveReferences();
        currentMode = initialMode;
        InitializeModes();
        EnforceSingleCameraAndListener();
        ApplyRendererVisibility();
        ApplyCursorState();

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
        HandleModeInput();
        ApplyCursorState();

        if (GameplayInputState.IsBlocked)
            return;

        float dt = SafeDeltaTime();
        if (currentMode == CameraMode.FirstPerson)
            firstPerson?.ReadInput(dt);
        else
            thirdPerson?.ReadInput(dt);
    }

    private void LateUpdate()
    {
        ResolveReferences();
        UpdatePose(false);

        if (playerMovement != null && playerMovement.cameraTransform != ActiveCameraTransform)
            playerMovement.cameraTransform = ActiveCameraTransform;
    }

    public void SetMode(CameraMode mode)
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
        SetMode(IsFirstPerson ? CameraMode.ThirdPerson : CameraMode.FirstPerson);
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
                SetMode(pressed ? CameraMode.FirstPerson : CameraMode.ThirdPerson);
            }
        }

        if (toggleKey != KeyCode.None && Input.GetKeyDown(toggleKey))
            ToggleMode();
    }

    private void UpdatePose(bool immediate)
    {
        if (gameCamera == null)
            return;

        float dt = SafeDeltaTime();
        bool valid;
        Vector3 targetPosition;
        Quaternion targetRotation;
        float targetFov;

        if (currentMode == CameraMode.FirstPerson)
        {
            valid = firstPerson != null && firstPerson.TryGetPose(dt, out targetPosition, out targetRotation, out targetFov);
        }
        else
        {
            valid = thirdPerson != null && thirdPerson.TryGetPose(out targetPosition, out targetRotation, out targetFov);
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
                dt
            );

            cameraTransform.rotation = rotationSpeed <= 0.0001f
                ? targetRotation
                : Quaternion.Slerp(
                    cameraTransform.rotation,
                    targetRotation,
                    1f - Mathf.Exp(-rotationSpeed * dt)
                );
        }

        if (immediate || fieldOfViewSpeed <= 0.0001f)
        {
            gameCamera.fieldOfView = targetFov;
        }
        else
        {
            gameCamera.fieldOfView = Mathf.Lerp(
                gameCamera.fieldOfView,
                targetFov,
                1f - Mathf.Exp(-fieldOfViewSpeed * dt)
            );
        }
    }

    private void ResolveReferences()
    {
        if (gameCamera == null)
            gameCamera = GetComponent<Camera>();

        if (thirdPerson == null)
            thirdPerson = GetComponent<ThirdPersonCamera>();

        if (firstPerson == null)
            firstPerson = GetComponent<FirstPersonCamera>();

        if (playerMovement == null && player != null)
            playerMovement = player.GetComponent<PlayerMove>();

        if (player == null && playerMovement != null)
            player = playerMovement.transform;

        if (thirdPerson != null && thirdPerson.target == null)
            thirdPerson.target = player;

        if (firstPerson != null && firstPerson.playerBody == null)
            firstPerson.playerBody = player;
    }

    private void InitializeModes()
    {
        thirdPerson?.Initialize(transform);
        firstPerson?.Initialize(transform);
    }

    private void SynchronizeAngles(CameraMode destination)
    {
        if (thirdPerson == null || firstPerson == null)
            return;

        if (destination == CameraMode.FirstPerson)
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
            Renderer renderer = renderersHiddenInFirstPerson[i];
            if (renderer != null && renderer.enabled != visible)
                renderer.enabled = visible;
        }
    }

    private void ApplyCursorState()
    {
        if (!lockCursorDuringGameplay || GameplayInputState.IsBlocked)
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

        if (disableOtherMainCameras)
        {
            Camera[] cameras = Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < cameras.Length; i++)
            {
                Camera camera = cameras[i];
                if (camera == null || camera == gameCamera)
                    continue;

                if (camera.CompareTag("MainCamera") || camera.GetComponent<AudioListener>() != null)
                {
                    camera.enabled = false;
                    if (camera.CompareTag("MainCamera"))
                        camera.gameObject.tag = "Untagged";
                }
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
