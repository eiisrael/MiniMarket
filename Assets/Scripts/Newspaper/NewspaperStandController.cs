using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Controla a coleta de jornal no Newspaper_Stand.
/// O prompt do expositor é persistente na cena, sempre visível e reutilizado
/// tanto para a tecla E quanto para o contador de respawn.
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
    private float nextPlayerResolve;
    private float holdProgress;
    private float respawnRemaining;
    private bool newspaperAvailable = true;

    public bool NewspaperAvailable => newspaperAvailable;
    public float HoldProgress01 => Mathf.Clamp01(holdProgress);
    public float RespawnRemaining => Mathf.Max(0f, respawnRemaining);

    private void Awake()
    {
        ResolveReferences();
        ConfigureControlledNewspaper();
        EnsurePrompt();
        SetNewspaperAvailable(true);
    }

    private void OnEnable()
    {
        ResolveReferences();
        ConfigureControlledNewspaper();
        EnsurePrompt();

        if (!Application.isPlaying && promptVisual != null && previewPromptInEditMode)
            promptVisual.SetVisible(true);
    }

    private void OnDisable()
    {
        if (highlight != null)
            highlight.Clear();

        if (Application.isPlaying && promptVisual != null)
            promptVisual.SetVisible(false);
    }

    private void Update()
    {
        ResolveReferences();
        EnsurePrompt();

        if (newspaperAvailable)
            UpdateAvailableState();
        else
            UpdateRespawnState();
    }

    private void UpdateAvailableState()
    {
        bool near = IsPlayerWithin(interactionRadius);

        if (highlight != null)
            highlight.SetFocused(near);

        bool holding = near && !GameplayInputState.IsBlocked && Input.GetKey(interactionKey);

        if (holding)
        {
            holdProgress += Time.deltaTime / Mathf.Max(0.1f, holdDuration);
            holdProgress = Mathf.Clamp01(holdProgress);
        }
        else if (!near || !Input.GetKey(interactionKey))
        {
            DecayOrResetHoldProgress();
        }

        if (promptVisual != null)
        {
            promptVisual.SetVisible(alwaysShowPrompt || near);
            promptVisual.SetInteractionPrompt(
                "E",
                holdProgress > 0.001f ? holdingInstruction : availableInstruction,
                holdProgress,
                holdProgress > 0.001f,
                holdProgress > 0.001f ? holdingColor : availableColor
            );
        }

        if (!near || GameplayInputState.IsBlocked)
            return;

        if (holdProgress >= 0.999f)
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

        ResolveReferences();
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

        if (newspaperVisual != null)
            newspaperVisual.SetActive(available);

        if (highlight != null)
        {
            highlight.Clear();
            highlight.enabled = available;
        }
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

            body.isKinematic = true;
            body.useGravity = false;
        }
    }

    private void ResolveReferences()
    {
        if (inventory == null)
            inventory = MiniMarketNewspaperInventoryService.Instance;

        if (inventory == null && Application.isPlaying)
        {
            inventory = UnityEngine.Object.FindAnyObjectByType<MiniMarketNewspaperInventoryService>(
                FindObjectsInactive.Include
            );
        }

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
        {
            highlight = newspaperVisual.GetComponent<InteractionHighlight>();
            if (highlight == null && Application.isPlaying)
                highlight = newspaperVisual.AddComponent<InteractionHighlight>();
        }

        if (player == null && Application.isPlaying && Time.unscaledTime >= nextPlayerResolve)
        {
            nextPlayerResolve = Time.unscaledTime + 0.5f;
            CameraRelativeMovement movement =
                UnityEngine.Object.FindAnyObjectByType<CameraRelativeMovement>(
                    FindObjectsInactive.Exclude
                );
            if (movement != null)
                player = movement.transform;
        }
    }

    private void EnsurePrompt()
    {
        NewspaperWorldPromptVisual[] prompts =
            GetComponentsInChildren<NewspaperWorldPromptVisual>(true);

        NewspaperWorldPromptVisual selected = promptVisual;

        if (selected == null)
        {
            for (int i = 0; i < prompts.Length; i++)
            {
                if (prompts[i] != null && prompts[i].name == "Newspaper_InteractionPrompt")
                {
                    selected = prompts[i];
                    break;
                }
            }
        }

        if (selected == null && prompts.Length > 0)
            selected = prompts[0];

        promptVisual = selected;

        for (int i = 0; i < prompts.Length; i++)
        {
            NewspaperWorldPromptVisual candidate = prompts[i];
            if (candidate == null || candidate == promptVisual)
                continue;

            candidate.SetVisible(false);
            if (Application.isPlaying)
                candidate.enabled = false;
        }

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
        }
        else
        {
            holdProgress = Mathf.MoveTowards(
                holdProgress,
                0f,
                Mathf.Max(0f, progressDecayPerSecond) * Time.deltaTime
            );
        }
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

        if (newspaperVisual == null)
        {
            Transform candidate = FindChildRecursive(transform, "Jornal");
            if (candidate != null)
                newspaperVisual = candidate.gameObject;
        }

        if (promptAnchor == null)
            promptAnchor = transform;

        if (promptVisual == null)
        {
            NewspaperWorldPromptVisual[] prompts =
                GetComponentsInChildren<NewspaperWorldPromptVisual>(true);

            for (int i = 0; i < prompts.Length; i++)
            {
                if (prompts[i] != null && prompts[i].name == "Newspaper_InteractionPrompt")
                {
                    promptVisual = prompts[i];
                    break;
                }
            }
        }
    }
}
