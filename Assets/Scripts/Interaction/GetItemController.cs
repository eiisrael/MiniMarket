using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// GetItem realista baseado em física.
/// Seleciona pelo centro da câmera, segura com mola/amortecimento, respeita paredes,
/// preserva colisões, permite ajustar distância e arremessar o objeto.
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(1200)]
public sealed class GetItemController : MonoBehaviour
{
    [Header("Referências")]
    public Camera cameraSource;
    public Transform playerRoot;
    public PlayerCameraController cameraController;

    [Header("Uso")]
    public bool onlyInFirstPerson = true;
    [Range(0, 2)] public int grabMouseButton = 0;
    public KeyCode throwKey = KeyCode.F;

    [Header("Seleção")]
    [Min(0.1f)] public float selectionDistance = 6f;
    [Min(0.01f)] public float selectionRadius = 0.14f;
    public LayerMask selectableLayers = ~0;
    public bool ignoreTriggers = true;
    [Min(0f)] public float selectionMemory = 0.08f;

    [Header("Distância ao segurar")]
    [Min(0.3f)] public float holdDistance = 2.25f;
    [Min(0.3f)] public float minimumHoldDistance = 0.75f;
    [Min(0.3f)] public float maximumHoldDistance = 4.5f;
    public Vector2 screenOffset;
    public bool allowMouseWheelDistance = true;
    [Min(0.01f)] public float mouseWheelSpeed = 0.3f;

    [Header("Mola física")]
    [Min(0f)] public float positionSpring = 85f;
    [Min(0f)] public float positionDamping = 16f;
    [Min(0f)] public float maximumAcceleration = 70f;
    [Min(0f)] public float rotationSpring = 35f;
    [Min(0f)] public float rotationDamping = 8f;
    [Min(0f)] public float maximumAngularAcceleration = 40f;

    [Header("Colisão / Segurança")]
    public bool preventWallClipping = true;
    public LayerMask blockingLayers = ~0;
    [Range(0.05f, 1f)] public float collisionRadiusMultiplier = 0.45f;
    [Min(0f)] public float wallPadding = 0.12f;
    [Min(0.5f)] public float releaseErrorDistance = 3f;
    public bool ignorePlayerCollisionsWhileHeld = true;

    [Header("Soltar / Arremessar")]
    [Min(0f)] public float throwForce = 8f;
    [Range(0f, 1f)] public float inheritCameraVelocity = 0.25f;
    [Range(0f, 1f)] public float releaseVelocityMultiplier = 0.65f;

    [Header("Preparação automática")]
    public bool addRigidbodyWhenMissing = true;
    public bool addColliderWhenMissing = false;
    [Min(0.01f)] public float defaultMass = 1f;

    [Header("Debug")]
    public bool drawDebug;
    public bool logEvents;

    private readonly RaycastHit[] selectionHits = new RaycastHit[48];
    private readonly RaycastHit[] blockingHits = new RaycastHit[32];
    private readonly List<Collider> heldColliders = new List<Collider>(8);
    private readonly List<Collider> playerColliders = new List<Collider>(8);

    private GrabbableItem selectedItem;
    private GrabbableItem lastSelectedItem;
    private GrabbableItem heldItem;
    private Rigidbody heldBody;
    private float lastSelectionTime;
    private float currentHoldDistance;
    private float heldRadius = 0.25f;
    private Quaternion heldRotation;
    private Vector3 previousCameraPosition;
    private Vector3 cameraVelocity;

    private bool originalUseGravity;
    private bool originalIsKinematic;
    private float originalLinearDamping;
    private float originalAngularDamping;
    private RigidbodyConstraints originalConstraints;
    private RigidbodyInterpolation originalInterpolation;
    private CollisionDetectionMode originalCollisionMode;

    public GrabbableItem SelectedItem => selectedItem;
    public GrabbableItem HeldItem => heldItem;
    public bool IsHolding => heldItem != null;

    private void Awake()
    {
        ResolveReferences();
        currentHoldDistance = Mathf.Clamp(holdDistance, minimumHoldDistance, maximumHoldDistance);

        if (cameraSource != null)
            previousCameraPosition = cameraSource.transform.position;

        CachePlayerColliders();
    }

    private void OnDisable()
    {
        Release(false);
        ClearSelection();
    }

    private void Update()
    {
        ResolveReferences();
        UpdateCameraVelocity();

        if (GameplayInputState.IsBlocked || !CanUseInCurrentView())
        {
            if (IsHolding)
                Release(false);
            ClearSelection();
            return;
        }

        if (!IsHolding)
        {
            UpdateSelection();

            if (Input.GetMouseButtonDown(grabMouseButton) && selectedItem != null)
                Grab(selectedItem);
        }
        else
        {
            UpdateHoldDistance();

            if (Input.GetKeyDown(throwKey))
            {
                Release(true);
                return;
            }

            if (Input.GetMouseButtonUp(grabMouseButton))
                Release(false);
        }
    }

