using UnityEngine;

/// <summary>
/// Alterna entre camera normal/terceira pessoa e uma camera de primeira pessoa.
///
/// Modo seguro:
/// - Nao desativa o GameObject da Main Camera, apenas o componente Camera/AudioListener.
/// - Assim, scripts de camera/mouse que estiverem na Main Camera continuam rodando.
/// - A camera de primeira pessoa copia a rotacao da camera normal e segue um ponto nos olhos/cabeca.
/// - Em primeira pessoa, pode rotacionar o personagem junto com a camera e ocultar renderers para nao ver a cabeca por dentro.
/// </summary>
[DefaultExecutionOrder(20000)]
public class MiniMarketCameraPerspectiveSwitcher : MonoBehaviour
{
    [Header("Cameras")]
    [Tooltip("Camera normal/terceira pessoa. Geralmente a Main Camera atual.")]
    public Camera cameraNormal;

    [Tooltip("Nova camera em primeira pessoa. Crie uma Camera e arraste aqui.")]
    public Camera cameraPrimeiraPessoa;

    [Header("Ponto de Primeira Pessoa")]
    [Tooltip("Empty no personagem, posicionado nos olhos/cabeca. A camera de primeira pessoa segue este ponto.")]
    public Transform pontoPrimeiraPessoa;

    [Tooltip("Usado se pontoPrimeiraPessoa estiver vazio. Arraste o Character 01 aqui.")]
    public Transform alvoFallback;

    [Tooltip("Offset local usado quando seguir o alvoFallback.")]
    public Vector3 offsetLocalFallback = new Vector3(0f, 1.68f, 0.22f);

    [Header("Input")]
    public KeyCode teclaAlternar = KeyCode.Tab;

    [Tooltip("Comeca renderizando a camera normal.")]
    public bool iniciarNaCameraNormal = true;

    [Header("Comportamento")]
    [Tooltip("Copia a rotacao da camera normal para a camera de primeira pessoa. Recomendado deixar ligado.")]
    public bool copiarRotacaoDaCameraNormal = true;

    [Tooltip("Quando alternar, copia FOV da camera normal para a primeira pessoa.")]
    public bool copiarFOVDaCameraNormal = true;

    [Tooltip("Mantem o cursor travado durante a troca de camera.")]
    public bool manterCursorTravado = true;

    [Header("Primeira Pessoa - Personagem")]
    [Tooltip("Objeto principal do personagem que deve virar junto com a camera. Se vazio, usa Alvo Fallback.")]
    public Transform personagemParaRotacionar;

    [Tooltip("Se ligado, em primeira pessoa o personagem vira para o mesmo Yaw da camera.")]
    public bool rotacionarPersonagemComCamera = true;

    [Tooltip("Velocidade de suavizacao para o personagem acompanhar a camera.")]
    [Min(0.1f)] public float velocidadeRotacaoPersonagem = 18f;

    [Tooltip("Oculta renderers do personagem em primeira pessoa para nao ver cabeca/corpo por dentro.")]
    public bool ocultarRenderersNaPrimeiraPessoa = true;

    [Tooltip("Se vazio, encontra automaticamente renderers dentro do personagem.")]
    public bool encontrarRenderersAutomaticamente = true;

    public Renderer[] renderersDoPersonagem;

    [Header("Bloqueio por Menu")]
    [Tooltip("Se um menu estiver aberto, evita alternar camera.")]
    public bool bloquearAlternanciaComMenuAberto = true;

    [Tooltip("Arraste CanvasGroup do Menu aqui se quiser bloquear explicitamente.")]
    public CanvasGroup[] menusQueBloqueiamAlternancia;

    [Tooltip("Procura automaticamente CanvasGroups visiveis com nome contendo 'Menu'. Evita conflito se Menu e Camera usam TAB.")]
    public bool detectarMenusAutomaticamente = true;

    [Header("Debug")]
    public bool logarEventos = true;

    private bool usandoPrimeiraPessoa;
    private AudioListener audioNormal;
    private AudioListener audioPrimeiraPessoa;

    public bool UsandoPrimeiraPessoa => usandoPrimeiraPessoa;

    private void Awake()
    {
        ResolverReferencias();
        usandoPrimeiraPessoa = !iniciarNaCameraNormal;
        AplicarEstadoDasCameras();
    }

    private void Start()
    {
        ResolverReferencias();
        AplicarEstadoDasCameras();
    }

    private void OnDisable()
    {
        AplicarVisibilidadeRenderers(true);
    }

    private void Update()
    {
        if (Input.GetKeyDown(teclaAlternar))
        {
            if (AlgumMenuBloqueando())
                return;

            AlternarCamera();
        }
    }

    private void LateUpdate()
    {
        AtualizarCameraPrimeiraPessoa();
        AtualizarRotacaoPersonagemPrimeiraPessoa();
    }

    public void AlternarCamera()
    {
        usandoPrimeiraPessoa = !usandoPrimeiraPessoa;
        AplicarEstadoDasCameras();

        if (manterCursorTravado)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        if (logarEventos)
            Debug.Log("[MiniMarketCameraPerspectiveSwitcher] Camera ativa: " + (usandoPrimeiraPessoa ? "Primeira Pessoa" : "Normal"));
    }

