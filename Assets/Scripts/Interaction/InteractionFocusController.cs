using UnityEngine;

/// <summary>
/// Detecta objetos interativos pela câmera em primeira ou terceira pessoa.
/// O mesmo componente funciona com mouse, toque central e botões de UI mobile.
/// Itens GrabbableItem continuam sob responsabilidade do GetItemController.
///
/// Em terceira pessoa, além do raio da câmera, usa um segundo raio saindo da altura
/// do personagem em direção ao centro da tela. Isso evita que a distância/offset da
/// câmera impeça a abertura de portas próximas ao jogador.
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

    [Header("Terceira pessoa")]
    public bool usePlayerOriginFallbackInThirdPerson = true;
    [Min(0f)] public float thirdPersonOriginHeight = 1.25f;
    [Min(0.1f)] public float thirdPersonFallbackDistance = 7f;

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
            out float cameraDistance
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

        if (direction.sqrMagnitude <= 0.0001f)
        {
            DrawDebugRay(cameraRay, interactionDistance, false);
            return null;
        }

        float fallbackDistance = Mathf.Min(
            Mathf.Max(0.1f, thirdPersonFallbackDistance),
            direction.magnitude
        );

        Ray playerRay = new Ray(playerOrigin, direction.normalized);
        candidate = FindCandidateAlongRay(playerRay, fallbackDistance, out float playerDistance);

        if (drawDebug)
        {
            DrawDebugRay(cameraRay, interactionDistance, false);
            DrawDebugRay(playerRay, fallbackDistance, candidate != null);
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

    private bool ShouldUseThirdPersonFallback()
    {
        return usePlayerOriginFallbackInThirdPerson &&
               playerRoot != null &&
               (cameraController == null || !cameraController.IsFirstPerson);
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
    }
}
