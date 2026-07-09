using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Area de entrada da BuyScene.
///
/// Correção atual:
/// - OnValidate não cria GameObject, não usa SetParent e não adiciona LineRenderer.
/// - Remove spam "SendMessage cannot be called during Awake/OnValidate" visto nos logs.
/// - Mantém abrir/fechar BuyScene, detecção do player e marcação visual.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Collider))]
public class BuySceneEntryTrigger : MonoBehaviour
{
    [Header("CONTROLADOR DA BUYSCENE")]
    public BuySceneCameraModeController controladorBuyScene;
    public Transform pontoDeFocoDaCamera;
    public BuyableLandAreaMarker[] terrenosDestaArea;
    public bool usarTerrenosProximosSeListaVazia = true;
    public float raioBuscaTerrenosProximos = 60f;

    [Header("ABRIR / FECHAR")]
    public bool usarTeclaParaAbrirFechar = true;
    public KeyCode teclaAbrirFechar = KeyCode.E;
    public bool entrarAutomaticamenteAoPassar = false;
    public bool exigirTeclaParaEntrar = true;
    public KeyCode teclaEntrar = KeyCode.E;
    [Min(0f)] public float intervaloMinimoEntreAtivacoes = 0.25f;

    [Header("DETECCAO DO PLAYER")]
    public string tagDoPlayer = "Player";
    public Transform jogadorRaizOpcional;
    public bool aceitarCharacterController = true;
    public bool aceitarScriptPlayerMove = true;
    public bool usarDeteccaoPorOverlapSegura = true;
    public LayerMask camadasDeteccao = ~0;
    public float margemVerticalDeteccao = 2.5f;

    [Header("MARCACAO VISUAL DA AREA")]
    public bool mostrarMarcacaoVisual = true;
    public bool mostrarXCentral = true;
    public Color corNormal = new Color(1f, 0.92f, 0f, 1f);
    public Color corPlayerDentro = new Color(0.1f, 1f, 0.1f, 1f);
    [Min(0.01f)] public float larguraLinha = 0.08f;
    public float alturaAcimaDoCollider = 0.08f;
    public bool atualizarVisualEmTempoReal = true;

    [Header("SINCRONIA COM STATUS DOS TERRENOS")]
    public bool sincronizarMarcacaoComStatusDosTerrenos = true;
    public bool calcadaIndisponivelSomenteQuandoTodosTerrenosIndisponiveis = true;
    public bool manterMarcacaoVisivelQuandoIndisponivel = true;
    public bool sincronizarComTerrenosEncontradosAutomaticamente = true;

    [Header("DESTAQUE DOS TERRENOS")]
    public bool destacarTerrenosAoDetectarPlayer = true;
    public bool limparDestaqueAoSairDaArea = true;

    [Header("DEBUG")]
    public bool logarEventos = true;
    public bool desenharGizmos = true;
    public Color corGizmo = new Color(1f, 0.92f, 0f, 0.25f);

    private Collider areaCollider;
    private LineRenderer linhaBorda;
    private LineRenderer linhaDiagonalA;
    private LineRenderer linhaDiagonalB;
    private Material materialLinhas;
    private bool playerDentro;
    private float ultimoTempoAtivacao = -999f;
    private readonly Collider[] resultadosOverlap = new Collider[32];

    private const string NomeLinhaBorda = "BuyScene_Entrada_Borda";
    private const string NomeLinhaDiagonalA = "BuyScene_Entrada_Diagonal_A";
    private const string NomeLinhaDiagonalB = "BuyScene_Entrada_Diagonal_B";

    private void Reset()
    {
        areaCollider = GetComponent<Collider>();
        PrepararColliderComoTrigger();
    }

    private void Awake()
    {
        areaCollider = GetComponent<Collider>();
        PrepararColliderComoTrigger();
        ResolverReferencias();
        CriarRenderizadores();
        AtualizarVisualCompleto();
    }

    private void OnEnable()
    {
        areaCollider = GetComponent<Collider>();
        PrepararColliderComoTrigger();
        ResolverReferencias();
        CriarRenderizadores();
        AtualizarVisualCompleto();
    }

    private void Start()
    {
        ResolverReferencias();
        AtualizarVisualCompleto();
    }

