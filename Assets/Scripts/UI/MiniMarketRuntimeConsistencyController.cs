using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>
/// Corrige e unifica comportamentos de runtime que dependem do jogador ativo:
/// - energia total contínua, incluindo a reserva de recarga entre segmentos;
/// - progress bar e texto sempre sincronizados com a energia real;
/// - menu aberto pelo TAB, com ESC usado somente para fechar;
/// - interação por clique executada após o foco ser resolvido, em primeira e terceira pessoa.
///
/// O componente é instalado automaticamente e não exige alterações manuais na cena.
/// </summary>
[DefaultExecutionOrder(9000)]
[DisallowMultipleComponent]
public sealed class MiniMarketRuntimeConsistencyController : MonoBehaviour
{
    public static MiniMarketRuntimeConsistencyController Instance { get; private set; }

    [Header("Energia visual")]
    [Min(0.1f)] public float normalVisualSpeed = 14f;
    [Min(0.1f)] public float benefitVisualSpeed = 38f;
    [Range(0.01f, 0.5f)] public float benefitJumpThreshold = 0.08f;
    [Min(0.05f)] public float benefitAnimationDuration = 0.45f;

    [Header("Atualização")]
    [Min(0.05f)] public float referenceRefreshInterval = 0.35f;

    private PlayerCameraController playerCamera;
    private CameraRelativeMovement movement;
    private MiniMarketEnergyProgressBar progressBar;
    private MiniMarketMenuController menuController;
    private InteractionFocusController interactionController;

    private float nextReferenceRefresh;
    private float visualEnergy01 = 1f;
    private float previousTarget01 = 1f;
    private float benefitAnimationUntil;
    private bool visualInitialized;
    private int lastInteractionFrame = -1;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Instance = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        MiniMarketRuntimeConsistencyController existing =
            Object.FindAnyObjectByType<MiniMarketRuntimeConsistencyController>(
                FindObjectsInactive.Include
            );

        if (existing != null)
        {
            Instance = existing;
            return;
        }

        GameObject host = new GameObject("[MiniMarket] Runtime Consistency");
        DontDestroyOnLoad(host);
        Instance = host.AddComponent<MiniMarketRuntimeConsistencyController>();
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
        ConfigureControllers();
        InitializeVisualEnergy();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void Update()
    {
        ResolveReferences(false);
        ConfigureControllers();
        HandleMenuShortcut();
        HandleFocusedInteraction();
    }

    private void LateUpdate()
    {
        ResolveReferences(false);

        if (movement == null)
            return;

        float target = CalculateContinuousEnergy01(movement);
        if (!visualInitialized)
        {
            visualEnergy01 = target;
            previousTarget01 = target;
            visualInitialized = true;
        }

        if (target - previousTarget01 >= benefitJumpThreshold)
            benefitAnimationUntil = Time.unscaledTime + benefitAnimationDuration;

        previousTarget01 = target;

        float speed = Time.unscaledTime < benefitAnimationUntil
            ? benefitVisualSpeed
            : normalVisualSpeed;
        float delta = Mathf.Clamp(Time.unscaledDeltaTime, 0.0001f, 0.05f);
        float blend = 1f - Mathf.Exp(-Mathf.Max(0.1f, speed) * delta);

        visualEnergy01 = Mathf.Lerp(visualEnergy01, target, blend);
        if (Mathf.Abs(visualEnergy01 - target) <= 0.0005f)
            visualEnergy01 = target;

        ApplyProgressBar(target, visualEnergy01);
        ApplyMenuEnergy(target);
    }

    /// <summary>
    /// Retorna a energia total real. Durante a recarga de segmentos adicionais,
    /// inclui a reserva que antes ficava invisível e causava saltos de 60 para 80/100.
    /// </summary>
    public static float CalculateContinuousEnergy01(CameraRelativeMovement source)
    {
        if (source == null)
            return 0f;

        if (!source.useSegmentedEnergy)
            return Mathf.Clamp01(source.Stamina01);

        int maximum = Mathf.Max(1, source.StaminaSegmentosMaximos);
        int current = Mathf.Clamp(source.StaminaSegmentosAtuais, 0, maximum);
        float activeBar01 = Mathf.Clamp01(source.StaminaPercentual01);
        float reserve01 = source.MaxStamina > 0.001f
            ? Mathf.Clamp01(source.StaminaRecargaReserva / source.MaxStamina)
            : 0f;

        float units = current <= 0
            ? activeBar01
            : Mathf.Max(0, current - 1) + activeBar01;

        // A reserva começa a crescer depois que a barra ativa fica cheia.
        // Somá-la continuamente elimina os degraus entre cada segmento.
        if (current > 0 && current < maximum)
            units += reserve01;

        return Mathf.Clamp01(units / maximum);
    }

