using UnityEngine;

/// <summary>
/// Alterna entre camera normal/terceira pessoa e uma camera de primeira pessoa.
///
/// Modo seguro:
/// - Nao desativa o GameObject da Main Camera, apenas o componente Camera/AudioListener.
/// - Assim, scripts de camera/mouse que estiverem na Main Camera continuam rodando.
/// - A camera de primeira pessoa copia a rotacao da camera normal e segue um ponto nos olhos/cabeca.
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

    [Tooltip("Se um menu estiver aberto, evite alternar camera. Arraste CanvasGroup do menu aqui se quiser bloquear.")]
    public CanvasGroup[] menusQueBloqueiamAlternancia;

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
        {
            Debug.Log("[MiniMarketCameraPerspectiveSwitcher] Camera ativa: " + (usandoPrimeiraPessoa ? "Primeira Pessoa" : "Normal"));
        }
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

        AtualizarCameraPrimeiraPessoa();
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

    private bool AlgumMenuBloqueando()
    {
        if (menusQueBloqueiamAlternancia == null)
            return false;

        for (int i = 0; i < menusQueBloqueiamAlternancia.Length; i++)
        {
            CanvasGroup menu = menusQueBloqueiamAlternancia[i];
            if (menu == null)
                continue;

            if (menu.gameObject.activeInHierarchy && menu.alpha > 0.05f && menu.blocksRaycasts)
                return true;
        }

        return false;
    }
}
