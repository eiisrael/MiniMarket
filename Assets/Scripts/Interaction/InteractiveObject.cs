using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Marca qualquer objeto como interativo.
/// Serve para portas, caixas, balcões, interruptores e outros objetos acionados pelo mouse.
/// A ação concreta permanece configurável por UnityEvent, sem acoplar este script à porta.
/// </summary>
[DisallowMultipleComponent]
public sealed class InteractiveObject : MonoBehaviour
{
    [Header("Identificação")]
    public string displayName = "Objeto interativo";
    public bool canInteract = true;

    [Header("Realce")]
    public InteractionHighlight highlight;
    public bool createHighlightWhenMissing = true;

    [Header("Eventos")]
    public UnityEvent onFocused;
    public UnityEvent onUnfocused;
    public UnityEvent onInteract;

    public bool IsFocused { get; private set; }

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
        SetFocused(false);
    }

    public void SetFocused(bool focused)
    {
        if (IsFocused == focused)
            return;

        IsFocused = focused;
        ResolveHighlight();

        if (highlight != null)
            highlight.SetFocused(focused);

        if (focused)
            onFocused?.Invoke();
        else
            onUnfocused?.Invoke();
    }

    public bool Interact()
    {
        if (!canInteract || !isActiveAndEnabled)
            return false;

        onInteract?.Invoke();
        return true;
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
        if (highlight == null)
            highlight = GetComponent<InteractionHighlight>();
    }
}
