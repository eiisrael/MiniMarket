using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Área onde o jogador coloca um jornal coletado.
/// O jornal colocado pode existir permanentemente na cena e ser editado pelo
/// Inspector. Durante o jogo ele é apenas ativado/desativado, nunca destruído.
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

    [Tooltip("Jornal persistente colocado dentro da Put_Area. Ele permanece na cena e pode ser editado livremente.")]
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
    [Tooltip("Mantém Placed_Newspaper_Runtime e todos os seus filhos salvos na cena, inclusive ao sair do Play Mode.")]
    public bool keepPlacedVisualInScene = true;

    [Tooltip("Marcado: posição, rotação e escala são controladas diretamente pelo Transform do Placed_Newspaper_Runtime.")]
    public bool usePlacedVisualTransformAsSource = true;

    [Tooltip("Mostra o jornal colocado fora do Play Mode para facilitar a edição no Inspector.")]
    public bool previewPlacedVisualInEditMode = true;

    [Header("Transform inicial do jornal")]
    [Tooltip("Usado somente ao criar o objeto persistente pela primeira vez ou quando Use Placed Visual Transform As Source está desmarcado.")]
    public Vector3 placedLocalPosition = Vector3.zero;

    [Tooltip("Usado somente ao criar o objeto persistente pela primeira vez ou quando Use Placed Visual Transform As Source está desmarcado.")]
    public Vector3 placedLocalEuler = Vector3.zero;

    [Tooltip("Usado somente ao criar o objeto persistente pela primeira vez ou quando Use Placed Visual Transform As Source está desmarcado.")]
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

    [Header("Debug")]
    public bool logEvents;

    private MiniMarketNewspaperInventoryService inventory;
    private Transform player;
    private float nextPlayerResolve;
    private bool occupied;

    private LineRenderer borderLine;
    private LineRenderer diagonalA;
    private LineRenderer diagonalB;
    private Material lineMaterial;

    public bool IsOccupied => occupied;
    public bool CanPlace => !occupied && inventory != null && inventory.PossuiJornal;

    private void Awake()
    {
        NormalizeSerializedReferences();
        ResolveReferences();
        ResolvePersistentPlacedVisual();
        EnsureAreaVisual();
        EnsurePrompt();
        RestorePlacedState();
    }

    private void OnEnable()
    {
        NormalizeSerializedReferences();
        ResolveReferences();
        ResolvePersistentPlacedVisual();
        EnsureAreaVisual();
        EnsurePrompt();

        if (Application.isPlaying)
            RestorePlacedState();
        else
            ApplyEditModePreview();
    }

    private void Update()
    {
        ResolveReferences();
        ResolvePersistentPlacedVisual();
        EnsureAreaVisual();
        EnsurePrompt();
        RefreshState();
    }

    private void OnDisable()
    {
        if (!Application.isPlaying)
            return;

        SetAreaVisualVisible(false);
        SetPlacementGuideVisible(false);

        if (promptVisual != null)
            promptVisual.SetVisible(false);
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

        SetAreaVisualVisible(shouldOffer);
        SetPlacementGuideVisible(shouldOffer && showPlacementGuide);
        SetAreaColor(near ? nearbyAreaColor : availableAreaColor);

        if (promptVisual != null)
        {
            promptVisual.SetVisible(near);
            promptVisual.SetInteractionPrompt(
                "E",
                promptInstruction,
                0f,
                false,
                nearbyAreaColor
            );
        }

        if (!near || GameplayInputState.IsBlocked)
            return;

        if (Input.GetKeyDown(interactionKey))
            TryPlaceNewspaper();
    }

    public bool TryPlaceNewspaper()
    {
        ResolveReferences();
        ResolvePersistentPlacedVisual();

        if (occupied || inventory == null || !inventory.TentarConsumirJornal(1))
            return false;

        occupied = true;
        ShowPlacedNewspaper();

        if (persistPlacedState)
            inventory.DefinirJornalNoLocal(placeId, true);

        SetAreaVisualVisible(false);
        SetPlacementGuideVisible(false);

        if (promptVisual != null)
            promptVisual.SetVisible(false);

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
        ResolvePersistentPlacedVisual();

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
            }
        }

        ResolveReferences();
        if (persistPlacedState && inventory != null)
            inventory.DefinirJornalNoLocal(placeId, false);
    }

    private void RestorePlacedState()
    {
        ResolveReferences();
        ResolvePersistentPlacedVisual();

        occupied = persistPlacedState && inventory != null && inventory.LocalPossuiJornal(placeId);

        if (occupied)
        {
            ShowPlacedNewspaper();
            SetPlacementGuideVisible(false);
        }
        else if (placedNewspaperVisual != null)
        {
            placedNewspaperVisual.SetActive(!Application.isPlaying && previewPlacedVisualInEditMode);
        }
    }

    private void ApplyEditModePreview()
    {
        ResolvePersistentPlacedVisual();

        if (placedNewspaperVisual != null && keepPlacedVisualInScene)
            placedNewspaperVisual.SetActive(previewPlacedVisualInEditMode);

        if (promptVisual != null)
            promptVisual.SetVisible(true);
    }

    private void NormalizeSerializedReferences()
    {
        if (putArea == null)
            putArea = transform;

        if (placedNewspaperVisual != null && !IsPlacedVisual(placedNewspaperVisual))
        {
            if (placementGuideVisual == null)
                placementGuideVisual = placedNewspaperVisual;

            placedNewspaperVisual = null;
        }

        if (placementGuideVisual == null && putArea != null)
            placementGuideVisual = putArea.gameObject;
    }

    private void ResolvePersistentPlacedVisual()
    {
        if (placedNewspaperVisual != null && IsPlacedVisual(placedNewspaperVisual))
        {
            EnsurePersistentMarker(placedNewspaperVisual);
            SyncCompatibilityTransformFields();
            return;
        }

        Transform root = putArea != null ? putArea : transform;
        Transform existing = FindChildByPlacedName(root);

        if (existing == null)
            return;

        placedNewspaperVisual = existing.gameObject;
        EnsurePersistentMarker(placedNewspaperVisual);
        SanitizePlacedVisual(placedNewspaperVisual);
        SyncCompatibilityTransformFields();
    }

    private void ShowPlacedNewspaper()
    {
        ResolvePersistentPlacedVisual();

        if (placedNewspaperVisual == null)
            CreatePlacedNewspaperFallback();

        if (placedNewspaperVisual == null)
            return;

        if (!usePlacedVisualTransformAsSource)
            ApplyPlacedTransform(placedNewspaperVisual.transform, false);

        SanitizePlacedVisual(placedNewspaperVisual);
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
        EnsurePersistentMarker(placedNewspaperVisual);
        SanitizePlacedVisual(placedNewspaperVisual);
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

    private static void EnsurePersistentMarker(GameObject target)
    {
        if (target == null || target.GetComponent<PersistentPlacedNewspaperVisual>() != null)
            return;

        target.AddComponent<PersistentPlacedNewspaperVisual>();
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

    private static void SanitizePlacedVisual(GameObject target)
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
            if (controller != null && controller.gameObject != target)
                controller.enabled = false;
        }

        NewspaperWorldPromptVisual[] prompts =
            target.GetComponentsInChildren<NewspaperWorldPromptVisual>(true);
        for (int i = 0; i < prompts.Length; i++)
        {
            if (prompts[i] == null)
                continue;
            prompts[i].SetVisible(false);
            prompts[i].enabled = false;
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
            if (grabbables[i] == null)
                continue;
            grabbables[i].canBeGrabbed = false;
            grabbables[i].enabled = false;
            grabbables[i].SetSelected(false);
        }

        InteractionHighlight[] highlights =
            target.GetComponentsInChildren<InteractionHighlight>(true);
        for (int i = 0; i < highlights.Length; i++)
        {
            if (highlights[i] == null)
                continue;
            highlights[i].Clear();
            highlights[i].enabled = false;
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

    private void ResolveReferences()
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

        if (inventory == null)
            inventory = MiniMarketNewspaperInventoryService.Instance;

        if (inventory == null && Application.isPlaying)
        {
            inventory = UnityEngine.Object.FindAnyObjectByType<MiniMarketNewspaperInventoryService>(
                FindObjectsInactive.Include
            );
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

        if (promptVisual != null && !Application.isPlaying)
            promptVisual.SetVisible(true);
    }

    private void EnsureAreaVisual()
    {
        if (!Application.isPlaying)
            return;

        borderLine = GetOrCreateLine("NewspaperPutArea_Border", borderLine, 5);
        diagonalA = GetOrCreateLine("NewspaperPutArea_DiagonalA", diagonalA, 2);
        diagonalB = GetOrCreateLine("NewspaperPutArea_DiagonalB", diagonalB, 2);

        ConfigureLine(borderLine);
        ConfigureLine(diagonalA);
        ConfigureLine(diagonalB);
        UpdateAreaPositions();
    }

    private LineRenderer GetOrCreateLine(string objectName, LineRenderer current, int points)
    {
        if (current != null)
            return current;

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
        if (borderLine != null)
            borderLine.gameObject.SetActive(visible);
        if (diagonalA != null)
            diagonalA.gameObject.SetActive(visible && showCentralX);
        if (diagonalB != null)
            diagonalB.gameObject.SetActive(visible && showCentralX);
    }

    private void SetPlacementGuideVisible(bool visible)
    {
        if (placementGuideVisual == null)
            return;

        Renderer[] renderers = placementGuideVisual.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
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

    private void OnValidate()
    {
        interactionRadius = Mathf.Max(0.25f, interactionRadius);
        areaSize.x = Mathf.Max(0.1f, areaSize.x);
        areaSize.y = Mathf.Max(0.1f, areaSize.y);
        lineWidth = Mathf.Max(0.005f, lineWidth);
        promptWorldScale = Mathf.Max(0.0005f, promptWorldScale);

        NormalizeSerializedReferences();
        ResolvePersistentPlacedVisual();

        if (putArea == null)
            putArea = transform;
        if (areaCollider == null)
            areaCollider = GetComponent<Collider>();

        if (!Application.isPlaying && placedNewspaperVisual != null && keepPlacedVisualInScene)
            placedNewspaperVisual.SetActive(previewPlacedVisualInEditMode);
    }
}