    private void FixedUpdate()
    {
        if (IsHolding && !GameplayInputState.IsBlocked)
            MoveHeldObject();
    }

    public void ReleaseHeldItem()
    {
        Release(false);
    }

    private void ResolveReferences()
    {
        if (cameraSource == null)
            cameraSource = GetComponent<Camera>();

        if (cameraSource == null && Camera.main != null)
            cameraSource = Camera.main;

        if (cameraController == null)
            cameraController = GetComponent<PlayerCameraController>();
    }

    private bool CanUseInCurrentView()
    {
        return !onlyInFirstPerson || cameraController == null || cameraController.IsFirstPerson;
    }

    private void UpdateCameraVelocity()
    {
        if (cameraSource == null)
            return;

        float deltaTime = Mathf.Max(Time.unscaledDeltaTime, 0.0001f);
        Vector3 position = cameraSource.transform.position;
        cameraVelocity = (position - previousCameraPosition) / deltaTime;
        previousCameraPosition = position;
    }

    private void UpdateSelection()
    {
        GrabbableItem candidate = FindCandidate();

        if (candidate != null)
        {
            lastSelectedItem = candidate;
            lastSelectionTime = Time.unscaledTime;
        }
        else if (lastSelectedItem != null && Time.unscaledTime - lastSelectionTime <= selectionMemory)
        {
            candidate = lastSelectedItem;
        }

        if (candidate == selectedItem)
            return;

        ClearSelection();
        selectedItem = candidate;

        if (selectedItem != null)
            selectedItem.SetSelected(true);
    }

    private GrabbableItem FindCandidate()
    {
        if (cameraSource == null)
            return null;

        Ray ray = cameraSource.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        QueryTriggerInteraction triggerMode = ignoreTriggers
            ? QueryTriggerInteraction.Ignore
            : QueryTriggerInteraction.Collide;

        int count = Physics.SphereCastNonAlloc(
            ray,
            selectionRadius,
            selectionHits,
            selectionDistance,
            selectableLayers,
            triggerMode
        );

        SortHits(selectionHits, count);

        for (int i = 0; i < count; i++)
        {
            Collider hitCollider = selectionHits[i].collider;
            if (hitCollider == null || IsPlayerCollider(hitCollider))
                continue;

            GrabbableItem item = hitCollider.GetComponentInParent<GrabbableItem>();
            if (item == null)
                continue;

            Rigidbody body = item.GetComponent<Rigidbody>();
            Bounds bounds = CalculateBounds(item.transform, out bool hasBounds);
            if (!hasBounds)
                bounds = new Bounds(item.transform.position, Vector3.one * 0.25f);

            if (item.Validate(body, bounds))
                return item;
        }

        return null;
    }

    private void Grab(GrabbableItem item)
    {
        if (item == null)
            return;

        heldBody = item.GetComponent<Rigidbody>();
        if (heldBody == null && addRigidbodyWhenMissing)
        {
            heldBody = item.gameObject.AddComponent<Rigidbody>();
            heldBody.mass = Mathf.Max(0.01f, defaultMass);
        }

        if (heldBody == null)
            return;

        if (item.GetComponentInChildren<Collider>() == null && addColliderWhenMissing)
            item.gameObject.AddComponent<BoxCollider>();

        CacheOriginalBodyState();

        heldItem = item;
        selectedItem = null;
        lastSelectedItem = null;
        item.SetSelected(false);
        item.SetHeld(true);

        heldRotation = heldBody.rotation;
        currentHoldDistance = Mathf.Clamp(
            holdDistance * item.holdDistanceMultiplier,
            minimumHoldDistance,
            maximumHoldDistance
        );
        heldRadius = CalculateObjectRadius(item.transform);

        heldBody.isKinematic = false;
        heldBody.useGravity = false;
        heldBody.constraints = RigidbodyConstraints.None;
        heldBody.interpolation = RigidbodyInterpolation.Interpolate;
        heldBody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        heldBody.linearDamping = Mathf.Max(0.1f, originalLinearDamping);
        heldBody.angularDamping = Mathf.Max(0.1f, originalAngularDamping);
        heldBody.WakeUp();

        CacheHeldColliders();
        SetPlayerCollisionIgnored(true);

        if (logEvents)
            Debug.Log("[GetItem] Pegou: " + item.name);
    }

