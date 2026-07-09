using UnityEngine;

/// <summary>
/// Permite que o CharacterController empurre caixas/produtos com Rigidbody.
///
/// Unity CharacterController nao empurra Rigidbody automaticamente de forma forte.
/// Este script aplica uma velocidade/impulso horizontal leve e controlado quando o player encosta.
/// Ele se instala automaticamente em objetos com CharacterController ao iniciar a cena.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class MiniMarketPlayerRigidbodyPusher : MonoBehaviour
{
    [Header("Empurrar Rigidbody")]
    public bool ativo = true;

    [Tooltip("Forca base para empurrar caixas/produtos.")]
    [Min(0f)] public float forcaEmpurrar = 2.8f;

    [Tooltip("Massa acima deste valor reduz bastante o empurrao.")]
    [Min(0.1f)] public float massaReferencia = 8f;

    [Tooltip("Nao empurra se o contato for muito vertical, evitando empurrar chao.")]
    [Range(0f, 1f)] public float normalYMaximaParaEmpurrar = 0.45f;

    [Tooltip("Velocidade maxima horizontal aplicada pelo empurrao.")]
    [Min(0.1f)] public float velocidadeMaximaEmpurrar = 3.5f;

    [Header("Debug")]
    public bool logarEventos;

    private CharacterController controller;
    private float ultimoLog;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void InstalarAutomaticamente()
    {
        CharacterController[] controllers = FindObjectsOfType<CharacterController>(true);
        for (int i = 0; i < controllers.Length; i++)
        {
            CharacterController cc = controllers[i];
            if (cc == null)
                continue;

            if (cc.GetComponent<MiniMarketPlayerRigidbodyPusher>() == null)
                cc.gameObject.AddComponent<MiniMarketPlayerRigidbodyPusher>();
        }
    }

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
    }

    private void OnControllerColliderHit(ControllerColliderHit hit)
    {
        if (!ativo || hit == null)
            return;

        Rigidbody rb = hit.rigidbody;
        if (rb == null || rb.isKinematic)
            return;

        if (hit.moveDirection.y < -0.3f)
            return;

        if (hit.normal.y > normalYMaximaParaEmpurrar)
            return;

        Vector3 direcao = hit.moveDirection;
        direcao.y = 0f;

        if (direcao.sqrMagnitude <= 0.0001f)
            return;

        direcao.Normalize();

        float massa = Mathf.Max(0.01f, rb.mass);
        float fatorMassa = Mathf.Clamp01(massaReferencia / massa);
        float velocidade = Mathf.Clamp(forcaEmpurrar * fatorMassa, 0.05f, velocidadeMaximaEmpurrar);

        Vector3 velocidadeAtual = rb.linearVelocity;
        Vector3 velocidadeHorizontal = new Vector3(velocidadeAtual.x, 0f, velocidadeAtual.z);
        Vector3 alvoHorizontal = direcao * velocidade;
        Vector3 novaHorizontal = Vector3.Lerp(velocidadeHorizontal, alvoHorizontal, 0.45f);

        rb.linearVelocity = new Vector3(novaHorizontal.x, velocidadeAtual.y, novaHorizontal.z);
        rb.WakeUp();

        if (logarEventos && Time.unscaledTime - ultimoLog > 1f)
        {
            ultimoLog = Time.unscaledTime;
            MiniMarketUpgradeLogger.Log("Physics", "Player empurrou Rigidbody", rb.gameObject.name + " | massa=" + massa.ToString("0.##"), "push-" + rb.GetInstanceID(), 1f);
        }
    }
}
