using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

/// <summary>
/// Fonte única de verdade dos dados locais do jogador.
///
/// Salva nome, gold, gemas, stamina/energia segmentada, empresas, propriedades,
/// última cena/posição e tempo jogado. O formato V2 migra automaticamente saves V1,
/// usa gravação atômica com backup e evita referências serializadas entre cenas.
/// </summary>
[DefaultExecutionOrder(-10000)]
[DisallowMultipleComponent]
public class MiniMarketPlayerDatabase : MonoBehaviour
{
    public static MiniMarketPlayerDatabase Instance { get; private set; }
    public static bool EncerrandoAplicacao => encerrandoAplicacao;

    private const int VersaoSchemaAtual = 2;
    private const string NomeArquivoBanco = "player_database.mmdb";
    private const string PrefixoArquivoV1 = "MMDB1";
    private const string PrefixoArquivoV2 = "MMDB2";
    private const string SaltTexto = "MiniMarket.LocalSecureDatabase.v2.ErickIsrael";

    private const string KeySegmentosAtuaisLegado = "MiniMarket.Player.StaminaSegmentosAtuais";
    private const string KeySegmentosMaximosLegado = "MiniMarket.Player.StaminaSegmentosMaximos";
    private const string KeyRecargaReservaLegado = "MiniMarket.Player.StaminaRecargaReserva";

    [Header("Identificação")]
    public string playerIdPadrao = "LOCAL_PLAYER_001";
    public string nomePadrao = "Player";

    [Header("Valores iniciais")]
    [Min(0)] public int goldInicialPadrao = 20000;
    [Min(0)] public int gemasIniciaisPadrao;
    [Min(1f)] public float staminaMaximaPadrao = 100f;
    [Min(1)] public int energiaSegmentosMaximosPadrao = 5;

    [Header("Banco local")]
    public bool salvarAutomaticamente = true;
    public bool usarCriptografiaLocal = true;
    public bool usarSalvamentoDiferido = true;
    [Min(0.25f)] public float intervaloSalvamentoDiferido = 8f;
    public bool salvarAoSairOuPerderFoco = true;
    public bool criarBackupAntesDeSubstituir = true;
    public bool logarEventos;

    private static bool encerrandoAplicacao;

    private MiniMarketPlayerData dados;
    private string caminhoBanco;
    private string caminhoBackup;
    private bool carregado;
    private bool salvamentoPendente;
    private float proximoSalvamentoPermitido;
    private bool destruindo;
    private float ultimoTempoRealtime;

    public event Action<MiniMarketPlayerData> OnDatabaseChanged;

    public MiniMarketPlayerData Dados
    {
        get
        {
            GarantirCarregado();
            return dados;
        }
    }

    public string NomePersonagem => Dados.nome;
    public int GoldAtual => Dados.gold;
    public int GemasAtuais => Dados.gemas;
    public float StaminaAtual => Dados.staminaAtual;
    public float StaminaMaxima => Dados.staminaMaxima;
    public int EnergiaSegmentosAtuais => Dados.energiaSegmentosAtuais;
    public int EnergiaSegmentosMaximos => Dados.energiaSegmentosMaximos;
    public float EnergiaRecargaReserva => Dados.energiaRecargaReserva;
    public float EnergiaPercentual01 => CalcularEnergiaPercentual01(Dados);
    public int EmpresasCompradas => Dados.empresasCompradas != null ? Dados.empresasCompradas.Count : 0;
    public bool SalvamentoPendente => salvamentoPendente;
    public string CaminhoBanco => caminhoBanco;
    public string CaminhoBackup => caminhoBackup;

    [Serializable]
    public class MiniMarketPlayerData
    {
        public int schemaVersion = VersaoSchemaAtual;
        public string playerId = "LOCAL_PLAYER_001";
        public string nome = "Player";

        public bool goldInicializado;
        public int gold;
        public int gemas;

        public bool staminaInicializada;
        public float staminaAtual = 100f;
        public float staminaMaxima = 100f;
        public int energiaSegmentosAtuais = 5;
        public int energiaSegmentosMaximos = 5;
        public float energiaRecargaReserva;

        public List<string> empresasCompradas = new List<string>();
        public List<MiniMarketPropertyState> propriedades = new List<MiniMarketPropertyState>();

        public string ultimaCena = string.Empty;
        public Vector3 ultimaPosicao;
        public Vector3 ultimaRotacaoEuler;
        public bool possuiPosicaoSalva;
        public double tempoJogadoSegundos;

