using UnityEngine;

/// <summary>
/// Controlador central do Camera System V2.
/// Alterna terceira/primeira pessoa, mantém o PlayerMove sincronizado e garante
/// que somente a Camera/AudioListener V2 ativos permaneçam ligados.
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

    [Tooltip("Desliga Camera antiga/extras que estejam dentro da mesma raiz do CameraSystemV2.")]
    public bool desativarCamerasExtrasNoMesmoRoot = true;

    [Tooltip("Desliga AudioListeners extras dentro da mesma raiz. Corrige o aviso de 3 listeners.")]
    public bool desativarAudioListenersExtrasNoMesmoRoot = true;

    [Tooltip("Mantém a câmera V2 ativa com a tag MainCamera e remove a tag das câmeras inativas/extras.")]
    public bool gerenciarTagMainCamera = true;

    [Header("Revalidação Inicial")]
    public bool revalidarNosPrimeirosSegundos = true;
    [Min(0.5f)] public float duracaoRevalidacaoInicial = 4f;
    [Min(0.1f)] public float intervaloRevalidacao = 0.5f;

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

    public Transform CameraAtivaTransform
    {
        get
        {
            if (primeiraPessoaAtiva && firstPersonCAM != null)
                return firstPersonCAM.UnityCamera != null ? firstPersonCAM.UnityCamera.transform : firstPersonCAM.transform;

            if (thirdPersonCAM != null)
                return thirdPersonCAM.UnityCamera != null ? thirdPersonCAM.UnityCamera.transform : thirdPersonCAM.transform;

            if (firstPersonCAM != null)
                return firstPersonCAM.UnityCamera != null ? firstPersonCAM.UnityCamera.transform : firstPersonCAM.transform;

            return null;
        }
    }

    public UnityEngine.Camera CameraAtiva
    {
        get
        {
            if (primeiraPessoaAtiva && firstPersonCAM != null)
                return firstPersonCAM.UnityCamera;

            return thirdPersonCAM != null ? thirdPersonCAM.UnityCamera : null;
        }
    }

    private void Awake()
    {
        tempoInicio = Time.unscaledTime;
        ResolverReferencias();
        AplicarEstadoInicial();
        SanearCameraEAudioNoMesmoRoot();
    }

    private void Start()
    {
        ResolverReferencias();
        AplicarEstadoInicial();

        if (posicionarTerceiraPessoaAntesPrimeiroFrame && !primeiraPessoaAtiva && thirdPersonCAM != null)
            thirdPersonCAM.ForcarAtualizacaoImediata();

        SanearCameraEAudioNoMesmoRoot();
    }

    private void Update()
    {
        ResolverReferenciasLeve();

        if (revalidarNosPrimeirosSegundos &&
            Time.unscaledTime - tempoInicio <= duracaoRevalidacaoInicial &&
            Time.unscaledTime >= proximaRevalidacao)
        {
            proximaRevalidacao = Time.unscaledTime + Mathf.Max(0.1f, intervaloRevalidacao);
            SanearCameraEAudioNoMesmoRoot();
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
        ResolverReferenciasLeve();

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
        ResolverReferencias();
        AplicarAtivacao();

        if (!primeiraPessoaAtiva && thirdPersonCAM != null)
            thirdPersonCAM.ForcarAtualizacaoImediata();

        SanearCameraEAudioNoMesmoRoot();
    }

    public int ContarCamerasAtivasNoMesmoRoot()
    {
        Transform raiz = transform.root;
        UnityEngine.Camera[] cameras = raiz.GetComponentsInChildren<UnityEngine.Camera>(true);
        int total = 0;

        for (int i = 0; i < cameras.Length; i++)
        {
            if (cameras[i] != null && cameras[i].enabled && cameras[i].gameObject.activeInHierarchy)
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
            if (listeners[i] != null && listeners[i].enabled && listeners[i].gameObject.activeInHierarchy)
                total++;
        }

        return total;
    }

    private void ResolverReferencias()
    {
        ResolverReferenciasLeve();

        if (thirdPersonCAM != null && playerTarget != null)
            thirdPersonCAM.target = playerTarget;

        if (firstPersonCAM != null)
        {
            if (playerTarget != null)
                firstPersonCAM.corpoPersonagem = playerTarget;

            if (pov != null)
                firstPersonCAM.pontoPOV = pov;
        }

        AtualizarListenersCache();
    }

    private void ResolverReferenciasLeve()
    {
        if (thirdPersonCAM == null)
            thirdPersonCAM = Object.FindFirstObjectByType<Camera3Person>(FindObjectsInactive.Include);

        if (firstPersonCAM == null)
            firstPersonCAM = Object.FindFirstObjectByType<Camera1Person>(FindObjectsInactive.Include);

        if (playerMove == null)
            playerMove = Object.FindFirstObjectByType<PlayerMove>(FindObjectsInactive.Include);

        if (playerTarget == null && playerMove != null)
            playerTarget = playerMove.transform;

        if (thirdPersonCAM != null && thirdPersonCAM.target == null && playerTarget != null)
            thirdPersonCAM.target = playerTarget;

        if (firstPersonCAM != null)
        {
            if (firstPersonCAM.corpoPersonagem == null && playerTarget != null)
                firstPersonCAM.corpoPersonagem = playerTarget;

            if (firstPersonCAM.pontoPOV == null && pov != null)
                firstPersonCAM.pontoPOV = pov;
        }
    }

    private void AtualizarListenersCache()
    {
        thirdListener = thirdPersonCAM != null && thirdPersonCAM.UnityCamera != null
            ? thirdPersonCAM.UnityCamera.GetComponent<AudioListener>()
            : null;

        firstListener = firstPersonCAM != null && firstPersonCAM.UnityCamera != null
            ? firstPersonCAM.UnityCamera.GetComponent<AudioListener>()
            : null;
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

        AtualizarAudioListeners();
        AtualizarTagsCamera();
        AtualizarCameraTransformPlayer();
        SanearCameraEAudioNoMesmoRoot();
    }

    private void AtualizarAudioListeners()
    {
        if (!controlarAudioListeners)
            return;

        if (thirdListener == null || firstListener == null)
            AtualizarListenersCache();

        if (thirdListener != null && thirdListener.enabled != !primeiraPessoaAtiva)
            thirdListener.enabled = !primeiraPessoaAtiva;

        if (firstListener != null && firstListener.enabled != primeiraPessoaAtiva)
            firstListener.enabled = primeiraPessoaAtiva;
    }

    private void AtualizarTagsCamera()
    {
        if (!gerenciarTagMainCamera)
            return;

        UnityEngine.Camera ativa = CameraAtiva;
        UnityEngine.Camera third = thirdPersonCAM != null ? thirdPersonCAM.UnityCamera : null;
        UnityEngine.Camera first = firstPersonCAM != null ? firstPersonCAM.UnityCamera : null;

        if (third != null)
            third.gameObject.tag = third == ativa ? "MainCamera" : "Untagged";

        if (first != null)
            first.gameObject.tag = first == ativa ? "MainCamera" : "Untagged";
    }

    private void SanearCameraEAudioNoMesmoRoot()
    {
        if (thirdPersonCAM == null && firstPersonCAM == null)
            return;

        Transform raiz = transform.root;
        UnityEngine.Camera third = thirdPersonCAM != null ? thirdPersonCAM.UnityCamera : null;
        UnityEngine.Camera first = firstPersonCAM != null ? firstPersonCAM.UnityCamera : null;
        UnityEngine.Camera ativa = CameraAtiva;

        if (desativarCamerasExtrasNoMesmoRoot || desativarCameraAntigaSeEncontrar)
        {
            UnityEngine.Camera[] cameras = raiz.GetComponentsInChildren<UnityEngine.Camera>(true);
            for (int i = 0; i < cameras.Length; i++)
            {
                UnityEngine.Camera cam = cameras[i];
                if (cam == null)
                    continue;

                bool cameraV2 = cam == third || cam == first;
                bool deveAtivar = cameraV2 && cam == ativa;

                if (cam.enabled != deveAtivar)
                {
                    if (!cameraV2 && cam.enabled)
                    {
                        camerasExtrasDesativadas++;
                        ultimaCameraExtraDesativada = cam.name;
                    }

                    cam.enabled = deveAtivar;
                }

                if (!cameraV2 && gerenciarTagMainCamera && cam.CompareTag("MainCamera"))
                    cam.gameObject.tag = "Untagged";
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

                bool listenerAtivo = ativa != null && listener.gameObject == ativa.gameObject;
                if (listener.enabled != listenerAtivo)
                {
                    if (!listenerAtivo && listener.enabled)
                        listenersExtrasDesativados++;

                    listener.enabled = listenerAtivo;
                }
            }
        }

        AtualizarTagsCamera();
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
