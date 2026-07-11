using UnityEngine;

/// <summary>
/// Detecta objetos interativos pela câmera em primeira ou terceira pessoa.
/// O mesmo componente funciona com mouse, toque central e botões de UI mobile.
/// Itens GrabbableItem continuam sob responsabilidade do GetItemController.
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
        Ray ray = BuildRay();
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
                interactionDistance,
                interactionLayers,
                triggerMode
            );
        }
        else
        {
            count = Physics.RaycastNonAlloc(
                ray,
                hits,
                interactionDistance,
                interactionLayers,
                triggerMode
            );
        }

        InteractiveObject best = null;
        float bestDistance = float.PositiveInfinity;

        for (int i = 0; i < count; i++)
        {
            Collider hitCollider = hits[i].collider;
            if (hitCollider == null || IsPlayerCollider(hitCollider))
                continue;

            InteractiveObject candidate = hitCollider.GetComponentInParent<InteractiveObject>();
            if (candidate == null || !candidate.canInteract || !candidate.isActiveAndEnabled)
                continue;

            if (hits[i].distance >= bestDistance)
                continue;

            bestDistance = hits[i].distance;
            best = candidate;
        }

        if (drawDebug)
        {
            Color color = best != null ? Color.green : Color.red;
            Debug.DrawRay(ray.origin, ray.direction * interactionDistance, color);
        }

        return best;
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
    }
}
