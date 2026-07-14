using System.Globalization;
using TMPro;
using UnityEngine;

/// <summary>
/// Perfil editável dos textos exibidos pelo objeto filho Instruction.
/// O componente é anexado diretamente ao Instruction para que todos os textos
/// de estado possam ser alterados no próprio Inspector.
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
[DefaultExecutionOrder(10000)]
public sealed class NewspaperInstructionTextSettings : MonoBehaviour
{
    public enum InstructionState
    {
        Available,
        Holding,
        Respawn,
        Placement
    }

    [Header("Controle")]
    [Tooltip("Quando marcado, os textos abaixo substituem os textos fixos enviados pelos controladores.")]
    public bool overrideControllerTexts = true;

    [Tooltip("Mostra no Edit Mode o estado escolhido em Estado de visualização.")]
    public bool previewInEditMode = true;

    public InstructionState previewState = InstructionState.Available;

    [Header("Todos os textos do Instruction")]
    [TextArea(1, 3)]
    public string availableText = "Segure 'E' para pegar";

    [TextArea(1, 3)]
    public string holdingText = "Coletando jornal...";

    [Tooltip("Use {0:0.0} para inserir automaticamente os segundos restantes.")]
    [TextArea(1, 3)]
    public string respawnText = "Novo jornal em {0:0.0}s";

    [TextArea(1, 3)]
    public string placementText = "Pressione 'E' para colocar o jornal";

    [Header("Visualização do respawn")]
    [Min(0f)] public float previewRespawnSeconds = 10f;

    [Header("Estado atual")]
    [SerializeField] private InstructionState currentState;
    [SerializeField] private string lastControllerText;

    private TMP_Text targetText;
    private NewspaperWorldPromptVisual promptVisual;
    private string lastAppliedText;

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnEnable()
    {
        ResolveReferences();
        ApplyNow();
    }

    private void OnValidate()
    {
        previewRespawnSeconds = Mathf.Max(0f, previewRespawnSeconds);
        ResolveReferences();

        if (!Application.isPlaying && previewInEditMode)
            ApplyPreview();
    }

    private void LateUpdate()
    {
        ResolveReferences();

        if (!overrideControllerTexts || targetText == null)
            return;

        if (!Application.isPlaying)
        {
            if (previewInEditMode)
                ApplyPreview();
            return;
        }

        string controllerText = targetText.text;
        if (!string.IsNullOrEmpty(controllerText) && controllerText != lastAppliedText)
            lastControllerText = controllerText;

        currentState = DetectRuntimeState();
        string resolved = ResolveText(currentState, controllerText);

        if (targetText.text != resolved)
            targetText.text = resolved;

        lastAppliedText = resolved;
    }

    [ContextMenu("Aplicar textos agora")]
    public void ApplyNow()
    {
        ResolveReferences();

        if (targetText == null || !overrideControllerTexts)
            return;

        currentState = Application.isPlaying ? DetectRuntimeState() : previewState;
        string resolved = ResolveText(currentState, targetText.text);
        targetText.text = resolved;
        lastAppliedText = resolved;
    }

    private void ApplyPreview()
    {
        if (targetText == null || !overrideControllerTexts)
            return;

        currentState = previewState;
        string resolved = previewState == InstructionState.Respawn
            ? FormatRespawn(respawnText, previewRespawnSeconds)
            : ResolveText(previewState, targetText.text);

        targetText.text = resolved;
        lastAppliedText = resolved;
    }

    private InstructionState DetectRuntimeState()
    {
        if (IsPlacementPrompt())
            return InstructionState.Placement;

        if (promptVisual == null)
            return InstructionState.Available;

        string center = promptVisual.centerText != null
            ? promptVisual.centerText.text
            : string.Empty;

        if (!string.IsNullOrEmpty(center) && center.IndexOf('%') >= 0)
            return InstructionState.Respawn;

        if (promptVisual.progressImage != null &&
            promptVisual.progressImage.enabled &&
            center == "E")
        {
            return InstructionState.Holding;
        }

        return InstructionState.Available;
    }

    private string ResolveText(InstructionState state, string controllerText)
    {
        switch (state)
        {
            case InstructionState.Holding:
                return SelectText(holdingText, controllerText);

            case InstructionState.Respawn:
            {
                float seconds = ExtractFirstNumber(controllerText, previewRespawnSeconds);
                return FormatRespawn(SelectText(respawnText, controllerText), seconds);
            }

            case InstructionState.Placement:
                return SelectText(placementText, controllerText);

            default:
                return SelectText(availableText, controllerText);
        }
    }

    private static string SelectText(string customText, string fallback)
    {
        return string.IsNullOrWhiteSpace(customText) ? fallback : customText;
    }

    private static string FormatRespawn(string template, float seconds)
    {
        if (string.IsNullOrWhiteSpace(template))
            return string.Empty;

        if (template.IndexOf("{0", System.StringComparison.Ordinal) < 0)
            return template;

        try
        {
            return string.Format(CultureInfo.CurrentCulture, template, seconds);
        }
        catch (System.FormatException)
        {
            return template;
        }
    }

    private static float ExtractFirstNumber(string source, float fallback)
    {
        if (string.IsNullOrEmpty(source))
            return fallback;

        int start = -1;
        int length = 0;

        for (int i = 0; i < source.Length; i++)
        {
            char value = source[i];
            bool numeric = char.IsDigit(value) || value == ',' || value == '.' || value == '-';

            if (numeric)
            {
                if (start < 0)
                    start = i;
                length++;
            }
            else if (start >= 0)
            {
                break;
            }
        }

        if (start < 0 || length <= 0)
            return fallback;

        string number = source.Substring(start, length);

        if (float.TryParse(number, NumberStyles.Float, CultureInfo.CurrentCulture, out float current))
            return current;

        number = number.Replace(',', '.');
        if (float.TryParse(number, NumberStyles.Float, CultureInfo.InvariantCulture, out float invariant))
            return invariant;

        return fallback;
    }

    private bool IsPlacementPrompt()
    {
        Transform current = transform;

        while (current != null)
        {
            string objectName = current.name;
            if (objectName.IndexOf("Put_Area", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                objectName.IndexOf("PlacePrompt", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                objectName.IndexOf("Jornal_Place", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }

            current = current.parent;
        }

        return false;
    }

    private void ResolveReferences()
    {
        if (targetText == null)
            targetText = GetComponent<TMP_Text>();

        if (promptVisual == null)
            promptVisual = GetComponentInParent<NewspaperWorldPromptVisual>(true);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void InstallMissingRuntimeComponents()
    {
        NewspaperWorldPromptVisual[] prompts =
            UnityEngine.Object.FindObjectsByType<NewspaperWorldPromptVisual>(FindObjectsInactive.Include);

        for (int i = 0; i < prompts.Length; i++)
        {
            NewspaperWorldPromptVisual prompt = prompts[i];
            if (prompt == null)
                continue;

            Transform instruction = prompt.transform.Find("Instruction");
            if (instruction == null || instruction.GetComponent<TMP_Text>() == null)
                continue;

            if (instruction.GetComponent<NewspaperInstructionTextSettings>() == null)
                instruction.gameObject.AddComponent<NewspaperInstructionTextSettings>();
        }
    }
}
