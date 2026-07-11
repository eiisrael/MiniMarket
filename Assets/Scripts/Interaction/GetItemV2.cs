using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// MiniMarket GetItem V2.
///
/// Sistema limpo de selecionar, pegar, mover e soltar objetos pela câmera ativa.
/// Compatível com Unity antigo e Unity 6: usa wrappers para velocity/linearVelocity e drag/linearDamping.
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(20200)]
public class GetItemV2 : MonoBehaviour
{
    [Header("Referências")]
    public UnityEngine.Camera cameraOrigem;
    public Transform raizPlayer;

    [Header("Input")]
    [Range(0, 2)] public int botaoPegar = 0;
    [Range(0, 2)] public int botaoMiraObrigatoria = 1;
    public bool exigirMiraParaSelecionar = true;
    public bool exigirMiraParaSegurar = true;

    [Header("Seleção")]
    [Min(0.1f)] public float distanciaSelecao = 7.5f;
    [Min(0.01f)] public float raioSelecao = 0.22f;
    public LayerMask layersSelecionaveis = ~0;
    [Min(0f)] public float memoriaSelecao = 0.16f;
    public bool ordenarHitsPorDistancia = true;

    [Header("Segurar")]
    [Min(0.3f)] public float distanciaSegurando = 2.55f;
    [Min(0.3f)] public float distanciaMinima = 1.0f;
    [Min(0.3f)] public float distanciaMaxima = 5f;
    public Vector2 offsetSegurando = new Vector2(0f, 0.04f);
    public bool permitirScrollDistancia = true;
    [Min(0.01f)] public float velocidadeScroll = 0.35f;

    [Header("Movimento Físico")]
    public bool usarKinematicEnquantoSegura = true;
    [Range(0.01f, 1f)] public float suavizacaoMovimento = 0.72f;
    [Min(1f)] public float rigidezVelocidade = 24f;
    [Min(0.5f)] public float velocidadeMaxima = 14f;
    [Min(0.1f)] public float velocidadeRotacao = 16f;
    public bool manterRotacaoOriginal = true;
    public bool alinharComCamera = false;

    [Header("Colisão / Anti Atravessar")]
    public bool impedirAtravessarParede = true;
    public LayerMask layersBloqueio = ~0;
    [Range(0.05f, 1f)] public float multiplicadorRaioColisao = 0.45f;
    [Min(0f)] public float margemParede = 0.12f;
    public bool ignorarTriggers = true;
    public bool soltarSeMuitoLonge = true;
    [Min(0.5f)] public float distanciaMaximaErro = 3.8f;

    [Header("Soltar")]
    public bool dinamicoAoSoltar = true;
    public bool usarGravidadeAoSoltar = true;
    public bool removerConstraintsAoSoltar = true;
    [Range(0f, 1f)] public float multiplicadorVelocidadeSoltar = 0.12f;
    [Range(0f, 1f)] public float multiplicadorAngularSoltar = 0.12f;
    [Min(0f)] public float linearDampingSoltar = 0.2f;
    [Min(0f)] public float angularDampingSoltar = 0.2f;

    [Header("Preparação Automática")]
    public bool adicionarRigidbodySeNaoExistir = true;
    public bool adicionarColliderSeNaoExistir = true;
    [Min(0.01f)] public float massaPadrao = 1f;

    [Header("Debug")]
    public bool desenharDebug = false;
    public bool logarEventos = false;

    private readonly RaycastHit[] hitsSelecao = new RaycastHit[64];
    private readonly RaycastHit[] hitsBloqueio = new RaycastHit[32];
    private readonly List<Collider> collidersPegando = new List<Collider>();

    private GetItemObjectV2 selecionado;
    private GetItemObjectV2 ultimoSelecionado;
    private GetItemObjectV2 pegando;
    private Rigidbody rbPegando;
    private Quaternion rotacaoOriginal;
    private Vector3 centroOffset;
    private float distanciaAtual;
    private float raioObjetoAtual = 0.25f;
    private float tempoUltimaSelecao;

