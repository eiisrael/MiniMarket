using UnityEngine;

/// <summary>
/// Controlador central do Camera System V2.
/// Alterna terceira/primeira pessoa e garante que a câmera renderizada seja a Camera
/// existente no mesmo GameObject de Camera3Person/Camera1Person.
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(19900)]
public class CameraV2Controller : MonoBehaviour
{
    [Header("Referências")]
    public Camera3Person thirdPersonCAM;
    public Camera1Person firstPersonCAM;
    public Transform playerTarget;
    public Transform pov;
    public PlayerMove playerMove;

    [Header("Input")]
    public bool usarBotaoDireitoParaPrimeiraPessoa = true;
    [Range(0, 2)] public int botaoPrimeiraPessoa = 1;
    public bool voltarParaTerceiraAoSoltar = true;
    public KeyCode teclaAlternar = KeyCode.None;

    [Header("Estado Inicial")]
    public bool iniciarEmTerceiraPessoa = true;
    public bool sincronizarYawAoTrocar = true;
    public bool posicionarTerceiraPessoaAntesPrimeiroFrame = true;

    [Header("Movimento do Player")]
    public bool atualizarCameraTransformDoPlayer = true;

    [Header("Câmeras e Áudio")]
    public bool controlarAudioListeners = true;
    public bool desativarCamerasExtrasNoMesmoRoot = true;
    public bool desativarAudioListenersExtrasNoMesmoRoot = true;
    public bool gerenciarTagMainCamera = true;

    [Header("Revalidação Inicial")]
    public bool revalidarNosPrimeirosSegundos = true;
    [Min(0.5f)] public float duracaoRevalidacaoInicial = 5f;
    [Min(0.05f)] public float intervaloRevalidacao = 0.15f;
    public bool forcarPoseTerceiraDuranteRevalidacao = true;

    [Header("Segurança")]
    public bool desativarCameraAntigaSeEncontrar = true;
    public bool ignorarInputComMenuAberto = true;
    public bool logarEventos = false;

    private bool primeiraPessoaAtiva;
    private bool ultimoBotao;
    private float tempoInicio;
    private float proximaRevalidacao;
    private AudioListener thirdListener;
    private AudioListener firstListener;
    private string ultimaCameraExtraDesativada = string.Empty;
    private int camerasExtrasDesativadas;
    private int listenersExtrasDesativados;

    public bool PrimeiraPessoaAtiva => primeiraPessoaAtiva;
    public int CamerasExtrasDesativadas => camerasExtrasDesativadas;
    public int ListenersExtrasDesativados => listenersExtrasDesativados;
    public string UltimaCameraExtraDesativada => ultimaCameraExtraDesativada;

    public UnityEngine.Camera ThirdCameraLocal => ObterCameraLocal(thirdPersonCAM);
    public UnityEngine.Camera FirstCameraLocal => ObterCameraLocal(firstPersonCAM);

    public Transform CameraAtivaTransform
    {
        get
        {
            UnityEngine.Camera ativa = CameraAtiva;
            if (ativa != null)
                return ativa.transform;

            if (primeiraPessoaAtiva && firstPersonCAM != null)
                return firstPersonCAM.transform;

            return thirdPersonCAM != null ? thirdPersonCAM.transform : null;
        }
    }

    public UnityEngine.Camera CameraAtiva => primeiraPessoaAtiva ? FirstCameraLocal : ThirdCameraLocal;

    private void Awake()
    {
        tempoInicio = Time.unscaledTime;
        ResolverReferencias(true);
        AplicarEstadoInicial();
        SanearCameraEAudioNoMesmoRoot();

        if (!primeiraPessoaAtiva && posicionarTerceiraPessoaAntesPrimeiroFrame && thirdPersonCAM != null)
            thirdPersonCAM.ForcarAtualizacaoImediata();
    }

    private void Start()
    {
        ResolverReferencias(true);
        AplicarEstadoInicial();
        SanearCameraEAudioNoMesmoRoot();

        if (!primeiraPessoaAtiva && posicionarTerceiraPessoaAntesPrimeiroFrame && thirdPersonCAM != null)
            thirdPersonCAM.ForcarAtualizacaoImediata();
    }

