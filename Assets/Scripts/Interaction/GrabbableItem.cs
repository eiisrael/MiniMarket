using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Define um objeto que pode ser selecionado e manipulado pelo GetItem.
/// Mantém as regras do item separadas da lógica de interação.
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

    [Header("Eventos")]
    public UnityEvent onSelected;
    public UnityEvent onDeselected;
    public UnityEvent onGrabbed;
    public UnityEvent onReleased;

    public bool IsSelected { get; private set; }
    public bool IsHeld { get; private set; }

    public void SetSelected(bool selected)
    {
        if (IsSelected == selected)
            return;

        IsSelected = selected;
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
}