    private bool rbGravityOriginal;
    private bool rbKinematicOriginal;
    private float rbLinearDampingOriginal;
    private float rbAngularDampingOriginal;
    private RigidbodyConstraints rbConstraintsOriginal;
    private RigidbodyInterpolation rbInterpolationOriginal;
    private CollisionDetectionMode rbCollisionOriginal;

    public bool EstaPegando => pegando != null;
    public GetItemObjectV2 Selecionado => selecionado;
    public GetItemObjectV2 Pegando => pegando;

    private void Awake()
    {
        ResolverReferencias();
        distanciaAtual = distanciaSegurando;
    }

    private void OnDisable()
    {
        SoltarObjeto();
        LimparSelecao();
    }

    private void Update()
    {
        ResolverReferencias();

        if (cameraOrigem == null)
            return;

        bool miraOk = !exigirMiraParaSelecionar || Input.GetMouseButton(botaoMiraObrigatoria);
        bool segurandoPegar = Input.GetMouseButton(botaoPegar);

        if (!miraOk && pegando == null)
        {
            LimparSelecao();
            return;
        }

        if (pegando == null)
        {
            AtualizarSelecao();

            if (segurandoPegar && selecionado != null)
                PegarObjeto(selecionado);
        }
        else
        {
            if (exigirMiraParaSegurar && !Input.GetMouseButton(botaoMiraObrigatoria))
            {
                SoltarObjeto();
                return;
            }

            AtualizarScroll();

            if (!segurandoPegar)
                SoltarObjeto();
        }
    }

    private void FixedUpdate()
    {
        if (pegando != null)
            MoverObjeto();
    }

    private void ResolverReferencias()
    {
        if (cameraOrigem == null)
            cameraOrigem = GetComponent<UnityEngine.Camera>();

        if (cameraOrigem == null)
            cameraOrigem = UnityEngine.Camera.main;
    }

    private void AtualizarSelecao()
    {
        GetItemObjectV2 novo = ProcurarObjeto();

        if (novo != null)
        {
            ultimoSelecionado = novo;
            tempoUltimaSelecao = Time.unscaledTime;
        }
        else if (ultimoSelecionado != null && Time.unscaledTime - tempoUltimaSelecao <= memoriaSelecao)
        {
            novo = ultimoSelecionado;
        }

        if (novo == selecionado)
            return;

        LimparSelecao();
        selecionado = novo;

        if (selecionado != null)
            selecionado.Selecionar(true);
    }

    private GetItemObjectV2 ProcurarObjeto()
    {
        Ray ray = cameraOrigem.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        QueryTriggerInteraction triggerMode = ignorarTriggers ? QueryTriggerInteraction.Ignore : QueryTriggerInteraction.Collide;
        int count = Physics.SphereCastNonAlloc(ray, raioSelecao, hitsSelecao, distanciaSelecao, layersSelecionaveis, triggerMode);

        if (ordenarHitsPorDistancia)
            OrdenarHits(hitsSelecao, count);

        for (int i = 0; i < count; i++)
        {
            RaycastHit hit = hitsSelecao[i];
            if (hit.collider == null || DeveIgnorarCollider(hit.collider))
                continue;

            GetItemObjectV2 obj = hit.collider.GetComponentInParent<GetItemObjectV2>();
            if (PodeSelecionar(obj))
                return obj;
        }

        return null;
    }

    private bool PodeSelecionar(GetItemObjectV2 obj)
    {
        if (obj == null || !obj.podeSerPego || obj.sendoPego)
            return false;

        Bounds bounds = CalcularBounds(obj.transform, out bool temBounds);
        if (temBounds && Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z) > obj.tamanhoMaximo)
            return false;