    private void Update()
    {
        ResolverReferencias(false);

        if (revalidarNosPrimeirosSegundos &&
            Time.unscaledTime - tempoInicio <= duracaoRevalidacaoInicial &&
            Time.unscaledTime >= proximaRevalidacao)
        {
            proximaRevalidacao = Time.unscaledTime + Mathf.Max(0.05f, intervaloRevalidacao);
            SanearCameraEAudioNoMesmoRoot();

            if (forcarPoseTerceiraDuranteRevalidacao &&
                !primeiraPessoaAtiva &&
                thirdPersonCAM != null &&
                thirdPersonCAM.cameraAtiva)
            {
                thirdPersonCAM.ForcarAtualizacaoImediata();
            }
        }

        if (ignorarInputComMenuAberto && CameraV2MenuInputBlocker.MenuAberto)
        {
            ultimoBotao = false;
            return;
        }

        if (usarBotaoDireitoParaPrimeiraPessoa)
        {
            bool pressionado = Input.GetMouseButton(botaoPrimeiraPessoa);
            if (pressionado != ultimoBotao)
            {
                ultimoBotao = pressionado;

                if (pressionado)
                    SetPrimeiraPessoa(true);
                else if (voltarParaTerceiraAoSoltar)
                    SetPrimeiraPessoa(false);
            }
        }

        if (teclaAlternar != KeyCode.None && Input.GetKeyDown(teclaAlternar))
            SetPrimeiraPessoa(!primeiraPessoaAtiva);
    }

    private void LateUpdate()
    {
        AtualizarCameraTransformPlayer();
    }

    public void SetPrimeiraPessoa(bool ativa)
    {
        ResolverReferencias(false);

        if (primeiraPessoaAtiva == ativa)
        {
            AplicarAtivacao();
            return;
        }

        if (sincronizarYawAoTrocar)
            SincronizarAngulos(ativa);

        primeiraPessoaAtiva = ativa;
        AplicarAtivacao();

        if (!ativa && posicionarTerceiraPessoaAntesPrimeiroFrame && thirdPersonCAM != null)
            thirdPersonCAM.ForcarAtualizacaoImediata();

        if (logarEventos)
        {
            MiniMarketUpgradeLogger.Log(
                "CameraV2",
                ativa ? "FirstPersonCAM ON" : "ThirdPersonCAM ON",
                "Sistema V2 alternou câmera.",
                "camera-v2-switch",
                0.5f
            );
        }
    }

    [ContextMenu("Camera V2/Reparar câmeras e AudioListeners agora")]
    public void RepararAgora()
    {
        ResolverReferencias(true);
        AplicarAtivacao();
        SanearCameraEAudioNoMesmoRoot();

        if (!primeiraPessoaAtiva && thirdPersonCAM != null)
            thirdPersonCAM.ForcarAtualizacaoImediata();
    }

    public int ContarCamerasAtivasNoMesmoRoot()
    {
        Transform raiz = transform.root;
        UnityEngine.Camera[] cameras = raiz.GetComponentsInChildren<UnityEngine.Camera>(true);
        int total = 0;

        for (int i = 0; i < cameras.Length; i++)
        {
            UnityEngine.Camera cam = cameras[i];
            if (cam != null && cam.enabled && cam.gameObject.activeInHierarchy)
                total++;
        }

        return total;
    }

    public int ContarAudioListenersAtivosNoMesmoRoot()
    {
        Transform raiz = transform.root;
        AudioListener[] listeners = raiz.GetComponentsInChildren<AudioListener>(true);
        int total = 0;

        for (int i = 0; i < listeners.Length; i++)
        {
            AudioListener listener = listeners[i];
            if (listener != null && listener.enabled && listener.gameObject.activeInHierarchy)
                total++;
        }

        return total;
    }

    private void ResolverReferencias(bool forcar)
    {
        if (thirdPersonCAM == null || forcar)
        {
            Camera3Person encontrada = EncontrarCamera3PersonNaRaiz();
            if (encontrada != null)
                thirdPersonCAM = encontrada;
        }

        if (firstPersonCAM == null || forcar)
        {
            Camera1Person encontrada = EncontrarCamera1PersonNaRaiz();
            if (encontrada != null)
                firstPersonCAM = encontrada;
        }

        if (playerMove == null)
            playerMove = Object.FindAnyObjectByType<PlayerMove>(FindObjectsInactive.Include);

        if (playerTarget == null && playerMove != null)
            playerTarget = playerMove.transform;

        if (thirdPersonCAM != null && playerTarget != null)
            thirdPersonCAM.DefinirTarget(playerTarget, false);

        if (firstPersonCAM != null)
        {
            if (playerTarget != null)
                firstPersonCAM.corpoPersonagem = playerTarget;

            if (pov != null)
                firstPersonCAM.pontoPOV = pov;
        }

        AtualizarListenersCache();
    }

