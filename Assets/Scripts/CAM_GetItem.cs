using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Sistema profissional e funcional de pegar, mover e soltar objetos pela mira.
///
/// Fluxo:
/// - Segure botão direito para entrar no modo mira/zoom.
/// - Aponte para o objeto.
/// - Segure botão esquerdo para pegar e mover.
/// - Solte botão esquerdo para colocar/soltar.
///
/// O script prepara automaticamente objetos permitidos com Collider, Rigidbody e GrabbableObjectHardcore.
/// Usa APIs novas do Unity 6/7: linearVelocity, linearDamping e angularDamping.
/// </summary>
[DefaultExecutionOrder(12000)]
public class PlayerObjectGrabberHardcore : MonoBehaviour
{
    [Header("Referencias")]
    public Camera cameraDoPlayer;

    [Tooltip("Arraste aqui o Character 01 para evitar selecionar o proprio player.")]
    public Transform raizDoPlayer;

    public CrosshairAim crosshairAim;
    public CameraGTAFollowHardcore cameraGTA;

    [Header("Input")]
    [Range(0, 2)] public int botaoDeMira = 1;
    [Range(0, 2)] public int botaoDePegar = 0;

    [Header("Modo Mira Obrigatorio")]
    public bool selecionarApenasEnquantoMira = true;
    public bool aceitarEstadoDoCrosshairAim = true;
    public bool aceitarEstadoZoomCameraGTA = true;

    [Header("Selecao")]
    [Min(0.1f)] public float distanciaSelecao = 6f;
    [Min(0.001f)] public float raioSelecao = 0.12f;
    public LayerMask layersSelecionaveis = ~0;

    [Tooltip("Se o raycast nao bater perfeitamente, tenta selecionar objeto permitido proximo ao centro da tela.")]
    public bool usarFallbackPorCentroDaTela = true;

    [Tooltip("Raio visual em viewport usado no fallback. 0.08 = perto do centro da mira.")]
    [Range(0.01f, 0.35f)] public float raioViewportFallback = 0.10f;

    [Header("Objetos Permitidos")]
    public bool usarListaDeObjetosPermitidos = true;
    public List<GameObject> objetosPermitidos = new List<GameObject>();
    public bool permitirQualquerObjetoComGrabbable = false;

    [Header("Preparacao Automatica")]
    public bool adicionarComponenteAutomaticamenteNosPermitidos = true;
    public bool adicionarRigidbodyAutomaticamente = true;
    public bool adicionarColliderAutomaticamente = true;

    [Tooltip("Massa aplicada quando o objeto nao possui Rigidbody nem perfil proprio.")]
    [Min(0.01f)] public float massaPadraoSemRigidbody = 1f;

    [Header("Limites Fisicos")]
    [Min(0.01f)] public float massaMaximaParaPegar = 35f;
    [Min(0.05f)] public float alturaMaximaObjeto = 3f;
    [Min(0.05f)] public float larguraMaximaObjeto = 3.5f;
    [Min(0.05f)] public float comprimentoMaximoObjeto = 3.5f;

    [Header("Segurar / Mover")]
    [Min(0.3f)] public float distanciaSegurando = 2.1f;
    [Min(0.3f)] public float distanciaMinimaSegurando = 0.9f;
    [Min(0.3f)] public float distanciaMaximaSegurando = 4f;
    public Vector2 offsetSegurando = new Vector2(0f, -0.10f);

    [Tooltip("Permite aproximar/afastar o objeto com scroll enquanto segura.")]
    public bool permitirScrollDistancia = true;
    [Min(0.05f)] public float velocidadeScrollDistancia = 0.35f;

    [Tooltip("Forca/resposta do objeto indo para o ponto da mira. Maior = mais firme.")]
    [Min(1f)] public float rigidezSegurar = 18f;

    [Tooltip("Suaviza a velocidade para nao tremer.")]
    [Range(0.01f, 1f)] public float suavizacaoVelocidade = 0.42f;

    [Tooltip("Velocidade maxima permitida enquanto segura.")]
    [Min(0.5f)] public float velocidadeMaximaObjeto = 12f;

    [Tooltip("Objetos pesados respondem mais devagar.")]
    [Range(0f, 1f)] public float influenciaPesoNaResposta = 0.45f;

    [Tooltip("Objetos pesados ficam um pouco mais perto do jogador.")]
    [Range(0f, 1f)] public float influenciaPesoNaDistancia = 0.35f;

