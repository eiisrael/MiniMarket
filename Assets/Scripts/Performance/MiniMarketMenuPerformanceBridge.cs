using UnityEngine;

/// <summary>
/// Reduz o custo do menu do MiniMarket.
///
/// O MiniMarketMenuController antigo atualizava textos e fazia reflexao em todo frame
/// enquanto o menu estava aberto. Este bridge desliga essa atualizacao por frame e
/// atualiza em intervalo controlado, sem alterar o funcionamento visual do menu.
/// </summary>
[DefaultExecutionOrder(9000)]
[DisallowMultipleComponent]
public class MiniMarketMenuPerformanceBridge : MonoBehaviour
{
    [Header("Ativacao")]
    public bool ativo = true;
    public bool criarAutomaticamente = true;

    [Header("Atualizacao")]
    [Min(0.1f)] public float intervaloAtualizacaoMenu = 0.25f;
    [Min(0.5f)] public float intervaloBusca = 2f;
    public bool atualizarSomenteQuandoMenuAberto = true;

    [Header("Debug")]
    public bool desativarLogsDoMenu = true;

    private static MiniMarketMenuPerformanceBridge instancia;
    private MiniMarketMenuController[] menus = new MiniMarketMenuController[0];
    private PlayerMove playerMove;
    private float proximaBusca;
    private float proximaAtualizacao;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetarStatics()
    {
        instancia = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void CriarAuto()
    {
        if (instancia != null)
            return;

        GameObject go = new GameObject("MiniMarket_MenuPerformanceBridge");
        DontDestroyOnLoad(go);
        go.AddComponent<MiniMarketMenuPerformanceBridge>();
    }

    private void Awake()
    {
        if (instancia != null && instancia != this)
        {
            Destroy(gameObject);
            return;
        }

        instancia = this;
        DontDestroyOnLoad(gameObject);
        BuscarReferencias();
        AplicarConfiguracao();
    }

    private void Update()
    {
        if (!ativo)
            return;

        if (Time.unscaledTime >= proximaBusca)
        {
            proximaBusca = Time.unscaledTime + Mathf.Max(0.5f, intervaloBusca);
            BuscarReferencias();
            AplicarConfiguracao();
        }

        if (Time.unscaledTime < proximaAtualizacao)
            return;

        proximaAtualizacao = Time.unscaledTime + Mathf.Max(0.1f, intervaloAtualizacaoMenu);

        if (atualizarSomenteQuandoMenuAberto && !CameraV2MenuInputBlocker.MenuAberto)
            return;

        for (int i = 0; i < menus.Length; i++)
        {
            MiniMarketMenuController menu = menus[i];
            if (menu != null && menu.isActiveAndEnabled)
                menu.AtualizarTextos();
        }
    }

    private void BuscarReferencias()
    {
        if (playerMove == null)
            playerMove = Object.FindFirstObjectByType<PlayerMove>(FindObjectsInactive.Include);

        if (menus == null || menus.Length == 0 || TodosMenusInvalidos())
            menus = Object.FindObjectsByType<MiniMarketMenuController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
    }

    private bool TodosMenusInvalidos()
    {
        if (menus == null || menus.Length == 0)
            return true;

        for (int i = 0; i < menus.Length; i++)
        {
            if (menus[i] != null)
                return false;
        }

        return true;
    }

    private void AplicarConfiguracao()
    {
        if (menus == null)
            return;

        for (int i = 0; i < menus.Length; i++)
        {
            MiniMarketMenuController menu = menus[i];
            if (menu == null)
                continue;

            menu.atualizarEmTempoReal = false;

            if (playerMove != null)
                menu.componenteStaminaOuMovimento = playerMove;

            if (desativarLogsDoMenu)
            {
                menu.logarEventos = false;
                menu.logarComponenteEnergiaEncontrado = false;
            }
        }
    }
}
