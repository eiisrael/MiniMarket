using UnityEngine;

/// <summary>
/// Controlador de perspectiva do MiniMarket.
///
/// Responsabilidade atual:
/// - Segurar botão direito: solicita primeira pessoa na própria Main Camera.
/// - Soltar botão direito: volta para terceira pessoa.
/// - Mantém a câmera secundária antiga desativada.
/// - Atualiza PlayerMove.cameraTransform para a Main Camera.
///
/// Por padrão, este script NÃO força mais valores do CameraGTAFollowHardcore,
/// para deixar o Inspector livre para ajuste fino.
/// </summary>
[DefaultExecutionOrder(21000)]
public class MiniMarketCameraPerspectiveSwitcher : MonoBehaviour
{
    [Header("Cameras")]
    public Camera cameraNormal;

    [Tooltip("Compatibilidade: se existir uma camera antiga de primeira pessoa, ela será desativada para evitar snap/jumping.")]
    public Camera cameraPrimeiraPessoa;

    [Header("Modo Inspector")]
    [Tooltip("Ligado: não força AutoAlign/Zoom/PrimeiraPessoa no CameraGTAFollowHardcore.")]
    public bool modoInspectorLivre = true;

    [Header("Primeira Pessoa")]
    public bool usarBotaoDireitoComoPrimeiraPessoa = true;
    [Range(0, 2)] public int botaoPrimeiraPessoa = 1;
    public bool voltarParaCameraNormalAoSoltar = true;

    [Tooltip("Mantido por compatibilidade. Não é mais usado para alternar, pois TAB pertence ao Menu.")]
    public KeyCode teclaAlternar = KeyCode.Tab;
    public bool iniciarNaCameraNormal = true;

    [Header("Ponto de Primeira Pessoa")]
    public Transform pontoPrimeiraPessoa;
    public Transform alvoFallback;
    public Vector3 offsetLocalFallback = new Vector3(0f, 1.68f, 0.18f);

    [Header("Compatibilidade")]
    public bool copiarRotacaoDaCameraNormal = true;
    public bool copiarFOVDaCameraNormal = true;
    public bool manterCursorTravado = true;

    [Header("Estabilidade / PlayerMove")]
    public PlayerMove playerMove;
    public bool atualizarCameraTransformDoPlayerMove = true;
    public bool desativarAutoAlignCameraGTA = true;
    public bool reforcarEstabilizacaoInicial = false;
    [Min(0.1f)] public float tempoReforcoEstabilizacao = 3f;

    [Header("Primeira Pessoa - Personagem")]
    public Transform personagemParaRotacionar;

    [Tooltip("Desligado por padrão. Evita duplicar a rotação que já é feita pela CameraGTAFollowHardcore.")]
    public bool rotacionarPersonagemComCamera = false;

    [Min(0.1f)] public float velocidadeRotacaoPersonagem = 18f;
    public bool ocultarRenderersNaPrimeiraPessoa = true;
    public bool encontrarRenderersAutomaticamente = true;
    public Renderer[] renderersDoPersonagem;

    [Header("Bloqueio por Menu")]
    public bool bloquearAlternanciaComMenuAberto = true;
    public CanvasGroup[] menusQueBloqueiamAlternancia;
    public bool detectarMenusAutomaticamente = true;
    [Min(0.1f)] public float intervaloBuscaMenus = 0.75f;

    [Header("Debug")]
    public bool logarEventos = false;

    private bool usandoPrimeiraPessoa;
    private bool ultimoEstadoBotao;
    private float tempoInicio;
    private float proximaBuscaMenus;
    private CanvasGroup[] menusAuto;
    private CameraGTAFollowHardcore cameraGTA;
    private AudioListener audioNormal;
    private AudioListener audioPrimeiraPessoa;

    public bool UsandoPrimeiraPessoa => usandoPrimeiraPessoa;

    private void Awake()
    {
        tempoInicio = Time.unscaledTime;
        ResolverReferencias();
        usandoPrimeiraPessoa = false;
        AplicarEstadoInicial();
        EstabilizarCameraGTASePermitido();
    }

    private void Start()
    {
        ResolverReferencias();
        AplicarEstadoInicial();
        EstabilizarCameraGTASePermitido();
    }

    private void OnDisable()
    {
        SetPrimeiraPessoa(false, true);
        AplicarVisibilidadeRenderers(true);
    }

