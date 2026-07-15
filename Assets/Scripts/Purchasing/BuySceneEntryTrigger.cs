using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Área da calçada que abre o modo de compra ao pressionar E.
/// Mantém a API pública antiga para preservar a cena e recria a marcação visual em runtime.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Collider))]
public sealed class BuySceneEntryTrigger : MonoBehaviour
{
    [Header("Controlador da compra")]
    public BuySceneCameraModeController controladorBuyScene;
    public Transform pontoDeFocoDaCamera;
    public BuyableLandAreaMarker[] terrenosDestaArea;
    public bool usarTerrenosProximosSeListaVazia = true;
    [Min(1f)] public float raioBuscaTerrenosProximos = 60f;

    [Header("Abrir / fechar")]
    public bool usarTeclaParaAbrirFechar = true;
    public KeyCode teclaAbrirFechar = KeyCode.E;
    public bool entrarAutomaticamenteAoPassar;
    public bool exigirTeclaParaEntrar = true;
    public KeyCode teclaEntrar = KeyCode.E;
    [Min(0f)] public float intervaloMinimoEntreAtivacoes = 0.25f;

    [Header("Detecção do jogador")]
    public string tagDoPlayer = "Player";
    public Transform jogadorRaizOpcional;
    public bool aceitarCharacterController = true;
    public bool aceitarScriptPlayerMove = true;
    public bool usarDeteccaoPorOverlapSegura = true;
    public LayerMask camadasDeteccao = ~0;
    [Min(0f)] public float margemVerticalDeteccao = 2.5f;

    [Header("Marcação visual da área")]
    public bool mostrarMarcacaoVisual = true;
    public bool mostrarXCentral = true;
    public Color corNormal = new Color(1f, 0.92f, 0f, 1f);
    public Color corPlayerDentro = new Color(0.1f, 1f, 0.1f, 1f);
    public Color corIndisponivel = new Color(1f, 0.18f, 0.08f, 1f);
    [Min(0.01f)] public float larguraLinha = 0.08f;
    public float alturaAcimaDoCollider = 0.08f;
    public bool atualizarVisualEmTempoReal = true;

    [Header("Sincronia com terrenos")]
    public bool sincronizarMarcacaoComStatusDosTerrenos = true;
    public bool calcadaIndisponivelSomenteQuandoTodosTerrenosIndisponiveis = true;
    public bool manterMarcacaoVisivelQuandoIndisponivel = true;
    public bool sincronizarComTerrenosEncontradosAutomaticamente = true;

    [Header("Destaque dos terrenos")]
    public bool destacarTerrenosAoDetectarPlayer = true;
    public bool limparDestaqueAoSairDaArea = true;

    [Header("Debug")]
    public bool logarEventos = true;
    public bool desenharGizmos = true;
    public Color corGizmo = new Color(1f, 0.92f, 0f, 0.25f);

    private const string NomeLinhaBorda = "BuyScene_Entrada_Borda";
    private const string NomeLinhaDiagonalA = "BuyScene_Entrada_Diagonal_A";
    private const string NomeLinhaDiagonalB = "BuyScene_Entrada_Diagonal_B";

    private readonly Collider[] resultadosOverlap = new Collider[32];
    private Collider areaCollider;
    private LineRenderer linhaBorda;
    private LineRenderer linhaDiagonalA;
    private LineRenderer linhaDiagonalB;
    private Material materialLinhas;
    private bool playerDentro;
    private float ultimoTempoAtivacao = -999f;

    public bool PlayerDentro => playerDentro;

    private void Reset()
    {
        areaCollider = GetComponent<Collider>();
        PrepararColliderComoTrigger();
        pontoDeFocoDaCamera = transform;
    }

    private void Awake()
    {
        InicializarRuntime();
    }

