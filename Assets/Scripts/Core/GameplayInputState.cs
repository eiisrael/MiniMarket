using UnityEngine;

/// <summary>
/// Estado central de entrada do gameplay.
/// Câmera, movimento e GetItem consultam este estado para parar imediatamente
/// quando um menu libera o cursor ou pausa o jogo.
/// </summary>
public static class GameplayInputState
{
    private static int manualBlockCount;

    public static bool IsManuallyBlocked => manualBlockCount > 0;

    public static bool IsBlocked
    {
        get
        {
            if (IsManuallyBlocked || Time.timeScale <= 0.0001f)
                return true;

            return Cursor.visible && Cursor.lockState != CursorLockMode.Locked;
        }
    }

    public static void PushBlock()
    {
        manualBlockCount++;
    }

    public static void PopBlock()
    {
        manualBlockCount = Mathf.Max(0, manualBlockCount - 1);
    }

    public static void ClearBlocks()
    {
        manualBlockCount = 0;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetState()
    {
        manualBlockCount = 0;
    }
}
