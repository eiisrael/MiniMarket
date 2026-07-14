using UnityEngine;

/// <summary>
/// Proteção visual complementar da Put_Area.
///
/// Não controla inventário nem interação. Apenas garante que o prompt de colocação use
/// a camada premium e que os renderizadores originais do Placed_Newspaper_Runtime não
/// permaneçam ocultos depois que o guia da área é desligado.
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(12000)]
public sealed class NewspaperPlacementAreaVisualGuard : MonoBehaviour
{
    [Header("Referências")]
    public NewspaperPlacementAreaController controller;
    public NewspaperPromptPremiumKeyVisual premiumPrompt;

    [Header("Proteções")]
    public bool ensurePremiumPromptAtRuntime = true;
    public bool restorePlacedNewspaperRenderers = true;

    [Tooltip("Controle interno do instalador. Impede que o Put_Area seja resincronizado após o visual já ter sido salvo.")]
    public bool premiumVisualSynchronizedFromStand;

    private GameObject cachedPlacedVisual;
    private Renderer[] cachedRenderers = System.Array.Empty<Renderer>();
    private bool[] originalRendererStates = System.Array.Empty<bool>();
    private bool restoredForCurrentPlacement;

    private void Awake()
    {
        ResolveReferences();
        CachePlacedVisualRenderers();
        EnsurePremiumRuntimeFallback();
    }

    private void OnEnable()
    {
        ResolveReferences();
        CachePlacedVisualRenderers();
        EnsurePremiumRuntimeFallback();
        restoredForCurrentPlacement = false;
    }

    private void LateUpdate()
    {
        ResolveReferences();

        if (controller == null)
            return;

        if (cachedPlacedVisual != controller.placedNewspaperVisual)
        {
            CachePlacedVisualRenderers();
            restoredForCurrentPlacement = false;
        }

        if (!controller.IsOccupied)
        {
            restoredForCurrentPlacement = false;
            return;
        }

        if (!restorePlacedNewspaperRenderers || restoredForCurrentPlacement)
            return;

        RestoreOriginalRendererStates();
        restoredForCurrentPlacement = true;
    }

    [ContextMenu("Jornal/Reparar visual da Put Area")]
    public void RepairVisualNow()
    {
        ResolveReferences();
        CachePlacedVisualRenderers();
        EnsurePremiumRuntimeFallback();
        RestoreOriginalRendererStates();
    }

    private void ResolveReferences()
    {
        if (controller == null)
            controller = GetComponent<NewspaperPlacementAreaController>();

        if (controller == null)
            controller = GetComponentInParent<NewspaperPlacementAreaController>();

        if (controller == null)
            return;

        if (premiumPrompt == null && controller.promptVisual != null)
        {
            premiumPrompt = controller.promptVisual.GetComponent<NewspaperPromptPremiumKeyVisual>();
        }
    }

    private void EnsurePremiumRuntimeFallback()
    {
        if (!Application.isPlaying || !ensurePremiumPromptAtRuntime || controller == null)
            return;

        NewspaperWorldPromptVisual prompt = controller.promptVisual;
        if (prompt == null)
            return;

        if (premiumPrompt == null)
            premiumPrompt = prompt.GetComponent<NewspaperPromptPremiumKeyVisual>();

        if (premiumPrompt == null)
            premiumPrompt = prompt.gameObject.AddComponent<NewspaperPromptPremiumKeyVisual>();

        premiumPrompt.EnsureEditableHierarchy(false);
    }

    private void CachePlacedVisualRenderers()
    {
        cachedPlacedVisual = controller != null ? controller.placedNewspaperVisual : null;

        if (cachedPlacedVisual == null)
        {
            cachedRenderers = System.Array.Empty<Renderer>();
            originalRendererStates = System.Array.Empty<bool>();
            return;
        }

        cachedRenderers = cachedPlacedVisual.GetComponentsInChildren<Renderer>(true);
        originalRendererStates = new bool[cachedRenderers.Length];

        for (int i = 0; i < cachedRenderers.Length; i++)
        {
            Renderer renderer = cachedRenderers[i];
            originalRendererStates[i] = renderer != null && renderer.enabled;
        }
    }

    private void RestoreOriginalRendererStates()
    {
        if (cachedPlacedVisual == null || !cachedPlacedVisual.activeInHierarchy)
            return;

        int count = Mathf.Min(cachedRenderers.Length, originalRendererStates.Length);

        for (int i = 0; i < count; i++)
        {
            Renderer renderer = cachedRenderers[i];
            if (renderer == null)
                continue;

            renderer.enabled = originalRendererStates[i];
        }
    }

    private void OnValidate()
    {
        if (controller == null)
            controller = GetComponent<NewspaperPlacementAreaController>();
    }
}
