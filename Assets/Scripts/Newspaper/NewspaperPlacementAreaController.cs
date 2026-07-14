using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Área onde o jogador coloca um jornal coletado.
///
/// O Placed_Newspaper_Runtime é preparado antes da interação, permanece editável na
/// cena e durante o jogo é somente ativado/desativado. Renderers, física e referências
/// são cacheados para que o clique de colocar não faça reconstruções recursivas.
/// </summary>
[DisallowMultipleComponent]
public sealed class NewspaperPlacementAreaController : MonoBehaviour
{
    public const string PersistentPlacedVisualName = "Placed_Newspaper_Runtime";

    [Header("Identificação")]
    public string placeId = "BRONZE_MARKET_NEWSPAPER_PLACE";

    [Header("Referências")]
    public Transform putArea;

    [Tooltip("Jornal real usado como fonte. Deve apontar para Newspaper_Stand/Jornal.")]
    public GameObject newspaperSourceVisual;

    [Tooltip("Jornal persistente colocado dentro da Put_Area.")]
    public GameObject placedNewspaperVisual;

    [Tooltip("Objeto opcional usado apenas para demonstrar onde o jornal será colocado.")]
    public GameObject placementGuideVisual;

    public Transform promptAnchor;
    public Collider areaCollider;

    [Header("Interação")]
    public KeyCode interactionKey = KeyCode.E;
    [Min(0.25f)] public float interactionRadius = 2.4f;
    public bool showOnlyWhenPlayerHasNewspaper = true;
    public bool persistPlacedState = true;

    [Header("Jornal persistente da cena")]
    public bool keepPlacedVisualInScene = true;

    [Tooltip("Marcado: posição, rotação e escala são controladas diretamente pelo Transform do Placed_Newspaper_Runtime.")]
    public bool usePlacedVisualTransformAsSource = true;

    [Tooltip("Mostra o jornal colocado fora do Play Mode para facilitar a edição.")]
    public bool previewPlacedVisualInEditMode = true;

    [Header("Transform inicial do jornal")]
    public Vector3 placedLocalPosition = Vector3.zero;
    public Vector3 placedLocalEuler = Vector3.zero;
    public Vector3 placedLocalScale = Vector3.one;
    public bool useSourceLocalRotation = true;
    public bool useSourceLocalScale = true;

    [Header("Demarcação no chão")]
    public Vector2 areaSize = new Vector2(1.35f, 0.9f);
    [Min(0f)] public float areaHeightOffset = 0.025f;
    [Min(0.005f)] public float lineWidth = 0.055f;
    public bool showCentralX = true;
    public bool showPlacementGuide = true;
    public Color availableAreaColor = new Color32(255, 215, 52, 255);
    public Color nearbyAreaColor = new Color32(73, 235, 118, 255);

    [Header("Prompt")]
    public NewspaperWorldPromptVisual promptVisual;
    public Vector3 promptLocalOffset = new Vector3(0f, 1.35f, 0f);
    [Min(0.0005f)] public float promptWorldScale = 0.0075f;
    [Min(0f)] public float promptRotationSpeed = 28f;
    public string promptInstruction = "Pressione 'E' para colocar o jornal";

    [Tooltip("Mantém o prompt voltado horizontalmente para a câmera ativa do jogador.")]
    public bool promptFaceCamera = true;

    [Header("Debug")]
    public bool logEvents;

    private MiniMarketNewspaperInventoryService inventory;
    private Transform player;
    private float nextDependencyResolve;
    private bool occupied;

    private LineRenderer borderLine;
    private LineRenderer diagonalA;
    private LineRenderer diagonalB;
    private Material lineMaterial;

    private Renderer[] guideRenderers = System.Array.Empty<Renderer>();
    private bool guideRenderersCached;
    private bool placedVisualPrepared;
    private bool lastOfferState;
    private bool lastNearState;
    private bool stateVisualInitialized;
    private bool lastGuideVisible;
    private bool guideVisibilityInitialized;

