using UnityEngine;

/// <summary>
/// Script unico da area de entrada da BuyScene.
/// Responsabilidades:
/// - Desenhar a marcacao visual da area da calcada no Game View.
/// - Detectar o player dentro da area.
/// - Abrir e fechar a BuyScene com a mesma tecla configuravel no Inspector.
///
/// Fluxo recomendado:
/// Player entra na area -> aperta E -> abre BuyScene.
/// Player ainda esta na area -> aperta E novamente -> fecha BuyScene.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Collider))]
public class BuySceneEntryTrigger : MonoBehaviour
{
    [Header("CONTROLADOR DA BUYSCENE")]
    [Tooltip("Arraste aqui o objeto que possui o BuySceneCameraModeController.")]
    public BuySceneCameraModeController controladorBuyScene;

    [Tooltip("Ponto que a camera aerea vai focar. Pode ser um Empty no meio dos terrenos.")]
    public Transform pontoDeFocoDaCamera;

    [Tooltip("Terrenos que devem ser destacados quando o player entrar nessa area.")]
    public BuyableLandAreaMarker[] terrenosDestaArea;

    [Tooltip("Se a lista acima estiver vazia, o script tenta encontrar terrenos proximos automaticamente.")]
    public bool usarTerrenosProximosSeListaVazia = true;

    [Tooltip("Raio usado para buscar terrenos proximos automaticamente.")]
    public float raioBuscaTerrenosProximos = 60f;

    [Header("ABRIR / FECHAR")]
    [Tooltip("Modo recomendado. A mesma tecla abre e fecha a BuyScene enquanto o player estiver na area.")]
    public bool usarTeclaParaAbrirFechar = true;

    [Tooltip("Tecla unica para abrir e fechar a BuyScene. Padrao: E.")]
    public KeyCode teclaAbrirFechar = KeyCode.E;

    [Tooltip("Mantido por compatibilidade. Se Usar Tecla Para Abrir Fechar estiver ligado, este campo nao abre automaticamente.")]
    public bool entrarAutomaticamenteAoPassar = false;

    [Tooltip("LEGADO. Mantido para nao quebrar configuracoes antigas. Prefira Usar Tecla Para Abrir Fechar.")]
    public bool exigirTeclaParaEntrar = true;

    [Tooltip("LEGADO. Mantido para cenas antigas. Prefira Tecla Abrir Fechar.")]
    public KeyCode teclaEntrar = KeyCode.E;

    [Min(0f)]
    [Tooltip("Evita duplo clique/duplo trigger no mesmo frame ou em sequencia rapida.")]
    public float intervaloMinimoEntreAtivacoes = 0.25f;

    [Header("DETECCAO DO PLAYER")]
    public string tagDoPlayer = "Player";

    [Tooltip("Opcional. Se arrastar o player aqui, a deteccao fica ainda mais segura.")]
    public Transform jogadorRaizOpcional;

    public bool aceitarCharacterController = true;
    public bool aceitarScriptPlayerMove = true;

    [Tooltip("Mantem uma verificacao extra por OverlapBox. Ajuda quando OnTriggerEnter falha.")]
    public bool usarDeteccaoPorOverlapSegura = true;

    [Tooltip("Camadas que podem ser detectadas. Normalmente deixe Everything.")]
    public LayerMask camadasDeteccao = ~0;

    [Tooltip("Margem vertical extra para detectar o player mesmo se o collider estiver baixo na calcada.")]
    public float margemVerticalDeteccao = 2.5f;

    [Header("MARCACAO VISUAL DA AREA")]
    public bool mostrarMarcacaoVisual = true;
    public bool mostrarXCentral = true;

    [Tooltip("Cor normal da area da calcada.")]
    public Color corNormal = new Color(1f, 0.92f, 0f, 1f);

    [Tooltip("Cor quando o player esta dentro da area.")]
    public Color corPlayerDentro = new Color(0.1f, 1f, 0.1f, 1f);

    [Min(0.01f)]
    public float larguraLinha = 0.08f;

    [Tooltip("Altura da linha acima do collider. Aumente se a linha ficar dentro da calcada.")]
    public float alturaAcimaDoCollider = 0.08f;

    [Tooltip("Atualiza as linhas em tempo real enquanto voce move/escala o objeto.")]
    public bool atualizarVisualEmTempoReal = true;

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
        if (larguraLinha < 0.01f)
            larguraLinha = 0.01f;

        if (raioBuscaTerrenosProximos < 1f)
            raioBuscaTerrenosProximos = 1f;

        if (intervaloMinimoEntreAtivacoes < 0f)
            intervaloMinimoEntreAtivacoes = 0f;

