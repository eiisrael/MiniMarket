using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Controla o modo BuyScene sem trocar de cena Unity.
/// Responsabilidades:
/// - Mover a Main Camera para a vista aerea/ortografica.
/// - Restaurar a camera original imediatamente ao sair.
/// - Bloquear movimento/mira durante a BuyScene.
/// - Forcar Idle no Animator para evitar personagem correndo parado.
///
/// Observacao importante:
/// A abertura/fechamento por tecla deve ficar preferencialmente no BuySceneEntryTrigger.
/// Assim evitamos duplo input no mesmo frame e bug de abrir/fechar imediatamente.
/// </summary>
public class BuySceneCameraModeController : MonoBehaviour
{
    [Header("Referencias Principais")]
    [Tooltip("Camera usada no jogo. Se deixar vazio, usa Camera.main.")]
    public Camera cameraPrincipal;

    [Tooltip("Raiz do personagem. Exemplo: Character01 / Player.")]
    public Transform jogadorRaiz;

    [Tooltip("Componentes extras que devem ser desativados enquanto estiver na BuyScene.")]
    public MonoBehaviour[] componentesExtrasParaDesativar;

    [Header("Camera BuyScene")]
    [Min(1f)]
    public float alturaCameraAerea = 18f;

    [Min(0f)]
    public float recuoCameraAerea = 0.35f;

    public float rotacaoYCameraAerea = 0f;
    public Vector3 offsetFoco = Vector3.zero;

    [Header("Modo Ortografico")]
    public bool usarOrtograficaNoModoCompra = true;

    [Min(1f)]
    public float tamanhoOrtografico = 16f;

    [Header("Transicao")]
    [Min(0.1f)]
    public float velocidadeMoverCamera = 8f;

    [Min(0.1f)]
    public float velocidadeRotacionarCamera = 10f;

    [Min(0.1f)]
    public float velocidadeZoomCamera = 8f;

    [Tooltip("Se ligado, a entrada no modo compra encaixa a camera instantaneamente.")]
    public bool entradaInstantanea = false;

    [Tooltip("LEGADO. Por padrao a saida atual e instantanea para evitar camera virando para o lado.")]
    public bool retornoSuave = false;

    [Tooltip("Ao sair, restaura a camera original no mesmo frame.")]
    public bool forcarRetornoInstantaneoAoSair = true;

    [Header("Animacao do Player")]
    public bool forcarIdleAoEntrarNoModoCompra = true;
    public bool zerarParametrosMovimentoAnimator = true;
    public bool pausarAnimatorDuranteBuyScene = false;

    [Tooltip("Nomes de estados Idle que o script tenta encontrar no Animator Controller.")]
    public string[] nomesEstadosIdle =
    {
        "Base Layer.Idle",
        "Idle",
        "idle",
        "Stand",
        "Standing",
        "Standing Idle",
        "Idle_A",
        "Idle01"
    };

    [Header("Input do Controller")]
    [Tooltip("LEGADO. Mantido apenas para nao quebrar Inspector antigo. Nao usado por padrao.")]
    public bool sairComEscape = false;

    [Tooltip("Use apenas se quiser que este controller tambem feche por tecla. Recomendado deixar desligado e controlar pelo BuySceneEntryTrigger.")]
    public bool permitirFecharPelaTeclaDoController = false;

    [Tooltip("Tecla opcional para fechar pelo controller, caso Permitir Fechar Pela Tecla Do Controller esteja ligado.")]
    public KeyCode teclaFecharPeloController = KeyCode.E;

    [Header("Cursor")]
    public bool mostrarCursorNoModoCompra = true;
    public CursorLockMode lockModeNoModoCompra = CursorLockMode.None;

    [Header("Mensagem Temporaria")]
    public bool mostrarMensagemNaTela = true;
    public string mensagemModoCompra = "BuyScene: terrenos a venda - E para voltar";

    [Header("Debug")]
    public bool logarEventos = true;

    private Transform focoAtual;
    private readonly List<BuyableLandAreaMarker> terrenosDestacados = new List<BuyableLandAreaMarker>();
    private readonly List<MonoBehaviour> componentesDesativadosAutomaticamente = new List<MonoBehaviour>();
    private readonly List<AnimatorEstadoSalvo> animadoresSalvos = new List<AnimatorEstadoSalvo>();