    public bool IsOccupied => occupied;
    public bool CanPlace => !occupied && inventory != null && inventory.PossuiJornal;

    private void Awake()
    {
        ResolveSceneReferences();
        ResolveRuntimeDependencies(true);
        EnsurePromptReference();
        EnsureAreaVisual();
        PreparePlacedVisualBeforeInteraction();
        RestorePlacedState();
    }

    private void OnEnable()
    {
        ResolveSceneReferences();
        ResolveRuntimeDependencies(true);
        EnsurePromptReference();
        EnsureAreaVisual();
        PreparePlacedVisualBeforeInteraction();

        if (Application.isPlaying)
            RestorePlacedState();
        else
            ApplyEditModePreview();
    }

    private void Update()
    {
        ResolveRuntimeDependencies(false);

        if (promptVisual == null && Time.unscaledTime >= nextDependencyResolve)
            EnsurePromptReference();

        RefreshState();
    }

    private void OnDisable()
    {
        if (!Application.isPlaying)
            return;

        SetAreaVisualVisible(false);
        SetPlacementGuideVisible(false);
        SetPromptVisible(false);
    }

    private void OnDestroy()
    {
        if (Application.isPlaying && lineMaterial != null)
            Destroy(lineMaterial);
    }

    private void RefreshState()
    {
        bool hasNewspaper = inventory != null && inventory.PossuiJornal;
        bool shouldOffer = !occupied && (!showOnlyWhenPlayerHasNewspaper || hasNewspaper);
        bool near = shouldOffer && IsPlayerWithin(interactionRadius);

        if (!stateVisualInitialized || lastOfferState != shouldOffer || lastNearState != near)
        {
            lastOfferState = shouldOffer;
            lastNearState = near;
            stateVisualInitialized = true;

            SetAreaVisualVisible(shouldOffer);
            SetPlacementGuideVisible(shouldOffer && showPlacementGuide);
            SetAreaColor(near ? nearbyAreaColor : availableAreaColor);
            SetPromptVisible(near);

            if (promptVisual != null)
            {
                promptVisual.SetInteractionPrompt(
                    "E",
                    promptInstruction,
                    0f,
                    false,
                    nearbyAreaColor
                );
            }
        }

        if (near && !GameplayInputState.IsBlocked && Input.GetKeyDown(interactionKey))
            TryPlaceNewspaper();
    }

    public bool TryPlaceNewspaper()
    {
        ResolveRuntimeDependencies(true);
        PreparePlacedVisualBeforeInteraction();

        if (occupied || inventory == null || placedNewspaperVisual == null)
            return false;

        if (!inventory.TentarConsumirJornal(1))
            return false;

        occupied = true;
        ShowPlacedNewspaper();

        if (persistPlacedState)
            inventory.DefinirJornalNoLocal(placeId, true);

        stateVisualInitialized = false;
        SetAreaVisualVisible(false);
        SetPlacementGuideVisible(false);
        SetPromptVisible(false);

        if (logEvents)
        {
            Debug.Log(
                "[NewspaperPlace] Jornal colocado em " + placeId +
                ". Restantes: " + inventory.QuantidadeJornais,
                this
            );
        }

        return true;
    }

    [ContextMenu("Jornal/Remover jornal colocado")]
    public void RemovePlacedNewspaper()
    {
        occupied = false;
        PreparePlacedVisualBeforeInteraction();

        if (placedNewspaperVisual != null)
        {
            if (keepPlacedVisualInScene || HasPersistentMarker(placedNewspaperVisual))
            {
                placedNewspaperVisual.SetActive(!Application.isPlaying && previewPlacedVisualInEditMode);
            }
            else
            {
                if (Application.isPlaying)
                    Destroy(placedNewspaperVisual);
                else
                    DestroyImmediate(placedNewspaperVisual);

                placedNewspaperVisual = null;
                placedVisualPrepared = false;
            }
        }

        ResolveRuntimeDependencies(true);
        if (persistPlacedState && inventory != null)
            inventory.DefinirJornalNoLocal(placeId, false);

        stateVisualInitialized = false;
    }