    [Header("Fisica Enquanto Segura")]
    [Tooltip("Recomendado desligado para ficar funcional. Ligue se quiser peso puxando para baixo.")]
    public bool usarGravidadeEnquantoSegura = false;

    [Min(0f)] public float linearDampingEnquantoSegura = 7f;
    [Min(0f)] public float angularDampingEnquantoSegura = 9f;
    [Min(0.05f)] public float maxAngularVelocityEnquantoSegura = 6f;

    [Header("Colisao")]
    public bool impedirAtravessarParede = true;
    public LayerMask layersBloqueioObjeto = ~0;
    [Min(0.01f)] public float multiplicadorRaioColisao = 0.65f;
    [Min(0f)] public float margemParede = 0.15f;

    [Tooltip("Se o objeto ficar muito longe do ponto alvo, solta automaticamente.")]
    public bool soltarSeFicarMuitoLonge = true;
    [Min(0.5f)] public float distanciaMaximaErroAntesDeSoltar = 3f;

    [Header("Rotacao do Objeto")]
    public bool manterRotacaoOriginalAoPegar = true;
    public bool alinharObjetoComCamera = false;
    [Min(0.1f)] public float velocidadeRotacaoObjeto = 12f;

    [Header("Soltar / Colocar")]
    [Range(0f, 1.5f)] public float multiplicadorVelocidadeAoSoltar = 0.45f;
    [Range(0f, 1.5f)] public float multiplicadorRotacaoAoSoltar = 0.45f;
    public bool afastarDoPlayerAoSoltar = true;
    [Min(0.05f)] public float distanciaMinimaDoPlayerAoSoltar = 0.75f;

    [Header("Debug")]
    public bool desenharRaycast = true;
    public bool logarMotivoNaoPegou = false;

    private GrabbableObjectHardcore objetoSelecionado;
    private GrabbableObjectHardcore objetoPegando;
    private MiniMarketObjectPhysicsProfile perfilPegando;
    private Rigidbody rbPegando;

    private Quaternion rotacaoOriginalObjeto;
    private float distanciaAtualSegurando;
    private float massaAtual = 1f;
    private float raioObjetoAtual = 0.25f;

    private bool rbUseGravityOriginal;
    private bool rbIsKinematicOriginal;
    private float rbLinearDampingOriginal;
    private float rbAngularDampingOriginal;
    private float rbMaxAngularVelocityOriginal;
    private CollisionDetectionMode rbCollisionOriginal;
    private RigidbodyInterpolation rbInterpolationOriginal;
    private RigidbodyConstraints rbConstraintsOriginal;

    private readonly RaycastHit[] hitsSelecao = new RaycastHit[48];
    private readonly RaycastHit[] hitsBloqueio = new RaycastHit[48];
    private readonly List<Collider> collidersObjetoPegando = new List<Collider>();

    private void Awake()
    {
        ResolverReferencias();
        distanciaAtualSegurando = distanciaSegurando;
        PrepararObjetosPermitidos();
    }

    private void OnEnable()
    {
        ResolverReferencias();
        PrepararObjetosPermitidos();
    }

    private void OnDisable()
    {
        SoltarObjeto();
        LimparSelecao();
    }

    private void Update()
    {
        ResolverReferenciasLeves();

        if (cameraDoPlayer == null)
            return;

        bool segurandoMira = EstaSegurandoMira();
        bool segurandoPegar = Input.GetMouseButton(botaoDePegar);

        if (!segurandoMira)
        {
            SoltarObjeto();
            LimparSelecao();
            return;
        }

        if (objetoPegando == null)
        {
            AtualizarSelecao();

            if (segurandoPegar && objetoSelecionado != null)
                PegarObjeto(objetoSelecionado);
        }
        else
        {
            AtualizarScrollDistancia();

            if (!segurandoPegar)
                SoltarObjeto();
        }
    }

    private void FixedUpdate()
    {
        if (objetoPegando == null)
            return;

        MoverObjetoPegandoFisico();
    }

    private void ResolverReferencias()
    {
        if (cameraDoPlayer == null)
            cameraDoPlayer = Camera.main;

        if (cameraDoPlayer != null)
        {
            if (crosshairAim == null)
                crosshairAim = cameraDoPlayer.GetComponent<CrosshairAim>();

            if (cameraGTA == null)
                cameraGTA = cameraDoPlayer.GetComponent<CameraGTAFollowHardcore>();
        }
    }

