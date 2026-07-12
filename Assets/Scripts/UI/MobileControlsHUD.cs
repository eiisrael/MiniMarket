using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>
/// HUD touch oficial do MiniMarket.
/// Aparece automaticamente em Android/iOS e permanece oculto no Desktop,
/// salvo quando o modo de teste forçado estiver habilitado no Inspector.
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(23000)]
public sealed class MobileControlsHUD : MonoBehaviour
{
    public static MobileControlsHUD Instance { get; private set; }

    [Header("Plataforma")]
    public bool criarAutomaticamente = true;
    public bool ocultarNoDesktop = true;
    public bool forcarVisivelParaTestes;

    [Header("Referências")]
    public CameraRelativeMovement movimento;
    public PlayerCameraController cameraController;
    public InteractionFocusController interactionController;
    public GetItemController getItemController;

    [Header("Layout")]
    public bool respeitarSafeArea = true;
    public int canvasSortingOrder = 120;
    [Min(90f)] public float tamanhoJoystick = 180f;
    [Range(0.2f, 0.95f)] public float raioMovimentoJoystick = 0.72f;
    public Vector2 margemJoystick = new Vector2(34f, 34f);
    [Min(42f)] public float tamanhoBotao = 76f;
    [Min(0f)] public float espacamentoBotoes = 14f;
    public Vector2 margemBotoes = new Vector2(32f, 30f);
    [Range(0.25f, 0.8f)] public float larguraAreaOlhar = 0.52f;

    [Header("Sensibilidade touch")]
    [Min(0.01f)] public float multiplicadorOlhar = 1f;
    [Min(0f)] public float deadZoneJoystick = 0.08f;

    [Header("Visual")]
    public Color corJoystickFundo = new Color(0.05f, 0.07f, 0.09f, 0.45f);
    public Color corJoystickPino = new Color(1f, 1f, 1f, 0.72f);
    public Color corBotao = new Color(0.08f, 0.1f, 0.14f, 0.72f);
    public Color corBotaoPressionado = new Color(0.15f, 0.75f, 0.3f, 0.9f);
    public Color corTexto = Color.white;
    [Range(12, 40)] public int tamanhoFonte = 20;

    [Header("Rótulos")]
    public string textoCorrer = "RUN";
    public string textoPular = "JUMP";
    public string textoInteragir = "E";
    public string textoPegar = "GRAB";
    public string textoArremessar = "THROW";
    public string textoMirar = "AIM";

    [Header("Busca")]
    [Min(0.25f)] public float intervaloBuscaReferencias = 1f;

    [Header("Debug")]
    public bool logarInicializacao;

    private const string RuntimeRootName = "MobileControlsRuntime";

    private Canvas canvas;
    private RectTransform safeAreaRect;
    private RectTransform joystickBase;
    private RectTransform joystickThumb;
    private Image runButtonImage;
    private Image grabButtonImage;
    private Image aimButtonImage;
    private float nextReferenceSearch;
    private Vector2 joystickValue;
    private bool runHeld;
    private bool grabHeld;
    private bool aimHeld;
    private bool joystickPointerActive;
    private int joystickPointerId = int.MinValue;
    private bool lookPointerActive;
    private int lookPointerId = int.MinValue;
    private Vector2 previousLookPosition;
    private Rect lastSafeArea;
    private int lastScreenWidth;
    private int lastScreenHeight;
    private bool built;

    public bool IsVisibleForCurrentPlatform =>
        Application.isMobilePlatform || forcarVisivelParaTestes || !ocultarNoDesktop;

    public Vector2 CurrentMoveInput => joystickValue;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Instance = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void CreateAfterSceneLoad()
    {
        MobileControlsHUD existing = Object.FindAnyObjectByType<MobileControlsHUD>(FindObjectsInactive.Include);
        if (existing != null)
        {
            Instance = existing;
            existing.ResolveReferences(true);
            existing.RefreshPlatformVisibility();
            return;
        }

        if (!Application.isMobilePlatform)
            return;

        GameObject host = new GameObject("MobileControlsHUD");
        DontDestroyOnLoad(host);
        host.AddComponent<MobileControlsHUD>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        if (transform.parent == null)
            DontDestroyOnLoad(gameObject);

        SceneManager.sceneLoaded += HandleSceneLoaded;
        ResolveReferences(true);
        RefreshPlatformVisibility();
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        ResetAllInputs();
        if (Instance == this)
            Instance = null;
    }

