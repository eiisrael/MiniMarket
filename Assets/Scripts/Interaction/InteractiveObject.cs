using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Marca portas, caixas, interruptores e outros objetos como interativos.
///
/// A ação pode ser ligada pelo UnityEvent. Para objetos antigos, o componente também
/// procura uma função pública comum no próprio objeto, nos pais e nos filhos.
/// Isso permite que o collider, o InteractiveObject e o script real da porta fiquem
/// em pontos diferentes da mesma hierarquia.
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

    [Header("Compatibilidade com scripts existentes")]
    public bool autoInvokeCommonMethod = true;
    public bool searchMethodInParents = true;
    public bool searchMethodInChildren = true;
    public string[] commonMethodNames =
    {
        "Interact",
        "Interagir",
        "Toggle",
        "Alternar",
        "ToggleDoor",
        "OpenDoor",
        "AbrirPorta",
        "AbrirFechar",
        "AlternarPorta",
        "Open",
        "Abrir",
        "Use",
        "Usar",
        "Activate",
        "Ativar"
    };

    [Header("Eventos")]
    public UnityEvent onFocused;
    public UnityEvent onUnfocused;
    public UnityEvent onInteract;

    [Header("Debug")]
    public bool logInvokedMethod;

    private MonoBehaviour cachedMethodTarget;
    private MethodInfo cachedMethod;
    private bool methodResolved;

    public bool IsFocused { get; private set; }

    private void Awake()
    {
        ResolveHighlight();
        ResolveCommonMethod();
    }

    private void OnEnable()
    {
        ResolveHighlight();
        RefreshCommonMethod();
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

        if (autoInvokeCommonMethod)
            InvokeCommonMethod();

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

    private void ResolveCommonMethod()
    {
        if (methodResolved)
            return;

        methodResolved = true;
        cachedMethodTarget = null;
        cachedMethod = null;

        if (!autoInvokeCommonMethod || commonMethodNames == null || commonMethodNames.Length == 0)
            return;

        if (TryResolveFromBehaviours(GetComponents<MonoBehaviour>()))
            return;

        if (searchMethodInParents && TryResolveFromBehaviours(GetComponentsInParent<MonoBehaviour>(true)))
            return;

        if (searchMethodInChildren)
            TryResolveFromBehaviours(GetComponentsInChildren<MonoBehaviour>(true));
    }

    private bool TryResolveFromBehaviours(MonoBehaviour[] behaviours)
    {
        if (behaviours == null || behaviours.Length == 0)
            return false;

        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public;

        for (int i = 0; i < commonMethodNames.Length; i++)
        {
            string methodName = commonMethodNames[i];
            if (string.IsNullOrWhiteSpace(methodName))
                continue;

            for (int b = 0; b < behaviours.Length; b++)
            {
                MonoBehaviour behaviour = behaviours[b];
                if (behaviour == null || behaviour == this || behaviour is InteractionHighlight)
                    continue;

                MethodInfo method = behaviour.GetType().GetMethod(
                    methodName,
                    flags,
                    null,
                    Type.EmptyTypes,
                    null
                );

                if (method == null || method.ReturnType != typeof(void))
                    continue;

                cachedMethodTarget = behaviour;
                cachedMethod = method;
                return true;
            }
        }

        return false;
    }

    private void InvokeCommonMethod()
    {
        ResolveCommonMethod();

        if (cachedMethodTarget == null || cachedMethod == null)
            return;

        try
        {
            cachedMethod.Invoke(cachedMethodTarget, null);

            if (logInvokedMethod)
            {
                Debug.Log(
                    "[InteractiveObject] Chamou " + cachedMethodTarget.GetType().Name +
                    "." + cachedMethod.Name + " em " + name + ".",
                    this
                );
            }
        }
        catch (Exception exception)
        {
            Debug.LogWarning(
                "[InteractiveObject] Falha ao executar " + cachedMethod.Name +
                " em " + name + ": " + exception.GetBaseException().Message,
                this
            );
        }
    }

    [ContextMenu("Interação/Rebuscar método compatível")]
    public void RefreshCommonMethod()
    {
        methodResolved = false;
        ResolveCommonMethod();
    }

    private void OnValidate()
    {
        methodResolved = false;

        if (highlight == null)
            highlight = GetComponent<InteractionHighlight>();
    }
}
