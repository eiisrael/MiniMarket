using System;
using UnityEngine;
using UnityEngine.UI;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Converte o botão direito em alternância permanente entre terceira e primeira pessoa.
/// Um clique entra na mira e outro clique retorna para terceira pessoa.
///
/// Também localiza automaticamente a Image da mão e troca hand_open/hand_close
/// enquanto o botão esquerdo estiver pressionado no modo de mira.
/// </summary>
[DefaultExecutionOrder(900)]
[DisallowMultipleComponent]
public sealed class MiniMarketPersistentAimHandController : MonoBehaviour
{
    [Header("Câmera")]
    public PlayerCameraController cameraController;
    [Range(0, 2)] public int aimMouseButton = 1;
    public bool toggleAimOnClick = true;

    [Header("Mão / Mira")]
    public Image handImage;
    public Sprite handOpenSprite;
    public Sprite handCloseSprite;
    [Range(0, 2)] public int handCloseMouseButton = 0;
    public bool closeHandOnlyInFirstPerson = true;

    [Header("Busca automática")]
    public bool autoFindCamera = true;
    public bool autoFindHandImage = true;
    public bool autoFindHandSprites = true;
    [Min(0.1f)] public float referenceSearchInterval = 0.75f;

    [Header("Debug")]
    public bool logStateChanges;

    private static MiniMarketPersistentAimHandController instance;
    private float nextReferenceSearch;
    private bool lastClosedState;
    private Sprite lastAppliedSprite;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (instance != null)
            return;

        MiniMarketPersistentAimHandController existing =
            UnityEngine.Object.FindAnyObjectByType<MiniMarketPersistentAimHandController>(
                FindObjectsInactive.Include
            );

        if (existing != null)
        {
            instance = existing;
            return;
        }

