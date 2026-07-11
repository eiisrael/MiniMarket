using System;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Painel F10 completo do MiniMarket.
/// Exibe camera, FOV, menu, movimento, stamina, persistencia, memoria e frame spikes.
/// O painel fica totalmente inativo quando fechado.
/// </summary>
[DefaultExecutionOrder(35000)]
[DisallowMultipleComponent]
public class CameraV2F10Diagnostics : MonoBehaviour
{
    [Header("Ativacao")]
    public bool ativo = true;
    public KeyCode teclaAbrirFechar = KeyCode.F10;
    public bool iniciarAberto = false;
    public bool criarAutomaticamente = true;

    [Header("Referencias")]
    public CameraV2Controller controller;
    public Camera3Person thirdPersonCAM;
    public Camera1Person firstPersonCAM;
    public GetItemV2 getItem;
    public PlayerMove playerMove;
    public MiniMarketPlayerDatabase banco;
    public MiniMarketRuntimePerformanceOptimizer performance;

    [Header("Busca Automatica")]
    public bool procurarAutomaticamente = true;
    [Min(0.5f)] public float intervaloBusca = 2f;

    [Header("Visual")]
    public Vector2 posicao = new Vector2(18f, 18f);
    public Vector2 tamanho = new Vector2(720f, 790f);
    public int tamanhoFonte = 13;
    public bool mostrarAjuda = true;

    [Header("Debug")]
    public bool logarNoConsoleAoAbrir = false;

    private static CameraV2F10Diagnostics instancia;
    private bool aberto;
    private Rect janela;
    private Vector2 scroll;
    private float proximaBusca;

    private GUIStyle labelStyle;
    private GUIStyle titleStyle;
    private GUIStyle okStyle;
    private GUIStyle warnStyle;
    private GUIStyle neutralStyle;

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

