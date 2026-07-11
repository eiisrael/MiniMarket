using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

/// <summary>
/// Banco local seguro/tamper-resistant do MiniMarket.
///
/// Mantém uma única fonte de verdade para nome, gold, stamina, empresas e propriedades.
/// Impede que MiniMarket_PlayerDatabase seja recriado durante o encerramento do Play Mode.
/// </summary>
[DefaultExecutionOrder(-10000)]
[DisallowMultipleComponent]
public class MiniMarketPlayerDatabase : MonoBehaviour
{
    public static MiniMarketPlayerDatabase Instance { get; private set; }
    public static bool EncerrandoAplicacao => encerrandoAplicacao;

    private const int VersaoSchemaAtual = 1;
    private const string NomeArquivoBanco = "player_database.mmdb";
    private const string PrefixoArquivo = "MMDB1";
    private const string SaltTexto = "MiniMarket.LocalSecureDatabase.v1.ErickIsrael";

    [Header("Identificacao")]
    public string playerIdPadrao = "LOCAL_PLAYER_001";
    public string nomePadrao = "Player";

    [Header("Valores iniciais")]
    [Min(0)] public int goldInicialPadrao = 20000;
    [Min(1f)] public float staminaMaximaPadrao = 100f;

    [Header("Banco local")]
    public bool salvarAutomaticamente = true;
    public bool usarCriptografiaLocal = true;
    public bool usarSalvamentoDiferido = true;
    [Min(0.25f)] public float intervaloSalvamentoDiferido = 5f;
    public bool salvarAoSairOuPerderFoco = true;
    public bool logarEventos = false;

    private static bool encerrandoAplicacao;

    private MiniMarketPlayerData dados;
    private string caminhoBanco;
    private bool carregado;
    private bool salvamentoPendente;
    private float proximoSalvamentoPermitido;
    private bool destruindo;

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
    public float StaminaAtual => Dados.staminaAtual;
    public float StaminaMaxima => Dados.staminaMaxima;
    public int EmpresasCompradas => Dados.empresasCompradas != null ? Dados.empresasCompradas.Count : 0;
    public bool SalvamentoPendente => salvamentoPendente;
    public string CaminhoBanco => caminhoBanco;

    [Serializable]
    public class MiniMarketPlayerData
    {
        public int schemaVersion = VersaoSchemaAtual;
        public string playerId = "LOCAL_PLAYER_001";
        public string nome = "Player";

        public bool goldInicializado;
        public int gold;

        public bool staminaInicializada;
        public float staminaAtual = 100f;
        public float staminaMaxima = 100f;

        public List<string> empresasCompradas = new List<string>();
        public List<MiniMarketPropertyState> propriedades = new List<MiniMarketPropertyState>();
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

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        encerrandoAplicacao = false;

        if (transform.parent == null)
            DontDestroyOnLoad(gameObject);

        GarantirCarregado();
    }

    private void Update()
    {
        if (!encerrandoAplicacao)
            ProcessarSalvamentoDiferido();
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
    }

    private void OnApplicationQuit()
    {
        encerrandoAplicacao = true;

        if (salvarAoSairOuPerderFoco)
            SalvarSePendenteOuForcar();
    }

