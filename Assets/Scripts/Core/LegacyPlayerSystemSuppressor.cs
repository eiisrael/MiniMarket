using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Proteção temporária de migração.
/// Impede que bootstraps antigos de câmera/GetItem sejam recriados por
/// RuntimeInitializeOnLoadMethod depois que a cena foi convertida para o novo sistema.
/// </summary>
[DefaultExecutionOrder(-50000)]
public sealed class LegacyPlayerSystemSuppressor : MonoBehaviour
{
    private static LegacyPlayerSystemSuppressor instance;

    private static readonly HashSet<string> LegacyTypes = new HashSet<string>(StringComparer.Ordinal)
    {
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
        "MiniMarketCameraAuthorityStabilizer"
    };

    private static readonly HashSet<string> LegacyRuntimeObjects = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "MiniMarket_CameraV2MenuInputBlocker",
        "MiniMarket_CameraV2LegacyBlocker",
        "MiniMarket_CameraV2F10Diagnostics"
    };

    private float stopAt;
    private float nextScan;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        instance = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void CreateBeforeScene()
    {
        if (instance != null)
            return;

        GameObject host = new GameObject("LegacyPlayerSystemSuppressor");
        DontDestroyOnLoad(host);
        instance = host.AddComponent<LegacyPlayerSystemSuppressor>();
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
        stopAt = Time.unscaledTime + 5f;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        if (instance == this)
            instance = null;
    }

    private void Update()
    {
        if (Time.unscaledTime > stopAt || Time.unscaledTime < nextScan)
            return;

        nextScan = Time.unscaledTime + 0.2f;
        RemoveLegacyInstances();
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        stopAt = Time.unscaledTime + 5f;
        nextScan = 0f;
        RemoveLegacyInstances();
    }

    private void RemoveLegacyInstances()
    {
        MonoBehaviour[] behaviours = FindObjectsByType<MonoBehaviour>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None
        );

        for (int i = 0; i < behaviours.Length; i++)
        {
            MonoBehaviour behaviour = behaviours[i];
            if (behaviour == null || behaviour == this)
                continue;

            if (LegacyTypes.Contains(behaviour.GetType().Name))
                Destroy(behaviour);
        }

        GameObject[] objects = FindObjectsByType<GameObject>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None
        );

        for (int i = 0; i < objects.Length; i++)
        {
            GameObject target = objects[i];
            if (target != null && target != gameObject && LegacyRuntimeObjects.Contains(target.name))
                Destroy(target);
        }
    }
}
