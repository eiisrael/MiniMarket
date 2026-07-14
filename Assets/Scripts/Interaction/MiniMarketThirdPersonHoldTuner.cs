using System.Reflection;
using UnityEngine;

/// <summary>
/// Ajusta a distância de retenção do GetItemController em terceira pessoa.
///
/// Como o raio nasce na câmera atrás do jogador, uma distância fixa curta posiciona
/// o objeto dentro das costas do personagem. Este componente calcula:
/// distância câmera -> corpo + espaço frontal + raio do objeto.
/// </summary>
[DefaultExecutionOrder(1150)]
[DisallowMultipleComponent]
public sealed class MiniMarketThirdPersonHoldTuner : MonoBehaviour
{
    public static MiniMarketThirdPersonHoldTuner Instance { get; private set; }

    [Header("Referências")]
    public PlayerCameraController playerCamera;
    public ThirdPersonCamera thirdPersonCamera;
    public GetItemController getItemController;
    public Transform playerRoot;

    [Header("Terceira pessoa")]
    [Min(0.4f)] public float forwardClearance = 1.35f;
    [Min(0f)] public float extraObjectRadiusPadding = 0.25f;
    [Min(0.1f)] public float defaultObjectRadius = 0.35f;
    [Min(0.1f)] public float distanceSmoothSpeed = 18f;
    public bool disableMouseWheelWhileHoldingInThirdPerson = true;

    [Header("Limites")]
    [Min(1f)] public float minimumThirdPersonDistance = 3.5f;
    [Min(2f)] public float maximumThirdPersonDistance = 9f;

    private static readonly FieldInfo CurrentHoldDistanceField =
        typeof(GetItemController).GetField(
            "currentHoldDistance",
            BindingFlags.Instance | BindingFlags.NonPublic
        );

    private float originalHoldDistance;
    private float originalMaximumHoldDistance;
    private bool originalAllowMouseWheel;
    private bool cachedOriginalSettings;
    private float appliedDistance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Instance = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        MiniMarketThirdPersonHoldTuner existing =
            Object.FindAnyObjectByType<MiniMarketThirdPersonHoldTuner>(
                FindObjectsInactive.Include
            );

        if (existing != null)
        {
            Instance = existing;
            return;
        }

