using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>
/// Controla automaticamente imagens de mira/crosshair da UI.
/// A mira aparece apenas em primeira pessoa e fica oculta em terceira pessoa,
/// menus e modo de compra.
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(26000)]
public sealed class FirstPersonReticleController : MonoBehaviour
{
    public static FirstPersonReticleController Instance { get; private set; }

    [Min(0.25f)] public float scanInterval = 1f;
    public bool hideWhenGameplayInputBlocked = true;

    private readonly List<Graphic> reticles = new List<Graphic>(8);
    private PlayerCameraController cameraController;
    private BuySceneCameraModeController purchaseController;
    private float nextScan;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Instance = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void CreateAfterSceneLoad()
    {
        FirstPersonReticleController existing = Object.FindAnyObjectByType<FirstPersonReticleController>(FindObjectsInactive.Include);
        if (existing != null)
        {
            Instance = existing;
            existing.Rescan();
            return;
        }

        GameObject host = new GameObject("FirstPersonReticleController");
        DontDestroyOnLoad(host);
        host.AddComponent<FirstPersonReticleController>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        SceneManager.sceneLoaded += HandleSceneLoaded;
        Rescan();
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        if (Instance == this)
            Instance = null;
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        nextScan = 0f;
        Rescan();
    }

    private void Update()
    {
        if (Time.unscaledTime >= nextScan)
        {
            nextScan = Time.unscaledTime + Mathf.Max(0.25f, scanInterval);
            Rescan();
        }

        bool show = cameraController != null && cameraController.IsFirstPerson;

        if (hideWhenGameplayInputBlocked && GameplayInputState.IsBlocked)
            show = false;

        if (purchaseController != null && purchaseController.ModoCompraAtivo)
            show = false;

        for (int i = reticles.Count - 1; i >= 0; i--)
        {
            Graphic graphic = reticles[i];
            if (graphic == null)
            {
                reticles.RemoveAt(i);
                continue;
            }

            if (graphic.enabled != show)
                graphic.enabled = show;
        }
    }

    [ContextMenu("Mira/Rebuscar elementos")]
    public void Rescan()
    {
        cameraController = Object.FindAnyObjectByType<PlayerCameraController>(FindObjectsInactive.Include);
        purchaseController = Object.FindAnyObjectByType<BuySceneCameraModeController>(FindObjectsInactive.Include);

        reticles.Clear();
        Graphic[] graphics = Object.FindObjectsByType<Graphic>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        for (int i = 0; i < graphics.Length; i++)
        {
            Graphic graphic = graphics[i];
            if (graphic == null || graphic.GetComponentInParent<Canvas>() == null)
                continue;

            if (IsReticleName(graphic.name))
                reticles.Add(graphic);
        }
    }

    private bool IsReticleName(string objectName)
    {
        if (string.IsNullOrWhiteSpace(objectName))
            return false;

        string lower = objectName.ToLowerInvariant();

        if (ContainsAny(lower, "button", "botao", "botão", "slider", "texto", "text", "label", "config"))
            return false;

        return ContainsAny(lower, "mira", "crosshair", "reticle", "aimpoint", "aim_point", "ponto_mira");
    }

    private bool ContainsAny(string value, params string[] terms)
    {
        for (int i = 0; i < terms.Length; i++)
        {
            if (value.Contains(terms[i]))
                return true;
        }

        return false;
    }
}
