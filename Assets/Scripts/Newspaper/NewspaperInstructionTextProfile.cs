using UnityEngine;

/// <summary>
/// Armazena no objeto raiz do prompt os textos editados no Instruction.
/// Assim as personalizações sobrevivem quando os filhos visuais são reconstruídos.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("")]
public sealed class NewspaperInstructionTextProfile : MonoBehaviour
{
    [HideInInspector] public bool initialized;
    [HideInInspector] public bool overrideControllerTexts = true;
    [HideInInspector] public bool previewInEditMode = true;
    [HideInInspector] public NewspaperInstructionTextSettings.InstructionState previewState =
        NewspaperInstructionTextSettings.InstructionState.Available;

    [HideInInspector] public string availableText = "Segure 'E' para pegar";
    [HideInInspector] public string holdingText = "Coletando jornal...";
    [HideInInspector] public string respawnText = "Novo jornal em {0:0.0}s";
    [HideInInspector] public string placementText = "Pressione 'E' para colocar o jornal";
    [HideInInspector] public float previewRespawnSeconds = 10f;
}
