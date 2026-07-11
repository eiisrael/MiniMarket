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
/// Limpeza estrutural solicitada para o projeto.
///
/// Executa uma vez depois do git pull:
/// - remove sistemas próprios de câmera e movimentação;
/// - remove componentes/objetos correspondentes de cenas e prefabs;
/// - remove MonoBehaviours sem uso serializado nem dependentes;
/// - remove o prefixo MiniMarket de classes/arquivos restantes;
/// - organiza Assets/Scripts por responsabilidade;
/// - ignora completamente Brick Project Studio.
/// </summary>
[InitializeOnLoad]
public static class ScriptProjectOrganizer
{
    private const string MigrationKey = "MiniMarket.ScriptOrganization.2026-07-11.v2";
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

    private static readonly string[] CameraMovementTokens =
    {
        "camera3person", "camera1person", "thirdperson", "firstperson",
        "camerav2", "cameragta", "mouselook", "crosshair", "pov",
        "player_move", "playermove", "playermovement", "charactermovement",
        "movementcontroller", "locomotion", "playerrigidbody",
        "cameracollision", "cameraframe", "cameraperspective",
        "cameraauthority", "camerastabilizer", "cameradiagnostic",
        "camerarealtime", "legacyblocker", "menuinputblocker",
        "playershutdownguard"
    };

