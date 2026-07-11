using System;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Otimizador global e leve do MiniMarket.
///
/// Principais objetivos:
/// - impedir PlayerPrefs.Save em loop durante consumo/regeneracao de stamina;
/// - aumentar o debounce do banco local e do UpgradeLog;
/// - manter PlayerMove usando a camera V2 ativa;
/// - aplicar um perfil seguro de 60 FPS no mobile;
/// - coletar metricas leves para o painel F10.
///
/// O script se cria automaticamente. Nao precisa adicionar manualmente.
/// </summary>
[DefaultExecutionOrder(-45000)]
[DisallowMultipleComponent]
public class MiniMarketRuntimePerformanceOptimizer : MonoBehaviour
{
    public static MiniMarketRuntimePerformanceOptimizer Instance { get; private set; }

    private const string KeySegmentosAtuais = "MiniMarket.Player.StaminaSegmentosAtuais";
    private const string KeySegmentosMaximos = "MiniMarket.Player.StaminaSegmentosMaximos";
    private const string KeyRecargaReserva = "MiniMarket.Player.StaminaRecargaReserva";

    [Header("Ativacao")]
    public bool ativo = true;
    public bool criarAutomaticamente = true;

    [Header("Perfil Mobile")]
    public bool aplicarPerfilMobileAutomaticamente = true;
    [Range(30, 120)] public int targetFpsMobile = 60;
    public bool desligarVSyncNoMobile = true;
    public bool reduzirPrioridadeCarregamentoBackground = true;

    [Header("Stamina / Persistencia")]
    [Tooltip("Desliga o PlayerPrefs.Save interno do PlayerMove durante o gameplay e salva somente em pausa/saida.")]
    public bool impedirSaveDeSegmentosDuranteGameplay = true;

    [Min(0.25f)] public float intervaloAtualizarValoresPlayerPrefs = 1f;
    public bool permitirFlushPeriodicoOpcional = false;
    [Min(10f)] public float intervaloFlushPeriodico = 30f;

    [Tooltip("Piso de diferenca para enviar stamina ao banco, reduzindo eventos e serializacao.")]
    [Min(0.1f)] public float diferencaMinimaBancoDesktop = 0.75f;
    [Min(0.1f)] public float diferencaMinimaBancoMobile = 1.25f;

    [Tooltip("Piso do intervalo de envio da stamina para o banco.")]
    [Min(0.25f)] public float intervaloBancoDesktop = 0.8f;
    [Min(0.25f)] public float intervaloBancoMobile = 1.25f;

    [Header("Banco Local")]
    [Min(2f)] public float debounceDiscoDesktop = 12f;
    [Min(2f)] public float debounceDiscoMobile = 20f;

    [Header("UpgradeLog")]
    public bool otimizarUpgradeLog = true;
    [Min(10f)] public float intervaloFlushLogDesktop = 60f;
    [Min(10f)] public float intervaloFlushLogMobile = 120f;
    public bool evitarGravacaoDuplicadaEditorEPersistent = true;

    [Header("Monitor de Frames")]
    [Min(10f)] public float limiteSpikeLeveMs = 33.3f;
    [Min(20f)] public float limiteSpikeGraveMs = 50f;
    [Range(0.01f, 1f)] public float suavizacaoMetricas = 0.08f;

    [Header("Busca")]
    [Min(0.25f)] public float intervaloBuscaReferencias = 1f;

    [Header("Debug")]
    public bool logarAplicacao;

    private PlayerMove playerMove;
    private MiniMarketPlayerDatabase banco;
    private MiniMarketUpgradeLogger logger;
    private CameraV2Controller cameraController;

    private bool playerConfigurado;
    private bool bancoConfigurado;
    private bool loggerConfigurado;
    private bool salvarSegmentosOriginal;
    private bool capturouSalvarSegmentosOriginal;

    private float proximaBusca;
    private float proximaAtualizacaoPrefs;
    private float proximoFlushOpcional;
    private bool prefsPendentes;
    private int flushesPlayerPrefs;

    private float fpsMedio;
    private float frameMsMedio;
    private float piorFrameMs;
    private int spikesLeves;
    private int spikesGraves;
    private float ultimoFrameMs;