        return true;
    }

    private void PegarObjeto(GetItemObjectV2 obj)
    {
        if (obj == null)
            return;

        PrepararFisica(obj);

        pegando = obj;
        selecionado = null;
        ultimoSelecionado = null;
        pegando.ComecarPegar();
        rotacaoOriginal = pegando.transform.rotation;
        raioObjetoAtual = CalcularRaio(pegando.transform);
        distanciaAtual = Mathf.Clamp(distanciaSegurando * pegando.multiplicadorDistancia, distanciaMinima, distanciaMaxima);
        centroOffset = rbPegando != null ? rbPegando.position - rbPegando.worldCenterOfMass : Vector3.zero;
        CacheColliders();

        if (logarEventos)
            UpgradeLogger.Log("GetItemV2", "Objeto pego", pegando.name, "getitem-v2-pick-" + pegando.name, 0.5f);
    }

    private void PrepararFisica(GetItemObjectV2 obj)
    {
        rbPegando = obj.GetComponent<Rigidbody>();

        if (rbPegando == null && adicionarRigidbodySeNaoExistir)
        {
            rbPegando = obj.gameObject.AddComponent<Rigidbody>();
            rbPegando.mass = Mathf.Max(0.01f, obj.massaVirtual > 0f ? obj.massaVirtual : massaPadrao);
        }

        if (obj.GetComponentInChildren<Collider>() == null && adicionarColliderSeNaoExistir)
            obj.gameObject.AddComponent<BoxCollider>();

        if (rbPegando == null)
            return;

        rbGravityOriginal = rbPegando.useGravity;
        rbKinematicOriginal = rbPegando.isKinematic;
        rbLinearDampingOriginal = GetLinearDamping(rbPegando);
        rbAngularDampingOriginal = GetAngularDamping(rbPegando);
        rbConstraintsOriginal = rbPegando.constraints;
        rbInterpolationOriginal = rbPegando.interpolation;
        rbCollisionOriginal = rbPegando.collisionDetectionMode;

        SetLinearVelocity(rbPegando, Vector3.zero);
        rbPegando.angularVelocity = Vector3.zero;
        rbPegando.useGravity = false;
        rbPegando.isKinematic = usarKinematicEnquantoSegura;
        rbPegando.constraints = RigidbodyConstraints.None;
        SetLinearDamping(rbPegando, 1.5f);
        SetAngularDamping(rbPegando, 4f);
        rbPegando.interpolation = RigidbodyInterpolation.Interpolate;
        rbPegando.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        rbPegando.WakeUp();
    }

    private void AtualizarScroll()
    {
        if (!permitirScrollDistancia)
            return;

        float scroll = Input.mouseScrollDelta.y;
        if (Mathf.Abs(scroll) <= 0.001f)
            return;

        distanciaAtual = Mathf.Clamp(distanciaAtual + scroll * velocidadeScroll, distanciaMinima, distanciaMaxima);
    }

    private void MoverObjeto()
    {
        if (pegando == null)
            return;

        Vector3 alvo = CalcularPontoSegurandoSeguro();

        if (rbPegando == null)
        {
            pegando.transform.position = Vector3.Lerp(pegando.transform.position, alvo, suavizacaoMovimento);
            return;
        }

        Vector3 centroAtual = rbPegando.worldCenterOfMass;
        Vector3 erro = alvo - centroAtual;

        if (soltarSeMuitoLonge && erro.magnitude > distanciaMaximaErro)
        {
            SoltarObjeto();
            return;
        }

        if (usarKinematicEnquantoSegura || rbPegando.isKinematic)
        {
            Vector3 alvoRb = alvo + centroOffset;
            rbPegando.MovePosition(Vector3.Lerp(rbPegando.position, alvoRb, suavizacaoMovimento));
        }
        else
        {
            Vector3 velocidadeDesejada = Vector3.ClampMagnitude(erro * rigidezVelocidade, velocidadeMaxima);
            SetLinearVelocity(rbPegando, Vector3.Lerp(GetLinearVelocity(rbPegando), velocidadeDesejada, suavizacaoMovimento));
        }

        Quaternion rotacaoAlvo = CalcularRotacaoAlvo();
        rbPegando.MoveRotation(Quaternion.Slerp(rbPegando.rotation, rotacaoAlvo, Suavizacao(velocidadeRotacao, Time.fixedDeltaTime)));
    }

    private Vector3 CalcularPontoSegurandoSeguro()
    {
        Transform cam = cameraOrigem.transform;
        Vector3 destino = cam.position + cam.forward * distanciaAtual + cam.right * offsetSegurando.x + cam.up * offsetSegurando.y;

        if (!impedirAtravessarParede)
            return destino;

        Vector3 direcao = destino - cam.position;
        float distancia = direcao.magnitude;
        if (distancia <= 0.001f)
            return destino;

        direcao /= distancia;
        float raio = Mathf.Max(0.03f, raioObjetoAtual * multiplicadorRaioColisao);
        QueryTriggerInteraction triggerMode = ignorarTriggers ? QueryTriggerInteraction.Ignore : QueryTriggerInteraction.Collide;
        int count = Physics.SphereCastNonAlloc(cam.position, raio, direcao, hitsBloqueio, distancia, layersBloqueio, triggerMode);
        float menor = distancia;

        for (int i = 0; i < count; i++)
        {
            RaycastHit hit = hitsBloqueio[i];
            if (hit.collider == null || DeveIgnorarCollider(hit.collider))
                continue;

            if (hit.distance < menor)
                menor = hit.distance;
        }

        if (menor < distancia)
            destino = cam.position + direcao * Mathf.Max(distanciaMinima, menor - raio - margemParede);

        if (desenharDebug)
            Debug.DrawLine(cam.position, destino, Color.cyan);

        return destino;
    }

    private Quaternion CalcularRotacaoAlvo()
    {
        if (manterRotacaoOriginal)
            return rotacaoOriginal;

        if (alinharComCamera && cameraOrigem != null)
            return Quaternion.LookRotation(cameraOrigem.transform.forward, Vector3.up);

        return pegando != null ? pegando.transform.rotation : Quaternion.identity;
    }

    public void SoltarObjeto()
    {
        if (pegando == null)
            return;

        string nome = pegando.name;

        if (rbPegando != null)
        {
            Vector3 vel = rbPegando.isKinematic ? Vector3.zero : GetLinearVelocity(rbPegando) * multiplicadorVelocidadeSoltar;
            Vector3 ang = rbPegando.isKinematic ? Vector3.zero : rbPegando.angularVelocity * multiplicadorAngularSoltar;

            rbPegando.isKinematic = dinamicoAoSoltar ? false : rbKinematicOriginal;
            rbPegando.useGravity = dinamicoAoSoltar ? usarGravidadeAoSoltar : rbGravityOriginal;
            rbPegando.constraints = removerConstraintsAoSoltar ? RigidbodyConstraints.None : rbConstraintsOriginal;
            SetLinearDamping(rbPegando, dinamicoAoSoltar ? linearDampingSoltar : rbLinearDampingOriginal);
            SetAngularDamping(rbPegando, dinamicoAoSoltar ? angularDampingSoltar : rbAngularDampingOriginal);
            rbPegando.interpolation = rbInterpolationOriginal;
            rbPegando.collisionDetectionMode = rbCollisionOriginal;
            SetLinearVelocity(rbPegando, vel);
            rbPegando.angularVelocity = ang;
            rbPegando.WakeUp();
        }

        pegando.Soltar();
        pegando = null;
        rbPegando = null;
        collidersPegando.Clear();

        if (logarEventos)
            UpgradeLogger.Log("GetItemV2", "Objeto solto", nome, "getitem-v2-release-" + nome, 0.5f);
    }

    private void LimparSelecao()
    {
        if (selecionado != null)
            selecionado.Selecionar(false);

        selecionado = null;
    }

    private void CacheColliders()
    {
        collidersPegando.Clear();
        if (pegando == null)
            return;

        Collider[] cols = pegando.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < cols.Length; i++)
        {
            if (cols[i] != null)
                collidersPegando.Add(cols[i]);
        }
    }

    private bool DeveIgnorarCollider(Collider col)
    {
        if (col == null || !col.enabled || (col.isTrigger && ignorarTriggers))
            return true;

        if (raizPlayer != null && col.transform.IsChildOf(raizPlayer))
            return true;

        if (pegando != null && (col.transform == pegando.transform || col.transform.IsChildOf(pegando.transform)))
            return true;

        for (int i = 0; i < collidersPegando.Count; i++)
        {
            if (col == collidersPegando[i])
                return true;
        }

        return false;
    }

    private Bounds CalcularBounds(Transform raiz, out bool temBounds)
    {
        temBounds = false;
        Bounds bounds = new Bounds(raiz != null ? raiz.position : Vector3.zero, Vector3.zero);

        if (raiz == null)
            return bounds;

        Renderer[] renderers = raiz.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer r = renderers[i];
            if (r == null)
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

        Collider[] colliders = raiz.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider c = colliders[i];
            if (c == null || c.isTrigger)
                continue;

            if (!temBounds)
            {
                bounds = c.bounds;
                temBounds = true;
            }
            else
            {
                bounds.Encapsulate(c.bounds);
            }
        }

        return bounds;
    }

    private float CalcularRaio(Transform raiz)
    {
        Bounds b = CalcularBounds(raiz, out bool temBounds);
        if (!temBounds)
            return 0.25f;

        return Mathf.Clamp(Mathf.Max(b.extents.x, b.extents.y, b.extents.z), 0.08f, 2.5f);
    }

    private void OrdenarHits(RaycastHit[] array, int count)
    {
        for (int i = 0; i < count - 1; i++)
        {
            for (int j = i + 1; j < count; j++)
            {
                if (array[j].distance < array[i].distance)
                {
                    RaycastHit tmp = array[i];
                    array[i] = array[j];
                    array[j] = tmp;
                }
            }
        }
    }

    private float Suavizacao(float velocidade, float dt)
    {
        if (velocidade <= 0.0001f)
            return 1f;
        return 1f - Mathf.Exp(-velocidade * dt);
    }

    private Vector3 GetLinearVelocity(Rigidbody rb)
    {
#if UNITY_6000_0_OR_NEWER
        return rb.linearVelocity;
#else
        return rb.velocity;
#endif
    }

    private void SetLinearVelocity(Rigidbody rb, Vector3 value)
    {
#if UNITY_6000_0_OR_NEWER
        rb.linearVelocity = value;
#else
        rb.velocity = value;
#endif
    }

    private float GetLinearDamping(Rigidbody rb)
    {
#if UNITY_6000_0_OR_NEWER
        return rb.linearDamping;
#else
        return rb.drag;
#endif
    }

    private void SetLinearDamping(Rigidbody rb, float value)
    {
#if UNITY_6000_0_OR_NEWER
        rb.linearDamping = value;
#else
        rb.drag = value;
#endif
    }

    private float GetAngularDamping(Rigidbody rb)
    {
#if UNITY_6000_0_OR_NEWER
        return rb.angularDamping;
#else
        return rb.angularDrag;
#endif
    }

    private void SetAngularDamping(Rigidbody rb, float value)
    {
#if UNITY_6000_0_OR_NEWER
        rb.angularDamping = value;
#else
        rb.angularDrag = value;
#endif
    }

    private void OnValidate()
    {
        distanciaSegurando = Mathf.Clamp(distanciaSegurando, distanciaMinima, distanciaMaxima);
        raioSelecao = Mathf.Max(0.01f, raioSelecao);
    }
}