    private bool modoCompraAtivo;
    private bool retornandoCamera;
    private bool estadoCameraSalvo;

    private Vector3 cameraPosicaoAntes;
    private Quaternion cameraRotacaoAntes;
    private float cameraFovAntes;
    private bool cameraOrtograficaAntes;
    private float cameraOrthographicSizeAntes;
    private CursorLockMode cursorLockAntes;
    private bool cursorVisivelAntes;

    public bool ModoCompraAtivo => modoCompraAtivo || retornandoCamera;

    private struct AnimatorEstadoSalvo
    {
        public Animator animator;
        public float velocidadeAntes;
        public bool applyRootMotionAntes;
    }

    private void Awake()
    {
        ResolverReferenciasBasicas();
    }

    private void Update()
    {
        if (modoCompraAtivo && permitirFecharPelaTeclaDoController && Input.GetKeyDown(teclaFecharPeloController))
            SairDoModoCompra();
    }

    private void LateUpdate()
    {
        if (cameraPrincipal == null)
            return;

        if (modoCompraAtivo)
            AtualizarCameraModoCompra();
        else if (retornandoCamera)
            AtualizarRetornoCamera();
    }

    private void OnDisable()
    {
        RestaurarTudoInstantaneo();
    }

    public void EntrarNoModoCompra(Transform pontoDeFoco)
    {
        EntrarNoModoCompra(pontoDeFoco, null);
    }

    public void EntrarNoModoCompra(Transform pontoDeFoco, BuyableLandAreaMarker[] terrenosDaArea)
    {
        ResolverReferenciasBasicas();

        if (cameraPrincipal == null)
        {
            Debug.LogWarning("[BuySceneCameraModeController] Nenhuma camera encontrada.");
            return;
        }

        if (pontoDeFoco == null)
            pontoDeFoco = transform;

        focoAtual = pontoDeFoco;

        if (!modoCompraAtivo && !retornandoCamera)
            SalvarEstadoCameraECursor();

        retornandoCamera = false;
        modoCompraAtivo = true;

        PrepararCursorModoCompra();
        DesativarComponentesDoJogo();
        PrepararAnimacaoModoCompra();
        DestacarTerrenosDaArea(terrenosDaArea, true);

        if (usarOrtograficaNoModoCompra)
        {
            cameraPrincipal.orthographic = true;
            cameraPrincipal.orthographicSize = entradaInstantanea ? tamanhoOrtografico : cameraPrincipal.orthographicSize;
        }

        if (entradaInstantanea)
        {
            Vector3 posicaoAlvo = CalcularPosicaoCameraAerea();
            Quaternion rotacaoAlvo = CalcularRotacaoCameraAerea(posicaoAlvo);

            cameraPrincipal.transform.position = posicaoAlvo;
            cameraPrincipal.transform.rotation = rotacaoAlvo;

            if (usarOrtograficaNoModoCompra)
                cameraPrincipal.orthographicSize = tamanhoOrtografico;
        }

        if (logarEventos)
            Debug.Log("[BuyScene] Entrou no modo de compra perto de: " + pontoDeFoco.name);
    }

    public void SairDoModoCompra()
    {
        if (!modoCompraAtivo && !retornandoCamera)
            return;

        modoCompraAtivo = false;
        retornandoCamera = false;
        LimparDestaquesDosTerrenos();

        if (forcarRetornoInstantaneoAoSair || !retornoSuave || !estadoCameraSalvo)
            FinalizarRetornoCamera();
        else
            retornandoCamera = true;

        if (logarEventos)
            Debug.Log("[BuyScene] Saiu do modo de compra.");
    }

    private void ResolverReferenciasBasicas()
    {
        if (cameraPrincipal == null)
            cameraPrincipal = Camera.main;
    }

