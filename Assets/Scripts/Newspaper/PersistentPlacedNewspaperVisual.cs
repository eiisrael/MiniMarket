using UnityEngine;

/// <summary>
/// Identifica o jornal colocado que pertence permanentemente à cena.
/// O objeto e seus filhos permanecem editáveis no Inspector e não são destruídos
/// quando o Play Mode termina.
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
public sealed class PersistentPlacedNewspaperVisual : MonoBehaviour
{
    [Tooltip("Mostra este jornal fora do Play Mode para facilitar a edição.")]
    public bool previewInEditMode = true;

    [Tooltip("Indica que posição, rotação e escala são controladas diretamente pelo Transform deste objeto.")]
    public bool transformControlledByInspector = true;

    [SerializeField, HideInInspector]
    private bool setupDefaultsApplied;

    public bool SetupDefaultsApplied => setupDefaultsApplied;

    public void MarkSetupDefaultsApplied()
    {
        setupDefaultsApplied = true;
    }
}
