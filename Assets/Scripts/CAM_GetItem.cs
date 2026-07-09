using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Sistema profissional de pegar/soltar objetos pela mira.
///
/// Regras principais:
/// - So permite pegar enquanto segura a mira/zoom no botao direito.
/// - Click esquerdo segura o objeto; soltar o click esquerdo coloca/solta.
/// - Respeita massa, tamanho, altura, distancia e colisao.
/// - Usa fisica por forca/velocidade, sem teletransporte duro.
/// - Objetos pesados ficam mais lentos, mais proximos e podem falhar se excederem o limite.
/// </summary>
public class PlayerObjectGrabberHardcore : MonoBehaviour
{
    [Header("Referencias")]
    public Camera cameraDoPlayer;

    [Tooltip("Arraste aqui o Character01 para evitar que o raycast selecione o proprio personagem.")]
    public Transform raizDoPlayer;

    [Tooltip("Opcional. Pode arrastar o script CrosshairAim aqui.")]
    public CrosshairAim crosshairAim;

    [Tooltip("Opcional. Script da camera principal. Usado para garantir que so pega no modo mira/zoom.")]
    public CameraGTAFollowHardcore cameraGTA;

    [Header("Input")]
    [Range(0, 2)] public int botaoDeMira = 1;
    [Range(0, 2)] public int botaoDePegar = 0;

    [Header("Modo Mira Obrigatorio")]
    [Tooltip("So permite selecionar e pegar enquanto segura o botao direito/mira.")]
    public bool selecionarApenasEnquantoMira = true;

    [Tooltip("Se ligado, alem do botao direito, aceita o estado EstaMirando do CrosshairAim.")]
    public bool aceitarEstadoDoCrosshairAim = true;

    [Tooltip("Se ligado, aceita tambem EstaEmPrimeiraPessoa/zoom da CameraGTA.")]
    public bool aceitarEstadoZoomCameraGTA = true;

    [Header("Selecao")]
    [Min(0.1f)] public float distanciaSelecao = 5f;
    public LayerMask layersSelecionaveis = ~0;

    [Tooltip("Raio pequeno no centro da mira. Ajuda a pegar objetos pequenos sem precisar acertar pixel perfeito.")]
    [Min(0.001f)] public float raioSelecao = 0.08f;

    [Header("Objetos Permitidos")]
    public bool usarListaDeObjetosPermitidos = true;
    public List<GameObject> objetosPermitidos = new List<GameObject>();
    public bool adicionarComponenteAutomaticamenteNosPermitidos = true;
    public bool permitirQualquerObjetoComGrabbable = false;

    [Header("Fisica Realista - Peso / Tamanho")]
    [Tooltip("Objetos com massa acima deste valor nao podem ser pegos, salvo override no MiniMarketObjectPhysicsProfile.")]
    [Min(0.01f)] public float massaMaximaParaPegar = 25f;

    [Tooltip("Massa usada quando o objeto nao possui Rigidbody e o script cria um automaticamente.")]
    [Min(0.01f)] public float massaPadraoSemRigidbody = 1f;

    [Tooltip("Adiciona Rigidbody automaticamente se o objeto permitido ainda nao tiver.")]
    public bool adicionarRigidbodyAutomaticamente = true;

    [Tooltip("Objetos acima desta altura nao podem ser pegos.")]
    [Min(0.05f)] public float alturaMaximaObjeto = 2.2f;

    [Tooltip("Objetos acima desta largura nao podem ser pegos.")]
    [Min(0.05f)] public float larguraMaximaObjeto = 2.4f;

    [Tooltip("Objetos acima deste comprimento nao podem ser pegos.")]
    [Min(0.05f)] public float comprimentoMaximoObjeto = 2.4f;

    [Tooltip("Se o objeto for grande, segura um pouco mais longe para nao entrar na camera/personagem.")]
    public bool ajustarDistanciaPeloTamanho = true;

    [Tooltip("Quanto o peso reduz a forca de segurar. 0 = peso quase nao influencia, 1 = influencia bastante.")]
    [Range(0f, 1f)] public float influenciaPesoNaForca = 0.65f;

    [Tooltip("Quanto o peso reduz a distancia maxima enquanto segura.")]
    [Range(0f, 1f)] public float influenciaPesoNaDistancia = 0.45f;