    private void SalvarEstadoCameraECursor()
    {
        cameraPosicaoAntes = cameraPrincipal.transform.position;
        cameraRotacaoAntes = cameraPrincipal.transform.rotation;
        cameraFovAntes = cameraPrincipal.fieldOfView;
        cameraOrtograficaAntes = cameraPrincipal.orthographic;
        cameraOrthographicSizeAntes = cameraPrincipal.orthographicSize;
        cursorLockAntes = Cursor.lockState;
        cursorVisivelAntes = Cursor.visible;
        estadoCameraSalvo = true;
    }

    private void PrepararCursorModoCompra()
    {
        Cursor.visible = mostrarCursorNoModoCompra;
        Cursor.lockState = lockModeNoModoCompra;
    }

    private void DesativarComponentesDoJogo()
    {
        componentesDesativadosAutomaticamente.Clear();

        AdicionarComponentesPorNome(jogadorRaiz, "PlayerMove");
        AdicionarComponentesPorNome(jogadorRaiz, "Player_Move");
        AdicionarComponentesPorNome(jogadorRaiz, "PlayerObjectGrabberHardcore");
        AdicionarComponentesPorNome(jogadorRaiz, "MouseLookHardcore");
        AdicionarComponentesPorNome(jogadorRaiz, "PlayerAnimation");
        AdicionarComponentesPorNome(jogadorRaiz, "Player_Animation");
        AdicionarComponentesPorNome(jogadorRaiz, "PlayerAnimator");
        AdicionarComponentesPorNome(jogadorRaiz, "PlayerAnimatorController");

        if (cameraPrincipal != null)
        {
            AdicionarComponentesPorNome(cameraPrincipal.transform, "CrosshairAim");
            AdicionarComponentesPorNome(cameraPrincipal.transform, "CameraGTAFollowHardcore");
            AdicionarComponentesPorNome(cameraPrincipal.transform, "MouseLookHardcore");
        }

        if (componentesExtrasParaDesativar != null)
        {
            for (int i = 0; i < componentesExtrasParaDesativar.Length; i++)
                AdicionarComponenteParaDesativar(componentesExtrasParaDesativar[i]);
        }

        for (int i = 0; i < componentesDesativadosAutomaticamente.Count; i++)
        {
            MonoBehaviour componente = componentesDesativadosAutomaticamente[i];

            if (componente != null && componente.enabled)
                componente.enabled = false;
        }
    }

    private void ReativarComponentesDoJogo()
    {
        for (int i = 0; i < componentesDesativadosAutomaticamente.Count; i++)
        {
            MonoBehaviour componente = componentesDesativadosAutomaticamente[i];

            if (componente != null)
                componente.enabled = true;
        }

        componentesDesativadosAutomaticamente.Clear();
    }

    private void AdicionarComponentesPorNome(Transform raiz, string nomeDoTipo)
    {
        if (raiz == null || string.IsNullOrEmpty(nomeDoTipo))
            return;

        MonoBehaviour[] componentes = raiz.GetComponentsInChildren<MonoBehaviour>(true);

        for (int i = 0; i < componentes.Length; i++)
        {
            MonoBehaviour componente = componentes[i];

            if (componente == null)
                continue;

            if (componente.GetType().Name == nomeDoTipo)
                AdicionarComponenteParaDesativar(componente);
        }
    }

    private void AdicionarComponenteParaDesativar(MonoBehaviour componente)
    {
        if (componente == null || componente == this)
            return;

        if (!componentesDesativadosAutomaticamente.Contains(componente))
            componentesDesativadosAutomaticamente.Add(componente);
    }

    private void PrepararAnimacaoModoCompra()
    {
        if (!forcarIdleAoEntrarNoModoCompra || jogadorRaiz == null)
            return;

        animadoresSalvos.Clear();
        Animator[] animadores = jogadorRaiz.GetComponentsInChildren<Animator>(true);

        for (int i = 0; i < animadores.Length; i++)
        {
            Animator animator = animadores[i];

            if (animator == null)
                continue;

            AnimatorEstadoSalvo estado = new AnimatorEstadoSalvo
            {
                animator = animator,
                velocidadeAntes = animator.speed,
                applyRootMotionAntes = animator.applyRootMotion
            };

            animadoresSalvos.Add(estado);
            animator.applyRootMotion = false;

            if (zerarParametrosMovimentoAnimator)
                ZerarParametrosAnimator(animator);

            TentarForcarEstadoIdle(animator);

            if (pausarAnimatorDuranteBuyScene)
                animator.speed = 0f;
            else
                animator.speed = Mathf.Max(0.01f, animator.speed);

            animator.Update(0f);
        }
    }

