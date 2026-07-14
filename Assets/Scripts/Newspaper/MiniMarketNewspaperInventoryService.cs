using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

/// <summary>
/// Inventário provisório de jornais armazenado dentro do banco principal do jogador.
///
/// As alterações de quantidade e de locais ocupados são agrupadas e gravadas uma única
/// vez após o frame de interação. Isso evita executar serialização, criptografia e escrita
/// em disco duas ou três vezes ao pegar/colocar o mesmo jornal.
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

    [Tooltip("Mantido por compatibilidade. Marcado: agenda a gravação logo após a interação, sem bloquear o mesmo frame.")]
    public bool salvarImediatamente = true;

    [Tooltip("Tempo usado para agrupar alterações consecutivas em uma única gravação criptografada.")]
    [Min(0.05f)] public float atrasoSalvamentoCoalescido = 0.35f;

    public bool logarEventos;

    private MiniMarketPlayerDatabase database;
    private int quantidadeJornais = -1;
    private bool subscribed;
    private bool saveQueued;
    private float saveAtUnscaledTime;

    public event Action<int> OnNewspaperCountChanged;

    public int QuantidadeJornais
    {
        get
        {
            EnsureLoaded();
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

        if (transform.parent != null)
            transform.SetParent(null);

        DontDestroyOnLoad(gameObject);
        ResolveDatabase();
        RefreshFromDatabase(true, true);
    }

    private void OnEnable()
    {
        ResolveDatabase();
        Subscribe();
        RefreshFromDatabase(true, true);
    }

    private void Update()
    {
        if (!saveQueued || database == null)
            return;

        if (Time.unscaledTime >= saveAtUnscaledTime)
            FlushPendingSave();
    }

    private void OnApplicationPause(bool paused)
    {
        if (paused)
            FlushPendingSave();
    }

    private void OnApplicationFocus(bool focused)
    {
        if (!focused)
            FlushPendingSave();
    }

    private void OnApplicationQuit()
    {
        FlushPendingSave();
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    private void OnDestroy()
    {
        FlushPendingSave();
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
        EnsureLoaded();
        MiniMarketPlayerDatabase.MiniMarketPropertyState state =
            FindRecord(BuildPlaceRecordId(placeId));

        return state != null &&
               string.Equals(state.status, PlacedStatus, StringComparison.OrdinalIgnoreCase);
    }

    public void DefinirJornalNoLocal(string placeId, bool colocado)
    {
        EnsureLoaded();
        if (database == null)
            return;

        string recordId = BuildPlaceRecordId(placeId);
        MiniMarketPlayerDatabase.MiniMarketPropertyState state = GetOrCreateRecord(
            recordId,
            "Expositor de jornal"
        );

        string nextStatus = colocado ? PlacedStatus : EmptyStatus;
        if (state.status == nextStatus && !state.comprada && state.disponivel)
            return;

        state.comprada = false;
        state.disponivel = true;
        state.status = nextStatus;
        state.lastUpdatedUnix = GetUnixNow();

        TouchDatabaseTimestamp();
        QueueSave();
    }

    [ContextMenu("Jornal/Salvar alterações pendentes")]
    public void FlushPendingSave()
    {
        if (!saveQueued)
            return;

        saveQueued = false;

        if (database == null || MiniMarketPlayerDatabase.EncerrandoAplicacao)
            return;

        database.Salvar();
    }

    private void SetNewspaperCount(int value)
    {
        EnsureLoaded();
        value = Mathf.Max(0, value);

        if (quantidadeJornais == value)
            return;

        quantidadeJornais = value;

        if (database != null)
        {
            MiniMarketPlayerDatabase.MiniMarketPropertyState state = GetOrCreateRecord(
                InventoryRecordId,
                "Inventário - Jornais"
            );

            state.comprada = false;
            state.disponivel = true;
            state.status = CountPrefix + quantidadeJornais.ToString(CultureInfo.InvariantCulture);
            state.lastUpdatedUnix = GetUnixNow();

            TouchDatabaseTimestamp();
            QueueSave();
        }

        OnNewspaperCountChanged?.Invoke(quantidadeJornais);

        if (logarEventos)
            Debug.Log("[NewspaperInventory] Jornais: " + quantidadeJornais, this);
    }

    private void EnsureLoaded()
    {
        ResolveDatabase();

        if (quantidadeJornais < 0)
            RefreshFromDatabase(false, true);
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
        RefreshFromDatabase(true, false);
    }

    private void RefreshFromDatabase(bool notify, bool createMissingRecord)
    {
        ResolveDatabase();

        if (database == null)
        {
            if (quantidadeJornais < 0)
                quantidadeJornais = Mathf.Max(0, quantidadeInicial);
            return;
        }

        MiniMarketPlayerDatabase.MiniMarketPropertyState state = FindRecord(InventoryRecordId);
        int loaded = state == null
            ? Mathf.Max(0, quantidadeInicial)
            : ParseCount(state.status);

        if (state == null && createMissingRecord)
        {
            state = GetOrCreateRecord(InventoryRecordId, "Inventário - Jornais");
            state.comprada = false;
            state.disponivel = true;
            state.status = CountPrefix + loaded.ToString(CultureInfo.InvariantCulture);
            state.lastUpdatedUnix = GetUnixNow();
            TouchDatabaseTimestamp();
            QueueSave();
        }

        if (quantidadeJornais == loaded)
            return;

        quantidadeJornais = loaded;

        if (notify)
            OnNewspaperCountChanged?.Invoke(quantidadeJornais);
    }

    private MiniMarketPlayerDatabase.MiniMarketPropertyState FindRecord(string recordId)
    {
        if (database == null || string.IsNullOrEmpty(recordId))
            return null;

        List<MiniMarketPlayerDatabase.MiniMarketPropertyState> records =
            database.Dados.propriedades;

        if (records == null)
            return null;

        for (int i = 0; i < records.Count; i++)
        {
            MiniMarketPlayerDatabase.MiniMarketPropertyState state = records[i];
            if (state != null && state.areaId == recordId)
                return state;
        }

        return null;
    }

    private MiniMarketPlayerDatabase.MiniMarketPropertyState GetOrCreateRecord(
        string recordId,
        string displayName)
    {
        MiniMarketPlayerDatabase.MiniMarketPropertyState existing = FindRecord(recordId);
        if (existing != null)
        {
            if (!string.IsNullOrWhiteSpace(displayName))
                existing.nome = displayName;
            return existing;
        }

        MiniMarketPlayerDatabase.MiniMarketPlayerData data = database.Dados;
        if (data.propriedades == null)
            data.propriedades = new List<MiniMarketPlayerDatabase.MiniMarketPropertyState>();

        MiniMarketPlayerDatabase.MiniMarketPropertyState created =
            new MiniMarketPlayerDatabase.MiniMarketPropertyState
            {
                areaId = recordId,
                nome = string.IsNullOrWhiteSpace(displayName) ? recordId : displayName,
                comprada = false,
                disponivel = true,
                status = string.Empty,
                lastUpdatedUnix = GetUnixNow()
            };

        data.propriedades.Add(created);
        return created;
    }

    private void TouchDatabaseTimestamp()
    {
        if (database != null && database.Dados != null)
            database.Dados.lastUpdatedUnix = GetUnixNow();
    }

    private void QueueSave()
    {
        if (database == null)
            return;

        saveQueued = true;
        float delay = salvarImediatamente
            ? Mathf.Max(0.05f, atrasoSalvamentoCoalescido)
            : Mathf.Max(1f, atrasoSalvamentoCoalescido);

        saveAtUnscaledTime = Time.unscaledTime + delay;
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

    private static long GetUnixNow()
    {
        return DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    }

    private void OnValidate()
    {
        quantidadeInicial = Mathf.Max(0, quantidadeInicial);
        atrasoSalvamentoCoalescido = Mathf.Max(0.05f, atrasoSalvamentoCoalescido);
    }
}
