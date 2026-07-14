using UnityEngine;

/// <summary>
/// Detecta objetos interativos pela câmera em primeira ou terceira pessoa.
/// O mesmo componente funciona com mouse, toque central e botões de UI mobile.
/// Itens GrabbableItem continuam sob responsabilidade do GetItemController.
///
/// Em terceira pessoa, além dos raios da câmera e do personagem, usa uma busca
/// de proximidade em cone. Isso permite abrir portas próximas sem entrar na mira,
/// mantendo proteção contra interação através de paredes.
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
    [Tooltip("Procura portas e outros objetos interativos próximos quando os raios centrais não encontram nada.")]
    public bool useProximityFallbackInThirdPerson = true;

    [Min(0.5f)] public float thirdPersonProximityRadius = 3.25f;
    [Range(10f, 180f)] public float thirdPersonProximityAngle = 115f;

    [Tooltip("Impede que a busca de proximidade concorra com o sistema de pegar caixas.")]
    public bool ignoreGrabbablesInProximityFallback = true;

    [Tooltip("Impede abrir uma porta através de paredes ou objetos sólidos.")]
    public bool requireProximityLineOfSight = true;

    [Header("Input Desktop")]
    public bool interactWithMouse = true;
    [Range(0, 2)] public int interactMouseButton = 0;
    public bool interactWithKey = true;
    public KeyCode interactKey = KeyCode.E;
    public bool skipMouseInteractionForGrabbables = true;

    [Header("Debug")]
    public bool drawDebug;
    public bool logInteractions;

    private readonly RaycastHit[] hits = new RaycastHit[48];
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

        SetFocusedObject(FindCandidate());

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

        if (requested)
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

    private InteractiveObject FindCandidate()
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

        Vector3 playerOrigin = playerRoot.position + Vector3.up * thirdPersonOriginHeight;
        Vector3 screenTarget = cameraRay.origin + cameraRay.direction * interactionDistance;
        Vector3 direction = screenTarget - playerOrigin;

        Ray playerRay = new Ray(playerOrigin, cameraRay.direction);
        float fallbackDistance = Mathf.Max(0.1f, thirdPersonFallbackDistance);

        if (direction.sqrMagnitude > 0.0001f)
        {
            fallbackDistance = Mathf.Min(fallbackDistance, direction.magnitude);
            playerRay = new Ray(playerOrigin, direction.normalized);

            candidate = FindCandidateAlongRay(playerRay, fallbackDistance, out _);
            if (candidate != null)
            {
                if (drawDebug)
                {
                    DrawDebugRay(cameraRay, interactionDistance, false);
                    DrawDebugRay(playerRay, fallbackDistance, true);
                }

                return candidate;
            }
        }

        candidate = FindThirdPersonProximityCandidate(playerOrigin);

        if (drawDebug)
        {
            DrawDebugRay(cameraRay, interactionDistance, false);
            DrawDebugRay(playerRay, fallbackDistance, false);
        }

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

        int count;
        if (interactionRadius > 0.001f)
        {
            count = Physics.SphereCastNonAlloc(
                ray,
                interactionRadius,
                hits,
                maxDistance,
                interactionLayers,
                triggerMode
            );
        }
        else
        {
            count = Physics.RaycastNonAlloc(
                ray,
                hits,
                maxDistance,
                interactionLayers,
                triggerMode
            );
        }

        InteractiveObject best = null;
        bestDistance = float.PositiveInfinity;

        for (int i = 0; i < count; i++)
        {
            Collider hitCollider = hits[i].collider;
            if (hitCollider == null || IsPlayerCollider(hitCollider))
                continue;

            InteractiveObject current = hitCollider.GetComponentInParent<InteractiveObject>();
            if (current == null || !current.canInteract || !current.isActiveAndEnabled)
                continue;

            if (hits[i].distance >= bestDistance)
                continue;

            bestDistance = hits[i].distance;
            best = current;
        }

        return best;
    }

    private InteractiveObject FindThirdPersonProximityCandidate(Vector3 origin)
    {
        if (!useProximityFallbackInThirdPerson || playerRoot == null)
            return null;

        QueryTriggerInteraction triggerMode = ignoreTriggers
            ? QueryTriggerInteraction.Ignore
            : QueryTriggerInteraction.Collide;

        int count = Physics.OverlapSphereNonAlloc(
            origin,
            Mathf.Max(0.5f, thirdPersonProximityRadius),
            proximityHits,
            interactionLayers,
            triggerMode
        );

        Vector3 viewForward = cameraSource != null
            ? Vector3.ProjectOnPlane(cameraSource.transform.forward, Vector3.up)
            : Vector3.ProjectOnPlane(playerRoot.forward, Vector3.up);

        if (viewForward.sqrMagnitude <= 0.0001f)
            viewForward = playerRoot.forward;

        viewForward.Normalize();

        InteractiveObject best = null;
        float bestScore = float.PositiveInfinity;

        for (int i = 0; i < count; i++)
        {
            Collider candidateCollider = proximityHits[i];
            if (candidateCollider == null || IsPlayerCollider(candidateCollider))
                continue;

            InteractiveObject current = candidateCollider.GetComponentInParent<InteractiveObject>();
            if (current == null || !current.canInteract || !current.isActiveAndEnabled)
                continue;

            if (ignoreGrabbablesInProximityFallback &&
                current.GetComponentInParent<GrabbableItem>() != null)
            {
                continue;
            }

            Vector3 targetPoint = candidateCollider.bounds.center;
            Vector3 toTarget = targetPoint - origin;
            float distance = toTarget.magnitude;

            if (distance <= 0.001f || distance > thirdPersonProximityRadius)
                continue;

            Vector3 horizontalDirection = Vector3.ProjectOnPlane(toTarget, Vector3.up);
            if (horizontalDirection.sqrMagnitude <= 0.0001f)
                horizontalDirection = toTarget;

            float angle = Vector3.Angle(viewForward, horizontalDirection.normalized);
            if (angle > thirdPersonProximityAngle * 0.5f)
                continue;

            if (requireProximityLineOfSight &&
                !HasLineOfSight(origin, targetPoint, current))
            {
                continue;
            }

            float normalizedAngle = angle / Mathf.Max(1f, thirdPersonProximityAngle * 0.5f);
            float score = distance + normalizedAngle * thirdPersonProximityRadius * 0.35f;

            if (score >= bestScore)
                continue;

            bestScore = score;
            best = current;
        }

        if (drawDebug)
        {
            Color color = best != null ? Color.cyan : Color.yellow;
            Debug.DrawRay(origin, viewForward * thirdPersonProximityRadius, color);
        }

        return best;
    }

    private bool HasLineOfSight(
        Vector3 origin,
        Vector3 targetPoint,
        InteractiveObject target)
    {
        Vector3 direction = targetPoint - origin;
        float distance = direction.magnitude;
        if (distance <= 0.001f)
            return true;

        QueryTriggerInteraction triggerMode = ignoreTriggers
            ? QueryTriggerInteraction.Ignore
            : QueryTriggerInteraction.Collide;

        int count = Physics.RaycastNonAlloc(
            origin,
            direction.normalized,
            visibilityHits,
            distance + 0.15f,
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

        InteractiveObject hitInteractive = closest.GetComponentInParent<InteractiveObject>();
        if (hitInteractive == target)
            return true;

        Transform hitTransform = closest.transform;
        Transform targetTransform = target.transform;
        return hitTransform == targetTransform ||
               hitTransform.IsChildOf(targetTransform) ||
               targetTransform.IsChildOf(hitTransform);
    }

    private bool ShouldUseThirdPersonFallback()
    {
        return playerRoot != null &&
               (cameraController == null || !cameraController.IsFirstPerson) &&
               (usePlayerOriginFallbackInThirdPerson || useProximityFallbackInThirdPerson);
    }

    private Ray BuildRay()
    {
        if (useScreenCenter || !hasExternalScreenPosition)
            return cameraSource.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));

        return cameraSource.ScreenPointToRay(externalScreenPosition);
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

        if (cameraSource == null && cameraController != null)
            cameraSource = cameraController.gameCamera;

        if (cameraSource == null)
            cameraSource = GetComponent<Camera>();

        if (cameraSource == null && Camera.main != null)
            cameraSource = Camera.main;

        if (getItemController == null)
            getItemController = GetComponent<GetItemController>();

        if (playerRoot == null && cameraController != null)
            playerRoot = cameraController.player;
    }

    private void OnValidate()
    {
        interactionDistance = Mathf.Max(0.1f, interactionDistance);
        interactionRadius = Mathf.Max(0f, interactionRadius);
        thirdPersonOriginHeight = Mathf.Max(0f, thirdPersonOriginHeight);
        thirdPersonFallbackDistance = Mathf.Max(0.1f, thirdPersonFallbackDistance);
        thirdPersonProximityRadius = Mathf.Max(0.5f, thirdPersonProximityRadius);
        thirdPersonProximityAngle = Mathf.Clamp(thirdPersonProximityAngle, 10f, 180f);
    }
}