        GameObject runtimeObject = new GameObject("[MiniMarket] Persistent Aim Hand");
        DontDestroyOnLoad(runtimeObject);
        instance = runtimeObject.AddComponent<MiniMarketPersistentAimHandController>();
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
        ResolveReferences(true);
        DisableHoldToAimMode();
        ApplyHandSprite(false, true);
    }

    private void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }

    private void Update()
    {
        ResolveReferences(false);
        DisableHoldToAimMode();

        if (cameraController == null)
            return;

        if (!GameplayInputState.IsBlocked &&
            !cameraController.ExternalPoseControl &&
            toggleAimOnClick &&
            Input.GetMouseButtonDown(aimMouseButton))
        {
            cameraController.ToggleMode();

            if (logStateChanges)
            {
                Debug.Log(
                    "[PersistentAim] Modo=" +
                    (cameraController.IsFirstPerson ? "Primeira pessoa" : "Terceira pessoa"),
                    cameraController
                );
            }
        }

        bool closeHand = !GameplayInputState.IsBlocked &&
                         Input.GetMouseButton(handCloseMouseButton) &&
                         (!closeHandOnlyInFirstPerson || cameraController.IsFirstPerson);

        ApplyHandSprite(closeHand, false);
    }

    private void DisableHoldToAimMode()
    {
        if (cameraController == null)
            return;

        // O controlador central deixa de interpretar o botão direito como "segurar".
        // O clique passa a ser tratado exclusivamente por este componente.
        cameraController.holdRightMouseForFirstPerson = false;
    }

    private void ResolveReferences(bool force)
    {
        if (!force && Time.unscaledTime < nextReferenceSearch &&
            cameraController != null &&
            (!autoFindHandImage || handImage != null))
        {
            return;
        }

        nextReferenceSearch = Time.unscaledTime + Mathf.Max(0.1f, referenceSearchInterval);

        if (autoFindCamera && cameraController == null)
        {
            cameraController = UnityEngine.Object.FindAnyObjectByType<PlayerCameraController>(
                FindObjectsInactive.Include
            );
        }

        if (autoFindHandImage && handImage == null)
            handImage = FindBestHandImage();

        if (autoFindHandSprites)
            ResolveHandSprites();
    }

    private Image FindBestHandImage()
    {
        Image[] images = Resources.FindObjectsOfTypeAll<Image>();
        Image best = null;
        int bestScore = int.MinValue;

        for (int i = 0; i < images.Length; i++)
        {
            Image image = images[i];
            if (image == null || !image.gameObject.scene.IsValid())
                continue;

            string objectName = Normalize(image.name);
            string spriteName = image.sprite != null ? Normalize(image.sprite.name) : string.Empty;
            int score = 0;

            if (IsOpenHandName(spriteName) || IsCloseHandName(spriteName)) score += 1000;
            if (objectName.Contains("hand")) score += 500;
            if (objectName.Contains("mao")) score += 500;
            if (objectName.Contains("mira")) score += 350;
            if (objectName.Contains("crosshair")) score += 350;
            if (objectName.Contains("cursor")) score += 200;
            if (image.gameObject.activeInHierarchy) score += 50;

            if (score <= bestScore)
                continue;

            bestScore = score;
            best = image;
        }

        return bestScore > 0 ? best : null;
    }

    private void ResolveHandSprites()
    {
        if (handImage != null && handImage.sprite != null)
        {
            string currentName = Normalize(handImage.sprite.name);
            if (handOpenSprite == null && IsOpenHandName(currentName))
                handOpenSprite = handImage.sprite;
            if (handCloseSprite == null && IsCloseHandName(currentName))
                handCloseSprite = handImage.sprite;
        }

        if (handOpenSprite != null && handCloseSprite != null)
            return;

        Sprite[] loadedSprites = Resources.FindObjectsOfTypeAll<Sprite>();
        for (int i = 0; i < loadedSprites.Length; i++)
        {
            Sprite sprite = loadedSprites[i];
            if (sprite == null)
                continue;

            string spriteName = Normalize(sprite.name);
            if (handOpenSprite == null && IsOpenHandName(spriteName))
                handOpenSprite = sprite;
            else if (handCloseSprite == null && IsCloseHandName(spriteName))
                handCloseSprite = sprite;

            if (handOpenSprite != null && handCloseSprite != null)
                return;
        }

#if UNITY_EDITOR
        if (handOpenSprite == null)
            handOpenSprite = FindSpriteAsset("hand_open");
        if (handCloseSprite == null)
            handCloseSprite = FindSpriteAsset("hand_close");
#endif
    }

#if UNITY_EDITOR
    private static Sprite FindSpriteAsset(string searchName)
    {
        string[] guids = AssetDatabase.FindAssets(searchName + " t:Sprite");
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(path);

            for (int a = 0; a < assets.Length; a++)
            {
                Sprite sprite = assets[a] as Sprite;
                if (sprite == null)
                    continue;

                string normalized = Normalize(sprite.name);
                if ((searchName == "hand_open" && IsOpenHandName(normalized)) ||
                    (searchName == "hand_close" && IsCloseHandName(normalized)))
                {
                    return sprite;
                }
            }
        }

        return null;
    }
#endif

    private void ApplyHandSprite(bool closed, bool force)
    {
        if (handImage == null)
            return;

        ResolveHandSprites();

        Sprite target = closed ? handCloseSprite : handOpenSprite;
        if (target == null)
            return;

        if (!force && lastClosedState == closed && lastAppliedSprite == target && handImage.sprite == target)
            return;

        handImage.sprite = target;
        lastClosedState = closed;
        lastAppliedSprite = target;
    }

    private static bool IsOpenHandName(string value)
    {
        return value.Contains("handopen") ||
               value.Contains("openhand") ||
               value.Contains("maoaberta");
    }

    private static bool IsCloseHandName(string value)
    {
        return value.Contains("handclose") ||
               value.Contains("handclosed") ||
               value.Contains("closedhand") ||
               value.Contains("maofechada");
    }

    private static string Normalize(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        return value.Trim()
            .ToLowerInvariant()
            .Replace("_", string.Empty)
            .Replace("-", string.Empty)
            .Replace(" ", string.Empty)
            .Replace("á", "a")
            .Replace("ã", "a")
            .Replace("ç", "c");
    }
}
