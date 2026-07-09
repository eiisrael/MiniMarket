using UnityEngine;

/// <summary>
/// Ajusta automaticamente o PlayerObjectGrabberHardcore em runtime sem depender do Inspector.
///
/// Ajuste importante de câmera:
/// - desativa a assistência lateral que movia diretamente camera.transform.position;
/// - essa assistência usava camera.right em LateUpdate e podia causar pulo/eixo estranho
///   quando o jogador virava o mouse rápido;
/// - o objeto continua podendo ser selecionado/pego normalmente.
/// </summary>
[DefaultExecutionOrder(31000)]
public class MiniMarketGrabberRuntimeStabilizer : MonoBehaviour
{
    [Header("Auto")]
    public bool procurarAutomaticamente = true;
    public float intervaloBusca = 1f;

    [Header("Primeira Pessoa")]
    public bool centralizarObjetoNaPrimeiraPessoa = true;
    public Vector2 offsetPrimeiraPessoaCentral = new Vector2(0f, 0.08f);
    public float distanciaPrimeiraPessoa = 2.65f;

    [Header("Selecao")]
    public float raioSelecao = 0.28f;
    public float raioViewportFallback = 0.22f;
    public float tempoMemoriaSelecao = 0.25f;

    [Header("Camera Assist")]
    [Tooltip("Desligado por padrão. Não mover a Main Camera diretamente pelo sistema de pegar objetos.")]
    public bool aplicarAssistenciaNaPrimeiraPessoa = false;

    [Tooltip("Desligado por padrão. Evita deslocamento lateral da câmera quando há objeto selecionado/segurando.")]
    public bool cameraAssistNaTerceiraPessoa = false;

    public float deslocamentoCameraEsquerda = 0f;

    private static MiniMarketGrabberRuntimeStabilizer instancia;
    private PlayerObjectGrabberHardcore[] grabbers;
    private float proximaBusca;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void CriarAutomaticamente()
    {
        if (instancia != null)
            return;

        GameObject go = new GameObject("MiniMarket_GrabberRuntimeStabilizer");
        DontDestroyOnLoad(go);
        instancia = go.AddComponent<MiniMarketGrabberRuntimeStabilizer>();
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
        BuscarGrabbers();
        AplicarAjustes();
    }

    private void LateUpdate()
    {
        if (!procurarAutomaticamente)
            return;

        if (Time.unscaledTime >= proximaBusca)
        {
            proximaBusca = Time.unscaledTime + Mathf.Max(0.25f, intervaloBusca);
            BuscarGrabbers();
            AplicarAjustes();
        }
    }

    private void BuscarGrabbers()
    {
        grabbers = Object.FindObjectsByType<PlayerObjectGrabberHardcore>(FindObjectsInactive.Include, FindObjectsSortMode.None);
    }

    private void AplicarAjustes()
    {
        if (grabbers == null)
            return;

        for (int i = 0; i < grabbers.Length; i++)
        {
            PlayerObjectGrabberHardcore grabber = grabbers[i];
            if (grabber == null)
                continue;

            if (centralizarObjetoNaPrimeiraPessoa)
                grabber.offsetSegurandoPrimeiraPessoa = offsetPrimeiraPessoaCentral;

            grabber.distanciaSegurandoPrimeiraPessoa = distanciaPrimeiraPessoa;
            grabber.raioSelecao = raioSelecao;
            grabber.raioViewportFallback = raioViewportFallback;
            grabber.tempoMemoriaSelecao = tempoMemoriaSelecao;

            // Correção principal: o grabber não deve mais empurrar a Main Camera.
            grabber.aplicarAssistenciaCameraNaPrimeiraPessoa = aplicarAssistenciaNaPrimeiraPessoa;
            grabber.puxarCameraParaEsquerdaAoMirarOuSegurar = cameraAssistNaTerceiraPessoa;
            grabber.deslocamentoCameraEsquerda = deslocamentoCameraEsquerda;
        }
    }
}
