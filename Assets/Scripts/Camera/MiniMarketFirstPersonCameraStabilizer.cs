using System.Reflection;
using UnityEngine;

/// <summary>
/// Estabilizador auxiliar da câmera do MiniMarket.
///
/// Mudança importante:
/// - por padrão, este script NÃO trava mais os campos do CameraGTAFollowHardcore no Inspector;
/// - ele só limpa inércia interna/velocidades quando necessário;
/// - presets seguros só são aplicados se você desligar o Modo Inspector Livre.
///
/// Assim você consegue editar normalmente no Inspector:
/// Anti Tremor, Auto Align, Proteção de Ângulo, Primeira Pessoa Anti-Jumping,
/// Mira/Zoom, Rotação do Personagem e Colisão da Câmera.
/// </summary>
[DefaultExecutionOrder(32000)]
public class MiniMarketFirstPersonCameraStabilizer : MonoBehaviour
{
    public CameraGTAFollowHardcore cameraGTA;

    [Header("Modo Inspector")]
    [Tooltip("Ligado: deixa o CameraGTAFollowHardcore editável no Inspector e não força valores em loop.")]
    public bool modoInspectorLivre = true;

    [Tooltip("Compatibilidade. Só aplica presets se Modo Inspector Livre estiver desligado.")]
    public bool aplicarConfiguracaoAutomaticamente = true;

    [Tooltip("Só use para diagnóstico pesado. Quando ligado com Modo Inspector Livre desligado, força valores todo frame.")]
    public bool forcarConfiguracaoContinuamente = false;

    [Header("Auto")]
    public bool procurarCameraAutomaticamente = true;

    [Header("Preset Seguro - Main Camera")]
    public bool desativarAutoAlignDaCamera = true;
    public bool manterCameraLivreSemRecenter = true;

    [Header("Preset Seguro - Anti Tremor S/A/D")]
    public bool removerEfeitoMolaMovimento = true;
    [Min(0f)] public float positionSmoothTimeTerceiraPessoa = 0f;
    [Min(0f)] public float rotationSmoothTimeTerceiraPessoa = 0f;
    [Min(0f)] public float tremorVerticalIgnorado = 0.01f;

    [Header("Preset Seguro - Limite vertical")]
    public bool forcarLimitesVerticaisSeguros = true;
    public float minPitchSeguro = -40f;
    public float maxPitchSeguro = 60f;

    [Header("Preset Seguro - Mouse")]
    public bool desligarSuavizacaoMouseTotal = true;
    [Min(0f)] public float mouseSmoothTimeTerceiraPessoa = 0f;

    [Header("Limpeza de Inércia")]
    [Tooltip("Pode ficar ligado. Não altera campos públicos do Inspector; apenas zera inércia interna quando para o mouse.")]
    public bool zerarInerciaMouseQuandoParar = true;
    public bool usarMesmaSensibilidadeDaCameraNormal = true;
    [Min(0f)] public float mouseSmoothTimePrimeiraPessoa = 0f;
    [Min(0f)] public float deadzoneParadaMouse = 0.0015f;
    public bool limparVelocidadesDuranteTransicao = true;

    [Header("Preset Seguro - Primeira Pessoa")]
    public bool permitirCorpoAcompanharCamera = true;

    [Header("Debug")]
    public bool logarEventos;

    private static MiniMarketFirstPersonCameraStabilizer instancia;
    private FieldInfo campoMouseSuavizado;
    private FieldInfo campoMouseSmoothVelocity;
    private FieldInfo campoPositionVelocity;
    private FieldInfo campoRotationVelocity;
    private FieldInfo campoPitch;
    private bool ultimoEstadoPrimeiraPessoa;
    private float ultimoLogTempo;
    private bool presetInicialAplicado;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void CriarAutomaticamente()
    {
        if (instancia != null)
            return;

        GameObject go = new GameObject("MiniMarket_CameraStabilizer");
        DontDestroyOnLoad(go);
        instancia = go.AddComponent<MiniMarketFirstPersonCameraStabilizer>();
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
        ResolverCamera();
        PrepararReflection();
        AplicarPresetSePermitido(true);
    }

