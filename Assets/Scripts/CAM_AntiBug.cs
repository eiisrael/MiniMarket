using UnityEngine;

[DefaultExecutionOrder(1000)]
public class CameraAntiParede : MonoBehaviour
{
    [Header("Referências")]
    [Tooltip("Pivô da câmera. Normalmente um Empty na cabeça/costas do personagem.")]
    public Transform cameraPivot;

    [Tooltip("Transform da câmera. Se deixar vazio, usa este próprio objeto.")]
    public Transform cameraVisual;

    [Tooltip("Arraste aqui o objeto principal do Player para a câmera ignorar o collider dele.")]
    public Transform jogadorParaIgnorar;

    [Header("Distância da Câmera")]
    [Tooltip("Distância normal da câmera atrás do personagem.")]
    public float distanciaNormal = 4f;

    [Tooltip("Distância mínima quando encostar em parede/objeto.")]
    public float distanciaMinima = 0.55f;

    [Tooltip("Offset do ponto de origem da câmera em relação ao pivô.")]
    public Vector3 offsetDoPivot = new Vector3(0f, 1.45f, 0f);

    [Header("Colisão")]
    [Tooltip("Raio usado para evitar que a câmera atravesse parede.")]
    public float raioDaCamera = 0.28f;

    [Tooltip("Espaço extra para a câmera não ficar colada na parede.")]
    public float margemDaParede = 0.12f;

    [Tooltip("Camadas que bloqueiam a câmera. Deixe Player fora dessa LayerMask.")]
    public LayerMask camadasQueBloqueiamCamera = ~0;

    [Tooltip("Ignora triggers na colisão da câmera.")]
    public bool ignorarTriggers = true;

    [Header("Suavização")]
    [Tooltip("Velocidade para aproximar a câmera quando tem parede.")]
    public float velocidadeAproximar = 30f;

    [Tooltip("Velocidade para voltar a câmera para trás quando não tem parede.")]
    public float velocidadeVoltar = 10f;

    [Header("Rotação")]
    [Tooltip("Copia a rotação do pivô. Use true se seu pivô já gira com o mouse.")]
    public bool copiarRotacaoDoPivot = true;

    [Tooltip("Faz a câmera olhar para o pivô. Use false se sua câmera já está com rotação correta.")]
    public bool olharParaPivot = false;

    private float distanciaAtual;

    private void Reset()
    {
        cameraVisual = transform;
    }

    private void Awake()
    {
        if (cameraVisual == null)
            cameraVisual = transform;

        distanciaAtual = distanciaNormal;
    }

    private void LateUpdate()
    {
        if (cameraPivot == null || cameraVisual == null)
            return;

        Vector3 origem = cameraPivot.position + cameraPivot.TransformDirection(offsetDoPivot);
        Vector3 direcaoParaTras = -cameraPivot.forward;

        float distanciaCorrigida = CalcularDistanciaCorrigida(origem, direcaoParaTras);

        float velocidade = distanciaCorrigida < distanciaAtual ? velocidadeAproximar : velocidadeVoltar;

        distanciaAtual = Mathf.Lerp(
            distanciaAtual,
            distanciaCorrigida,
            1f - Mathf.Exp(-velocidade * Time.deltaTime)
        );

        cameraVisual.position = origem + direcaoParaTras * distanciaAtual;

        if (olharParaPivot)
        {
            Vector3 direcaoOlhar = origem - cameraVisual.position;

            if (direcaoOlhar.sqrMagnitude > 0.001f)
            {
                cameraVisual.rotation = Quaternion.LookRotation(direcaoOlhar.normalized, Vector3.up);
            }
        }
        else if (copiarRotacaoDoPivot)
        {
            cameraVisual.rotation = cameraPivot.rotation;
        }
    }

    private float CalcularDistanciaCorrigida(Vector3 origem, Vector3 direcaoParaTras)
    {
        QueryTriggerInteraction triggerMode = ignorarTriggers
            ? QueryTriggerInteraction.Ignore
            : QueryTriggerInteraction.Collide;

        RaycastHit[] hits = Physics.SphereCastAll(
            origem,
            raioDaCamera,
            direcaoParaTras,
            distanciaNormal,
            camadasQueBloqueiamCamera,
            triggerMode
        );

        float menorDistancia = distanciaNormal;
        bool encontrouParede = false;

        foreach (RaycastHit hit in hits)
        {
            if (hit.collider == null)
                continue;

            if (jogadorParaIgnorar != null && hit.collider.transform.IsChildOf(jogadorParaIgnorar))
                continue;

            if (cameraVisual != null && hit.collider.transform.IsChildOf(cameraVisual))
                continue;

            if (hit.distance < menorDistancia)
            {
                menorDistancia = hit.distance;
                encontrouParede = true;
            }
        }

        if (!encontrouParede)
            return distanciaNormal;

        float distanciaFinal = menorDistancia - margemDaParede;

        return Mathf.Clamp(distanciaFinal, distanciaMinima, distanciaNormal);
    }

    private void OnValidate()
    {
        distanciaNormal = Mathf.Max(0.1f, distanciaNormal);
        distanciaMinima = Mathf.Clamp(distanciaMinima, 0.05f, distanciaNormal);
        raioDaCamera = Mathf.Max(0.01f, raioDaCamera);
        margemDaParede = Mathf.Max(0f, margemDaParede);
        velocidadeAproximar = Mathf.Max(1f, velocidadeAproximar);
        velocidadeVoltar = Mathf.Max(1f, velocidadeVoltar);
    }
}