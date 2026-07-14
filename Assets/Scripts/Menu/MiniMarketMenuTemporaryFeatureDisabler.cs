using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Desativa temporariamente o botão de energia grátis do Menu ESC.
/// Remove eventos antigos, proxies de clique e também oculta botões runtime
/// criados por versões anteriores da interface profissional.
/// </summary>
[DefaultExecutionOrder(32000)]
[DisallowMultipleComponent]
public sealed class MiniMarketMenuTemporaryFeatureDisabler : MonoBehaviour
{
    public static MiniMarketMenuTemporaryFeatureDisabler Instance { get; private set; }

    [Header("Estado temporário")]
    public bool disableFreeEnergyButton = true;
    [Min(0.1f)] public float refreshInterval = 0.75f;

    private float nextRefresh;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Instance = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (Instance != null)
            return;

        MiniMarketMenuTemporaryFeatureDisabler existing =
            Object.FindAnyObjectByType<MiniMarketMenuTemporaryFeatureDisabler>(
                FindObjectsInactive.Include
            );

        if (existing != null)
        {
            Instance = existing;
            return;
        }

        GameObject host = new GameObject("[MiniMarket] Menu Temporary Features");
        DontDestroyOnLoad(host);
        Instance = host.AddComponent<MiniMarketMenuTemporaryFeatureDisabler>();
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
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private void Start()
    {
        StartCoroutine(DisableAfterFrame());
    }

    private void Update()
    {
        if (!disableFreeEnergyButton || Time.unscaledTime < nextRefresh)
            return;

        nextRefresh = Time.unscaledTime + Mathf.Max(0.1f, refreshInterval);
        Apply();
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        if (Instance == this)
            Instance = null;
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        StartCoroutine(DisableAfterFrame());
    }

    private IEnumerator DisableAfterFrame()
    {
        yield return null;
        Apply();
    }

    [ContextMenu("Menu/Desativar energia grátis agora")]
    public void Apply()
    {
        if (!disableFreeEnergyButton)
            return;

        MiniMarketMenuController[] menus = Object.FindObjectsByType<MiniMarketMenuController>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None
        );

        for (int i = 0; i < menus.Length; i++)
        {
            MiniMarketMenuController menu = menus[i];
            if (menu == null)
                continue;

            menu.gemasGratisRecarregaEnergia = false;
            menu.usarCliqueManualDeSeguranca = false;

            Button button = menu.botaoGemasGratis;
            DisableButton(button);
            menu.botaoGemasGratis = null;
        }

        Button[] buttons = Object.FindObjectsByType<Button>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None
        );

        for (int i = 0; i < buttons.Length; i++)
        {
            Button button = buttons[i];
            if (button == null || !LooksLikeFreeEnergyButton(button.name))
                continue;

            DisableButton(button);
        }

        MiniMarketEnergyQuarterRefill[] services =
            Object.FindObjectsByType<MiniMarketEnergyQuarterRefill>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None
            );

        for (int i = 0; i < services.Length; i++)
        {
            if (services[i] != null)
                services[i].enabled = false;
        }
    }

    private static void DisableButton(Button button)
    {
        if (button == null)
            return;

        button.onClick = new Button.ButtonClickedEvent();
        button.interactable = false;

        MiniMarketEnergyQuarterButtonProxy proxy =
            button.GetComponent<MiniMarketEnergyQuarterButtonProxy>();
        if (proxy != null)
            Destroy(proxy);

        button.gameObject.SetActive(false);
    }

    private static bool LooksLikeFreeEnergyButton(string objectName)
    {
        if (string.IsNullOrWhiteSpace(objectName))
            return false;

        string compact = objectName.ToUpperInvariant()
            .Replace(" ", string.Empty)
            .Replace("_", string.Empty)
            .Replace("-", string.Empty);

        return compact.Contains("GEMASBUTTON") ||
               compact.Contains("ENERGYBUTTON") ||
               compact.Contains("ENERGIAGRATIS") ||
               compact.Contains("RECUPERAR25");
    }
}
