using UnityEngine;

/// <summary>
/// Compatibilidade para scripts antigos que ainda referenciam CrosshairAim.
///
/// O sistema novo usa Camera1Person + mira própria + GetItemV2.
/// Este script existe apenas para evitar erro de compilação em sistemas legados.
/// </summary>
[DisallowMultipleComponent]
public class CrosshairAim : MonoBehaviour
{
    [Header("Legacy State")]
    public bool usarMouseDireitoComoMira = true;
    [Range(0, 2)] public int botaoDeMira = 1;
    public bool forcarEstadoManual;
    public bool estadoManual;

    public bool EstaMirando
    {
        get
        {
            if (forcarEstadoManual)
                return estadoManual;

            return usarMouseDireitoComoMira && Input.GetMouseButton(botaoDeMira);
        }
    }
}
