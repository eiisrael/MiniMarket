using UnityEngine;

/// <summary>
/// Garante feedback visual da mão em qualquer clique esquerdo do gameplay.
/// Funciona em primeira e terceira pessoa e mantém hand_close visível por um
/// intervalo mínimo para que cliques rápidos também sejam percebidos.
/// </summary>
[DefaultExecutionOrder(980)]
[DisallowMultipleComponent]
public sealed class MiniMarketHandClickOverride : MonoBehaviour
{
    [Header("Referência")]
    public MiniMarketPersistentAimHandController handController;

    [Header("Clique")]
    [Range(0, 2)] public int mouseButton = 0;
    [Min(0.02f)] public float minimumClosedVisualTime = 0.10f;

    [Header("Busca automática")]
    [Min(0.1f)] public float searchInterval = 0.5f;

    private static MiniMarketHandClickOverride instance;
    private float closeUntil;
    private float nextSearch;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (instance != null)
            return;

        MiniMarketHandClickOverride existing =
            Object.FindAnyObjectByType<MiniMarketHandClickOverride>(FindObjectsInactive.Include);

        if (existing != null)
        {
            instance = existing;
            return;
        }

        GameObject runtimeObject = new GameObject("[MiniMarket] Hand Click Override");
        DontDestroyOnLoad(runtimeObject);
        instance = runtimeObject.AddComponent<MiniMarketHandClickOverride>();
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
        ResolveController(true);
    }

    private void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }

    private void Update()
    {
        ResolveController(false);

        if (handController == null)
            return;

        // A mão deve responder nos dois modos de câmera.
        handController.closeHandOnlyInFirstPerson = false;

        if (!GameplayInputState.IsBlocked && Input.GetMouseButtonDown(mouseButton))
        {
            closeUntil = Time.unscaledTime + Mathf.Max(0.02f, minimumClosedVisualTime);
        }
    }

    private void LateUpdate()
    {
        if (handController == null || handController.handImage == null)
            return;

        bool gameplayClick = !GameplayInputState.IsBlocked &&
                             (Input.GetMouseButton(mouseButton) || Time.unscaledTime < closeUntil);

        Sprite target = gameplayClick
            ? handController.handCloseSprite
            : handController.handOpenSprite;

        if (target != null && handController.handImage.sprite != target)
            handController.handImage.sprite = target;
    }

    private void ResolveController(bool force)
    {
        if (!force && handController != null && Time.unscaledTime < nextSearch)
            return;

        nextSearch = Time.unscaledTime + Mathf.Max(0.1f, searchInterval);

        if (handController == null)
        {
            handController = Object.FindAnyObjectByType<MiniMarketPersistentAimHandController>(
                FindObjectsInactive.Include
            );
        }
    }
}
