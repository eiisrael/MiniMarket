using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>
/// Controla automaticamente imagens de mira/crosshair da UI.
/// A mira aparece apenas em primeira pessoa e fica oculta em terceira pessoa,
/// menus e modo de compra. O sprite muda para click_on enquanto um objeto está
/// selecionado ou sendo segurado.
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(26000)]
public sealed class FirstPersonReticleController : MonoBehaviour
{
    public static FirstPersonReticleController Instance { get; private set; }

    [Header("Atualização")]
    [Min(0.25f)] public float scanInterval = 1f;
    public bool hideWhenGameplayInputBlocked = true;

    [Header("Sprites da mão/mira")]
    public Sprite idleSprite;
    public Sprite selectedSprite;
    public Sprite holdingSprite;
    public bool autoDetectSpritesByName = true;
    public string idleSpriteName = "click_off";
    public string activeSpriteName = "click_on";
    public bool useActiveSpriteWhenSelected = true;
    public bool useActiveSpriteWhenHolding = true;

    private readonly List<Graphic> reticles = new List<Graphic>(8);
    private readonly List<Image> reticleImages = new List<Image>(8);
    private PlayerCameraController cameraController;
    private BuySceneCameraModeController purchaseController;
    private GetItemController getItemController;
    private float nextScan;
    private bool spriteSearchDone;

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

        if (transform.parent == null)
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
        spriteSearchDone = false;
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

        if (show)
            ApplyReticleSprite();
    }

    [ContextMenu("Mira/Rebuscar elementos e sprites")]
    public void Rescan()
    {
        cameraController = Object.FindAnyObjectByType<PlayerCameraController>(FindObjectsInactive.Include);
        purchaseController = Object.FindAnyObjectByType<BuySceneCameraModeController>(FindObjectsInactive.Include);

        if (cameraController != null)
            getItemController = cameraController.GetComponent<GetItemController>();
        if (getItemController == null)
            getItemController = Object.FindAnyObjectByType<GetItemController>(FindObjectsInactive.Include);

        reticles.Clear();
        reticleImages.Clear();

        Graphic[] graphics = Object.FindObjectsByType<Graphic>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < graphics.Length; i++)
        {
            Graphic graphic = graphics[i];
            if (graphic == null || graphic.GetComponentInParent<Canvas>() == null)
                continue;

            if (!IsReticleName(graphic.name))
                continue;

            reticles.Add(graphic);
            if (graphic is Image image)
                reticleImages.Add(image);
        }

        ResolveSpriteReferences(graphics);
    }

    private void ApplyReticleSprite()
    {
        bool holding = getItemController != null && getItemController.IsHolding;
        bool selected = getItemController != null && getItemController.SelectedItem != null;

        Sprite desired = idleSprite;
        if (holding && useActiveSpriteWhenHolding)
            desired = holdingSprite != null ? holdingSprite : selectedSprite;
        else if (selected && useActiveSpriteWhenSelected)
            desired = selectedSprite;

        if (desired == null)
            return;

        for (int i = reticleImages.Count - 1; i >= 0; i--)
        {
            Image image = reticleImages[i];
            if (image == null)
            {
                reticleImages.RemoveAt(i);
                continue;
            }

            if (image.sprite != desired)
                image.sprite = desired;
        }
    }

    private void ResolveSpriteReferences(Graphic[] graphics)
    {
        if (reticleImages.Count > 0 && idleSprite == null)
            idleSprite = reticleImages[0].sprite;

        if (!autoDetectSpritesByName || spriteSearchDone)
            return;

        spriteSearchDone = true;

        for (int i = 0; i < graphics.Length; i++)
        {
            if (!(graphics[i] is Image image) || image.sprite == null)
                continue;

            TryAssignNamedSprite(image.sprite);
        }

        if (selectedSprite == null || holdingSprite == null || idleSprite == null)
        {
            Sprite[] loadedSprites = Resources.FindObjectsOfTypeAll<Sprite>();
            for (int i = 0; i < loadedSprites.Length; i++)
                TryAssignNamedSprite(loadedSprites[i]);
        }

        if (holdingSprite == null)
            holdingSprite = selectedSprite;
    }

    private void TryAssignNamedSprite(Sprite sprite)
    {
        if (sprite == null || string.IsNullOrWhiteSpace(sprite.name))
            return;

        string lower = sprite.name.ToLowerInvariant();
        string idle = string.IsNullOrWhiteSpace(idleSpriteName) ? "click_off" : idleSpriteName.ToLowerInvariant();
        string active = string.IsNullOrWhiteSpace(activeSpriteName) ? "click_on" : activeSpriteName.ToLowerInvariant();

        if (idleSprite == null && lower.Contains(idle))
            idleSprite = sprite;

        if (lower.Contains(active))
        {
            if (selectedSprite == null)
                selectedSprite = sprite;
            if (holdingSprite == null)
                holdingSprite = sprite;
        }
    }

    private bool IsReticleName(string objectName)
    {
        if (string.IsNullOrWhiteSpace(objectName))
            return false;

        string lower = objectName.ToLowerInvariant();

        if (ContainsAny(lower, "button", "botao", "botão", "slider", "texto", "text", "label", "config"))
            return false;

        return ContainsAny(lower, "mira", "crosshair", "reticle", "aimpoint", "aim_point", "ponto_mira", "click_cursor");
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

    private void OnValidate()
    {
        scanInterval = Mathf.Max(0.25f, scanInterval);
    }
}
