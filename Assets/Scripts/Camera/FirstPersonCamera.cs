using UnityEngine;

/// <summary>
/// Modo de câmera em primeira pessoa sem head bob ou sway.
/// Calcula posição/rotação; PlayerCameraController é a única autoridade do Transform.
/// </summary>
[DisallowMultipleComponent]
public sealed class FirstPersonCamera : MonoBehaviour
{
    [Header("Referências")]
    public Transform playerBody;
    public Transform eyePoint;

    [Header("Posição")]
    public Vector3 localEyeOffset = new Vector3(0f, 1.68f, 0.08f);

    [Header("Mouse")]
    [Min(1f)] public float sensitivityX = 175f;
    [Min(1f)] public float sensitivityY = 125f;
    public bool invertY;
    [Min(0f)] public float mouseDeadZone = 0.0008f;
    [Range(-89f, 89f)] public float minPitch = -78f;
    [Range(-89f, 89f)] public float maxPitch = 82f;

    [Header("Corpo")]
    public bool rotateBodyWithCamera = true;
    [Min(0f)] public float bodyRotationSpeed = 22f;

    [Header("Lente")]
    [Range(1f, 179f)] public float fieldOfView = 60f;
    [Range(1f, 179f)] public float aimFieldOfView = 42f;
    [Range(0, 2)] public int aimMouseButton = 1;
    public bool useAimZoom = true;

    private float yaw;
    private float pitch;
    private bool initialized;
    private bool externalAimHeld;

    public float Yaw => yaw;
    public float Pitch => pitch;
    public bool IsAiming => useAimZoom && (Input.GetMouseButton(aimMouseButton) || externalAimHeld);

    public void Initialize(Transform cameraTransform)
    {
        if (initialized)
            return;

        yaw = playerBody != null ? playerBody.eulerAngles.y : cameraTransform.eulerAngles.y;
        pitch = Mathf.Clamp(cameraTransform.eulerAngles.x > 180f
            ? cameraTransform.eulerAngles.x - 360f
            : cameraTransform.eulerAngles.x, minPitch, maxPitch);
        initialized = true;
    }

    public void SetAngles(float newYaw, float newPitch)
    {
        yaw = newYaw;
        pitch = Mathf.Clamp(newPitch, minPitch, maxPitch);
        initialized = true;
    }

    public void SetExternalAimHeld(bool held)
    {
        externalAimHeld = held;
    }

    public void AddLookDelta(Vector2 degreesDelta)
    {
        if (degreesDelta.sqrMagnitude <= 0.000001f)
            return;

        yaw += degreesDelta.x;
        pitch += invertY ? degreesDelta.y : -degreesDelta.y;
        pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
        initialized = true;
    }

    public void ReadInput(float deltaTime)
    {
        float mouseX = Input.GetAxisRaw("Mouse X");
        float mouseY = Input.GetAxisRaw("Mouse Y");

        if (mouseX * mouseX + mouseY * mouseY <= mouseDeadZone * mouseDeadZone)
            return;

        yaw += mouseX * sensitivityX * deltaTime;
        pitch += (invertY ? mouseY : -mouseY) * sensitivityY * deltaTime;
        pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
    }

    public bool TryGetPose(float deltaTime, out Vector3 position, out Quaternion rotation, out float fov)
    {
        position = transform.position;
        rotation = Quaternion.Euler(pitch, yaw, 0f);
        fov = IsAiming ? aimFieldOfView : fieldOfView;

        if (playerBody == null && eyePoint == null)
            return false;

        position = eyePoint != null
            ? eyePoint.position
            : playerBody.TransformPoint(localEyeOffset);

        if (rotateBodyWithCamera && playerBody != null)
        {
            Quaternion targetRotation = Quaternion.Euler(0f, yaw, 0f);
            playerBody.rotation = bodyRotationSpeed <= 0.001f
                ? targetRotation
                : Quaternion.Slerp(
                    playerBody.rotation,
                    targetRotation,
                    1f - Mathf.Exp(-bodyRotationSpeed * deltaTime)
                );
        }

        return true;
    }

    private void OnDisable()
    {
        externalAimHeld = false;
    }

    private void OnValidate()
    {
        if (maxPitch < minPitch)
            maxPitch = minPitch;

        fieldOfView = Mathf.Clamp(fieldOfView, 1f, 179f);
        aimFieldOfView = Mathf.Clamp(aimFieldOfView, 1f, 179f);
    }
}