    private void MoveHeldObject()
    {
        if (heldItem == null || heldBody == null || cameraSource == null)
        {
            Release(false);
            return;
        }

        Vector3 targetPosition = CalculateSafeHoldPoint() +
                                 cameraSource.transform.TransformVector(heldItem.localHoldOffset);
        Vector3 error = targetPosition - heldBody.worldCenterOfMass;

        if (error.magnitude > releaseErrorDistance)
        {
            Release(false);
            return;
        }

        float spring = positionSpring * heldItem.springMultiplier;
        float damping = positionDamping * heldItem.dampingMultiplier;
        Vector3 acceleration = error * spring - heldBody.linearVelocity * damping;
        acceleration = Vector3.ClampMagnitude(acceleration, maximumAcceleration);
        heldBody.AddForce(acceleration, ForceMode.Acceleration);

        Quaternion targetRotation = CalculateTargetRotation();
        Quaternion rotationError = targetRotation * Quaternion.Inverse(heldBody.rotation);
        rotationError.ToAngleAxis(out float angle, out Vector3 axis);

        if (angle > 180f)
            angle -= 360f;

        if (!float.IsNaN(axis.x) && axis.sqrMagnitude > 0.0001f)
        {
            axis.Normalize();
            float rotationSpringValue = rotationSpring * heldItem.rotationMultiplier;
            float rotationDampingValue = rotationDamping * heldItem.dampingMultiplier;
            Vector3 angularAcceleration = axis * (angle * Mathf.Deg2Rad * rotationSpringValue) -
                                          heldBody.angularVelocity * rotationDampingValue;
            angularAcceleration = Vector3.ClampMagnitude(
                angularAcceleration,
                maximumAngularAcceleration
            );
            heldBody.AddTorque(angularAcceleration, ForceMode.Acceleration);
        }

        if (drawDebug)
        {
            Debug.DrawLine(cameraSource.transform.position, targetPosition, Color.cyan);
            Debug.DrawLine(heldBody.worldCenterOfMass, targetPosition, Color.yellow);
        }
    }

    private Vector3 CalculateSafeHoldPoint()
    {
        Transform cameraTransform = cameraSource.transform;
        Vector3 viewportPoint = new Vector3(
            0.5f + screenOffset.x,
            0.5f + screenOffset.y,
            0f
        );
        Ray ray = cameraSource.ViewportPointToRay(viewportPoint);
        Vector3 desiredPoint = ray.origin + ray.direction * currentHoldDistance;

        if (!preventWallClipping)
            return desiredPoint;

        float castRadius = Mathf.Max(0.03f, heldRadius * collisionRadiusMultiplier);
        QueryTriggerInteraction triggerMode = ignoreTriggers
            ? QueryTriggerInteraction.Ignore
            : QueryTriggerInteraction.Collide;

        int count = Physics.SphereCastNonAlloc(
            ray.origin,
            castRadius,
            ray.direction,
            blockingHits,
            currentHoldDistance,
            blockingLayers,
            triggerMode
        );

        float safeDistance = currentHoldDistance;
        for (int i = 0; i < count; i++)
        {
            Collider hitCollider = blockingHits[i].collider;
            if (hitCollider == null || IsHeldCollider(hitCollider) || IsPlayerCollider(hitCollider))
                continue;

            safeDistance = Mathf.Min(
                safeDistance,
                Mathf.Max(minimumHoldDistance, blockingHits[i].distance - wallPadding)
            );
        }

        return ray.origin + ray.direction * safeDistance;
    }

    private Quaternion CalculateTargetRotation()
    {
        if (heldItem == null || cameraSource == null)
            return heldRotation;

        if (heldItem.alignToCamera)
        {
            return cameraSource.transform.rotation *
                   Quaternion.Euler(heldItem.cameraAlignmentEuler);
        }

        return heldItem.preserveRotation ? heldRotation : heldBody.rotation;
    }

    private void UpdateHoldDistance()
    {
        if (!allowMouseWheelDistance)
            return;

        float scroll = Input.mouseScrollDelta.y;
        if (Mathf.Abs(scroll) <= 0.001f)
            return;

        currentHoldDistance = Mathf.Clamp(
            currentHoldDistance + scroll * mouseWheelSpeed,
            minimumHoldDistance,
            maximumHoldDistance
        );
    }

    private void Release(bool throwItem)
    {
        if (heldItem == null)
            return;

        SetPlayerCollisionIgnored(false);

        if (heldBody != null)
        {
            Vector3 releaseVelocity = heldBody.linearVelocity * releaseVelocityMultiplier +
                                      cameraVelocity * inheritCameraVelocity;

            RestoreOriginalBodyState();
            heldBody.linearVelocity = releaseVelocity;

            if (throwItem && cameraSource != null)
                heldBody.AddForce(cameraSource.transform.forward * throwForce, ForceMode.VelocityChange);

            heldBody.WakeUp();
        }

        GrabbableItem releasedItem = heldItem;
        heldItem.SetHeld(false);
        heldItem = null;
        heldBody = null;
        heldColliders.Clear();

        if (logEvents)
            Debug.Log("[GetItem] Soltou: " + releasedItem.name + (throwItem ? " (arremesso)" : string.Empty));
    }

