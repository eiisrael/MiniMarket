using UnityEngine;

/// <summary>
/// Autoridade de foco e interação para primeira e terceira pessoa.
///
/// O raycast central continua sendo a primeira opção. Em terceira pessoa, portas e
/// mecanismos próximos também podem ser selecionados pelo espaço ao redor do jogador,
/// sem exigir mira. A busca usa buffers reutilizados, ignora itens pegáveis e mantém
/// proteção contra paredes.
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(1100)]
public sealed class InteractionFocusController : MonoBehaviour
{
    [Header("Referências")]
    public Camera cameraSource;
    public PlayerCameraController cameraController;
    public Transform playerRoot;
    public GetItemController getItemController;

    [Header("Detecção")]
    [Min(0.1f)] public float interactionDistance = 6f;
    [Min(0f)] public float interactionRadius = 0.12f;
    public LayerMask interactionLayers = ~0;
    public bool ignoreTriggers = true;
    public bool useScreenCenter = true;

    [Header("Terceira pessoa - raio alternativo")]
    public bool usePlayerOriginFallbackInThirdPerson = true;
    [Min(0f)] public float thirdPersonOriginHeight = 1.25f;
    [Min(0.1f)] public float thirdPersonFallbackDistance = 7f;

    [Header("Terceira pessoa - proximidade")]
    [Tooltip("Mantém o foco em portas e mecanismos próximos mesmo sem a mira central.")]
    public bool useProximityFallbackInThirdPerson = true;

    [Min(0.5f)] public float thirdPersonProximityRadius = 3.25f;
    [Range(10f, 180f)] public float thirdPersonProximityAngle = 135f;

    [Tooltip("Ao pressionar Interagir, permite uma busca um pouco mais ampla ao redor do personagem.")]
    public bool useExpandedProximityOnInteraction = true;

    [Min(0.5f)] public float interactionRequestProximityRadius = 3.8f;
    [Range(10f, 180f)] public float interactionRequestProximityAngle = 180f;

    [Tooltip("Impede que a proximidade concorra com o sistema de pegar caixas e produtos.")]
    public bool ignoreGrabbablesInProximityFallback = true;

    [Tooltip("Impede abrir objetos através de paredes.")]
    public bool requireProximityLineOfSight = true;

    [Tooltip("Permite ignorar uma moldura/obstáculo muito fino quando a porta está imediatamente à frente do jogador.")]
    [Min(0f)] public float closeRangeOccluderTolerance = 0.45f;

    [Min(0.25f)] public float closeRangeToleranceDistance = 1.75f;

    [Header("Input Desktop")]
    public bool interactWithMouse = true;
    [Range(0, 2)] public int interactMouseButton = 0;
    public bool interactWithKey = true;
    public KeyCode interactKey = KeyCode.E;
    public bool skipMouseInteractionForGrabbables = true;

    [Header("Debug")]
    public bool drawDebug;
    public bool logInteractions;

    private readonly RaycastHit[] rayHits = new RaycastHit[48];
    private readonly Collider[] proximityHits = new Collider[64];
    private readonly RaycastHit[] visibilityHits = new RaycastHit[32];

    private InteractiveObject focusedObject;
    private Vector2 externalScreenPosition;
    private bool hasExternalScreenPosition;
    private bool externalInteractRequested;

    public InteractiveObject FocusedObject => focusedObject;

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnEnable()
    {
        ResolveReferences();
    }

    private void OnDisable()
    {
        SetFocusedObject(null);
        externalInteractRequested = false;
    }

    private void Update()
    {
        ResolveReferences();

        if (GameplayInputState.IsBlocked || cameraSource == null)
        {
            SetFocusedObject(null);
            externalInteractRequested = false;
            return;
        }

        SetFocusedObject(FindCandidate(false));

        bool requested = externalInteractRequested;
        externalInteractRequested = false;

        if (interactWithKey && interactKey != KeyCode.None && Input.GetKeyDown(interactKey))
            requested = true;

        if (interactWithMouse && Input.GetMouseButtonDown(interactMouseButton))
        {
            bool focusedIsGrabbable = focusedObject != null &&
                                       focusedObject.GetComponentInParent<GrabbableItem>() != null;

            if (!skipMouseInteractionForGrabbables || !focusedIsGrabbable)
                requested = true;
        }

        if (!requested)
            return;

        if (focusedObject == null && IsThirdPerson() && useExpandedProximityOnInteraction)
        {
            Vector3 origin = GetPlayerInteractionOrigin();
            SetFocusedObject(FindThirdPersonProximityCandidate(origin, true));
        }

        InteractWithFocusedObject();
    }