    [Header("Pegar / Segurar")]
    [Min(0.3f)] public float distanciaSegurando = 2.2f;
    public Vector2 offsetSegurando = new Vector2(0f, -0.15f);

    [Tooltip("Forca base da mola fisica que puxa o objeto ate o ponto de segurar.")]
    [Min(1f)] public float forcaSegurar = 180f;

    [Tooltip("Amortecimento para o objeto nao ficar tremendo.")]
    [Min(0f)] public float amortecimentoSegurar = 28f;

    [Tooltip("Velocidade maxima fisica do objeto enquanto esta sendo puxado.")]
    [Min(0.1f)] public float velocidadeMaximaObjeto = 8f;

    [Tooltip("Forca maxima aplicada no objeto. Evita explosoes fisicas.")]
    [Min(1f)] public float forcaMaximaSegurar = 650f;

    public bool limitarDistanciaSegurando = true;
    [Min(0.3f)] public float distanciaMinimaSegurando = 1.1f;
    [Min(0.3f)] public float distanciaMaximaSegurando = 3.5f;

    [Tooltip("Permite ajustar a distancia do objeto com o scroll enquanto segura.")]
    public bool permitirScrollDistancia = true;
    [Min(0.05f)] public float velocidadeScrollDistancia = 0.28f;

    [Tooltip("Se o objeto se afastar demais do ponto de segurar, solta automaticamente.")]
    public bool soltarSeFicarMuitoLonge = true;
    [Min(0.5f)] public float distanciaMaximaErroAntesDeSoltar = 2.2f;

    [Header("Gravidade / Arrasto enquanto segura")]
    public bool usarGravidadeEnquantoSegura = true;
    [Min(0f)] public float dragEnquantoSegura = 4f;
    [Min(0f)] public float angularDragEnquantoSegura = 6f;
    [Min(0.05f)] public float maxAngularVelocityEnquantoSegura = 8f;

    [Header("Colisao / Parede")]
    public bool impedirAtravessarParede = true;
    public LayerMask layersBloqueioObjeto = ~0;
    [Min(0.01f)] public float multiplicadorRaioColisao = 0.85f;
    [Min(0f)] public float margemParede = 0.12f;

    [Header("Rotacao do Objeto")]
    public bool manterRotacaoOriginalAoPegar = true;
    public bool alinharObjetoComCamera = false;
    [Min(0.1f)] public float velocidadeRotacaoObjeto = 10f;

    [Header("Soltar / Colocar")]
    [Tooltip("Quanto da velocidade fisica fica ao soltar. 0 = para seco, 1 = natural.")]
    [Range(0f, 1.5f)] public float multiplicadorVelocidadeAoSoltar = 0.65f;

    [Tooltip("Quanto da rotacao fisica fica ao soltar.")]
    [Range(0f, 1.5f)] public float multiplicadorRotacaoAoSoltar = 0.65f;

    [Tooltip("Ao soltar, faz uma pequena checagem para evitar deixar o objeto dentro do player.")]
    public bool afastarDoPlayerAoSoltar = true;
    [Min(0.05f)] public float distanciaMinimaDoPlayerAoSoltar = 0.65f;

    [Header("Debug")]
    public bool desenharRaycast = true;
    public bool logarMotivoNaoPegou = false;

    private GrabbableObjectHardcore objetoSelecionado;
    private GrabbableObjectHardcore objetoPegando;
    private MiniMarketObjectPhysicsProfile perfilPegando;
    private Rigidbody rbPegando;

    private Quaternion rotacaoOriginalObjeto;
    private float distanciaAtualSegurando;
    private float massaAtual;
    private float raioObjetoAtual;

    private bool rbTinhaGravidade;
    private float rbDragOriginal;
    private float rbAngularDragOriginal;
    private float rbMaxAngularVelocityOriginal;
    private CollisionDetectionMode rbCollisionOriginal;
    private RigidbodyInterpolation rbInterpolationOriginal;
    private RigidbodyConstraints rbConstraintsOriginal;

    private readonly RaycastHit[] hitsSelecao = new RaycastHit[32];
    private readonly RaycastHit[] hitsBloqueio = new RaycastHit[32];
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

