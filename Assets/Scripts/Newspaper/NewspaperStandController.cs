using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Controla a coleta de jornal no Newspaper_Stand.
/// Referências estruturais são resolvidas somente na inicialização ou quando ficam nulas;
/// o Update processa apenas distância, input e animação do estado.
/// </summary>
[DisallowMultipleComponent]
public sealed class NewspaperStandController : MonoBehaviour
{
    [Header("Referências")]
    public GameObject newspaperVisual;
    public Transform interactionPoint;
    public Transform promptAnchor;
    public InteractionHighlight highlight;
    public GrabbableItem grabbableItem;

    [Header("Interação")]
    public KeyCode interactionKey = KeyCode.E;
    [Min(0.25f)] public float interactionRadius = 2.4f;
    [Min(0.1f)] public float holdDuration = 1.25f;
    public bool resetProgressWhenReleased = true;
    [Min(0f)] public float progressDecayPerSecond = 1.8f;

    [Header("Respawn")]
    [Min(0.5f)] public float respawnSeconds = 10f;
    [Min(1f)] public float respawnPromptDistance = 8f;
    public bool showRespawnProgress = true;

    [Header("Prompt Persistente")]
    public NewspaperWorldPromptVisual promptVisual;
    public bool alwaysShowPrompt = true;
    public bool previewPromptInEditMode = true;

    [Tooltip("Usado apenas se o prompt ainda precisar ser criado como fallback em runtime.")]
    public Vector3 promptLocalOffset = new Vector3(0f, 0.72f, 0f);

    [Tooltip("Usado apenas se o prompt ainda precisar ser criado como fallback em runtime.")]
    [Min(0.0005f)] public float promptWorldScale = 0.0023f;

    [Min(0f)] public float promptRotationSpeed = 38f;
    public Color availableColor = new Color32(88, 210, 255, 255);
    public Color holdingColor = new Color32(89, 244, 128, 255);
    public Color respawnColor = new Color32(255, 190, 55, 255);

    [Header("Textos")]
    public string availableInstruction = "Segure 'E' para pegar";
    public string holdingInstruction = "Coletando jornal...";
    public string respawnInstruction = "Novo jornal em {0:0.0}s";

    [Header("Eventos")]
    public UnityEvent onCollected;
    public UnityEvent onRespawned;

    [Header("Debug")]
    public bool logEvents;

    private MiniMarketNewspaperInventoryService inventory;
    private Transform player;
    private float nextDependencyResolve;
    private float holdProgress;
    private float respawnRemaining;
    private bool newspaperAvailable = true;
    private bool lastNearState;
    private bool nearStateInitialized;

    public bool NewspaperAvailable => newspaperAvailable;
    public float HoldProgress01 => Mathf.Clamp01(holdProgress);
    public float RespawnRemaining => Mathf.Max(0f, respawnRemaining);

    private void Awake()
    {
        ResolveSceneReferences();
        ResolveRuntimeDependencies(true);
        ConfigureControlledNewspaper();
        EnsurePromptReference();
        SetNewspaperAvailable(true);
    }

    private void OnEnable()
    {
        ResolveSceneReferences();
        ResolveRuntimeDependencies(true);
        ConfigureControlledNewspaper();
        EnsurePromptReference();

        if (!Application.isPlaying && promptVisual != null && previewPromptInEditMode)
            promptVisual.SetVisible(true);
    }

    private void OnDisable()
    {
        if (highlight != null)
            highlight.Clear();

        nearStateInitialized = false;

        if (Application.isPlaying && promptVisual != null)
            promptVisual.SetVisible(false);
    }

    private void Update()
    {
        ResolveRuntimeDependencies(false);

        if (promptVisual == null && Time.unscaledTime >= nextDependencyResolve)
            EnsurePromptReference();

        if (newspaperAvailable)
            UpdateAvailableState();
        else
            UpdateRespawnState();
    }

