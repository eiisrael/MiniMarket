using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Marca portas, interruptores e outros objetos como interativos.
///
/// A ação principal pode ser ligada pelo UnityEvent. Para componentes legados,
/// a compatibilidade é resolvida uma única vez e fica em cache: primeiro procura
/// um método público conhecido; quando não existe, alterna um campo/propriedade
/// booleana comum, como o campo Open usado pelas portas antigas do projeto.
/// Nenhuma reflexão acontece no Update.
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

    [Tooltip("Quando nenhum método compatível existe, alterna um campo ou propriedade bool conhecido no script legado.")]
    public bool toggleCommonBooleanWhenNoMethod = true;

    public string[] commonBooleanNames =
    {
        "Open",
        "open",
        "IsOpen",
        "isOpen",
        "Opened",
        "opened",
        "Aberta",
        "aberta",
        "EstaAberta",
        "estaAberta"
    };

    [Tooltip("Último fallback: alterna um parâmetro bool conhecido no Animator da mesma hierarquia.")]
    public bool toggleAnimatorBooleanWhenNoMember = true;

    public string[] commonAnimatorBooleanNames =
    {
        "Open",
        "open",
        "IsOpen",
        "Aberta"
    };

    [Header("Eventos")]
    public UnityEvent onFocused;
    public UnityEvent onUnfocused;
    public UnityEvent onInteract;

    [Header("Debug")]
    public bool logInvokedMethod;

    private MonoBehaviour cachedActionTarget;
    private MethodInfo cachedMethod;
    private FieldInfo cachedBooleanField;
    private PropertyInfo cachedBooleanProperty;
    private Animator cachedAnimator;
    private int cachedAnimatorBoolHash;
    private string cachedActionDescription;
    private bool actionResolved;

    public bool IsFocused { get; private set; }

    private void Awake()
    {
        ResolveHighlight();
        ResolveCommonAction();
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
            InvokeCommonAction();

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

    private void ResolveCommonAction()
    {
        if (actionResolved)
            return;

        actionResolved = true;
        ClearCachedAction();

        if (!autoInvokeCommonMethod)
            return;

        MonoBehaviour[] ownBehaviours = GetComponents<MonoBehaviour>();
        MonoBehaviour[] parentBehaviours = searchMethodInParents
            ? GetComponentsInParent<MonoBehaviour>(true)
            : null;
        MonoBehaviour[] childBehaviours = searchMethodInChildren
            ? GetComponentsInChildren<MonoBehaviour>(true)
            : null;

        if (TryResolveMethodFromBehaviours(ownBehaviours) ||
            TryResolveMethodFromBehaviours(parentBehaviours) ||
            TryResolveMethodFromBehaviours(childBehaviours))
        {
            return;
        }

        if (toggleCommonBooleanWhenNoMethod &&
            (TryResolveBooleanFromBehaviours(ownBehaviours) ||
             TryResolveBooleanFromBehaviours(parentBehaviours) ||
             TryResolveBooleanFromBehaviours(childBehaviours)))
        {
            return;
        }

        if (toggleAnimatorBooleanWhenNoMember)
            TryResolveAnimatorBoolean();
    }

    private bool TryResolveMethodFromBehaviours(MonoBehaviour[] behaviours)
    {
        if (behaviours == null || behaviours.Length == 0 ||
            commonMethodNames == null || commonMethodNames.Length == 0)
        {
            return false;
        }

        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public;

        for (int nameIndex = 0; nameIndex < commonMethodNames.Length; nameIndex++)
        {
            string methodName = commonMethodNames[nameIndex];
            if (string.IsNullOrWhiteSpace(methodName))
                continue;

            for (int behaviourIndex = 0; behaviourIndex < behaviours.Length; behaviourIndex++)
            {
                MonoBehaviour behaviour = behaviours[behaviourIndex];
                if (!IsValidLegacyTarget(behaviour))
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

                cachedActionTarget = behaviour;
                cachedMethod = method;
                cachedActionDescription = behaviour.GetType().Name + "." + method.Name + "()";
                return true;
            }
        }

        return false;
    }

    private bool TryResolveBooleanFromBehaviours(MonoBehaviour[] behaviours)
    {
        if (behaviours == null || behaviours.Length == 0 ||
            commonBooleanNames == null || commonBooleanNames.Length == 0)
        {
            return false;
        }

        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public;

        for (int behaviourIndex = 0; behaviourIndex < behaviours.Length; behaviourIndex++)
        {
            MonoBehaviour behaviour = behaviours[behaviourIndex];
            if (!IsValidLegacyTarget(behaviour))
                continue;

            Type type = behaviour.GetType();

            for (int nameIndex = 0; nameIndex < commonBooleanNames.Length; nameIndex++)
            {
                string memberName = commonBooleanNames[nameIndex];
                if (string.IsNullOrWhiteSpace(memberName))
                    continue;

                FieldInfo field = type.GetField(memberName, flags);
                if (field != null && field.FieldType == typeof(bool) && !field.IsInitOnly)
                {
                    cachedActionTarget = behaviour;
                    cachedBooleanField = field;
                    cachedActionDescription = type.Name + "." + field.Name;
                    return true;
                }

                PropertyInfo property = type.GetProperty(memberName, flags);
                if (property != null &&
                    property.PropertyType == typeof(bool) &&
                    property.CanRead &&
                    property.CanWrite &&
                    property.GetIndexParameters().Length == 0)
                {
                    cachedActionTarget = behaviour;
                    cachedBooleanProperty = property;
                    cachedActionDescription = type.Name + "." + property.Name;
                    return true;
                }
            }
        }

        return false;
    }

    private void TryResolveAnimatorBoolean()
    {
        Animator[] animators;

        if (searchMethodInChildren)
            animators = GetComponentsInChildren<Animator>(true);
        else
        {
            Animator animator = GetComponent<Animator>();
            animators = animator != null ? new[] { animator } : Array.Empty<Animator>();
        }

        if ((animators == null || animators.Length == 0) && searchMethodInParents)
        {
            Animator parentAnimator = GetComponentInParent<Animator>(true);
            if (parentAnimator != null)
                animators = new[] { parentAnimator };
        }

        if (animators == null || commonAnimatorBooleanNames == null)
            return;

        for (int animatorIndex = 0; animatorIndex < animators.Length; animatorIndex++)
        {
            Animator animator = animators[animatorIndex];
            if (animator == null)
                continue;

            AnimatorControllerParameter[] parameters = animator.parameters;
            for (int nameIndex = 0; nameIndex < commonAnimatorBooleanNames.Length; nameIndex++)
            {
                string parameterName = commonAnimatorBooleanNames[nameIndex];
                if (string.IsNullOrWhiteSpace(parameterName))
                    continue;

                int hash = Animator.StringToHash(parameterName);
                for (int parameterIndex = 0; parameterIndex < parameters.Length; parameterIndex++)
                {
                    AnimatorControllerParameter parameter = parameters[parameterIndex];
                    if (parameter.type != AnimatorControllerParameterType.Bool ||
                        parameter.nameHash != hash)
                    {
                        continue;
                    }

                    cachedAnimator = animator;
                    cachedAnimatorBoolHash = hash;
                    cachedActionDescription = animator.name + ".Animator[" + parameterName + "]";
                    return;
                }
            }
        }
    }

    private bool IsValidLegacyTarget(MonoBehaviour behaviour)
    {
        return behaviour != null &&
               behaviour != this &&
               !(behaviour is InteractionHighlight) &&
               !(behaviour is InteractionFocusController) &&
               behaviour.isActiveAndEnabled;
    }

    private void InvokeCommonAction()
    {
        ResolveCommonAction();

        try
        {
            if (cachedActionTarget != null && cachedMethod != null)
            {
                cachedMethod.Invoke(cachedActionTarget, null);
                LogResolvedAction();
                return;
            }

            if (cachedActionTarget != null && cachedBooleanField != null)
            {
                bool current = (bool)cachedBooleanField.GetValue(cachedActionTarget);
                cachedBooleanField.SetValue(cachedActionTarget, !current);
                LogResolvedAction();
                return;
            }

            if (cachedActionTarget != null && cachedBooleanProperty != null)
            {
                bool current = (bool)cachedBooleanProperty.GetValue(cachedActionTarget, null);
                cachedBooleanProperty.SetValue(cachedActionTarget, !current, null);
                LogResolvedAction();
                return;
            }

            if (cachedAnimator != null && cachedAnimatorBoolHash != 0)
            {
                bool current = cachedAnimator.GetBool(cachedAnimatorBoolHash);
                cachedAnimator.SetBool(cachedAnimatorBoolHash, !current);
                LogResolvedAction();
            }
        }
        catch (Exception exception)
        {
            Debug.LogWarning(
                "[InteractiveObject] Falha ao executar " +
                (string.IsNullOrEmpty(cachedActionDescription)
                    ? "ação compatível"
                    : cachedActionDescription) +
                " em " + name + ": " + exception.GetBaseException().Message,
                this
            );

            RefreshCommonMethod();
        }
    }

    private void LogResolvedAction()
    {
        if (!logInvokedMethod)
            return;

        Debug.Log(
            "[InteractiveObject] Executou " + cachedActionDescription + " em " + name + ".",
            this
        );
    }

    private void ClearCachedAction()
    {
        cachedActionTarget = null;
        cachedMethod = null;
        cachedBooleanField = null;
        cachedBooleanProperty = null;
        cachedAnimator = null;
        cachedAnimatorBoolHash = 0;
        cachedActionDescription = string.Empty;
    }

    [ContextMenu("Interação/Rebuscar ação compatível")]
    public void RefreshCommonMethod()
    {
        actionResolved = false;
        ResolveCommonAction();
    }

    private void OnValidate()
    {
        actionResolved = false;
        ClearCachedAction();

        if (highlight == null)
            highlight = GetComponent<InteractionHighlight>();
    }
}
