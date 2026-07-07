using System.Collections.Generic;
using UnityEngine;

public class PlayerObjectGrabberHardcore : MonoBehaviour
{
    [Header("Referências")]
    public Camera cameraDoPlayer;

    [Tooltip("Arraste aqui o Character01 para evitar que o raycast selecione o próprio personagem.")]
    public Transform raizDoPlayer;

    [Tooltip("Opcional. Pode arrastar o script CrosshairAim aqui.")]
    public CrosshairAim crosshairAim;

    [Header("Input")]
    [Tooltip("Botão da mira. 0 = esquerdo, 1 = direito, 2 = meio.")]
    [Range(0, 2)]
    public int botaoDeMira = 1;

    [Tooltip("Botão de pegar. 0 = esquerdo, 1 = direito, 2 = meio.")]
    [Range(0, 2)]
    public int botaoDePegar = 0;

    [Header("Seleção")]
    [Tooltip("Só permite selecionar objetos enquanto segura o botão direito/mira.")]
    public bool selecionarApenasEnquantoMira = true;

    [Tooltip("Distância máxima para selecionar um objeto.")]
    [Min(0.1f)]
    public float distanciaSelecao = 5f;

    [Tooltip("Layers que podem ser selecionadas. Pode deixar Everything no começo.")]
    public LayerMask layersSelecionaveis = ~0;

    [Header("Objetos Permitidos")]
    [Tooltip("Se ativado, apenas objetos colocados na lista abaixo podem ser pegos.")]
    public bool usarListaDeObjetosPermitidos = true;

    [Tooltip("Arraste aqui Box_3, caixas, produtos e objetos que poderão ser pegos.")]
    public List<GameObject> objetosPermitidos = new List<GameObject>();

    [Tooltip("Se o objeto da lista não tiver GrabbableObjectHardcore, o script adiciona automaticamente.")]
    public bool adicionarComponenteAutomaticamenteNosPermitidos = true;

    [Tooltip("Se ativado, qualquer objeto com GrabbableObjectHardcore também poderá ser pego, mesmo fora da lista.")]
    public bool permitirQualquerObjetoComGrabbable = false;

    [Header("Pegar / Mover")]
    [Tooltip("Distância em frente da câmera onde o objeto fica enquanto está sendo segurado.")]
    [Min(0.3f)]
    public float distanciaSegurando = 2.2f;

    [Tooltip("Offset horizontal/vertical do objeto segurado em relação à câmera.")]
    public Vector2 offsetSegurando = new Vector2(0f, -0.15f);

    [Tooltip("Velocidade com que o objeto acompanha a câmera.")]
    [Min(0.1f)]
    public float velocidadeMoverObjeto = 18f;

    [Tooltip("Se ativado, limita a distância enquanto segura.")]
    public bool limitarDistanciaSegurando = true;

    [Min(0.3f)]
    public float distanciaMinimaSegurando = 1.1f;

    [Min(0.3f)]
    public float distanciaMaximaSegurando = 3.5f;

    [Header("Rotação do Objeto")]
    public bool manterRotacaoOriginalAoPegar = true;

    [Tooltip("Se manterRotacaoOriginalAoPegar estiver desligado, o objeto olha para a câmera.")]
    public bool alinharObjetoComCamera = false;

    [Min(0.1f)]
    public float velocidadeRotacaoObjeto = 12f;

    [Header("Debug")]
    public bool desenharRaycast = true;

    private GrabbableObjectHardcore objetoSelecionado;
    private GrabbableObjectHardcore objetoPegando;

    private Rigidbody rbPegando;
    private Quaternion rotacaoOriginalObjeto;
    private float distanciaAtualSegurando;

    private void Awake()
    {
        if (cameraDoPlayer == null)
            cameraDoPlayer = Camera.main;

        if (crosshairAim == null && cameraDoPlayer != null)
            crosshairAim = cameraDoPlayer.GetComponent<CrosshairAim>();

        distanciaAtualSegurando = distanciaSegurando;
    }

