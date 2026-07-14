using System;
using System.Globalization;
using UnityEngine;

/// <summary>
/// Inventário provisório de jornais persistido no banco principal do jogador.
///
/// Até o inventário definitivo existir, a quantidade é armazenada em um registro
/// reservado de MiniMarketPlayerDatabase.propriedades. Dessa forma o dado permanece
/// dentro do player_database.mmdb e pode ser migrado futuramente sem usar PlayerPrefs.
/// </summary>
[DefaultExecutionOrder(-8500)]
[DisallowMultipleComponent]
public sealed class MiniMarketNewspaperInventoryService : MonoBehaviour
{
    public const string InventoryRecordId = "INVENTORY_NEWSPAPER";
    private const string CountPrefix = "COUNT=";
    private const string PlacePrefix = "NEWSPAPER_PLACE_";
    private const string PlacedStatus = "PLACED=1";
    private const string EmptyStatus = "PLACED=0";

    public static MiniMarketNewspaperInventoryService Instance { get; private set; }

    [Header("Configuração")]
    [Min(0)] public int quantidadeInicial;
    public bool salvarImediatamente = true;
    public bool logarEventos;

    private MiniMarketPlayerDatabase database;
    private int quantidadeJornais = -1;
    private bool subscribed;

    public event Action<int> OnNewspaperCountChanged;

    public int QuantidadeJornais
    {
        get
        {
            ResolveDatabase();
            RefreshFromDatabase(false);
            return Mathf.Max(0, quantidadeJornais);
        }
    }

    public bool PossuiJornal => QuantidadeJornais > 0;

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

        MiniMarketNewspaperInventoryService existing =
            UnityEngine.Object.FindAnyObjectByType<MiniMarketNewspaperInventoryService>(
                FindObjectsInactive.Include
            );

        if (existing != null)
        {
            Instance = existing;
            return;
        }

        GameObject host = new GameObject("[MiniMarket] Newspaper Inventory");
        DontDestroyOnLoad(host);
        Instance = host.AddComponent<MiniMarketNewspaperInventoryService>();
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
        ResolveDatabase();
        RefreshFromDatabase(true);
    }

    private void OnEnable()
    {
        ResolveDatabase();
        Subscribe();
        RefreshFromDatabase(true);
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    private void OnDestroy()
    {
        Unsubscribe();

        if (Instance == this)
            Instance = null;
    }

    public int AdicionarJornais(int quantidade = 1)
    {
        if (quantidade <= 0)
            return QuantidadeJornais;

        int novoValor = Mathf.Max(0, QuantidadeJornais + quantidade);
        SetNewspaperCount(novoValor);
        return novoValor;
    }

    public bool TentarConsumirJornal(int quantidade = 1)
    {
        quantidade = Mathf.Max(1, quantidade);
        int atual = QuantidadeJornais;

        if (atual < quantidade)
            return false;

        SetNewspaperCount(atual - quantidade);
        return true;
    }

    public void DefinirQuantidadeJornais(int quantidade)
    {
        SetNewspaperCount(Mathf.Max(0, quantidade));
    }

    public bool LocalPossuiJornal(string placeId)
    {
        ResolveDatabase();
        if (database == null)
            return false;

        MiniMarketPlayerDatabase.MiniMarketPropertyState state =
            database.ObterPropriedade(BuildPlaceRecordId(placeId));

        return state != null &&
               string.Equals(state.status, PlacedStatus, StringComparison.OrdinalIgnoreCase);
    }

    public void DefinirJornalNoLocal(string placeId, bool colocado)
    {
        ResolveDatabase();
        if (database == null)
            return;

        string recordId = BuildPlaceRecordId(placeId);
        database.DefinirStatusPropriedade(
            recordId,
            "Expositor de jornal",
            false,
            true,
            colocado ? PlacedStatus : EmptyStatus
        );

        if (salvarImediatamente)
            database.SalvarSePendenteOuForcar();
    }

    private void SetNewspaperCount(int value)
    {
        ResolveDatabase();
        value = Mathf.Max(0, value);

        if (database == null)
        {
            quantidadeJornais = value;
            OnNewspaperCountChanged?.Invoke(quantidadeJornais);
            return;
        }

        if (quantidadeJornais == value)
            return;

        quantidadeJornais = value;
        database.DefinirStatusPropriedade(
            InventoryRecordId,
            "Inventário - Jornais",
            false,
            true,
            CountPrefix + quantidadeJornais.ToString(CultureInfo.InvariantCulture)
        );

        if (salvarImediatamente)
            database.SalvarSePendenteOuForcar();

        OnNewspaperCountChanged?.Invoke(quantidadeJornais);

        if (logarEventos)
            Debug.Log("[NewspaperInventory] Jornais: " + quantidadeJornais, this);
    }

    private void ResolveDatabase()
    {
        if (database != null)
            return;

        database = MiniMarketPlayerDatabase.Instance;
        if (database == null && Application.isPlaying)
            database = MiniMarketPlayerDatabase.ObterOuCriar();

        Subscribe();
    }

    private void Subscribe()
    {
        if (subscribed || database == null)
            return;

        database.OnDatabaseChanged += HandleDatabaseChanged;
        subscribed = true;
    }

    private void Unsubscribe()
    {
        if (!subscribed)
            return;

        if (database != null)
            database.OnDatabaseChanged -= HandleDatabaseChanged;

        subscribed = false;
    }

    private void HandleDatabaseChanged(MiniMarketPlayerDatabase.MiniMarketPlayerData data)
    {
        RefreshFromDatabase(true);
    }

    private void RefreshFromDatabase(bool notify)
    {
        ResolveDatabase();
        if (database == null)
        {
            if (quantidadeJornais < 0)
                quantidadeJornais = Mathf.Max(0, quantidadeInicial);
            return;
        }

        MiniMarketPlayerDatabase.MiniMarketPropertyState state =
            database.ObterPropriedade(InventoryRecordId);

        int loaded = state == null
            ? Mathf.Max(0, quantidadeInicial)
            : ParseCount(state.status);

        if (state == null)
        {
            quantidadeJornais = loaded;
            database.DefinirStatusPropriedade(
                InventoryRecordId,
                "Inventário - Jornais",
                false,
                true,
                CountPrefix + loaded.ToString(CultureInfo.InvariantCulture)
            );

            if (salvarImediatamente)
                database.SalvarSePendenteOuForcar();
        }

        if (quantidadeJornais == loaded)
            return;

        quantidadeJornais = loaded;
        if (notify)
            OnNewspaperCountChanged?.Invoke(quantidadeJornais);
    }

    private static int ParseCount(string status)
    {
        if (string.IsNullOrWhiteSpace(status))
            return 0;

        string trimmed = status.Trim();
        if (trimmed.StartsWith(CountPrefix, StringComparison.OrdinalIgnoreCase))
            trimmed = trimmed.Substring(CountPrefix.Length);

        return int.TryParse(
            trimmed,
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out int parsed)
            ? Mathf.Max(0, parsed)
            : 0;
    }

    private static string BuildPlaceRecordId(string placeId)
    {
        if (string.IsNullOrWhiteSpace(placeId))
            placeId = "DEFAULT";

        string normalized = placeId.Trim().ToUpperInvariant();
        normalized = normalized.Replace(' ', '_').Replace('/', '_').Replace('\\', '_');
        return PlacePrefix + normalized;
    }
}
