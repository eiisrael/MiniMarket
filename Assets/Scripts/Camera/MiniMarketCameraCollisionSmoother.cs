using UnityEngine;

/// <summary>
/// Suavizador de colisão da câmera em terceira pessoa.
///
/// Diagnóstico real dos logs:
/// - A câmera dava salto de distância quando o anti-parede interno encurtava a câmera de uma vez.
/// - Isso gerava sensação de travada/mudança brusca de eixo ao usar W/S/A/D perto de parede/objeto.
///
/// Correção:
/// - Desativa a colisão interna instantânea da CameraGTAFollowHardcore antes do LateUpdate da câmera.
/// - Este script assume 100% da colisão da câmera em terceira pessoa.
/// - A distância câmera->alvo é suavizada separadamente da rotação/orbit.
/// - Se o jogador mexer o mouse, a órbita continua responsiva, mas a aproximação/retorno de parede não dá snap.
/// </summary>
[DefaultExecutionOrder(32500)]
public class MiniMarketCameraCollisionSmoother : MonoBehaviour
{
    [Header("Referências")]
    public Camera cameraMonitorada;
    public CameraGTAFollowHardcore cameraGTA;

    [Header("Ativação")]
    public bool ativo = true;
    public bool procurarAutomaticamente = true;
    [Min(0.2f)] public float intervaloBusca = 1f;

    [Header("Controle da Colisão")]
    [Tooltip("Desliga a colisão instantânea dentro da CameraGTAFollowHardcore para evitar dois sistemas brigando.")]
    public bool desativarColisaoInternaCameraGTA = true;

    [Tooltip("Este script calcula e aplica a colisão suavizada da câmera.")]
    public bool usarColisaoSuavizada = true;

    public bool suavizarSomenteTerceiraPessoa = true;

    [Header("Suavização da Distância")]
    [Tooltip("Velocidade máxima para aproximar a câmera quando parede/objeto aparece atrás dela.")]
    [Min(0.1f)] public float velocidadeAproximarPorColisao = 5.5f;

    [Tooltip("Velocidade máxima para voltar à distância normal quando sai da parede.")]
    [Min(0.1f)] public float velocidadeRetornarDistanciaNormal = 4.0f;

    [Tooltip("Velocidade de emergência se a câmera realmente estiver sobreposta a uma parede.")]
    [Min(0.1f)] public float velocidadeEmergenciaQuandoSobreposto = 16f;

    [Tooltip("Diferenças menores que isso são aplicadas direto para evitar vibração pequena.")]
    [Min(0f)] public float zonaMortaDistancia = 0.02f;

    [Tooltip("Limita o quanto a distância pode mudar em um único frame. Esse é o anti-snap principal.")]
    [Min(0.01f)] public float maxDeltaDistanciaPorFrame = 0.18f;

    [Header("Colisão")]
    [Min(0.01f)] public float raioExtraOverlap = 0.04f;
    public bool ignorarObjetosGrabbable = true;
    public bool ignorarProprioPlayer = true;

    [Header("Debug")]
    public bool logarIncidentes;
    public bool desenharDebug;

    private static MiniMarketCameraCollisionSmoother instancia;
    private readonly RaycastHit[] hits = new RaycastHit[32];
    private readonly Collider[] overlaps = new Collider[16];
    private float distanciaAtual;
    private bool possuiDistanciaAtual;
    private float proximaBusca;
    private float ultimoLog;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void CriarAutomaticamente()
    {
        if (instancia != null)
            return;

        GameObject go = new GameObject("MiniMarket_CameraCollisionSmoother");
        DontDestroyOnLoad(go);
        instancia = go.AddComponent<MiniMarketCameraCollisionSmoother>();
    }

    private void Awake()
    {
        if (instancia != null && instancia != this)
        {
            Destroy(gameObject);
            return;
        }

        instancia = this;
        DontDestroyOnLoad(gameObject);
        ResolverReferencias(true);
        AplicarControleNaCameraGTA();
    }

    private void Update()
    {
        if (!ativo)
            return;

        if (procurarAutomaticamente && Time.unscaledTime >= proximaBusca)
        {
            proximaBusca = Time.unscaledTime + intervaloBusca;
            ResolverReferencias(false);
        }

        // Roda em Update para garantir que a CameraGTA já chegue no LateUpdate com colisão interna desligada.
        AplicarControleNaCameraGTA();
    }

    private void LateUpdate()
    {
        if (!ativo)
            return;

        if (cameraMonitorada == null || cameraGTA == null || cameraGTA.target == null)
            return;

        AplicarControleNaCameraGTA();

        if (suavizarSomenteTerceiraPessoa && cameraGTA.EstaEmPrimeiraPessoa)
        {
            possuiDistanciaAtual = false;
            return;
        }

        AplicarSuavizacaoDeColisao();
    }

    private void ResolverReferencias(bool forcar)
    {
        if (!forcar && cameraMonitorada != null && cameraGTA != null)
            return;

        if (cameraMonitorada == null)
            cameraMonitorada = Camera.main;

        if (cameraGTA == null && cameraMonitorada != null)
            cameraGTA = cameraMonitorada.GetComponent<CameraGTAFollowHardcore>();

        if (cameraGTA == null)
            cameraGTA = Object.FindFirstObjectByType<CameraGTAFollowHardcore>(FindObjectsInactive.Include);

        if (cameraMonitorada == null && cameraGTA != null)
            cameraMonitorada = cameraGTA.GetComponent<Camera>();
    }