    private void CacheOriginalBodyState()
    {
        originalUseGravity = heldBody.useGravity;
        originalIsKinematic = heldBody.isKinematic;
        originalLinearDamping = heldBody.linearDamping;
        originalAngularDamping = heldBody.angularDamping;
        originalConstraints = heldBody.constraints;
        originalInterpolation = heldBody.interpolation;
        originalCollisionMode = heldBody.collisionDetectionMode;
    }

    private void RestoreOriginalBodyState()
    {
        heldBody.useGravity = originalUseGravity;
        heldBody.isKinematic = originalIsKinematic;
        heldBody.linearDamping = originalLinearDamping;
        heldBody.angularDamping = originalAngularDamping;
        heldBody.constraints = originalConstraints;
        heldBody.interpolation = originalInterpolation;
        heldBody.collisionDetectionMode = originalCollisionMode;
    }

    private void CachePlayerColliders()
    {
        playerColliders.Clear();
        if (playerRoot == null)
            return;

        Collider[] colliders = playerRoot.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] != null)
                playerColliders.Add(colliders[i]);
        }
    }

    private void CacheHeldColliders()
    {
        heldColliders.Clear();
        if (heldItem == null)
            return;

        Collider[] colliders = heldItem.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] != null)
                heldColliders.Add(colliders[i]);
        }
    }

    private void SetPlayerCollisionIgnored(bool ignored)
    {
        if (!ignorePlayerCollisionsWhileHeld)
            return;

        for (int i = 0; i < heldColliders.Count; i++)
        {
            Collider heldCollider = heldColliders[i];
            if (heldCollider == null)
                continue;

            for (int j = 0; j < playerColliders.Count; j++)
            {
                Collider playerCollider = playerColliders[j];
                if (playerCollider != null)
                    Physics.IgnoreCollision(heldCollider, playerCollider, ignored);
            }
        }
    }

    private bool IsHeldCollider(Collider target)
    {
        for (int i = 0; i < heldColliders.Count; i++)
        {
            if (heldColliders[i] == target)
                return true;
        }
        return false;
    }

    private bool IsPlayerCollider(Collider target)
    {
        if (target == null || playerRoot == null)
            return false;

        Transform targetTransform = target.transform;
        return targetTransform == playerRoot || targetTransform.IsChildOf(playerRoot);
    }

    private void ClearSelection()
    {
        if (selectedItem != null)
            selectedItem.SetSelected(false);

        selectedItem = null;
        lastSelectedItem = null;
    }

    private Bounds CalculateBounds(Transform root, out bool hasBounds)
    {
        hasBounds = false;
        Bounds result = new Bounds(root.position, Vector3.zero);
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer targetRenderer = renderers[i];
            if (targetRenderer == null)
                continue;

            if (!hasBounds)
            {
                result = targetRenderer.bounds;
                hasBounds = true;
            }
            else
            {
                result.Encapsulate(targetRenderer.bounds);
            }
        }

        if (hasBounds)
            return result;

        Collider[] colliders = root.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider targetCollider = colliders[i];
            if (targetCollider == null)
                continue;

            if (!hasBounds)
            {
                result = targetCollider.bounds;
                hasBounds = true;
            }
            else
            {
                result.Encapsulate(targetCollider.bounds);
            }
        }

        return result;
    }

    private float CalculateObjectRadius(Transform root)
    {
        Bounds bounds = CalculateBounds(root, out bool hasBounds);
        if (!hasBounds)
            return 0.25f;

        return Mathf.Max(0.05f, Mathf.Min(bounds.extents.x, bounds.extents.y, bounds.extents.z));
    }

    private void SortHits(RaycastHit[] hits, int count)
    {
        for (int i = 1; i < count; i++)
        {
            RaycastHit value = hits[i];
            int index = i - 1;

            while (index >= 0 && hits[index].distance > value.distance)
            {
                hits[index + 1] = hits[index];
                index--;
            }

            hits[index + 1] = value;
        }
    }

    private void OnValidate()
    {
        maximumHoldDistance = Mathf.Max(minimumHoldDistance, maximumHoldDistance);
        holdDistance = Mathf.Clamp(holdDistance, minimumHoldDistance, maximumHoldDistance);
        selectionRadius = Mathf.Max(0.01f, selectionRadius);
        selectionDistance = Mathf.Max(0.1f, selectionDistance);
    }
}