    private void OnDisable()
    {
        ResetAllInputs();
    }

    private void OnApplicationPause(bool paused)
    {
        if (paused)
            ResetAllInputs();
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        movimento = null;
        cameraController = null;
        interactionController = null;
        getItemController = null;
        nextReferenceSearch = 0f;
        ResolveReferences(true);
        RefreshPlatformVisibility();
    }

    private void Update()
    {
        if (!IsVisibleForCurrentPlatform)
            return;

        if (!built)
            BuildUI();

        if (Time.unscaledTime >= nextReferenceSearch && ReferencesNeedRefresh())
            ResolveReferences(false);

        UpdateSafeAreaIfNeeded();

        if (GameplayInputState.IsManuallyBlocked || Time.timeScale <= 0.0001f)
        {
            ResetAllInputs();
            return;
        }

        if (movimento != null)
        {
            movimento.SetMoveInput(joystickValue);
            movimento.SetRunInput(runHeld);
        }
    }

    [ContextMenu("Mobile HUD/Recriar controles runtime")]
    public void RebuildRuntimeUI()
    {
        ResetAllInputs();

        Transform oldRoot = transform.Find(RuntimeRootName);
        if (oldRoot != null)
        {
            if (Application.isPlaying)
                Destroy(oldRoot.gameObject);
            else
                DestroyImmediate(oldRoot.gameObject);
        }

        canvas = null;
        safeAreaRect = null;
        joystickBase = null;
        joystickThumb = null;
        runButtonImage = null;
        grabButtonImage = null;
        aimButtonImage = null;
        built = false;

        if (Application.isPlaying && IsVisibleForCurrentPlatform)
            BuildUI();
    }

    [ContextMenu("Mobile HUD/Rebuscar referências")]
    public void RebuscarReferencias()
    {
        ResolveReferences(true);
    }

    private void RefreshPlatformVisibility()
    {
        bool visible = IsVisibleForCurrentPlatform;

        if (visible && !built && Application.isPlaying)
            BuildUI();

        if (canvas != null)
            canvas.gameObject.SetActive(visible);

        if (!visible)
            ResetAllInputs();

        if (logarInicializacao)
        {
            Debug.Log(
                "[MobileControlsHUD] Plataforma=" + Application.platform +
                ", mobile=" + Application.isMobilePlatform +
                ", visível=" + visible,
                this
            );
        }
    }

    private bool ReferencesNeedRefresh()
    {
        return movimento == null || cameraController == null ||
               interactionController == null || getItemController == null;
    }

    private void ResolveReferences(bool force)
    {
        if (force || movimento == null)
            movimento = Object.FindAnyObjectByType<CameraRelativeMovement>(FindObjectsInactive.Include);

        if (force || cameraController == null)
            cameraController = Object.FindAnyObjectByType<PlayerCameraController>(FindObjectsInactive.Include);

        if (cameraController != null)
        {
            if (interactionController == null)
                interactionController = cameraController.GetComponent<InteractionFocusController>();
            if (getItemController == null)
                getItemController = cameraController.GetComponent<GetItemController>();
        }

        if (force || interactionController == null)
            interactionController = Object.FindAnyObjectByType<InteractionFocusController>(FindObjectsInactive.Include);

        if (force || getItemController == null)
            getItemController = Object.FindAnyObjectByType<GetItemController>(FindObjectsInactive.Include);

        nextReferenceSearch = Time.unscaledTime + Mathf.Max(0.25f, intervaloBuscaReferencias);
    }

    private void BuildUI()
    {
        if (built || !Application.isPlaying || !IsVisibleForCurrentPlatform)
            return;

        EnsureEventSystem();

        Transform existing = transform.Find(RuntimeRootName);
        if (existing != null)
            Destroy(existing.gameObject);

        GameObject canvasObject = new GameObject(RuntimeRootName, typeof(RectTransform));
        canvasObject.transform.SetParent(transform, false);
        canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = canvasSortingOrder;

        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        canvasObject.AddComponent<GraphicRaycaster>();

        GameObject safeObject = new GameObject("SafeArea", typeof(RectTransform));
        safeObject.transform.SetParent(canvasObject.transform, false);
        safeAreaRect = safeObject.GetComponent<RectTransform>();
        safeAreaRect.anchorMin = Vector2.zero;
        safeAreaRect.anchorMax = Vector2.one;
        safeAreaRect.offsetMin = Vector2.zero;
        safeAreaRect.offsetMax = Vector2.zero;

        BuildLookArea();
        BuildJoystick();
        BuildActionButtons();

        built = true;
        ApplySafeArea(Screen.safeArea);
    }