    private void OnEnable()
    {
        InicializarRuntime();
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

    private void OnDisable()
    {
        playerDentro = false;
        DefinirDestaqueTerrenos(false);
    }

    private void OnValidate()
    {
        larguraLinha = Mathf.Max(0.01f, larguraLinha);
        raioBuscaTerrenosProximos = Mathf.Max(1f, raioBuscaTerrenosProximos);
        intervaloMinimoEntreAtivacoes = Mathf.Max(0f, intervaloMinimoEntreAtivacoes);
        margemVerticalDeteccao = Mathf.Max(0f, margemVerticalDeteccao);

        areaCollider = GetComponent<Collider>();
        AtualizarPosicoesVisual();
        AtualizarCorVisual();
        DefinirLinhasAtivas(DeveMostrarMarcacao());
    }

    private void InicializarRuntime()
    {
        areaCollider = GetComponent<Collider>();
        PrepararColliderComoTrigger();
        ResolverReferencias();
        CriarRenderizadores();
        AtualizarVisualCompleto();
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
            controladorBuyScene = Object.FindAnyObjectByType<BuySceneCameraModeController>(FindObjectsInactive.Include);

        if (pontoDeFocoDaCamera == null)
            pontoDeFocoDaCamera = transform;

        if (jogadorRaizOpcional == null)
            jogadorRaizOpcional = TentarEncontrarPlayerAutomaticamente();

        if ((terrenosDestaArea == null || terrenosDestaArea.Length == 0) &&
            sincronizarComTerrenosEncontradosAutomaticamente)
        {
            terrenosDestaArea = ObterTerrenosParaCamera(transform.position);
        }
    }

    private Transform TentarEncontrarPlayerAutomaticamente()
    {
        CameraRelativeMovement movement = Object.FindAnyObjectByType<CameraRelativeMovement>(FindObjectsInactive.Include);
        if (movement != null)
            return movement.transform;

        if (!string.IsNullOrWhiteSpace(tagDoPlayer))
        {
            try
            {
                GameObject player = GameObject.FindGameObjectWithTag(tagDoPlayer);
                if (player != null)
                    return player.transform;
            }
            catch
            {
                // Tag inexistente: continua pela busca do CharacterController.
            }
        }

        CharacterController characterController =
            Object.FindAnyObjectByType<CharacterController>(FindObjectsInactive.Include);
        return characterController != null ? characterController.transform : null;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (EhPlayer(other))
            RegistrarEntradaDoPlayer(ObterRaizPlayer(other.transform));
    }

    private void OnTriggerExit(Collider other)
    {
        if (EhPlayer(other))
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
            if (teclaAbrirFechar != KeyCode.None && Input.GetKeyDown(teclaAbrirFechar))
                AlternarBuyScene();
            return;
        }

        if (exigirTeclaParaEntrar &&
            teclaEntrar != KeyCode.None &&
            Input.GetKeyDown(teclaEntrar))
        {
            TentarEntrarNaBuyScene();
        }
    }

    private void VerificarPlayerPorOverlapSeguro()
    {
        Transform encontrado = EncontrarPlayerDentroDaArea();
        bool dentroAgora = encontrado != null;

        if (dentroAgora && !playerDentro)
            RegistrarEntradaDoPlayer(encontrado);
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

        int count = Physics.OverlapBoxNonAlloc(
            bounds.center,
            bounds.extents,
            resultadosOverlap,
            Quaternion.identity,
            camadasDeteccao,
            QueryTriggerInteraction.Collide);

        for (int i = 0; i < count; i++)
        {
            Collider candidate = resultadosOverlap[i];
            if (candidate == null || candidate == areaCollider)
                continue;

            if (EhPlayer(candidate))
                return ObterRaizPlayer(candidate.transform);
        }

        return null;
    }

    private bool PontoDentroDaArea(Vector3 worldPoint)
    {
        if (areaCollider == null)
            return false;

        Bounds bounds = areaCollider.bounds;
        bounds.Expand(new Vector3(0f, margemVerticalDeteccao, 0f));
        return bounds.Contains(worldPoint);
    }

