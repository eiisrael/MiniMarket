using UnityEngine;

/// <summary>
/// Realce visual sem instanciar materiais.
/// Usa MaterialPropertyBlock, preserva blocos existentes e funciona com shaders URP
/// (_BaseColor) e Standard (_Color). Pode ser usado por portas, caixas e itens móveis.
/// </summary>
[DisallowMultipleComponent]
public sealed class InteractionHighlight : MonoBehaviour
{
    [Header("Renderers")]
    public Renderer[] targetRenderers;
    public bool includeInactiveChildren = true;
    public bool autoFindRenderers = true;

    [Header("Cores")]
    public Color focusColor = new Color(0.2f, 0.85f, 1f, 1f);
    public Color activeColor = new Color(0.25f, 1f, 0.35f, 1f);
    [Range(0f, 1f)] public float tintStrength = 0.65f;

    [Header("Comportamento")]
    public bool highlightWhenFocused = true;
    public bool highlightWhenActive = true;

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    private MaterialPropertyBlock[] originalBlocks;
    private MaterialPropertyBlock workingBlock;
    private Color[] originalColors;
    private bool focused;
    private bool active;
    private bool cached;

    public bool IsFocused => focused;
    public bool IsActive => active;

    private void Awake()
    {
        CacheRenderers();
        ApplyState();
    }

    private void OnEnable()
    {
        CacheRenderers();
        ApplyState();
    }

    private void OnDisable()
    {
        RestoreOriginalBlocks();
    }

    private void OnDestroy()
    {
        RestoreOriginalBlocks();
    }

    public void SetFocused(bool value)
    {
        if (focused == value)
            return;

        focused = value;
        ApplyState();
    }

    public void SetActiveInteraction(bool value)
    {
        if (active == value)
            return;

        active = value;
        ApplyState();
    }

    public void Clear()
    {
        focused = false;
        active = false;
        RestoreOriginalBlocks();
    }

    [ContextMenu("Highlight/Atualizar renderers")]
    public void RefreshRenderers()
    {
        RestoreOriginalBlocks();
        cached = false;
        CacheRenderers();
        ApplyState();
    }

    private void CacheRenderers()
    {
        if (cached)
            return;

        if ((targetRenderers == null || targetRenderers.Length == 0) && autoFindRenderers)
            targetRenderers = GetComponentsInChildren<Renderer>(includeInactiveChildren);

        if (targetRenderers == null)
            targetRenderers = new Renderer[0];

        originalBlocks = new MaterialPropertyBlock[targetRenderers.Length];
        originalColors = new Color[targetRenderers.Length];
        workingBlock = new MaterialPropertyBlock();

        for (int i = 0; i < targetRenderers.Length; i++)
        {
            Renderer target = targetRenderers[i];
            originalBlocks[i] = new MaterialPropertyBlock();

            if (target == null)
            {
                originalColors[i] = Color.white;
                continue;
            }

            target.GetPropertyBlock(originalBlocks[i]);
            originalColors[i] = ReadBaseColor(target);
        }

        cached = true;
    }

    private Color ReadBaseColor(Renderer target)
    {
        if (target == null || target.sharedMaterial == null)
            return Color.white;

        Material material = target.sharedMaterial;
        if (material.HasProperty(BaseColorId))
            return material.GetColor(BaseColorId);
        if (material.HasProperty(ColorId))
            return material.GetColor(ColorId);

        return Color.white;
    }

    private void ApplyState()
    {
        CacheRenderers();

        bool showActive = active && highlightWhenActive;
        bool showFocus = focused && highlightWhenFocused;

        if (!showActive && !showFocus)
        {
            RestoreOriginalBlocks();
            return;
        }

        Color desired = showActive ? activeColor : focusColor;

        for (int i = 0; i < targetRenderers.Length; i++)
        {
            Renderer target = targetRenderers[i];
            if (target == null)
                continue;

            workingBlock.Clear();
            target.GetPropertyBlock(workingBlock);

            Color tinted = Color.Lerp(originalColors[i], desired, tintStrength);
            workingBlock.SetColor(BaseColorId, tinted);
            workingBlock.SetColor(ColorId, tinted);
            target.SetPropertyBlock(workingBlock);
        }
    }

    private void RestoreOriginalBlocks()
    {
        if (!cached || targetRenderers == null || originalBlocks == null)
            return;

        int count = Mathf.Min(targetRenderers.Length, originalBlocks.Length);
        for (int i = 0; i < count; i++)
        {
            Renderer target = targetRenderers[i];
            if (target != null)
                target.SetPropertyBlock(originalBlocks[i]);
        }
    }

    private void OnValidate()
    {
        tintStrength = Mathf.Clamp01(tintStrength);
    }
}