    private void BuildLookArea()
    {
        GameObject area = CreatePanel("LookArea", safeAreaRect, new Color(0f, 0f, 0f, 0.001f));
        RectTransform rect = area.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(1f - larguraAreaOlhar, 0f);
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        AddEvent(area, EventTriggerType.PointerDown, data => OnLookPointerDown((PointerEventData)data));
        AddEvent(area, EventTriggerType.Drag, data => OnLookDrag((PointerEventData)data));
        AddEvent(area, EventTriggerType.PointerUp, data => OnLookPointerUp((PointerEventData)data));
        AddEvent(area, EventTriggerType.PointerExit, data => OnLookPointerUp((PointerEventData)data));
    }

    private void BuildJoystick()
    {
        GameObject baseObject = CreatePanel("MoveJoystick", safeAreaRect, corJoystickFundo);
        joystickBase = baseObject.GetComponent<RectTransform>();
        joystickBase.anchorMin = Vector2.zero;
        joystickBase.anchorMax = Vector2.zero;
        joystickBase.pivot = Vector2.zero;
        joystickBase.anchoredPosition = margemJoystick;
        joystickBase.sizeDelta = Vector2.one * tamanhoJoystick;

        GameObject thumbObject = CreatePanel("Thumb", joystickBase, corJoystickPino);
        joystickThumb = thumbObject.GetComponent<RectTransform>();
        joystickThumb.anchorMin = new Vector2(0.5f, 0.5f);
        joystickThumb.anchorMax = new Vector2(0.5f, 0.5f);
        joystickThumb.pivot = new Vector2(0.5f, 0.5f);
        joystickThumb.anchoredPosition = Vector2.zero;
        joystickThumb.sizeDelta = Vector2.one * (tamanhoJoystick * 0.42f);
        thumbObject.GetComponent<Image>().raycastTarget = false;

        AddEvent(baseObject, EventTriggerType.PointerDown, data => OnJoystickPointer((PointerEventData)data, true));
        AddEvent(baseObject, EventTriggerType.Drag, data => OnJoystickPointer((PointerEventData)data, false));
        AddEvent(baseObject, EventTriggerType.PointerUp, data => OnJoystickRelease((PointerEventData)data));
        AddEvent(baseObject, EventTriggerType.PointerExit, data => OnJoystickRelease((PointerEventData)data));
    }

    private void BuildActionButtons()
    {
        RectTransform actionRoot = CreateActionRoot();
        float step = tamanhoBotao + espacamentoBotoes;

        Image aim = CreateActionButton("Aim", textoMirar, actionRoot, Vector2.zero);
        aimButtonImage = aim;
        AddHoldEvents(aim, SetAimHeld);

        Image jump = CreateActionButton("Jump", textoPular, actionRoot, new Vector2(-step, 0f));
        AddEvent(jump.gameObject, EventTriggerType.PointerDown, data => RequestJump());

        Image run = CreateActionButton("Run", textoCorrer, actionRoot, new Vector2(-step * 2f, 0f));
        runButtonImage = run;
        AddHoldEvents(run, held =>
        {
            runHeld = held;
            SetButtonPressed(runButtonImage, held);
        });

        Image grab = CreateActionButton("Grab", textoPegar, actionRoot, new Vector2(0f, step));
        grabButtonImage = grab;
        AddHoldEvents(grab, SetGrabHeld);

        Image interact = CreateActionButton("Interact", textoInteragir, actionRoot, new Vector2(-step, step));
        AddEvent(interact.gameObject, EventTriggerType.PointerDown, data => RequestInteract());

        Image throwButton = CreateActionButton("Throw", textoArremessar, actionRoot, new Vector2(0f, step * 2f));
        AddEvent(throwButton.gameObject, EventTriggerType.PointerDown, data => RequestThrow());
    }

    private RectTransform CreateActionRoot()
    {
        GameObject root = new GameObject("Actions", typeof(RectTransform));
        root.transform.SetParent(safeAreaRect, false);
        RectTransform rect = root.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(1f, 0f);
        rect.anchorMax = new Vector2(1f, 0f);
        rect.pivot = new Vector2(1f, 0f);
        rect.anchoredPosition = new Vector2(-margemBotoes.x, margemBotoes.y);
        rect.sizeDelta = new Vector2(
            tamanhoBotao * 3f + espacamentoBotoes * 2f,
            tamanhoBotao * 3f + espacamentoBotoes * 2f
        );
        return rect;
    }