    private bool EhPlayer(Collider other)
    {
        if (other == null)
            return false;

        Transform root = ObterRaizPlayer(other.transform);
        if (root == null)
            return false;

        if (jogadorRaizOpcional != null &&
            (root == jogadorRaizOpcional || root.IsChildOf(jogadorRaizOpcional) || jogadorRaizOpcional.IsChildOf(root)))
        {
            return true;
        }

        if (!string.IsNullOrWhiteSpace(tagDoPlayer))
        {
            try
            {
                if (other.CompareTag(tagDoPlayer) || root.CompareTag(tagDoPlayer))
                    return true;
            }
            catch
            {
                // Tag inexistente.
            }
        }

        if (aceitarCharacterController && other.GetComponentInParent<CharacterController>() != null)
            return true;

        if (aceitarScriptPlayerMove && other.GetComponentInParent<CameraRelativeMovement>() != null)
            return true;

        return false;
    }

    private Transform ObterRaizPlayer(Transform source)
    {
        if (source == null)
            return null;

        CameraRelativeMovement movement = source.GetComponentInParent<CameraRelativeMovement>();
        if (movement != null)
            return movement.transform;

        CharacterController characterController = source.GetComponentInParent<CharacterController>();
        if (characterController != null)
            return characterController.transform;

        return source.root;
    }

    private void RegistrarEntradaDoPlayer(Transform root)
    {
        if (jogadorRaizOpcional == null && root != null)
            jogadorRaizOpcional = root;

        if (playerDentro)
            return;

        playerDentro = true;
        AtualizarCorVisual();

        if (destacarTerrenosAoDetectarPlayer)
            DefinirDestaqueTerrenos(true);

        if (logarEventos)
            Debug.Log("[BuySceneEntryTrigger] Jogador entrou na área de compra: " + name, this);

        if (!usarTeclaParaAbrirFechar && entrarAutomaticamenteAoPassar && !exigirTeclaParaEntrar)
            TentarEntrarNaBuyScene();
    }

    private void RegistrarSaidaDoPlayer()
    {
        if (!playerDentro)
            return;

        playerDentro = false;
        AtualizarCorVisual();

        if (limparDestaqueAoSairDaArea &&
            (controladorBuyScene == null || !controladorBuyScene.ModoCompraAtivo))
        {
            DefinirDestaqueTerrenos(false);
        }

        if (logarEventos)
            Debug.Log("[BuySceneEntryTrigger] Jogador saiu da área de compra: " + name, this);
    }

    public void AlternarBuyScene()
    {
        ResolverReferencias();

        if (controladorBuyScene == null)
        {
            Debug.LogWarning("[BuySceneEntryTrigger] BuySceneCameraModeController não encontrado.", this);
            return;
        }

        if (Time.unscaledTime < ultimoTempoAtivacao + intervaloMinimoEntreAtivacoes)
            return;

        ultimoTempoAtivacao = Time.unscaledTime;

        if (controladorBuyScene.ModoCompraAtivo)
        {
            controladorBuyScene.SairDoModoCompra();
            DefinirDestaqueTerrenos(playerDentro);
            AtualizarVisualCompleto();
            return;
        }

        EntrarNaBuySceneSemCooldown();
    }

    public void TentarEntrarNaBuyScene()
    {
        ResolverReferencias();

        if (controladorBuyScene == null || controladorBuyScene.ModoCompraAtivo)
            return;

        if (Time.unscaledTime < ultimoTempoAtivacao + intervaloMinimoEntreAtivacoes)
            return;

        ultimoTempoAtivacao = Time.unscaledTime;
        EntrarNaBuySceneSemCooldown();
    }

    private void EntrarNaBuySceneSemCooldown()
    {
        Transform focus = pontoDeFocoDaCamera != null ? pontoDeFocoDaCamera : transform;
        BuyableLandAreaMarker[] terrenos = ObterTerrenosParaCamera(focus.position);

        if (destacarTerrenosAoDetectarPlayer)
            DefinirDestaqueTerrenos(true, terrenos);

        AtualizarVisualCompleto();
        controladorBuyScene.EntrarNoModoCompra(focus, terrenos);
    }