        if (!Application.isPlaying)
            return;

        areaCollider = GetComponent<Collider>();
        PrepararColliderComoTrigger();
        CriarRenderizadores();
        AtualizarVisualCompleto();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!EhPlayer(other))
            return;

        Transform raizPlayer = ObterRaizPlayer(other.transform);
        RegistrarEntradaDoPlayer(raizPlayer);
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

        if (usarTeclaParaAbrirFechar)
        {
            if (Input.GetKeyDown(teclaAbrirFechar))
                AlternarBuyScene();

            return;
        }

        if (exigirTeclaParaEntrar && Input.GetKeyDown(teclaEntrar))
            TentarEntrarNaBuyScene();
    }

    private void PrepararColliderComoTrigger()
    {
        if (areaCollider == null)
            areaCollider = GetComponent<Collider>();

        if (areaCollider == null)
            return;

        areaCollider.isTrigger = true;
    }

    private void ResolverReferencias()
    {
        if (controladorBuyScene == null)
            controladorBuyScene = FindObjectOfType<BuySceneCameraModeController>();

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
            catch
            {
                // A tag pode nao existir. Nesse caso, tenta pelo CharacterController.
            }
        }

        CharacterController characterController = FindObjectOfType<CharacterController>();

        if (characterController != null)
            return characterController.transform;

        return null;
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

        BoxCollider box = areaCollider as BoxCollider;

        if (box != null)
        {
            Vector3 centro = transform.TransformPoint(box.center);
            Vector3 escala = transform.lossyScale;
            Vector3 metade = new Vector3(
                Mathf.Abs(box.size.x * escala.x) * 0.5f,
                Mathf.Abs(box.size.y * escala.y) * 0.5f + margemVerticalDeteccao,
                Mathf.Abs(box.size.z * escala.z) * 0.5f
            );

            int quantidade = Physics.OverlapBoxNonAlloc(
                centro,
                metade,
                resultadosOverlap,
                transform.rotation,
                camadasDeteccao,
                QueryTriggerInteraction.Collide
            );

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

        Bounds bounds = areaCollider.bounds;
        bounds.Expand(new Vector3(0f, margemVerticalDeteccao, 0f));

        int quantidadeBounds = Physics.OverlapBoxNonAlloc(
            bounds.center,
            bounds.extents,
            resultadosOverlap,
            Quaternion.identity,
            camadasDeteccao,
            QueryTriggerInteraction.Collide
        );

        for (int i = 0; i < quantidadeBounds; i++)
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

        BoxCollider box = areaCollider as BoxCollider;

        if (box != null)
        {
            Vector3 local = transform.InverseTransformPoint(pontoMundo);
            Vector3 centro = box.center;
            Vector3 metade = box.size * 0.5f;

            bool dentroX = local.x >= centro.x - metade.x && local.x <= centro.x + metade.x;
            bool dentroZ = local.z >= centro.z - metade.z && local.z <= centro.z + metade.z;
            bool dentroY = local.y >= centro.y - metade.y - margemVerticalDeteccao &&
                           local.y <= centro.y + metade.y + margemVerticalDeteccao;

            return dentroX && dentroY && dentroZ;
        }

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

            if (logarEventos)
                Debug.Log("[BuySceneEntryTrigger] Fechou BuyScene pela tecla: " + teclaAbrirFechar);

            return;
        }

        EntrarNaBuySceneSemCooldown();
    }

    public void TentarEntrarNaBuyScene()
    {
        ResolverReferencias();

        if (controladorBuyScene == null)
        {
            Debug.LogWarning("[BuySceneEntryTrigger] Nenhum BuySceneCameraModeController foi encontrado na cena.");
            return;
        }

        if (controladorBuyScene.ModoCompraAtivo)
            return;

        if (Time.time < ultimoTempoAtivacao + intervaloMinimoEntreAtivacoes)
            return;

        ultimoTempoAtivacao = Time.time;
        EntrarNaBuySceneSemCooldown();
    }

    private void EntrarNaBuySceneSemCooldown()
    {
        Transform foco = pontoDeFocoDaCamera != null ? pontoDeFocoDaCamera : transform;
        BuyableLandAreaMarker[] terrenosParaCamera = ObterTerrenosParaCamera(foco.position);

        if (destacarTerrenosAoDetectarPlayer)
            DefinirDestaqueTerrenos(true, terrenosParaCamera);

        controladorBuyScene.EntrarNoModoCompra(foco, terrenosParaCamera);

        if (logarEventos)
            Debug.Log("[BuySceneEntryTrigger] Abriu BuyScene pela area: " + gameObject.name);
    }

    private BuyableLandAreaMarker[] ObterTerrenosParaCamera(Vector3 posicaoReferencia)
    {
        if (terrenosDestaArea != null && terrenosDestaArea.Length > 0)
            return terrenosDestaArea;

        if (!usarTerrenosProximosSeListaVazia)
            return terrenosDestaArea;

        BuyableLandAreaMarker[] todos = FindObjectsOfType<BuyableLandAreaMarker>();

        if (todos == null || todos.Length == 0)
            return terrenosDestaArea;

        int quantidadeProxima = 0;

        for (int i = 0; i < todos.Length; i++)
        {
            if (todos[i] == null)
                continue;

            float distancia = Vector3.Distance(posicaoReferencia, todos[i].transform.position);

            if (distancia <= raioBuscaTerrenosProximos)
                quantidadeProxima++;
        }

        if (quantidadeProxima == 0)
            return todos;

        BuyableLandAreaMarker[] proximos = new BuyableLandAreaMarker[quantidadeProxima];
        int indice = 0;

        for (int i = 0; i < todos.Length; i++)
        {
            if (todos[i] == null)
                continue;

            float distancia = Vector3.Distance(posicaoReferencia, todos[i].transform.position);

            if (distancia <= raioBuscaTerrenosProximos)
            {
                proximos[indice] = todos[i];
                indice++;
            }
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
            BuyableLandAreaMarker terreno = terrenos[i];

            if (terreno != null)
                terreno.DefinirDestaque(destacar);
        }
    }

    private bool EhPlayer(Collider other)
    {
        if (other == null)
            return false;

        Transform outroTransform = other.transform;

        if (jogadorRaizOpcional != null)
        {
            if (outroTransform == jogadorRaizOpcional)
                return true;

            if (outroTransform.IsChildOf(jogadorRaizOpcional))
                return true;

            if (outroTransform.root == jogadorRaizOpcional.root)
                return true;
        }

        if (!string.IsNullOrEmpty(tagDoPlayer))
        {
            try
            {
                if (other.CompareTag(tagDoPlayer))
                    return true;
            }
            catch
            {
                // Tag inexistente. Ignora e continua os outros metodos.
            }
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

            if (nomeTipo == "PlayerMove" ||
                nomeTipo == "Player_Move" ||
                nomeTipo.Contains("PlayerMove") ||
                nomeTipo.Contains("Player_Move"))
            {
                return componente.transform;
            }
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

            if (nomeTipo == "PlayerMove" ||
                nomeTipo == "Player_Move" ||
                nomeTipo.Contains("PlayerMove") ||
                nomeTipo.Contains("Player_Move"))
            {
                return true;
            }
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
        linha.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
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

        if (shader == null)
            shader = Shader.Find("Universal Render Pipeline/Unlit");

        if (shader == null)
            shader = Shader.Find("Unlit/Color");

        if (shader == null)
            shader = Shader.Find("Hidden/Internal-Colored");

        if (shader == null)
            return null;

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
        if (linhaBorda != null)
            linhaBorda.gameObject.SetActive(ativo);

        if (linhaDiagonalA != null)
            linhaDiagonalA.gameObject.SetActive(ativo && mostrarXCentral);

        if (linhaDiagonalB != null)
            linhaDiagonalB.gameObject.SetActive(ativo && mostrarXCentral);
    }

    private void AtualizarCorVisual()
    {
        Color corAtual = playerDentro ? corPlayerDentro : corNormal;
        AplicarCorNaLinha(linhaBorda, corAtual);
        AplicarCorNaLinha(linhaDiagonalA, corAtual);
        AplicarCorNaLinha(linhaDiagonalB, corAtual);
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

        if (areaCollider == null)
            return;

        Vector3 p0;
        Vector3 p1;
        Vector3 p2;
        Vector3 p3;
        CalcularCantosSuperiores(out p0, out p1, out p2, out p3);

        if (linhaBorda != null)
        {
            linhaBorda.positionCount = 5;
            linhaBorda.SetPosition(0, p0);
            linhaBorda.SetPosition(1, p1);
            linhaBorda.SetPosition(2, p2);
            linhaBorda.SetPosition(3, p3);
            linhaBorda.SetPosition(4, p0);
        }

        if (linhaDiagonalA != null)
        {
            linhaDiagonalA.positionCount = 2;
            linhaDiagonalA.SetPosition(0, p0);
            linhaDiagonalA.SetPosition(1, p2);
        }

        if (linhaDiagonalB != null)
        {
            linhaDiagonalB.positionCount = 2;
            linhaDiagonalB.SetPosition(0, p1);
            linhaDiagonalB.SetPosition(1, p3);
        }
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