    public void RequestInteract()
    {
        externalInteractRequested = true;
    }

    public void SetPointerScreenPosition(Vector2 screenPosition)
    {
        externalScreenPosition = screenPosition;
        hasExternalScreenPosition = true;
    }

    public void ClearPointerScreenPosition()
    {
        hasExternalScreenPosition = false;
    }

    public bool InteractWithFocusedObject()
    {
        if (focusedObject == null)
            return false;

        bool success = focusedObject.Interact();
        if (success && logInteractions)
            Debug.Log("[Interaction] Interagiu com: " + focusedObject.displayName, focusedObject);

        return success;
    }

    private InteractiveObject FindCandidate(bool explicitInteractionRequest)
    {
        Ray cameraRay = BuildRay();
        InteractiveObject candidate = FindCandidateAlongRay(
            cameraRay,
            interactionDistance,
            out _
        );

        if (candidate != null)
        {
            DrawDebugRay(cameraRay, interactionDistance, true);
            return candidate;
        }

        if (!ShouldUseThirdPersonFallback())
        {
            DrawDebugRay(cameraRay, interactionDistance, false);
            return null;
        }

        Vector3 playerOrigin = GetPlayerInteractionOrigin();
        Vector3 screenTarget = cameraRay.origin + cameraRay.direction * interactionDistance;
        Vector3 direction = screenTarget - playerOrigin;

        Ray playerRay = new Ray(playerOrigin, cameraRay.direction);
        float fallbackDistance = Mathf.Max(0.1f, thirdPersonFallbackDistance);

        if (usePlayerOriginFallbackInThirdPerson && direction.sqrMagnitude > 0.0001f)
        {
            fallbackDistance = Mathf.Min(fallbackDistance, direction.magnitude);
            playerRay = new Ray(playerOrigin, direction.normalized);

            candidate = FindCandidateAlongRay(playerRay, fallbackDistance, out _);
            if (candidate != null)
            {
                DrawDebugRay(cameraRay, interactionDistance, false);
                DrawDebugRay(playerRay, fallbackDistance, true);
                return candidate;
            }
        }

        candidate = FindThirdPersonProximityCandidate(playerOrigin, explicitInteractionRequest);

        DrawDebugRay(cameraRay, interactionDistance, false);
        DrawDebugRay(playerRay, fallbackDistance, false);
        return candidate;
    }

    private InteractiveObject FindCandidateAlongRay(
        Ray ray,
        float maxDistance,
        out float bestDistance)
    {
        QueryTriggerInteraction triggerMode = ignoreTriggers
            ? QueryTriggerInteraction.Ignore
            : QueryTriggerInteraction.Collide;

        int count = interactionRadius > 0.001f
            ? Physics.SphereCastNonAlloc(
                ray,
                interactionRadius,
                rayHits,
                maxDistance,
                interactionLayers,
                triggerMode
            )
            : Physics.RaycastNonAlloc(
                ray,
                rayHits,
                maxDistance,
                interactionLayers,
                triggerMode
            );

        InteractiveObject best = null;
        bestDistance = float.PositiveInfinity;

        for (int i = 0; i < count; i++)
        {
            Collider hitCollider = rayHits[i].collider;
            if (hitCollider == null || IsPlayerCollider(hitCollider))
                continue;

            InteractiveObject current = hitCollider.GetComponentInParent<InteractiveObject>();
            if (!IsValidCandidate(current))
                continue;

            if (rayHits[i].distance >= bestDistance)
                continue;

            bestDistance = rayHits[i].distance;
            best = current;
        }

        return best;
    }