    private BuyableLandAreaMarker[] ObterTerrenosParaCamera(Vector3 referencePosition)
    {
        if (terrenosDestaArea != null && terrenosDestaArea.Length > 0)
            return terrenosDestaArea;

        if (!usarTerrenosProximosSeListaVazia)
            return terrenosDestaArea;

        BuyableLandAreaMarker[] all = Object.FindObjectsByType<BuyableLandAreaMarker>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        if (all == null || all.Length == 0)
            return System.Array.Empty<BuyableLandAreaMarker>();

        int nearbyCount = 0;
        float radiusSquared = raioBuscaTerrenosProximos * raioBuscaTerrenosProximos;

        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] != null &&
                (all[i].ObterPontoDeFoco() - referencePosition).sqrMagnitude <= radiusSquared)
            {
                nearbyCount++;
            }
        }

        if (nearbyCount == 0)
            return all;

        BuyableLandAreaMarker[] nearby = new BuyableLandAreaMarker[nearbyCount];
        int index = 0;
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] != null &&
                (all[i].ObterPontoDeFoco() - referencePosition).sqrMagnitude <= radiusSquared)
            {
                nearby[index++] = all[i];
            }
        }

        if (sincronizarComTerrenosEncontradosAutomaticamente)
            terrenosDestaArea = nearby;

        return nearby;
    }

    private void DefinirDestaqueTerrenos(bool highlight)
    {
        DefinirDestaqueTerrenos(highlight, ObterTerrenosParaCamera(transform.position));
    }

    private void DefinirDestaqueTerrenos(bool highlight, BuyableLandAreaMarker[] terrenos)
    {
        if (terrenos == null)
            return;

        for (int i = 0; i < terrenos.Length; i++)
        {
            if (terrenos[i] != null)
                terrenos[i].DefinirDestaque(highlight);
        }
    }

    private bool TodosTerrenosIndisponiveis()
    {
        if (!sincronizarMarcacaoComStatusDosTerrenos)
            return false;

        BuyableLandAreaMarker[] terrenos = ObterTerrenosParaCamera(transform.position);
        if (terrenos == null || terrenos.Length == 0)
            return false;

        int validos = 0;
        int indisponiveis = 0;
        for (int i = 0; i < terrenos.Length; i++)
        {
            BuyableLandAreaMarker terreno = terrenos[i];
            if (terreno == null)
                continue;

            validos++;
            terreno.SincronizarEstadoComBanco();
            if (!terreno.EstaDisponivelParaCompra)
                indisponiveis++;
        }

        if (validos == 0)
            return false;

        return calcadaIndisponivelSomenteQuandoTodosTerrenosIndisponiveis
            ? indisponiveis == validos
            : indisponiveis > 0;
    }

    private bool DeveMostrarMarcacao()
    {
        if (!mostrarMarcacaoVisual)
            return false;

        return manterMarcacaoVisivelQuandoIndisponivel || !TodosTerrenosIndisponiveis();
    }

    /// <summary>
    /// API tipada para bootstraps e ferramentas atualizarem o visual sem SendMessage.
    /// </summary>
    public void AtualizarVisualRuntime()
    {
        AtualizarVisualCompleto();
    }

    private void CriarRenderizadores()
    {
        linhaBorda = ObterOuCriarLinha(NomeLinhaBorda, linhaBorda, 5);
        linhaDiagonalA = ObterOuCriarLinha(NomeLinhaDiagonalA, linhaDiagonalA, 2);
        linhaDiagonalB = ObterOuCriarLinha(NomeLinhaDiagonalB, linhaDiagonalB, 2);

        ConfigurarLinha(linhaBorda);
        ConfigurarLinha(linhaDiagonalA);
        ConfigurarLinha(linhaDiagonalB);
    }

    private LineRenderer ObterOuCriarLinha(string objectName, LineRenderer current, int points)
    {
        if (current != null)
        {
            current.positionCount = points;
            return current;
        }

        Transform existing = transform.Find(objectName);
        if (existing != null)
        {
            LineRenderer line = existing.GetComponent<LineRenderer>();
            if (line != null)
            {
                line.positionCount = points;
                return line;
            }
        }

        GameObject lineObject = new GameObject(objectName);
        lineObject.transform.SetParent(transform, false);
        LineRenderer created = lineObject.AddComponent<LineRenderer>();
        created.positionCount = points;
        return created;
    }

    private void ConfigurarLinha(LineRenderer line)
    {
        if (line == null)
            return;

        line.useWorldSpace = true;
        line.loop = false;
        line.startWidth = larguraLinha;
        line.endWidth = larguraLinha;
        line.numCornerVertices = 4;
        line.numCapVertices = 4;
        line.shadowCastingMode = ShadowCastingMode.Off;
        line.receiveShadows = false;
        line.material = ObterMaterialLinhas();
    }

    private Material ObterMaterialLinhas()
    {
        if (materialLinhas != null)
            return materialLinhas;

        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Sprites/Default");
        if (shader == null) shader = Shader.Find("Unlit/Color");
        if (shader == null) shader = Shader.Find("Standard");

        if (shader == null)
            return null;

        materialLinhas = new Material(shader)
        {
            name = "BuySceneEntry_RuntimeMaterial",
            hideFlags = HideFlags.HideAndDontSave
        };
        return materialLinhas;
    }

    private void AtualizarVisualCompleto()
    {
        if (linhaBorda == null || linhaDiagonalA == null || linhaDiagonalB == null)
            CriarRenderizadores();

        AtualizarPosicoesVisual();
        AtualizarCorVisual();
        DefinirLinhasAtivas(DeveMostrarMarcacao());
    }

    private void AtualizarPosicoesVisual()
    {
        if (areaCollider == null)
            return;

        Bounds bounds = areaCollider.bounds;
        float y = bounds.max.y + alturaAcimaDoCollider;
        Vector3 a = new Vector3(bounds.min.x, y, bounds.min.z);
        Vector3 b = new Vector3(bounds.max.x, y, bounds.min.z);
        Vector3 c = new Vector3(bounds.max.x, y, bounds.max.z);
        Vector3 d = new Vector3(bounds.min.x, y, bounds.max.z);

        if (linhaBorda != null)
        {
            linhaBorda.positionCount = 5;
            linhaBorda.SetPosition(0, a);
            linhaBorda.SetPosition(1, b);
            linhaBorda.SetPosition(2, c);
            linhaBorda.SetPosition(3, d);
            linhaBorda.SetPosition(4, a);
        }

        if (linhaDiagonalA != null)
        {
            linhaDiagonalA.positionCount = 2;
            linhaDiagonalA.SetPosition(0, a);
            linhaDiagonalA.SetPosition(1, c);
        }

        if (linhaDiagonalB != null)
        {
            linhaDiagonalB.positionCount = 2;
            linhaDiagonalB.SetPosition(0, b);
            linhaDiagonalB.SetPosition(1, d);
        }
    }

    private void AtualizarCorVisual()
    {
        bool indisponivel = TodosTerrenosIndisponiveis();
        Color color = indisponivel ? corIndisponivel : playerDentro ? corPlayerDentro : corNormal;
        AplicarCorLinha(linhaBorda, color);
        AplicarCorLinha(linhaDiagonalA, color);
        AplicarCorLinha(linhaDiagonalB, color);
    }

    private static void AplicarCorLinha(LineRenderer line, Color color)
    {
        if (line == null)
            return;

        line.startColor = color;
        line.endColor = color;

        if (line.material != null)
        {
            if (line.material.HasProperty("_BaseColor"))
                line.material.SetColor("_BaseColor", color);
            if (line.material.HasProperty("_Color"))
                line.material.SetColor("_Color", color);
        }
    }

    private void DefinirLinhasAtivas(bool active)
    {
        if (linhaBorda != null)
            linhaBorda.gameObject.SetActive(active);
        if (linhaDiagonalA != null)
            linhaDiagonalA.gameObject.SetActive(active && mostrarXCentral);
        if (linhaDiagonalB != null)
            linhaDiagonalB.gameObject.SetActive(active && mostrarXCentral);
    }

    private void OnDrawGizmosSelected()
    {
        if (!desenharGizmos)
            return;

        Collider collider = areaCollider != null ? areaCollider : GetComponent<Collider>();
        if (collider == null)
            return;

        Gizmos.color = corGizmo;
        Gizmos.DrawCube(collider.bounds.center, collider.bounds.size);
        Gizmos.color = new Color(corGizmo.r, corGizmo.g, corGizmo.b, 1f);
        Gizmos.DrawWireCube(collider.bounds.center, collider.bounds.size);
    }
}