    private void Update()
    {
        ResolverReferenciasLeves();

        if (cameraDoPlayer == null)
            return;

        bool segurandoMira = EstaSegurandoMira();
        bool segurandoPegar = EstaSegurandoPegar();

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

    private void PrepararObjetosPermitidos()
    {
        if (!adicionarComponenteAutomaticamenteNosPermitidos)
            return;

        if (objetosPermitidos == null)
            return;

        for (int i = 0; i < objetosPermitidos.Count; i++)
        {
            GameObject objeto = objetosPermitidos[i];
            if (objeto == null)
                continue;

            if (objeto.GetComponent<GrabbableObjectHardcore>() == null)
                objeto.AddComponent<GrabbableObjectHardcore>();

            if (adicionarRigidbodyAutomaticamente && objeto.GetComponent<Rigidbody>() == null)
            {
                Rigidbody rb = objeto.AddComponent<Rigidbody>();
                MiniMarketObjectPhysicsProfile perfil = objeto.GetComponent<MiniMarketObjectPhysicsProfile>();
                rb.mass = perfil != null ? Mathf.Max(0.01f, perfil.massaVirtual) : massaPadraoSemRigidbody;
                rb.interpolation = RigidbodyInterpolation.Interpolate;
                rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            }
        }
    }

    private bool EstaSegurandoMira()
    {
        if (!selecionarApenasEnquantoMira)
            return true;

        bool botaoDireito = Input.GetMouseButton(botaoDeMira);

        if (botaoDireito)
            return true;

        if (aceitarEstadoDoCrosshairAim && crosshairAim != null && crosshairAim.EstaMirando)
            return true;

        if (aceitarEstadoZoomCameraGTA && cameraGTA != null && cameraGTA.EstaEmPrimeiraPessoa)
            return true;

        return false;
    }

    private bool EstaSegurandoPegar()
    {
        return Input.GetMouseButton(botaoDePegar);
    }

    private void AtualizarSelecao()
    {
        GrabbableObjectHardcore novoSelecionado = ProcurarObjetoNaMira();

        if (novoSelecionado == objetoSelecionado)
            return;

        LimparSelecao();
        objetoSelecionado = novoSelecionado;

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

        if (hitCount <= 0)
            return null;

        OrdenarHitsPorDistancia(hitsSelecao, hitCount);

        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit hit = hitsSelecao[i];

            if (hit.collider == null)
                continue;

            if (DeveIgnorarCollider(hit.collider))
                continue;

            GrabbableObjectHardcore grabbable = ResolverGrabbable(hit.collider);

            if (grabbable == null)
                continue;

            if (!grabbable.podeSerPego)
                continue;

            if (!PodePegarFisicamente(grabbable, out string motivo))
            {
                if (logarMotivoNaoPegou)
                    Debug.Log("[PlayerObjectGrabberHardcore] Nao pegou " + grabbable.name + ": " + motivo);
                continue;
            }

            return grabbable;
        }

        return null;
    }

    private void OrdenarHitsPorDistancia(RaycastHit[] hits, int count)
    {
        for (int i = 0; i < count - 1; i++)
        {
            for (int j = i + 1; j < count; j++)
            {
                if (hits[j].distance < hits[i].distance)
                {
                    RaycastHit temp = hits[i];
                    hits[i] = hits[j];
                    hits[j] = temp;
                }
            }
        }
    }

    private GrabbableObjectHardcore ResolverGrabbable(Collider collider)
    {
        GrabbableObjectHardcore grabbable = collider.GetComponentInParent<GrabbableObjectHardcore>();

        if (grabbable != null)
        {
            if (permitirQualquerObjetoComGrabbable)
                return grabbable;

            if (!usarListaDeObjetosPermitidos)
                return grabbable;

            if (ObjetoEstaNaLista(grabbable.gameObject))
                return grabbable;
        }

        if (!usarListaDeObjetosPermitidos)
            return grabbable;

        GameObject objetoPermitido = EncontrarObjetoPermitidoPeloCollider(collider);

        if (objetoPermitido == null)
            return null;

        grabbable = objetoPermitido.GetComponent<GrabbableObjectHardcore>();

        if (grabbable == null && adicionarComponenteAutomaticamenteNosPermitidos)
            grabbable = objetoPermitido.AddComponent<GrabbableObjectHardcore>();

        return grabbable;
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

            if (objeto == permitido)
                return true;

            if (objeto.transform.IsChildOf(permitido.transform))
                return true;

            if (permitido.transform.IsChildOf(objeto.transform))
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

            if (hitTransform == permitido.transform)
                return permitido;

            if (hitTransform.IsChildOf(permitido.transform))
                return permitido;
        }

