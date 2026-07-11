using System;
using Object = UnityEngine.Object;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Painel F10 completo do MiniMarket.
/// Exibe câmera, FOV, AudioListeners, movimento, stamina, persistência, luzes e frame spikes.
/// Quando fechado, não desenha GUI nem realiza buscas contínuas.
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
    public MiniMarketLightingPerformanceOptimizer lighting;
    public MiniMarketSegmentedStaminaRuntimeGuard staminaGuard;

    [Header("Busca Automatica")]
    public bool procurarAutomaticamente = true;
    [Min(0.5f)] public float intervaloBusca = 2f;

    [Header("Visual")]
    public Vector2 posicao = new Vector2(18f, 18f);
    public Vector2 tamanho = new Vector2(760f, 820f);
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

        // Evita o warning quando o componente foi colocado no CameraSystemV2 filho.
        if (transform.parent == null)
            DontDestroyOnLoad(gameObject);

        aberto = iniciarAberto;
        janela = new Rect(posicao.x, posicao.y, tamanho.x, tamanho.y);
        ResolverReferencias(true);
    }

    private void OnDestroy()
    {
        if (instancia == this)
            instancia = null;
    }

    private void Update()
    {
        if (!ativo)
            return;

        if (Input.GetKeyDown(teclaAbrirFechar))
        {
            aberto = !aberto;

            if (aberto)
            {
                ResolverReferencias(true);

                if (performance != null)
                    performance.ResetarMetricas();
            }

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
        janela = GUI.Window(70021, janela, DesenharJanela, "MiniMarket - Diagnóstico Completo F10");
    }

    private void DesenharJanela(int id)
    {
        scroll = GUILayout.BeginScrollView(scroll);

        DesenharStatusGeral();
        Espaco();
        DesenharPerformance();
        Espaco();
        DesenharRenderizacao();
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
            LinhaTitulo("Leitura rápida");
            LinhaTexto("Em 60 FPS, um frame ideal fica perto de 16,7 ms. Acima de 33 ms existe microtravada perceptível.", true);
            LinhaTexto("Câmeras ativas e AudioListeners ativos devem mostrar exatamente 1.", true);
            LinhaTexto("Point/Spot Lights com sombra usam vários mapas; o otimizador limita essas sombras sem apagar as luzes.", true);
            LinhaTexto("O pior frame é resetado ao abrir o F10 para ignorar carregamento inicial do Editor.", true);
        }

        GUILayout.EndScrollView();
        GUI.DragWindow(new Rect(0f, 0f, 10000f, 24f));
    }

    private void DesenharStatusGeral()
    {
        LinhaTitulo("Status geral");
        Linha("Cena", SceneManager.GetActiveScene().name, true);
        Linha("Plataforma", Application.platform + (Application.isMobilePlatform ? " / MOBILE" : " / DESKTOP"), true);
        Linha("Resolução", Screen.width + "x" + Screen.height + " | Fullscreen=" + Screen.fullScreen, true);
        Linha("Qualidade", ObterNomeQualidade(), true);
        Linha("Target FPS / VSync", Application.targetFrameRate + " / " + QualitySettings.vSyncCount, true);
        Linha("TimeScale / FixedDelta", Time.timeScale.ToString("0.000") + " / " + Time.fixedDeltaTime.ToString("0.0000"), Time.timeScale > 0f);
        Linha("Menu aberto", CameraV2MenuInputBlocker.MenuAberto.ToString(), !CameraV2MenuInputBlocker.MenuAberto);
        Linha("Cursor", Cursor.lockState + " | visible=" + Cursor.visible, CameraV2MenuInputBlocker.MenuAberto ? Cursor.visible : Cursor.lockState == CursorLockMode.Locked);
        Linha("Câmera ativa", ObterCameraAtiva(), controller != null);
        Linha("Controller / Player", Estado(controller) + " / " + Estado(playerMove), controller != null && playerMove != null);
    }

    private void DesenharPerformance()
    {
        LinhaTitulo("Performance / fluidez");

        float fpsInstantaneo = 1f / Mathf.Max(Time.unscaledDeltaTime, 0.0001f);
        Linha("FPS instantâneo", fpsInstantaneo.ToString("0.0"), fpsInstantaneo >= 45f);

        if (performance != null)
        {
            Linha("FPS médio", performance.FpsMedio.ToString("0.0"), performance.FpsMedio >= 45f);
            Linha("Frame atual / médio", performance.UltimoFrameMs.ToString("0.00") + " / " + performance.FrameMsMedio.ToString("0.00") + " ms", performance.FrameMsMedio < 25f);
            Linha("Pior frame válido", performance.PiorFrameMs.ToString("0.00") + " ms", performance.PiorFrameMs < 50f);
            Linha("Spikes >=33 / >=50 ms", performance.SpikesLeves + " / " + performance.SpikesGraves, performance.SpikesGraves == 0);
            Linha("Frames ignorados", performance.FramesIgnorados.ToString(), true);
            Linha("Perfil mobile", performance.PerfilMobileAtivo.ToString(), true);
        }
        else
        {
            Linha("Otimizador runtime", "NÃO ENCONTRADO", false);
        }

        double managedMb = GC.GetTotalMemory(false) / (1024d * 1024d);
        Linha("Memória gerenciada", managedMb.ToString("0.0") + " MB", true);
        Linha("RAM sistema", SystemInfo.systemMemorySize + " MB", true);
        Linha("GPU / VRAM", SystemInfo.graphicsDeviceName + " / " + SystemInfo.graphicsMemorySize + " MB", true);
    }

    private void DesenharRenderizacao()
    {
        LinhaTitulo("Câmeras / áudio / luzes");

        if (controller != null)
        {
            int camerasAtivas = controller.ContarCamerasAtivasNoMesmoRoot();
            int listenersAtivos = controller.ContarAudioListenersAtivosNoMesmoRoot();

            Linha("Câmeras ativas na raiz", camerasAtivas.ToString(), camerasAtivas == 1);
            Linha("AudioListeners ativos", listenersAtivos.ToString(), listenersAtivos == 1);
            Linha("Câmeras extras desligadas", controller.CamerasExtrasDesativadas.ToString(), true);
            Linha("Listeners extras desligados", controller.ListenersExtrasDesativados.ToString(), true);
            Linha("Última câmera extra", string.IsNullOrEmpty(controller.UltimaCameraExtraDesativada) ? "nenhuma" : controller.UltimaCameraExtraDesativada, true);
        }
        else
        {
            Linha("CameraV2Controller", "NÃO ENCONTRADO", false);
        }

        if (lighting != null)
        {
            Linha("Luzes Point/Spot", lighting.LuzesPontuaisEncontradas.ToString(), true);
            Linha("Sombras pontuais mantidas", lighting.SombrasPontuaisMantidas.ToString(), true);
            Linha("Sombras pontuais desligadas", lighting.SombrasPontuaisDesligadas.ToString(), true);
        }
        else
        {
            Linha("Otimizador de luzes", "NÃO ENCONTRADO", false);
        }
    }

    private void DesenharMovimentoStamina()
    {
        LinhaTitulo("Player / movimento / stamina");

        if (playerMove == null)
        {
            LinhaTexto("PlayerMove não encontrado.", false);
            return;
        }

        CharacterController cc = playerMove.GetComponent<CharacterController>();
        float velocidade = cc != null ? cc.velocity.magnitude : 0f;

        Linha("GameObject", playerMove.name, true);
        Linha("Velocidade real", velocidade.ToString("0.000") + " m/s", true);
        Linha("Câmera movimento", playerMove.cameraTransform != null ? playerMove.cameraTransform.name : "null", playerMove.cameraTransform != null);
        Linha("Andando / Correndo", playerMove.EstaCorrendo ? "CORRENDO" : (velocidade > 0.05f ? "ANDANDO" : "PARADO"), true);
        Linha("Stamina", playerMove.StaminaAtual.ToString("0.00") + " / " + playerMove.StaminaMaxima.ToString("0.00") + " (" + (playerMove.StaminaPercentual01 * 100f).ToString("0.0") + "%)", true);
        Linha("Segmentos", playerMove.StaminaSegmentadaTexto, true);
        Linha("Reserva", playerMove.StaminaRecargaReserva.ToString("0.00"), true);
        Linha("Gastando / Cansado", playerMove.EstaGastandoStamina + " / " + playerMove.EstaCansado, !playerMove.EstaCansado);

        if (staminaGuard != null)
            Linha("Guard fantasma / bloqueio", staminaGuard.SegmentoFantasmaAtivo + " / " + staminaGuard.BloqueadoAteSoltarShift, true);
    }

    private void DesenharPersistencia()
    {
        LinhaTitulo("Persistência / possíveis fontes de hitch");

        if (performance != null)
        {
            Linha("Save contínuo segmentos", performance.SaveContinuoDeSegmentosDesligado ? "DESLIGADO (otimizado)" : "ATIVO/NÃO APLICADO", performance.SaveContinuoDeSegmentosDesligado);
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
            Linha("Encerrando aplicação", MiniMarketPlayerDatabase.EncerrandoAplicacao.ToString(), !MiniMarketPlayerDatabase.EncerrandoAplicacao);
        }
        else
        {
            Linha("Banco local", "NÃO ENCONTRADO", false);
        }
    }

    private void DesenharThirdPerson()
    {
        LinhaTitulo("ThirdPersonCAM");

        if (thirdPersonCAM == null)
        {
            LinhaTexto("ThirdPersonCAM não encontrado.", false);
            return;
        }

        Linha("Camera component", thirdPersonCAM.UnityCamera != null ? thirdPersonCAM.UnityCamera.name : "null", thirdPersonCAM.UnityCamera != null);
        Linha("Ativa / Input", thirdPersonCAM.cameraAtiva + " / " + thirdPersonCAM.InputMouseAtivo, thirdPersonCAM.cameraAtiva ? thirdPersonCAM.InputMouseAtivo : true);
        Linha("Target", thirdPersonCAM.target != null ? thirdPersonCAM.target.name : "null", thirdPersonCAM.target != null);
        Linha("Yaw / Pitch", thirdPersonCAM.YawAtual.ToString("0.00") + " / " + thirdPersonCAM.PitchAtual.ToString("0.00"), true);
        Linha("FOV atual / alvo", thirdPersonCAM.FovAtual.ToString("0.00") + " / " + (thirdPersonCAM.ZoomAtivo ? thirdPersonCAM.fovZoom : thirdPersonCAM.fovNormal).ToString("0.00"), true);
        Linha("Zoom", thirdPersonCAM.ZoomAtivo.ToString(), true);
        Linha("Dist config / atual", thirdPersonCAM.distancia.ToString("0.00") + " / " + thirdPersonCAM.DistanciaAtual.ToString("0.00"), true);
        Linha("Dist segura", thirdPersonCAM.DistanciaSeguraAtual.ToString("0.00"), true);
        Linha("Colisão", thirdPersonCAM.ColisaoNoFrame ? "SIM: " + thirdPersonCAM.UltimoColliderColisao : "NÃO", !thirdPersonCAM.ColisaoNoFrame);
        Linha("Auto Align", thirdPersonCAM.autoAlinharAtrasDoPersonagem.ToString(), !thirdPersonCAM.autoAlinharAtrasDoPersonagem);
    }

    private void DesenharFirstPerson()
    {
        LinhaTitulo("FirstPersonCAM");

        if (firstPersonCAM == null)
        {
            LinhaTexto("FirstPersonCAM não encontrado.", false);
            return;
        }

        Linha("Camera component", firstPersonCAM.UnityCamera != null ? firstPersonCAM.UnityCamera.name : "null", firstPersonCAM.UnityCamera != null);
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
            LinhaTexto("GetItemV2 não encontrado.", false);
            return;
        }

        Linha("Enabled", getItem.enabled.ToString(), true);
        Linha("Selecionado", getItem.Selecionado != null ? getItem.Selecionado.name : "null", true);
        Linha("Pegando", getItem.Pegando != null ? getItem.Pegando.name : "null", true);
        Linha("Distância / raio", getItem.distanciaSelecao.ToString("0.00") + " / " + getItem.raioSelecao.ToString("0.00"), true);
        Linha("Anti parede", getItem.impedirAtravessarParede.ToString(), getItem.impedirAtravessarParede);
    }

    private void DesenharAcoes()
    {
        LinhaTitulo("Ações de diagnóstico");
        GUILayout.BeginHorizontal();

        if (GUILayout.Button("Resetar métricas") && performance != null)
            performance.ResetarMetricas();

        if (GUILayout.Button("Reparar câmeras") && controller != null)
            controller.RepararAgora();

        if (GUILayout.Button("Revalidar luzes") && lighting != null)
            lighting.RevalidarAgora();

        if (GUILayout.Button("Salvar stamina") && performance != null)
            performance.SalvarSegmentosAgora(true);

        if (GUILayout.Button("Atualizar refs"))
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

        if (forcar || lighting == null)
            lighting = MiniMarketLightingPerformanceOptimizer.Instance != null ? MiniMarketLightingPerformanceOptimizer.Instance : Object.FindFirstObjectByType<MiniMarketLightingPerformanceOptimizer>(FindObjectsInactive.Include);

        if (forcar || staminaGuard == null)
            staminaGuard = Object.FindFirstObjectByType<MiniMarketSegmentedStaminaRuntimeGuard>(FindObjectsInactive.Include);

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

    private string ObterNomeQualidade()
    {
        int indice = QualitySettings.GetQualityLevel();
        string[] nomes = QualitySettings.names;
        return nomes != null && indice >= 0 && indice < nomes.Length ? nomes[indice] : indice.ToString();
    }

    private void PrepararStyles()
    {
        if (labelStyle != null)
            return;

        labelStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = tamanhoFonte,
            wordWrap = true
        };
        labelStyle.normal.textColor = Color.white;

        titleStyle = new GUIStyle(labelStyle)
        {
            fontStyle = FontStyle.Bold,
            fontSize = tamanhoFonte + 2
        };
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
        GUILayout.Label(nome + ":", labelStyle, GUILayout.Width(240f));
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
