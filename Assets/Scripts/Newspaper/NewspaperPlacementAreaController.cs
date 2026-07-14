using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Área onde o jogador coloca um jornal coletado.
/// Só aparece quando existe ao menos um jornal no banco e o local está vazio.
/// </summary>
[DisallowMultipleComponent]
public sealed class NewspaperPlacementAreaController : MonoBehaviour
{
    [Header("Identificação")]
    public string placeId = "BRONZE_MARKET_NEWSPAPER_PLACE";

    [Header("Referências")]
    public Transform putArea;

    [Tooltip("Jornal real usado como fonte. Deve apontar para Newspaper_Stand/Jornal.")]
    public GameObject newspaperSourceVisual;

    [Tooltip("Referência runtime do jornal realmente colocado. Não use como guia visual.")]
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

    [Header("Posição do jornal")]
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
        EnsureAreaVisual();
        EnsurePrompt();
        RestorePlacedState();
    }

    private void OnEnable()
    {
        NormalizeSerializedReferences();
        ResolveReferences();
        EnsureAreaVisual();
        EnsurePrompt();
        RestorePlacedState();
    }

    private void Update()
    {
        ResolveReferences();
        EnsureAreaVisual();
        EnsurePrompt();
        RefreshState();
    }

    private void OnDisable()
    {
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
            // O prompt da Put_Area continua aparecendo somente de perto.
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

        if (occupied || inventory == null || !inventory.TentarConsumirJornal(1))
            return false;

        occupied = true;
        CreatePlacedNewspaperVisual();

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

        if (placedNewspaperVisual != null)
        {
            if (Application.isPlaying)
                Destroy(placedNewspaperVisual);
            else
                DestroyImmediate(placedNewspaperVisual);
        }

        placedNewspaperVisual = null;

        ResolveReferences();
        if (persistPlacedState && inventory != null)
            inventory.DefinirJornalNoLocal(placeId, false);
    }

    private void RestorePlacedState()
    {
        ResolveReferences();
        occupied = persistPlacedState && inventory != null && inventory.LocalPossuiJornal(placeId);

        if (occupied)
        {
            CreatePlacedNewspaperVisual();
            SetPlacementGuideVisible(false);
        }
        else if (placedNewspaperVisual != null)
        {
            placedNewspaperVisual.SetActive(false);
        }
    }

    private void NormalizeSerializedReferences()
    {
        // Versões anteriores usavam placedNewspaperVisual como objeto demonstrativo.
        // Converte automaticamente essa referência para placementGuideVisual.
        if (placedNewspaperVisual != null && !IsRuntimePlacedVisual(placedNewspaperVisual))
        {
            if (placementGuideVisual == null)
                placementGuideVisual = placedNewspaperVisual;

            placedNewspaperVisual = null;
        }

        if (putArea == null)
            putArea = transform;

        if (placementGuideVisual == null && putArea != null)
            placementGuideVisual = putArea.gameObject;
    }

    private void CreatePlacedNewspaperVisual()
    {
        NormalizeSerializedReferences();
        ResolveReferences();

        if (placedNewspaperVisual != null && IsRuntimePlacedVisual(placedNewspaperVisual))
        {
            placedNewspaperVisual.SetActive(true);
            ApplyPlacedTransform(placedNewspaperVisual.transform);
            SanitizePlacedVisual(placedNewspaperVisual);
            return;
        }

        if (newspaperSourceVisual == null || putArea == null)
        {
            Debug.LogWarning(
                "[NewspaperPlace] Jornal real do Newspaper_Stand ou Put_Area não configurado.",
                this
            );
            return;
        }

        placedNewspaperVisual = Instantiate(newspaperSourceVisual, putArea);
        placedNewspaperVisual.name = "Placed_Newspaper_Runtime";
        placedNewspaperVisual.SetActive(true);
        ApplyPlacedTransform(placedNewspaperVisual.transform);
        SanitizePlacedVisual(placedNewspaperVisual);
    }

    private static bool IsRuntimePlacedVisual(GameObject target)
    {
        return target != null && target.name.StartsWith("Placed_Newspaper_Runtime");
    }

    private void ApplyPlacedTransform(Transform target)
    {
        if (target == null || putArea == null)
            return;

        target.SetParent(putArea, false);
        target.localPosition = placedLocalPosition;
        target.localRotation = useSourceLocalRotation && newspaperSourceVisual != null
            ? newspaperSourceVisual.transform.localRotation
            : Quaternion.Euler(placedLocalEuler);
        target.localScale = useSourceLocalScale && newspaperSourceVisual != null
            ? newspaperSourceVisual.transform.localScale
            : placedLocalScale;
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
            if (placementControllers[i] != null)
                placementControllers[i].enabled = false;
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

        GrabbableItem[] grabbables = target.GetComponentsInChildren<GrabbableItem>(true);
        for (int i = 0; i < grabbables.Length; i++)
        {
            if (grabbables[i] == null)
                continue;
            grabbables[i].canBeGrabbed = false;
            grabbables[i].enabled = false;
            grabbables[i].SetSelected(false);
        }

        InteractionHighlight[] highlights = target.GetComponentsInChildren<InteractionHighlight>(true);
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
        if (promptVisual != null || !Application.isPlaying)
            return;

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

        if (putArea == null)
            putArea = transform;
        if (areaCollider == null)
            areaCollider = GetComponent<Collider>();
    }
}