    public PlayerMove Player => playerMove;
    public MiniMarketPlayerDatabase Banco => banco;
    public CameraV2Controller CameraController => cameraController;
    public float FpsMedio => fpsMedio;
    public float FrameMsMedio => frameMsMedio;
    public float UltimoFrameMs => ultimoFrameMs;
    public float PiorFrameMs => piorFrameMs;
    public int SpikesLeves => spikesLeves;
    public int SpikesGraves => spikesGraves;
    public int FlushesPlayerPrefs => flushesPlayerPrefs;
    public bool PlayerPrefsPendentes => prefsPendentes;
    public bool SaveContinuoDeSegmentosDesligado => playerMove != null && impedirSaveDeSegmentosDuranteGameplay && !playerMove.salvarSegmentosLocalmente;
    public bool PerfilMobileAtivo => Application.isMobilePlatform && aplicarPerfilMobileAutomaticamente;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetarStatics()
    {
        Instance = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void CriarAuto()
    {
        if (Instance != null)
            return;

        GameObject go = new GameObject("MiniMarket_RuntimePerformanceOptimizer");
        DontDestroyOnLoad(go);
        go.AddComponent<MiniMarketRuntimePerformanceOptimizer>();
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
        SceneManager.sceneLoaded += AoCarregarCena;

        AplicarPerfilGlobal();
        ResolverReferencias(true);
        AplicarOtimizacoes();
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= AoCarregarCena;

        if (Instance == this)
        {
            SalvarSegmentosAgora(true);
            Instance = null;
        }
    }

    private void OnDisable()
    {
        if (Instance == this)
            SalvarSegmentosAgora(true);
    }

    private void OnApplicationPause(bool pausado)
    {
        if (pausado)
            SalvarSegmentosAgora(true);
    }

    private void OnApplicationFocus(bool focado)
    {
        if (!focado)
            SalvarSegmentosAgora(true);
    }

    private void OnApplicationQuit()
    {
        SalvarSegmentosAgora(true);
    }

    private void Update()
    {
        if (!ativo)
            return;

        AtualizarMetricasFrame();

        if (Time.unscaledTime >= proximaBusca)
        {
            proximaBusca = Time.unscaledTime + Mathf.Max(0.25f, intervaloBuscaReferencias);
            ResolverReferencias(false);
            AplicarOtimizacoes();
            AtualizarCameraDoPlayer();
        }

        if (Time.unscaledTime >= proximaAtualizacaoPrefs)
        {
            proximaAtualizacaoPrefs = Time.unscaledTime + Mathf.Max(0.25f, intervaloAtualizarValoresPlayerPrefs);
            AtualizarValoresPlayerPrefsSemFlush();
        }

        if (permitirFlushPeriodicoOpcional && prefsPendentes && Time.unscaledTime >= proximoFlushOpcional)
        {
            proximoFlushOpcional = Time.unscaledTime + Mathf.Max(10f, intervaloFlushPeriodico);
            SalvarSegmentosAgora(true);
        }
    }

    private void LateUpdate()
    {
        if (ativo)
            AtualizarCameraDoPlayer();
    }

    private void AoCarregarCena(Scene cena, LoadSceneMode modo)
    {
        playerMove = null;
        cameraController = null;
        playerConfigurado = false;
        proximaBusca = 0f;
    }

    private void AplicarPerfilGlobal()
    {
        if (reduzirPrioridadeCarregamentoBackground)
            Application.backgroundLoadingPriority = ThreadPriority.Low;

#if UNITY_2020_2_OR_NEWER
        Physics.reuseCollisionCallbacks = true;
#endif

        if (!Application.isMobilePlatform || !aplicarPerfilMobileAutomaticamente)
            return;

        if (desligarVSyncNoMobile)
            QualitySettings.vSyncCount = 0;

        Application.targetFrameRate = Mathf.Clamp(targetFpsMobile, 30, 120);
    }

    private void ResolverReferencias(bool forcar)
    {
        if (forcar || playerMove == null)
            playerMove = Object.FindFirstObjectByType<PlayerMove>(FindObjectsInactive.Include);

        if (forcar || banco == null)
            banco = MiniMarketPlayerDatabase.Instance != null
                ? MiniMarketPlayerDatabase.Instance
                : Object.FindFirstObjectByType<MiniMarketPlayerDatabase>(FindObjectsInactive.Include);

        if (forcar || logger == null)
            logger = Object.FindFirstObjectByType<MiniMarketUpgradeLogger>(FindObjectsInactive.Include);

        if (forcar || cameraController == null)
            cameraController = Object.FindFirstObjectByType<CameraV2Controller>(FindObjectsInactive.Include);
    }

    private void AplicarOtimizacoes()
    {
        bool mobile = Application.isMobilePlatform;

        if (playerMove != null)
        {
            if (!capturouSalvarSegmentosOriginal)
            {
                salvarSegmentosOriginal = playerMove.salvarSegmentosLocalmente;
                capturouSalvarSegmentosOriginal = true;
            }

            if (impedirSaveDeSegmentosDuranteGameplay)
                playerMove.salvarSegmentosLocalmente = false;

            playerMove.diferencaMinimaParaSalvarStamina = Mathf.Max(
                playerMove.diferencaMinimaParaSalvarStamina,
                mobile ? diferencaMinimaBancoMobile : diferencaMinimaBancoDesktop
            );

            playerMove.intervaloSalvarStamina = Mathf.Max(
                playerMove.intervaloSalvarStamina,
                mobile ? intervaloBancoMobile : intervaloBancoDesktop
            );

            if (!playerConfigurado && logarAplicacao)
                Debug.Log("[MiniMarketPerformance] PlayerMove otimizado para reduzir salvamentos de stamina.");

            playerConfigurado = true;
        }

        if (banco != null)
        {
            banco.usarSalvamentoDiferido = true;
            banco.intervaloSalvamentoDiferido = Mathf.Max(
                banco.intervaloSalvamentoDiferido,
                mobile ? debounceDiscoMobile : debounceDiscoDesktop
            );

            bancoConfigurado = true;
        }

        if (logger != null && otimizarUpgradeLog)
        {
            logger.intervaloFlush = Mathf.Max(
                logger.intervaloFlush,
                mobile ? intervaloFlushLogMobile : intervaloFlushLogDesktop
            );

            logger.maxEntradasPorFlush = Mathf.Max(logger.maxEntradasPorFlush, 50);

            if (evitarGravacaoDuplicadaEditorEPersistent)
            {
#if UNITY_EDITOR
                logger.escreverNaRaizDoProjetoNoEditor = true;
                logger.escreverTambemNoPersistentDataPath = false;
#else
                logger.escreverTambemNoPersistentDataPath = true;
#endif
            }

            loggerConfigurado = true;
        }
    }

    private void AtualizarCameraDoPlayer()
    {
        if (playerMove == null || cameraController == null)
            return;

        Transform cameraAtiva = cameraController.CameraAtivaTransform;
        if (cameraAtiva != null && playerMove.cameraTransform != cameraAtiva)
            playerMove.cameraTransform = cameraAtiva;
    }

    private void AtualizarValoresPlayerPrefsSemFlush()
    {
        if (playerMove == null || !impedirSaveDeSegmentosDuranteGameplay)
            return;

        PlayerPrefs.SetInt(KeySegmentosAtuais, playerMove.StaminaSegmentosAtuais);
        PlayerPrefs.SetInt(KeySegmentosMaximos, playerMove.StaminaSegmentosMaximos);
        PlayerPrefs.SetFloat(KeyRecargaReserva, playerMove.StaminaRecargaReserva);
        prefsPendentes = true;
    }

    public void SalvarSegmentosAgora(bool flushDisco)
    {
        if (playerMove == null || !capturouSalvarSegmentosOriginal || !salvarSegmentosOriginal)
            return;

        PlayerPrefs.SetInt(KeySegmentosAtuais, playerMove.StaminaSegmentosAtuais);
        PlayerPrefs.SetInt(KeySegmentosMaximos, playerMove.StaminaSegmentosMaximos);
        PlayerPrefs.SetFloat(KeyRecargaReserva, playerMove.StaminaRecargaReserva);
        prefsPendentes = true;

        if (!flushDisco)
            return;

        PlayerPrefs.Save();
        prefsPendentes = false;
        flushesPlayerPrefs++;
    }

    private void AtualizarMetricasFrame()
    {
        float dt = Mathf.Max(Time.unscaledDeltaTime, 0.0001f);
        float ms = dt * 1000f;
        float fps = 1f / dt;

        ultimoFrameMs = ms;

        if (fpsMedio <= 0f)
        {
            fpsMedio = fps;
            frameMsMedio = ms;
        }
        else
        {
            float t = Mathf.Clamp01(suavizacaoMetricas);
            fpsMedio = Mathf.Lerp(fpsMedio, fps, t);
            frameMsMedio = Mathf.Lerp(frameMsMedio, ms, t);
        }

        if (ms > piorFrameMs)
            piorFrameMs = ms;

        if (ms >= limiteSpikeLeveMs)
            spikesLeves++;

        if (ms >= limiteSpikeGraveMs)
            spikesGraves++;
    }

    public void ResetarMetricas()
    {
        fpsMedio = 0f;
        frameMsMedio = 0f;
        ultimoFrameMs = 0f;
        piorFrameMs = 0f;
        spikesLeves = 0;
        spikesGraves = 0;
    }
}
