using System;
using System.Globalization;
using System.Reflection;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Autoridade única da energia exibida pelo menu e do botão de recuperação.
/// Sempre usa o CameraRelativeMovement ligado à câmera/jogador ativo, evitando
/// leituras de cópias inativas ou objetos antigos da cena.
/// </summary>
[DefaultExecutionOrder(12000)]
[DisallowMultipleComponent]
public sealed class MiniMarketEnergyQuarterRefill : MonoBehaviour
{
    public static MiniMarketEnergyQuarterRefill Instance { get; private set; }

    [Header("Referências")]
    public MiniMarketMenuController menuController;
    public PlayerCameraController playerCamera;
    public CameraRelativeMovement movement;

    [Header("Recarga")]
    [Range(0.01f, 1f)] public float amountPerClick = 0.25f;
    [Min(0.02f)] public float liveUpdateInterval = 0.05f;

    [Header("Debug")]
    public bool logRefills;

    private static readonly BindingFlags InstancePrivate =
        BindingFlags.Instance | BindingFlags.NonPublic;

    private readonly CultureInfo culture = new CultureInfo("pt-BR");
    private Button boundButton;
    private MiniMarketEnergyQuarterButtonProxy boundProxy;
    private float nextRefresh;
    private int lastProcessedFrame = -1;

    public CameraRelativeMovement ActiveMovement => movement;
    public float CurrentEnergy01 => movement != null
        ? MiniMarketRuntimeConsistencyController.CalculateContinuousEnergy01(movement)
        : 0f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Instance = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        MiniMarketEnergyQuarterRefill existing =
            UnityEngine.Object.FindAnyObjectByType<MiniMarketEnergyQuarterRefill>(
                FindObjectsInactive.Include
            );

        if (existing != null)
        {
            Instance = existing;
            return;
        }