    private static readonly HashSet<string> CameraObjectNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "Main Camera", "Camera", "CameraSystemV2", "Camera1Person", "Camera3Person",
        "FirstPersonCAM", "ThirdPersonCAM", "POV",
        "MiniMarket_CameraV2MenuInputBlocker",
        "MiniMarket_CameraV2LegacyBlocker",
        "MiniMarket_CameraV2F10Diagnostics"
    };

    private sealed class ScriptInfo
    {
        public string Path;
        public string Guid;
        public string ClassName;
        public string Content;
        public bool IsMonoBehaviour;
        public bool IsEditor;
        public bool IsBootstrap;
        public bool IsCameraMovement;
    }

    private sealed class Report
    {
        public readonly List<string> CameraMovementDeleted = new List<string>();
        public readonly List<string> UnusedDeleted = new List<string>();
        public readonly List<string> Renamed = new List<string>();
        public readonly List<string> Moved = new List<string>();
        public readonly List<string> Scenes = new List<string>();
        public readonly List<string> Prefabs = new List<string>();
        public readonly List<string> Warnings = new List<string>();
        public int RemovedObjects;
        public int RemovedComponents;
        public int RemovedControllers;
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
        Run();
    }

    [MenuItem("Tools/Project Maintenance/Reset Script Cleanup Marker", priority = 20)]
    public static void ResetMarker()
    {
        EditorPrefs.DeleteKey(MigrationKey);
        Debug.Log("[ScriptProjectOrganizer] Marcador da migração apagado.");
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

        Run();
    }

    private static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("[ScriptProjectOrganizer] Saia do Play Mode antes da limpeza.");
            return;
        }

        Report report = new Report();
        string originalScene = SceneManager.GetActiveScene().path;
        bool reloadLocked = false;

        try
        {
            Debug.Log("[ScriptProjectOrganizer] Iniciando limpeza e organização...");
            EditorSceneManager.SaveOpenScenes();

            List<ScriptInfo> scripts = LoadScripts();
            HashSet<string> deleteSet = BuildCameraMovementDeleteSet(scripts);

            CleanupScenesAndPrefabs(deleteSet, report);

            EditorApplication.LockReloadAssemblies();
            reloadLocked = true;

            DeleteAssets(deleteSet, report.CameraMovementDeleted);
            AssetDatabase.SaveAssets();

            scripts = LoadScriptsFromDisk();
            RenameMiniMarketClasses(scripts, report);
            AssetDatabase.SaveAssets();

            scripts = LoadScriptsFromDisk();
            OrganizeScripts(scripts, report);
            AssetDatabase.SaveAssets();

            scripts = LoadScriptsFromDisk();
            RemoveUnusedScripts(scripts, report);
            WriteReport(report);

            AssetDatabase.SaveAssets();

            EditorApplication.UnlockReloadAssemblies();
            reloadLocked = false;
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);

            if (!string.IsNullOrEmpty(originalScene) && File.Exists(originalScene))
                EditorSceneManager.OpenScene(originalScene, OpenSceneMode.Single);

            EditorPrefs.SetBool(MigrationKey, true);

            Debug.Log(
                "[ScriptProjectOrganizer] Concluído. " +
                "Câmera/movimento: " + report.CameraMovementDeleted.Count +
                ", sem uso: " + report.UnusedDeleted.Count +
                ", movidos: " + report.Moved.Count +
                ". Relatório: " + ReportPath
            );
        }
        catch (Exception ex)
        {
            Debug.LogError("[ScriptProjectOrganizer] Falha: " + ex);
        }
        finally
        {
            if (reloadLocked)
                EditorApplication.UnlockReloadAssemblies();

            AssetDatabase.Refresh();
        }
    }

    private static List<ScriptInfo> LoadScripts()
    {
        string[] guids = AssetDatabase.FindAssets("t:MonoScript", new[] { ScriptsRoot });
        List<ScriptInfo> result = new List<ScriptInfo>();

        for (int i = 0; i < guids.Length; i++)
        {
            string path = Normalize(AssetDatabase.GUIDToAssetPath(guids[i]));
            if (!IsManagedScript(path) || !File.Exists(path))
                continue;

            ScriptInfo info = ReadScript(path);
            if (info != null)
                result.Add(info);
        }

        return result;
    }

    private static List<ScriptInfo> LoadScriptsFromDisk()
    {
        if (!Directory.Exists(ScriptsRoot))
            return new List<ScriptInfo>();

        return Directory
            .GetFiles(ScriptsRoot, "*.cs", SearchOption.AllDirectories)
            .Select(Normalize)
            .Where(IsManagedScript)
            .Select(ReadScript)
            .Where(x => x != null)
            .ToList();
    }

    private static ScriptInfo ReadScript(string path)
    {
        try
        {
            string content = File.ReadAllText(path, Encoding.UTF8);
            Match match = ClassRegex.Match(content);
            string className = match.Success ? match.Groups[1].Value : Path.GetFileNameWithoutExtension(path);

            return new ScriptInfo
            {
                Path = path,
                Guid = AssetDatabase.AssetPathToGUID(path),
                ClassName = className,
                Content = content,
                IsMonoBehaviour = Regex.IsMatch(content, @"\b:\s*(?:[A-Za-z_]\w*\.)*MonoBehaviour\b"),
                IsEditor = path.IndexOf("/Editor/", StringComparison.OrdinalIgnoreCase) >= 0,
                IsBootstrap = ContainsBootstrap(content),
                IsCameraMovement = IsCameraMovement(path, className)
            };
        }
        catch
        {
            return null;
        }
    }

    private static HashSet<string> BuildCameraMovementDeleteSet(List<ScriptInfo> scripts)
    {
        HashSet<string> paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        HashSet<string> classes = new HashSet<string>(StringComparer.Ordinal);

        foreach (ScriptInfo script in scripts)
        {
            if (!script.IsCameraMovement)
                continue;

            paths.Add(script.Path);
            classes.Add(script.ClassName);
        }

        bool changed;
        do
        {
            changed = false;

            foreach (ScriptInfo script in scripts)
            {
                if (paths.Contains(script.Path) || script.IsEditor)
                    continue;

                foreach (string removedClass in classes.ToArray())
                {
                    if (!ContainsWord(script.Content, removedClass))
                        continue;

                    paths.Add(script.Path);
                    classes.Add(script.ClassName);
                    changed = true;
                    break;
                }
            }
        }
        while (changed);

        return paths;
    }

    private static bool IsCameraMovement(string path, string className)
    {
        string normalized = Normalize(path);
        if (normalized.StartsWith(ScriptsRoot + "/Camera/", StringComparison.OrdinalIgnoreCase) ||
            normalized.IndexOf("/Movement/", StringComparison.OrdinalIgnoreCase) >= 0 ||
            normalized.IndexOf("/Locomotion/", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return true;
        }

        string compact = Regex.Replace((normalized + className).ToLowerInvariant(), @"[^a-z0-9_]", string.Empty);
        for (int i = 0; i < CameraMovementTokens.Length; i++)
        {
            if (compact.Contains(CameraMovementTokens[i]))
                return true;
        }

        return false;
    }

    private static void CleanupScenesAndPrefabs(HashSet<string> deleteSet, Report report)
    {
        string[] scenes = AssetDatabase.FindAssets("t:Scene", new[] { "Assets" });
        foreach (string guid in scenes)
        {
            string path = Normalize(AssetDatabase.GUIDToAssetPath(guid));
            if (IsIgnored(path))
                continue;

            try
            {
                Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                bool changed = false;
                foreach (GameObject root in scene.GetRootGameObjects())
                    changed |= CleanupHierarchy(root, deleteSet, report, false);

                if (changed)
                {
                    EditorSceneManager.MarkSceneDirty(scene);
                    EditorSceneManager.SaveScene(scene);
                    report.Scenes.Add(path);
                }
            }
            catch (Exception ex)
            {
                report.Warnings.Add("Cena: " + path + " | " + ex.Message);
            }
        }

        string[] prefabs = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" });
        foreach (string guid in prefabs)
        {
            string path = Normalize(AssetDatabase.GUIDToAssetPath(guid));
            if (IsIgnored(path))
                continue;

            GameObject root = null;
            try
            {
                root = PrefabUtility.LoadPrefabContents(path);
                if (CleanupHierarchy(root, deleteSet, report, true))
                {
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    report.Prefabs.Add(path);
                }
            }
            catch (Exception ex)
            {
                report.Warnings.Add("Prefab: " + path + " | " + ex.Message);
            }
            finally
            {
                if (root != null)
                    PrefabUtility.UnloadPrefabContents(root);
            }
        }
    }

    private static bool CleanupHierarchy(GameObject root, HashSet<string> deleteSet, Report report, bool prefabRoot)
    {
        if (root == null)
            return false;

        bool changed = false;
        Transform[] all = root.GetComponentsInChildren<Transform>(true);
        List<GameObject> deleteObjects = new List<GameObject>();
        List<Component> deleteComponents = new List<Component>();
        HashSet<GameObject> movementHosts = new HashSet<GameObject>();

        foreach (Transform transform in all)
        {
            GameObject go = transform.gameObject;
            bool cameraObject = IsCameraObject(go);

            if (cameraObject && go != root)
            {
                deleteObjects.Add(go);
                continue;
            }

            Component[] components = go.GetComponents<Component>();
            foreach (Component component in components)
            {
                if (component == null)
                    continue;

                if (component is MonoBehaviour behaviour)
                {
                    MonoScript script = MonoScript.FromMonoBehaviour(behaviour);
                    string scriptPath = script != null ? Normalize(AssetDatabase.GetAssetPath(script)) : string.Empty;
                    if (!string.IsNullOrEmpty(scriptPath) && deleteSet.Contains(scriptPath))
                    {
                        deleteComponents.Add(component);
                        if (IsMovementPath(scriptPath))
                            movementHosts.Add(go);
                        continue;
                    }
                }

                string typeName = component.GetType().Name;
                if (component is Camera || component is AudioListener ||
                    typeName.IndexOf("AdditionalCameraData", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    deleteComponents.Add(component);
                }
            }
        }

        foreach (GameObject go in deleteObjects.Where(x => x != null).Distinct().OrderByDescending(GetDepth))
        {
            Object.DestroyImmediate(go);
            report.RemovedObjects++;
            changed = true;
        }

        foreach (Component component in deleteComponents.Where(x => x != null).Distinct())
        {
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
                report.RemovedControllers++;
                changed = true;
            }
        }

        foreach (Transform transform in root.GetComponentsInChildren<Transform>(true))
        {
            int removed = GameObjectUtility.RemoveMonoBehavioursWithMissingScript(transform.gameObject);
            if (removed <= 0)
                continue;

            report.RemovedMissingScripts += removed;
            changed = true;
        }

        return changed;
    }

    private static bool IsCameraObject(GameObject go)
    {
        if (go == null)
            return false;

        if (CameraObjectNames.Contains(go.name) || go.GetComponent<Camera>() != null)
            return true;

        string lower = go.name.ToLowerInvariant();
        return lower.Contains("camera1person") || lower.Contains("camera3person") ||
               lower.Contains("camerasystem") || lower.Contains("thirdpersoncam") ||
               lower.Contains("firstpersoncam");
    }

    private static bool IsMovementPath(string path)
    {
        string lower = path.ToLowerInvariant();
        return lower.Contains("player_move") || lower.Contains("playermove") ||
               lower.Contains("movement") || lower.Contains("locomotion") ||
               lower.Contains("playerrigidbody");
    }

    private static int GetDepth(GameObject go)
    {
        int depth = 0;
        Transform current = go != null ? go.transform : null;
        while (current != null)
        {
            depth++;
            current = current.parent;
        }
        return depth;
    }

    private static void DeleteAssets(IEnumerable<string> paths, List<string> output)
    {
        foreach (string path in paths.Distinct(StringComparer.OrdinalIgnoreCase).OrderByDescending(x => x.Length))
        {
            if (!IsManagedScript(path))
                continue;

            if (AssetDatabase.DeleteAsset(path))
                output.Add(path);
        }
    }

    private static void RenameMiniMarketClasses(List<ScriptInfo> scripts, Report report)
    {
        Dictionary<string, string> map = new Dictionary<string, string>(StringComparer.Ordinal);
        HashSet<string> existing = new HashSet<string>(scripts.Select(x => x.ClassName), StringComparer.Ordinal);

        foreach (ScriptInfo script in scripts)
        {
            if (script.IsEditor || string.IsNullOrEmpty(script.ClassName) ||
                !script.ClassName.StartsWith("MiniMarket", StringComparison.Ordinal))
            {
                continue;
            }

            string baseName = script.ClassName.Substring("MiniMarket".Length).TrimStart('_');
            if (string.IsNullOrWhiteSpace(baseName))
                baseName = "GameSystem";

            string candidate = baseName;
            int suffix = 2;
            while (existing.Contains(candidate) || map.ContainsValue(candidate))
                candidate = baseName + suffix++;

            map[script.ClassName] = candidate;
            existing.Add(candidate);
        }

        if (map.Count == 0)
            return;

        foreach (ScriptInfo script in scripts)
        {
            string content = script.Content;
            foreach (KeyValuePair<string, string> pair in map)
                content = Regex.Replace(content, @"\b" + Regex.Escape(pair.Key) + @"\b", pair.Value);

            if (!string.Equals(content, script.Content, StringComparison.Ordinal))
                File.WriteAllText(script.Path, content, new UTF8Encoding(false));
        }

        foreach (ScriptInfo script in scripts)
        {
            if (!map.TryGetValue(script.ClassName, out string newName))
                continue;

            string directory = Normalize(Path.GetDirectoryName(script.Path) ?? ScriptsRoot);
            string destination = AssetDatabase.GenerateUniqueAssetPath(directory + "/" + newName + ".cs");
            string error = AssetDatabase.MoveAsset(script.Path, destination);

            if (string.IsNullOrEmpty(error))
            {
                report.Renamed.Add(script.ClassName + " -> " + newName);
                report.Moved.Add(script.Path + " -> " + destination);
            }
            else
            {
                report.Warnings.Add("Renomear " + script.Path + ": " + error);
            }
        }
    }

    private static void OrganizeScripts(List<ScriptInfo> scripts, Report report)
    {
        foreach (ScriptInfo script in scripts)
        {
            if (script.IsEditor || !IsManagedScript(script.Path))
                continue;

            string folder = ScriptsRoot + "/" + Category(script);
            EnsureFolder(folder);

            string fileName = string.IsNullOrEmpty(script.ClassName)
                ? Path.GetFileName(script.Path)
                : script.ClassName + ".cs";

            string destination = folder + "/" + fileName;
            if (string.Equals(script.Path, destination, StringComparison.OrdinalIgnoreCase))
                continue;

            destination = AssetDatabase.GenerateUniqueAssetPath(destination);
            string error = AssetDatabase.MoveAsset(script.Path, destination);

            if (string.IsNullOrEmpty(error))
                report.Moved.Add(script.Path + " -> " + destination);
            else
                report.Warnings.Add("Mover " + script.Path + ": " + error);
        }
    }

    private static string Category(ScriptInfo script)
    {
        string key = (script.Path + " " + script.ClassName).ToLowerInvariant();

        if (key.Contains("database") || key.Contains("repository") || key.Contains("profile") || key.Contains("save")) return "Data";
        if (key.Contains("gold") || key.Contains("wallet") || key.Contains("currency") || key.Contains("econom")) return "Economy";
        if (key.Contains("menu") || key.Contains("pause")) return "Menus";
        if (key.Contains("hud") || key.Contains("ui") || key.Contains("panel") || key.Contains("button") || key.Contains("text")) return "UI";
        if (key.Contains("buy") || key.Contains("purchase") || key.Contains("store") || key.Contains("shop")) return "Purchasing";
        if (key.Contains("interact") || key.Contains("getitem") || key.Contains("grab") || key.Contains("pickup") || key.Contains("select")) return "Interaction";
        if (key.Contains("physics") || key.Contains("rigidbody") || key.Contains("collision")) return "Physics";
        if (key.Contains("stamina") || key.Contains("energy")) return "Stamina";
        if (key.Contains("debug") || key.Contains("diagnostic") || key.Contains("logger") || key.Contains("upgrade")) return "Diagnostics";
        if (key.Contains("performance") || key.Contains("optim") || key.Contains("quality") || key.Contains("lighting")) return "Performance";
        if (key.Contains("scene") || key.Contains("world") || key.Contains("city") || key.Contains("solar") || key.Contains("day") || key.Contains("night")) return "World";
        if (key.Contains("config") || key.Contains("setting")) return "Configuration";
        if (!script.IsMonoBehaviour) return "Core";
        return "Gameplay";
    }

    private static void RemoveUnusedScripts(List<ScriptInfo> scripts, Report report)
    {
        HashSet<string> serializedGuids = FindSerializedGuids();
        Dictionary<string, ScriptInfo> byClass = scripts
            .Where(x => !string.IsNullOrEmpty(x.ClassName))
            .GroupBy(x => x.ClassName)
            .ToDictionary(x => x.Key, x => x.First(), StringComparer.Ordinal);

        HashSet<ScriptInfo> reachable = new HashSet<ScriptInfo>();
        Queue<ScriptInfo> queue = new Queue<ScriptInfo>();

        foreach (ScriptInfo script in scripts)
        {
            bool root = serializedGuids.Contains(script.Guid) || script.IsEditor || script.IsBootstrap ||
                        !script.IsMonoBehaviour || script.Content.Contains("ScriptableObject") ||
                        script.Content.Contains("[Serializable]") || script.Content.Contains("[System.Serializable]");

            if (root && reachable.Add(script))
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

                if (!ContainsWord(current.Content, pair.Key))
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

        DeleteAssets(unused, report.UnusedDeleted);
    }

    private static HashSet<string> FindSerializedGuids()
    {
        HashSet<string> result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        string[] patterns = { "*.unity", "*.prefab", "*.asset" };

        foreach (string pattern in patterns)
        {
            string[] files;
            try
            {
                files = Directory.GetFiles("Assets", pattern, SearchOption.AllDirectories);
            }
            catch
            {
                continue;
            }

            foreach (string rawPath in files)
            {
                string path = Normalize(rawPath);
                if (IsIgnored(path))
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

                foreach (Match match in GuidRegex.Matches(content))
                    result.Add(match.Groups[1].Value);
            }
        }

        return result;
    }

    private static bool ContainsBootstrap(string content)
    {
        return content.Contains("RuntimeInitializeOnLoadMethod") ||
               content.Contains("InitializeOnLoad") || content.Contains("MenuItem(") ||
               content.Contains("DidReloadScripts") || content.Contains("AssetPostprocessor");
    }

    private static bool ContainsWord(string content, string word)
    {
        return !string.IsNullOrEmpty(content) && !string.IsNullOrEmpty(word) &&
               Regex.IsMatch(content, @"\b" + Regex.Escape(word) + @"\b");
    }

    private static void EnsureFolder(string folder)
    {
        folder = Normalize(folder);
        if (AssetDatabase.IsValidFolder(folder))
            return;

        string parent = Normalize(Path.GetDirectoryName(folder) ?? "Assets");
        EnsureFolder(parent);

        if (!AssetDatabase.IsValidFolder(folder))
            AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
    }

    private static bool IsManagedScript(string path)
    {
        path = Normalize(path);
        return path.StartsWith(ScriptsRoot + "/", StringComparison.OrdinalIgnoreCase) &&
               path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) && !IsIgnored(path);
    }

    private static bool IsIgnored(string path)
    {
        path = Normalize(path);
        return path.IndexOf("Brick Project Studio", StringComparison.OrdinalIgnoreCase) >= 0 ||
               path.IndexOf("/Packages/", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static string Normalize(string path)
    {
        return string.IsNullOrEmpty(path) ? string.Empty : path.Replace('\\', '/');
    }

    private static void WriteReport(Report report)
    {
        EnsureFolder(ScriptsRoot);
        StringBuilder text = new StringBuilder();
        text.AppendLine("# Relatório de Organização dos Scripts");
        text.AppendLine();
        text.AppendLine("Executado em: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        text.AppendLine();
        text.AppendLine("- Sistemas de câmera e movimentação removidos.");
        text.AppendLine("- Cenas e prefabs limpos.");
        text.AppendLine("- Prefixo `MiniMarket` removido das classes restantes.");
        text.AppendLine("- Scripts organizados por função.");
        text.AppendLine("- Brick Project Studio ignorado.");
        text.AppendLine();

        Append(text, "Câmera/movimentação removidos", report.CameraMovementDeleted);
        Append(text, "Sem uso removidos", report.UnusedDeleted);
        Append(text, "Renomeados", report.Renamed);
        Append(text, "Movidos", report.Moved);
        Append(text, "Cenas limpas", report.Scenes);
        Append(text, "Prefabs limpos", report.Prefabs);
        Append(text, "Avisos", report.Warnings);

        text.AppendLine("## Objetos e componentes");
        text.AppendLine();
        text.AppendLine("- Objetos de câmera removidos: " + report.RemovedObjects);
        text.AppendLine("- Componentes removidos: " + report.RemovedComponents);
        text.AppendLine("- CharacterControllers removidos: " + report.RemovedControllers);
        text.AppendLine("- Missing Scripts removidos: " + report.RemovedMissingScripts);

        File.WriteAllText(ReportPath, text.ToString(), new UTF8Encoding(false));
    }

    private static void Append(StringBuilder text, string title, List<string> items)
    {
        text.AppendLine("## " + title);
        text.AppendLine();

        if (items.Count == 0)
            text.AppendLine("- Nenhum.");
        else
            foreach (string item in items)
                text.AppendLine("- `" + item + "`");

        text.AppendLine();
    }
}
#endif