    private void RestorePlacedState()
    {
        ResolveRuntimeDependencies(true);
        PreparePlacedVisualBeforeInteraction();

        occupied = persistPlacedState && inventory != null && inventory.LocalPossuiJornal(placeId);

        if (placedNewspaperVisual != null)
            placedNewspaperVisual.SetActive(occupied);

        if (occupied)
            SetPlacementGuideVisible(false);

        stateVisualInitialized = false;
    }

    private void ApplyEditModePreview()
    {
        ResolveSceneReferences();
        ResolvePersistentPlacedVisualReference();

        if (placedNewspaperVisual != null && keepPlacedVisualInScene)
            placedNewspaperVisual.SetActive(previewPlacedVisualInEditMode);

        if (promptVisual != null)
            promptVisual.SetVisible(true);
    }

    private void PreparePlacedVisualBeforeInteraction()
    {
        if (placedVisualPrepared && placedNewspaperVisual != null)
            return;

        ResolvePersistentPlacedVisualReference();

        if (placedNewspaperVisual == null && Application.isPlaying)
            CreatePlacedNewspaperFallback();

        if (placedNewspaperVisual == null)
            return;

        EnsurePersistentMarkerRuntimeOnly(placedNewspaperVisual);
        SanitizePlacedVisualOnce(placedNewspaperVisual);
        SyncCompatibilityTransformFields();
        placedVisualPrepared = true;

        if (Application.isPlaying)
            placedNewspaperVisual.SetActive(false);
    }

    private void ResolvePersistentPlacedVisualReference()
    {
        if (placedNewspaperVisual != null && IsPlacedVisual(placedNewspaperVisual))
            return;

        Transform root = putArea != null ? putArea : transform;
        Transform existing = FindChildByPlacedName(root);
        placedNewspaperVisual = existing != null ? existing.gameObject : null;
    }

    private void ShowPlacedNewspaper()
    {
        if (placedNewspaperVisual == null)
            return;

        if (!usePlacedVisualTransformAsSource)
            ApplyPlacedTransform(placedNewspaperVisual.transform, false);

        placedNewspaperVisual.SetActive(true);
    }

    private void CreatePlacedNewspaperFallback()
    {
        if (newspaperSourceVisual == null || putArea == null)
        {
            Debug.LogWarning(
                "[NewspaperPlace] Jornal real do Newspaper_Stand ou Put_Area não configurado.",
                this
            );
            return;
        }

        placedNewspaperVisual = Instantiate(newspaperSourceVisual, putArea);
        placedNewspaperVisual.name = PersistentPlacedVisualName;
        ApplyPlacedTransform(placedNewspaperVisual.transform, true);
        placedNewspaperVisual.SetActive(false);
    }

    private void ApplyPlacedTransform(Transform target, bool forceInitialization)
    {
        if (target == null || putArea == null)
            return;

        target.SetParent(putArea, false);

        if (usePlacedVisualTransformAsSource && !forceInitialization)
            return;

        target.localPosition = placedLocalPosition;
        target.localRotation = useSourceLocalRotation && newspaperSourceVisual != null
            ? newspaperSourceVisual.transform.localRotation
            : Quaternion.Euler(placedLocalEuler);
        target.localScale = useSourceLocalScale && newspaperSourceVisual != null
            ? newspaperSourceVisual.transform.localScale
            : placedLocalScale;

        SyncCompatibilityTransformFields();
    }

    private void SyncCompatibilityTransformFields()
    {
        if (!usePlacedVisualTransformAsSource || placedNewspaperVisual == null)
            return;

        Transform value = placedNewspaperVisual.transform;
        placedLocalPosition = value.localPosition;
        placedLocalEuler = value.localEulerAngles;
        placedLocalScale = value.localScale;
    }

    private static bool IsPlacedVisual(GameObject target)
    {
        return target != null &&
               target.name.StartsWith(PersistentPlacedVisualName, System.StringComparison.Ordinal);
    }