    private Image CreateActionButton(string objectName, string label, Transform parent, Vector2 position)
    {
        GameObject buttonObject = CreatePanel(objectName, parent, corBotao);
        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(1f, 0f);
        rect.anchorMax = new Vector2(1f, 0f);
        rect.pivot = new Vector2(1f, 0f);
        rect.anchoredPosition = position;
        rect.sizeDelta = Vector2.one * tamanhoBotao;

        GameObject textObject = new GameObject("Label", typeof(RectTransform), typeof(Text));
        textObject.transform.SetParent(buttonObject.transform, false);
        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        Text text = textObject.GetComponent<Text>();
        text.text = label;
        text.alignment = TextAnchor.MiddleCenter;
        text.fontSize = tamanhoFonte;
        text.color = corTexto;
        text.raycastTarget = false;
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        return buttonObject.GetComponent<Image>();
    }

    private GameObject CreatePanel(string objectName, Transform parent, Color color)
    {
        GameObject target = new GameObject(objectName, typeof(RectTransform), typeof(Image));
        target.transform.SetParent(parent, false);
        Image image = target.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = true;
        return target;
    }

    private void AddHoldEvents(Image image, Action<bool> setter)
    {
        AddEvent(image.gameObject, EventTriggerType.PointerDown, data => setter(true));
        AddEvent(image.gameObject, EventTriggerType.PointerUp, data => setter(false));
        AddEvent(image.gameObject, EventTriggerType.PointerExit, data => setter(false));
    }

    private void AddEvent(GameObject target, EventTriggerType type, UnityAction<BaseEventData> callback)
    {
        EventTrigger trigger = target.GetComponent<EventTrigger>();
        if (trigger == null)
            trigger = target.AddComponent<EventTrigger>();
        if (trigger.triggers == null)
            trigger.triggers = new List<EventTrigger.Entry>();

        EventTrigger.Entry entry = new EventTrigger.Entry { eventID = type };
        entry.callback.AddListener(callback);
        trigger.triggers.Add(entry);
    }

