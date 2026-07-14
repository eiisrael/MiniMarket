using System;
using System.Globalization;
using System.Reflection;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Fonte autoritativa do botão Energia +25%.
///
/// - encontra o CameraRelativeMovement realmente ligado à câmera/jogador ativo;
/// - evita conflito com os listeners antigos que restauravam 100%;
/// - aplica exatamente +25% da energia total segmentada;
/// - atualiza o texto do menu no mesmo frame;
/// - salva imediatamente no banco local.
/// </summary>
[DefaultExecutionOrder(11050)]
[DisallowMultipleComponent]
public sealed class MiniMarketEnergyQuarterRefill : MonoBehaviour
{
    [Header("Referências")]
    public MiniMarketMenuController menuController;
    public PlayerCameraController playerCamera;
    public CameraRelativeMovement movement;

    [Header("Recarga")]
    [Range(0.01f, 1f)] public float amountPerClick = 0.25f;
    [Min(0.02f)] public float liveUpdateInterval = 0.08f;

    [Header("Debug")]
    public bool logRefills;

    private static MiniMarketEnergyQuarterRefill instance;
    private static readonly BindingFlags InstancePrivate =
        BindingFlags.Instance | BindingFlags.NonPublic;

    private readonly CultureInfo culture = new CultureInfo("pt-BR");
    private Button boundButton;
    private MiniMarketEnergyQuarterButtonProxy boundProxy;
    private float nextRefresh;
    private int lastProcessedFrame = -1;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (instance != null)
            return;

        MiniMarketEnergyQuarterRefill existing =
            UnityEngine.Object.FindAnyObjectByType<MiniMarketEnergyQuarterRefill>(
                FindObjectsInactive.Include
            );

        if (existing != null)
        {
            instance = existing;
            return;
        }

