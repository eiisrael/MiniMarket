using UnityEngine;

/// <summary>
/// Suavizador de colisão da câmera em terceira pessoa.
///
/// Diagnóstico real dos logs v2.1.3:
/// - Os raros saltos restantes aconteciam quando a distância câmera->alvo caía para ~1.0.
/// - Isso era típico de colisão contra chão/calçada/objetos baixos ou raycast perto do player.
/// - Visualmente parecia que a câmera estava "encaixando" na cena e puxando para perto do personagem.
///
/// Correção:
/// - Desativa a colisão instantânea da CameraGTAFollowHardcore antes do LateUpdate da câmera.
/// - Este script assume 100% da colisão da câmera em terceira pessoa.
/// - Ignora chão/superfícies horizontais na colisão da câmera.
/// - Impõe uma distância mínima visual para a terceira pessoa, evitando câmera colada no personagem.
/// - Suaviza apenas aproximação/retorno de parede, sem efeito mola no movimento normal.
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

    [Header("Distância mínima visual")]
    [Tooltip("Evita que a câmera chegue perto demais do personagem quando a colisão pega chão/parede.")]
    public bool usarDistanciaMinimaVisualTerceiraPessoa = true;

    [Min(0.3f)] public float distanciaMinimaVisualTerceiraPessoa = 2.15f;

    [Tooltip("Se a distância normal for menor que esta margem, não força distância mínima visual.")]
    [Min(0f)] public float margemDistanciaNormal = 0.15f;

    [Header("Suavização da Distância")]
    [Min(0.1f)] public float velocidadeAproximarPorColisao = 4.2f;
    [Min(0.1f)] public float velocidadeRetornarDistanciaNormal = 3.6f;
    [Min(0.1f)] public float velocidadeEmergenciaQuandoSobreposto = 10f;
    [Min(0f)] public float zonaMortaDistancia = 0.025f;
    [Min(0.01f)] public float maxDeltaDistanciaPorFrame = 0.10f;

    [Header("Colisão")]
    [Min(0.01f)] public float raioExtraOverlap = 0.04f;
    public bool ignorarObjetosGrabbable = true;
    public bool ignorarProprioPlayer = true;

    [Tooltip("Ignora chão/calçada/terreno para a câmera não ser puxada para perto quando olha para baixo.")]
    public bool ignorarSuperficiesHorizontais = true;

    [Range(0f, 1f)] public float normalYMinimaParaIgnorarComoChao = 0.45f;

    [Tooltip("Ignora colisões muito próximas do foco/personagem, que costumam ser chão ou collider do próprio ambiente perto dos pés.")]
    [Min(0f)] public float distanciaMinimaHitParaConsiderar = 1.25f;

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
        float distanciaMinimaPermitida = CalcularDistanciaMinimaPermitida(distanciaNormal);

        if (!possuiDistanciaAtual)
        {
            distanciaAtual = Mathf.Min(Vector3.Distance(cameraMonitorada.transform.position, focus), distanciaNormal);
            distanciaAtual = Mathf.Clamp(distanciaAtual, distanciaMinimaPermitida, distanciaNormal);
            possuiDistanciaAtual = true;
        }

        float alvoDistancia = Mathf.Clamp(distanciaSegura, distanciaMinimaPermitida, distanciaNormal);
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
                MiniMarketUpgradeLogger.Log("Camera", "Colisão suavizada", "Distância normal=" + distanciaNormal.ToString("0.000") + " segura=" + distanciaSegura.ToString("0.000") + " atual=" + distanciaAtual.ToString("0.000") + " minima=" + distanciaMinimaPermitida.ToString("0.000"), "camera-collision-smooth", 1f, LogType.Warning);
            }
        }

        distanciaAtual = Mathf.Clamp(distanciaAtual, distanciaMinimaPermitida, distanciaNormal);
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

    private float CalcularDistanciaMinimaPermitida(float distanciaNormal)
    {
        float minima = cameraGTA != null ? cameraGTA.cameraMinDistanceFromFocus : 0.85f;

        if (usarDistanciaMinimaVisualTerceiraPessoa && distanciaNormal > distanciaMinimaVisualTerceiraPessoa + margemDistanciaNormal)
            minima = Mathf.Max(minima, distanciaMinimaVisualTerceiraPessoa);

        return Mathf.Clamp(minima, 0.05f, distanciaNormal);
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
            if (DeveIgnorarHit(hit, focus))
                continue;

            float distanciaHit = Mathf.Max(CalcularDistanciaMinimaPermitida(distanciaNormal), hit.distance - cameraGTA.cameraCollisionOffset);
            if (distanciaHit < distanciaSegura)
                distanciaSegura = distanciaHit;
        }

        return Mathf.Clamp(distanciaSegura, CalcularDistanciaMinimaPermitida(distanciaNormal), distanciaNormal);
    }

    private bool DeveIgnorarHit(RaycastHit hit, Vector3 focus)
    {
        if (hit.collider == null || DeveIgnorarCollider(hit.collider))
            return true;

        if (hit.distance < distanciaMinimaHitParaConsiderar)
            return true;

        if (ignorarSuperficiesHorizontais && hit.normal.y >= normalYMinimaParaIgnorarComoChao)
            return true;

        return false;
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