    private InteractiveObject FindThirdPersonProximityCandidate(
        Vector3 origin,
        bool explicitInteractionRequest)
    {
        if (!useProximityFallbackInThirdPerson || playerRoot == null)
            return null;

        float radius = explicitInteractionRequest
            ? Mathf.Max(thirdPersonProximityRadius, interactionRequestProximityRadius)
            : thirdPersonProximityRadius;

        float angleLimit = explicitInteractionRequest
            ? Mathf.Max(thirdPersonProximityAngle, interactionRequestProximityAngle)
            : thirdPersonProximityAngle;

        QueryTriggerInteraction triggerMode = ignoreTriggers
            ? QueryTriggerInteraction.Ignore
            : QueryTriggerInteraction.Collide;

        int count = Physics.OverlapSphereNonAlloc(
            origin,
            Mathf.Max(0.5f, radius),
            proximityHits,
            interactionLayers,
            triggerMode
        );

        Vector3 cameraForward = cameraSource != null
            ? Vector3.ProjectOnPlane(cameraSource.transform.forward, Vector3.up)
            : Vector3.zero;

        Vector3 playerForward = playerRoot != null
            ? Vector3.ProjectOnPlane(playerRoot.forward, Vector3.up)
            : Vector3.zero;

        if (cameraForward.sqrMagnitude > 0.0001f)
            cameraForward.Normalize();
        if (playerForward.sqrMagnitude > 0.0001f)
            playerForward.Normalize();

        InteractiveObject best = null;
        float bestScore = float.PositiveInfinity;

        for (int i = 0; i < count; i++)
        {
            Collider candidateCollider = proximityHits[i];
            if (candidateCollider == null || IsPlayerCollider(candidateCollider))
                continue;

            InteractiveObject current = candidateCollider.GetComponentInParent<InteractiveObject>();
            if (!IsValidCandidate(current))
                continue;

            if (ignoreGrabbablesInProximityFallback &&
                current.GetComponentInParent<GrabbableItem>() != null)
            {
                continue;
            }

            Vector3 targetPoint = candidateCollider.ClosestPoint(origin);
            if ((targetPoint - origin).sqrMagnitude <= 0.0001f)
                targetPoint = candidateCollider.bounds.center;

            Vector3 toTarget = targetPoint - origin;
            float distance = toTarget.magnitude;
            if (distance <= 0.001f || distance > radius)
                continue;

            Vector3 horizontalDirection = Vector3.ProjectOnPlane(toTarget, Vector3.up);
            if (horizontalDirection.sqrMagnitude <= 0.0001f)
                horizontalDirection = toTarget;
            horizontalDirection.Normalize();

            float cameraAngle = cameraForward.sqrMagnitude > 0.0001f
                ? Vector3.Angle(cameraForward, horizontalDirection)
                : 180f;

            float playerAngle = playerForward.sqrMagnitude > 0.0001f
                ? Vector3.Angle(playerForward, horizontalDirection)
                : 180f;

            float angle = Mathf.Min(cameraAngle, playerAngle);
            if (angle > angleLimit * 0.5f)
                continue;

            if (requireProximityLineOfSight &&
                !HasLineOfSight(origin, targetPoint, current, explicitInteractionRequest))
            {
                continue;
            }

            float normalizedAngle = angle / Mathf.Max(1f, angleLimit * 0.5f);
            float score = distance + normalizedAngle * radius * 0.25f;

            if (score >= bestScore)
                continue;

            bestScore = score;
            best = current;
        }

        if (drawDebug)
        {
            Vector3 direction = cameraForward.sqrMagnitude > 0.0001f
                ? cameraForward
                : playerForward;
            Debug.DrawRay(origin, direction * radius, best != null ? Color.cyan : Color.yellow);
        }

        return best;
    }

    private bool HasLineOfSight(
        Vector3 origin,
        Vector3 targetPoint,
        InteractiveObject target,
        bool explicitInteractionRequest)
    {
        Vector3 direction = targetPoint - origin;
        float targetDistance = direction.magnitude;
        if (targetDistance <= 0.001f)
            return true;

        QueryTriggerInteraction triggerMode = ignoreTriggers
            ? QueryTriggerInteraction.Ignore
            : QueryTriggerInteraction.Collide;

        int count = Physics.RaycastNonAlloc(
            origin,
            direction / targetDistance,
            visibilityHits,
            targetDistance + 0.15f,
            interactionLayers,
            triggerMode
        );

        Collider closest = null;
        float closestDistance = float.PositiveInfinity;

        for (int i = 0; i < count; i++)
        {
            Collider hitCollider = visibilityHits[i].collider;
            if (hitCollider == null || IsPlayerCollider(hitCollider))
                continue;

            if (visibilityHits[i].distance >= closestDistance)
                continue;

            closestDistance = visibilityHits[i].distance;
            closest = hitCollider;
        }

        if (closest == null)
            return true;

        if (BelongsToInteractiveObject(closest.transform, target))
            return true;

        bool closeEnoughForFrameTolerance =
            explicitInteractionRequest &&
            targetDistance <= closeRangeToleranceDistance &&
            targetDistance - closestDistance <= closeRangeOccluderTolerance;

        return closeEnoughForFrameTolerance;
    }