        GameObject go = new GameObject("MiniMarket_CameraV2F10Diagnostics");
        DontDestroyOnLoad(go);
        go.AddComponent<CameraV2F10Diagnostics>();
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
        aberto = iniciarAberto;
        janela = new Rect(posicao.x, posicao.y, tamanho.x, tamanho.y);
        ResolverReferencias(true);
    }

    private void Update()
    {
        if (!ativo)
            return;

        if (Input.GetKeyDown(teclaAbrirFechar))
        {
            aberto = !aberto;
            if (aberto)
                ResolverReferencias(true);

            if (logarNoConsoleAoAbrir)
                Debug.Log("[CameraV2F10Diagnostics] " + (aberto ? "Aberto" : "Fechado"));
        }

        if (!aberto)
            return;

        if (procurarAutomaticamente && Time.unscaledTime >= proximaBusca)
        {
            proximaBusca = Time.unscaledTime + Mathf.Max(0.5f, intervaloBusca);
            ResolverReferencias(false);
        }
    }

    private void OnGUI()
    {
        if (!ativo || !aberto)
            return;

        PrepararStyles();
        janela.width = tamanho.x;
        janela.height = tamanho.y;
        janela = GUI.Window(70021, janela, DesenharJanela, "MiniMarket - Diagnostico Completo F10");
    }

    private void DesenharJanela(int id)
    {
        scroll = GUILayout.BeginScrollView(scroll);

        DesenharStatusGeral();
        Espaco();
        DesenharPerformance();
        Espaco();
        DesenharMovimentoStamina();
        Espaco();
        DesenharPersistencia();
        Espaco();
        DesenharThirdPerson();
        Espaco();
        DesenharFirstPerson();
        Espaco();
        DesenharGetItem();
        Espaco();
        DesenharAcoes();

        if (mostrarAjuda)
        {
            Espaco();
            LinhaTitulo("Leitura rapida");
            LinhaTexto("Frame ideal em 60 FPS: aproximadamente 16,7 ms. Acima de 33 ms ja existe microtravada perceptivel.", true);
            LinhaTexto("Spikes aumentando ao consumir/regenerar energia indicam persistencia, UI ou outro trabalho no mesmo frame.", true);
            LinhaTexto("F10 fecha este painel. Ele nao coleta GUI quando esta fechado.", true);
        }

        GUILayout.EndScrollView();
        GUI.DragWindow(new Rect(0f, 0f, 10000f, 24f));
    }

    private void DesenharStatusGeral()
    {
        LinhaTitulo("Status geral");
        Linha("Cena", SceneManager.GetActiveScene().name, true);
        Linha("Plataforma", Application.platform.ToString() + (Application.isMobilePlatform ? " / MOBILE" : " / DESKTOP"), true);
        Linha("Resolucao", Screen.width + "x" + Screen.height + " | Fullscreen=" + Screen.fullScreen, true);
        Linha("Qualidade", QualitySettings.names.Length > QualitySettings.GetQualityLevel() ? QualitySettings.names[QualitySettings.GetQualityLevel()] : QualitySettings.GetQualityLevel().ToString(), true);
        Linha("Target FPS / VSync", Application.targetFrameRate + " / " + QualitySettings.vSyncCount, true);
        Linha("TimeScale / FixedDelta", Time.timeScale.ToString("0.000") + " / " + Time.fixedDeltaTime.ToString("0.0000"), Time.timeScale > 0f);
        Linha("Menu aberto", CameraV2MenuInputBlocker.MenuAberto.ToString(), !CameraV2MenuInputBlocker.MenuAberto);
        Linha("Cursor", Cursor.lockState + " | visible=" + Cursor.visible, !CameraV2MenuInputBlocker.MenuAberto ? Cursor.lockState == CursorLockMode.Locked : Cursor.visible);
        Linha("Camera ativa", ObterCameraAtiva(), controller != null);
        Linha("Controller / Player", Estado(controller) + " / " + Estado(playerMove), controller != null && playerMove != null);
    }

    private void DesenharPerformance()
    {
        LinhaTitulo("Performance / fluidez");

        float fpsInstantaneo = 1f / Mathf.Max(Time.unscaledDeltaTime, 0.0001f);
        Linha("FPS instantaneo", fpsInstantaneo.ToString("0.0"), fpsInstantaneo >= 45f);

        if (performance != null)
        {
            Linha("FPS medio", performance.FpsMedio.ToString("0.0"), performance.FpsMedio >= 45f);
            Linha("Frame atual / medio", performance.UltimoFrameMs.ToString("0.00") + " ms / " + performance.FrameMsMedio.ToString("0.00") + " ms", performance.FrameMsMedio < 25f);
            Linha("Pior frame", performance.PiorFrameMs.ToString("0.00") + " ms", performance.PiorFrameMs < 50f);
            Linha("Spikes >=33 / >=50 ms", performance.SpikesLeves + " / " + performance.SpikesGraves, performance.SpikesGraves == 0);
            Linha("Perfil mobile", performance.PerfilMobileAtivo.ToString(), true);
        }
        else
        {
            Linha("Otimizador runtime", "NAO ENCONTRADO", false);
        }

        double managedMb = GC.GetTotalMemory(false) / (1024d * 1024d);
        Linha("Memoria gerenciada", managedMb.ToString("0.0") + " MB", true);
        Linha("RAM sistema", SystemInfo.systemMemorySize + " MB", true);
        Linha("GPU / VRAM", SystemInfo.graphicsDeviceName + " / " + SystemInfo.graphicsMemorySize + " MB", true);
    }

    private void DesenharMovimentoStamina()
    {
        LinhaTitulo("Player / movimento / stamina");

        if (playerMove == null)
        {
            LinhaTexto("PlayerMove nao encontrado.", false);
            return;
        }

        CharacterController cc = playerMove.GetComponent<CharacterController>();
        float velocidade = cc != null ? cc.velocity.magnitude : 0f;
        Linha("GameObject", playerMove.name, true);
        Linha("Velocidade real", velocidade.ToString("0.000") + " m/s", true);
        Linha("Camera movimento", playerMove.cameraTransform != null ? playerMove.cameraTransform.name : "null", playerMove.cameraTransform != null);
        Linha("Andando / Correndo", playerMove.EstaCorrendo ? "CORRENDO" : (velocidade > 0.05f ? "ANDANDO" : "PARADO"), true);
        Linha("Stamina", playerMove.StaminaAtual.ToString("0.00") + " / " + playerMove.StaminaMaxima.ToString("0.00") + " (" + (playerMove.StaminaPercentual01 * 100f).ToString("0.0") + "%)", true);
        Linha("Segmentos", playerMove.StaminaSegmentadaTexto, true);
        Linha("Reserva", playerMove.StaminaRecargaReserva.ToString("0.00"), true);
        Linha("Gastando / Cansado", playerMove.EstaGastandoStamina + " / " + playerMove.EstaCansado, !playerMove.EstaCansado);

        MiniMarketSegmentedStaminaRuntimeGuard guard = Object.FindFirstObjectByType<MiniMarketSegmentedStaminaRuntimeGuard>(FindObjectsInactive.Include);
        if (guard != null)
            Linha("Guard fantasma / bloqueio", guard.SegmentoFantasmaAtivo + " / " + guard.BloqueadoAteSoltarShift, true);
    }

    private void DesenharPersistencia()
    {
        LinhaTitulo("Persistencia / possiveis fontes de hitch");

        if (performance != null)
        {
            Linha("Save continuo segmentos", performance.SaveContinuoDeSegmentosDesligado ? "DESLIGADO (otimizado)" : "ATIVO/NAO APLICADO", performance.SaveContinuoDeSegmentosDesligado);
            Linha("PlayerPrefs pendente", performance.PlayerPrefsPendentes.ToString(), true);
            Linha("Flushes PlayerPrefs", performance.FlushesPlayerPrefs.ToString(), true);
        }

        if (playerMove != null)
        {
            Linha("DB stamina diff/intervalo", playerMove.diferencaMinimaParaSalvarStamina.ToString("0.00") + " / " + playerMove.intervaloSalvarStamina.ToString("0.00") + "s", playerMove.intervaloSalvarStamina >= 0.5f);
            Linha("PlayerPrefs interno", playerMove.salvarSegmentosLocalmente.ToString(), !playerMove.salvarSegmentosLocalmente);
        }

        if (banco != null)
        {
            Linha("Banco pendente", banco.SalvamentoPendente.ToString(), true);
            Linha("Debounce disco", banco.intervaloSalvamentoDiferido.ToString("0.0") + "s", banco.intervaloSalvamentoDiferido >= 5f);
            Linha("Criptografia", banco.usarCriptografiaLocal.ToString(), true);
        }
        else
        {
            Linha("Banco local", "NAO ENCONTRADO", false);
        }
    }

    private void DesenharThirdPerson()
    {
        LinhaTitulo("ThirdPersonCAM");

        if (thirdPersonCAM == null)
        {
            LinhaTexto("ThirdPersonCAM nao encontrado.", false);
            return;
        }

        Linha("Ativa / Input", thirdPersonCAM.cameraAtiva + " / " + thirdPersonCAM.InputMouseAtivo, thirdPersonCAM.cameraAtiva ? thirdPersonCAM.InputMouseAtivo : true);
        Linha("Target", thirdPersonCAM.target != null ? thirdPersonCAM.target.name : "null", thirdPersonCAM.target != null);
        Linha("Yaw / Pitch", thirdPersonCAM.YawAtual.ToString("0.00") + " / " + thirdPersonCAM.PitchAtual.ToString("0.00"), true);
        Linha("FOV atual / alvo", thirdPersonCAM.FovAtual.ToString("0.00") + " / " + (thirdPersonCAM.ZoomAtivo ? thirdPersonCAM.fovZoom : thirdPersonCAM.fovNormal).ToString("0.00"), true);
        Linha("Zoom", thirdPersonCAM.ZoomAtivo.ToString(), true);
        Linha("Dist config / atual", thirdPersonCAM.distancia.ToString("0.00") + " / " + thirdPersonCAM.DistanciaAtual.ToString("0.00"), true);
        Linha("Dist segura", thirdPersonCAM.DistanciaSeguraAtual.ToString("0.00"), true);
        Linha("Colisao", thirdPersonCAM.ColisaoNoFrame ? "SIM: " + thirdPersonCAM.UltimoColliderColisao : "NAO", !thirdPersonCAM.ColisaoNoFrame);
        Linha("Auto Align", thirdPersonCAM.autoAlinharAtrasDoPersonagem.ToString(), !thirdPersonCAM.autoAlinharAtrasDoPersonagem);
    }

    private void DesenharFirstPerson()
    {
        LinhaTitulo("FirstPersonCAM");

        if (firstPersonCAM == null)
        {
            LinhaTexto("FirstPersonCAM nao encontrado.", false);
            return;
        }

        Linha("Ativa / Input", firstPersonCAM.cameraAtiva + " / " + firstPersonCAM.InputMouseAtivo, firstPersonCAM.cameraAtiva ? firstPersonCAM.InputMouseAtivo : true);
        Linha("Corpo / POV", Nome(firstPersonCAM.corpoPersonagem) + " / " + Nome(firstPersonCAM.pontoPOV), firstPersonCAM.corpoPersonagem != null && firstPersonCAM.pontoPOV != null);
        Linha("Yaw / Pitch", firstPersonCAM.YawAtual.ToString("0.00") + " / " + firstPersonCAM.PitchAtual.ToString("0.00"), true);
        Linha("FOV atual / alvo", firstPersonCAM.FovAtual.ToString("0.00") + " / " + (firstPersonCAM.ZoomAtivo ? firstPersonCAM.fovZoom : firstPersonCAM.fovNormal).ToString("0.00"), true);
        Linha("Zoom", firstPersonCAM.ZoomAtivo.ToString(), true);
        Linha("HeadBob / Sway", firstPersonCAM.desativarHeadBob + " / " + firstPersonCAM.desativarSway, firstPersonCAM.desativarHeadBob && firstPersonCAM.desativarSway);
        Linha("Mira", firstPersonCAM.exibirMira.ToString(), true);
    }

    private void DesenharGetItem()
    {
        LinhaTitulo("GetItemV2");

        if (getItem == null)
        {
            LinhaTexto("GetItemV2 nao encontrado.", false);
            return;
        }

        Linha("Enabled", getItem.enabled.ToString(), true);
        Linha("Selecionado", getItem.Selecionado != null ? getItem.Selecionado.name : "null", true);
        Linha("Pegando", getItem.Pegando != null ? getItem.Pegando.name : "null", true);
        Linha("Distancia / raio", getItem.distanciaSelecao.ToString("0.00") + " / " + getItem.raioSelecao.ToString("0.00"), true);
        Linha("Anti parede", getItem.impedirAtravessarParede.ToString(), getItem.impedirAtravessarParede);
    }

    private void DesenharAcoes()
    {
        LinhaTitulo("Acoes de diagnostico");
        GUILayout.BeginHorizontal();

        if (GUILayout.Button("Resetar metricas"))
        {
            if (performance != null)
                performance.ResetarMetricas();
        }

        if (GUILayout.Button("Salvar stamina agora"))
        {
            if (performance != null)
                performance.SalvarSegmentosAgora(true);
        }

        if (GUILayout.Button("Atualizar referencias"))
            ResolverReferencias(true);

        GUILayout.EndHorizontal();
    }

    private void ResolverReferencias(bool forcar)
    {
        if (forcar || controller == null)
            controller = Object.FindFirstObjectByType<CameraV2Controller>(FindObjectsInactive.Include);

        if (forcar || thirdPersonCAM == null)
            thirdPersonCAM = Object.FindFirstObjectByType<Camera3Person>(FindObjectsInactive.Include);

        if (forcar || firstPersonCAM == null)
            firstPersonCAM = Object.FindFirstObjectByType<Camera1Person>(FindObjectsInactive.Include);

        if (forcar || playerMove == null)
            playerMove = Object.FindFirstObjectByType<PlayerMove>(FindObjectsInactive.Include);

        if (forcar || banco == null)
            banco = MiniMarketPlayerDatabase.Instance != null ? MiniMarketPlayerDatabase.Instance : Object.FindFirstObjectByType<MiniMarketPlayerDatabase>(FindObjectsInactive.Include);

        if (forcar || performance == null)
            performance = MiniMarketRuntimePerformanceOptimizer.Instance != null ? MiniMarketRuntimePerformanceOptimizer.Instance : Object.FindFirstObjectByType<MiniMarketRuntimePerformanceOptimizer>(FindObjectsInactive.Include);

        if (forcar || getItem == null)
        {
            getItem = firstPersonCAM != null ? firstPersonCAM.GetComponent<GetItemV2>() : null;
            if (getItem == null)
                getItem = Object.FindFirstObjectByType<GetItemV2>(FindObjectsInactive.Include);
        }
    }

    private string ObterCameraAtiva()
    {
        if (controller != null)
            return controller.PrimeiraPessoaAtiva ? "FirstPersonCAM" : "ThirdPersonCAM";

        if (firstPersonCAM != null && firstPersonCAM.cameraAtiva)
            return "FirstPersonCAM";

        if (thirdPersonCAM != null && thirdPersonCAM.cameraAtiva)
            return "ThirdPersonCAM";

        return "Nenhuma";
    }

    private void PrepararStyles()
    {
        if (labelStyle != null)
            return;

        labelStyle = new GUIStyle(GUI.skin.label);
        labelStyle.fontSize = tamanhoFonte;
        labelStyle.normal.textColor = Color.white;
        labelStyle.wordWrap = true;

        titleStyle = new GUIStyle(labelStyle);
        titleStyle.fontStyle = FontStyle.Bold;
        titleStyle.fontSize = tamanhoFonte + 2;
        titleStyle.normal.textColor = new Color(1f, 0.85f, 0.25f, 1f);

        okStyle = new GUIStyle(labelStyle);
        okStyle.normal.textColor = new Color(0.45f, 1f, 0.65f, 1f);

        warnStyle = new GUIStyle(labelStyle);
        warnStyle.normal.textColor = new Color(1f, 0.38f, 0.38f, 1f);

        neutralStyle = new GUIStyle(labelStyle);
        neutralStyle.normal.textColor = new Color(0.72f, 0.82f, 1f, 1f);
    }

    private void LinhaTitulo(string texto)
    {
        GUILayout.Label(texto, titleStyle);
    }

    private void Linha(string nome, string valor, bool ok)
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label(nome + ":", labelStyle, GUILayout.Width(220f));
        GUILayout.Label(valor, ok ? okStyle : warnStyle);
        GUILayout.EndHorizontal();
    }

    private void LinhaTexto(string texto, bool ok)
    {
        GUILayout.Label(texto, ok ? neutralStyle : warnStyle);
    }

    private void Espaco()
    {
        GUILayout.Space(9f);
    }

    private string Estado(UnityEngine.Object obj)
    {
        return obj != null ? "OK" : "NULL";
    }

    private string Nome(Transform t)
    {
        return t != null ? t.name : "null";
    }
}