    private void LateUpdate()
    {
        ResolverCamera();

        if (cameraGTA == null)
            return;

        AplicarPresetSePermitido(false);

        bool estadoPrimeiraPessoa = cameraGTA.EstaEmPrimeiraPessoa;

        if (limparVelocidadesDuranteTransicao && estadoPrimeiraPessoa != ultimoEstadoPrimeiraPessoa)
            LimparVelocidadesInternas();

        ultimoEstadoPrimeiraPessoa = estadoPrimeiraPessoa;

        if (zerarInerciaMouseQuandoParar && estadoPrimeiraPessoa)
            RemoverInerciaResidualDoMouse();
    }

    private void ResolverCamera()
    {
        if (!procurarCameraAutomaticamente || cameraGTA != null)
            return;

        Camera main = Camera.main;
        if (main != null)
            cameraGTA = main.GetComponent<CameraGTAFollowHardcore>();

        if (cameraGTA == null)
            cameraGTA = Object.FindFirstObjectByType<CameraGTAFollowHardcore>(FindObjectsInactive.Include);

        if (cameraGTA != null)
        {
            PrepararReflection();
            MiniMarketUpgradeLogger.Log("Camera", "CameraGTAFollowHardcore encontrada", "Estabilizador conectado a " + cameraGTA.gameObject.name + ". Modo Inspector Livre=" + modoInspectorLivre, "camera-found", 10f);
        }
    }

    private void AplicarPresetSePermitido(bool inicial)
    {
        if (cameraGTA == null)
            return;

        if (modoInspectorLivre)
            return;

        if (!aplicarConfiguracaoAutomaticamente)
            return;

        if (!forcarConfiguracaoContinuamente && presetInicialAplicado)
            return;

        AplicarConfiguracao();
        presetInicialAplicado = true;
    }

    private void AplicarConfiguracao()
    {
        if (cameraGTA == null)
            return;

        if (desativarAutoAlignDaCamera)
        {
            cameraGTA.autoAlignBehindPlayer = false;
            cameraGTA.autoAlignDelay = 999f;
            cameraGTA.autoAlignSpeed = 0f;
        }

        if (manterCameraLivreSemRecenter)
        {
            cameraGTA.protegerContraResetDeAngulo = true;
            cameraGTA.anguloMaximoAutoAlignSemReset = 180f;
            cameraGTA.bloquearAutoAlignDuranteMira = true;
        }

        if (removerEfeitoMolaMovimento)
        {
            cameraGTA.removerEfeitoMolaDaCamera = true;
            cameraGTA.usarSeguimentoDiretoQuandoNaoEstaTransicionando = true;
            cameraGTA.positionSmoothTime = positionSmoothTimeTerceiraPessoa;
            cameraGTA.rotationSmoothTime = rotationSmoothTimeTerceiraPessoa;
            cameraGTA.positionSmoothTimePrimeiraPessoa = 0f;
            cameraGTA.rotationSmoothTimePrimeiraPessoa = 0f;
            cameraGTA.ignorarTremorVerticalMenorQue = tremorVerticalIgnorado;
            cameraGTA.estabilizarPontoPrimeiraPessoaContraAnimacao = true;
        }

        if (forcarLimitesVerticaisSeguros)
        {
            cameraGTA.minPitch = minPitchSeguro;
            cameraGTA.maxPitch = maxPitchSeguro;
            ClampPitchInterno();
        }

        if (desligarSuavizacaoMouseTotal)
        {
            cameraGTA.suavizarMouse = false;
            cameraGTA.mouseSmoothTimeTerceiraPessoa = mouseSmoothTimeTerceiraPessoa;
            cameraGTA.mouseSmoothTimePrimeiraPessoa = mouseSmoothTimePrimeiraPessoa;
            LimparVelocidadesInternas();
        }

        cameraGTA.usarZoomMiraPorDistancia = false;
        cameraGTA.aplicarColisaoNoZoomMira = false;
        cameraGTA.preservarAnguloAtualNaTransicao = true;
        cameraGTA.alinharComFrenteDoPersonagemAoEntrarNaMira = false;
        cameraGTA.corrigirPitchAoEntrarNaMira = false;
        cameraGTA.usarPosicaoPrimeiraPessoaEstavel = true;
        cameraGTA.evitarRealimentacaoRotacaoPersonagemCamera = true;
        cameraGTA.rotacionarPersonagemNaPrimeiraPessoa = permitirCorpoAcompanharCamera;
        cameraGTA.sincronizarCorpoSuaveNaPrimeiraPessoa = permitirCorpoAcompanharCamera;
        cameraGTA.mouseSmoothTimePrimeiraPessoa = mouseSmoothTimePrimeiraPessoa;

        if (usarMesmaSensibilidadeDaCameraNormal)
        {
            cameraGTA.usarMesmaSensibilidadeDaMainCameraNaPrimeiraPessoa = true;
            cameraGTA.usarSensibilidadeSeparadaNaMira = false;
            cameraGTA.mouseSensitivityXMira = cameraGTA.mouseSensitivityX;
            cameraGTA.mouseSensitivityYMira = cameraGTA.mouseSensitivityY;
        }

        if (logarEventos && Time.unscaledTime - ultimoLogTempo > 10f)
        {
            ultimoLogTempo = Time.unscaledTime;
            MiniMarketUpgradeLogger.Log("Camera", "Preset seguro aplicado", "Modo Inspector Livre desligado. Valores seguros aplicados no CameraGTA.", "camera-config", 10f);
        }
    }