    private void Update()
    {
        ResolverReferenciasLeve();

        if (!modoInspectorLivre && reforcarEstabilizacaoInicial && Time.unscaledTime - tempoInicio <= tempoReforcoEstabilizacao)
            EstabilizarCameraGTASePermitido();

        if (!usarBotaoDireitoComoPrimeiraPessoa)
            return;

        bool menuAberto = AlgumMenuBloqueando();
        bool botaoPressionado = Input.GetMouseButton(botaoPrimeiraPessoa) && !menuAberto;

        if (botaoPressionado != ultimoEstadoBotao)
        {
            ultimoEstadoBotao = botaoPressionado;

            if (botaoPressionado)
                SetPrimeiraPessoa(true, false);
            else if (voltarParaCameraNormalAoSoltar)
                SetPrimeiraPessoa(false, false);
        }
    }

    private void LateUpdate()
    {
        DesativarCameraPrimeiraPessoaAntiga();
        AtualizarCameraDoPlayerMove();
        AtualizarRotacaoPersonagemPrimeiraPessoa();
    }

    public void AlternarCamera()
    {
        SetPrimeiraPessoa(!usandoPrimeiraPessoa, false);
    }

    public void UsarCameraNormal()
    {
        SetPrimeiraPessoa(false, false);
    }

    public void UsarCameraPrimeiraPessoa()
    {
        SetPrimeiraPessoa(true, false);
    }

    private void SetPrimeiraPessoa(bool ativa, bool instantaneo)
    {
        ResolverReferenciasLeve();

        usandoPrimeiraPessoa = ativa;

        if (cameraGTA != null)
            cameraGTA.SetPrimeiraPessoa(ativa);

        if (cameraNormal != null)
            cameraNormal.enabled = true;

        DesativarCameraPrimeiraPessoaAntiga();
        AplicarVisibilidadeRenderers(!ativa || !ocultarRenderersNaPrimeiraPessoa);
        AtualizarCameraDoPlayerMove();

        if (manterCursorTravado && ativa)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        if (logarEventos)
            MiniMarketUpgradeLogger.Log("Camera", ativa ? "Primeira pessoa ON" : "Primeira pessoa OFF", "Controle por botão direito na Main Camera.", "camera-rmb-" + ativa, 1f);
    }

    private void AplicarEstadoInicial()
    {
        if (cameraNormal == null)
            cameraNormal = Camera.main;

        if (cameraNormal != null)
        {
            cameraNormal.enabled = true;
            audioNormal = cameraNormal.GetComponent<AudioListener>();
            if (audioNormal != null)
                audioNormal.enabled = true;
        }

        DesativarCameraPrimeiraPessoaAntiga();
        SetPrimeiraPessoa(!iniciarNaCameraNormal, true);
    }

    private void ResolverReferencias()
    {
        if (cameraNormal == null)
            cameraNormal = Camera.main;

        if (cameraNormal != null)
        {
            cameraGTA = cameraNormal.GetComponent<CameraGTAFollowHardcore>();
            audioNormal = cameraNormal.GetComponent<AudioListener>();
        }

        if (cameraPrimeiraPessoa != null)
            audioPrimeiraPessoa = cameraPrimeiraPessoa.GetComponent<AudioListener>();

        if (playerMove == null)
            playerMove = Object.FindFirstObjectByType<PlayerMove>(FindObjectsInactive.Include);

        if (alvoFallback == null && playerMove != null)
            alvoFallback = playerMove.transform;

        if (personagemParaRotacionar == null)
            personagemParaRotacionar = playerMove != null ? playerMove.transform : alvoFallback;

        if (cameraGTA != null)
        {
            if (cameraGTA.target == null && personagemParaRotacionar != null)
                cameraGTA.target = personagemParaRotacionar;

            if (pontoPrimeiraPessoa != null)
                cameraGTA.pontoPrimeiraPessoa = pontoPrimeiraPessoa;
            else if (cameraGTA.pontoPrimeiraPessoa == null && alvoFallback != null)
                cameraGTA.offsetPrimeiraPessoa = offsetLocalFallback;
        }

        if (encontrarRenderersAutomaticamente && (renderersDoPersonagem == null || renderersDoPersonagem.Length == 0))
        {
            Transform raiz = personagemParaRotacionar != null ? personagemParaRotacionar : alvoFallback;
            if (raiz != null)
                renderersDoPersonagem = raiz.GetComponentsInChildren<Renderer>(true);
        }
    }