    private void ResolverReferenciasLeves()
    {
        if (cameraDoPlayer == null)
            cameraDoPlayer = Camera.main;

        if (cameraDoPlayer == null)
            return;

        if (crosshairAim == null)
            crosshairAim = cameraDoPlayer.GetComponent<CrosshairAim>();

        if (cameraGTA == null)
            cameraGTA = cameraDoPlayer.GetComponent<CameraGTAFollowHardcore>();
    }

    private bool EstaSegurandoMira()
    {
        if (!selecionarApenasEnquantoMira)
            return true;

        if (Input.GetMouseButton(botaoDeMira))
            return true;

        if (aceitarEstadoDoCrosshairAim && crosshairAim != null && crosshairAim.EstaMirando)
            return true;

        if (aceitarEstadoZoomCameraGTA && cameraGTA != null && cameraGTA.EstaEmPrimeiraPessoa)
            return true;

        return false;
    }

    private void PrepararObjetosPermitidos()
    {
        if (!adicionarComponenteAutomaticamenteNosPermitidos || objetosPermitidos == null)
            return;

        for (int i = 0; i < objetosPermitidos.Count; i++)
        {
            PrepararObjetoPermitido(objetosPermitidos[i]);
        }
    }

    private void PrepararObjetoPermitido(GameObject objeto)
    {
        if (objeto == null)
            return;

        if (objeto.GetComponent<GrabbableObjectHardcore>() == null)
            objeto.AddComponent<GrabbableObjectHardcore>();

        if (adicionarColliderAutomaticamente && objeto.GetComponentsInChildren<Collider>(true).Length == 0)
            AdicionarBoxColliderPorBounds(objeto);

        if (adicionarRigidbodyAutomaticamente && objeto.GetComponent<Rigidbody>() == null)
        {
            MiniMarketObjectPhysicsProfile perfil = objeto.GetComponent<MiniMarketObjectPhysicsProfile>();
            Rigidbody rb = objeto.AddComponent<Rigidbody>();
            rb.mass = perfil != null ? Mathf.Max(0.01f, perfil.massaVirtual) : massaPadraoSemRigidbody;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.linearDamping = 0.1f;
            rb.angularDamping = 0.1f;
        }
    }

    private void AdicionarBoxColliderPorBounds(GameObject objeto)
    {
        Bounds bounds = CalcularBoundsObjeto(objeto.transform, out bool temBounds);
        BoxCollider box = objeto.AddComponent<BoxCollider>();

        if (!temBounds)
        {
            box.size = Vector3.one;
            box.center = Vector3.zero;
            return;
        }

        box.center = objeto.transform.InverseTransformPoint(bounds.center);
        Vector3 tamanhoLocal = objeto.transform.InverseTransformVector(bounds.size);
        box.size = new Vector3(Mathf.Abs(tamanhoLocal.x), Mathf.Abs(tamanhoLocal.y), Mathf.Abs(tamanhoLocal.z));
    }

    private void AtualizarSelecao()
    {
        GrabbableObjectHardcore novo = ProcurarObjetoNaMira();

        if (novo == objetoSelecionado)
            return;

        LimparSelecao();
        objetoSelecionado = novo;

        if (objetoSelecionado != null)
            objetoSelecionado.Selecionar(true);
    }

