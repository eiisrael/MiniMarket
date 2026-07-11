#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>
/// Monta a arquitetura definitiva do jogador:
/// Character 01 + CameraRelativeMovement + uma única PlayerCameraRig + GetItemController.
/// Remove componentes e objetos legados de câmera/movimentação da cena.
/// </summary>
[InitializeOnLoad]
public static class PlayerSystemSetupWizard
{
    private const string SetupKey = "MiniMarket.PlayerSystemSetup.2026-07-11.v1";
    private const string CameraRigName = "PlayerCameraRig";
    private const string CameraTargetName = "CameraTarget";
    private const string EyePointName = "FirstPersonEye";

    private static readonly HashSet<string> LegacyObjectNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "Main Camera",
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

    private static readonly HashSet<string> LegacyComponentNames = new HashSet<string>(StringComparer.Ordinal)
    {
        "PlayerMove",
        "CameraV2Controller",
        "Camera3Person",
        "Camera1Person",
        "CameraV2MenuInputBlocker",
        "CameraV2LegacyBlocker",
        "CameraV2F10Diagnostics",
        "CameraGTAFollowHardcore",
        "CrosshairAim",
        "CAM_GetItem",
        "GetItemV2",
        "PlayerObjectGrabberHardcore",
        "MiniMarketGrabberRuntimeStabilizer",
        "MiniMarketCameraCollisionSmoother",
        "MiniMarketCameraFrameSpikeGuard",
        "MiniMarketCameraPerspectiveSwitcher",
        "MiniMarketCameraRealtimeAnomalyLogger",
        "MiniMarketFirstPersonCameraStabilizer",
        "MiniMarketCameraDiagnosticsTuner",
        "MiniMarketCameraAuthorityStabilizer",
        "MiniMarketPlayerRigidbodyPusher"
    };

    static PlayerSystemSetupWizard()
    {
        EditorApplication.delayCall += TryAutoSetup;
    }

    [MenuItem("Tools/Player System/Create or Repair Player System", priority = 1)]
    public static void CreateOrRepairFromMenu()
    {
        EditorPrefs.DeleteKey(SetupKey);
        CreateOrRepair();
    }

    [MenuItem("Tools/Player System/Reset Automatic Setup Marker", priority = 20)]
    public static void ResetSetupMarker()
    {
        EditorPrefs.DeleteKey(SetupKey);
        Debug.Log("[PlayerSystemSetup] Marcador apagado. O setup poderá executar novamente.");
    }

    private static void TryAutoSetup()
    {
        if (EditorPrefs.GetBool(SetupKey, false))
            return;

        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            EditorApplication.delayCall += TryAutoSetup;
            return;
        }

        if (!SceneManager.GetActiveScene().IsValid() || string.IsNullOrEmpty(SceneManager.GetActiveScene().path))
            return;