        GameObject host = new GameObject("[MiniMarket] Third Person Hold Tuner");
        DontDestroyOnLoad(host);
        Instance = host.AddComponent<MiniMarketThirdPersonHoldTuner>();
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
        ResolveReferences(true);
        CacheOriginalSettings();
    }

    private void OnDestroy()
    {
        RestoreOriginalSettings();

        if (Instance == this)
            Instance = null;
    }

    private void Update()
    {
        ResolveReferences(false);
        CacheOriginalSettings();

        if (getItemController == null || playerCamera == null)
            return;

        if (playerCamera.IsFirstPerson)
        {
            RestoreRuntimeSettingsForFirstPerson();
            return;
        }

        float desired = CalculateRequiredThirdPersonDistance();
        float delta = Mathf.Clamp(Time.unscaledDeltaTime, 0.0001f, 0.05f);
        float blend = 1f - Mathf.Exp(-Mathf.Max(0.1f, distanceSmoothSpeed) * delta);

        if (appliedDistance <= 0.001f)
            appliedDistance = desired;
        else
            appliedDistance = Mathf.Lerp(appliedDistance, desired, blend);

        getItemController.holdDistance = appliedDistance;
        getItemController.maximumHoldDistance = Mathf.Max(
            originalMaximumHoldDistance,
            appliedDistance + 0.75f
        );

        if (disableMouseWheelWhileHoldingInThirdPerson && getItemController.IsHolding)
            getItemController.allowMouseWheelDistance = false;
        else
            getItemController.allowMouseWheelDistance = originalAllowMouseWheel;

        if (getItemController.IsHolding && CurrentHoldDistanceField != null)
        {
            CurrentHoldDistanceField.SetValue(
                getItemController,
                Mathf.Clamp(
                    appliedDistance,
                    getItemController.minimumHoldDistance,
                    getItemController.maximumHoldDistance
                )
            );
        }
    }

    private float CalculateRequiredThirdPersonDistance()
    {
        Camera cameraSource = playerCamera != null ? playerCamera.gameCamera : null;
        if (cameraSource == null || playerRoot == null)
            return Mathf.Clamp(
                originalHoldDistance + 3f,
                minimumThirdPersonDistance,
                maximumThirdPersonDistance
            );

        Vector3 bodyFocus = playerRoot.position + Vector3.up * 1.2f;
        if (thirdPersonCamera != null)
        {
            bodyFocus = thirdPersonCamera.lookTarget != null
                ? thirdPersonCamera.lookTarget.position
                : playerRoot.position + thirdPersonCamera.targetOffset;
        }

        float cameraToBody = Vector3.Distance(cameraSource.transform.position, bodyFocus);
        float objectRadius = CalculateHeldObjectRadius();
        float desired = cameraToBody + forwardClearance + objectRadius + extraObjectRadiusPadding;

        return Mathf.Clamp(
            desired,
            Mathf.Max(minimumThirdPersonDistance, cameraToBody + 0.75f),
            maximumThirdPersonDistance
        );
    }

    private float CalculateHeldObjectRadius()
    {
        GrabbableItem held = getItemController != null ? getItemController.HeldItem : null;
        if (held == null)
            return defaultObjectRadius;

        bool hasBounds = false;
        Bounds bounds = new Bounds(held.transform.position, Vector3.zero);

        Collider[] colliders = held.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider target = colliders[i];
            if (target == null)
                continue;

            if (!hasBounds)
            {
                bounds = target.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(target.bounds);
            }
        }

        if (!hasBounds)
        {
            Renderer[] renderers = held.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer target = renderers[i];
                if (target == null)
                    continue;

                if (!hasBounds)
                {
                    bounds = target.bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(target.bounds);
                }
            }
        }

        if (!hasBounds)
            return defaultObjectRadius;

        return Mathf.Max(
            defaultObjectRadius,
            Mathf.Max(bounds.extents.x, bounds.extents.z)
        );
    }

    private void ResolveReferences(bool force)
    {
        if (force || playerCamera == null || !playerCamera.gameObject.scene.IsValid())
        {
            playerCamera = Object.FindAnyObjectByType<PlayerCameraController>(
                FindObjectsInactive.Include
            );
        }

        if (playerCamera != null)
        {
            if (thirdPersonCamera == null)
                thirdPersonCamera = playerCamera.thirdPerson;

            if (getItemController == null)
                getItemController = playerCamera.GetComponent<GetItemController>();

            if (playerRoot == null)
                playerRoot = playerCamera.player;
        }

        if (getItemController == null)
        {
            getItemController = Object.FindAnyObjectByType<GetItemController>(
                FindObjectsInactive.Include
            );
        }

        if (playerRoot == null && getItemController != null)
            playerRoot = getItemController.playerRoot;
    }

    private void CacheOriginalSettings()
    {
        if (cachedOriginalSettings || getItemController == null)
            return;

        originalHoldDistance = getItemController.holdDistance;
        originalMaximumHoldDistance = getItemController.maximumHoldDistance;
        originalAllowMouseWheel = getItemController.allowMouseWheelDistance;
        appliedDistance = originalHoldDistance;
        cachedOriginalSettings = true;
    }

    private void RestoreRuntimeSettingsForFirstPerson()
    {
        if (!cachedOriginalSettings || getItemController == null)
            return;

        getItemController.holdDistance = originalHoldDistance;
        getItemController.maximumHoldDistance = originalMaximumHoldDistance;
        getItemController.allowMouseWheelDistance = originalAllowMouseWheel;
        appliedDistance = originalHoldDistance;
    }

    private void RestoreOriginalSettings()
    {
        RestoreRuntimeSettingsForFirstPerson();
    }
}