        return null;
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
            motivo = "profile bloqueou";
            return false;
        }

        Rigidbody rb = ResolverRigidbody(objeto, perfil);
        float massa = rb != null ? Mathf.Max(0.01f, rb.mass) : (perfil != null ? perfil.massaVirtual : massaPadraoSemRigidbody);

        float limiteMassa = perfil != null && perfil.sobrescreverLimitesGlobais ? perfil.massaMaximaParaPegar : massaMaximaParaPegar;
        if (massa > limiteMassa)
        {
            motivo = "massa " + massa.ToString("0.0") + " maior que limite " + limiteMassa.ToString("0.0");
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
                motivo = "altura maior que limite";
                return false;
            }

            if (bounds.size.x > limiteLargura || bounds.size.z > limiteComprimento)
            {
                motivo = "largura/comprimento maior que limite";
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
        if (objeto == null)
            return;

        if (!PodePegarFisicamente(objeto, out string motivo))
        {
            if (logarMotivoNaoPegou)
                Debug.Log("[PlayerObjectGrabberHardcore] Falha ao pegar: " + motivo);
            return;
        }

        objetoPegando = objeto;
        objetoSelecionado = null;
        perfilPegando = objetoPegando.GetComponent<MiniMarketObjectPhysicsProfile>();
        rbPegando = ResolverRigidbody(objetoPegando, perfilPegando);

        objetoPegando.ComecarPegar();
        rotacaoOriginalObjeto = objetoPegando.transform.rotation;
        distanciaAtualSegurando = CalcularDistanciaInicialSegurando();

        CacheCollidersObjetoPegando();
        PrepararRigidbodyParaSegurar();
    }

    private void PrepararRigidbodyParaSegurar()
    {
        if (rbPegando == null)
            return;

        massaAtual = Mathf.Max(0.01f, rbPegando.mass);
        raioObjetoAtual = CalcularRaioObjeto(objetoPegando.transform);

        rbTinhaGravidade = rbPegando.useGravity;
        rbDragOriginal = rbPegando.drag;
        rbAngularDragOriginal = rbPegando.angularDrag;
        rbMaxAngularVelocityOriginal = rbPegando.maxAngularVelocity;
        rbCollisionOriginal = rbPegando.collisionDetectionMode;
        rbInterpolationOriginal = rbPegando.interpolation;
        rbConstraintsOriginal = rbPegando.constraints;

        bool gravidadeSegurando = perfilPegando != null ? perfilPegando.usarGravidadeEnquantoSegura : usarGravidadeEnquantoSegura;
        rbPegando.useGravity = gravidadeSegurando;
        rbPegando.drag = dragEnquantoSegura;
        rbPegando.angularDrag = angularDragEnquantoSegura;
        rbPegando.maxAngularVelocity = maxAngularVelocityEnquantoSegura;
        rbPegando.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        rbPegando.interpolation = RigidbodyInterpolation.Interpolate;
        rbPegando.WakeUp();
    }

    private float CalcularDistanciaInicialSegurando()
    {
        float distancia = distanciaSegurando;

        if (ajustarDistanciaPeloTamanho && objetoPegando != null)
            distancia += CalcularRaioObjeto(objetoPegando.transform) * 0.35f;

        if (perfilPegando != null)
            distancia *= perfilPegando.multiplicadorDistanciaSegurar;

        return CalcularDistanciaPermitidaPorPeso(distancia);
    }

    private float CalcularDistanciaPermitidaPorPeso(float distancia)
    {
        float massa = rbPegando != null ? Mathf.Max(0.01f, rbPegando.mass) : massaPadraoSemRigidbody;
        float peso01 = Mathf.InverseLerp(0.5f, Mathf.Max(0.6f, massaMaximaParaPegar), massa);
        float distanciaMaxPeso = Mathf.Lerp(distanciaMaximaSegurando, Mathf.Max(distanciaMinimaSegurando, distanciaMaximaSegurando * 0.65f), peso01 * influenciaPesoNaDistancia);

        if (limitarDistanciaSegurando)
            distancia = Mathf.Clamp(distancia, distanciaMinimaSegurando, distanciaMaxPeso);

        return distancia;
    }

    private void AtualizarScrollDistancia()
    {
        if (!permitirScrollDistancia)
            return;

        float scroll = Input.mouseScrollDelta.y;
        if (Mathf.Abs(scroll) < 0.001f)
            return;

        distanciaAtualSegurando += scroll * velocidadeScrollDistancia;
        distanciaAtualSegurando = CalcularDistanciaPermitidaPorPeso(distanciaAtualSegurando);
    }

    private void MoverObjetoPegandoFisico()
    {
        if (objetoPegando == null)
            return;

        Vector3 destinoCentroMassa = CalcularPontoSegurandoSeguro();

        if (rbPegando != null)
        {
            Vector3 centroAtual = rbPegando.worldCenterOfMass;
            Vector3 erro = destinoCentroMassa - centroAtual;

            if (soltarSeFicarMuitoLonge && erro.magnitude > distanciaMaximaErroAntesDeSoltar)
            {
                SoltarObjeto();
                return;
            }

            float multiplicadorPerfil = perfilPegando != null ? perfilPegando.multiplicadorForcaSegurar : 1f;
            float peso01 = Mathf.InverseLerp(0.5f, Mathf.Max(0.6f, massaMaximaParaPegar), massaAtual);
            float forcaPeso = Mathf.Lerp(1f, 0.38f, peso01 * influenciaPesoNaForca);

            Vector3 velocidadeAlvo = erro * forcaSegurar * forcaPeso * multiplicadorPerfil * Time.fixedDeltaTime;
            velocidadeAlvo = Vector3.ClampMagnitude(velocidadeAlvo, velocidadeMaximaObjeto);

            Vector3 aceleracaoDesejada = (velocidadeAlvo - rbPegando.velocity) * amortecimentoSegurar;
            Vector3 forca = aceleracaoDesejada * massaAtual;
            forca = Vector3.ClampMagnitude(forca, forcaMaximaSegurar * multiplicadorPerfil);

            rbPegando.AddForce(forca, ForceMode.Force);
            AplicarRotacaoObjetoFisica();
        }
        else
        {
            float suavizacao = CalcularSuavizacao(Mathf.Max(1f, forcaSegurar * 0.06f), Time.fixedDeltaTime);
            objetoPegando.transform.position = Vector3.Lerp(objetoPegando.transform.position, destinoCentroMassa, suavizacao);
            objetoPegando.transform.rotation = Quaternion.Slerp(objetoPegando.transform.rotation, CalcularRotacaoObjeto(), CalcularSuavizacao(velocidadeRotacaoObjeto, Time.fixedDeltaTime));
        }
    }

    private void AplicarRotacaoObjetoFisica()
    {
        if (rbPegando == null || objetoPegando == null)
            return;

        Quaternion rotacaoAlvo = CalcularRotacaoObjeto();
        Quaternion novaRotacao = Quaternion.Slerp(rbPegando.rotation, rotacaoAlvo, CalcularSuavizacao(velocidadeRotacaoObjeto, Time.fixedDeltaTime));
        rbPegando.MoveRotation(novaRotacao);
    }

    private Vector3 CalcularPontoSegurandoSeguro()
    {
        Transform cam = cameraDoPlayer.transform;

        Vector3 destino = cam.position
            + cam.forward * distanciaAtualSegurando
            + cam.right * offsetSegurando.x
            + cam.up * offsetSegurando.y;

        if (!impedirAtravessarParede)
            return destino;

        Vector3 direcao = destino - cam.position;
        float distancia = direcao.magnitude;

        if (distancia <= 0.001f)
            return destino;

        direcao /= distancia;
        float raio = Mathf.Max(0.03f, raioObjetoAtual * multiplicadorRaioColisao);

        int hitCount = Physics.SphereCastNonAlloc(
            cam.position,
            raio,
            direcao,
            hitsBloqueio,
            distancia,
            layersBloqueioObjeto,
            QueryTriggerInteraction.Ignore
        );

        float menorDistancia = distancia;
        bool bloqueado = false;

        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit hit = hitsBloqueio[i];
            if (hit.collider == null)
                continue;

            if (DeveIgnorarCollider(hit.collider))
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

        if (alinharObjetoComCamera)
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

            rbPegando.velocity *= multVel;
            rbPegando.angularVelocity *= multRot;

            rbPegando.useGravity = rbTinhaGravidade;
            rbPegando.drag = rbDragOriginal;
            rbPegando.angularDrag = rbAngularDragOriginal;
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

        Vector3 direcao = plano.normalized;
        Vector3 novaPosicao = playerPos + direcao * distanciaMinimaDoPlayerAoSoltar;
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

        Collider[] colliders = objetoPegando.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] != null)
                collidersObjetoPegando.Add(colliders[i]);
        }
    }

    private bool DeveIgnorarCollider(Collider col)
    {
        if (col == null || !col.enabled || col.isTrigger)
            return true;

        if (raizDoPlayer != null && col.transform.IsChildOf(raizDoPlayer))
            return true;

        if (objetoPegando != null)
        {
            if (col.transform == objetoPegando.transform || col.transform.IsChildOf(objetoPegando.transform))
                return true;
        }

        for (int i = 0; i < collidersObjetoPegando.Count; i++)
        {
            if (col == collidersObjetoPegando[i])
                return true;
        }

        return false;
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
            Renderer renderer = renderers[i];
            if (renderer == null || !renderer.enabled)
                continue;

            if (!temBounds)
            {
                bounds = renderer.bounds;
                temBounds = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        return bounds;
    }

    private float CalcularRaioObjeto(Transform raiz)
    {
        Bounds bounds = CalcularBoundsObjeto(raiz, out bool temBounds);
        if (!temBounds)
            return 0.25f;

        return Mathf.Clamp(Mathf.Max(bounds.extents.x, bounds.extents.y, bounds.extents.z), 0.08f, 2.5f);
    }

    private float CalcularSuavizacao(float velocidade, float deltaTime)
    {
        return 1f - Mathf.Exp(-velocidade * deltaTime);
    }

    private void OnValidate()
    {
        massaMaximaParaPegar = Mathf.Max(0.01f, massaMaximaParaPegar);
        massaPadraoSemRigidbody = Mathf.Max(0.01f, massaPadraoSemRigidbody);
        alturaMaximaObjeto = Mathf.Max(0.05f, alturaMaximaObjeto);
        larguraMaximaObjeto = Mathf.Max(0.05f, larguraMaximaObjeto);
        comprimentoMaximoObjeto = Mathf.Max(0.05f, comprimentoMaximoObjeto);

        distanciaSelecao = Mathf.Max(0.1f, distanciaSelecao);
        raioSelecao = Mathf.Max(0.001f, raioSelecao);
        distanciaSegurando = Mathf.Max(0.3f, distanciaSegurando);
        distanciaMinimaSegurando = Mathf.Max(0.3f, distanciaMinimaSegurando);
        distanciaMaximaSegurando = Mathf.Max(distanciaMinimaSegurando, distanciaMaximaSegurando);
        velocidadeScrollDistancia = Mathf.Max(0.05f, velocidadeScrollDistancia);

        forcaSegurar = Mathf.Max(1f, forcaSegurar);
        amortecimentoSegurar = Mathf.Max(0f, amortecimentoSegurar);
        velocidadeMaximaObjeto = Mathf.Max(0.1f, velocidadeMaximaObjeto);
        forcaMaximaSegurar = Mathf.Max(1f, forcaMaximaSegurar);
        distanciaMaximaErroAntesDeSoltar = Mathf.Max(0.5f, distanciaMaximaErroAntesDeSoltar);
        dragEnquantoSegura = Mathf.Max(0f, dragEnquantoSegura);
        angularDragEnquantoSegura = Mathf.Max(0f, angularDragEnquantoSegura);
        maxAngularVelocityEnquantoSegura = Mathf.Max(0.05f, maxAngularVelocityEnquantoSegura);
        multiplicadorRaioColisao = Mathf.Max(0.01f, multiplicadorRaioColisao);
        margemParede = Mathf.Max(0f, margemParede);
        distanciaMinimaDoPlayerAoSoltar = Mathf.Max(0.05f, distanciaMinimaDoPlayerAoSoltar);
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
