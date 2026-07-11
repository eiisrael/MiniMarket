#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>
/// Migração destrutiva solicitada para o MiniMarket.
///
/// Ao abrir o projeto depois do git pull, executa uma única vez:
/// 1. remove todos os sistemas próprios de câmera e movimentação;
/// 2. remove esses componentes e objetos das cenas/prefabs;
/// 3. remove MonoBehaviours que não são usados por objetos nem por outros scripts;
/// 4. remove o prefixo "MiniMarket" dos scripts/classes restantes;
/// 5. organiza Assets/Scripts por função;
/// 6. ignora completamente qualquer pasta "Brick Project Studio".
///
/// O histórico do Git é o backup. O script também gera um relatório em
/// Assets/Scripts/ScriptOrganizationReport.md.
/// </summary>
[InitializeOnLoad]
public static class ScriptProjectOrganizer
{
    private const string MigrationKey = "MiniMarket.ScriptOrganization.2026-07-11.v1";
    private const string ScriptsRoot = "Assets/Scripts";
    private const string ReportPath = ScriptsRoot + "/ScriptOrganizationReport.md";

    private static readonly Regex ClassRegex = new Regex(
        @"\b(?:public\s+|internal\s+|private\s+|protected\s+)?(?:sealed\s+|abstract\s+|static\s+|partial\s+)*class\s+([A-Za-z_]\w*)",
        RegexOptions.Compiled
    );

    private static readonly Regex GuidRegex = new Regex(
        @"guid:\s*([a-fA-F0-9]{32})",
        RegexOptions.Compiled
    );

    private static readonly Regex CameraMovementNameRegex = new Regex(
        @"(^|_)(Camera|CameraV2|Camera1Person|Camera3Person|ThirdPerson|FirstPerson|MouseLook|Crosshair|POV|PlayerMove|PlayerMovement|CharacterMovement|Movement|Locomotion|PlayerRigidbody|CameraAuthority|CameraCollision|CameraPerspective|CameraStabilizer|CameraDiagnostics|FrameSpikeGuard|LegacyBlocker|MenuInputBlocker|PlayerShutdownGuard)($|_)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled
    );

    private static readonly string[] CameraMovementFragments =
    {
        "camera3person",
        "camera1person",
        "thirdpersoncam",
        "firstpersoncam",
        "camerav2",
        "cameragta",
        "mouselook",
        "crosshair",
        "player_move",
        "playermove",
        "playermovement",
        "charactermovement",
        "playerobjectgrabberhardcore",
        "playerrigidbody",
        "cameracollisionsmoother",
        "cameraframeguard",
        "cameraframespikeguard",
        "cameraperspectiveswitcher",
        "cameraauthoritystabilizer",
        "camerarealtimeanomalylogger",
        "cameradiagnosticstuner",
        "firstpersoncamerastabilizer",
        "cameramenusinput",
        "cameramenuinput",
        "legacycamera",
        "playershutdownguard"
    };

