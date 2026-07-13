#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>
/// Garante que o PlatformRenderProfile também seja um objeto real da cena depois que a
/// hierarquia runtime editável for materializada.
/// </summary>
[InitializeOnLoad]
public static class PersistentPlatformRenderProfileSetup
{
    private static bool verificacaoAgendada;

    static PersistentPlatformRenderProfileSetup()
    {
        EditorApplication.hierarchyChanged -= AgendarVerificacao;
        EditorApplication.hierarchyChanged += AgendarVerificacao;
    }

    [MenuItem("Tools/MiniMarket/Materializar Platform Render Profile", priority = 2)]
    public static void MaterializarAgora()
    {
        if (!PodeEditarCena(out Scene scene, true))
            return;

        PlatformRenderProfile existing = Object.FindAnyObjectByType<PlatformRenderProfile>(
            FindObjectsInactive.Include
        );

        if (existing != null)
        {
            EditorGUIUtility.PingObject(existing.gameObject);
            return;
        }

        GameObject host = new GameObject("PlatformRenderProfile");
        Undo.RegisterCreatedObjectUndo(host, "Criar PlatformRenderProfile persistente");
        PlatformRenderProfile profile = Undo.AddComponent<PlatformRenderProfile>(host);
        EditorUtility.SetDirty(profile);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        Debug.Log(
            "[PersistentPlatformRenderProfileSetup] PlatformRenderProfile criado e salvo na cena.",
            profile
        );
    }

    private static void AgendarVerificacao()
    {
        if (verificacaoAgendada ||
            EditorApplication.isPlayingOrWillChangePlaymode ||
            EditorApplication.isCompiling ||
            EditorApplication.isUpdating)
        {
            return;
        }

        RuntimeMiniMapHierarchyBinding marker =
            Object.FindAnyObjectByType<RuntimeMiniMapHierarchyBinding>(FindObjectsInactive.Include);
        if (marker == null)
            return;

        if (Object.FindAnyObjectByType<PlatformRenderProfile>(FindObjectsInactive.Include) != null)
            return;

        verificacaoAgendada = true;
        EditorApplication.delayCall += MaterializarDepoisDaAlteracao;
    }

    private static void MaterializarDepoisDaAlteracao()
    {
        verificacaoAgendada = false;

        if (!PodeEditarCena(out Scene scene, false))
            return;

        RuntimeMiniMapHierarchyBinding marker =
            Object.FindAnyObjectByType<RuntimeMiniMapHierarchyBinding>(FindObjectsInactive.Include);
        if (marker == null)
            return;

        if (Object.FindAnyObjectByType<PlatformRenderProfile>(FindObjectsInactive.Include) != null)
            return;

        GameObject host = new GameObject("PlatformRenderProfile");
        PlatformRenderProfile profile = host.AddComponent<PlatformRenderProfile>();
        EditorUtility.SetDirty(profile);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    private static bool PodeEditarCena(out Scene scene, bool logar)
    {
        scene = SceneManager.GetActiveScene();

        if (EditorApplication.isPlayingOrWillChangePlaymode ||
            EditorApplication.isCompiling ||
            EditorApplication.isUpdating)
        {
            if (logar)
            {
                Debug.LogWarning(
                    "[PersistentPlatformRenderProfileSetup] Saia do Play Mode e aguarde a compilação."
                );
            }
            return false;
        }

        if (!scene.IsValid() || !scene.isLoaded || string.IsNullOrWhiteSpace(scene.path))
        {
            if (logar)
            {
                Debug.LogWarning(
                    "[PersistentPlatformRenderProfileSetup] Abra e salve a SampleScene primeiro."
                );
            }
            return false;
        }

        return true;
    }
}
#endif