    private void UpdateAvailableState()
    {
        bool near = IsPlayerWithin(interactionRadius);
        UpdateHighlight(near);

        bool inputAllowed = !GameplayInputState.IsBlocked;
        bool holding = near && inputAllowed && Input.GetKey(interactionKey);

        if (holding)
        {
            holdProgress = Mathf.Clamp01(
                holdProgress + Time.deltaTime / Mathf.Max(0.1f, holdDuration)
            );
        }
        else if (!near || !Input.GetKey(interactionKey))
        {
            DecayOrResetHoldProgress();
        }

        if (promptVisual != null)
        {
            bool progressing = holdProgress > 0.001f;
            promptVisual.SetVisible(alwaysShowPrompt || near);
            promptVisual.SetInteractionPrompt(
                "E",
                progressing ? holdingInstruction : availableInstruction,
                holdProgress,
                progressing,
                progressing ? holdingColor : availableColor
            );
        }

        if (near && inputAllowed && holdProgress >= 0.999f)
            CollectNewspaper();
    }

    private void UpdateRespawnState()
    {
        respawnRemaining = Mathf.Max(0f, respawnRemaining - Time.deltaTime);
        float duration = Mathf.Max(0.5f, respawnSeconds);
        float progress = 1f - respawnRemaining / duration;
        bool show = alwaysShowPrompt ||
                    (showRespawnProgress && IsPlayerWithin(respawnPromptDistance));

        if (promptVisual != null)
        {
            promptVisual.SetVisible(show);
            promptVisual.SetInteractionPrompt(
                Mathf.RoundToInt(Mathf.Clamp01(progress) * 100f) + "%",
                string.Format(respawnInstruction, respawnRemaining),
                progress,
                true,
                respawnColor
            );
        }

        if (respawnRemaining <= 0.001f)
            RespawnNewspaper();
    }

    public void CollectNewspaper()
    {
        if (!newspaperAvailable)
            return;

        ResolveRuntimeDependencies(true);
        if (inventory == null)
        {
            Debug.LogWarning("[NewspaperStand] Inventário de jornais não encontrado.", this);
            return;
        }

        inventory.AdicionarJornais(1);
        holdProgress = 0f;
        respawnRemaining = Mathf.Max(0.5f, respawnSeconds);
        SetNewspaperAvailable(false);

        if (promptVisual != null)
        {
            promptVisual.SetVisible(true);
            promptVisual.SetInteractionPrompt(
                "0%",
                string.Format(respawnInstruction, respawnRemaining),
                0f,
                true,
                respawnColor
            );
        }

        onCollected?.Invoke();

        if (logEvents)
        {
            Debug.Log(
                "[NewspaperStand] Jornal coletado. Inventário: " + inventory.QuantidadeJornais,
                this
            );
        }
    }

    public void RespawnNewspaper()
    {
        respawnRemaining = 0f;
        holdProgress = 0f;
        SetNewspaperAvailable(true);

        if (promptVisual != null)
        {
            promptVisual.SetVisible(true);
            promptVisual.SetInteractionPrompt(
                "E",
                availableInstruction,
                0f,
                false,
                availableColor
            );
        }

        onRespawned?.Invoke();

        if (logEvents)
            Debug.Log("[NewspaperStand] Novo jornal disponível.", this);
    }

    private void SetNewspaperAvailable(bool available)
    {
        newspaperAvailable = available;

        if (newspaperVisual != null && newspaperVisual.activeSelf != available)
            newspaperVisual.SetActive(available);

        if (highlight != null)
        {
            highlight.Clear();
            highlight.enabled = available;
        }

        nearStateInitialized = false;
    }

    private void UpdateHighlight(bool near)
    {
        if (highlight == null)
            return;

        if (nearStateInitialized && lastNearState == near)
            return;

        lastNearState = near;
        nearStateInitialized = true;
        highlight.SetFocused(near);
    }

    private void ConfigureControlledNewspaper()
    {
        if (newspaperVisual == null)
            return;

        if (grabbableItem == null)
            grabbableItem = newspaperVisual.GetComponentInChildren<GrabbableItem>(true);

        if (grabbableItem != null)
        {
            grabbableItem.canBeGrabbed = false;
            grabbableItem.enabled = false;
            grabbableItem.SetSelected(false);
        }

        Rigidbody[] bodies = newspaperVisual.GetComponentsInChildren<Rigidbody>(true);
        for (int i = 0; i < bodies.Length; i++)
        {
            Rigidbody body = bodies[i];
            if (body == null)
                continue;

            if (!body.isKinematic)
            {
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
            }

            body.useGravity = false;
            body.isKinematic = true;
        }
    }