    private void Update()
    {
        if (usarDeteccaoPorOverlapSegura)
            VerificarPlayerPorOverlapSeguro();

        ProcessarInputAbrirFechar();

        if (atualizarVisualEmTempoReal)
            AtualizarVisualCompleto();
    }

    private void OnValidate()
    {
        larguraLinha = Mathf.Max(0.01f, larguraLinha);
        raioBuscaTerrenosProximos = Mathf.Max(1f, raioBuscaTerrenosProximos);
        intervaloMinimoEntreAtivacoes = Mathf.Max(0f, intervaloMinimoEntreAtivacoes);

        // Importante: OnValidate não pode criar GameObject, SetParent ou AddComponent.
        // Atualiza apenas referências já existentes, evitando SendMessage spam/travadas no Editor.
        areaCollider = GetComponent<Collider>();
        AtualizarPosicoesVisual();
        AtualizarCorVisual();
        DefinirLinhasAtivas(mostrarMarcacaoVisual);
    }

    private void PrepararColliderComoTrigger()
    {
        if (areaCollider == null)
            areaCollider = GetComponent<Collider>();

        if (areaCollider != null)
            areaCollider.isTrigger = true;
    }

    private void ResolverReferencias()
    {
        if (controladorBuyScene == null)
            controladorBuyScene = Object.FindFirstObjectByType<BuySceneCameraModeController>(FindObjectsInactive.Include);

        if (pontoDeFocoDaCamera == null)
            pontoDeFocoDaCamera = transform;

        if (jogadorRaizOpcional == null)
            jogadorRaizOpcional = TentarEncontrarPlayerAutomaticamente();
    }

    private Transform TentarEncontrarPlayerAutomaticamente()
    {
        if (!string.IsNullOrEmpty(tagDoPlayer))
        {
            try
            {
                GameObject playerPorTag = GameObject.FindGameObjectWithTag(tagDoPlayer);
                if (playerPorTag != null)
                    return playerPorTag.transform;
            }
            catch { }
        }

        CharacterController controller = Object.FindFirstObjectByType<CharacterController>(FindObjectsInactive.Include);
        return controller != null ? controller.transform : null;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!EhPlayer(other))
            return;

