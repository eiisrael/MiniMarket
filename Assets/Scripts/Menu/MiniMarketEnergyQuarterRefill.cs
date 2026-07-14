using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Reconfigura automaticamente o botão Energia Grátis para adicionar 25% da
/// energia total por clique, em vez de restaurar 100% de uma vez.
///
/// O cálculo respeita o sistema segmentado, dispara o evento de stamina,
/// atualiza o HUD imediatamente e força a persistência no banco local.
/// </summary>
[DefaultExecutionOrder(11050)]
[DisallowMultipleComponent]
public sealed class MiniMarketEnergyQuarterRefill : MonoBehaviour
{
    [Header("Referências")]
    public MiniMarketMenuController menuController;
    public CameraRelativeMovement movement;

    [Header("Recarga")]
    [Range(0.01f, 1f)] public float amountPerClick = 0.25f;

    [Header("Busca automática")]
    [Min(0.1f)] public float referenceSearchInterval = 0.75f;

    [Header("Debug")]
    public bool logRefills;

    private static MiniMarketEnergyQuarterRefill instance;
    private Button boundButton;
    private bool previousMenuOpen;
    private float nextReferenceSearch;

    private static readonly BindingFlags InstancePrivate =
        BindingFlags.Instance | BindingFlags.NonPublic;

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
        EnsureButtonBinding(true);
    }

    private void OnDestroy()
    {
        UnbindButton();

        if (instance == this)
            instance = null;
    }

    private void LateUpdate()
    {
        ResolveReferences(false);

        if (menuController == null)
            return;

        // O clique manual antigo chamava diretamente a recarga de 100%.
        // O Button padrão permanece como única fonte do clique.
        menuController.usarCliqueManualDeSeguranca = false;

        bool menuOpenedNow = menuController.MenuAberto && !previousMenuOpen;
        EnsureButtonBinding(menuOpenedNow);
        previousMenuOpen = menuController.MenuAberto;
    }

    public void AddTwentyFivePercent()
    {
        ResolveReferences(true);

        if (movement == null)
        {
            Debug.LogWarning(
                "[EnergyQuarterRefill] CameraRelativeMovement não foi encontrado.",
                this
            );
            return;
        }

        float before = movement.EnergiaPercentual01;
        float target = Mathf.Clamp01(before + Mathf.Clamp(amountPerClick, 0.01f, 1f));

        if (!TrySetEnergyPercent(movement, target))
        {
            Debug.LogWarning(
                "[EnergyQuarterRefill] Não foi possível aplicar a recarga incremental.",
                movement
            );
            return;
        }

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
        if (!force && Time.unscaledTime < nextReferenceSearch &&
            menuController != null && movement != null)
        {
            return;
        }

        nextReferenceSearch = Time.unscaledTime + Mathf.Max(0.1f, referenceSearchInterval);

        if (menuController == null)
        {
            menuController = UnityEngine.Object.FindAnyObjectByType<MiniMarketMenuController>(
                FindObjectsInactive.Include
            );
        }

        if (movement == null && menuController != null)
            movement = menuController.movimento;

        if (movement == null)
        {
            movement = UnityEngine.Object.FindAnyObjectByType<CameraRelativeMovement>(
                FindObjectsInactive.Include
            );
        }

        if (menuController != null && menuController.movimento == null && movement != null)
            menuController.movimento = movement;
    }

    private void EnsureButtonBinding(bool force)
    {
        if (menuController == null)
            return;

        Button targetButton = menuController.botaoGemasGratis;
        if (targetButton == null)
            return;

        if (!force && boundButton == targetButton)
            return;

        UnbindButton();
        boundButton = targetButton;

        boundButton.onClick.RemoveListener(menuController.RecarregarEnergiaComGemasGratis);
        boundButton.onClick.RemoveListener(menuController.RecarregarStaminaComGemasGratis);
        boundButton.onClick.RemoveListener(AddTwentyFivePercent);
        boundButton.onClick.AddListener(AddTwentyFivePercent);
    }

    private void UnbindButton()
    {
        if (boundButton == null)
            return;

        boundButton.onClick.RemoveListener(AddTwentyFivePercent);
        boundButton = null;
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

                // Em valores exatos como 20%, 40% e 100%, a barra ativa é cheia.
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
