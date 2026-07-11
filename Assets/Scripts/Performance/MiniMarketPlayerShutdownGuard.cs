using UnityEngine;

/// <summary>
/// Evita que PlayerMove tente recriar/acessar o banco durante Stop/Quit.
/// O banco e o otimizador já fazem o flush final; este guard apenas impede a chamada
/// tardia que gerava NullReferenceException em PlayerMove.OnDisable.
/// </summary>
[DefaultExecutionOrder(-32000)]
[DisallowMultipleComponent]
public class MiniMarketPlayerShutdownGuard : MonoBehaviour
{
    private static MiniMarketPlayerShutdownGuard instancia;
    private static bool encerrando;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetarStatics()
    {
        instancia = null;
        encerrando = false;
        Application.quitting -= PrepararTodosParaEncerramento;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void RegistrarEncerramento()
    {
        Application.quitting -= PrepararTodosParaEncerramento;
        Application.quitting += PrepararTodosParaEncerramento;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void CriarAutomaticamente()
    {
        if (instancia != null)
            return;

        GameObject go = new GameObject("MiniMarket_PlayerShutdownGuard");
        DontDestroyOnLoad(go);
        go.AddComponent<MiniMarketPlayerShutdownGuard>();
    }

    private void Awake()
    {
        if (instancia != null && instancia != this)
        {
            Destroy(gameObject);
            return;
        }

        instancia = this;

        if (transform.parent == null)
            DontDestroyOnLoad(gameObject);
    }

    private void OnApplicationQuit()
    {
        PrepararTodosParaEncerramento();
    }

    private void OnDisable()
    {
        // Fallback do Editor ao sair do Play Mode.
        if (!Application.isPlaying || MiniMarketPlayerDatabase.EncerrandoAplicacao)
            PrepararTodosParaEncerramento();
    }

    private void OnDestroy()
    {
        if (!Application.isPlaying || MiniMarketPlayerDatabase.EncerrandoAplicacao)
            PrepararTodosParaEncerramento();

        if (instancia == this)
            instancia = null;
    }

    private static void PrepararTodosParaEncerramento()
    {
        if (encerrando)
            return;

        encerrando = true;

        PlayerMove[] players = Object.FindObjectsByType<PlayerMove>(FindObjectsInactive.Include);
        for (int i = 0; i < players.Length; i++)
        {
            PlayerMove player = players[i];
            if (player == null)
                continue;

            // PlayerMove.OnDisable passa a ignorar SalvarStaminaNoBanco.
            // O MiniMarketPlayerDatabase/PerformanceOptimizer já cuidam do flush final.
            player.usarBancoDeDadosStamina = false;
        }
    }
}