    private void RestaurarAnimacaoModoJogo()
    {
        for (int i = 0; i < animadoresSalvos.Count; i++)
        {
            AnimatorEstadoSalvo estado = animadoresSalvos[i];

            if (estado.animator == null)
                continue;

            estado.animator.speed = estado.velocidadeAntes;
            estado.animator.applyRootMotion = estado.applyRootMotionAntes;
        }

        animadoresSalvos.Clear();
    }

    private void ZerarParametrosAnimator(Animator animator)
    {
        if (animator == null || animator.runtimeAnimatorController == null)
            return;

        AnimatorControllerParameter[] parametros = animator.parameters;

        for (int i = 0; i < parametros.Length; i++)
        {
            AnimatorControllerParameter parametro = parametros[i];
            string nome = parametro.name;
            string nomeLower = nome.ToLowerInvariant();

            switch (parametro.type)
            {
                case AnimatorControllerParameterType.Float:
                    animator.SetFloat(nome, 0f);
                    break;

                case AnimatorControllerParameterType.Int:
                    animator.SetInteger(nome, 0);
                    break;

                case AnimatorControllerParameterType.Bool:
                    if (nomeLower.Contains("ground") || nomeLower.Contains("chao") || nomeLower.Contains("floor"))
                        animator.SetBool(nome, true);
                    else
                        animator.SetBool(nome, false);
                    break;

                case AnimatorControllerParameterType.Trigger:
                    animator.ResetTrigger(nome);
                    break;
            }
        }
    }

    private void TentarForcarEstadoIdle(Animator animator)
    {
        if (animator == null || animator.runtimeAnimatorController == null || nomesEstadosIdle == null)
            return;

        for (int layer = 0; layer < animator.layerCount; layer++)
        {
            for (int i = 0; i < nomesEstadosIdle.Length; i++)
            {
                string nomeEstado = nomesEstadosIdle[i];

                if (string.IsNullOrEmpty(nomeEstado))
                    continue;

                int hashEstado = Animator.StringToHash(nomeEstado);

                if (!animator.HasState(layer, hashEstado))
                    continue;

                animator.CrossFadeInFixedTime(hashEstado, 0.05f, layer, 0f);
                return;
            }
        }
    }

    private void DestacarTerrenosDaArea(BuyableLandAreaMarker[] terrenosDaArea, bool destacar)
    {
        LimparDestaquesDosTerrenos();

        if (terrenosDaArea == null)
            return;

        for (int i = 0; i < terrenosDaArea.Length; i++)
        {
            BuyableLandAreaMarker terreno = terrenosDaArea[i];

            if (terreno == null)
                continue;

            terreno.DefinirDestaque(destacar);

            if (!terrenosDestacados.Contains(terreno))
                terrenosDestacados.Add(terreno);
        }
    }

    private void LimparDestaquesDosTerrenos()
    {
        for (int i = 0; i < terrenosDestacados.Count; i++)
        {
            BuyableLandAreaMarker terreno = terrenosDestacados[i];

            if (terreno != null)
                terreno.DefinirDestaque(false);
        }

        terrenosDestacados.Clear();
    }

    private void AtualizarCameraModoCompra()
    {
        Vector3 posicaoAlvo = CalcularPosicaoCameraAerea();
        Quaternion rotacaoAlvo = CalcularRotacaoCameraAerea(posicaoAlvo);

        float suavizacaoPosicao = CalcularSuavizacao(velocidadeMoverCamera, Time.deltaTime);
        float suavizacaoRotacao = CalcularSuavizacao(velocidadeRotacionarCamera, Time.deltaTime);
        float suavizacaoZoom = CalcularSuavizacao(velocidadeZoomCamera, Time.deltaTime);

        cameraPrincipal.transform.position = Vector3.Lerp(cameraPrincipal.transform.position, posicaoAlvo, suavizacaoPosicao);
        cameraPrincipal.transform.rotation = Quaternion.Slerp(cameraPrincipal.transform.rotation, rotacaoAlvo, suavizacaoRotacao);

        if (usarOrtograficaNoModoCompra)
        {
            cameraPrincipal.orthographic = true;
            cameraPrincipal.orthographicSize = Mathf.Lerp(cameraPrincipal.orthographicSize, tamanhoOrtografico, suavizacaoZoom);
        }
    }

