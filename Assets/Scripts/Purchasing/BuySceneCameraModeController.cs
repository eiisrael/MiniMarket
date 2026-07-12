using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Controla o modo de compra de terrenos usando a câmera principal do jogador.
/// Mantém os nomes públicos antigos para preservar referências serializadas da cena.
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(950)]
public sealed class BuySceneCameraModeController : MonoBehaviour
{
    [Header("Referências principais")]
    public Camera cameraPrincipal;
    public Transform jogadorRaiz;
    public MonoBehaviour[] componentesExtrasParaDesativar;

    [Header("Câmera de compra")]
    [Min(1f)] public float alturaCameraAerea = 18f;
    [Min(0f)] public float recuoCameraAerea = 0.35f;
    public float rotacaoYCameraAerea;
    public Vector3 offsetFoco = Vector3.zero;

    [Header("Modo ortográfico")]
    public bool usarOrtograficaNoModoCompra = true;
    [Min(1f)] public float tamanhoOrtografico = 16f;

    [Header("Transição")]
    [Min(0.1f)] public float velocidadeMoverCamera = 8f;
    [Min(0.1f)] public float velocidadeRotacionarCamera = 10f;
    [Min(0.1f)] public float velocidadeZoomCamera = 8f;
    public bool entradaInstantanea;
    public bool retornoSuave;
    public bool forcarRetornoInstantaneoAoSair = true;

    [Header("Animação do jogador")]
    public bool forcarIdleAoEntrarNoModoCompra = true;
    public bool zerarParametrosMovimentoAnimator = true;
    public bool pausarAnimatorDuranteBuyScene;
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

    [Header("Input")]
    public bool sairComEscape;
    public bool permitirFecharPelaTeclaDoController;
    public KeyCode teclaFecharPeloController = KeyCode.E;

    [Header("Cursor")]
    public bool mostrarCursorNoModoCompra = true;
    public CursorLockMode lockModeNoModoCompra = CursorLockMode.None;

    [Header("Mensagem")]
    public bool mostrarMensagemNaTela = true;
    public string mensagemModoCompra = "Compra de terreno: escolha uma área - E para voltar";

    [Header("Debug")]
    public bool logarEventos = true;

    private readonly List<BuyableLandAreaMarker> terrenosDestacados = new List<BuyableLandAreaMarker>();
    private readonly List<BehaviourState> behavioursDesativados = new List<BehaviourState>();
    private readonly List<AnimatorState> animadoresSalvos = new List<AnimatorState>();

    private Transform focoAtual;
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

    private struct BehaviourState
    {
        public Behaviour behaviour;
        public bool enabled;
    }

    private struct AnimatorState
    {
        public Animator animator;
        public float speed;
        public bool applyRootMotion;
    }

    private void Awake()
    {
        ResolverReferenciasBasicas();
    }

    private void OnEnable()
    {
        ResolverReferenciasBasicas();
    }

    private void Update()
    {
        if (!modoCompraAtivo)
            return;

        if (permitirFecharPelaTeclaDoController &&
            teclaFecharPeloController != KeyCode.None &&
            Input.GetKeyDown(teclaFecharPeloController))
        {
            SairDoModoCompra();
            return;
        }

        if (sairComEscape && Input.GetKeyDown(KeyCode.Escape))
            SairDoModoCompra();
    }

    private void LateUpdate()
    {
        if (cameraPrincipal == null)
            ResolverReferenciasBasicas();

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
            Debug.LogWarning("[BuySceneCameraModeController] Nenhuma câmera principal foi encontrada.", this);
            return;
        }

        focoAtual = pontoDeFoco != null ? pontoDeFoco : transform;

        if (!modoCompraAtivo && !retornandoCamera)
            SalvarEstadoCameraECursor();

        retornandoCamera = false;
        modoCompraAtivo = true;

        Cursor.visible = mostrarCursorNoModoCompra;
        Cursor.lockState = lockModeNoModoCompra;

        DesativarComponentesExtras();
        PrepararAnimadores();
        DestacarTerrenosDaArea(terrenosDaArea, true);