        public long createdAtUnix;
        public long lastUpdatedUnix;
    }

    [Serializable]
    public class MiniMarketPropertyState
    {
        public string areaId;
        public string nome;
        public bool comprada;
        public bool disponivel;
        public string status;
        public long lastUpdatedUnix;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetarStaticsAoEntrarNoPlay()
    {
        Instance = null;
        encerrandoAplicacao = false;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void CriarBancoAntesDaCena()
    {
        if (Instance != null)
            return;

        GameObject go = new GameObject("MiniMarket_PlayerDatabase");
        go.AddComponent<MiniMarketPlayerDatabase>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        encerrandoAplicacao = false;

        if (transform.parent != null)
            transform.SetParent(null);

        DontDestroyOnLoad(gameObject);
        GarantirCarregado();
        ultimoTempoRealtime = Time.realtimeSinceStartup;
    }

    private void Update()
    {
        if (encerrandoAplicacao)
            return;

        ProcessarSalvamentoDiferido();

        float agora = Time.realtimeSinceStartup;
        float delta = Mathf.Clamp(agora - ultimoTempoRealtime, 0f, 1f);
        ultimoTempoRealtime = agora;

        if (dados != null && delta > 0f)
        {
            dados.tempoJogadoSegundos += delta;
            salvamentoPendente = true;
        }
    }

    private void OnApplicationPause(bool pause)
    {
        if (pause && salvarAoSairOuPerderFoco)
            SalvarSePendenteOuForcar();
    }

    private void OnApplicationFocus(bool focus)
    {
        if (!focus && salvarAoSairOuPerderFoco && !encerrandoAplicacao)
            SalvarSePendenteOuForcar();

        ultimoTempoRealtime = Time.realtimeSinceStartup;
    }

    private void OnApplicationQuit()
    {
        encerrandoAplicacao = true;

        if (salvarAoSairOuPerderFoco)
            SalvarSePendenteOuForcar();
    }

    private void OnDisable()
    {
        if (!Application.isPlaying)
            encerrandoAplicacao = true;
    }

    private void OnDestroy()
    {
        if (destruindo)
            return;

        destruindo = true;

        if (Instance == this)
        {
            if (salvarAoSairOuPerderFoco && carregado && dados != null)
                SalvarSePendenteOuForcar();

            Instance = null;
        }
    }

    public static MiniMarketPlayerDatabase ObterOuCriar()
    {
        if (Instance != null)
            return Instance;

        if (encerrandoAplicacao || !Application.isPlaying)
            return null;

        MiniMarketPlayerDatabase encontrado =
            UnityEngine.Object.FindAnyObjectByType<MiniMarketPlayerDatabase>(FindObjectsInactive.Include);

        if (encontrado != null)
        {
            Instance = encontrado;
            encontrado.GarantirCarregado();
            return Instance;
        }

        GameObject go = new GameObject("MiniMarket_PlayerDatabase");
        return go.AddComponent<MiniMarketPlayerDatabase>();
    }

    public void GarantirCarregado()
    {
        if (carregado && dados != null)
            return;

        caminhoBanco = Path.Combine(Application.persistentDataPath, NomeArquivoBanco);
        caminhoBackup = caminhoBanco + ".bak";

        bool precisaSalvarMigracao;
        dados = CarregarDoDisco(out precisaSalvarMigracao);
        NormalizarDados(ref precisaSalvarMigracao);
        carregado = true;

        if (precisaSalvarMigracao)
            Salvar();
    }

    private MiniMarketPlayerData CarregarDoDisco(out bool precisaSalvarMigracao)
    {
        precisaSalvarMigracao = false;

        if (!File.Exists(caminhoBanco))
            return CriarDadosPadrao();

        try
        {
            string conteudo = File.ReadAllText(caminhoBanco, Encoding.UTF8);
            string json = usarCriptografiaLocal ? DescriptografarConteudo(conteudo, out bool eraV1) : conteudo;
            MiniMarketPlayerData carregadoDoDisco = JsonUtility.FromJson<MiniMarketPlayerData>(json);

            if (carregadoDoDisco == null)
                throw new InvalidDataException("JSON do banco retornou null.");

            precisaSalvarMigracao = eraV1 || carregadoDoDisco.schemaVersion < VersaoSchemaAtual;

            if (logarEventos)
                Debug.Log("[PlayerDatabase] Banco carregado: " + caminhoBanco);

            return carregadoDoDisco;
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[PlayerDatabase] Falha ao carregar save principal: " + ex.Message);

            MiniMarketPlayerData backup = TentarCarregarBackup();
            if (backup != null)
            {
                precisaSalvarMigracao = true;
                return backup;
            }

            FazerBackupArquivoCorrompido();
            precisaSalvarMigracao = true;
            return CriarDadosPadrao();
        }
    }

    private MiniMarketPlayerData TentarCarregarBackup()
    {
        if (string.IsNullOrEmpty(caminhoBackup) || !File.Exists(caminhoBackup))
            return null;

        try
        {
            string conteudo = File.ReadAllText(caminhoBackup, Encoding.UTF8);
            string json = usarCriptografiaLocal ? DescriptografarConteudo(conteudo, out _) : conteudo;
            return JsonUtility.FromJson<MiniMarketPlayerData>(json);
        }
        catch
        {
            return null;
        }
    }

    private MiniMarketPlayerData CriarDadosPadrao()
    {
        long agora = ObterUnixAgora();
        int maxSegmentos = Mathf.Max(1, energiaSegmentosMaximosPadrao);

        return new MiniMarketPlayerData
        {
            schemaVersion = VersaoSchemaAtual,
            playerId = string.IsNullOrWhiteSpace(playerIdPadrao) ? "LOCAL_PLAYER_001" : playerIdPadrao.Trim(),
            nome = string.IsNullOrWhiteSpace(nomePadrao) ? "Player" : nomePadrao.Trim(),
            goldInicializado = false,
            gold = Mathf.Max(0, goldInicialPadrao),
            gemas = Mathf.Max(0, gemasIniciaisPadrao),
            staminaInicializada = false,
            staminaMaxima = Mathf.Max(1f, staminaMaximaPadrao),
            staminaAtual = Mathf.Max(1f, staminaMaximaPadrao),
            energiaSegmentosAtuais = maxSegmentos,
            energiaSegmentosMaximos = maxSegmentos,
            energiaRecargaReserva = 0f,
            empresasCompradas = new List<string>(),
            propriedades = new List<MiniMarketPropertyState>(),
            createdAtUnix = agora,
            lastUpdatedUnix = agora
        };
    }

    private void NormalizarDados(ref bool mudou)
    {
        if (dados == null)
        {
            dados = CriarDadosPadrao();
            mudou = true;
        }

        if (dados.schemaVersion < VersaoSchemaAtual)
        {
            MigrarDadosLegados();
            mudou = true;
        }

        dados.schemaVersion = VersaoSchemaAtual;

        if (string.IsNullOrWhiteSpace(dados.playerId))
        {
            dados.playerId = string.IsNullOrWhiteSpace(playerIdPadrao) ? "LOCAL_PLAYER_001" : playerIdPadrao.Trim();
            mudou = true;
        }

        if (string.IsNullOrWhiteSpace(dados.nome))
        {
            dados.nome = string.IsNullOrWhiteSpace(nomePadrao) ? "Player" : nomePadrao.Trim();
            mudou = true;
        }

        dados.gold = Mathf.Max(0, dados.gold);
        dados.gemas = Mathf.Max(0, dados.gemas);
        dados.staminaMaxima = Mathf.Max(1f, dados.staminaMaxima);
        dados.staminaAtual = Mathf.Clamp(dados.staminaAtual, 0f, dados.staminaMaxima);
        dados.energiaSegmentosMaximos = Mathf.Max(1, dados.energiaSegmentosMaximos);
        dados.energiaSegmentosAtuais = Mathf.Clamp(
            dados.energiaSegmentosAtuais,
            0,
            dados.energiaSegmentosMaximos
        );
        dados.energiaRecargaReserva = Mathf.Clamp(
            dados.energiaRecargaReserva,
            0f,
            dados.staminaMaxima
        );

        if (dados.empresasCompradas == null)
        {
            dados.empresasCompradas = new List<string>();
            mudou = true;
        }

        if (dados.propriedades == null)
        {
            dados.propriedades = new List<MiniMarketPropertyState>();
            mudou = true;
        }

        if (dados.createdAtUnix <= 0)
        {
            dados.createdAtUnix = ObterUnixAgora();
            mudou = true;
        }

        RemoverDuplicatasEmpresas();
        dados.lastUpdatedUnix = ObterUnixAgora();
    }

    private void MigrarDadosLegados()
    {
        int maximo = PlayerPrefs.GetInt(
            KeySegmentosMaximosLegado,
            Mathf.Max(1, energiaSegmentosMaximosPadrao)
        );

        int atual = PlayerPrefs.GetInt(KeySegmentosAtuaisLegado, maximo);
        float reserva = PlayerPrefs.GetFloat(KeyRecargaReservaLegado, 0f);

        dados.energiaSegmentosMaximos = Mathf.Max(1, maximo);
        dados.energiaSegmentosAtuais = Mathf.Clamp(atual, 0, dados.energiaSegmentosMaximos);
        dados.energiaRecargaReserva = Mathf.Clamp(reserva, 0f, Mathf.Max(1f, dados.staminaMaxima));

        if (dados.energiaSegmentosAtuais == 0 && dados.staminaAtual > 0.001f)
            dados.energiaSegmentosAtuais = 1;
    }

    public void Salvar()
    {
        if (!carregado || dados == null)
            return;

        try
        {
            string diretorio = Path.GetDirectoryName(caminhoBanco);
            if (!string.IsNullOrEmpty(diretorio))
                Directory.CreateDirectory(diretorio);

            dados.schemaVersion = VersaoSchemaAtual;
            dados.lastUpdatedUnix = ObterUnixAgora();

            string json = JsonUtility.ToJson(dados, true);
            string conteudo = usarCriptografiaLocal ? CriptografarConteudo(json) : json;
            string temporario = caminhoBanco + ".tmp";

            File.WriteAllText(temporario, conteudo, new UTF8Encoding(false));

            if (File.Exists(caminhoBanco))
            {
                bool substituido = false;

                try
                {
                    File.Replace(
                        temporario,
                        caminhoBanco,
                        criarBackupAntesDeSubstituir ? caminhoBackup : null,
                        true
                    );
                    substituido = true;
                }
                catch
                {
                    // Android/WebGL e alguns file systems não suportam File.Replace.
                }

                if (!substituido)
                {
                    if (criarBackupAntesDeSubstituir)
                        File.Copy(caminhoBanco, caminhoBackup, true);

                    File.Delete(caminhoBanco);
                    File.Move(temporario, caminhoBanco);
                }
            }
            else
            {
                File.Move(temporario, caminhoBanco);
            }

            salvamentoPendente = false;
            proximoSalvamentoPermitido = Time.unscaledTime + Mathf.Max(0.25f, intervaloSalvamentoDiferido);

            if (logarEventos)
                Debug.Log("[PlayerDatabase] Banco salvo: " + caminhoBanco);
        }
        catch (Exception ex)
        {
            Debug.LogError("[PlayerDatabase] Falha ao salvar banco: " + ex.Message);
        }
    }

    public void SalvarSePendenteOuForcar()
    {
        if (!carregado || dados == null)
            return;

        if (salvamentoPendente || salvarAutomaticamente)
            Salvar();
    }

    private void ProcessarSalvamentoDiferido()
    {
        if (!salvarAutomaticamente || !usarSalvamentoDiferido || !salvamentoPendente)
            return;

        if (Time.unscaledTime >= proximoSalvamentoPermitido)
            Salvar();
    }

    private void NotificarAlteracao(bool salvarAgora)
    {
        if (dados == null)
            return;

        dados.lastUpdatedUnix = ObterUnixAgora();
        salvamentoPendente = true;

        if (salvarAutomaticamente && (salvarAgora || !usarSalvamentoDiferido))
            Salvar();

        OnDatabaseChanged?.Invoke(dados);
    }

    public void GarantirGoldInicial(int goldInicial)
    {
        GarantirCarregado();
        if (dados.goldInicializado)
            return;

        dados.gold = Mathf.Max(0, goldInicial);
        dados.goldInicializado = true;
        NotificarAlteracao(true);
    }

    public int DefinirGold(int novoGold)
    {
        GarantirCarregado();
        dados.gold = Mathf.Max(0, novoGold);
        dados.goldInicializado = true;
        NotificarAlteracao(true);
        return dados.gold;
    }

    public int AdicionarGold(int quantidade)
    {
        GarantirCarregado();
        if (quantidade <= 0)
            return dados.gold;

        dados.gold = Mathf.Max(0, dados.gold + quantidade);
        dados.goldInicializado = true;
        NotificarAlteracao(true);
        return dados.gold;
    }

    public bool RemoverGold(int quantidade)
    {
        GarantirCarregado();
        if (quantidade <= 0)
            return true;
        if (dados.gold < quantidade)
            return false;

        dados.gold -= quantidade;
        dados.goldInicializado = true;
        NotificarAlteracao(true);
        return true;
    }

    public bool TemGoldSuficiente(int quantidade)
    {
        GarantirCarregado();
        return dados.gold >= Mathf.Max(0, quantidade);
    }

    public int DefinirGemas(int valor)
    {
        GarantirCarregado();
        dados.gemas = Mathf.Max(0, valor);
        NotificarAlteracao(true);
        return dados.gemas;
    }

    public int AdicionarGemas(int quantidade)
    {
        GarantirCarregado();
        if (quantidade > 0)
        {
            dados.gemas += quantidade;
            NotificarAlteracao(true);
        }
        return dados.gemas;
    }

    public bool RemoverGemas(int quantidade)
    {
        GarantirCarregado();
        if (quantidade <= 0)
            return true;
        if (dados.gemas < quantidade)
            return false;

        dados.gemas -= quantidade;
        NotificarAlteracao(true);
        return true;
    }

    public void GarantirStaminaInicial(float staminaAtualInicial, float staminaMaximaInicial)
    {
        GarantirCarregado();
        if (dados.staminaInicializada)
            return;

        dados.staminaMaxima = Mathf.Max(1f, staminaMaximaInicial);
        dados.staminaAtual = Mathf.Clamp(staminaAtualInicial, 0f, dados.staminaMaxima);
        dados.energiaSegmentosMaximos = Mathf.Max(1, energiaSegmentosMaximosPadrao);
        dados.energiaSegmentosAtuais = dados.staminaAtual > 0f ? dados.energiaSegmentosMaximos : 0;
        dados.energiaRecargaReserva = 0f;
        dados.staminaInicializada = true;
        NotificarAlteracao(true);
    }

    public void DefinirStamina(float atual, float maxima)
    {
        DefinirStamina(atual, maxima, false);
    }

    public void DefinirStamina(float atual, float maxima, bool salvarAgora)
    {
        GarantirCarregado();
        dados.staminaMaxima = Mathf.Max(1f, maxima);
        dados.staminaAtual = Mathf.Clamp(atual, 0f, dados.staminaMaxima);
        dados.staminaInicializada = true;

        if (dados.staminaAtual > 0f && dados.energiaSegmentosAtuais <= 0)
            dados.energiaSegmentosAtuais = 1;

        NotificarAlteracao(salvarAgora);
    }

    public void DefinirStaminaAtual(float atual)
    {
        DefinirStaminaAtual(atual, false);
    }

    public void DefinirStaminaAtual(float atual, bool salvarAgora)
    {
        GarantirCarregado();
        dados.staminaAtual = Mathf.Clamp(atual, 0f, Mathf.Max(1f, dados.staminaMaxima));
        dados.staminaInicializada = true;
        NotificarAlteracao(salvarAgora);
    }

    public void DefinirEnergiaSegmentada(
        float staminaAtual,
        float staminaMaxima,
        int segmentosAtuais,
        int segmentosMaximos,
        float recargaReserva,
        bool salvarAgora = false)
    {
        GarantirCarregado();

        dados.staminaMaxima = Mathf.Max(1f, staminaMaxima);
        dados.staminaAtual = Mathf.Clamp(staminaAtual, 0f, dados.staminaMaxima);
        dados.energiaSegmentosMaximos = Mathf.Max(1, segmentosMaximos);
        dados.energiaSegmentosAtuais = Mathf.Clamp(
            segmentosAtuais,
            0,
            dados.energiaSegmentosMaximos
        );
        dados.energiaRecargaReserva = Mathf.Clamp(recargaReserva, 0f, dados.staminaMaxima);
        dados.staminaInicializada = true;
        NotificarAlteracao(salvarAgora);
    }

    public void RestaurarStaminaCompleta()
    {
        RestaurarEnergiaCompleta();
    }

    public void RestaurarEnergiaCompleta()
    {
        GarantirCarregado();
        dados.staminaMaxima = Mathf.Max(1f, dados.staminaMaxima);
        dados.staminaAtual = dados.staminaMaxima;
        dados.energiaSegmentosMaximos = Mathf.Max(1, dados.energiaSegmentosMaximos);
        dados.energiaSegmentosAtuais = dados.energiaSegmentosMaximos;
        dados.energiaRecargaReserva = 0f;
        dados.staminaInicializada = true;
        NotificarAlteracao(true);
    }

    public float ObterPercentualStamina01()
    {
        GarantirCarregado();
        return dados.staminaMaxima <= 0.001f
            ? 0f
            : Mathf.Clamp01(dados.staminaAtual / dados.staminaMaxima);
    }

    public void DefinirNome(string nome)
    {
        GarantirCarregado();
        dados.nome = string.IsNullOrWhiteSpace(nome) ? nomePadrao : nome.Trim();
        NotificarAlteracao(true);
    }

    public bool EmpresaJaComprada(string empresaId)
    {
        GarantirCarregado();
        empresaId = NormalizarId(empresaId);
        return !string.IsNullOrEmpty(empresaId) && dados.empresasCompradas.Contains(empresaId);
    }

    public bool RegistrarEmpresaComprada(string empresaId)
    {
        GarantirCarregado();
        empresaId = NormalizarId(empresaId);

        if (string.IsNullOrEmpty(empresaId) || dados.empresasCompradas.Contains(empresaId))
            return false;

        dados.empresasCompradas.Add(empresaId);
        NotificarAlteracao(true);
        return true;
    }

    public void ResetarEmpresasCompradas()
    {
        GarantirCarregado();
        dados.empresasCompradas.Clear();

        for (int i = 0; i < dados.propriedades.Count; i++)
        {
            MiniMarketPropertyState propriedade = dados.propriedades[i];
            if (propriedade == null)
                continue;

            propriedade.comprada = false;
            propriedade.disponivel = true;
            propriedade.status = "Disponivel";
            propriedade.lastUpdatedUnix = ObterUnixAgora();
        }

        NotificarAlteracao(true);
    }

    public bool PropriedadeComprada(string areaId)
    {
        MiniMarketPropertyState propriedade = ObterPropriedade(areaId);
        return propriedade != null && propriedade.comprada;
    }

    public bool PropriedadeIndisponivel(string areaId)
    {
        MiniMarketPropertyState propriedade = ObterPropriedade(areaId);
        return propriedade != null && !propriedade.disponivel;
    }

    public MiniMarketPropertyState RegistrarPropriedadeComprada(string areaId, string nome)
    {
        GarantirCarregado();
        areaId = NormalizarId(areaId);
        if (string.IsNullOrEmpty(areaId))
            return null;

        MiniMarketPropertyState propriedade = ObterOuCriarPropriedade(areaId, nome);
        propriedade.comprada = true;
        propriedade.disponivel = false;
        propriedade.status = "CompradaIndisponivel";
        propriedade.lastUpdatedUnix = ObterUnixAgora();

        if (!dados.empresasCompradas.Contains(areaId))
            dados.empresasCompradas.Add(areaId);

        NotificarAlteracao(true);
        return propriedade;
    }

    public MiniMarketPropertyState DefinirStatusPropriedade(
        string areaId,
        string nome,
        bool comprada,
        bool disponivel,
        string status)
    {
        GarantirCarregado();
        areaId = NormalizarId(areaId);
        if (string.IsNullOrEmpty(areaId))
            return null;

        MiniMarketPropertyState propriedade = ObterOuCriarPropriedade(areaId, nome);
        propriedade.comprada = comprada;
        propriedade.disponivel = disponivel;
        propriedade.status = string.IsNullOrWhiteSpace(status)
            ? (disponivel ? "Disponivel" : "Indisponivel")
            : status.Trim();
        propriedade.lastUpdatedUnix = ObterUnixAgora();

        if (comprada && !dados.empresasCompradas.Contains(areaId))
            dados.empresasCompradas.Add(areaId);

        NotificarAlteracao(true);
        return propriedade;
    }

    public MiniMarketPropertyState ObterPropriedade(string areaId)
    {
        GarantirCarregado();
        areaId = NormalizarId(areaId);
        if (string.IsNullOrEmpty(areaId))
            return null;

        for (int i = 0; i < dados.propriedades.Count; i++)
        {
            MiniMarketPropertyState propriedade = dados.propriedades[i];
            if (propriedade != null && propriedade.areaId == areaId)
                return propriedade;
        }

        return null;
    }

    private MiniMarketPropertyState ObterOuCriarPropriedade(string areaId, string nome)
    {
        MiniMarketPropertyState propriedade = ObterPropriedade(areaId);
        if (propriedade != null)
        {
            if (!string.IsNullOrWhiteSpace(nome))
                propriedade.nome = nome.Trim();
            return propriedade;
        }

        propriedade = new MiniMarketPropertyState
        {
            areaId = areaId,
            nome = string.IsNullOrWhiteSpace(nome) ? areaId : nome.Trim(),
            comprada = false,
            disponivel = true,
            status = "Disponivel",
            lastUpdatedUnix = ObterUnixAgora()
        };

        dados.propriedades.Add(propriedade);
        return propriedade;
    }

    public void SalvarPosicaoMundo(string cena, Vector3 posicao, Vector3 rotacaoEuler, bool salvarAgora = false)
    {
        GarantirCarregado();
        dados.ultimaCena = cena ?? string.Empty;
        dados.ultimaPosicao = posicao;
        dados.ultimaRotacaoEuler = rotacaoEuler;
        dados.possuiPosicaoSalva = true;
        NotificarAlteracao(salvarAgora);
    }

    public bool TentarObterPosicaoMundo(out string cena, out Vector3 posicao, out Vector3 rotacaoEuler)
    {
        GarantirCarregado();
        cena = dados.ultimaCena;
        posicao = dados.ultimaPosicao;
        rotacaoEuler = dados.ultimaRotacaoEuler;
        return dados.possuiPosicaoSalva;
    }

    [ContextMenu("Banco/Salvar agora")]
    public void ContextSalvarAgora()
    {
        Salvar();
    }

    [ContextMenu("Banco/Restaurar energia completa")]
    public void ContextRestaurarEnergia()
    {
        RestaurarEnergiaCompleta();
    }

    [ContextMenu("Banco/Resetar banco local")]
    public void ResetarBancoLocal()
    {
        dados = CriarDadosPadrao();
        carregado = true;
        salvamentoPendente = true;
        Salvar();
        OnDatabaseChanged?.Invoke(dados);
    }

    private void RemoverDuplicatasEmpresas()
    {
        HashSet<string> unicas = new HashSet<string>();
        List<string> limpas = new List<string>();

        for (int i = 0; i < dados.empresasCompradas.Count; i++)
        {
            string id = NormalizarId(dados.empresasCompradas[i]);
            if (string.IsNullOrEmpty(id) || !unicas.Add(id))
                continue;

            limpas.Add(id);
        }

        dados.empresasCompradas = limpas;
    }

    public string NormalizarIdPublico(string id)
    {
        return NormalizarId(id);
    }

    private string NormalizarId(string id)
    {
        return string.IsNullOrWhiteSpace(id)
            ? string.Empty
            : id.Trim().ToUpperInvariant().Replace(' ', '_');
    }

    private float CalcularEnergiaPercentual01(MiniMarketPlayerData data)
    {
        if (data == null || data.energiaSegmentosMaximos <= 0 || data.staminaMaxima <= 0f)
            return 0f;

        float barrasCompletas = Mathf.Max(0, data.energiaSegmentosAtuais - 1);
        float barraAtual = data.energiaSegmentosAtuais > 0
            ? Mathf.Clamp01(data.staminaAtual / data.staminaMaxima)
            : 0f;

        return Mathf.Clamp01((barrasCompletas + barraAtual) / data.energiaSegmentosMaximos);
    }

    private long ObterUnixAgora()
    {
        return DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    }

    private void FazerBackupArquivoCorrompido()
    {
        try
        {
            if (string.IsNullOrEmpty(caminhoBanco) || !File.Exists(caminhoBanco))
                return;

            string destino = caminhoBanco + ".corrupt_" + DateTime.UtcNow.ToString("yyyyMMdd_HHmmss") + ".bak";
            File.Copy(caminhoBanco, destino, true);
        }
        catch
        {
            // O jogo continua mesmo se não for possível criar o backup diagnóstico.
        }
    }

    private string CriptografarConteudo(string json)
    {
        byte[] chaveAes = DerivarChave("AES", 32, false);
        byte[] chaveHmac = DerivarChave("HMAC", 32, false);
        byte[] iv = new byte[16];

        using (RandomNumberGenerator rng = RandomNumberGenerator.Create())
            rng.GetBytes(iv);

        byte[] cifra;
        using (Aes aes = Aes.Create())
        {
            aes.Key = chaveAes;
            aes.IV = iv;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;

            using (ICryptoTransform encryptor = aes.CreateEncryptor())
            {
                byte[] plain = Encoding.UTF8.GetBytes(json);
                cifra = encryptor.TransformFinalBlock(plain, 0, plain.Length);
            }
        }

        byte[] pacote = Concatenar(iv, cifra);
        byte[] assinatura;
        using (HMACSHA256 hmac = new HMACSHA256(chaveHmac))
            assinatura = hmac.ComputeHash(pacote);

        return PrefixoArquivoV2 + ":" +
               Convert.ToBase64String(iv) + ":" +
               Convert.ToBase64String(cifra) + ":" +
               Convert.ToBase64String(assinatura);
    }

    private string DescriptografarConteudo(string conteudo, out bool eraV1)
    {
        eraV1 = false;

        if (string.IsNullOrWhiteSpace(conteudo))
            throw new InvalidDataException("Conteúdo vazio.");

        if (!conteudo.StartsWith(PrefixoArquivoV1 + ":", StringComparison.Ordinal) &&
            !conteudo.StartsWith(PrefixoArquivoV2 + ":", StringComparison.Ordinal))
        {
            return conteudo;
        }

        string[] partes = conteudo.Split(':');
        if (partes.Length != 4)
            throw new InvalidDataException("Formato do banco inválido.");

        eraV1 = string.Equals(partes[0], PrefixoArquivoV1, StringComparison.Ordinal);
        byte[] iv = Convert.FromBase64String(partes[1]);
        byte[] cifra = Convert.FromBase64String(partes[2]);
        byte[] assinatura = Convert.FromBase64String(partes[3]);

        return DescriptografarPacote(iv, cifra, assinatura, eraV1);
    }

    private string DescriptografarPacote(byte[] iv, byte[] cifra, byte[] assinatura, bool legado)
    {
        byte[] chaveAes = DerivarChave("AES", 32, legado);
        byte[] chaveHmac = DerivarChave("HMAC", 32, legado);
        byte[] pacote = Concatenar(iv, cifra);
        byte[] assinaturaCalculada;

        using (HMACSHA256 hmac = new HMACSHA256(chaveHmac))
            assinaturaCalculada = hmac.ComputeHash(pacote);

        if (!ComparacaoTempoConstante(assinatura, assinaturaCalculada))
            throw new CryptographicException("Assinatura HMAC inválida.");

        using (Aes aes = Aes.Create())
        {
            aes.Key = chaveAes;
            aes.IV = iv;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;

            using (ICryptoTransform decryptor = aes.CreateDecryptor())
            {
                byte[] plain = decryptor.TransformFinalBlock(cifra, 0, cifra.Length);
                return Encoding.UTF8.GetString(plain);
            }
        }
    }

    private byte[] DerivarChave(string proposito, int bytes, bool legado)
    {
        string baseSegredo;
        string saltBase;

        if (legado)
        {
            const string saltLegado = "MiniMarket.LocalSecureDatabase.v1.ErickIsrael";
            baseSegredo = Application.companyName + "|" +
                          Application.productName + "|" +
                          SystemInfo.deviceUniqueIdentifier + "|" +
                          saltLegado + "|" +
                          proposito;
            saltBase = saltLegado;
        }
        else
        {
            // V2 não depende do identificador do aparelho. Isso evita perder o save após
            // reinstalações, troca de dispositivo ou mudanças de identificador no mobile.
            baseSegredo = Application.companyName + "|" +
                          Application.productName + "|" +
                          SaltTexto + "|" +
                          proposito;
            saltBase = SaltTexto;
        }

        byte[] salt = Encoding.UTF8.GetBytes(saltBase + "|" + proposito);
        using (Rfc2898DeriveBytes kdf = new Rfc2898DeriveBytes(baseSegredo, salt, 12000))
            return kdf.GetBytes(bytes);
    }

    private byte[] Concatenar(byte[] a, byte[] b)
    {
        byte[] resultado = new byte[a.Length + b.Length];
        Buffer.BlockCopy(a, 0, resultado, 0, a.Length);
        Buffer.BlockCopy(b, 0, resultado, a.Length, b.Length);
        return resultado;
    }

    private bool ComparacaoTempoConstante(byte[] a, byte[] b)
    {
        if (a == null || b == null || a.Length != b.Length)
            return false;

        int diferenca = 0;
        for (int i = 0; i < a.Length; i++)
            diferenca |= a[i] ^ b[i];

        return diferenca == 0;
    }

    private void OnValidate()
    {
        goldInicialPadrao = Mathf.Max(0, goldInicialPadrao);
        gemasIniciaisPadrao = Mathf.Max(0, gemasIniciaisPadrao);
        staminaMaximaPadrao = Mathf.Max(1f, staminaMaximaPadrao);
        energiaSegmentosMaximosPadrao = Mathf.Max(1, energiaSegmentosMaximosPadrao);
        intervaloSalvamentoDiferido = Mathf.Max(0.25f, intervaloSalvamentoDiferido);
    }
}