    private static readonly HashSet<string> CameraObjectNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "Main Camera",
        "Camera",
        "CameraSystemV2",
        "Camera1Person",
        "Camera3Person",
        "FirstPersonCAM",
        "ThirdPersonCAM",
        "POV",
        "MiniMarket_CameraV2MenuInputBlocker",
        "MiniMarket_CameraV2LegacyBlocker",
        "MiniMarket_CameraV2F10Diagnostics"
    };

    private sealed class ScriptInfo
    {
        public string Path;
        public string Guid;
        public string FileName;
        public string ClassName;
        public string Content;
        public bool IsMonoBehaviour;
        public bool IsEditor;
        public bool IsBootstrap;
        public bool IsCameraMovement;
    }

    private sealed class MigrationReport
    {
        public readonly List<string> DeletedCameraMovementScripts = new List<string>();
        public readonly List<string> DeletedUnusedScripts = new List<string>();
        public readonly List<string> RenamedClasses = new List<string>();
        public readonly List<string> MovedScripts = new List<string>();
        public readonly List<string> CleanedScenes = new List<string>();
        public readonly List<string> CleanedPrefabs = new List<string>();
        public readonly List<string> Warnings = new List<string>();
        public int RemovedGameObjects;
        public int RemovedComponents;
        public int RemovedCharacterControllers;
        public int RemovedMissingScripts;
    }

    static ScriptProjectOrganizer()
    {
        EditorApplication.delayCall += TryRunAutomatically;
    }

    [MenuItem("Tools/Project Maintenance/Run Script Cleanup and Organization", priority = 1)]
    public static void RunFromMenu()
    {
        EditorPrefs.DeleteKey(MigrationKey);
        ExecuteMigration();
    }

    [MenuItem("Tools/Project Maintenance/Reset Script Cleanup Marker", priority = 20)]
    public static void ResetMarker()
    {
        EditorPrefs.DeleteKey(MigrationKey);
        Debug.Log("[ScriptProjectOrganizer] Marcador apagado. A migração poderá ser executada novamente.");
    }

    private static void TryRunAutomatically()
    {
        if (EditorPrefs.GetBool(MigrationKey, false))
            return;

        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            EditorApplication.delayCall += TryRunAutomatically;
            return;
        }

        ExecuteMigration();
    }

    private static void ExecuteMigration()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("[ScriptProjectOrganizer] Saia do Play Mode antes de executar a organização.");
            return;
        }

        MigrationReport report = new MigrationReport();
        string activeScenePath = SceneManager.GetActiveScene().path;
        bool reloadLocked = false;
        bool editingAssets = false;

        try
        {
            Debug.Log("[ScriptProjectOrganizer] Iniciando limpeza estrutural dos scripts...");
            EditorSceneManager.SaveOpenScenes();

            List<ScriptInfo> scripts = LoadScriptInfos();
            HashSet<string> deletePaths = BuildCameraMovementDeleteSet(scripts);

            CleanupScenesAndPrefabs(deletePaths, report);

            EditorApplication.LockReloadAssemblies();
            reloadLocked = true;
            AssetDatabase.StartAssetEditing();
            editingAssets = true;

            DeletePaths(deletePaths, report.DeletedCameraMovementScripts);

            scripts = LoadScriptInfos();
            RenamePrefixedClassesAndUpdateReferences(scripts, report);

            scripts = LoadScriptInfos();
            OrganizeRemainingScripts(scripts, report);

            scripts = LoadScriptInfos();
            RemoveUnusedMonoBehaviours(scripts, report);

            WriteReport(report);

            AssetDatabase.StopAssetEditing();
            editingAssets = false;
            EditorApplication.UnlockReloadAssemblies();
            reloadLocked = false;

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);

            if (!string.IsNullOrEmpty(activeScenePath) && File.Exists(activeScenePath))
                EditorSceneManager.OpenScene(activeScenePath, OpenSceneMode.Single);

            EditorPrefs.SetBool(MigrationKey, true);

            Debug.Log(
                "[ScriptProjectOrganizer] Concluído. " +
                "Câmera/movimento removidos: " + report.DeletedCameraMovementScripts.Count +
                ", não usados removidos: " + report.DeletedUnusedScripts.Count +
                ", scripts movidos: " + report.MovedScripts.Count +
                ". Relatório: " + ReportPath
            );
        }
        catch (Exception ex)
        {
            Debug.LogError("[ScriptProjectOrganizer] Falha durante a migração: " + ex);
        }
        finally
        {
            if (editingAssets)
                AssetDatabase.StopAssetEditing();

            if (reloadLocked)
                EditorApplication.UnlockReloadAssemblies();

            AssetDatabase.Refresh();
        }
    }

    private static List<ScriptInfo> LoadScriptInfos()
    {
        List<ScriptInfo> result = new List<ScriptInfo>();
        string[] guids = AssetDatabase.FindAssets("t:MonoScript", new[] { ScriptsRoot });

        for (int i = 0; i < guids.Length; i++)
        {
            string path = NormalizePath(AssetDatabase.GUIDToAssetPath(guids[i]));
            if (!IsManagedScriptPath(path) || !File.Exists(path))
                continue;

            string content;
            try
            {
                content = File.ReadAllText(path, Encoding.UTF8);
            }
            catch
            {
                continue;
            }

            Match match = ClassRegex.Match(content);
            string className = match.Success ? match.Groups[1].Value : Path.GetFileNameWithoutExtension(path);
            string lower = (path + " " + className).ToLowerInvariant();

            result.Add(new ScriptInfo
            {
                Path = path,
                Guid = AssetDatabase.AssetPathToGUID(path),
                FileName = Path.GetFileNameWithoutExtension(path),
                ClassName = className,
                Content = content,
                IsMonoBehaviour = Regex.IsMatch(content, @"\b:\s*(?:[A-Za-z_]\w*\.)*MonoBehaviour\b"),
                IsEditor = path.IndexOf("/Editor/", StringComparison.OrdinalIgnoreCase) >= 0,
                IsBootstrap = ContainsBootstrapMarker(content),
                IsCameraMovement = IsCameraMovementScript(path, className, lower)
            });
        }

        return result;
    }

    private static HashSet<string> BuildCameraMovementDeleteSet(List<ScriptInfo> scripts)
    {
        HashSet<string> deletePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        HashSet<string> deletedClasses = new HashSet<string>(StringComparer.Ordinal);

        for (int i = 0; i < scripts.Count; i++)
        {
            ScriptInfo script = scripts[i];
            if (!script.IsCameraMovement)
                continue;

            deletePaths.Add(script.Path);
            if (!string.IsNullOrEmpty(script.ClassName))
                deletedClasses.Add(script.ClassName);
        }

        // Remove dependências diretas dos sistemas excluídos para não deixar erros de compilação.
        bool changed;
        do
        {
            changed = false;

            for (int i = 0; i < scripts.Count; i++)
            {
                ScriptInfo script = scripts[i];
                if (deletePaths.Contains(script.Path) || script.IsEditor)
                    continue;

                foreach (string deletedClass in deletedClasses)
                {
                    if (!ContainsWholeWord(script.Content, deletedClass))
                        continue;

                    deletePaths.Add(script.Path);
                    if (!string.IsNullOrEmpty(script.ClassName))
                        deletedClasses.Add(script.ClassName);
                    changed = true;
                    break;
                }
            }
        }
        while (changed);

        return deletePaths;
    }

    private static bool IsCameraMovementScript(string path, string className, string lowerCombined)
    {
        if (IsIgnoredPath(path))
            return false;

        string normalized = NormalizePath(path);
        if (normalized.StartsWith(ScriptsRoot + "/Camera/", StringComparison.OrdinalIgnoreCase) ||
            normalized.IndexOf("/Movement/", StringComparison.OrdinalIgnoreCase) >= 0 ||
            normalized.IndexOf("/Locomotion/", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return true;
        }

        string file = Path.GetFileNameWithoutExtension(path);
        if (CameraMovementNameRegex.IsMatch(file) || CameraMovementNameRegex.IsMatch(className))
            return true;

        string compact = Regex.Replace(lowerCombined, @"[^a-z0-9_]", string.Empty);
        for (int i = 0; i < CameraMovementFragments.Length; i++)
        {
            if (compact.Contains(CameraMovementFragments[i]))
                return true;
        }

        return false;
    }

    private static void CleanupScenesAndPrefabs(HashSet<string> deletePaths, MigrationReport report)
    {
        string[] sceneGuids = AssetDatabase.FindAssets("t:Scene", new[] { "Assets" });
        for (int i = 0; i < sceneGuids.Length; i++)
        {
            string path = NormalizePath(AssetDatabase.GUIDToAssetPath(sceneGuids[i]));
            if (IsIgnoredPath(path))
                continue;

            try
            {
                Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                bool changed = false;
                GameObject[] roots = scene.GetRootGameObjects();

                for (int r = 0; r < roots.Length; r++)
                    changed |= CleanupHierarchy(roots[r], deletePaths, report);

                if (changed)
                {
                    EditorSceneManager.MarkSceneDirty(scene);
                    EditorSceneManager.SaveScene(scene);
                    report.CleanedScenes.Add(path);
                }
            }
            catch (Exception ex)
            {
                report.Warnings.Add("Cena não processada: " + path + " | " + ex.Message);
            }
        }

        string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" });
        for (int i = 0; i < prefabGuids.Length; i++)
        {
            string path = NormalizePath(AssetDatabase.GUIDToAssetPath(prefabGuids[i]));
            if (IsIgnoredPath(path))
                continue;

            GameObject root = null;
            try
            {
                root = PrefabUtility.LoadPrefabContents(path);
                bool changed = CleanupHierarchy(root, deletePaths, report);

                if (changed)
                {
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    report.CleanedPrefabs.Add(path);
                }
            }
            catch (Exception ex)
            {
                report.Warnings.Add("Prefab não processado: " + path + " | " + ex.Message);
            }
            finally
            {
                if (root != null)
                    PrefabUtility.UnloadPrefabContents(root);
            }
        }
    }

    private static bool CleanupHierarchy(GameObject root, HashSet<string> deletePaths, MigrationReport report)
    {
        if (root == null)
            return false;

        bool changed = false;
        Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
        List<GameObject> objectsToDelete = new List<GameObject>();
        List<Component> componentsToDelete = new List<Component>();
        HashSet<GameObject> movementHosts = new HashSet<GameObject>();

        for (int i = 0; i < transforms.Length; i++)
        {
            GameObject go = transforms[i].gameObject;
            if (go == null)
                continue;

            if (ShouldDeleteCameraObject(go))
            {
                objectsToDelete.Add(go);
                continue;
            }

            Component[] components = go.GetComponents<Component>();
            for (int c = 0; c < components.Length; c++)
            {
                Component component = components[c];
                if (component == null)
                    continue;

                if (component is MonoBehaviour behaviour)
                {
                    MonoScript script = MonoScript.FromMonoBehaviour(behaviour);
                    string scriptPath = script != null ? NormalizePath(AssetDatabase.GetAssetPath(script)) : string.Empty;

                    if (!string.IsNullOrEmpty(scriptPath) && deletePaths.Contains(scriptPath))
                    {
                        componentsToDelete.Add(component);
                        if (IsMovementPath(scriptPath))
                            movementHosts.Add(go);
                        continue;
                    }
                }

                string typeName = component.GetType().Name;
                if (typeName.IndexOf("Camera", StringComparison.OrdinalIgnoreCase) >= 0 &&
                    !(component is Canvas))
                {
                    componentsToDelete.Add(component);
                }
            }
        }

        // Remove filhos antes dos pais.
        objectsToDelete = objectsToDelete
            .Where(x => x != null)
            .Distinct()
            .OrderByDescending(GetDepth)
            .ToList();

        for (int i = 0; i < objectsToDelete.Count; i++)
        {
            GameObject go = objectsToDelete[i];
            if (go == null || go == root)
                continue;

            Object.DestroyImmediate(go);
            report.RemovedGameObjects++;
            changed = true;
        }

        for (int i = 0; i < componentsToDelete.Count; i++)
        {
            Component component = componentsToDelete[i];
            if (component == null)
                continue;

            Object.DestroyImmediate(component);
            report.RemovedComponents++;
            changed = true;
        }

        foreach (GameObject host in movementHosts)
        {
            if (host == null)
                continue;

            CharacterController controller = host.GetComponent<CharacterController>();
            if (controller != null)
            {
                Object.DestroyImmediate(controller);
                report.RemovedCharacterControllers++;
                changed = true;
            }
        }

        Transform[] remaining = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < remaining.Length; i++)
        {
            int removed = GameObjectUtility.RemoveMonoBehavioursWithMissingScript(remaining[i].gameObject);
            if (removed > 0)
            {
                report.RemovedMissingScripts += removed;
                changed = true;
            }
        }

        return changed;
    }

    private static bool ShouldDeleteCameraObject(GameObject go)
    {
        if (go == null)
            return false;

        if (CameraObjectNames.Contains(go.name))
            return true;

        if (go.GetComponent<Camera>() != null)
            return true;

        string lower = go.name.ToLowerInvariant();
        return lower.Contains("camera1person") ||
               lower.Contains("camera3person") ||
               lower.Contains("camerasystemv2") ||
               lower.Contains("thirdpersoncam") ||
               lower.Contains("firstpersoncam");
    }

    private static bool IsMovementPath(string path)
    {
        string lower = path.ToLowerInvariant();
        return lower.Contains("player_move") ||
               lower.Contains("playermove") ||
               lower.Contains("movement") ||
               lower.Contains("locomotion") ||
               lower.Contains("playerrigidbody");
    }

    private static int GetDepth(GameObject go)
    {
        int depth = 0;
        Transform t = go != null ? go.transform : null;
        while (t != null)
        {
            depth++;
            t = t.parent;
        }
        return depth;
    }

    private static void DeletePaths(HashSet<string> paths, List<string> reportList)
    {
        foreach (string path in paths.OrderByDescending(x => x.Length))
        {
            if (!IsManagedScriptPath(path))
                continue;

            if (AssetDatabase.DeleteAsset(path))
                reportList.Add(path);
        }
    }

    private static void RenamePrefixedClassesAndUpdateReferences(List<ScriptInfo> scripts, MigrationReport report)
    {
        Dictionary<string, string> renameMap = new Dictionary<string, string>(StringComparer.Ordinal);

        for (int i = 0; i < scripts.Count; i++)
        {
            ScriptInfo script = scripts[i];
            if (script.IsEditor || string.IsNullOrEmpty(script.ClassName))
                continue;

            if (!script.ClassName.StartsWith("MiniMarket", StringComparison.Ordinal))
                continue;

            string newName = script.ClassName.Substring("MiniMarket".Length).TrimStart('_');
            if (string.IsNullOrWhiteSpace(newName))
                newName = "Game" + script.ClassName;

            renameMap[script.ClassName] = newName;
        }

        if (renameMap.Count == 0)
            return;

        for (int i = 0; i < scripts.Count; i++)
        {
            ScriptInfo script = scripts[i];
            string updated = script.Content;

            foreach (KeyValuePair<string, string> pair in renameMap)
                updated = Regex.Replace(updated, @"\b" + Regex.Escape(pair.Key) + @"\b", pair.Value);

            if (!string.Equals(updated, script.Content, StringComparison.Ordinal))
                File.WriteAllText(script.Path, updated, new UTF8Encoding(false));
        }

        // Renomeia o arquivo principal preservando o .meta/GUID.
        foreach (ScriptInfo script in scripts)
        {
            if (!renameMap.TryGetValue(script.ClassName, out string newClassName))
                continue;

            string directory = NormalizePath(Path.GetDirectoryName(script.Path) ?? ScriptsRoot);
            string desiredPath = directory + "/" + newClassName + ".cs";

            if (!string.Equals(script.Path, desiredPath, StringComparison.OrdinalIgnoreCase))
            {
                desiredPath = AssetDatabase.GenerateUniqueAssetPath(desiredPath);
                string error = AssetDatabase.MoveAsset(script.Path, desiredPath);
                if (string.IsNullOrEmpty(error))
                {
                    report.RenamedClasses.Add(script.ClassName + " -> " + newClassName);
                    report.MovedScripts.Add(script.Path + " -> " + desiredPath);
                }
                else
                {
                    report.Warnings.Add("Falha ao renomear " + script.Path + ": " + error);
                }
            }
        }
    }

    private static void OrganizeRemainingScripts(List<ScriptInfo> scripts, MigrationReport report)
    {
        for (int i = 0; i < scripts.Count; i++)
        {
            ScriptInfo script = scripts[i];
            if (!IsManagedScriptPath(script.Path) || script.IsEditor)
                continue;

            string category = DetermineCategory(script);
            string folder = ScriptsRoot + "/" + category;
            EnsureFolder(folder);

            string currentFileName = Path.GetFileName(script.Path);
            string desiredName = !string.IsNullOrEmpty(script.ClassName)
                ? script.ClassName + ".cs"
                : currentFileName;

            string destination = folder + "/" + desiredName;
            if (string.Equals(script.Path, destination, StringComparison.OrdinalIgnoreCase))
                continue;

            destination = AssetDatabase.GenerateUniqueAssetPath(destination);
            string error = AssetDatabase.MoveAsset(script.Path, destination);

            if (string.IsNullOrEmpty(error))
                report.MovedScripts.Add(script.Path + " -> " + destination);
            else
                report.Warnings.Add("Falha ao mover " + script.Path + ": " + error);
        }
    }

    private static string DetermineCategory(ScriptInfo script)
    {
        string key = (script.Path + " " + script.FileName + " " + script.ClassName).ToLowerInvariant();

        if (key.Contains("database") || key.Contains("repository") || key.Contains("save") || key.Contains("profile"))
            return "Data";
        if (key.Contains("gold") || key.Contains("wallet") || key.Contains("econom") || key.Contains("currency"))
            return "Economy";
        if (key.Contains("menu") || key.Contains("pause"))
            return "Menus";
        if (key.Contains("hud") || key.Contains("ui") || key.Contains("panel") || key.Contains("button") || key.Contains("text"))
            return "UI";
        if (key.Contains("buy") || key.Contains("purchase") || key.Contains("shop") || key.Contains("store"))
            return "Purchasing";
        if (key.Contains("interact") || key.Contains("getitem") || key.Contains("grab") || key.Contains("pickup") || key.Contains("select"))
            return "Interaction";
        if (key.Contains("physics") || key.Contains("rigidbody") || key.Contains("collision"))
            return "Physics";
        if (key.Contains("stamina") || key.Contains("energy"))
            return "Stamina";
        if (key.Contains("debug") || key.Contains("diagnostic") || key.Contains("logger") || key.Contains("upgrade"))
            return "Diagnostics";
        if (key.Contains("performance") || key.Contains("optim") || key.Contains("quality") || key.Contains("lighting"))
            return "Performance";
        if (key.Contains("scene") || key.Contains("world") || key.Contains("city") || key.Contains("time") || key.Contains("day") || key.Contains("night"))
            return "World";
        if (key.Contains("config") || key.Contains("setting"))
            return "Configuration";

        if (!script.IsMonoBehaviour)
            return "Core";

        return "Gameplay";
    }

    private static void RemoveUnusedMonoBehaviours(List<ScriptInfo> scripts, MigrationReport report)
    {
        HashSet<string> usedGuids = FindSerializedScriptGuids();
        Dictionary<string, ScriptInfo> byClass = scripts
            .Where(x => !string.IsNullOrEmpty(x.ClassName))
            .GroupBy(x => x.ClassName)
            .ToDictionary(x => x.Key, x => x.First(), StringComparer.Ordinal);

        HashSet<ScriptInfo> reachable = new HashSet<ScriptInfo>();
        Queue<ScriptInfo> queue = new Queue<ScriptInfo>();

        for (int i = 0; i < scripts.Count; i++)
        {
            ScriptInfo script = scripts[i];
            bool root = usedGuids.Contains(script.Guid) ||
                        script.IsEditor ||
                        script.IsBootstrap ||
                        !script.IsMonoBehaviour ||
                        script.Content.Contains("ScriptableObject") ||
                        script.Content.Contains("[Serializable]") ||
                        script.Content.Contains("[System.Serializable]");

            if (!root || !reachable.Add(script))
                continue;

            queue.Enqueue(script);
        }

        while (queue.Count > 0)
        {
            ScriptInfo current = queue.Dequeue();
            foreach (KeyValuePair<string, ScriptInfo> pair in byClass)
            {
                ScriptInfo dependency = pair.Value;
                if (dependency == current || reachable.Contains(dependency))
                    continue;

                if (!ContainsWholeWord(current.Content, pair.Key))
                    continue;

                reachable.Add(dependency);
                queue.Enqueue(dependency);
            }
        }

        List<string> unused = scripts
            .Where(x => x.IsMonoBehaviour && !x.IsEditor && !reachable.Contains(x))
            .Select(x => x.Path)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        DeletePaths(new HashSet<string>(unused, StringComparer.OrdinalIgnoreCase), report.DeletedUnusedScripts);
    }

    private static HashSet<string> FindSerializedScriptGuids()
    {
        HashSet<string> result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        string[] extensions = { "*.unity", "*.prefab", "*.asset" };

        for (int e = 0; e < extensions.Length; e++)
        {
            string[] files;
            try
            {
                files = Directory.GetFiles("Assets", extensions[e], SearchOption.AllDirectories);
            }
            catch
            {
                continue;
            }

            for (int i = 0; i < files.Length; i++)
            {
                string path = NormalizePath(files[i]);
                if (IsIgnoredPath(path))
                    continue;

                string content;
                try
                {
                    content = File.ReadAllText(path);
                }
                catch
                {
                    continue;
                }

                MatchCollection matches = GuidRegex.Matches(content);
                for (int m = 0; m < matches.Count; m++)
                    result.Add(matches[m].Groups[1].Value);
            }
        }

        return result;
    }

    private static bool ContainsBootstrapMarker(string content)
    {
        return content.Contains("RuntimeInitializeOnLoadMethod") ||
               content.Contains("InitializeOnLoad") ||
               content.Contains("MenuItem(") ||
               content.Contains("DidReloadScripts") ||
               content.Contains("AssetPostprocessor");
    }

    private static bool ContainsWholeWord(string content, string word)
    {
        if (string.IsNullOrEmpty(content) || string.IsNullOrEmpty(word))
            return false;

        return Regex.IsMatch(content, @"\b" + Regex.Escape(word) + @"\b");
    }

    private static void EnsureFolder(string folder)
    {
        folder = NormalizePath(folder);
        if (AssetDatabase.IsValidFolder(folder))
            return;

        string parent = NormalizePath(Path.GetDirectoryName(folder) ?? "Assets");
        string name = Path.GetFileName(folder);
        EnsureFolder(parent);

        if (!AssetDatabase.IsValidFolder(folder))
            AssetDatabase.CreateFolder(parent, name);
    }

    private static bool IsManagedScriptPath(string path)
    {
        path = NormalizePath(path);
        return path.StartsWith(ScriptsRoot + "/", StringComparison.OrdinalIgnoreCase) &&
               path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) &&
               !IsIgnoredPath(path);
    }

    private static bool IsIgnoredPath(string path)
    {
        path = NormalizePath(path);
        return path.IndexOf("Brick Project Studio", StringComparison.OrdinalIgnoreCase) >= 0 ||
               path.IndexOf("/Packages/", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static string NormalizePath(string path)
    {
        return string.IsNullOrEmpty(path) ? string.Empty : path.Replace('\\', '/');
    }

    private static void WriteReport(MigrationReport report)
    {
        EnsureFolder(ScriptsRoot);

        StringBuilder sb = new StringBuilder();
        sb.AppendLine("# Organização dos Scripts");
        sb.AppendLine();
        sb.AppendLine("Migração executada em: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        sb.AppendLine();
        sb.AppendLine("## Regras aplicadas");
        sb.AppendLine();
        sb.AppendLine("- Sistemas próprios de câmera e movimentação removidos.");
        sb.AppendLine("- Componentes e objetos correspondentes removidos de cenas e prefabs.");
        sb.AppendLine("- Scripts `MiniMarket*.cs` restantes renomeados sem o prefixo.");
        sb.AppendLine("- Scripts restantes organizados por função.");
        sb.AppendLine("- MonoBehaviours sem uso serializado e sem dependentes removidos.");
        sb.AppendLine("- Pastas `Brick Project Studio` ignoradas integralmente.");
        sb.AppendLine();

        AppendSection(sb, "Scripts de câmera/movimentação removidos", report.DeletedCameraMovementScripts);
        AppendSection(sb, "Scripts não utilizados removidos", report.DeletedUnusedScripts);
        AppendSection(sb, "Classes renomeadas", report.RenamedClasses);
        AppendSection(sb, "Scripts movidos", report.MovedScripts);
        AppendSection(sb, "Cenas limpas", report.CleanedScenes);
        AppendSection(sb, "Prefabs limpos", report.CleanedPrefabs);
        AppendSection(sb, "Avisos", report.Warnings);

        sb.AppendLine("## Componentes removidos");
        sb.AppendLine();
        sb.AppendLine("- GameObjects de câmera: " + report.RemovedGameObjects);
        sb.AppendLine("- Componentes: " + report.RemovedComponents);
        sb.AppendLine("- CharacterControllers ligados à movimentação removida: " + report.RemovedCharacterControllers);
        sb.AppendLine("- Missing Scripts removidos: " + report.RemovedMissingScripts);

        File.WriteAllText(ReportPath, sb.ToString(), new UTF8Encoding(false));
    }

    private static void AppendSection(StringBuilder sb, string title, List<string> items)
    {
        sb.AppendLine("## " + title);
        sb.AppendLine();

        if (items.Count == 0)
        {
            sb.AppendLine("- Nenhum.");
        }
        else
        {
            for (int i = 0; i < items.Count; i++)
                sb.AppendLine("- `" + items[i] + "`");
        }

        sb.AppendLine();
    }
}
#endif
