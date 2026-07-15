using System;
using UnityEngine;

/// <summary>
/// Componente legado preservado para manter o GUID de cenas e prefabs antigos.
///
/// A versão anterior movia jogador, câmera e perfil para fora de DontDestroyOnLoad
/// durante o Play Mode. Isso mascarava referências serializadas inválidas e podia
/// destruir o ciclo de vida correto entre cenas. A correção agora acontece na origem:
/// fachadas de cena não são persistidas e referências runtime não são serializadas.
/// </summary>
[AddComponentMenu("")]
[Obsolete("SceneReferenceRepair foi desativado. Use o validador e o limpador manual do Editor.")]
public sealed class SceneReferenceRepair : MonoBehaviour
{
    [Header("Legado — sem efeito")]
    public bool ativo;
    public bool procurarAutomaticamente;
    [Min(0.1f)] public float intervaloBusca = 0.5f;
    [Min(0.1f)] public float tempoMaximoReparando = 8f;
    public bool repararCharacter01 = true;
    public bool repararMainCamera = true;
    public bool repararPlayerProfile = true;
    public bool logarReparos = true;

    private static bool avisoEmitido;

    private void Awake()
    {
        ativo = false;
        procurarAutomaticamente = false;
        enabled = false;

        if (avisoEmitido)
            return;

        avisoEmitido = true;
        Debug.LogWarning(
            "[SceneReferenceRepair] Componente legado desativado. " +
            "Corrija referências cross-scene no Edit Mode; objetos não serão movidos durante o Play."
        );
    }
}