    private Camera3Person EncontrarCamera3PersonNaRaiz()
    {
        Camera3Person[] locais = transform.root.GetComponentsInChildren<Camera3Person>(true);
        if (locais.Length > 0)
            return locais[0];

        return Object.FindAnyObjectByType<Camera3Person>(FindObjectsInactive.Include);
    }

    private Camera1Person EncontrarCamera1PersonNaRaiz()
    {
        Camera1Person[] locais = transform.root.GetComponentsInChildren<Camera1Person>(true);
        if (locais.Length > 0)
            return locais[0];

        return Object.FindAnyObjectByType<Camera1Person>(FindObjectsInactive.Include);
    }

    private UnityEngine.Camera ObterCameraLocal(Component componente)
    {
        if (componente == null)
            return null;

        return componente.GetComponent<UnityEngine.Camera>();
    }

    private void AtualizarListenersCache()
    {
        UnityEngine.Camera third = ThirdCameraLocal;
        UnityEngine.Camera first = FirstCameraLocal;

        thirdListener = third != null ? third.GetComponent<AudioListener>() : null;
        firstListener = first != null ? first.GetComponent<AudioListener>() : null;
    }

    private void AplicarEstadoInicial()
    {
        primeiraPessoaAtiva = !iniciarEmTerceiraPessoa;
        AplicarAtivacao();
    }

    private void AplicarAtivacao()
    {
        if (thirdPersonCAM != null)
            thirdPersonCAM.SetAtiva(!primeiraPessoaAtiva);

        if (firstPersonCAM != null)
            firstPersonCAM.SetAtiva(primeiraPessoaAtiva);

        SanearCameraEAudioNoMesmoRoot();
        AtualizarCameraTransformPlayer();
    }

    private void SanearCameraEAudioNoMesmoRoot()
    {
        if (thirdPersonCAM == null && firstPersonCAM == null)
            return;

        Transform raiz = transform.root;
        UnityEngine.Camera third = ThirdCameraLocal;
        UnityEngine.Camera first = FirstCameraLocal;
        UnityEngine.Camera ativa = CameraAtiva;

        UnityEngine.Camera[] cameras = raiz.GetComponentsInChildren<UnityEngine.Camera>(true);
        for (int i = 0; i < cameras.Length; i++)
        {
            UnityEngine.Camera cam = cameras[i];
            if (cam == null)
                continue;

            bool cameraV2 = cam == third || cam == first;
            bool deveAtivar = cameraV2 && cam == ativa;

            if ((desativarCamerasExtrasNoMesmoRoot || desativarCameraAntigaSeEncontrar || cameraV2) &&
                cam.enabled != deveAtivar)
            {
                if (!cameraV2 && cam.enabled)
                {
                    camerasExtrasDesativadas++;
                    ultimaCameraExtraDesativada = cam.name;
                }

                cam.enabled = deveAtivar;
            }

            if (gerenciarTagMainCamera)
            {
                if (cam == ativa)
                {
                    if (!cam.CompareTag("MainCamera"))
                        cam.gameObject.tag = "MainCamera";
                }
                else if (cam.CompareTag("MainCamera"))
                {
                    cam.gameObject.tag = "Untagged";
                }
            }
        }

        if (desativarAudioListenersExtrasNoMesmoRoot || controlarAudioListeners)
        {
            AudioListener[] listeners = raiz.GetComponentsInChildren<AudioListener>(true);
            for (int i = 0; i < listeners.Length; i++)
            {
                AudioListener listener = listeners[i];
                if (listener == null)
                    continue;

                bool deveAtivar = ativa != null && listener.gameObject == ativa.gameObject;
                if (listener.enabled != deveAtivar)
                {
                    if (!deveAtivar && listener.enabled)
                        listenersExtrasDesativados++;

                    listener.enabled = deveAtivar;
                }
            }
        }

        AtualizarListenersCache();
    }

    private void AtualizarCameraTransformPlayer()
    {
        if (!atualizarCameraTransformDoPlayer || playerMove == null)
            return;

        Transform ativa = CameraAtivaTransform;
        if (ativa != null && playerMove.cameraTransform != ativa)
            playerMove.cameraTransform = ativa;
    }

    private void SincronizarAngulos(bool indoParaPrimeiraPessoa)
    {
        if (thirdPersonCAM == null || firstPersonCAM == null)
            return;

        if (indoParaPrimeiraPessoa)
            firstPersonCAM.DefinirAngulos(thirdPersonCAM.YawAtual, thirdPersonCAM.PitchAtual);
        else
            thirdPersonCAM.DefinirAngulos(firstPersonCAM.YawAtual, firstPersonCAM.PitchAtual);
    }
}
