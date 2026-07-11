using UnityEngine;

/// <summary>
/// Componente de compatibilidade para cenas antigas.
///
/// A energia segmentada agora pertence diretamente a CameraRelativeMovement e ao
/// MiniMarketPlayerDatabase. Este componente não cria objetos runtime, não usa reflexão
/// e não executa lógica por frame. Pode ser removido das cenas quando conveniente.
/// </summary>
[DisallowMultipleComponent]
public class MiniMarketSegmentedStaminaRuntimeGuard : MonoBehaviour
{
    public static bool ForcarHudZeroNoSegmentoFantasma => false;
    public bool SegmentoFantasmaAtivo => false;
    public bool BloqueadoAteSoltarShift => false;

    [Header("Compatibilidade")]
    [Tooltip("Obsoleto. A stamina segmentada agora é controlada por CameraRelativeMovement.")]
    public bool componenteObsoleto = true;
}