    private static bool BelongsToInteractiveObject(
        Transform hitTransform,
        InteractiveObject target)
    {
        if (hitTransform == null || target == null)
            return false;

        Transform targetTransform = target.transform;
        if (hitTransform == targetTransform ||
            hitTransform.IsChildOf(targetTransform) ||
            targetTransform.IsChildOf(hitTransform))
        {
            return true;
        }

        InteractiveObject hitInteractive = hitTransform.GetComponentInParent<InteractiveObject>();
        return hitInteractive == target;
    }

    private bool IsValidCandidate(InteractiveObject candidate)
    {
        return candidate != null &&
               candidate.canInteract &&
               candidate.isActiveAndEnabled;
    }

    private bool ShouldUseThirdPersonFallback()
    {
        return IsThirdPerson() &&
               playerRoot != null &&
               (usePlayerOriginFallbackInThirdPerson || useProximityFallbackInThirdPerson);
    }

    private bool IsThirdPerson()
    {
        return cameraController == null || !cameraController.IsFirstPerson;
    }

    private Vector3 GetPlayerInteractionOrigin()
    {
        Transform root = playerRoot != null ? playerRoot : transform;
        return root.position + Vector3.up * thirdPersonOriginHeight;
    }

    private Ray BuildRay()
    {
        if (useScreenCenter || !hasExternalScreenPosition)
            return cameraSource.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));

        return cameraSource.ScreenPointToRay(externalScreenPosition);
    }

    private bool IsPlayerCollider(Collider collider)
    {
        if (collider == null || playerRoot == null)
            return false;

        Transform target = collider.transform;
        return target == playerRoot || target.IsChildOf(playerRoot);
    }

    private void SetFocusedObject(InteractiveObject target)
    {
        if (focusedObject == target)
            return;

        if (focusedObject != null)
            focusedObject.SetFocused(false);

        focusedObject = target;

        if (focusedObject != null)
            focusedObject.SetFocused(true);
    }

    private void ResolveReferences()
    {
        if (cameraController == null)
            cameraController = GetComponent<PlayerCameraController>();

        if (cameraController == null)
        {
            cameraController = Object.FindAnyObjectByType<PlayerCameraController>(
                FindObjectsInactive.Include
            );
        }

        if (cameraController != null)
        {
            if (cameraSource == null || !cameraSource.isActiveAndEnabled)
                cameraSource = cameraController.gameCamera;

            if (playerRoot == null)
                playerRoot = cameraController.player;
        }

        if (cameraSource == null)
            cameraSource = GetComponent<Camera>();

        if (cameraSource == null)
            cameraSource = Camera.main;

        if (getItemController == null)
            getItemController = GetComponent<GetItemController>();
    }

    private void DrawDebugRay(Ray ray, float distance, bool found)
    {
        if (!drawDebug)
            return;

        Debug.DrawRay(
            ray.origin,
            ray.direction * distance,
            found ? Color.green : Color.red
        );
    }

    private void OnValidate()
    {
        interactionDistance = Mathf.Max(0.1f, interactionDistance);
        interactionRadius = Mathf.Max(0f, interactionRadius);
        thirdPersonOriginHeight = Mathf.Max(0f, thirdPersonOriginHeight);
        thirdPersonFallbackDistance = Mathf.Max(0.1f, thirdPersonFallbackDistance);
        thirdPersonProximityRadius = Mathf.Max(0.5f, thirdPersonProximityRadius);
        thirdPersonProximityAngle = Mathf.Clamp(thirdPersonProximityAngle, 10f, 180f);
        interactionRequestProximityRadius = Mathf.Max(0.5f, interactionRequestProximityRadius);
        interactionRequestProximityAngle = Mathf.Clamp(interactionRequestProximityAngle, 10f, 180f);
        closeRangeOccluderTolerance = Mathf.Max(0f, closeRangeOccluderTolerance);
        closeRangeToleranceDistance = Mathf.Max(0.25f, closeRangeToleranceDistance);
    }
}
