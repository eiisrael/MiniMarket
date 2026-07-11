using System;
using UnityEngine;

/// <summary>
/// Componente de compatibilidade temporário para cenas antigas.
/// O sistema ativo é PlayerCameraController. Este componente não controla câmera,
/// não lê input e não depende do antigo PlayerMove.
/// </summary>
[Obsolete("Use PlayerCameraController. This compatibility component can be removed from the scene.")]
[DisallowMultipleComponent]
public sealed class CameraModeController : MonoBehaviour
{
    [Tooltip("Referência automática para o controlador novo.")]
    public PlayerCameraController playerCamera;

    public bool IsFirstPerson => playerCamera != null && playerCamera.IsFirstPerson;

    public Transform ActiveCameraTransform
    {
        get
        {
            if (playerCamera != null)
                return playerCamera.ActiveCameraTransform;

            return Camera.main != null ? Camera.main.transform : transform;
        }
    }

    private void Awake()
    {
        if (playerCamera == null)
            playerCamera = GetComponent<PlayerCameraController>();

        if (playerCamera == null)
            playerCamera = Object.FindAnyObjectByType<PlayerCameraController>(FindObjectsInactive.Include);

        enabled = false;
    }
}
