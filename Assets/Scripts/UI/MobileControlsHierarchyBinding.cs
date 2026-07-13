using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// Mantém o HUD mobile salvo e editável na hierarquia.
/// As referências privadas do MobileControlsHUD são ligadas uma única vez ao iniciar;
/// nenhum objeto visual precisa ser recriado ao apertar Play.
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
[DefaultExecutionOrder(-50000)]
public sealed class MobileControlsHierarchyBinding : MonoBehaviour
{
    [Header("Controlador")]
    public MobileControlsHUD mobileHud;

    [Header("Estrutura persistente")]
    public Canvas canvas;
    public RectTransform safeArea;
    public GameObject lookArea;
    public RectTransform joystickBase;
    public RectTransform joystickThumb;
    public RectTransform actionsRoot;

    [Header("Botões")]
    public Image aimButton;
    public Image jumpButton;
    public Image runButton;
    public Image grabButton;
    public Image interactButton;
    public Image throwButton;

    [Header("Editor")]
    public bool manterVisivelForaDoPlay = true;

    private static readonly BindingFlags PrivateInstance =
        BindingFlags.Instance | BindingFlags.NonPublic;

    private static FieldInfo canvasField;
    private static FieldInfo safeAreaField;
    private static FieldInfo joystickBaseField;
    private static FieldInfo joystickThumbField;
    private static FieldInfo runButtonImageField;
    private static FieldInfo grabButtonImageField;
    private static FieldInfo aimButtonImageField;
    private static FieldInfo builtField;
    private static FieldInfo runHeldField;

    private static MethodInfo onJoystickPointerMethod;
    private static MethodInfo onJoystickReleaseMethod;
    private static MethodInfo onLookPointerDownMethod;
    private static MethodInfo onLookDragMethod;
    private static MethodInfo onLookPointerUpMethod;
    private static MethodInfo requestJumpMethod;
    private static MethodInfo requestInteractMethod;
    private static MethodInfo setGrabHeldMethod;
    private static MethodInfo requestThrowMethod;
    private static MethodInfo setAimHeldMethod;
    private static MethodInfo setButtonPressedMethod;

    private bool eventosVinculados;

    private void Awake()
    {
        ResolverControlador();
        InjetarReferencias();
    }

    private void OnEnable()
    {
        ResolverControlador();
        InjetarReferencias();

        if (Application.isPlaying)
            VincularEventosRuntime();
        else if (canvas != null && manterVisivelForaDoPlay)
            canvas.gameObject.SetActive(true);
    }

    private void Start()
    {
        if (Application.isPlaying)
        {
            InjetarReferencias();
            VincularEventosRuntime();
        }
    }

    private void OnDisable()
    {
        if (Application.isPlaying)
            LimparEntradas();
    }

    [ContextMenu("Mobile HUD/Aplicar hierarquia persistente")]
    public void AplicarHierarquiaPersistente()
    {
        ResolverControlador();
        InjetarReferencias();

        if (!Application.isPlaying && canvas != null)
            canvas.gameObject.SetActive(manterVisivelForaDoPlay);
    }

    private void ResolverControlador()
    {
        if (mobileHud == null)
            mobileHud = GetComponent<MobileControlsHUD>();

        if (mobileHud == null)
            mobileHud = GetComponentInParent<MobileControlsHUD>();
    }

    private void InjetarReferencias()
    {
        if (mobileHud == null)
            return;

        GarantirReflection();
        Definir(canvasField, mobileHud, canvas);
        Definir(safeAreaField, mobileHud, safeArea);
        Definir(joystickBaseField, mobileHud, joystickBase);
        Definir(joystickThumbField, mobileHud, joystickThumb);
        Definir(runButtonImageField, mobileHud, runButton);
        Definir(grabButtonImageField, mobileHud, grabButton);
        Definir(aimButtonImageField, mobileHud, aimButton);
        Definir(builtField, mobileHud, EstruturaValida());
    }

