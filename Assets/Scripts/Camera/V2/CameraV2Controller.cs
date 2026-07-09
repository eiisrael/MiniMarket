using UnityEngine;

/// <summary>
/// Controlador central do Camera System V2.
///
/// Use em um GameObject vazio: CameraSystemV2.
/// Ele alterna entre ThirdPersonCAM e FirstPersonCAM sem depender dos scripts antigos.
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

    [Header("Input")]
    public bool usarBotaoDireitoParaPrimeiraPessoa = true;
    [Range(0, 2)] public int botaoPrimeiraPessoa = 1;
    public bool voltarParaTerceiraAoSoltar = true;
    public KeyCode teclaAlternar = KeyCode.None;

    [Header("Estado Inicial")]
    public bool iniciarEmTerceiraPessoa = true;
    public bool sincronizarYawAoTrocar = true;

    [Header("Segurança")]
    public bool desativarCameraAntigaSeEncontrar = false;
    public bool logarEventos = false;

    private bool primeiraPessoaAtiva;
    private bool ultimoBotao;

    public bool PrimeiraPessoaAtiva => primeiraPessoaAtiva;

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

    public void SetPrimeiraPessoa(bool ativa)
    {
        ResolverReferenciasLeve();

        if (primeiraPessoaAtiva == ativa && thirdPersonCAM != null && firstPersonCAM != null)
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
        if (thirdPersonCAM == null)
            thirdPersonCAM = Object.FindFirstObjectByType<Camera3Person>(FindObjectsInactive.Include);

        if (firstPersonCAM == null)
            firstPersonCAM = Object.FindFirstObjectByType<Camera1Person>(FindObjectsInactive.Include);

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
    }

    private void AplicarEstadoInicial()
    {
        primeiraPessoaAtiva = !iniciarEmTerceiraPessoa;
        AplicarAtivacao();
    }

    private void AplicarAtivacao()
    {
        if (thirdPersonCAM != null)
            thirdPersonCAM.cameraAtiva = !primeiraPessoaAtiva;

        if (firstPersonCAM != null)
            firstPersonCAM.SetAtiva(primeiraPessoaAtiva);
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
