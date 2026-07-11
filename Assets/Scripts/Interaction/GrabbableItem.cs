using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Define um objeto que pode ser selecionado e manipulado pelo GetItem.
/// Compartilha o mesmo realce visual usado por portas e outros objetos interativos.
/// </summary>
[DisallowMultipleComponent]
public sealed class GrabbableItem : MonoBehaviour
{
    [Header("Permissão")]
    public bool canBeGrabbed = true;
    [Min(0.01f)] public float maximumMass = 25f;
    [Min(0.1f)] public float maximumSize = 3.5f;

    [Header("Posição ao segurar")]
    [Range(0.25f, 2f)] public float holdDistanceMultiplier = 1f;
    public Vector3 localHoldOffset;

    [Header("Rotação")]
    public bool preserveRotation = true;
    public bool alignToCamera;
    public Vector3 cameraAlignmentEuler;

    [Header("Física")]
    [Range(0.1f, 3f)] public float springMultiplier = 1f;
    [Range(0.1f, 3f)] public float dampingMultiplier = 1f;
    [Range(0.1f, 3f)] public float rotationMultiplier = 1f;

    [Header("Realce")]
    public InteractionHighlight highlight;
    public bool createHighlightWhenMissing = true;

    [Header("Eventos")]
    public UnityEvent onSelected;
    public UnityEvent onDeselected;
    public UnityEvent onGrabbed;
    public UnityEvent onReleased;

    public bool IsSelected { get; private set; }
    public bool IsHeld { get; private set; }

    private void Awake()
    {
        ResolveHighlight();
    }

    private void OnEnable()
    {
        ResolveHighlight();
    }

    private void OnDisable()
    {
        IsSelected = false;
        IsHeld = false;

        if (highlight != null)
            highlight.Clear();
    }

    public void SetSelected(bool selected)
    {
        if (IsSelected == selected)
            return;

        IsSelected = selected;
        ResolveHighlight();

        if (highlight != null)
            highlight.SetFocused(selected);

        if (selected)
            onSelected?.Invoke();
        else
            onDeselected?.Invoke();
    }

    public void SetHeld(bool held)
    {
        if (IsHeld == held)
            return;

        IsHeld = held;
        ResolveHighlight();

        if (highlight != null)
        {
            highlight.SetFocused(false);
            highlight.SetActiveInteraction(held);
        }

        if (held)
            onGrabbed?.Invoke();
        else
            onReleased?.Invoke();
    }

    public bool Validate(Rigidbody body, Bounds bounds)
    {
        if (!canBeGrabbed || IsHeld)
            return false;

        if (body != null && body.mass > maximumMass)
            return false;

        float largestSize = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
        return largestSize <= maximumSize;
    }

    private void ResolveHighlight()
    {
        if (highlight != null)
            return;

        highlight = GetComponent<InteractionHighlight>();

        if (highlight == null && createHighlightWhenMissing && Application.isPlaying)
            highlight = gameObject.AddComponent<InteractionHighlight>();
    }

    private void OnValidate()
    {
        maximumMass = Mathf.Max(0.01f, maximumMass);
        maximumSize = Mathf.Max(0.1f, maximumSize);

        if (highlight == null)
            highlight = GetComponent<InteractionHighlight>();
    }
}