    private void ResolverReferenciasLeve()
    {
        if (cameraNormal == null)
            cameraNormal = Camera.main;

        if (cameraGTA == null && cameraNormal != null)
            cameraGTA = cameraNormal.GetComponent<CameraGTAFollowHardcore>();

        if (playerMove == null)
            playerMove = Object.FindFirstObjectByType<PlayerMove>(FindObjectsInactive.Include);
    }

    private void DesativarCameraPrimeiraPessoaAntiga()
    {
        if (cameraPrimeiraPessoa == null)
            return;

        cameraPrimeiraPessoa.enabled = false;

        if (audioPrimeiraPessoa == null)
            audioPrimeiraPessoa = cameraPrimeiraPessoa.GetComponent<AudioListener>();

        if (audioPrimeiraPessoa != null)
            audioPrimeiraPessoa.enabled = false;
    }

    private void AtualizarCameraDoPlayerMove()
    {
        if (!atualizarCameraTransformDoPlayerMove || playerMove == null || cameraNormal == null)
            return;

        playerMove.cameraTransform = cameraNormal.transform;
    }

    private void AtualizarRotacaoPersonagemPrimeiraPessoa()
    {
        if (!usandoPrimeiraPessoa || !rotacionarPersonagemComCamera)
            return;

        Transform alvo = personagemParaRotacionar != null ? personagemParaRotacionar : alvoFallback;
        if (alvo == null || cameraNormal == null)
            return;

        Vector3 forward = cameraNormal.transform.forward;
        forward.y = 0f;

        if (forward.sqrMagnitude < 0.0001f)
            return;

        Quaternion rotacaoAlvo = Quaternion.LookRotation(forward.normalized, Vector3.up);
        float t = 1f - Mathf.Exp(-velocidadeRotacaoPersonagem * Time.deltaTime);
        alvo.rotation = Quaternion.Slerp(alvo.rotation, rotacaoAlvo, t);
    }

    private void EstabilizarCameraGTASePermitido()
    {
        if (modoInspectorLivre)
            return;

        EstabilizarCameraGTA();
    }

    private void EstabilizarCameraGTA()
    {
        if (!desativarAutoAlignCameraGTA || cameraGTA == null)
            return;

        cameraGTA.autoAlignBehindPlayer = false;
        cameraGTA.autoAlignDelay = 999f;
        cameraGTA.autoAlignSpeed = 0f;
        cameraGTA.usarZoomMiraPorDistancia = false;
        cameraGTA.aplicarColisaoNoZoomMira = false;
        cameraGTA.preservarAnguloAtualNaTransicao = true;
        cameraGTA.usarPosicaoPrimeiraPessoaEstavel = true;
        cameraGTA.rotacionarPersonagemNaPrimeiraPessoa = true;
        cameraGTA.sincronizarCorpoSuaveNaPrimeiraPessoa = true;
        cameraGTA.mouseSmoothTimePrimeiraPessoa = 0f;
    }

    private void AplicarVisibilidadeRenderers(bool visivel)
    {
        if (renderersDoPersonagem == null)
            return;

        for (int i = 0; i < renderersDoPersonagem.Length; i++)
        {
            if (renderersDoPersonagem[i] != null)
                renderersDoPersonagem[i].enabled = visivel;
        }
    }

    private bool AlgumMenuBloqueando()
    {
        if (!bloquearAlternanciaComMenuAberto)
            return false;

        if (menusQueBloqueiamAlternancia != null)
        {
            for (int i = 0; i < menusQueBloqueiamAlternancia.Length; i++)
            {
                if (CanvasGroupEstaAberto(menusQueBloqueiamAlternancia[i]))
                    return true;
            }
        }

        if (!detectarMenusAutomaticamente)
            return false;

        if (Time.unscaledTime >= proximaBuscaMenus || menusAuto == null)
        {
            proximaBuscaMenus = Time.unscaledTime + intervaloBuscaMenus;
            menusAuto = Object.FindObjectsByType<CanvasGroup>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        }

        if (menusAuto == null)
            return false;

        for (int i = 0; i < menusAuto.Length; i++)
        {
            CanvasGroup grupo = menusAuto[i];
            if (grupo == null)
                continue;

            string nome = grupo.gameObject.name.ToLowerInvariant();
            if (!nome.Contains("menu"))
                continue;

            if (CanvasGroupEstaAberto(grupo))
                return true;
        }

        return false;
    }

    private bool CanvasGroupEstaAberto(CanvasGroup grupo)
    {
        return grupo != null && grupo.gameObject.activeInHierarchy && grupo.alpha > 0.05f && grupo.blocksRaycasts;
    }
}