        if (usarOrtograficaNoModoCompra)
            cameraPrincipal.orthographic = true;

        if (entradaInstantanea)
            AplicarPoseCompraImediata();

        if (logarEventos)
            Debug.Log("[BuyScene] Modo de compra ativado em: " + focoAtual.name, this);
    }

    public void SairDoModoCompra()
    {
        if (!modoCompraAtivo && !retornandoCamera)
            return;

        modoCompraAtivo = false;
        LimparDestaquesDosTerrenos();

        if (forcarRetornoInstantaneoAoSair || !retornoSuave || !estadoCameraSalvo)
        {
            retornandoCamera = false;
            FinalizarRetornoCamera();
        }
        else
        {
            retornandoCamera = true;
        }

        if (logarEventos)
            Debug.Log("[BuyScene] Modo de compra encerrado.", this);
    }

    private void ResolverReferenciasBasicas()
    {
        if (cameraPrincipal == null)
        {
            PlayerCameraController playerCamera = Object.FindAnyObjectByType<PlayerCameraController>(FindObjectsInactive.Include);
            if (playerCamera != null)
                cameraPrincipal = playerCamera.gameCamera;
        }

        if (cameraPrincipal == null)
            cameraPrincipal = Camera.main;

        if (jogadorRaiz == null)
        {
            CameraRelativeMovement movement = Object.FindAnyObjectByType<CameraRelativeMovement>(FindObjectsInactive.Include);
            if (movement != null)
                jogadorRaiz = movement.transform;
        }
    }

    private void SalvarEstadoCameraECursor()
    {
        if (cameraPrincipal == null)
            return;

        cameraPosicaoAntes = cameraPrincipal.transform.position;
        cameraRotacaoAntes = cameraPrincipal.transform.rotation;
        cameraFovAntes = cameraPrincipal.fieldOfView;
        cameraOrtograficaAntes = cameraPrincipal.orthographic;
        cameraOrthographicSizeAntes = cameraPrincipal.orthographicSize;
        cursorLockAntes = Cursor.lockState;
        cursorVisivelAntes = Cursor.visible;
        estadoCameraSalvo = true;
    }

    private void AplicarPoseCompraImediata()
    {
        if (cameraPrincipal == null)
            return;

        Vector3 posicao = CalcularPosicaoCameraAerea();
        cameraPrincipal.transform.SetPositionAndRotation(posicao, CalcularRotacaoCameraAerea(posicao));

        if (usarOrtograficaNoModoCompra)
        {
            cameraPrincipal.orthographic = true;
            cameraPrincipal.orthographicSize = tamanhoOrtografico;
        }
    }

    private void AtualizarCameraModoCompra()
    {
        Vector3 posicaoAlvo = CalcularPosicaoCameraAerea();
        Quaternion rotacaoAlvo = CalcularRotacaoCameraAerea(posicaoAlvo);
        float delta = Mathf.Clamp(Time.unscaledDeltaTime, 0.0001f, 0.05f);

        cameraPrincipal.transform.position = Vector3.Lerp(
            cameraPrincipal.transform.position,
            posicaoAlvo,
            CalcularSuavizacao(velocidadeMoverCamera, delta));

        cameraPrincipal.transform.rotation = Quaternion.Slerp(
            cameraPrincipal.transform.rotation,
            rotacaoAlvo,
            CalcularSuavizacao(velocidadeRotacionarCamera, delta));

        if (usarOrtograficaNoModoCompra)
        {
            cameraPrincipal.orthographic = true;
            cameraPrincipal.orthographicSize = Mathf.Lerp(
                cameraPrincipal.orthographicSize,
                tamanhoOrtografico,
                CalcularSuavizacao(velocidadeZoomCamera, delta));
        }
    }

    private void AtualizarRetornoCamera()
    {
        if (!estadoCameraSalvo || cameraPrincipal == null)
        {
            FinalizarRetornoCamera();
            return;
        }

        float delta = Mathf.Clamp(Time.unscaledDeltaTime, 0.0001f, 0.05f);
        cameraPrincipal.transform.position = Vector3.Lerp(
            cameraPrincipal.transform.position,
            cameraPosicaoAntes,
            CalcularSuavizacao(velocidadeMoverCamera, delta));
        cameraPrincipal.transform.rotation = Quaternion.Slerp(
            cameraPrincipal.transform.rotation,
            cameraRotacaoAntes,
            CalcularSuavizacao(velocidadeRotacionarCamera, delta));

        if (Vector3.Distance(cameraPrincipal.transform.position, cameraPosicaoAntes) <= 0.03f &&
            Quaternion.Angle(cameraPrincipal.transform.rotation, cameraRotacaoAntes) <= 0.35f)
        {
            FinalizarRetornoCamera();
        }
    }

    private Vector3 CalcularPosicaoCameraAerea()
    {
        Vector3 foco = ObterFocoAtual();
        Vector3 recuo = Quaternion.Euler(0f, rotacaoYCameraAerea, 0f) * Vector3.back;
        return foco + Vector3.up * alturaCameraAerea + recuo * recuoCameraAerea;
    }

    private Quaternion CalcularRotacaoCameraAerea(Vector3 posicaoCamera)
    {
        Vector3 direcao = ObterFocoAtual() - posicaoCamera;
        if (direcao.sqrMagnitude <= 0.0001f)
            direcao = Vector3.down;
        return Quaternion.LookRotation(direcao.normalized, Vector3.up);
    }

    private Vector3 ObterFocoAtual()
    {
        return (focoAtual != null ? focoAtual.position : transform.position) + offsetFoco;
    }

    private void DestacarTerrenosDaArea(BuyableLandAreaMarker[] terrenos, bool destacar)
    {
        LimparDestaquesDosTerrenos();

        if (terrenos == null)
            return;

        for (int i = 0; i < terrenos.Length; i++)
        {
            BuyableLandAreaMarker terreno = terrenos[i];
            if (terreno == null)
                continue;

            terreno.DefinirDestaque(destacar);
            terrenosDestacados.Add(terreno);
        }
    }

    private void LimparDestaquesDosTerrenos()
    {
        for (int i = 0; i < terrenosDestacados.Count; i++)
        {
            if (terrenosDestacados[i] != null)
                terrenosDestacados[i].DefinirDestaque(false);
        }
        terrenosDestacados.Clear();
    }

    private void DesativarComponentesExtras()
    {
        behavioursDesativados.Clear();

        if (componentesExtrasParaDesativar == null)
            return;

        for (int i = 0; i < componentesExtrasParaDesativar.Length; i++)
        {
            MonoBehaviour behaviour = componentesExtrasParaDesativar[i];
            if (behaviour == null || behaviour == this)
                continue;

            behavioursDesativados.Add(new BehaviourState
            {
                behaviour = behaviour,
                enabled = behaviour.enabled
            });

            behaviour.enabled = false;
        }
    }

    private void RestaurarComponentesExtras()
    {
        for (int i = 0; i < behavioursDesativados.Count; i++)
        {
            BehaviourState state = behavioursDesativados[i];
            if (state.behaviour != null)
                state.behaviour.enabled = state.enabled;
        }
        behavioursDesativados.Clear();
    }

    private void PrepararAnimadores()
    {
        animadoresSalvos.Clear();

        if (!forcarIdleAoEntrarNoModoCompra || jogadorRaiz == null)
            return;

        Animator[] animadores = jogadorRaiz.GetComponentsInChildren<Animator>(true);
        for (int i = 0; i < animadores.Length; i++)
        {
            Animator animator = animadores[i];
            if (animator == null)
                continue;

            animadoresSalvos.Add(new AnimatorState
            {
                animator = animator,
                speed = animator.speed,
                applyRootMotion = animator.applyRootMotion
            });

            animator.applyRootMotion = false;
            if (zerarParametrosMovimentoAnimator)
                ZerarParametrosAnimator(animator);
            TentarForcarIdle(animator);
            if (pausarAnimatorDuranteBuyScene)
                animator.speed = 0f;
        }
    }

    private void ZerarParametrosAnimator(Animator animator)
    {
        if (animator.runtimeAnimatorController == null)
            return;

        AnimatorControllerParameter[] parameters = animator.parameters;
        for (int i = 0; i < parameters.Length; i++)
        {
            AnimatorControllerParameter parameter = parameters[i];
            string lower = parameter.name.ToLowerInvariant();

            switch (parameter.type)
            {
                case AnimatorControllerParameterType.Float:
                    animator.SetFloat(parameter.nameHash, 0f);
                    break;
                case AnimatorControllerParameterType.Int:
                    animator.SetInteger(parameter.nameHash, 0);
                    break;
                case AnimatorControllerParameterType.Bool:
                    animator.SetBool(parameter.nameHash,
                        lower.Contains("ground") || lower.Contains("chao") || lower.Contains("floor"));
                    break;
                case AnimatorControllerParameterType.Trigger:
                    animator.ResetTrigger(parameter.nameHash);
                    break;
            }
        }
    }

    private void TentarForcarIdle(Animator animator)
    {
        if (animator.runtimeAnimatorController == null || nomesEstadosIdle == null)
            return;

        for (int layer = 0; layer < animator.layerCount; layer++)
        {
            for (int i = 0; i < nomesEstadosIdle.Length; i++)
            {
                string candidate = nomesEstadosIdle[i];
                if (string.IsNullOrWhiteSpace(candidate))
                    continue;

                int hash = Animator.StringToHash(candidate);
                if (!animator.HasState(layer, hash))
                    continue;

                animator.CrossFadeInFixedTime(hash, 0.05f, layer, 0f);
                return;
            }
        }
    }

    private void RestaurarAnimadores()
    {
        for (int i = 0; i < animadoresSalvos.Count; i++)
        {
            AnimatorState state = animadoresSalvos[i];
            if (state.animator == null)
                continue;

            state.animator.speed = state.speed;
            state.animator.applyRootMotion = state.applyRootMotion;
        }
        animadoresSalvos.Clear();
    }

    private void FinalizarRetornoCamera()
    {
        retornandoCamera = false;

        if (estadoCameraSalvo && cameraPrincipal != null)
        {
            cameraPrincipal.transform.SetPositionAndRotation(cameraPosicaoAntes, cameraRotacaoAntes);
            cameraPrincipal.fieldOfView = cameraFovAntes;
            cameraPrincipal.orthographic = cameraOrtograficaAntes;
            cameraPrincipal.orthographicSize = cameraOrthographicSizeAntes;
        }

        Cursor.lockState = cursorLockAntes;
        Cursor.visible = cursorVisivelAntes;
        RestaurarAnimadores();
        RestaurarComponentesExtras();
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

    private static float CalcularSuavizacao(float velocidade, float deltaTime)
    {
        return 1f - Mathf.Exp(-Mathf.Max(0.1f, velocidade) * deltaTime);
    }

    private void OnGUI()
    {
        if (!mostrarMensagemNaTela || !modoCompraAtivo)
            return;

        GUIStyle style = new GUIStyle(GUI.skin.box)
        {
            fontSize = 20,
            alignment = TextAnchor.MiddleCenter
        };

        GUI.Box(new Rect((Screen.width - 620f) * 0.5f, 24f, 620f, 42f), mensagemModoCompra, style);
    }

    private void OnValidate()
    {
        alturaCameraAerea = Mathf.Max(1f, alturaCameraAerea);
        recuoCameraAerea = Mathf.Max(0f, recuoCameraAerea);
        tamanhoOrtografico = Mathf.Max(1f, tamanhoOrtografico);
        velocidadeMoverCamera = Mathf.Max(0.1f, velocidadeMoverCamera);
        velocidadeRotacionarCamera = Mathf.Max(0.1f, velocidadeRotacionarCamera);
        velocidadeZoomCamera = Mathf.Max(0.1f, velocidadeZoomCamera);
    }
}
