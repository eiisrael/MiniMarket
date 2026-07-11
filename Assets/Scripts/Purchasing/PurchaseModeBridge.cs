using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// Integra a BuyScene antiga com o controlador atual do jogador.
/// Enquanto a compra está ativa, suspende a autoridade de pose da câmera do jogador,
/// bloqueia o input e mantém a câmera de compra visível. Ao sair, restaura tudo.
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(900)]
public sealed class PurchaseModeBridge : MonoBehaviour
{
    public BuySceneCameraModeController purchaseController;
    public PlayerCameraController playerCamera;
    public CameraRelativeMovement movement;

    private bool previousPurchaseState;
    private bool inputBlockApplied;
    private bool purchaseCameraWasEnabled;
    private string playerCameraTag;
    private string purchaseCameraTag;

    public bool PurchaseModeActive => purchaseController != null && purchaseController.ModoCompraAtivo;

    private void Awake()
    {
        ResolveReferences();
        previousPurchaseState = PurchaseModeActive;

        if (previousPurchaseState)
            EnterPurchaseMode();
    }

    private void Update()
    {
        ResolveReferences();
        bool current = PurchaseModeActive;

        if (current == previousPurchaseState)
            return;

        previousPurchaseState = current;

        if (current)
            EnterPurchaseMode();
        else
            ExitPurchaseMode();
    }

    private void LateUpdate()
    {
        if (!PurchaseModeActive)
            return;

        Camera purchaseCamera = GetPurchaseCamera();
        Camera gameplayCamera = playerCamera != null ? playerCamera.gameCamera : null;

        if (playerCamera != null && !playerCamera.ExternalPoseControl)
            playerCamera.SetExternalPoseControl(true);

        if (purchaseCamera != null && purchaseCamera != gameplayCamera)
        {
            if (!purchaseCamera.enabled)
                purchaseCamera.enabled = true;

            if (gameplayCamera != null && gameplayCamera.enabled)
                gameplayCamera.enabled = false;
        }
    }

    private void OnDisable()
    {
        ExitPurchaseMode();
        previousPurchaseState = false;
    }

    private void ResolveReferences()
    {
        if (purchaseController == null)
            purchaseController = GetComponent<BuySceneCameraModeController>();

        if (purchaseController == null)
            purchaseController = Object.FindAnyObjectByType<BuySceneCameraModeController>(FindObjectsInactive.Include);

        if (playerCamera == null)
            playerCamera = Object.FindAnyObjectByType<PlayerCameraController>(FindObjectsInactive.Include);

        if (movement == null)
        {
            movement = playerCamera != null && playerCamera.movement != null
                ? playerCamera.movement
                : Object.FindAnyObjectByType<CameraRelativeMovement>(FindObjectsInactive.Include);
        }

        if (purchaseController == null)
            return;

        if (purchaseController.cameraPrincipal == null && playerCamera != null)
            purchaseController.cameraPrincipal = playerCamera.gameCamera;

        if (purchaseController.jogadorRaiz == null && movement != null)
            purchaseController.jogadorRaiz = movement.transform;
    }

    private Camera GetPurchaseCamera()
    {
        if (purchaseController != null && purchaseController.cameraPrincipal != null)
            return purchaseController.cameraPrincipal;

        return playerCamera != null ? playerCamera.gameCamera : Camera.main;
    }

    private void EnterPurchaseMode()
    {
        ResolveReferences();

        Camera gameplayCamera = playerCamera != null ? playerCamera.gameCamera : null;
        Camera purchaseCamera = GetPurchaseCamera();

        if (!inputBlockApplied)
        {
            GameplayInputState.PushBlock();
            inputBlockApplied = true;
        }

        if (playerCamera != null)
            playerCamera.SetExternalPoseControl(true);

        if (gameplayCamera != null)
            playerCameraTag = gameplayCamera.tag;

        if (purchaseCamera != null)
        {
            purchaseCameraWasEnabled = purchaseCamera.enabled;
            purchaseCameraTag = purchaseCamera.tag;
        }

        if (purchaseCamera != null && purchaseCamera != gameplayCamera)
        {
            if (gameplayCamera != null)
            {
                gameplayCamera.enabled = false;
                if (gameplayCamera.CompareTag("MainCamera"))
                    gameplayCamera.gameObject.tag = "Untagged";
            }

            purchaseCamera.enabled = true;
            purchaseCamera.gameObject.tag = "MainCamera";
            ConfigureAudioListeners(purchaseCamera);
        }

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void ExitPurchaseMode()
    {
        Camera gameplayCamera = playerCamera != null ? playerCamera.gameCamera : null;
        Camera purchaseCamera = GetPurchaseCamera();

        if (purchaseCamera != null && purchaseCamera != gameplayCamera)
        {
            purchaseCamera.enabled = purchaseCameraWasEnabled;
            RestoreTag(purchaseCamera.gameObject, purchaseCameraTag, "Untagged");

            if (gameplayCamera != null)
            {
                gameplayCamera.enabled = true;
                RestoreTag(gameplayCamera.gameObject, playerCameraTag, "MainCamera");
                ConfigureAudioListeners(gameplayCamera);
            }
        }

        if (playerCamera != null)
            playerCamera.SetExternalPoseControl(false);

        if (inputBlockApplied)
        {
            GameplayInputState.PopBlock();
            inputBlockApplied = false;
        }
    }

    private void ConfigureAudioListeners(Camera activeCamera)
    {
        AudioListener[] listeners = Object.FindObjectsByType<AudioListener>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None
        );

        for (int i = 0; i < listeners.Length; i++)
        {
            AudioListener listener = listeners[i];
            if (listener != null)
                listener.enabled = activeCamera != null && listener.gameObject == activeCamera.gameObject;
        }
    }

    private void RestoreTag(GameObject target, string savedTag, string fallback)
    {
        if (target == null)
            return;

        try
        {
            target.tag = string.IsNullOrEmpty(savedTag) ? fallback : savedTag;
        }
        catch
        {
            target.tag = fallback;
        }
    }
}