    private void AtualizarRetornoCamera()
    {
        if (forcarRetornoInstantaneoAoSair)
        {
            FinalizarRetornoCamera();
            return;
        }

        float suavizacaoPosicao = CalcularSuavizacao(velocidadeMoverCamera, Time.deltaTime);
        float suavizacaoRotacao = CalcularSuavizacao(velocidadeRotacionarCamera, Time.deltaTime);

        cameraPrincipal.transform.position = Vector3.Lerp(cameraPrincipal.transform.position, cameraPosicaoAntes, suavizacaoPosicao);
        cameraPrincipal.transform.rotation = Quaternion.Slerp(cameraPrincipal.transform.rotation, cameraRotacaoAntes, suavizacaoRotacao);

        float distancia = Vector3.Distance(cameraPrincipal.transform.position, cameraPosicaoAntes);
        float angulo = Quaternion.Angle(cameraPrincipal.transform.rotation, cameraRotacaoAntes);

        if (distancia <= 0.05f && angulo <= 0.5f)
            FinalizarRetornoCamera();
    }

    private Vector3 CalcularPosicaoCameraAerea()
    {
        Vector3 foco = ObterFocoAtual();
        Vector3 direcaoRecuo = Quaternion.Euler(0f, rotacaoYCameraAerea, 0f) * Vector3.back;
        return foco + Vector3.up * alturaCameraAerea + direcaoRecuo * recuoCameraAerea;
    }

    private Quaternion CalcularRotacaoCameraAerea(Vector3 posicaoCamera)
    {
        Vector3 foco = ObterFocoAtual();
        Vector3 direcao = foco - posicaoCamera;

        if (direcao.sqrMagnitude <= 0.0001f)
            direcao = Vector3.down;

        return Quaternion.LookRotation(direcao.normalized, Vector3.up);
    }

    private Vector3 ObterFocoAtual()
    {
        if (focoAtual == null)
            return transform.position + offsetFoco;

        return focoAtual.position + offsetFoco;
    }

    private void FinalizarRetornoCamera()
    {
        retornandoCamera = false;

        if (estadoCameraSalvo && cameraPrincipal != null)
        {
            cameraPrincipal.transform.position = cameraPosicaoAntes;
            cameraPrincipal.transform.rotation = cameraRotacaoAntes;
            cameraPrincipal.fieldOfView = cameraFovAntes;
            cameraPrincipal.orthographic = cameraOrtograficaAntes;
            cameraPrincipal.orthographicSize = cameraOrthographicSizeAntes;
        }

        Cursor.lockState = cursorLockAntes;
        Cursor.visible = cursorVisivelAntes;

        RestaurarAnimacaoModoJogo();
        ReativarComponentesDoJogo();
        estadoCameraSalvo = false;
    }

    private void RestaurarTudoInstantaneo()
    {
        if (!modoCompraAtivo && !retornandoCamera)
            return;

        modoCompraAtivo = false;
        retornandoCamera = false;
        LimparDestaquesDosTerrenos();
        FinalizarRetornoCamera();
    }

    private float CalcularSuavizacao(float velocidade, float deltaTime)
    {
        return 1f - Mathf.Exp(-velocidade * deltaTime);
    }

    private void OnGUI()
    {
        if (!mostrarMensagemNaTela || !modoCompraAtivo)
            return;

        GUIStyle estilo = new GUIStyle(GUI.skin.box);
        estilo.fontSize = 22;
        estilo.alignment = TextAnchor.MiddleCenter;

        Rect area = new Rect((Screen.width - 620f) * 0.5f, 24f, 620f, 42f);
        GUI.Box(area, mensagemModoCompra, estilo);
    }
}