    private void Update()
    {
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
            if (!segurandoPegar)
            {
                SoltarObjeto();
            }
        }
    }

    private void FixedUpdate()
    {
        if (objetoPegando == null)
            return;

        MoverObjetoPegando();
    }

    private bool EstaSegurandoMira()
    {
        if (crosshairAim != null)
            return crosshairAim.EstaMirando || Input.GetMouseButton(botaoDeMira);

        return Input.GetMouseButton(botaoDeMira);
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

        RaycastHit[] hits = Physics.RaycastAll(
            ray,
            distanciaSelecao,
            layersSelecionaveis,
            QueryTriggerInteraction.Ignore
        );

        if (hits == null || hits.Length == 0)
            return null;

        System.Array.Sort(hits, CompararHitsPorDistancia);

        for (int i = 0; i < hits.Length; i++)
        {
            RaycastHit hit = hits[i];

            if (hit.collider == null)
                continue;

            if (raizDoPlayer != null && hit.collider.transform.IsChildOf(raizDoPlayer))
                continue;

            GrabbableObjectHardcore grabbable = ResolverGrabbable(hit.collider);

            if (grabbable == null)
                continue;

            if (!grabbable.podeSerPego)
                continue;

            return grabbable;
        }

        return null;
    }

    private int CompararHitsPorDistancia(RaycastHit a, RaycastHit b)
    {
        return a.distance.CompareTo(b.distance);
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
        if (objeto == null)
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
        if (collider == null)
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

    private void PegarObjeto(GrabbableObjectHardcore objeto)
    {
        if (objeto == null)
            return;

        objetoPegando = objeto;
        objetoSelecionado = null;

        objetoPegando.ComecarPegar();

        rbPegando = objetoPegando.RigidbodyDoObjeto;
        rotacaoOriginalObjeto = objetoPegando.transform.rotation;

        distanciaAtualSegurando = distanciaSegurando;

        if (limitarDistanciaSegurando)
        {
            distanciaAtualSegurando = Mathf.Clamp(
                distanciaAtualSegurando,
                distanciaMinimaSegurando,
                distanciaMaximaSegurando
            );
        }
    }

    private void MoverObjetoPegando()
    {
        if (objetoPegando == null)
            return;

        Vector3 destino = CalcularPontoSegurando();

        float suavizacao = CalcularSuavizacao(
            velocidadeMoverObjeto,
            Time.fixedDeltaTime
        );

        if (rbPegando != null)
        {
            Vector3 novaPosicao = Vector3.Lerp(
                rbPegando.position,
                destino,
                suavizacao
            );

            rbPegando.MovePosition(novaPosicao);

            Quaternion rotacaoAlvo = CalcularRotacaoObjeto();

            Quaternion novaRotacao = Quaternion.Slerp(
                rbPegando.rotation,
                rotacaoAlvo,
                CalcularSuavizacao(velocidadeRotacaoObjeto, Time.fixedDeltaTime)
            );

            rbPegando.MoveRotation(novaRotacao);
        }
        else
        {
            objetoPegando.transform.position = Vector3.Lerp(
                objetoPegando.transform.position,
                destino,
                suavizacao
            );

            objetoPegando.transform.rotation = Quaternion.Slerp(
                objetoPegando.transform.rotation,
                CalcularRotacaoObjeto(),
                CalcularSuavizacao(velocidadeRotacaoObjeto, Time.fixedDeltaTime)
            );
        }
    }

    private Vector3 CalcularPontoSegurando()
    {
        Transform cam = cameraDoPlayer.transform;

        return cam.position
            + cam.forward * distanciaAtualSegurando
            + cam.right * offsetSegurando.x
            + cam.up * offsetSegurando.y;
    }

    private Quaternion CalcularRotacaoObjeto()
    {
        if (manterRotacaoOriginalAoPegar)
            return rotacaoOriginalObjeto;

        if (alinharObjetoComCamera)
            return Quaternion.LookRotation(cameraDoPlayer.transform.forward, Vector3.up);

        return objetoPegando.transform.rotation;
    }

    private void SoltarObjeto()
    {
        if (objetoPegando == null)
            return;

        objetoPegando.Soltar();

        objetoPegando = null;
        rbPegando = null;
    }

    private void LimparSelecao()
    {
        if (objetoSelecionado == null)
            return;

        objetoSelecionado.Selecionar(false);
        objetoSelecionado = null;
    }

    private float CalcularSuavizacao(float velocidade, float deltaTime)
    {
        return 1f - Mathf.Exp(-velocidade * deltaTime);
    }

    private void OnDrawGizmos()
    {
        if (!desenharRaycast)
            return;

        if (cameraDoPlayer == null)
            return;

        Gizmos.color = Color.green;

        Ray ray = cameraDoPlayer.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        Gizmos.DrawLine(ray.origin, ray.origin + ray.direction * distanciaSelecao);
    }
}