    private void OnJoystickPointer(PointerEventData eventData, bool pointerDown)
    {
        if (pointerDown)
        {
            if (joystickPointerActive)
                return;
            joystickPointerActive = true;
            joystickPointerId = eventData.pointerId;
        }
        else if (!joystickPointerActive || eventData.pointerId != joystickPointerId)
        {
            return;
        }

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                joystickBase,
                eventData.position,
                eventData.pressEventCamera,
                out Vector2 localPoint))
        {
            return;
        }

        Vector2 center = joystickBase.rect.center;
        float radius = Mathf.Max(1f, joystickBase.rect.width * 0.5f * raioMovimentoJoystick);
        Vector2 normalized = Vector2.ClampMagnitude((localPoint - center) / radius, 1f);

        if (normalized.magnitude < deadZoneJoystick)
            normalized = Vector2.zero;

        joystickValue = normalized;
        if (joystickThumb != null)
            joystickThumb.anchoredPosition = normalized * radius;
        if (movimento != null)
            movimento.SetMoveInput(joystickValue);
    }

    private void OnJoystickRelease(PointerEventData eventData)
    {
        if (!joystickPointerActive || eventData.pointerId != joystickPointerId)
            return;

        joystickPointerActive = false;
        joystickPointerId = int.MinValue;
        joystickValue = Vector2.zero;

        if (joystickThumb != null)
            joystickThumb.anchoredPosition = Vector2.zero;
        if (movimento != null)
            movimento.SetMoveInput(Vector2.zero);
    }

    private void OnLookPointerDown(PointerEventData eventData)
    {
        if (lookPointerActive)
            return;

        lookPointerActive = true;
        lookPointerId = eventData.pointerId;
        previousLookPosition = eventData.position;
    }

    private void OnLookDrag(PointerEventData eventData)
    {
        if (!lookPointerActive || eventData.pointerId != lookPointerId)
            return;

        Vector2 delta = eventData.position - previousLookPosition;
        previousLookPosition = eventData.position;

        if (cameraController != null)
            cameraController.AddMobileLookDelta(delta * multiplicadorOlhar);
    }

    private void OnLookPointerUp(PointerEventData eventData)
    {
        if (!lookPointerActive || eventData.pointerId != lookPointerId)
            return;

        lookPointerActive = false;
        lookPointerId = int.MinValue;
    }

    private void RequestJump()
    {
        if (movimento != null)
            movimento.RequestJump();
    }

    private void RequestInteract()
    {
        if (interactionController != null)
            interactionController.RequestInteract();
    }

    private void SetGrabHeld(bool held)
    {
        if (grabHeld == held)
            return;

        grabHeld = held;
        SetButtonPressed(grabButtonImage, held);

        if (getItemController == null)
            return;

        if (held)
            getItemController.RequestGrabPressed();
        else
            getItemController.RequestGrabReleased();
    }

    private void RequestThrow()
    {
        if (getItemController != null)
            getItemController.RequestThrow();
    }

    private void SetAimHeld(bool held)
    {
        if (aimHeld == held)
            return;

        aimHeld = held;
        SetButtonPressed(aimButtonImage, held);

        if (cameraController != null)
            cameraController.SetMobileFirstPersonHeld(held);
    }

    private void SetButtonPressed(Image image, bool pressed)
    {
        if (image != null)
            image.color = pressed ? corBotaoPressionado : corBotao;
    }

    private void ResetAllInputs()
    {
        joystickValue = Vector2.zero;
        joystickPointerActive = false;
        joystickPointerId = int.MinValue;
        lookPointerActive = false;
        lookPointerId = int.MinValue;

        if (movimento != null)
        {
            movimento.SetMoveInput(Vector2.zero);
            movimento.SetRunInput(false);
        }

        if (grabHeld && getItemController != null)
            getItemController.RequestGrabReleased();

        if (aimHeld && cameraController != null)
            cameraController.SetMobileFirstPersonHeld(false);

        runHeld = false;
        grabHeld = false;
        aimHeld = false;

        if (joystickThumb != null)
            joystickThumb.anchoredPosition = Vector2.zero;
        SetButtonPressed(runButtonImage, false);
        SetButtonPressed(grabButtonImage, false);
        SetButtonPressed(aimButtonImage, false);
    }

    private void UpdateSafeAreaIfNeeded()
    {
        Rect safeArea = Screen.safeArea;
        if (lastScreenWidth == Screen.width &&
            lastScreenHeight == Screen.height &&
            lastSafeArea == safeArea)
        {
            return;
        }

        ApplySafeArea(safeArea);
    }

    private void ApplySafeArea(Rect safeArea)
    {
        if (safeAreaRect == null)
            return;

        lastScreenWidth = Screen.width;
        lastScreenHeight = Screen.height;
        lastSafeArea = safeArea;

        if (!respeitarSafeArea || Screen.width <= 0 || Screen.height <= 0)
        {
            safeAreaRect.anchorMin = Vector2.zero;
            safeAreaRect.anchorMax = Vector2.one;
            safeAreaRect.offsetMin = Vector2.zero;
            safeAreaRect.offsetMax = Vector2.zero;
            return;
        }

        safeAreaRect.anchorMin = new Vector2(safeArea.xMin / Screen.width, safeArea.yMin / Screen.height);
        safeAreaRect.anchorMax = new Vector2(safeArea.xMax / Screen.width, safeArea.yMax / Screen.height);
        safeAreaRect.offsetMin = Vector2.zero;
        safeAreaRect.offsetMax = Vector2.zero;
    }

    private void EnsureEventSystem()
    {
        if (Object.FindAnyObjectByType<EventSystem>(FindObjectsInactive.Include) != null)
            return;

        new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
    }

    private void OnValidate()
    {
        tamanhoJoystick = Mathf.Max(90f, tamanhoJoystick);
        raioMovimentoJoystick = Mathf.Clamp(raioMovimentoJoystick, 0.2f, 0.95f);
        tamanhoBotao = Mathf.Max(42f, tamanhoBotao);
        espacamentoBotoes = Mathf.Max(0f, espacamentoBotoes);
        larguraAreaOlhar = Mathf.Clamp(larguraAreaOlhar, 0.25f, 0.8f);
        multiplicadorOlhar = Mathf.Max(0.01f, multiplicadorOlhar);
        deadZoneJoystick = Mathf.Clamp01(deadZoneJoystick);
        intervaloBuscaReferencias = Mathf.Max(0.25f, intervaloBuscaReferencias);
        tamanhoFonte = Mathf.Clamp(tamanhoFonte, 12, 40);

        if (Application.isPlaying)
            RefreshPlatformVisibility();
    }
}
