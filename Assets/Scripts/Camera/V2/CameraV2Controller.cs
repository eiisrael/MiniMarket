using UnityEngine;

/// <summary>
/// Controlador central do Camera System V2.
/// Alterna ThirdPersonCAM/FirstPersonCAM, controla AudioListener e mantém o
/// PlayerMove apontando para a câmera realmente ativa.
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

    [Header("Movimento do Player")]
    public bool atualizarCameraTransformDoPlayer = true;

    [Header("Áudio")]
    public bool controlarAudioListeners = true;

    [Header("Segurança")]
    public bool desativarCameraAntigaSeEncontrar = false;
    public bool ignorarInputComMenuAberto = true;
    public bool logarEventos = false;

    private bool primeiraPessoaAtiva;
    private bool ultimoBotao;

    public bool PrimeiraPessoaAtiva => primeiraPessoaAtiva;
    public Transform CameraAtivaTransform
    {
        get
        {
            if (primeiraPessoaAtiva && firstPersonCAM != null)
                return firstPersonCAM.transform;

            if (thirdPersonCAM != null)
                return thirdPersonCAM.transform;

            return firstPersonCAM != null ? firstPersonCAM.transform : null;
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
        ResolverReferencias();
        AplicarEstadoInicial();
    }

    private void Start()
    {
        ResolverReferencias();
        AplicarEstadoInicial();
    }

    private void Update()
    {
        ResolverReferenciasLeve();

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

        if (logarEventos)
            MiniMarketUpgradeLogger.Log("CameraV2", ativa ? "FirstPersonCAM ON" : "ThirdPersonCAM ON", "Sistema V2 alternou câmera.", "camera-v2-switch", 0.5f);
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
        AtualizarCameraTransformPlayer();
    }

    private void AtualizarAudioListeners()
    {
        if (!controlarAudioListeners)
            return;

        AudioListener thirdListener = thirdPersonCAM != null ? thirdPersonCAM.GetComponent<AudioListener>() : null;
        AudioListener firstListener = firstPersonCAM != null ? firstPersonCAM.GetComponent<AudioListener>() : null;

        if (thirdListener != null && thirdListener.enabled != !primeiraPessoaAtiva)
            thirdListener.enabled = !primeiraPessoaAtiva;

        if (firstListener != null && firstListener.enabled != primeiraPessoaAtiva)
            firstListener.enabled = primeiraPessoaAtiva;
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
