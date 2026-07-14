using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Configuração runtime segura baseada nos nomes usados na cena.
/// Não substitui o configurador de Editor: apenas garante funcionamento imediato
/// após o git pull quando os componentes ainda não foram persistidos na SampleScene.
/// </summary>
[DefaultExecutionOrder(-7200)]
[DisallowMultipleComponent]
public sealed class MiniMarketNewspaperRuntimeInstaller : MonoBehaviour
{
    public static MiniMarketNewspaperRuntimeInstaller Instance { get; private set; }

    [Header("Nomes esperados")]
    public string standObjectName = "Newspaper_Stand";
    public string newspaperObjectName = "Jornal";
    public string placeRootName = "Jornal_Place";
    public string putAreaName = "Put_Area";

    [Header("Configuração inicial")]
    [Min(0.1f)] public float setupDelay = 0.15f;
    public bool logSetup;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Instance = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (Instance != null)
            return;

        MiniMarketNewspaperRuntimeInstaller existing =
            Object.FindAnyObjectByType<MiniMarketNewspaperRuntimeInstaller>(
                FindObjectsInactive.Include
            );

        if (existing != null)
        {
            Instance = existing;
            return;
        }

        GameObject host = new GameObject("[MiniMarket] Newspaper Runtime Installer");
        DontDestroyOnLoad(host);
        Instance = host.AddComponent<MiniMarketNewspaperRuntimeInstaller>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private void Start()
    {
        StartCoroutine(ConfigureAfterDelay());
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        if (Instance == this)
            Instance = null;
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        StartCoroutine(ConfigureAfterDelay());
    }

    private IEnumerator ConfigureAfterDelay()
    {
        if (setupDelay > 0f)
            yield return new WaitForSecondsRealtime(setupDelay);
        else
            yield return null;

        ConfigureCurrentScenes();
    }

    [ContextMenu("Jornal/Configurar objetos encontrados")]
    public void ConfigureCurrentScenes()
    {
        Transform stand = FindTransformByName(standObjectName, null);
        Transform newspaper = stand != null
            ? FindTransformByName(newspaperObjectName, stand)
            : null;

        NewspaperStandController standController = null;
        if (stand != null)
        {
            standController = stand.GetComponent<NewspaperStandController>();
            if (standController == null)
                standController = stand.gameObject.AddComponent<NewspaperStandController>();

            if (newspaper != null)
            {
                standController.newspaperVisual = newspaper.gameObject;
                standController.interactionPoint = newspaper;

                GrabbableItem grabbable = newspaper.GetComponentInChildren<GrabbableItem>(true);
                if (grabbable != null)
                {
                    grabbable.canBeGrabbed = false;
                    standController.grabbableItem = grabbable;
                }

                InteractionHighlight highlight = newspaper.GetComponent<InteractionHighlight>();
                if (highlight == null)
                    highlight = newspaper.gameObject.AddComponent<InteractionHighlight>();
                standController.highlight = highlight;
            }

            if (standController.promptAnchor == null)
                standController.promptAnchor = stand;
        }

        Transform placeRoot = FindTransformByName(placeRootName, null);
        Transform putArea = placeRoot != null
            ? FindTransformByName(putAreaName, placeRoot)
            : FindTransformByName(putAreaName, null);

        NewspaperPlacementAreaController placeController = null;
        if (putArea != null)
        {
            placeController = putArea.GetComponent<NewspaperPlacementAreaController>();
            if (placeController == null)
                placeController = putArea.gameObject.AddComponent<NewspaperPlacementAreaController>();

            placeController.putArea = putArea;
            placeController.promptAnchor = putArea;
            placeController.areaCollider = putArea.GetComponent<Collider>();
            placeController.placeId = BuildHierarchyId(putArea);

            if (newspaper != null)
                placeController.newspaperSourceVisual = newspaper.gameObject;
        }

        if (logSetup)
        {
            Debug.Log(
                "[NewspaperRuntimeInstaller] Stand=" + (standController != null) +
                " | PutArea=" + (placeController != null),
                this
            );
        }
    }

    private static Transform FindTransformByName(string exactName, Transform root)
    {
        if (string.IsNullOrWhiteSpace(exactName))
            return null;

        if (root != null)
        {
            if (root.name == exactName)
                return root;

            for (int i = 0; i < root.childCount; i++)
            {
                Transform found = FindTransformByName(exactName, root.GetChild(i));
                if (found != null)
                    return found;
            }

            return null;
        }

        Transform[] all = Object.FindObjectsByType<Transform>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None
        );

        for (int i = 0; i < all.Length; i++)
        {
            Transform candidate = all[i];
            if (candidate == null || candidate.name != exactName)
                continue;
            if (!candidate.gameObject.scene.IsValid())
                continue;
            return candidate;
        }

        return null;
    }

    private static string BuildHierarchyId(Transform target)
    {
        if (target == null)
            return "NEWSPAPER_PLACE_DEFAULT";

        string path = target.name;
        Transform current = target.parent;
        while (current != null)
        {
            path = current.name + "_" + path;
            current = current.parent;
        }

        string sceneName = target.gameObject.scene.IsValid()
            ? target.gameObject.scene.name
            : "SCENE";

        return (sceneName + "_" + path)
            .ToUpperInvariant()
            .Replace(' ', '_')
            .Replace('/', '_')
            .Replace('\\', '_');
    }
}
