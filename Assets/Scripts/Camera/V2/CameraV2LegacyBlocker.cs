using UnityEngine;

/// <summary>
/// Bloqueador de scripts antigos quando o Camera System V2 estiver presente.
///
/// Evita que CameraGTAFollowHardcore, estabilizadores antigos, collision smoothers antigos
/// e grabber antigo continuem mexendo na Main Camera enquanto o V2 está ativo.
///
/// Use em CameraSystemV2 ou deixe o auto-create funcionar.
/// </summary>
[DefaultExecutionOrder(19800)]
public class CameraV2LegacyBlocker : MonoBehaviour
{
    [Header("Ativação")]
    public bool ativo = true;
    public bool bloquearAutomaticamente = true;
    public bool repetirDurantePrimeirosSegundos = true;
    [Min(0.1f)] public float duracaoRepeticaoInicial = 5f;
    [Min(0.1f)] public float intervaloRepeticao = 0.5f;

    [Header("Bloquear")]
    public bool bloquearCameraGTAAntiga = true;
    public bool bloquearEstabilizadoresAntigos = true;
    public bool bloquearGrabberAntigo = true;
    public bool bloquearCrosshairAntigo = false;

    [Header("Debug")]
    public bool logarBloqueios;

    private static CameraV2LegacyBlocker instancia;
    private float tempoInicio;
    private float proximaVerificacao;
    private float ultimoLog;

    private readonly string[] scriptsCameraAntigos =
    {
        "CameraGTAFollowHardcore",
        "MiniMarketFirstPersonCameraStabilizer",
        "MiniMarketCameraCollisionSmoother",
        "MiniMarketCameraPerspectiveSwitcher",
        "MiniMarketCameraAuthorityStabilizer",
        "MiniMarketCameraFrameSpikeGuard",
        "MiniMarketCameraDiagnosticsTuner",
        "MiniMarketCameraRealtimeAnomalyLogger"
    };

    private readonly string[] scriptsGrabberAntigos =
    {
        "PlayerObjectGrabberHardcore",
        "MiniMarketGrabberRuntimeStabilizer"
    };

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void CriarAutomaticamente()
    {
        if (instancia != null)
            return;

        GameObject go = new GameObject("MiniMarket_CameraV2LegacyBlocker");
        DontDestroyOnLoad(go);
        instancia = go.AddComponent<CameraV2LegacyBlocker>();
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
        tempoInicio = Time.unscaledTime;
        BloquearSeV2Existe();
    }

    private void LateUpdate()
    {
        if (!ativo || !bloquearAutomaticamente)
            return;

        if (!repetirDurantePrimeirosSegundos && Time.frameCount > 3)
            return;

        if (repetirDurantePrimeirosSegundos && Time.unscaledTime - tempoInicio > duracaoRepeticaoInicial)
            return;

        if (Time.unscaledTime < proximaVerificacao)
            return;

        proximaVerificacao = Time.unscaledTime + intervaloRepeticao;
        BloquearSeV2Existe();
    }

    private void BloquearSeV2Existe()
    {
        if (!ExisteSistemaV2())
            return;

        int bloqueados = 0;
        MonoBehaviour[] behaviours = Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        for (int i = 0; i < behaviours.Length; i++)
        {
            MonoBehaviour mb = behaviours[i];
            if (mb == null || mb == this)
                continue;

            string nomeTipo = mb.GetType().Name;

            if (bloquearCameraGTAAntiga && NomeEstaNaLista(nomeTipo, scriptsCameraAntigos))
            {
                if (mb.enabled)
                {
                    mb.enabled = false;
                    bloqueados++;
                }
                continue;
            }

            if (bloquearGrabberAntigo && NomeEstaNaLista(nomeTipo, scriptsGrabberAntigos))
            {
                if (mb.enabled)
                {
                    mb.enabled = false;
                    bloqueados++;
                }
                continue;
            }

            if (bloquearCrosshairAntigo && nomeTipo == "CrosshairAim")
            {
                if (mb.enabled)
                {
                    mb.enabled = false;
                    bloqueados++;
                }
            }
        }

        if (logarBloqueios && bloqueados > 0 && Time.unscaledTime - ultimoLog > 1f)
        {
            ultimoLog = Time.unscaledTime;
            MiniMarketUpgradeLogger.Log("CameraV2", "Scripts antigos bloqueados", "Quantidade: " + bloqueados + ". Camera/GetItem V2 assumiu o controle.", "camera-v2-legacy-block", 1f);
        }
    }

    private bool ExisteSistemaV2()
    {
        return Object.FindFirstObjectByType<Camera3Person>(FindObjectsInactive.Include) != null ||
               Object.FindFirstObjectByType<Camera1Person>(FindObjectsInactive.Include) != null ||
               Object.FindFirstObjectByType<CameraV2Controller>(FindObjectsInactive.Include) != null;
    }

    private bool NomeEstaNaLista(string nome, string[] lista)
    {
        if (string.IsNullOrEmpty(nome) || lista == null)
            return false;

        for (int i = 0; i < lista.Length; i++)
        {
            if (nome == lista[i])
                return true;
        }

        return false;
    }
}