    private void AplicarControleNaCameraGTA()
    {
        if (cameraGTA == null || !desativarColisaoInternaCameraGTA)
            return;

        cameraGTA.usarColisaoCamera = false;
        cameraGTA.corrigirPosicaoFinalSuavizada = false;
    }

    private void AplicarSuavizacaoDeColisao()
    {
        Transform target = cameraGTA.target;
        Vector3 focus = target.position + cameraGTA.targetOffset;
        Quaternion orbit = Quaternion.Euler(cameraGTA.PitchAtual, cameraGTA.YawAtual, 0f);
        Vector3 posicaoNormal = focus - orbit * Vector3.forward * cameraGTA.distance + Vector3.up * cameraGTA.height;

        Vector3 direcao = posicaoNormal - focus;
        float distanciaNormal = direcao.magnitude;

        if (distanciaNormal <= 0.001f)
            return;

        direcao /= distanciaNormal;
        float distanciaSegura = CalcularDistanciaSegura(focus, direcao, distanciaNormal);

        if (!possuiDistanciaAtual)
        {
            distanciaAtual = Mathf.Min(Vector3.Distance(cameraMonitorada.transform.position, focus), distanciaNormal);
            if (distanciaAtual <= 0.001f)
                distanciaAtual = distanciaSegura;

            possuiDistanciaAtual = true;
        }

        float alvoDistancia = distanciaSegura;
        float diferenca = Mathf.Abs(alvoDistancia - distanciaAtual);

        if (diferenca <= zonaMortaDistancia)
        {
            distanciaAtual = alvoDistancia;
        }
        else
        {
            bool aproximandoPorColisao = alvoDistancia < distanciaAtual;
            bool cameraSobreposta = CameraEstaSobreposta(cameraMonitorada.transform.position);
            float velocidade = cameraSobreposta
                ? velocidadeEmergenciaQuandoSobreposto
                : (aproximandoPorColisao ? velocidadeAproximarPorColisao : velocidadeRetornarDistanciaNormal);

            float passo = Mathf.Min(velocidade * Time.deltaTime, maxDeltaDistanciaPorFrame);
            distanciaAtual = Mathf.MoveTowards(distanciaAtual, alvoDistancia, passo);

            if (logarIncidentes && Time.unscaledTime - ultimoLog > 1f && diferenca > 0.3f)
            {
                ultimoLog = Time.unscaledTime;
                MiniMarketUpgradeLogger.Log("Camera", "Colisão suavizada", "Distância normal=" + distanciaNormal.ToString("0.000") + " segura=" + distanciaSegura.ToString("0.000") + " atual=" + distanciaAtual.ToString("0.000"), "camera-collision-smooth", 1f, LogType.Warning);
            }
        }

        distanciaAtual = Mathf.Clamp(distanciaAtual, cameraGTA.cameraMinDistanceFromFocus, distanciaNormal);
        Vector3 novaPosicao = focus + direcao * distanciaAtual;
        Quaternion novaRotacao = Quaternion.LookRotation(focus - novaPosicao, Vector3.up);

        cameraMonitorada.transform.position = novaPosicao;
        cameraMonitorada.transform.rotation = novaRotacao;

        if (desenharDebug)
        {
            Debug.DrawLine(focus, posicaoNormal, Color.red);
            Debug.DrawLine(focus, novaPosicao, Color.green);
        }
    }

    private float CalcularDistanciaSegura(Vector3 focus, Vector3 direcao, float distanciaNormal)
    {
        if (!usarColisaoSuavizada)
            return distanciaNormal;

        float distanciaSegura = distanciaNormal;
        int count = Physics.SphereCastNonAlloc(
            focus,
            cameraGTA.cameraCollisionRadius,
            direcao,
            hits,
            distanciaNormal + cameraGTA.cameraCollisionOffset,
            cameraGTA.cameraCollisionLayers,
            QueryTriggerInteraction.Ignore
        );

        for (int i = 0; i < count; i++)
        {
            RaycastHit hit = hits[i];
            if (DeveIgnorarCollider(hit.collider))
                continue;

            float distanciaHit = Mathf.Max(cameraGTA.cameraMinDistanceFromFocus, hit.distance - cameraGTA.cameraCollisionOffset);
            if (distanciaHit < distanciaSegura)
                distanciaSegura = distanciaHit;
        }

        return Mathf.Clamp(distanciaSegura, cameraGTA.cameraMinDistanceFromFocus, distanciaNormal);
    }

    private bool CameraEstaSobreposta(Vector3 posicao)
    {
        if (cameraGTA == null)
            return false;

        int count = Physics.OverlapSphereNonAlloc(
            posicao,
            cameraGTA.cameraCollisionRadius + raioExtraOverlap,
            overlaps,
            cameraGTA.cameraCollisionLayers,
            QueryTriggerInteraction.Ignore
        );

        for (int i = 0; i < count; i++)
        {
            if (!DeveIgnorarCollider(overlaps[i]))
                return true;
        }

        return false;
    }

    private bool DeveIgnorarCollider(Collider col)
    {
        if (col == null || !col.enabled || col.isTrigger)
            return true;

        Transform t = col.transform;

        if (ignorarProprioPlayer && cameraGTA != null && cameraGTA.target != null)
        {
            if (t == cameraGTA.target || t.IsChildOf(cameraGTA.target))
                return true;
        }

        if (cameraMonitorada != null && (t == cameraMonitorada.transform || t.IsChildOf(cameraMonitorada.transform)))
            return true;

        if (ignorarObjetosGrabbable && col.GetComponentInParent<GrabbableObjectHardcore>() != null)
            return true;

        return false;
    }
}