    private bool EstruturaValida()
    {
        return canvas != null &&
               safeArea != null &&
               lookArea != null &&
               joystickBase != null &&
               joystickThumb != null &&
               actionsRoot != null &&
               aimButton != null &&
               jumpButton != null &&
               runButton != null &&
               grabButton != null &&
               interactButton != null &&
               throwButton != null;
    }

    private void VincularEventosRuntime()
    {
        if (eventosVinculados || mobileHud == null || !EstruturaValida())
            return;

        GarantirReflection();
        LimparEventosGerados();

        AdicionarEvento(
            joystickBase.gameObject,
            EventTriggerType.PointerDown,
            data => Invocar(onJoystickPointerMethod, (PointerEventData)data, true)
        );
        AdicionarEvento(
            joystickBase.gameObject,
            EventTriggerType.Drag,
            data => Invocar(onJoystickPointerMethod, (PointerEventData)data, false)
        );
        AdicionarEvento(
            joystickBase.gameObject,
            EventTriggerType.PointerUp,
            data => Invocar(onJoystickReleaseMethod, (PointerEventData)data)
        );
        AdicionarEvento(
            joystickBase.gameObject,
            EventTriggerType.PointerExit,
            data => Invocar(onJoystickReleaseMethod, (PointerEventData)data)
        );

        AdicionarEvento(
            lookArea,
            EventTriggerType.PointerDown,
            data => Invocar(onLookPointerDownMethod, (PointerEventData)data)
        );
        AdicionarEvento(
            lookArea,
            EventTriggerType.Drag,
            data => Invocar(onLookDragMethod, (PointerEventData)data)
        );
        AdicionarEvento(
            lookArea,
            EventTriggerType.PointerUp,
            data => Invocar(onLookPointerUpMethod, (PointerEventData)data)
        );
        AdicionarEvento(
            lookArea,
            EventTriggerType.PointerExit,
            data => Invocar(onLookPointerUpMethod, (PointerEventData)data)
        );

        AdicionarEvento(
            jumpButton.gameObject,
            EventTriggerType.PointerDown,
            data => Invocar(requestJumpMethod)
        );

        AdicionarEvento(
            interactButton.gameObject,
            EventTriggerType.PointerDown,
            data => Invocar(requestInteractMethod)
        );

        AdicionarHold(grabButton.gameObject, held =>
            Invocar(setGrabHeldMethod, held));

        AdicionarEvento(
            throwButton.gameObject,
            EventTriggerType.PointerDown,
            data => Invocar(requestThrowMethod)
        );

        AdicionarHold(aimButton.gameObject, held =>
            Invocar(setAimHeldMethod, held));

        AdicionarHold(runButton.gameObject, DefinirCorrida);
        eventosVinculados = true;
    }

    private void DefinirCorrida(bool pressionado)
    {
        if (mobileHud == null)
            return;

        Definir(runHeldField, mobileHud, pressionado);

        if (mobileHud.movimento != null)
            mobileHud.movimento.SetRunInput(pressionado);

        if (setButtonPressedMethod != null)
            Invocar(setButtonPressedMethod, runButton, pressionado);
        else if (runButton != null)
            runButton.color = pressionado
                ? mobileHud.corBotaoPressionado
                : mobileHud.corBotao;
    }

    private void LimparEntradas()
    {
        DefinirCorrida(false);

        if (setGrabHeldMethod != null)
            Invocar(setGrabHeldMethod, false);
        if (setAimHeldMethod != null)
            Invocar(setAimHeldMethod, false);

        if (mobileHud != null && mobileHud.movimento != null)
            mobileHud.movimento.SetMoveInput(Vector2.zero);

        if (joystickThumb != null)
            joystickThumb.anchoredPosition = Vector2.zero;
    }