    public void UsarCameraNormal()
    {
        usandoPrimeiraPessoa = false;
        AplicarEstadoDasCameras();
    }

    public void UsarCameraPrimeiraPessoa()
    {
        usandoPrimeiraPessoa = true;
        AplicarEstadoDasCameras();
    }

    private void ResolverReferencias()
    {
        if (cameraNormal == null)
            cameraNormal = Camera.main;

        if (cameraNormal != null && audioNormal == null)
            audioNormal = cameraNormal.GetComponent<AudioListener>();

        if (cameraPrimeiraPessoa != null && audioPrimeiraPessoa == null)
            audioPrimeiraPessoa = cameraPrimeiraPessoa.GetComponent<AudioListener>();

        if (personagemParaRotacionar == null)
            personagemParaRotacionar = alvoFallback;

        if (encontrarRenderersAutomaticamente && (renderersDoPersonagem == null || renderersDoPersonagem.Length == 0))
        {
            Transform raiz = personagemParaRotacionar != null ? personagemParaRotacionar : alvoFallback;
            if (raiz != null)
                renderersDoPersonagem = raiz.GetComponentsInChildren<Renderer>(true);
        }
    }

    private void AplicarEstadoDasCameras()
    {
        ResolverReferencias();

        if (cameraNormal != null)
            cameraNormal.enabled = !usandoPrimeiraPessoa;

        if (cameraPrimeiraPessoa != null)
            cameraPrimeiraPessoa.enabled = usandoPrimeiraPessoa;

        if (audioNormal != null)
            audioNormal.enabled = !usandoPrimeiraPessoa;

        if (audioPrimeiraPessoa != null)
            audioPrimeiraPessoa.enabled = usandoPrimeiraPessoa;

        AplicarVisibilidadeRenderers(!usandoPrimeiraPessoa || !ocultarRenderersNaPrimeiraPessoa);
        AtualizarCameraPrimeiraPessoa();
        AtualizarRotacaoPersonagemPrimeiraPessoa(true);
    }

    private void AtualizarCameraPrimeiraPessoa()
    {
        if (cameraPrimeiraPessoa == null)
            return;

        Transform camTransform = cameraPrimeiraPessoa.transform;

        if (pontoPrimeiraPessoa != null)
        {
            camTransform.position = pontoPrimeiraPessoa.position;
        }
        else if (alvoFallback != null)
        {
            camTransform.position = alvoFallback.TransformPoint(offsetLocalFallback);
        }

        if (copiarRotacaoDaCameraNormal && cameraNormal != null)
            camTransform.rotation = cameraNormal.transform.rotation;

        if (copiarFOVDaCameraNormal && cameraNormal != null)
            cameraPrimeiraPessoa.fieldOfView = cameraNormal.fieldOfView;
    }

    private void AtualizarRotacaoPersonagemPrimeiraPessoa(bool instantaneo = false)
    {
        if (!usandoPrimeiraPessoa)
            return;

        if (!rotacionarPersonagemComCamera)
            return;

        Transform alvo = personagemParaRotacionar != null ? personagemParaRotacionar : alvoFallback;
        if (alvo == null)
            return;

        Camera referencia = cameraPrimeiraPessoa != null ? cameraPrimeiraPessoa : cameraNormal;
        if (referencia == null)
            return;

        Vector3 forward = referencia.transform.forward;
        forward.y = 0f;

        if (forward.sqrMagnitude < 0.0001f)
            return;

        Quaternion rotacaoAlvo = Quaternion.LookRotation(forward.normalized, Vector3.up);

        if (instantaneo)
        {
            alvo.rotation = rotacaoAlvo;
            return;
        }

        float t = 1f - Mathf.Exp(-velocidadeRotacaoPersonagem * Time.deltaTime);
        alvo.rotation = Quaternion.Slerp(alvo.rotation, rotacaoAlvo, t);
    }

    private void AplicarVisibilidadeRenderers(bool visivel)
    {
        if (renderersDoPersonagem == null)
            return;

        for (int i = 0; i < renderersDoPersonagem.Length; i++)
        {
            if (renderersDoPersonagem[i] != null)
                renderersDoPersonagem[i].enabled = visivel;
        }
    }

    private bool AlgumMenuBloqueando()
    {
        if (!bloquearAlternanciaComMenuAberto)
            return false;

        if (menusQueBloqueiamAlternancia != null)
        {
            for (int i = 0; i < menusQueBloqueiamAlternancia.Length; i++)
            {
                if (CanvasGroupEstaAberto(menusQueBloqueiamAlternancia[i]))
                    return true;
            }
        }

        if (!detectarMenusAutomaticamente)
            return false;

        CanvasGroup[] grupos = FindObjectsOfType<CanvasGroup>(true);
        for (int i = 0; i < grupos.Length; i++)
        {
            CanvasGroup grupo = grupos[i];
            if (grupo == null)
                continue;

            string nome = grupo.gameObject.name.ToLowerInvariant();
            if (!nome.Contains("menu"))
                continue;

            if (CanvasGroupEstaAberto(grupo))
                return true;
        }

        return false;
    }

    private bool CanvasGroupEstaAberto(CanvasGroup grupo)
    {
        if (grupo == null)
            return false;

        return grupo.gameObject.activeInHierarchy && grupo.alpha > 0.05f && grupo.blocksRaycasts;
    }
}
