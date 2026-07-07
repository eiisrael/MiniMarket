using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Controla o modo BuyScene sem trocar de cena Unity ainda.
/// Ao entrar, a camera principal sobe para uma vista aerea/ortografica,
/// destaca terrenos proximos e bloqueia temporariamente scripts de movimento/mira.
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
    [Tooltip("Altura da camera no modo de compra.")]
    [Min(1f)]
    public float alturaCameraAerea = 18f;

    [Tooltip("Pequeno recuo horizontal para evitar rotacao completamente vertical.")]
    [Min(0f)]
    public float recuoCameraAerea = 0.35f;

    [Tooltip("Rotacao Y da vista aerea. 0 olha pelo eixo Z do mundo.")]
    public float rotacaoYCameraAerea = 0f;

    [Tooltip("Offset aplicado ao foco da camera.")]
    public Vector3 offsetFoco = Vector3.zero;

    [Header("Modo Ortografico")]
    [Tooltip("Recomendado para visualizar terrenos a venda de cima.")]
    public bool usarOrtograficaNoModoCompra = true;

    [Tooltip("Tamanho da visualizacao ortografica. Maior = mostra mais area.")]
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

    [Tooltip("LEGADO. Se Forcar Retorno Instantaneo Ao Sair estiver ligado, este campo e ignorado.")]
    public bool retornoSuave = false;

    [Tooltip("Se ligado, ao apertar ESC a camera volta IMEDIATAMENTE para a camera original do player, sem virar para os lados antes.")]
    public bool forcarRetornoInstantaneoAoSair = true;

    [Header("Input")]
    public bool sairComEscape = true;
    public KeyCode teclaSairModoCompra = KeyCode.Escape;

    [Header("Cursor")]
    [Tooltip("No modo compra, normalmente o cursor fica visivel para selecionar terrenos no futuro.")]
    public bool mostrarCursorNoModoCompra = true;

    public CursorLockMode lockModeNoModoCompra = CursorLockMode.None;

    [Header("Mensagem Temporaria")]
    [Tooltip("Mensagem simples por OnGUI para prototipo. Pode desligar quando criar uma UI propria no Canvas.")]
    public bool mostrarMensagemNaTela = true;

    public string mensagemModoCompra = "BuyScene: terrenos a venda - ESC para voltar";

    [Header("Debug")]
    public bool logarEventos = true;

    private Transform focoAtual;
    private readonly List<BuyableLandAreaMarker> terrenosDestacados = new List<BuyableLandAreaMarker>();
    private readonly List<MonoBehaviour> componentesDesativadosAutomaticamente = new List<MonoBehaviour>();

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

    private void Awake()
    {
        ResolverReferenciasBasicas();
    }

    private void Update()
    {
        if (modoCompraAtivo && sairComEscape && Input.GetKeyDown(teclaSairModoCompra))
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
        if (!modoCompraAtivo)
            return;

        modoCompraAtivo = false;
        retornandoCamera = false;
        LimparDestaquesDosTerrenos();

        if (forcarRetornoInstantaneoAoSair || !retornoSuave || !estadoCameraSalvo)
        {
            FinalizarRetornoCamera();
        }
        else
        {
            retornandoCamera = true;
        }

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
        AdicionarComponentesPorNome(jogadorRaiz, "PlayerObjectGrabberHardcore");
        AdicionarComponentesPorNome(jogadorRaiz, "MouseLookHardcore");

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
        if (componente == null)
            return;

        if (componente == this)
            return;

        if (componentesDesativadosAutomaticamente.Contains(componente))
            return;

        componentesDesativadosAutomaticamente.Add(componente);
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
