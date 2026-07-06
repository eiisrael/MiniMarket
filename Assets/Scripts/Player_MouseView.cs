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

    [Header("Limites Verticais")]
    public float minPitch = -25f;
    public float maxPitch = 55f;

    [Header("Suavização")]
    public float positionSmoothTime = 0.06f;
    public float rotationSmoothTime = 0.04f;

    [Header("Auto Alinhar Atrás do Personagem")]
    public bool autoAlignBehindPlayer = true;
    public float autoAlignDelay = 1.2f;
    public float autoAlignSpeed = 70f;

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

    private void Start()
    {
        if (target == null && transform.parent != null)
            target = transform.parent;

        if (target != null)
        {
            yaw = target.eulerAngles.y;
            lastTargetPosition = target.position;
        }

        if (lockCursorOnStart)
            LockCursor();
    }

    private void LateUpdate()
    {
        HandleCursor();

        if (target == null)
            return;

        if (Cursor.lockState == CursorLockMode.Locked)
            HandleMouse();

        HandleAutoAlign();

        UpdateCamera();
    }

    private void HandleMouse()
    {
        float mouseX = Input.GetAxisRaw("Mouse X");
        float mouseY = Input.GetAxisRaw("Mouse Y");

        if (Mathf.Abs(mouseX) > 0.01f || Mathf.Abs(mouseY) > 0.01f)
            timeWithoutMouse = 0f;
        else
            timeWithoutMouse += Time.deltaTime;

        yaw += mouseX * mouseSensitivityX * Time.deltaTime;

        if (invertY)
            pitch += mouseY * mouseSensitivityY * Time.deltaTime;
        else
            pitch -= mouseY * mouseSensitivityY * Time.deltaTime;

        pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
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

    private void UpdateCamera()
    {
        Vector3 focusPoint = target.position + targetOffset;

        Quaternion orbitRotation = Quaternion.Euler(pitch, yaw, 0f);

        Vector3 desiredPosition =
            focusPoint
            - orbitRotation * Vector3.forward * distance
            + Vector3.up * height;

        transform.position = Vector3.SmoothDamp(
            transform.position,
            desiredPosition,
            ref positionVelocity,
            positionSmoothTime
        );

        Quaternion desiredRotation = Quaternion.LookRotation(
            focusPoint - transform.position,
            Vector3.up
        );

        transform.rotation = SmoothDampQuaternion(
            transform.rotation,
            desiredRotation,
            ref rotationVelocity,
            rotationSmoothTime
        );
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
}