    private GrabbableObjectHardcore ProcurarObjetoNaMira()
    {
        Ray ray = cameraDoPlayer.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));

        int hitCount = Physics.SphereCastNonAlloc(
            ray,
            raioSelecao,
            hitsSelecao,
            distanciaSelecao,
            layersSelecionaveis,
            QueryTriggerInteraction.Ignore
        );

        OrdenarHitsPorDistancia(hitsSelecao, hitCount);

        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit hit = hitsSelecao[i];
            if (hit.collider == null || DeveIgnorarCollider(hit.collider))
                continue;

            GrabbableObjectHardcore grabbable = ResolverGrabbable(hit.collider);
            if (ObjetoPodeSerSelecionado(grabbable, out string motivo))
                return grabbable;

            if (logarMotivoNaoPegou && grabbable != null)
                Debug.Log("[PlayerObjectGrabberHardcore] Nao selecionou " + grabbable.name + ": " + motivo);
        }

        if (usarFallbackPorCentroDaTela)
            return ProcurarObjetoPermitidoPeloCentroDaTela();

        return null;
    }

    private GrabbableObjectHardcore ProcurarObjetoPermitidoPeloCentroDaTela()
    {
        if (objetosPermitidos == null || objetosPermitidos.Count == 0)
            return null;

        GrabbableObjectHardcore melhor = null;
        float melhorScore = float.MaxValue;

        for (int i = 0; i < objetosPermitidos.Count; i++)
        {
            GameObject objeto = objetosPermitidos[i];
            if (objeto == null)
                continue;

            GrabbableObjectHardcore grabbable = objeto.GetComponent<GrabbableObjectHardcore>();
            if (!ObjetoPodeSerSelecionado(grabbable, out _))
                continue;

            Vector3 ponto = ObterCentroObjeto(grabbable.transform);
            Vector3 viewport = cameraDoPlayer.WorldToViewportPoint(ponto);

            if (viewport.z < 0f || viewport.z > distanciaSelecao)
                continue;

            Vector2 delta = new Vector2(viewport.x - 0.5f, viewport.y - 0.5f);
            float distanciaCentro = delta.magnitude;

            if (distanciaCentro > raioViewportFallback)
                continue;

            if (!TemLinhaDeVisaoParaObjeto(grabbable, ponto))
                continue;

            float score = distanciaCentro * 10f + viewport.z * 0.02f;
            if (score < melhorScore)
            {
                melhorScore = score;
                melhor = grabbable;
            }
        }

        return melhor;
    }

    private bool TemLinhaDeVisaoParaObjeto(GrabbableObjectHardcore grabbable, Vector3 ponto)
    {
        Vector3 origem = cameraDoPlayer.transform.position;
        Vector3 direcao = ponto - origem;
        float distancia = direcao.magnitude;

        if (distancia <= 0.001f)
            return true;

        direcao /= distancia;

        if (!Physics.Raycast(origem, direcao, out RaycastHit hit, distanciaSelecao, layersSelecionaveis, QueryTriggerInteraction.Ignore))
            return true;

        if (hit.collider == null)
            return true;

        return hit.collider.transform == grabbable.transform || hit.collider.transform.IsChildOf(grabbable.transform);
    }

    private bool ObjetoPodeSerSelecionado(GrabbableObjectHardcore grabbable, out string motivo)
    {
        motivo = string.Empty;

        if (grabbable == null)
        {
            motivo = "sem GrabbableObjectHardcore";
            return false;
        }

        if (!grabbable.podeSerPego)
        {
            motivo = "podeSerPego desligado";
            return false;
        }

        if (usarListaDeObjetosPermitidos && !ObjetoEstaNaLista(grabbable.gameObject) && !permitirQualquerObjetoComGrabbable)
        {
            motivo = "fora da lista de objetos permitidos";
            return false;
        }

        return PodePegarFisicamente(grabbable, out motivo);
    }

    private GrabbableObjectHardcore ResolverGrabbable(Collider collider)
    {
        if (collider == null)
            return null;

        GrabbableObjectHardcore grabbable = collider.GetComponentInParent<GrabbableObjectHardcore>();

        if (grabbable != null)
            return grabbable;

        GameObject permitido = EncontrarObjetoPermitidoPeloCollider(collider);
        if (permitido == null)
            return null;

        grabbable = permitido.GetComponent<GrabbableObjectHardcore>();
        if (grabbable == null && adicionarComponenteAutomaticamenteNosPermitidos)
            grabbable = permitido.AddComponent<GrabbableObjectHardcore>();

        return grabbable;
    }

    private bool PodePegarFisicamente(GrabbableObjectHardcore objeto, out string motivo)
    {
        motivo = string.Empty;

        if (objeto == null)
        {
            motivo = "objeto null";
            return false;
        }

        MiniMarketObjectPhysicsProfile perfil = objeto.GetComponent<MiniMarketObjectPhysicsProfile>();
        if (perfil != null && !perfil.podeSerPego)
        {
            motivo = "perfil bloqueou";
            return false;
        }

        Rigidbody rb = ResolverRigidbody(objeto, perfil);
        float massa = rb != null ? Mathf.Max(0.01f, rb.mass) : (perfil != null ? perfil.massaVirtual : massaPadraoSemRigidbody);
        float limiteMassa = perfil != null && perfil.sobrescreverLimitesGlobais ? perfil.massaMaximaParaPegar : massaMaximaParaPegar;

        if (massa > limiteMassa)
        {
            motivo = "massa acima do limite";
            return false;
        }

        Bounds bounds = CalcularBoundsObjeto(objeto.transform, out bool temBounds);
        if (temBounds)
        {
            float limiteAltura = perfil != null && perfil.sobrescreverLimitesGlobais ? perfil.alturaMaximaParaPegar : alturaMaximaObjeto;
            float limiteLargura = perfil != null && perfil.sobrescreverLimitesGlobais ? perfil.larguraMaximaParaPegar : larguraMaximaObjeto;
            float limiteComprimento = perfil != null && perfil.sobrescreverLimitesGlobais ? perfil.comprimentoMaximoParaPegar : comprimentoMaximoObjeto;

            if (bounds.size.y > limiteAltura)
            {
                motivo = "altura acima do limite";
                return false;
            }

            if (bounds.size.x > limiteLargura || bounds.size.z > limiteComprimento)
            {
                motivo = "largura/comprimento acima do limite";
                return false;
            }
        }

        return true;
    }

    private Rigidbody ResolverRigidbody(GrabbableObjectHardcore objeto, MiniMarketObjectPhysicsProfile perfil)
    {
        if (objeto == null)
            return null;

        Rigidbody rb = objeto.RigidbodyDoObjeto;
        if (rb == null)
            rb = objeto.GetComponent<Rigidbody>();

        if (rb == null && adicionarRigidbodyAutomaticamente)
        {
            rb = objeto.gameObject.AddComponent<Rigidbody>();
            rb.mass = perfil != null ? Mathf.Max(0.01f, perfil.massaVirtual) : massaPadraoSemRigidbody;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        }

        return rb;
    }

    private void PegarObjeto(GrabbableObjectHardcore objeto)
    {
        if (objeto == null || !ObjetoPodeSerSelecionado(objeto, out string motivo))
        {
            if (logarMotivoNaoPegou)
                Debug.Log("[PlayerObjectGrabberHardcore] Falha ao pegar: " + motivo);
            return;
        }

        objetoPegando = objeto;
        objetoSelecionado = null;
        perfilPegando = objetoPegando.GetComponent<MiniMarketObjectPhysicsProfile>();
        rbPegando = ResolverRigidbody(objetoPegando, perfilPegando);
        massaAtual = rbPegando != null ? Mathf.Max(0.01f, rbPegando.mass) : massaPadraoSemRigidbody;
        raioObjetoAtual = CalcularRaioObjeto(objetoPegando.transform);
        rotacaoOriginalObjeto = objetoPegando.transform.rotation;
        distanciaAtualSegurando = CalcularDistanciaInicialSegurando();

        CacheCollidersObjetoPegando();
        PrepararRigidbodyParaSegurar();
        objetoPegando.ComecarPegar();
    }

    private void PrepararRigidbodyParaSegurar()
    {
        if (rbPegando == null)
            return;

        rbUseGravityOriginal = rbPegando.useGravity;
        rbIsKinematicOriginal = rbPegando.isKinematic;
        rbLinearDampingOriginal = rbPegando.linearDamping;
        rbAngularDampingOriginal = rbPegando.angularDamping;
        rbMaxAngularVelocityOriginal = rbPegando.maxAngularVelocity;
        rbCollisionOriginal = rbPegando.collisionDetectionMode;
        rbInterpolationOriginal = rbPegando.interpolation;
        rbConstraintsOriginal = rbPegando.constraints;

        bool gravidade = perfilPegando != null ? perfilPegando.usarGravidadeEnquantoSegura : usarGravidadeEnquantoSegura;

        rbPegando.isKinematic = false;
        rbPegando.useGravity = gravidade;
        rbPegando.linearDamping = linearDampingEnquantoSegura;
        rbPegando.angularDamping = angularDampingEnquantoSegura;
        rbPegando.maxAngularVelocity = maxAngularVelocityEnquantoSegura;
        rbPegando.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        rbPegando.interpolation = RigidbodyInterpolation.Interpolate;
        rbPegando.WakeUp();
    }

    private float CalcularDistanciaInicialSegurando()
    {
        float distancia = distanciaSegurando;
        distancia += raioObjetoAtual * 0.25f;

        if (perfilPegando != null)
            distancia *= perfilPegando.multiplicadorDistanciaSegurar;

        float massaLimite = Mathf.Max(0.1f, massaMaximaParaPegar);
        float peso01 = Mathf.InverseLerp(0.5f, massaLimite, massaAtual);
        float maxPorPeso = Mathf.Lerp(distanciaMaximaSegurando, Mathf.Max(distanciaMinimaSegurando, distanciaMaximaSegurando * 0.65f), peso01 * influenciaPesoNaDistancia);
        return Mathf.Clamp(distancia, distanciaMinimaSegurando, maxPorPeso);
    }

    private void AtualizarScrollDistancia()
    {
        if (!permitirScrollDistancia)
            return;

        float scroll = Input.mouseScrollDelta.y;
        if (Mathf.Abs(scroll) < 0.001f)
            return;

        distanciaAtualSegurando += scroll * velocidadeScrollDistancia;
        distanciaAtualSegurando = Mathf.Clamp(distanciaAtualSegurando, distanciaMinimaSegurando, distanciaMaximaSegurando);
    }

    private void MoverObjetoPegandoFisico()
    {
        if (objetoPegando == null)
            return;

        Vector3 alvo = CalcularPontoSegurandoSeguro();

        if (rbPegando == null)
        {
            objetoPegando.transform.position = Vector3.Lerp(objetoPegando.transform.position, alvo, CalcularSuavizacao(rigidezSegurar, Time.fixedDeltaTime));
            return;
        }

        Vector3 posAtual = rbPegando.worldCenterOfMass;
        Vector3 erro = alvo - posAtual;

        if (soltarSeFicarMuitoLonge && erro.magnitude > distanciaMaximaErroAntesDeSoltar)
        {
            SoltarObjeto();
            return;
        }

        float peso01 = Mathf.InverseLerp(0.5f, Mathf.Max(0.6f, massaMaximaParaPegar), massaAtual);
        float respostaPeso = Mathf.Lerp(1f, 0.45f, peso01 * influenciaPesoNaResposta);
        float multiplicadorPerfil = perfilPegando != null ? perfilPegando.multiplicadorForcaSegurar : 1f;

        Vector3 velocidadeDesejada = erro * rigidezSegurar * respostaPeso * multiplicadorPerfil;
        velocidadeDesejada = Vector3.ClampMagnitude(velocidadeDesejada, velocidadeMaximaObjeto);

        rbPegando.linearVelocity = Vector3.Lerp(rbPegando.linearVelocity, velocidadeDesejada, suavizacaoVelocidade);

        Quaternion rotacaoAlvo = CalcularRotacaoObjeto();
        Quaternion novaRotacao = Quaternion.Slerp(rbPegando.rotation, rotacaoAlvo, CalcularSuavizacao(velocidadeRotacaoObjeto, Time.fixedDeltaTime));
        rbPegando.MoveRotation(novaRotacao);
    }

    private Vector3 CalcularPontoSegurandoSeguro()
    {
        Transform cam = cameraDoPlayer.transform;
        Vector3 destino = cam.position + cam.forward * distanciaAtualSegurando + cam.right * offsetSegurando.x + cam.up * offsetSegurando.y;

        if (!impedirAtravessarParede)
            return destino;

        Vector3 direcao = destino - cam.position;
        float distancia = direcao.magnitude;
        if (distancia <= 0.001f)
            return destino;

        direcao /= distancia;
        float raio = Mathf.Max(0.03f, raioObjetoAtual * multiplicadorRaioColisao);

        int hitCount = Physics.SphereCastNonAlloc(cam.position, raio, direcao, hitsBloqueio, distancia, layersBloqueioObjeto, QueryTriggerInteraction.Ignore);
        float menorDistancia = distancia;
        bool bloqueado = false;

        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit hit = hitsBloqueio[i];
            if (hit.collider == null || DeveIgnorarCollider(hit.collider))
                continue;

            if (hit.distance < menorDistancia)
            {
                menorDistancia = hit.distance;
                bloqueado = true;
            }
        }

        if (bloqueado)
        {
            float distanciaSegura = Mathf.Max(distanciaMinimaSegurando, menorDistancia - raio - margemParede);
            destino = cam.position + direcao * distanciaSegura;
        }

        return destino;
    }

    private Quaternion CalcularRotacaoObjeto()
    {
        if (manterRotacaoOriginalAoPegar)
            return rotacaoOriginalObjeto;

        if (alinharObjetoComCamera && cameraDoPlayer != null)
            return Quaternion.LookRotation(cameraDoPlayer.transform.forward, Vector3.up);

        return objetoPegando != null ? objetoPegando.transform.rotation : Quaternion.identity;
    }

    private void SoltarObjeto()
    {
        if (objetoPegando == null)
            return;

        if (rbPegando != null)
        {
            if (afastarDoPlayerAoSoltar)
                AfastarDoPlayerSeNecessario();

            float multVel = perfilPegando != null ? perfilPegando.multiplicadorVelocidadeAoSoltar : multiplicadorVelocidadeAoSoltar;
            float multRot = perfilPegando != null ? perfilPegando.multiplicadorRotacaoAoSoltar : multiplicadorRotacaoAoSoltar;

            rbPegando.linearVelocity *= multVel;
            rbPegando.angularVelocity *= multRot;

            rbPegando.useGravity = rbUseGravityOriginal;
            rbPegando.isKinematic = rbIsKinematicOriginal;
            rbPegando.linearDamping = rbLinearDampingOriginal;
            rbPegando.angularDamping = rbAngularDampingOriginal;
            rbPegando.maxAngularVelocity = rbMaxAngularVelocityOriginal;
            rbPegando.collisionDetectionMode = rbCollisionOriginal;
            rbPegando.interpolation = rbInterpolationOriginal;
            rbPegando.constraints = rbConstraintsOriginal;
            rbPegando.WakeUp();
        }

        objetoPegando.Soltar();
        objetoPegando = null;
        perfilPegando = null;
        rbPegando = null;
        collidersObjetoPegando.Clear();
    }

    private void AfastarDoPlayerSeNecessario()
    {
        if (raizDoPlayer == null || rbPegando == null)
            return;

        Vector3 playerPos = raizDoPlayer.position;
        Vector3 objetoPos = rbPegando.position;
        Vector3 plano = objetoPos - playerPos;
        plano.y = 0f;

        float distancia = plano.magnitude;
        if (distancia >= distanciaMinimaDoPlayerAoSoltar || distancia <= 0.001f)
            return;

        Vector3 novaPosicao = playerPos + plano.normalized * distanciaMinimaDoPlayerAoSoltar;
        novaPosicao.y = objetoPos.y;
        rbPegando.position = novaPosicao;
    }

    private void LimparSelecao()
    {
        if (objetoSelecionado == null)
            return;

        objetoSelecionado.Selecionar(false);
        objetoSelecionado = null;
    }

    private void CacheCollidersObjetoPegando()
    {
        collidersObjetoPegando.Clear();
        if (objetoPegando == null)
            return;

        Collider[] cols = objetoPegando.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < cols.Length; i++)
        {
            if (cols[i] != null)
                collidersObjetoPegando.Add(cols[i]);
        }
    }

    private bool DeveIgnorarCollider(Collider col)
    {
        if (col == null || !col.enabled || col.isTrigger)
            return true;

        if (raizDoPlayer != null && col.transform.IsChildOf(raizDoPlayer))
            return true;

        if (objetoPegando != null && (col.transform == objetoPegando.transform || col.transform.IsChildOf(objetoPegando.transform)))
            return true;

        for (int i = 0; i < collidersObjetoPegando.Count; i++)
        {
            if (col == collidersObjetoPegando[i])
                return true;
        }

        return false;
    }

    private bool ObjetoEstaNaLista(GameObject objeto)
    {
        if (objeto == null || objetosPermitidos == null)
            return false;

        for (int i = 0; i < objetosPermitidos.Count; i++)
        {
            GameObject permitido = objetosPermitidos[i];
            if (permitido == null)
                continue;

            if (objeto == permitido || objeto.transform.IsChildOf(permitido.transform) || permitido.transform.IsChildOf(objeto.transform))
                return true;
        }

        return false;
    }

    private GameObject EncontrarObjetoPermitidoPeloCollider(Collider collider)
    {
        if (collider == null || objetosPermitidos == null)
            return null;

        Transform hitTransform = collider.transform;
        for (int i = 0; i < objetosPermitidos.Count; i++)
        {
            GameObject permitido = objetosPermitidos[i];
            if (permitido == null)
                continue;

            if (hitTransform == permitido.transform || hitTransform.IsChildOf(permitido.transform))
                return permitido;
        }

        return null;
    }

    private Bounds CalcularBoundsObjeto(Transform raiz, out bool temBounds)
    {
        temBounds = false;
        Bounds bounds = new Bounds(raiz != null ? raiz.position : Vector3.zero, Vector3.zero);

        if (raiz == null)
            return bounds;

        Collider[] colliders = raiz.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider col = colliders[i];
            if (col == null || !col.enabled || col.isTrigger)
                continue;

            if (!temBounds)
            {
                bounds = col.bounds;
                temBounds = true;
            }
            else
            {
                bounds.Encapsulate(col.bounds);
            }
        }

        Renderer[] renderers = raiz.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer r = renderers[i];
            if (r == null || !r.enabled)
                continue;

            if (!temBounds)
            {
                bounds = r.bounds;
                temBounds = true;
            }
            else
            {
                bounds.Encapsulate(r.bounds);
            }
        }

        return bounds;
    }

    private Vector3 ObterCentroObjeto(Transform raiz)
    {
        Bounds bounds = CalcularBoundsObjeto(raiz, out bool temBounds);
        return temBounds ? bounds.center : raiz.position;
    }

    private float CalcularRaioObjeto(Transform raiz)
    {
        Bounds bounds = CalcularBoundsObjeto(raiz, out bool temBounds);
        if (!temBounds)
            return 0.25f;

        return Mathf.Clamp(Mathf.Max(bounds.extents.x, bounds.extents.y, bounds.extents.z), 0.08f, 2.5f);
    }

    private void OrdenarHitsPorDistancia(RaycastHit[] hits, int count)
    {
        for (int i = 0; i < count - 1; i++)
        {
            for (int j = i + 1; j < count; j++)
            {
                if (hits[j].distance < hits[i].distance)
                {
                    RaycastHit tmp = hits[i];
                    hits[i] = hits[j];
                    hits[j] = tmp;
                }
            }
        }
    }

    private float CalcularSuavizacao(float velocidade, float deltaTime)
    {
        return 1f - Mathf.Exp(-velocidade * deltaTime);
    }

    private void OnValidate()
    {
        distanciaSelecao = Mathf.Max(0.1f, distanciaSelecao);
        raioSelecao = Mathf.Max(0.001f, raioSelecao);
        massaPadraoSemRigidbody = Mathf.Max(0.01f, massaPadraoSemRigidbody);
        massaMaximaParaPegar = Mathf.Max(0.01f, massaMaximaParaPegar);
        alturaMaximaObjeto = Mathf.Max(0.05f, alturaMaximaObjeto);
        larguraMaximaObjeto = Mathf.Max(0.05f, larguraMaximaObjeto);
        comprimentoMaximoObjeto = Mathf.Max(0.05f, comprimentoMaximoObjeto);
        distanciaSegurando = Mathf.Max(0.3f, distanciaSegurando);
        distanciaMinimaSegurando = Mathf.Max(0.3f, distanciaMinimaSegurando);
        distanciaMaximaSegurando = Mathf.Max(distanciaMinimaSegurando, distanciaMaximaSegurando);
        velocidadeScrollDistancia = Mathf.Max(0.05f, velocidadeScrollDistancia);
        rigidezSegurar = Mathf.Max(1f, rigidezSegurar);
        velocidadeMaximaObjeto = Mathf.Max(0.5f, velocidadeMaximaObjeto);
        linearDampingEnquantoSegura = Mathf.Max(0f, linearDampingEnquantoSegura);
        angularDampingEnquantoSegura = Mathf.Max(0f, angularDampingEnquantoSegura);
        maxAngularVelocityEnquantoSegura = Mathf.Max(0.05f, maxAngularVelocityEnquantoSegura);
        multiplicadorRaioColisao = Mathf.Max(0.01f, multiplicadorRaioColisao);
        margemParede = Mathf.Max(0f, margemParede);
        distanciaMaximaErroAntesDeSoltar = Mathf.Max(0.5f, distanciaMaximaErroAntesDeSoltar);
    }

    private void OnDrawGizmos()
    {
        if (!desenharRaycast || cameraDoPlayer == null)
            return;

        Gizmos.color = EstaSegurandoMira() ? Color.green : Color.gray;
        Ray ray = cameraDoPlayer.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        Gizmos.DrawLine(ray.origin, ray.origin + ray.direction * distanciaSelecao);

        if (objetoPegando != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(CalcularPontoSegurandoSeguro(), Mathf.Max(0.05f, raioObjetoAtual));
        }
    }
}