    private void OnDisable()
    {
        // No Editor, Application.isPlaying fica falso durante o fechamento do Play Mode.
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

    /// <summary>
    /// Retorna/cria o banco somente durante gameplay válido.
    /// Durante Stop/Quit retorna null em vez de gerar um GameObject órfão.
    /// </summary>
    public static MiniMarketPlayerDatabase ObterOuCriar()
    {
        if (Instance != null)
            return Instance;

        if (encerrandoAplicacao || !Application.isPlaying)
            return null;

        MiniMarketPlayerDatabase encontrado = FindObjectOfType<MiniMarketPlayerDatabase>(true);
        if (encontrado != null)
        {
            Instance = encontrado;
            encontrado.GarantirCarregado();
            return Instance;
        }

        GameObject go = new GameObject("MiniMarket_PlayerDatabase");
        Instance = go.AddComponent<MiniMarketPlayerDatabase>();
        return Instance;
    }

    public void GarantirCarregado()
    {
        if (carregado && dados != null)
            return;

        caminhoBanco = Path.Combine(Application.persistentDataPath, NomeArquivoBanco);
        dados = CarregarDoDisco();
        NormalizarDados();
        carregado = true;
    }

    private MiniMarketPlayerData CarregarDoDisco()
    {
        try
        {
            if (!File.Exists(caminhoBanco))
            {
                if (logarEventos)
                    Debug.Log("[MiniMarketPlayerDatabase] Banco nao encontrado. Criando dados novos.");

                return CriarDadosPadrao();
            }

            string conteudo = File.ReadAllText(caminhoBanco, Encoding.UTF8);
            string json = usarCriptografiaLocal ? DescriptografarConteudo(conteudo) : conteudo;
            MiniMarketPlayerData carregadoDoDisco = JsonUtility.FromJson<MiniMarketPlayerData>(json);

            if (carregadoDoDisco == null)
                throw new Exception("JSON do banco retornou null.");

            if (logarEventos)
                Debug.Log("[MiniMarketPlayerDatabase] Banco carregado: " + caminhoBanco);

            return carregadoDoDisco;
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[MiniMarketPlayerDatabase] Falha ao carregar banco. Um novo banco sera criado. Motivo: " + ex.Message);
            FazerBackupArquivoCorrompido();
            return CriarDadosPadrao();
        }
    }

    private MiniMarketPlayerData CriarDadosPadrao()
    {
        MiniMarketPlayerData novo = new MiniMarketPlayerData();
        novo.schemaVersion = VersaoSchemaAtual;
        novo.playerId = string.IsNullOrWhiteSpace(playerIdPadrao) ? "LOCAL_PLAYER_001" : playerIdPadrao.Trim();
        novo.nome = string.IsNullOrWhiteSpace(nomePadrao) ? "Player" : nomePadrao.Trim();
        novo.goldInicializado = false;
        novo.gold = Mathf.Max(0, goldInicialPadrao);
        novo.staminaInicializada = false;
        novo.staminaMaxima = Mathf.Max(1f, staminaMaximaPadrao);
        novo.staminaAtual = novo.staminaMaxima;
        novo.empresasCompradas = new List<string>();
        novo.propriedades = new List<MiniMarketPropertyState>();
        novo.lastUpdatedUnix = ObterUnixAgora();
        return novo;
    }

    private void NormalizarDados()
    {
        if (dados == null)
            dados = CriarDadosPadrao();

        dados.schemaVersion = Mathf.Max(1, dados.schemaVersion);

        if (string.IsNullOrWhiteSpace(dados.playerId))
            dados.playerId = string.IsNullOrWhiteSpace(playerIdPadrao) ? "LOCAL_PLAYER_001" : playerIdPadrao.Trim();

        if (string.IsNullOrWhiteSpace(dados.nome))
            dados.nome = string.IsNullOrWhiteSpace(nomePadrao) ? "Player" : nomePadrao.Trim();

        dados.gold = Mathf.Max(0, dados.gold);
        dados.staminaMaxima = Mathf.Max(1f, dados.staminaMaxima);
        dados.staminaAtual = Mathf.Clamp(dados.staminaAtual, 0f, dados.staminaMaxima);

        if (dados.empresasCompradas == null)
            dados.empresasCompradas = new List<string>();

        if (dados.propriedades == null)
            dados.propriedades = new List<MiniMarketPropertyState>();

        RemoverDuplicatasEmpresas();
        dados.lastUpdatedUnix = ObterUnixAgora();
    }

    public void Salvar()
    {
        if (destruindo && dados == null)
            return;

        GarantirCarregado();

        try
        {
            string diretorio = Path.GetDirectoryName(caminhoBanco);
            if (!string.IsNullOrEmpty(diretorio))
                Directory.CreateDirectory(diretorio);

            dados.lastUpdatedUnix = ObterUnixAgora();

            string json = JsonUtility.ToJson(dados, true);
            string conteudo = usarCriptografiaLocal ? CriptografarConteudo(json) : json;
            string temp = caminhoBanco + ".tmp";

            File.WriteAllText(temp, conteudo, Encoding.UTF8);

            if (File.Exists(caminhoBanco))
                File.Delete(caminhoBanco);

            File.Move(temp, caminhoBanco);

            salvamentoPendente = false;
            proximoSalvamentoPermitido = Time.unscaledTime + intervaloSalvamentoDiferido;

            if (logarEventos)
                Debug.Log("[MiniMarketPlayerDatabase] Banco salvo: " + caminhoBanco);
        }
        catch (Exception ex)
        {
            Debug.LogError("[MiniMarketPlayerDatabase] Falha ao salvar banco: " + ex.Message);
        }
    }

    public void SalvarSePendenteOuForcar()
    {
        if (!carregado || dados == null)
            return;

        if (salvamentoPendente || salvarAutomaticamente)
            Salvar();
    }

    private void MarcarSalvamentoPendente()
    {
        salvamentoPendente = true;
    }

    private void ProcessarSalvamentoDiferido()
    {
        if (!salvarAutomaticamente || !usarSalvamentoDiferido || !salvamentoPendente)
            return;

        if (Time.unscaledTime < proximoSalvamentoPermitido)
            return;

        Salvar();
    }

    private void NotificarAlteracao(bool salvarAgora)
    {
        if (dados == null)
            return;

        dados.lastUpdatedUnix = ObterUnixAgora();

        if (salvarAutomaticamente)
        {
            if (salvarAgora || !usarSalvamentoDiferido)
                Salvar();
            else
                MarcarSalvamentoPendente();
        }

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
        return dados.gold >= quantidade;
    }

    public void GarantirStaminaInicial(float staminaAtualInicial, float staminaMaximaInicial)
    {
        GarantirCarregado();
        if (dados.staminaInicializada)
            return;

        dados.staminaMaxima = Mathf.Max(1f, staminaMaximaInicial);
        dados.staminaAtual = Mathf.Clamp(staminaAtualInicial, 0f, dados.staminaMaxima);
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

    public void RestaurarStaminaCompleta()
    {
        GarantirCarregado();
        dados.staminaMaxima = Mathf.Max(1f, dados.staminaMaxima);
        dados.staminaAtual = dados.staminaMaxima;
        dados.staminaInicializada = true;
        NotificarAlteracao(true);
    }

    public float ObterPercentualStamina01()
    {
        GarantirCarregado();
        if (dados.staminaMaxima <= 0.001f)
            return 0f;

        return Mathf.Clamp01(dados.staminaAtual / dados.staminaMaxima);
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

        if (string.IsNullOrEmpty(empresaId))
            return false;

        if (dados.empresasCompradas.Contains(empresaId))
        {
            NotificarAlteracao(false);
            return false;
        }

        dados.empresasCompradas.Add(empresaId);
        NotificarAlteracao(true);
        return true;
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

        RegistrarEmpresaComprada(areaId);
        NotificarAlteracao(true);
        return propriedade;
    }

    public MiniMarketPropertyState DefinirStatusPropriedade(string areaId, string nome, bool comprada, bool disponivel, string status)
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

        if (comprada)
            RegistrarEmpresaComprada(areaId);

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

    [ContextMenu("Banco/Salvar Agora")]
    public void ContextSalvarAgora()
    {
        Salvar();
    }

    [ContextMenu("Banco/Resetar banco local")]
    public void ResetarBancoLocal()
    {
        dados = CriarDadosPadrao();
        carregado = true;
        salvamentoPendente = false;
        Salvar();
        OnDatabaseChanged?.Invoke(dados);
    }

    [ContextMenu("Banco/Abrir caminho no log")]
    public void LogarCaminhoBanco()
    {
        GarantirCarregado();
        Debug.Log("[MiniMarketPlayerDatabase] Caminho do banco: " + caminhoBanco);
    }

    private void RemoverDuplicatasEmpresas()
    {
        HashSet<string> set = new HashSet<string>();
        List<string> limpas = new List<string>();

        for (int i = 0; i < dados.empresasCompradas.Count; i++)
        {
            string id = NormalizarId(dados.empresasCompradas[i]);
            if (string.IsNullOrEmpty(id) || set.Contains(id))
                continue;

            set.Add(id);
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
        if (string.IsNullOrWhiteSpace(id))
            return string.Empty;

        return id.Trim().ToUpperInvariant().Replace(' ', '_');
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

            string backup = caminhoBanco + ".corrupt_" + DateTime.UtcNow.ToString("yyyyMMdd_HHmmss") + ".bak";
            File.Copy(caminhoBanco, backup, true);
        }
        catch
        {
            // Não impede o jogo de continuar.
        }
    }

    private string CriptografarConteudo(string json)
    {
        byte[] chaveAes = DerivarChave("AES", 32);
        byte[] chaveHmac = DerivarChave("HMAC", 32);
        byte[] iv = new byte[16];

        using (RNGCryptoServiceProvider rng = new RNGCryptoServiceProvider())
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

        byte[] pacoteParaAssinar = Concatenar(iv, cifra);
        byte[] assinatura;

        using (HMACSHA256 hmac = new HMACSHA256(chaveHmac))
            assinatura = hmac.ComputeHash(pacoteParaAssinar);

        return PrefixoArquivo + ":" +
               Convert.ToBase64String(iv) + ":" +
               Convert.ToBase64String(cifra) + ":" +
               Convert.ToBase64String(assinatura);
    }

    private string DescriptografarConteudo(string conteudo)
    {
        if (string.IsNullOrWhiteSpace(conteudo))
            throw new Exception("Conteudo vazio.");

        if (!conteudo.StartsWith(PrefixoArquivo + ":"))
            return conteudo;

        string[] partes = conteudo.Split(':');
        if (partes.Length != 4)
            throw new Exception("Formato do banco invalido.");

        byte[] iv = Convert.FromBase64String(partes[1]);
        byte[] cifra = Convert.FromBase64String(partes[2]);
        byte[] assinatura = Convert.FromBase64String(partes[3]);

        byte[] chaveAes = DerivarChave("AES", 32);
        byte[] chaveHmac = DerivarChave("HMAC", 32);
        byte[] pacoteParaAssinar = Concatenar(iv, cifra);
        byte[] assinaturaCalculada;

        using (HMACSHA256 hmac = new HMACSHA256(chaveHmac))
            assinaturaCalculada = hmac.ComputeHash(pacoteParaAssinar);

        if (!ComparacaoTempoConstante(assinatura, assinaturaCalculada))
            throw new Exception("Assinatura HMAC invalida. Banco alterado ou corrompido.");

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

    private byte[] DerivarChave(string proposito, int bytes)
    {
        string baseSegredo = Application.companyName + "|" +
                             Application.productName + "|" +
                             SystemInfo.deviceUniqueIdentifier + "|" +
                             SaltTexto + "|" +
                             proposito;

        byte[] salt = Encoding.UTF8.GetBytes(SaltTexto + "|" + proposito);

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

        int diff = 0;
        for (int i = 0; i < a.Length; i++)
            diff |= a[i] ^ b[i];

        return diff == 0;
    }
}