    private void ResolveReferences(bool force)
    {
        if (!force && Time.unscaledTime < nextReferenceRefresh &&
            movement != null && menuController != null && progressBar != null)
        {
            return;
        }

        nextReferenceRefresh = Time.unscaledTime + Mathf.Max(0.05f, referenceRefreshInterval);

        if (force || playerCamera == null || !playerCamera.gameObject.scene.IsValid())
        {
            playerCamera = Object.FindAnyObjectByType<PlayerCameraController>(
                FindObjectsInactive.Include
            );
        }

        CameraRelativeMovement activeMovement = null;
        if (playerCamera != null && IsUsable(playerCamera.movement))
            activeMovement = playerCamera.movement;

        if (activeMovement == null && playerCamera != null && playerCamera.player != null)
        {
            CameraRelativeMovement playerMovement =
                playerCamera.player.GetComponent<CameraRelativeMovement>();
            if (IsUsable(playerMovement))
                activeMovement = playerMovement;
        }

        if (activeMovement == null)
        {
            CameraRelativeMovement[] candidates =
                Object.FindObjectsByType<CameraRelativeMovement>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None
                );

            for (int i = 0; i < candidates.Length; i++)
            {
                if (IsUsable(candidates[i]))
                {
                    activeMovement = candidates[i];
                    break;
                }
            }
        }

        if (activeMovement != null && activeMovement != movement)
        {
            movement = activeMovement;
            visualInitialized = false;
        }

        if (force || menuController == null || !menuController.gameObject.scene.IsValid())
        {
            menuController = Object.FindAnyObjectByType<MiniMarketMenuController>(
                FindObjectsInactive.Include
            );
        }

        if (force || progressBar == null || !progressBar.gameObject.scene.IsValid())
        {
            progressBar = Object.FindAnyObjectByType<MiniMarketEnergyProgressBar>(
                FindObjectsInactive.Include
            );
        }