        GameObject runtimeObject = new GameObject("[MiniMarket] Energy Quarter Refill");
        DontDestroyOnLoad(runtimeObject);
        instance = runtimeObject.AddComponent<MiniMarketEnergyQuarterRefill>();
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
        ResolveReferences(true);
        ConfigureMenuAndButton(true);
        SyncMenuEnergyText();
    }

    private void OnDestroy()
    {
        ReleaseButton();

        if (instance == this)
            instance = null;
    }

    private void LateUpdate()
    {
        ResolveReferences(false);
        ConfigureMenuAndButton(false);

        if (Time.unscaledTime >= nextRefresh)
        {
            nextRefresh = Time.unscaledTime + Mathf.Max(0.02f, liveUpdateInterval);
            SyncMenuEnergyText();
        }
    }

    public void AddTwentyFivePercent()
    {
        if (Time.frameCount == lastProcessedFrame)
            return;

        lastProcessedFrame = Time.frameCount;
        ResolveReferences(true);

        if (movement == null)
        {
            Debug.LogWarning(
                "[EnergyQuarterRefill] Movimento ativo do jogador não foi encontrado.",
                this
            );
            return;
        }

        float before = Mathf.Clamp01(movement.EnergiaPercentual01);
        float target = Mathf.Clamp01(before + Mathf.Clamp(amountPerClick, 0.01f, 1f));

        if (!TrySetEnergyPercent(movement, target))
        {
            Debug.LogWarning(
                "[EnergyQuarterRefill] Falha ao aplicar energia incremental.",
                movement
            );
            return;
        }

        SyncMenuEnergyText();

        if (menuController != null)
            menuController.AtualizarTextos();

        if (logRefills)
        {
            Debug.Log(
                "[EnergyQuarterRefill] Energia: " +
                Mathf.RoundToInt(before * 100f) + "% -> " +
                Mathf.RoundToInt(target * 100f) + "%.",
                movement
            );
        }
    }

    private void ResolveReferences(bool force)
    {
        if (force || menuController == null)
        {
            menuController = UnityEngine.Object.FindAnyObjectByType<MiniMarketMenuController>(
                FindObjectsInactive.Include
            );
        }

        if (force || playerCamera == null)
        {
            playerCamera = UnityEngine.Object.FindAnyObjectByType<PlayerCameraController>(
                FindObjectsInactive.Include
            );
        }

        CameraRelativeMovement resolvedMovement = null;

        if (playerCamera != null && IsUsableMovement(playerCamera.movement))
            resolvedMovement = playerCamera.movement;

        if (resolvedMovement == null &&
            menuController != null &&
            IsUsableMovement(menuController.movimento))
        {
            resolvedMovement = menuController.movimento;
        }

        if (resolvedMovement == null)
        {
            CameraRelativeMovement[] candidates =
                UnityEngine.Object.FindObjectsByType<CameraRelativeMovement>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None
                );

            for (int i = 0; i < candidates.Length; i++)
            {
                CameraRelativeMovement candidate = candidates[i];
                if (candidate != null && candidate.isActiveAndEnabled)
                {
                    resolvedMovement = candidate;
                    break;
                }
            }

            if (resolvedMovement == null && candidates.Length > 0)
                resolvedMovement = candidates[0];
        }

        if (resolvedMovement != null)
            movement = resolvedMovement;

        if (menuController != null && movement != null)
            menuController.movimento = movement;
    }

    private static bool IsUsableMovement(CameraRelativeMovement value)
    {
        return value != null && value.gameObject.scene.IsValid();
    }

    private void ConfigureMenuAndButton(bool force)
    {
        if (menuController == null)
            return;

        menuController.usarCliqueManualDeSeguranca = false;
        menuController.atualizarEmTempoReal = true;
        menuController.intervaloAtualizacaoAberto = Mathf.Min(
            menuController.intervaloAtualizacaoAberto,
            0.10f
        );

        Button target = menuController.botaoGemasGratis;
        if (target == null)
            return;

        if (!force && target == boundButton && boundProxy != null)
        {
            boundProxy.service = this;
            target.enabled = false;
            return;
        }

        ReleaseButton();
        boundButton = target;

        // Desliga somente o componente Button antigo. A Image continua recebendo raycast
        // e o proxy abaixo passa a ser a única fonte do clique.
        boundButton.enabled = false;

        Graphic graphic = boundButton.GetComponent<Graphic>();
        if (graphic != null)
            graphic.raycastTarget = true;

        boundProxy = boundButton.GetComponent<MiniMarketEnergyQuarterButtonProxy>();
        if (boundProxy == null)
            boundProxy = boundButton.gameObject.AddComponent<MiniMarketEnergyQuarterButtonProxy>();

        boundProxy.service = this;

        Text label = boundButton.GetComponentInChildren<Text>(true);
        if (label != null)
            label.text = "ENERGIA\n+25%";
    }

    private void ReleaseButton()
    {
        if (boundProxy != null && boundProxy.service == this)
            boundProxy.service = null;

        boundProxy = null;
        boundButton = null;
    }

    private void SyncMenuEnergyText()
    {
        if (menuController == null || movement == null)
            return;

        menuController.movimento = movement;

        if (menuController.textoStamina != null)
        {
            int percent = Mathf.RoundToInt(Mathf.Clamp01(movement.EnergiaPercentual01) * 100f);
            menuController.textoStamina.text = menuController.incluirRotulosNosTextos
                ? menuController.rotuloEnergia + ": " + percent.ToString(culture) + "%"
                : percent.ToString(culture) + "%";
        }
    }

    private static bool TrySetEnergyPercent(
        CameraRelativeMovement targetMovement,
        float targetPercent)
    {
        if (targetMovement == null)
            return false;

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

                float activeBar01 = totalBars - (newSegments - 1);
                activeBar01 = Mathf.Clamp01(activeBar01);

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

        if (reserveField != null)
            reserveField.SetValue(targetMovement, 0f);
        if (dirtyField != null)
            dirtyField.SetValue(targetMovement, true);

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
/// Recebe o clique mesmo com o componente Button legado desativado.
/// A Image do botão permanece como alvo de raycast do EventSystem.
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