    private void ResolveSceneReferences()
    {
        if (newspaperVisual == null)
        {
            Transform candidate = FindChildRecursive(transform, "Jornal");
            if (candidate != null)
                newspaperVisual = candidate.gameObject;
        }

        if (interactionPoint == null)
            interactionPoint = newspaperVisual != null ? newspaperVisual.transform : transform;

        if (promptAnchor == null)
            promptAnchor = transform;

        if (highlight == null && newspaperVisual != null)
            highlight = newspaperVisual.GetComponent<InteractionHighlight>();
    }

    private void ResolveRuntimeDependencies(bool force)
    {
        if (!Application.isPlaying)
            return;

        if (!force && Time.unscaledTime < nextDependencyResolve)
            return;

        nextDependencyResolve = Time.unscaledTime + 0.5f;

        if (inventory == null)
        {
            inventory = MiniMarketNewspaperInventoryService.Instance;
            if (inventory == null)
            {
                inventory = UnityEngine.Object.FindAnyObjectByType<MiniMarketNewspaperInventoryService>(
                    FindObjectsInactive.Include
                );
            }
        }

        if (player == null)
        {
            CameraRelativeMovement movement =
                UnityEngine.Object.FindAnyObjectByType<CameraRelativeMovement>(
                    FindObjectsInactive.Exclude
                );

            if (movement != null)
                player = movement.transform;
        }
    }

    private void EnsurePromptReference()
    {
        if (promptVisual != null)
            return;

        NewspaperWorldPromptVisual[] prompts =
            GetComponentsInChildren<NewspaperWorldPromptVisual>(true);

        for (int i = 0; i < prompts.Length; i++)
        {
            NewspaperWorldPromptVisual candidate = prompts[i];
            if (candidate != null && candidate.name == "Newspaper_InteractionPrompt")
            {
                promptVisual = candidate;
                break;
            }
        }

        if (promptVisual == null && prompts.Length > 0)
            promptVisual = prompts[0];

        if (promptVisual == null && Application.isPlaying)
        {
            Transform anchor = promptAnchor != null ? promptAnchor : transform;
            promptVisual = NewspaperWorldPromptVisual.Create(
                anchor,
                "Newspaper_InteractionPrompt",
                promptLocalOffset,
                promptWorldScale
            );
            promptVisual.rotationDegreesPerSecond = promptRotationSpeed;
        }

        if (promptVisual != null && !Application.isPlaying && previewPromptInEditMode)
            promptVisual.SetVisible(true);
    }

    private bool IsPlayerWithin(float distance)
    {
        if (player == null)
            return false;

        Vector3 point = interactionPoint != null ? interactionPoint.position : transform.position;
        float maxDistance = Mathf.Max(0.1f, distance);
        return (player.position - point).sqrMagnitude <= maxDistance * maxDistance;
    }

    private void DecayOrResetHoldProgress()
    {
        if (resetProgressWhenReleased)
        {
            holdProgress = 0f;
            return;
        }

        holdProgress = Mathf.MoveTowards(
            holdProgress,
            0f,
            Mathf.Max(0f, progressDecayPerSecond) * Time.deltaTime
        );
    }

    private static Transform FindChildRecursive(Transform root, string exactName)
    {
        if (root == null)
            return null;

        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);
            if (child.name == exactName)
                return child;

            Transform nested = FindChildRecursive(child, exactName);
            if (nested != null)
                return nested;
        }

        return null;
    }

    private void OnValidate()
    {
        interactionRadius = Mathf.Max(0.25f, interactionRadius);
        holdDuration = Mathf.Max(0.1f, holdDuration);
        respawnSeconds = Mathf.Max(0.5f, respawnSeconds);
        respawnPromptDistance = Mathf.Max(1f, respawnPromptDistance);
        promptWorldScale = Mathf.Max(0.0005f, promptWorldScale);
        progressDecayPerSecond = Mathf.Max(0f, progressDecayPerSecond);

        if (!Application.isPlaying)
            ResolveSceneReferences();
    }
}