    private void PrepararReflection()
    {
        if (cameraGTA == null)
            return;

        System.Type tipo = typeof(CameraGTAFollowHardcore);
        campoMouseSuavizado = tipo.GetField("mouseSuavizado", BindingFlags.Instance | BindingFlags.NonPublic);
        campoMouseSmoothVelocity = tipo.GetField("mouseSmoothVelocity", BindingFlags.Instance | BindingFlags.NonPublic);
        campoPositionVelocity = tipo.GetField("positionVelocity", BindingFlags.Instance | BindingFlags.NonPublic);
        campoRotationVelocity = tipo.GetField("rotationVelocity", BindingFlags.Instance | BindingFlags.NonPublic);
        campoPitch = tipo.GetField("pitch", BindingFlags.Instance | BindingFlags.NonPublic);
    }

    private void ClampPitchInterno()
    {
        if (cameraGTA == null || campoPitch == null)
            return;

        object valor = campoPitch.GetValue(cameraGTA);
        if (valor is float pitchAtual)
            campoPitch.SetValue(cameraGTA, Mathf.Clamp(pitchAtual, minPitchSeguro, maxPitchSeguro));
    }

    private void RemoverInerciaResidualDoMouse()
    {
        float mx = Input.GetAxisRaw("Mouse X");
        float my = Input.GetAxisRaw("Mouse Y");

        if ((mx * mx + my * my) > deadzoneParadaMouse * deadzoneParadaMouse)
            return;

        if (campoMouseSuavizado != null)
            campoMouseSuavizado.SetValue(cameraGTA, Vector2.zero);

        if (campoMouseSmoothVelocity != null)
            campoMouseSmoothVelocity.SetValue(cameraGTA, Vector2.zero);

        if (campoRotationVelocity != null)
            campoRotationVelocity.SetValue(cameraGTA, new Quaternion(0f, 0f, 0f, 0f));
    }

    private void LimparVelocidadesInternas()
    {
        if (cameraGTA == null)
            return;

        if (campoMouseSuavizado != null)
            campoMouseSuavizado.SetValue(cameraGTA, Vector2.zero);

        if (campoMouseSmoothVelocity != null)
            campoMouseSmoothVelocity.SetValue(cameraGTA, Vector2.zero);

        if (campoPositionVelocity != null)
            campoPositionVelocity.SetValue(cameraGTA, Vector3.zero);

        if (campoRotationVelocity != null)
            campoRotationVelocity.SetValue(cameraGTA, new Quaternion(0f, 0f, 0f, 0f));
    }
}
