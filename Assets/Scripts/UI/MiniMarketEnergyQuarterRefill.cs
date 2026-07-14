using System;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Compatibilidade temporária.
///
/// A recarga gratuita do Menu ESC foi desativada por solicitação de design.
/// A classe permanece no projeto somente para não quebrar referências antigas
/// existentes em cenas ou em outros scripts.
/// </summary>
[DisallowMultipleComponent]
public sealed class MiniMarketEnergyQuarterRefill : MonoBehaviour
{
    public static MiniMarketEnergyQuarterRefill Instance => null;

    [Header("Desativado temporariamente")]
    [Tooltip("Mantido apenas para compatibilidade com cenas antigas.")]
    public bool featureDisabled = true;

    [NonSerialized] public CameraRelativeMovement movement;

    public CameraRelativeMovement ActiveMovement => null;
    public float CurrentEnergy01 => 0f;

    private void Awake()
    {
        enabled = false;
    }

    public void AddTwentyFivePercent()
    {
        // Recurso desativado temporariamente.
    }

    public bool AddNormalizedEnergy(float normalizedAmount)
    {
        return false;
    }
}

/// <summary>
/// Componente legado preservado para referências serializadas antigas.
/// Não executa nenhuma ação enquanto a recarga gratuita estiver desativada.
/// </summary>
public sealed class MiniMarketEnergyQuarterButtonProxy : MonoBehaviour, IPointerClickHandler
{
    [NonSerialized] public MiniMarketEnergyQuarterRefill service;

    public void OnPointerClick(PointerEventData eventData)
    {
        // Recurso desativado temporariamente.
    }
}
