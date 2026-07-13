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
    static PersistentPlatformRenderProfileSetup()
    {
        EditorApplication.hierarchyChanged -= VerificarHierarquiaMaterializada;
        EditorApplication.hierarchyChanged += VerificarHierarquiaMaterializada;
    }

    [MenuItem("Tools/MiniMarket/Materializar Platform Render Profile", priority = 2)]
    public static void MaterializarAgora()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning(
                "[PersistentPlatformRenderProfileSetup] Saia do Play Mode antes de executar."
            );
            return;
        }

        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded || string.IsNullOrWhiteSpace(scene.path))
        {
            Debug.LogWarning(
                "[PersistentPlatformRenderProfileSetup] Abra e salve a SampleScene primeiro."
            );
            return;
        }

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

    private static void VerificarHierarquiaMaterializada()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode ||
            EditorApplication.isCompiling ||
            EditorApplication.isUpdating)
        {
            return;
        }

        // Só atua depois que o usuário executou o materializador principal.
        RuntimeMiniMapHierarchyBinding marker =
            Object.FindAnyObjectByType<RuntimeMiniMapHierarchyBinding>(FindObjectsInactive.Include);
        if (marker == null)
            return;

        PlatformRenderProfile existing = Object.FindAnyObjectByType<PlatformRenderProfile>(
            FindObjectsInactive.Include
        );
        if (existing != null)
            return;

        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded || string.IsNullOrWhiteSpace(scene.path))
            return;

        GameObject host = new GameObject("PlatformRenderProfile");
        PlatformRenderProfile profile = host.AddComponent<PlatformRenderProfile>();
        EditorUtility.SetDirty(profile);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }
}
#endif