        if (force || interactionController == null ||
            !interactionController.gameObject.scene.IsValid())
        {
            interactionController = playerCamera != null
                ? playerCamera.GetComponent<InteractionFocusController>()
                : null;

            if (interactionController == null)
            {
                interactionController = Object.FindAnyObjectByType<InteractionFocusController>(
                    FindObjectsInactive.Include
                );
            }
        }
    }

    private static bool IsUsable(CameraRelativeMovement candidate)
    {
        return candidate != null &&
               candidate.gameObject.scene.IsValid() &&
               candidate.isActiveAndEnabled &&
               candidate.gameObject.activeInHierarchy;
    }

    private void ConfigureControllers()
    {
        if (menuController != null)
        {
            // Corrige valores serializados antigos da cena que trocaram TAB por ESC.
            menuController.teclaMenu = KeyCode.Tab;
            menuController.atualizarEmTempoReal = true;
            menuController.intervaloAtualizacaoAberto = Mathf.Min(
                menuController.intervaloAtualizacaoAberto,
                0.10f
            );

            if (movement != null)
            {
                menuController.movimento = movement;
                menuController.componenteStaminaOuMovimento = movement;
            }
        }

        if (progressBar != null && movement != null)
        {
            progressBar.movimento = movement;
            progressBar.animar = false;
        }

        if (interactionController != null)
        {
            // O clique é processado abaixo, depois que o foco/realce foi resolvido.
            // Isso remove a diferença entre primeira e terceira pessoa.
            interactionController.interactWithMouse = false;
            interactionController.usePlayerOriginFallbackInThirdPerson = true;
            interactionController.thirdPersonFallbackDistance = Mathf.Max(
                interactionController.thirdPersonFallbackDistance,
                8f
            );
            interactionController.interactionRadius = Mathf.Max(
                interactionController.interactionRadius,
                0.18f
            );
        }
    }

    private void HandleMenuShortcut()
    {
        if (menuController == null)
            return;

        // TAB é tratado pelo próprio MiniMarketMenuController.
        // ESC nunca abre o menu; serve somente para fechá-lo quando estiver aberto.
        if (menuController.MenuAberto && Input.GetKeyDown(KeyCode.Escape))
            menuController.FecharMenu();
    }

    private void HandleFocusedInteraction()
    {
        if (interactionController == null || GameplayInputState.IsBlocked)
            return;

        if (!Input.GetMouseButtonDown(0) || lastInteractionFrame == Time.frameCount)
            return;

        InteractiveObject focused = interactionController.FocusedObject;
        if (focused == null)
            return;

        // Itens pegáveis continuam sob responsabilidade do GetItemController.
        if (focused.GetComponentInParent<GrabbableItem>() != null)
            return;

        lastInteractionFrame = Time.frameCount;
        interactionController.InteractWithFocusedObject();
    }

    private void InitializeVisualEnergy()
    {
        if (movement == null)
            return;

        visualEnergy01 = CalculateContinuousEnergy01(movement);
        previousTarget01 = visualEnergy01;
        visualInitialized = true;
    }

    private void ApplyProgressBar(float realEnergy01, float displayedEnergy01)
    {
        if (progressBar == null)
            return;

        Image fill = progressBar.preenchimentoVerde;
        if (fill == null)
        {
            progressBar.RebuscarTudo();
            fill = progressBar.preenchimentoVerde;
        }

        float displayed = Mathf.Clamp01(displayedEnergy01);
        if (fill != null)
        {
            RectTransform rect = fill.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = new Vector2(displayed, 1f);
            rect.pivot = new Vector2(0f, 0.5f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            fill.enabled = displayed > 0.0001f;

            Color color = displayed >= progressBar.limiteVerde
                ? progressBar.corAlta
                : displayed <= progressBar.limiteVermelho
                    ? progressBar.corBaixa
                    : progressBar.corMedia;
            fill.color = color;
        }

        int percent = Mathf.Clamp(Mathf.RoundToInt(realEnergy01 * 100f), 0, 100);
        if (progressBar.textoQuantidade != null)
        {
            progressBar.textoQuantidade.text = string.IsNullOrWhiteSpace(
                progressBar.formatoPorcentagem
            )
                ? percent + "%"
                : string.Format(progressBar.formatoPorcentagem, percent);
        }

        if (progressBar.iconeEnergia != null)
        {
            Sprite sprite = realEnergy01 >= progressBar.limiteVerde
                ? progressBar.energiaVerdeSprite
                : realEnergy01 <= progressBar.limiteVermelho
                    ? progressBar.energiaVermelhaSprite
                    : progressBar.energiaAmarelaSprite;

            if (sprite != null)
            {
                progressBar.iconeEnergia.sprite = sprite;
                progressBar.iconeEnergia.color = Color.white;
                progressBar.iconeEnergia.preserveAspect = true;
            }
        }
    }

    private void ApplyMenuEnergy(float realEnergy01)
    {
        if (menuController == null)
            return;

        if (movement != null)
        {
            menuController.movimento = movement;
            menuController.componenteStaminaOuMovimento = movement;
        }

        if (menuController.textoStamina == null)
            return;

        int percent = Mathf.Clamp(Mathf.RoundToInt(realEnergy01 * 100f), 0, 100);
        string value = percent + "%";
        menuController.textoStamina.text = menuController.incluirRotulosNosTextos
            ? menuController.rotuloEnergia + ": " + value
            : value;
    }

    private void OnValidate()
    {
        normalVisualSpeed = Mathf.Max(0.1f, normalVisualSpeed);
        benefitVisualSpeed = Mathf.Max(0.1f, benefitVisualSpeed);
        benefitJumpThreshold = Mathf.Clamp(benefitJumpThreshold, 0.01f, 0.5f);
        benefitAnimationDuration = Mathf.Max(0.05f, benefitAnimationDuration);
        referenceRefreshInterval = Mathf.Max(0.05f, referenceRefreshInterval);
    }
}