        CreateOrRepair();
    }

    private static void CreateOrRepair()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("[PlayerSystemSetup] Saia do Play Mode antes de executar o setup.");
            return;
        }

        GameObject player = FindPlayer();
        if (player == null)
        {
            Debug.LogError(
                "[PlayerSystemSetup] Jogador não encontrado. Selecione o objeto principal do personagem " +
                "e execute Tools > Player System > Create or Repair Player System."
            );
            return;
        }

        try
        {
            EditorSceneManager.SaveOpenScenes();
            CrossSceneReferenceCleaner.CleanLoadedScenes();

            MigrateGrabbableMarkers();
            RemoveLegacyPlayerComponents(player);
            RemoveLegacyCameraObjects();

            CharacterController characterController = EnsureCharacterController(player);
            CameraRelativeMovement movement = EnsureComponent<CameraRelativeMovement>(player);
            movement.animator = player.GetComponentInChildren<Animator>(true);

            Transform cameraTarget = EnsureChild(player.transform, CameraTargetName, new Vector3(0f, 1.45f, 0f));
            Transform eyePoint = EnsureChild(player.transform, EyePointName, new Vector3(0f, 1.68f, 0.08f));

            GameObject cameraRig = new GameObject(CameraRigName);
            cameraRig.transform.SetPositionAndRotation(player.transform.position, player.transform.rotation);
            cameraRig.tag = "MainCamera";

            Camera camera = cameraRig.AddComponent<Camera>();
            camera.fieldOfView = 60f;
            camera.nearClipPlane = 0.03f;
            camera.farClipPlane = 1000f;
            cameraRig.AddComponent<AudioListener>();

            ThirdPersonCamera thirdPerson = cameraRig.AddComponent<ThirdPersonCamera>();
            thirdPerson.target = player.transform;
            thirdPerson.lookTarget = cameraTarget;

            FirstPersonCamera firstPerson = cameraRig.AddComponent<FirstPersonCamera>();
            firstPerson.playerBody = player.transform;
            firstPerson.eyePoint = eyePoint;

            PlayerCameraController cameraController = cameraRig.AddComponent<PlayerCameraController>();
            cameraController.gameCamera = camera;
            cameraController.thirdPerson = thirdPerson;
            cameraController.firstPerson = firstPerson;
            cameraController.player = player.transform;
            cameraController.movement = movement;
            cameraController.renderersHiddenInFirstPerson = player.GetComponentsInChildren<Renderer>(true);

            GetItemController getItem = cameraRig.AddComponent<GetItemController>();
            getItem.cameraSource = camera;
            getItem.playerRoot = player.transform;
            getItem.cameraController = cameraController;

            movement.cameraTransform = camera.transform;
            movement.cameraMode = null;

            Selection.activeGameObject = cameraRig;
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveOpenScenes();
            AssetDatabase.SaveAssets();

            EditorPrefs.SetBool(SetupKey, true);

            Debug.Log(
                "[PlayerSystemSetup] Sistema criado com sucesso. Player=" + player.name +
                ", CharacterController=" + (characterController != null) +
                ", CameraRig=" + CameraRigName + "."
            );
        }
        catch (Exception exception)
        {
            Debug.LogError("[PlayerSystemSetup] Falha durante o setup: " + exception);
        }
    }

    private static GameObject FindPlayer()
    {
        GameObject selected = Selection.activeGameObject;
        if (selected != null && IsPlayerCandidate(selected))
            return selected.transform.root.gameObject;

        CameraRelativeMovement existingMovement = Object.FindAnyObjectByType<CameraRelativeMovement>(FindObjectsInactive.Include);
        if (existingMovement != null)
            return existingMovement.gameObject;

        GameObject[] allObjects = Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        GameObject best = null;
        int bestScore = int.MinValue;

        for (int i = 0; i < allObjects.Length; i++)
        {
            GameObject candidate = allObjects[i];
            if (candidate == null || !candidate.scene.IsValid() || candidate.scene.name == "DontDestroyOnLoad")
                continue;

            int score = ScorePlayerCandidate(candidate);
            if (score <= bestScore)
                continue;

            bestScore = score;
            best = candidate;
        }

        return bestScore >= 20 ? best : null;
    }

    private static bool IsPlayerCandidate(GameObject candidate)
    {
        return ScorePlayerCandidate(candidate) >= 20;
    }

    private static int ScorePlayerCandidate(GameObject candidate)
    {
        if (candidate == null)
            return int.MinValue;

        int score = 0;
        string lowerName = candidate.name.ToLowerInvariant();

        if (lowerName == "character 01") score += 100;
        if (lowerName.Contains("player")) score += 60;
        if (lowerName.Contains("character")) score += 40;
        if (candidate.GetComponent<CharacterController>() != null) score += 50;
        if (candidate.GetComponentInChildren<Animator>(true) != null) score += 25;
        if (candidate.transform.parent == null) score += 10;
        if (candidate.GetComponent<Camera>() != null) score -= 100;
        if (candidate.GetComponent<Canvas>() != null) score -= 100;

        return score;
    }

    private static void RemoveLegacyCameraObjects()
    {
        GameObject[] allObjects = Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        List<GameObject> toDelete = new List<GameObject>();

        for (int i = 0; i < allObjects.Length; i++)
        {
            GameObject target = allObjects[i];
            if (target == null || !target.scene.IsValid() || target.scene.name == "DontDestroyOnLoad")
                continue;

            if (target.name == CameraRigName)
            {
                toDelete.Add(target);
                continue;
            }

            bool legacyName = LegacyObjectNames.Contains(target.name);
            bool hasCamera = target.GetComponent<Camera>() != null;
            bool hasLegacyComponent = HasLegacyComponent(target);

            if (legacyName || hasCamera || hasLegacyComponent)
                toDelete.Add(target);
        }

        foreach (GameObject target in toDelete.Distinct().OrderByDescending(GetHierarchyDepth))
        {
            if (target != null)
                Object.DestroyImmediate(target);
        }
    }

    private static void RemoveLegacyPlayerComponents(GameObject player)
    {
        MonoBehaviour[] behaviours = Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        for (int i = 0; i < behaviours.Length; i++)
        {
            MonoBehaviour behaviour = behaviours[i];
            if (behaviour == null)
                continue;

            string typeName = behaviour.GetType().Name;
            if (!LegacyComponentNames.Contains(typeName))
                continue;

            Object.DestroyImmediate(behaviour);
        }

        CameraRelativeMovement[] duplicateMovements = player.GetComponents<CameraRelativeMovement>();
        for (int i = 1; i < duplicateMovements.Length; i++)
            Object.DestroyImmediate(duplicateMovements[i]);
    }

    private static bool HasLegacyComponent(GameObject target)
    {
        MonoBehaviour[] behaviours = target.GetComponents<MonoBehaviour>();
        for (int i = 0; i < behaviours.Length; i++)
        {
            MonoBehaviour behaviour = behaviours[i];
            if (behaviour != null && LegacyComponentNames.Contains(behaviour.GetType().Name))
                return true;
        }

        return false;
    }

    private static void MigrateGrabbableMarkers()
    {
        MonoBehaviour[] behaviours = Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < behaviours.Length; i++)
        {
            MonoBehaviour behaviour = behaviours[i];
            if (behaviour == null || behaviour.GetType().Name != "GetItemObjectV2")
                continue;

            GameObject target = behaviour.gameObject;
            if (target.GetComponent<GrabbableItem>() == null)
                target.AddComponent<GrabbableItem>();

            Object.DestroyImmediate(behaviour);
        }
    }

    private static CharacterController EnsureCharacterController(GameObject player)
    {
        CharacterController controller = player.GetComponent<CharacterController>();
        if (controller == null)
            controller = player.AddComponent<CharacterController>();

        if (controller.height <= 0.5f)
            controller.height = 1.8f;
        if (controller.radius <= 0.05f)
            controller.radius = 0.32f;

        controller.center = new Vector3(0f, controller.height * 0.5f, 0f);
        controller.stepOffset = Mathf.Min(0.35f, controller.height * 0.25f);
        controller.slopeLimit = 50f;
        controller.skinWidth = Mathf.Max(0.03f, controller.radius * 0.1f);
        controller.minMoveDistance = 0f;
        return controller;
    }

    private static T EnsureComponent<T>(GameObject target) where T : Component
    {
        T component = target.GetComponent<T>();
        return component != null ? component : target.AddComponent<T>();
    }

    private static Transform EnsureChild(Transform parent, string childName, Vector3 localPosition)
    {
        Transform child = parent.Find(childName);
        if (child == null)
        {
            GameObject childObject = new GameObject(childName);
            child = childObject.transform;
            child.SetParent(parent, false);
        }

        child.localPosition = localPosition;
        child.localRotation = Quaternion.identity;
        child.localScale = Vector3.one;
        return child;
    }

    private static int GetHierarchyDepth(GameObject target)
    {
        int depth = 0;
        Transform current = target != null ? target.transform : null;
        while (current != null)
        {
            depth++;
            current = current.parent;
        }
        return depth;
    }
}
#endif