    private static bool HasPersistentMarker(GameObject target)
    {
        return target != null && target.GetComponent<PersistentPlacedNewspaperVisual>() != null;
    }

    private static void EnsurePersistentMarkerRuntimeOnly(GameObject target)
    {
        if (!Application.isPlaying || target == null || HasPersistentMarker(target))
            return;

        target.AddComponent<PersistentPlacedNewspaperVisual>();
    }

    private void SanitizePlacedVisualOnce(GameObject target)
    {
        if (target == null)
            return;

        NewspaperStandController[] standControllers =
            target.GetComponentsInChildren<NewspaperStandController>(true);
        for (int i = 0; i < standControllers.Length; i++)
        {
            if (standControllers[i] != null)
                standControllers[i].enabled = false;
        }

        NewspaperPlacementAreaController[] placementControllers =
            target.GetComponentsInChildren<NewspaperPlacementAreaController>(true);
        for (int i = 0; i < placementControllers.Length; i++)
        {
            NewspaperPlacementAreaController controller = placementControllers[i];
            if (controller != null && controller != this)
                controller.enabled = false;
        }

        NewspaperWorldPromptVisual[] prompts =
            target.GetComponentsInChildren<NewspaperWorldPromptVisual>(true);
        for (int i = 0; i < prompts.Length; i++)
        {
            NewspaperWorldPromptVisual prompt = prompts[i];
            if (prompt == null)
                continue;

            prompt.SetVisible(false);
            prompt.enabled = false;
        }

        NewspaperInstructionTextSettings[] textSettings =
            target.GetComponentsInChildren<NewspaperInstructionTextSettings>(true);
        for (int i = 0; i < textSettings.Length; i++)
        {
            if (textSettings[i] != null)
                textSettings[i].enabled = false;
        }

        GrabbableItem[] grabbables = target.GetComponentsInChildren<GrabbableItem>(true);
        for (int i = 0; i < grabbables.Length; i++)
        {
            GrabbableItem item = grabbables[i];
            if (item == null)
                continue;

            item.canBeGrabbed = false;
            item.enabled = false;
            item.SetSelected(false);
        }

        InteractionHighlight[] highlights =
            target.GetComponentsInChildren<InteractionHighlight>(true);
        for (int i = 0; i < highlights.Length; i++)
        {
            InteractionHighlight value = highlights[i];
            if (value == null)
                continue;

            value.Clear();
            value.enabled = false;
        }

        Collider[] colliders = target.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] != null)
                colliders[i].enabled = false;
        }

        Rigidbody[] bodies = target.GetComponentsInChildren<Rigidbody>(true);
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
        if (putArea == null)
            putArea = transform;

        if (promptAnchor == null)
            promptAnchor = putArea;

        if (areaCollider == null && putArea != null)
            areaCollider = putArea.GetComponent<Collider>();

        if (newspaperSourceVisual == null)
        {
            NewspaperStandController stand =
                UnityEngine.Object.FindAnyObjectByType<NewspaperStandController>(
                    FindObjectsInactive.Include
                );

            if (stand != null)
                newspaperSourceVisual = stand.newspaperVisual;
        }

        if (placementGuideVisual == null && putArea != null)
            placementGuideVisual = putArea.gameObject;

        CacheGuideRenderers();
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
        if (promptVisual == null)
        {
            NewspaperWorldPromptVisual[] prompts =
                GetComponentsInChildren<NewspaperWorldPromptVisual>(true);

            for (int i = 0; i < prompts.Length; i++)
            {
                NewspaperWorldPromptVisual candidate = prompts[i];
                if (candidate != null && candidate.name == "Newspaper_PlacePrompt")
                {
                    promptVisual = candidate;
                    break;
                }
            }
        }

        if (promptVisual == null && Application.isPlaying)
        {
            Transform anchor = promptAnchor != null ? promptAnchor : transform;
            promptVisual = NewspaperWorldPromptVisual.Create(
                anchor,
                "Newspaper_PlacePrompt",
                promptLocalOffset,
                promptWorldScale,
                261
            );
            promptVisual.rotationDegreesPerSecond = promptRotationSpeed;
        }

        ConfigurePlacementPrompt();
    }

    private void ConfigurePlacementPrompt()
    {
        if (promptVisual == null)
            return;

        promptVisual.faceCamera = promptFaceCamera;
        promptVisual.keepBillboardUpright = true;
        promptVisual.useRootTransformAsSource = true;
        promptVisual.useCircularPromptTransformAsSource = true;

        if (!Application.isPlaying)
            promptVisual.SetVisible(true);
    }

    private void SetPromptVisible(bool visible)
    {
        if (promptVisual != null)
            promptVisual.SetVisible(visible);
    }

    private void EnsureAreaVisual()
    {
        if (!Application.isPlaying || borderLine != null)
            return;

        borderLine = GetOrCreateLine("NewspaperPutArea_Border", 5);
        diagonalA = GetOrCreateLine("NewspaperPutArea_DiagonalA", 2);
        diagonalB = GetOrCreateLine("NewspaperPutArea_DiagonalB", 2);

        ConfigureLine(borderLine);
        ConfigureLine(diagonalA);
        ConfigureLine(diagonalB);
        UpdateAreaPositions();
    }

    private LineRenderer GetOrCreateLine(string objectName, int points)
    {
        Transform existing = transform.Find(objectName);
        if (existing != null)
        {
            LineRenderer existingLine = existing.GetComponent<LineRenderer>();
            if (existingLine != null)
            {
                existingLine.positionCount = points;
                return existingLine;
            }
        }

        GameObject lineObject = new GameObject(objectName);
        lineObject.transform.SetParent(transform, false);
        LineRenderer created = lineObject.AddComponent<LineRenderer>();
        created.positionCount = points;
        return created;
    }

    private void ConfigureLine(LineRenderer line)
    {
        if (line == null)
            return;

        line.useWorldSpace = true;
        line.loop = false;
        line.startWidth = lineWidth;
        line.endWidth = lineWidth;
        line.numCornerVertices = 4;
        line.numCapVertices = 4;
        line.shadowCastingMode = ShadowCastingMode.Off;
        line.receiveShadows = false;
        line.sharedMaterial = GetLineMaterial();
    }

    private Material GetLineMaterial()
    {
        if (lineMaterial != null)
            return lineMaterial;

        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Sprites/Default");
        if (shader == null) shader = Shader.Find("Unlit/Color");

        if (shader == null)
            return null;

        lineMaterial = new Material(shader)
        {
            name = "NewspaperPutArea_RuntimeMaterial",
            hideFlags = HideFlags.HideAndDontSave
        };
        return lineMaterial;
    }

    private void UpdateAreaPositions()
    {
        if (borderLine == null || diagonalA == null || diagonalB == null)
            return;

        Vector3 center;
        Vector3 right;
        Vector3 forward;

        if (areaCollider != null)
        {
            Bounds bounds = areaCollider.bounds;
            center = new Vector3(bounds.center.x, bounds.max.y + areaHeightOffset, bounds.center.z);
            right = Vector3.right * Mathf.Max(0.05f, bounds.extents.x);
            forward = Vector3.forward * Mathf.Max(0.05f, bounds.extents.z);
        }
        else
        {
            Transform reference = putArea != null ? putArea : transform;
            center = reference.position + reference.up * areaHeightOffset;
            right = reference.right * Mathf.Max(0.05f, areaSize.x * 0.5f);
            forward = reference.forward * Mathf.Max(0.05f, areaSize.y * 0.5f);
        }

        Vector3 p0 = center - right - forward;
        Vector3 p1 = center - right + forward;
        Vector3 p2 = center + right + forward;
        Vector3 p3 = center + right - forward;

        borderLine.positionCount = 5;
        borderLine.SetPosition(0, p0);
        borderLine.SetPosition(1, p1);
        borderLine.SetPosition(2, p2);
        borderLine.SetPosition(3, p3);
        borderLine.SetPosition(4, p0);

        diagonalA.positionCount = 2;
        diagonalA.SetPosition(0, p0);
        diagonalA.SetPosition(1, p2);

        diagonalB.positionCount = 2;
        diagonalB.SetPosition(0, p1);
        diagonalB.SetPosition(1, p3);
    }

    private void SetAreaColor(Color color)
    {
        ApplyLineColor(borderLine, color);
        ApplyLineColor(diagonalA, color);
        ApplyLineColor(diagonalB, color);
    }

    private static void ApplyLineColor(LineRenderer line, Color color)
    {
        if (line == null)
            return;

        line.startColor = color;
        line.endColor = color;
    }

    private void SetAreaVisualVisible(bool visible)
    {
        if (borderLine != null && borderLine.gameObject.activeSelf != visible)
            borderLine.gameObject.SetActive(visible);

        bool diagonalsVisible = visible && showCentralX;
        if (diagonalA != null && diagonalA.gameObject.activeSelf != diagonalsVisible)
            diagonalA.gameObject.SetActive(diagonalsVisible);
        if (diagonalB != null && diagonalB.gameObject.activeSelf != diagonalsVisible)
            diagonalB.gameObject.SetActive(diagonalsVisible);
    }

    private void CacheGuideRenderers()
    {
        if (guideRenderersCached)
            return;

        guideRenderersCached = true;
        guideRenderers = placementGuideVisual != null
            ? placementGuideVisual.GetComponentsInChildren<Renderer>(true)
            : System.Array.Empty<Renderer>();
    }

    private void SetPlacementGuideVisible(bool visible)
    {
        CacheGuideRenderers();

        if (guideVisibilityInitialized && lastGuideVisible == visible)
            return;

        guideVisibilityInitialized = true;
        lastGuideVisible = visible;

        for (int i = 0; i < guideRenderers.Length; i++)
        {
            Renderer renderer = guideRenderers[i];
            if (renderer == null)
                continue;

            if (placedNewspaperVisual != null &&
                renderer.transform.IsChildOf(placedNewspaperVisual.transform))
            {
                continue;
            }

            renderer.enabled = visible;
        }
    }

    private bool IsPlayerWithin(float distance)
    {
        if (player == null)
            return false;

        Vector3 point = putArea != null ? putArea.position : transform.position;
        float maxDistance = Mathf.Max(0.1f, distance);
        return (player.position - point).sqrMagnitude <= maxDistance * maxDistance;
    }

    private static Transform FindChildByPlacedName(Transform root)
    {
        if (root == null)
            return null;

        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);
            if (child.name.StartsWith(PersistentPlacedVisualName, System.StringComparison.Ordinal))
                return child;

            Transform nested = FindChildByPlacedName(child);
            if (nested != null)
                return nested;
        }

        return null;
    }

    private void OnValidate()
    {
        interactionRadius = Mathf.Max(0.25f, interactionRadius);
        areaSize.x = Mathf.Max(0.1f, areaSize.x);
        areaSize.y = Mathf.Max(0.1f, areaSize.y);
        lineWidth = Mathf.Max(0.005f, lineWidth);
        promptWorldScale = Mathf.Max(0.0005f, promptWorldScale);

        if (putArea == null)
            putArea = transform;
        if (promptAnchor == null)
            promptAnchor = putArea;
        if (areaCollider == null)
            areaCollider = GetComponent<Collider>();

        guideRenderersCached = false;
        guideVisibilityInitialized = false;
        stateVisualInitialized = false;

        if (!Application.isPlaying)
        {
            ResolvePersistentPlacedVisualReference();
            if (placedNewspaperVisual != null && keepPlacedVisualInScene)
                placedNewspaperVisual.SetActive(previewPlacedVisualInEditMode);

            ConfigurePlacementPrompt();
        }
    }
}
