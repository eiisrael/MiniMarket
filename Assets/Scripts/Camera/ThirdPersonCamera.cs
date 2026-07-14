using UnityEngine;

/// <summary>
/// Modo de câmera em terceira pessoa.
/// Este componente calcula a pose; somente PlayerCameraController escreve no Transform da câmera.
///
/// A colisão da câmera ignora o personagem e o objeto atualmente segurado pelo GetItemController.
/// Isso evita a oscilação de aproximação/afastamento quando uma caixa fica entre o jogador
/// e a câmera, sem desativar a proteção normal contra paredes e cenários.
/// </summary>
[DisallowMultipleComponent]
public sealed class ThirdPersonCamera : MonoBehaviour
{
    [Header("Referências")]
    public Transform target;
    public Transform lookTarget;
    public GetItemController getItemController;

    [Header("Órbita")]
    public Vector3 targetOffset = new Vector3(0f, 1.45f, 0f);
    [Min(0.5f)] public float distance = 4.25f;
    public float shoulderOffset = 0.45f;
    public float verticalOffset = 0.12f;
    [Range(-89f, 89f)] public float minPitch = -35f;
    [Range(-89f, 89f)] public float maxPitch = 65f;
    public float initialYaw;
    public float initialPitch = 18f;

    [Header("Mouse")]
    [Min(1f)] public float sensitivityX = 180f;
    [Min(1f)] public float sensitivityY = 130f;
    public bool invertY;
    [Min(0f)] public float mouseDeadZone = 0.0008f;

    [Header("Colisão")]
    public bool useCollision = true;
    public LayerMask collisionLayers = ~0;
    [Min(0.02f)] public float collisionRadius = 0.28f;
    [Min(0f)] public float wallPadding = 0.16f;
    [Min(0.3f)] public float minimumDistance = 1.1f;
    public bool ignoreTriggers = true;
    public bool ignoreHorizontalGround = true;
    [Range(0f, 1f)] public float groundNormalThreshold = 0.55f;
    public bool ignoreCurrentlyHeldItem = true;

    [Header("Lente")]
    [Range(1f, 179f)] public float fieldOfView = 60f;

    [Header("Debug")]
    public bool drawCollisionRay;

    private readonly RaycastHit[] collisionHits = new RaycastHit[32];
    private float yaw;
    private float pitch;
    private bool initialized;
    private Collider lastBlockingCollider;

    public float Yaw => yaw;
    public float Pitch => pitch;
    public Collider LastBlockingCollider => lastBlockingCollider;

    public void Initialize(Transform cameraTransform)
    {
        ResolveGetItemController();

        if (initialized)
            return;

        yaw = Mathf.Abs(initialYaw) > 0.001f
            ? initialYaw
            : (target != null ? target.eulerAngles.y : cameraTransform.eulerAngles.y);

        pitch = Mathf.Clamp(initialPitch, minPitch, maxPitch);
        initialized = true;
    }

    public void SetAngles(float newYaw, float newPitch)
    {
        yaw = newYaw;
        pitch = Mathf.Clamp(newPitch, minPitch, maxPitch);
        initialized = true;
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

    public bool TryGetPose(out Vector3 position, out Quaternion rotation, out float fov)
    {
        ResolveGetItemController();

        position = transform.position;
        rotation = transform.rotation;
        fov = fieldOfView;

        if (target == null)
            return false;

        Vector3 focus = lookTarget != null ? lookTarget.position : target.position + targetOffset;
        Quaternion orbit = Quaternion.Euler(pitch, yaw, 0f);
        Vector3 desiredPosition = focus +
                                  orbit * Vector3.back * distance +
                                  orbit * Vector3.right * shoulderOffset +
                                  Vector3.up * verticalOffset;

        Vector3 direction = desiredPosition - focus;
        float desiredDistance = direction.magnitude;
        if (desiredDistance <= 0.001f)
            return false;

        direction /= desiredDistance;
        float safeDistance = useCollision
            ? CalculateSafeDistance(focus, direction, desiredDistance)
            : desiredDistance;

        position = focus + direction * safeDistance;
        rotation = Quaternion.LookRotation(focus - position, Vector3.up);

        if (drawCollisionRay)
        {
            Debug.DrawLine(focus, desiredPosition, Color.red);
            Debug.DrawLine(focus, position, Color.green);
        }

        return true;
    }

    private float CalculateSafeDistance(Vector3 origin, Vector3 direction, float desiredDistance)
    {
        lastBlockingCollider = null;
        float safeDistance = desiredDistance;
        QueryTriggerInteraction triggerMode = ignoreTriggers
            ? QueryTriggerInteraction.Ignore
            : QueryTriggerInteraction.Collide;

        int count = Physics.SphereCastNonAlloc(
            origin,
            collisionRadius,
            direction,
            collisionHits,
            desiredDistance + wallPadding,
            collisionLayers,
            triggerMode
        );

        for (int i = 0; i < count; i++)
        {
            RaycastHit hit = collisionHits[i];
            Collider hitCollider = hit.collider;
            if (hitCollider == null || ShouldIgnore(hit))
                continue;

            float candidate = Mathf.Max(minimumDistance, hit.distance - wallPadding);
            if (candidate >= safeDistance)
                continue;

            safeDistance = candidate;
            lastBlockingCollider = hitCollider;
        }

        return Mathf.Clamp(safeDistance, minimumDistance, desiredDistance);
    }

    private bool ShouldIgnore(RaycastHit hit)
    {
        Collider hitCollider = hit.collider;
        if (hitCollider == null)
            return true;

        Transform hitTransform = hitCollider.transform;

        if (target != null &&
            (hitTransform == target || hitTransform.IsChildOf(target)))
        {
            return true;
        }

        if (ignoreCurrentlyHeldItem && getItemController != null)
        {
            GrabbableItem heldItem = getItemController.HeldItem;
            if (heldItem != null)
            {
                Transform heldTransform = heldItem.transform;
                if (hitTransform == heldTransform || hitTransform.IsChildOf(heldTransform))
                    return true;
            }
        }

        if (ignoreHorizontalGround && hit.normal.y >= groundNormalThreshold)
            return true;

        return false;
    }

    private void ResolveGetItemController()
    {
        if (getItemController != null)
            return;

        getItemController = GetComponent<GetItemController>();

        if (getItemController == null)
        {
            getItemController = Object.FindAnyObjectByType<GetItemController>(
                FindObjectsInactive.Include
            );
        }
    }

    private void OnValidate()
    {
        if (maxPitch < minPitch)
            maxPitch = minPitch;

        distance = Mathf.Max(0.5f, distance);
        minimumDistance = Mathf.Clamp(minimumDistance, 0.3f, distance);
        collisionRadius = Mathf.Max(0.02f, collisionRadius);
        wallPadding = Mathf.Max(0f, wallPadding);
    }
}