    private void LimparEventosGerados()
    {
        LimparEvento(lookArea);
        LimparEvento(joystickBase != null ? joystickBase.gameObject : null);
        LimparEvento(aimButton != null ? aimButton.gameObject : null);
        LimparEvento(jumpButton != null ? jumpButton.gameObject : null);
        LimparEvento(runButton != null ? runButton.gameObject : null);
        LimparEvento(grabButton != null ? grabButton.gameObject : null);
        LimparEvento(interactButton != null ? interactButton.gameObject : null);
        LimparEvento(throwButton != null ? throwButton.gameObject : null);
    }

    private static void LimparEvento(GameObject target)
    {
        if (target == null)
            return;

        EventTrigger trigger = target.GetComponent<EventTrigger>();
        if (trigger != null)
            trigger.triggers = new List<EventTrigger.Entry>();
    }

    private void AdicionarHold(GameObject target, Action<bool> setter)
    {
        AdicionarEvento(target, EventTriggerType.PointerDown, data => setter(true));
        AdicionarEvento(target, EventTriggerType.PointerUp, data => setter(false));
        AdicionarEvento(target, EventTriggerType.PointerExit, data => setter(false));
    }

    private static void AdicionarEvento(
        GameObject target,
        EventTriggerType type,
        UnityAction<BaseEventData> callback)
    {
        if (target == null)
            return;

        EventTrigger trigger = target.GetComponent<EventTrigger>();
        if (trigger == null)
            trigger = target.AddComponent<EventTrigger>();
        if (trigger.triggers == null)
            trigger.triggers = new List<EventTrigger.Entry>();

        EventTrigger.Entry entry = new EventTrigger.Entry { eventID = type };
        entry.callback.AddListener(callback);
        trigger.triggers.Add(entry);
    }

    private void Invocar(MethodInfo method, params object[] args)
    {
        if (method == null || mobileHud == null)
            return;

        try
        {
            method.Invoke(mobileHud, args);
        }
        catch (TargetInvocationException exception)
        {
            Debug.LogException(exception.InnerException ?? exception, this);
        }
    }

    private static void GarantirReflection()
    {
        if (canvasField != null)
            return;

        Type type = typeof(MobileControlsHUD);

        canvasField = type.GetField("canvas", PrivateInstance);
        safeAreaField = type.GetField("safeAreaRect", PrivateInstance);
        joystickBaseField = type.GetField("joystickBase", PrivateInstance);
        joystickThumbField = type.GetField("joystickThumb", PrivateInstance);
        runButtonImageField = type.GetField("runButtonImage", PrivateInstance);
        grabButtonImageField = type.GetField("grabButtonImage", PrivateInstance);
        aimButtonImageField = type.GetField("aimButtonImage", PrivateInstance);
        builtField = type.GetField("built", PrivateInstance);
        runHeldField = type.GetField("runHeld", PrivateInstance);

        onJoystickPointerMethod = type.GetMethod("OnJoystickPointer", PrivateInstance);
        onJoystickReleaseMethod = type.GetMethod("OnJoystickRelease", PrivateInstance);
        onLookPointerDownMethod = type.GetMethod("OnLookPointerDown", PrivateInstance);
        onLookDragMethod = type.GetMethod("OnLookDrag", PrivateInstance);
        onLookPointerUpMethod = type.GetMethod("OnLookPointerUp", PrivateInstance);
        requestJumpMethod = type.GetMethod("RequestJump", PrivateInstance);
        requestInteractMethod = type.GetMethod("RequestInteract", PrivateInstance);
        setGrabHeldMethod = type.GetMethod("SetGrabHeld", PrivateInstance);
        requestThrowMethod = type.GetMethod("RequestThrow", PrivateInstance);
        setAimHeldMethod = type.GetMethod("SetAimHeld", PrivateInstance);
        setButtonPressedMethod = type.GetMethod("SetButtonPressed", PrivateInstance);
    }

    private static void Definir(FieldInfo field, object target, object value)
    {
        if (field != null && target != null)
            field.SetValue(target, value);
    }

    private void OnValidate()
    {
        ResolverControlador();
        InjetarReferencias();

        if (!Application.isPlaying && canvas != null && manterVisivelForaDoPlay)
            canvas.gameObject.SetActive(true);
    }
}