        RegistrarEntradaDoPlayer(ObterRaizPlayer(other.transform));
    }

    private void OnTriggerExit(Collider other)
    {
        if (!EhPlayer(other))
            return;

        RegistrarSaidaDoPlayer();
    }

    private void ProcessarInputAbrirFechar()
    {
        if (!playerDentro)
            return;

        if (BuyScenePurchaseConfirmationPanel.ExistePainelAberto)
            return;

        if (usarTeclaParaAbrirFechar)
        {
            if (Input.GetKeyDown(teclaAbrirFechar))
                AlternarBuyScene();
            return;
        }

        if (exigirTeclaParaEntrar && Input.GetKeyDown(teclaEntrar))
            TentarEntrarNaBuyScene();
    }

    private void VerificarPlayerPorOverlapSeguro()
    {
        Transform playerEncontrado = EncontrarPlayerDentroDaArea();
        bool dentroAgora = playerEncontrado != null;

        if (dentroAgora && !playerDentro)
            RegistrarEntradaDoPlayer(playerEncontrado);
        else if (!dentroAgora && playerDentro)
            RegistrarSaidaDoPlayer();
    }

    private Transform EncontrarPlayerDentroDaArea()
    {
        if (areaCollider == null)
            areaCollider = GetComponent<Collider>();

        if (areaCollider == null)
            return null;

        if (jogadorRaizOpcional != null && PontoDentroDaArea(jogadorRaizOpcional.position))
            return jogadorRaizOpcional;

        Bounds bounds = areaCollider.bounds;
        bounds.Expand(new Vector3(0f, margemVerticalDeteccao, 0f));

        int quantidade = Physics.OverlapBoxNonAlloc(bounds.center, bounds.extents, resultadosOverlap, Quaternion.identity, camadasDeteccao, QueryTriggerInteraction.Collide);
        for (int i = 0; i < quantidade; i++)
        {
            Collider encontrado = resultadosOverlap[i];
            if (encontrado == null || encontrado == areaCollider)
                continue;

            if (EhPlayer(encontrado))
                return ObterRaizPlayer(encontrado.transform);
        }

        return null;
    }

    private bool PontoDentroDaArea(Vector3 pontoMundo)
    {
        if (areaCollider == null)
            return false;

        Bounds bounds = areaCollider.bounds;
        bounds.Expand(new Vector3(0f, margemVerticalDeteccao, 0f));
        return bounds.Contains(pontoMundo);
    }

    private void RegistrarEntradaDoPlayer(Transform raizPlayer)
    {
        if (jogadorRaizOpcional == null && raizPlayer != null)
            jogadorRaizOpcional = raizPlayer;

        if (playerDentro)
            return;

        playerDentro = true;
        AtualizarCorVisual();

        if (destacarTerrenosAoDetectarPlayer)
            DefinirDestaqueTerrenos(true);

        if (logarEventos)
            Debug.Log("[BuySceneEntryTrigger] Player entrou na area de compra: " + gameObject.name);

        if (!usarTeclaParaAbrirFechar && entrarAutomaticamenteAoPassar && !exigirTeclaParaEntrar)
            TentarEntrarNaBuyScene();
    }

    private void RegistrarSaidaDoPlayer()
    {
        if (!playerDentro)
            return;

        playerDentro = false;
        AtualizarCorVisual();

        if (limparDestaqueAoSairDaArea && (controladorBuyScene == null || !controladorBuyScene.ModoCompraAtivo))
            DefinirDestaqueTerrenos(false);

        if (logarEventos)
            Debug.Log("[BuySceneEntryTrigger] Player saiu da area de compra: " + gameObject.name);
    }

    public void AlternarBuyScene()
    {
        ResolverReferencias();

        if (controladorBuyScene == null)
        {
            Debug.LogWarning("[BuySceneEntryTrigger] Nenhum BuySceneCameraModeController foi encontrado na cena.");
            return;
        }

        if (Time.time < ultimoTempoAtivacao + intervaloMinimoEntreAtivacoes)
            return;

        ultimoTempoAtivacao = Time.time;

        if (controladorBuyScene.ModoCompraAtivo)
        {
            controladorBuyScene.SairDoModoCompra();

            if (destacarTerrenosAoDetectarPlayer)
                DefinirDestaqueTerrenos(playerDentro);

            AtualizarCorVisual();
            return;
        }

        EntrarNaBuySceneSemCooldown();
    }

    public void TentarEntrarNaBuyScene()
    {
        ResolverReferencias();

        if (controladorBuyScene == null || controladorBuyScene.ModoCompraAtivo)
            return;

        if (Time.time < ultimoTempoAtivacao + intervaloMinimoEntreAtivacoes)
            return;

        ultimoTempoAtivacao = Time.time;
        EntrarNaBuySceneSemCooldown();
    }

    private void EntrarNaBuySceneSemCooldown()
    {
        if (controladorBuyScene == null)
            return;

        Transform foco = pontoDeFocoDaCamera != null ? pontoDeFocoDaCamera : transform;
        BuyableLandAreaMarker[] terrenosParaCamera = ObterTerrenosParaCamera(foco.position);

        if (destacarTerrenosAoDetectarPlayer)
            DefinirDestaqueTerrenos(true, terrenosParaCamera);

        AtualizarCorVisual();
        controladorBuyScene.EntrarNoModoCompra(foco, terrenosParaCamera);
    }

    private BuyableLandAreaMarker[] ObterTerrenosParaCamera(Vector3 posicaoReferencia)
    {
        if (terrenosDestaArea != null && terrenosDestaArea.Length > 0)
            return terrenosDestaArea;

        if (!usarTerrenosProximosSeListaVazia)
            return terrenosDestaArea;

        BuyableLandAreaMarker[] todos = Object.FindObjectsByType<BuyableLandAreaMarker>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (todos == null || todos.Length == 0)
            return terrenosDestaArea;

        int quantidadeProxima = 0;
        for (int i = 0; i < todos.Length; i++)
        {
            if (todos[i] != null && Vector3.Distance(posicaoReferencia, todos[i].transform.position) <= raioBuscaTerrenosProximos)
                quantidadeProxima++;
        }

        if (quantidadeProxima == 0)
            return todos;

        BuyableLandAreaMarker[] proximos = new BuyableLandAreaMarker[quantidadeProxima];
        int indice = 0;
        for (int i = 0; i < todos.Length; i++)
        {
            if (todos[i] != null && Vector3.Distance(posicaoReferencia, todos[i].transform.position) <= raioBuscaTerrenosProximos)
                proximos[indice++] = todos[i];
        }

        return proximos;
    }

    private void DefinirDestaqueTerrenos(bool destacar)
    {
        DefinirDestaqueTerrenos(destacar, terrenosDestaArea);
    }

    private void DefinirDestaqueTerrenos(bool destacar, BuyableLandAreaMarker[] terrenos)
    {
        if (terrenos == null || terrenos.Length == 0)
            return;

        for (int i = 0; i < terrenos.Length; i++)
        {
            if (terrenos[i] != null)
                terrenos[i].DefinirDestaque(destacar);
        }
    }

    private bool EhPlayer(Collider other)
    {
        if (other == null)
            return false;

        Transform outro = other.transform;

        if (jogadorRaizOpcional != null)
        {
            if (outro == jogadorRaizOpcional || outro.IsChildOf(jogadorRaizOpcional) || outro.root == jogadorRaizOpcional.root)
                return true;
        }

        if (!string.IsNullOrEmpty(tagDoPlayer))
        {
            try
            {
                if (other.CompareTag(tagDoPlayer))
                    return true;
            }
            catch { }
        }

        if (aceitarCharacterController && other.GetComponentInParent<CharacterController>() != null)
            return true;

        if (aceitarScriptPlayerMove && TemComponenteDeMovimentoDoPlayer(other.transform))
            return true;

        return false;
    }

    private Transform ObterRaizPlayer(Transform origem)
    {
        if (origem == null)
            return null;

        if (jogadorRaizOpcional != null)
            return jogadorRaizOpcional;

        CharacterController characterController = origem.GetComponentInParent<CharacterController>();
        if (characterController != null)
            return characterController.transform;

        MonoBehaviour[] componentes = origem.GetComponentsInParent<MonoBehaviour>(true);
        for (int i = 0; i < componentes.Length; i++)
        {
            MonoBehaviour componente = componentes[i];
            if (componente == null)
                continue;

            string nomeTipo = componente.GetType().Name;
            if (nomeTipo == "PlayerMove" || nomeTipo == "Player_Move" || nomeTipo.Contains("PlayerMove") || nomeTipo.Contains("Player_Move"))
                return componente.transform;
        }

        return origem.root;
    }

    private bool TemComponenteDeMovimentoDoPlayer(Transform origem)
    {
        if (origem == null)
            return false;

        MonoBehaviour[] componentes = origem.GetComponentsInParent<MonoBehaviour>(true);
        for (int i = 0; i < componentes.Length; i++)
        {
            MonoBehaviour componente = componentes[i];
            if (componente == null)
                continue;

            string nomeTipo = componente.GetType().Name;
            if (nomeTipo == "PlayerMove" || nomeTipo == "Player_Move" || nomeTipo.Contains("PlayerMove") || nomeTipo.Contains("Player_Move"))
                return true;
        }

        return false;
    }

    private void CriarRenderizadores()
    {
        if (linhaBorda == null)
            linhaBorda = CriarLinha(NomeLinhaBorda, 5);

        if (linhaDiagonalA == null)
            linhaDiagonalA = CriarLinha(NomeLinhaDiagonalA, 2);

        if (linhaDiagonalB == null)
            linhaDiagonalB = CriarLinha(NomeLinhaDiagonalB, 2);
    }

    private LineRenderer CriarLinha(string nome, int quantidadePontos)
    {
        Transform existente = transform.Find(nome);
        GameObject objetoLinha = existente != null ? existente.gameObject : new GameObject(nome);
        objetoLinha.transform.SetParent(transform, false);
        objetoLinha.transform.localPosition = Vector3.zero;
        objetoLinha.transform.localRotation = Quaternion.identity;
        objetoLinha.transform.localScale = Vector3.one;

        LineRenderer linha = objetoLinha.GetComponent<LineRenderer>();
        if (linha == null)
            linha = objetoLinha.AddComponent<LineRenderer>();

        linha.useWorldSpace = true;
        linha.loop = false;
        linha.positionCount = quantidadePontos;
        linha.widthMultiplier = larguraLinha;
        linha.alignment = LineAlignment.View;
        linha.textureMode = LineTextureMode.Stretch;
        linha.shadowCastingMode = ShadowCastingMode.Off;
        linha.receiveShadows = false;

        Material material = ObterMaterialLinhas();
        if (material != null)
            linha.material = material;

        return linha;
    }

    private Material ObterMaterialLinhas()
    {
        if (materialLinhas != null)
            return materialLinhas;

        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Unlit/Color");
        if (shader == null) shader = Shader.Find("Hidden/Internal-Colored");
        if (shader == null) return null;

        materialLinhas = new Material(shader);
        materialLinhas.name = "BuySceneEntryTrigger_LineMaterial";
        materialLinhas.hideFlags = HideFlags.HideAndDontSave;
        return materialLinhas;
    }

    private void AtualizarVisualCompleto()
    {
        if (!mostrarMarcacaoVisual)
        {
            DefinirLinhasAtivas(false);
            return;
        }

        CriarRenderizadores();
        AtualizarPosicoesVisual();
        AtualizarCorVisual();
        DefinirLinhasAtivas(true);
    }

    private void DefinirLinhasAtivas(bool ativo)
    {
        if (linhaBorda != null) linhaBorda.gameObject.SetActive(ativo);
        if (linhaDiagonalA != null) linhaDiagonalA.gameObject.SetActive(ativo && mostrarXCentral);
        if (linhaDiagonalB != null) linhaDiagonalB.gameObject.SetActive(ativo && mostrarXCentral);
    }

    private void AtualizarCorVisual()
    {
        Color corAtual = ObterCorVisualAtual();
        AplicarCorNaLinha(linhaBorda, corAtual);
        AplicarCorNaLinha(linhaDiagonalA, corAtual);
        AplicarCorNaLinha(linhaDiagonalB, corAtual);
    }

    private Color ObterCorVisualAtual()
    {
        if (!sincronizarMarcacaoComStatusDosTerrenos)
            return playerDentro ? corPlayerDentro : corNormal;

        BuyableLandAreaMarker terrenoReferencia = ObterTerrenoReferenciaParaCor();
        if (terrenoReferencia == null)
            return playerDentro ? corPlayerDentro : corNormal;

        BuyableLandAreaMarker.EstadoDoTerreno estado = CalcularEstadoAgregadoDosTerrenos(terrenoReferencia);
        if (estado == BuyableLandAreaMarker.EstadoDoTerreno.Indisponivel) return terrenoReferencia.corIndisponivel;
        if (estado == BuyableLandAreaMarker.EstadoDoTerreno.Destacado) return terrenoReferencia.corDestacado;
        return terrenoReferencia.corDisponivel;
    }

    private BuyableLandAreaMarker[] ObterTerrenosParaSincronia()
    {
        if (terrenosDestaArea != null && terrenosDestaArea.Length > 0)
            return terrenosDestaArea;

        if (!sincronizarComTerrenosEncontradosAutomaticamente)
            return null;

        Transform foco = pontoDeFocoDaCamera != null ? pontoDeFocoDaCamera : transform;
        return ObterTerrenosParaCamera(foco.position);
    }

    private BuyableLandAreaMarker ObterTerrenoReferenciaParaCor()
    {
        BuyableLandAreaMarker[] terrenos = ObterTerrenosParaSincronia();
        if (terrenos == null || terrenos.Length == 0)
            return null;

        for (int i = 0; i < terrenos.Length; i++)
        {
            if (terrenos[i] != null && terrenos[i].estadoAtual != BuyableLandAreaMarker.EstadoDoTerreno.Indisponivel)
                return terrenos[i];
        }

        for (int i = 0; i < terrenos.Length; i++)
        {
            if (terrenos[i] != null)
                return terrenos[i];
        }

        return null;
    }

    private BuyableLandAreaMarker.EstadoDoTerreno CalcularEstadoAgregadoDosTerrenos(BuyableLandAreaMarker terrenoReferencia)
    {
        BuyableLandAreaMarker[] terrenos = ObterTerrenosParaSincronia();
        if (terrenos == null || terrenos.Length == 0)
            return terrenoReferencia.estadoAtual;

        bool todosIndisponiveis = true;
        bool existeDestacado = false;
        bool existeDisponivel = false;

        for (int i = 0; i < terrenos.Length; i++)
        {
            BuyableLandAreaMarker terreno = terrenos[i];
            if (terreno == null)
                continue;

            if (terreno.estadoAtual != BuyableLandAreaMarker.EstadoDoTerreno.Indisponivel)
                todosIndisponiveis = false;
            if (terreno.estadoAtual == BuyableLandAreaMarker.EstadoDoTerreno.Destacado)
                existeDestacado = true;
            if (terreno.estadoAtual == BuyableLandAreaMarker.EstadoDoTerreno.Disponivel)
                existeDisponivel = true;
        }

        if (calcadaIndisponivelSomenteQuandoTodosTerrenosIndisponiveis)
        {
            if (todosIndisponiveis) return BuyableLandAreaMarker.EstadoDoTerreno.Indisponivel;
            if (existeDestacado) return BuyableLandAreaMarker.EstadoDoTerreno.Destacado;
            if (existeDisponivel) return BuyableLandAreaMarker.EstadoDoTerreno.Disponivel;
        }

        return terrenoReferencia.estadoAtual;
    }

    private void AplicarCorNaLinha(LineRenderer linha, Color cor)
    {
        if (linha == null)
            return;

        linha.widthMultiplier = larguraLinha;
        linha.startColor = cor;
        linha.endColor = cor;

        if (linha.material != null && linha.material.HasProperty("_Color"))
            linha.material.color = cor;
    }

    private void AtualizarPosicoesVisual()
    {
        if (areaCollider == null)
            areaCollider = GetComponent<Collider>();

        if (areaCollider == null || linhaBorda == null || linhaDiagonalA == null || linhaDiagonalB == null)
            return;

        Vector3 p0, p1, p2, p3;
        CalcularCantosSuperiores(out p0, out p1, out p2, out p3);

        linhaBorda.positionCount = 5;
        linhaBorda.SetPosition(0, p0);
        linhaBorda.SetPosition(1, p1);
        linhaBorda.SetPosition(2, p2);
        linhaBorda.SetPosition(3, p3);
        linhaBorda.SetPosition(4, p0);

        linhaDiagonalA.positionCount = 2;
        linhaDiagonalA.SetPosition(0, p0);
        linhaDiagonalA.SetPosition(1, p2);

        linhaDiagonalB.positionCount = 2;
        linhaDiagonalB.SetPosition(0, p1);
        linhaDiagonalB.SetPosition(1, p3);
    }

    private void CalcularCantosSuperiores(out Vector3 p0, out Vector3 p1, out Vector3 p2, out Vector3 p3)
    {
        BoxCollider box = areaCollider as BoxCollider;
        if (box != null)
        {
            Vector3 centro = box.center;
            Vector3 metade = box.size * 0.5f;
            float y = centro.y + metade.y + alturaAcimaDoCollider;
            p0 = transform.TransformPoint(new Vector3(centro.x - metade.x, y, centro.z - metade.z));
            p1 = transform.TransformPoint(new Vector3(centro.x - metade.x, y, centro.z + metade.z));
            p2 = transform.TransformPoint(new Vector3(centro.x + metade.x, y, centro.z + metade.z));
            p3 = transform.TransformPoint(new Vector3(centro.x + metade.x, y, centro.z - metade.z));
            return;
        }

        Bounds b = areaCollider.bounds;
        float topo = b.max.y + alturaAcimaDoCollider;
        p0 = new Vector3(b.min.x, topo, b.min.z);
        p1 = new Vector3(b.min.x, topo, b.max.z);
        p2 = new Vector3(b.max.x, topo, b.max.z);
        p3 = new Vector3(b.max.x, topo, b.min.z);
    }

    private void OnDrawGizmos()
    {
        if (!desenharGizmos)
            return;

        Collider col = GetComponent<Collider>();
        if (col == null)
            return;

        Gizmos.color = corGizmo;
        BoxCollider box = col as BoxCollider;
        if (box != null)
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawCube(box.center, box.size);
            Gizmos.color = new Color(corGizmo.r, corGizmo.g, corGizmo.b, 1f);
            Gizmos.DrawWireCube(box.center, box.size);
            return;
        }

        Gizmos.matrix = Matrix4x4.identity;
        Gizmos.DrawCube(col.bounds.center, col.bounds.size);
        Gizmos.color = new Color(corGizmo.r, corGizmo.g, corGizmo.b, 1f);
        Gizmos.DrawWireCube(col.bounds.center, col.bounds.size);
    }

    private void OnDestroy()
    {
        if (Application.isPlaying && materialLinhas != null)
            Destroy(materialLinhas);
    }
}
