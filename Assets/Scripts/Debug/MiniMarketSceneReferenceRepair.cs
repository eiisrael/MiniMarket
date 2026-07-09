using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Corrige warnings do Unity sobre Cross Scene References durante o Play.
///
/// Logs recentes mostraram:
/// - Character 01 em DontDestroyOnLoad sendo referenciado por objetos da SampleScene;
/// - Main Camera em DontDestroyOnLoad sendo referenciada por BuySceneController;
/// - MiniMarket_PlayerProfile em DontDestroyOnLoad sendo referenciado por Menu.
///
/// Em uma cena única de teste, esses objetos não precisam ficar em DontDestroyOnLoad.
/// Este reparador move esses roots de volta para a cena ativa durante o Play,
/// evitando warnings e referências inválidas no Editor.
/// </summary>
[DefaultExecutionOrder(-31000)]
public class MiniMarketSceneReferenceRepair : MonoBehaviour
{
    [Header("Ativação")]
    public bool ativo = true;
    public bool procurarAutomaticamente = true;
    [Min(0.1f)] public float intervaloBusca = 0.5f;
    [Min(0.1f)] public float tempoMaximoReparando = 8f;

    [Header("Objetos que devem voltar para a cena ativa")]
    public bool repararCharacter01 = true;
    public bool repararMainCamera = true;
    public bool repararPlayerProfile = true;

    [Header("Debug")]
    public bool logarReparos = true;

    private static MiniMarketSceneReferenceRepair instancia;
    private float proximaBusca;
    private float tempoInicio;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void CriarAutomaticamente()
    {
        if (instancia != null)
            return;

        GameObject go = new GameObject("MiniMarket_SceneReferenceRepair");
        DontDestroyOnLoad(go);
        instancia = go.AddComponent<MiniMarketSceneReferenceRepair>();
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
        RepararAgora();
    }

    private void Update()
    {
        if (!ativo || !procurarAutomaticamente)
            return;

        if (Time.unscaledTime - tempoInicio > tempoMaximoReparando)
            return;

        if (Time.unscaledTime < proximaBusca)
            return;

        proximaBusca = Time.unscaledTime + intervaloBusca;
        RepararAgora();
    }

    private void RepararAgora()
    {
        Scene ativa = SceneManager.GetActiveScene();
        if (!ativa.IsValid() || !ativa.isLoaded)
            return;

        Transform[] todos = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < todos.Length; i++)
        {
            Transform t = todos[i];
            if (t == null)
                continue;

            if (!DeveRepararNome(t.name))
                continue;

            GameObject root = t.root != null ? t.root.gameObject : t.gameObject;
            if (root == null)
                continue;

            if (root.scene == ativa)
                continue;

            if (!root.scene.IsValid())
                continue;

            if (root.scene.name != "DontDestroyOnLoad")
                continue;

            SceneManager.MoveGameObjectToScene(root, ativa);

            if (logarReparos)
            {
                string detalhes = "Movido de DontDestroyOnLoad para " + ativa.name + ": " + root.name;
                Debug.Log("[MiniMarketSceneReferenceRepair] " + detalhes);
                MiniMarketUpgradeLogger.Log("Runtime", "Cross scene reference reparada", detalhes, "scene-ref-repair-" + root.name, 2f);
            }
        }
    }

    private bool DeveRepararNome(string nome)
    {
        if (string.IsNullOrEmpty(nome))
            return false;

        if (repararCharacter01 && nome == "Character 01")
            return true;

        if (repararMainCamera && nome == "Main Camera")
            return true;

        if (repararPlayerProfile && nome == "MiniMarket_PlayerProfile")
            return true;

        return false;
    }
}