        GameObject runtimeObject = new GameObject("[MiniMarket] Energy Authority");
        DontDestroyOnLoad(runtimeObject);
        Instance = runtimeObject.AddComponent<MiniMarketEnergyQuarterRefill>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        ResolveReferences(true);
        ConfigureLegacyMenuAndButton(true);
        SyncLegacyMenuText();
    }

    private void OnDestroy()
    {
        ReleaseButton();

        if (Instance == this)
            Instance = null;
    }

    private void LateUpdate()
    {
        ResolveReferences(false);
        ConfigureLegacyMenuAndButton(false);

        if (Time.unscaledTime < nextRefresh)
            return;

        nextRefresh = Time.unscaledTime + Mathf.Max(0.02f, liveUpdateInterval);
        SyncLegacyMenuText();
    }

    public void AddTwentyFivePercent()
    {
        AddNormalizedEnergy(amountPerClick);
    }

    public bool AddNormalizedEnergy(float normalizedAmount)
    {
        if (Time.frameCount == lastProcessedFrame)
            return false;

        lastProcessedFrame = Time.frameCount;
        ResolveReferences(true);

        if (movement == null)
        {
            Debug.LogWarning(
                "[EnergyAuthority] CameraRelativeMovement ativo não encontrado.",
                this
            );
            return false;
        }

        float before = CurrentEnergy01;
        float target = Mathf.Clamp01(before + Mathf.Max(0f, normalizedAmount));

        if (Mathf.Approximately(before, target))
        {
            SyncLegacyMenuText();
            return true;
        }

        if (!TrySetEnergyPercent(movement, target))
        {
            Debug.LogWarning(
                "[EnergyAuthority] Não foi possível alterar a energia do jogador ativo.",
                movement
            );
            return false;
        }

        SyncLegacyMenuText();
        menuController?.AtualizarTextos();

        if (logRefills)
        {
            Debug.Log(
                "[EnergyAuthority] Energia " +
                Mathf.RoundToInt(before * 100f) + "% -> " +
                Mathf.RoundToInt(target * 100f) + "%.",
                movement
            );
        }

        return true;
    }

    private void ResolveReferences(bool force)
    {
        if (force || playerCamera == null || !playerCamera.gameObject.scene.IsValid())
        {
            playerCamera = UnityEngine.Object.FindAnyObjectByType<PlayerCameraController>(
                FindObjectsInactive.Include
            );
        }

        if (force || menuController == null || !menuController.gameObject.scene.IsValid())
        {
            menuController = UnityEngine.Object.FindAnyObjectByType<MiniMarketMenuController>(
                FindObjectsInactive.Include
            );
        }

        CameraRelativeMovement resolved = null;

        if (playerCamera != null && IsUsable(playerCamera.movement))
            resolved = playerCamera.movement;

        if (resolved == null && playerCamera != null && playerCamera.player != null)
        {
            CameraRelativeMovement playerMovement =
                playerCamera.player.GetComponent<CameraRelativeMovement>();

            if (IsUsable(playerMovement))
                resolved = playerMovement;
        }

        if (resolved == null && menuController != null && IsUsable(menuController.movimento))
            resolved = menuController.movimento;

        if (resolved == null)
        {
            CameraRelativeMovement[] candidates =
                UnityEngine.Object.FindObjectsByType<CameraRelativeMovement>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None
                );

            for (int i = 0; i < candidates.Length; i++)
            {
                CameraRelativeMovement candidate = candidates[i];
                if (candidate != null && candidate.isActiveAndEnabled &&
                    candidate.gameObject.activeInHierarchy)
                {
                    resolved = candidate;
                    break;
                }
            }
        }

        if (resolved != null)
            movement = resolved;

        if (menuController != null && movement != null)
        {
            menuController.movimento = movement;
            menuController.componenteStaminaOuMovimento = movement;
        }
    }

    private static bool IsUsable(CameraRelativeMovement value)
    {
        return value != null &&
               value.gameObject.scene.IsValid() &&
               value.isActiveAndEnabled &&
               value.gameObject.activeInHierarchy;
    }

    private void ConfigureLegacyMenuAndButton(bool force)
    {
        if (menuController == null)
            return;

        menuController.gemasGratisRecarregaEnergia = false;
        menuController.usarCliqueManualDeSeguranca = false;
        menuController.atualizarEmTempoReal = true;
        menuController.intervaloAtualizacaoAberto = Mathf.Min(
            menuController.intervaloAtualizacaoAberto,
            0.08f
        );

        Button target = menuController.botaoGemasGratis;
        if (target == null)
            return;

        DisableChildGraphicRaycasts(target);

        if (!force && target == boundButton && boundProxy != null)
        {
            boundProxy.service = this;
            return;
        }

        ReleaseButton();
        boundButton = target;
        boundButton.enabled = true;
        boundButton.interactable = true;

        // Substitui inclusive eventos persistentes antigos que restauravam 100%.
        boundButton.onClick = new Button.ButtonClickedEvent();
        boundButton.onClick.AddListener(AddTwentyFivePercent);

        Graphic graphic = boundButton.GetComponent<Graphic>();
        if (graphic != null)
            graphic.raycastTarget = true;

        DisableChildGraphicRaycasts(boundButton);

        boundProxy = boundButton.GetComponent<MiniMarketEnergyQuarterButtonProxy>();
        if (boundProxy == null)
            boundProxy = boundButton.gameObject.AddComponent<MiniMarketEnergyQuarterButtonProxy>();

        boundProxy.service = this;

        Text label = boundButton.GetComponentInChildren<Text>(true);
        if (label != null)
        {
            label.text = "RECUPERAR\n+25%";
            label.raycastTarget = false;
        }
    }

    private static void DisableChildGraphicRaycasts(Button button)
    {
        if (button == null)
            return;

        Graphic rootGraphic = button.GetComponent<Graphic>();
        Graphic[] graphics = button.GetComponentsInChildren<Graphic>(true);

        for (int i = 0; i < graphics.Length; i++)
        {
            Graphic graphic = graphics[i];
            if (graphic == null)
                continue;

            graphic.raycastTarget = graphic == rootGraphic;
        }
    }

    private void ReleaseButton()
    {
        if (boundButton != null)
            boundButton.onClick.RemoveListener(AddTwentyFivePercent);

        if (boundProxy != null && boundProxy.service == this)
            boundProxy.service = null;

        boundProxy = null;
        boundButton = null;
    }

    private void SyncLegacyMenuText()
    {
        if (menuController == null || movement == null)
            return;

        menuController.movimento = movement;
        menuController.componenteStaminaOuMovimento = movement;

        if (menuController.textoStamina == null)
            return;

        int percent = Mathf.RoundToInt(CurrentEnergy01 * 100f);
        menuController.textoStamina.text = menuController.incluirRotulosNosTextos
            ? menuController.rotuloEnergia + ": " + percent.ToString(culture) + "%"
            : percent.ToString(culture) + "%";
    }

    private static bool TrySetEnergyPercent(
        CameraRelativeMovement targetMovement,
        float targetPercent)
    {
        Type movementType = typeof(CameraRelativeMovement);
        FieldInfo staminaField = movementType.GetField("currentStamina", InstancePrivate);
        FieldInfo segmentsField = movementType.GetField("currentEnergySegments", InstancePrivate);
        FieldInfo reserveField = movementType.GetField("energyRechargeReserve", InstancePrivate);
        FieldInfo dirtyField = movementType.GetField("staminaDirty", InstancePrivate);

        if (staminaField == null || segmentsField == null)
            return false;

        float normalized = Mathf.Clamp01(targetPercent);
        float maxStamina = Mathf.Max(1f, targetMovement.MaxStamina);
        float newStamina;
        int newSegments;

        if (targetMovement.useSegmentedEnergy)
        {
            int maxSegments = Mathf.Max(1, targetMovement.StaminaSegmentosMaximos);

            if (normalized <= 0.0001f)
            {
                newSegments = 0;
                newStamina = 0f;
            }
            else
            {
                float totalBars = normalized * maxSegments;
                newSegments = Mathf.Clamp(Mathf.CeilToInt(totalBars), 1, maxSegments);
                float activeBar01 = Mathf.Clamp01(totalBars - (newSegments - 1));

                if (activeBar01 <= 0.0001f)
                    activeBar01 = 1f;

                newStamina = activeBar01 * maxStamina;
            }
        }
        else
        {
            newSegments = normalized > 0.0001f ? 1 : 0;
            newStamina = normalized * maxStamina;
        }

        staminaField.SetValue(targetMovement, Mathf.Clamp(newStamina, 0f, maxStamina));
        segmentsField.SetValue(
            targetMovement,
            Mathf.Clamp(newSegments, 0, Mathf.Max(1, targetMovement.StaminaSegmentosMaximos))
        );
        reserveField?.SetValue(targetMovement, 0f);
        dirtyField?.SetValue(targetMovement, true);

        MethodInfo markChanged = movementType.GetMethod(
            "MarkStaminaChanged",
            InstancePrivate,
            null,
            Type.EmptyTypes,
            null
        );
        markChanged?.Invoke(targetMovement, null);

        MethodInfo saveDatabase = movementType.GetMethod(
            "SaveStaminaToDatabase",
            InstancePrivate,
            null,
            new[] { typeof(bool) },
            null
        );
        saveDatabase?.Invoke(targetMovement, new object[] { true });
        return true;
    }
}

/// <summary>
/// Fallback de clique para EventSystems onde o Button legado esteja sendo interceptado.
/// O bloqueio por frame no serviço impede recarga dupla.
/// </summary>
public sealed class MiniMarketEnergyQuarterButtonProxy : MonoBehaviour, IPointerClickHandler
{
    [NonSerialized] public MiniMarketEnergyQuarterRefill service;

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData == null || eventData.button != PointerEventData.InputButton.Left)
            return;

        service?.AddTwentyFivePercent();
    }
}
