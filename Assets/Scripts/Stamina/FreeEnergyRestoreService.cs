using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>
/// Torna o botão de energia grátis autoritativo.
/// Restaura o banco e todos os componentes de movimento no fim do frame, impedindo
/// listeners antigos ou eventos atrasados de recolocarem a porcentagem anterior.
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(28000)]
public sealed class FreeEnergyRestoreService : MonoBehaviour
{
    public static FreeEnergyRestoreService Instance { get; private set; }

    [Min(0.5f)] public float buttonScanInterval = 2f;
    [Min(0f)] public float finalReapplyDelay = 0.15f;

    private readonly HashSet<Button> boundButtons = new HashSet<Button>();
    private float nextScan;
    private Coroutine restoreRoutine;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Instance = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void CreateAfterSceneLoad()
    {
        FreeEnergyRestoreService existing = Object.FindAnyObjectByType<FreeEnergyRestoreService>(FindObjectsInactive.Include);
        if (existing != null)
        {
            Instance = existing;
            existing.ScanButtons();
            return;
        }

        GameObject host = new GameObject("FreeEnergyRestoreService");
        DontDestroyOnLoad(host);
        host.AddComponent<FreeEnergyRestoreService>();
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
        ScanButtons();
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        if (Instance == this)
            Instance = null;
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        boundButtons.Clear();
        nextScan = 0f;
        ScanButtons();
    }

    private void Update()
    {
        if (Time.unscaledTime < nextScan)
            return;

        nextScan = Time.unscaledTime + Mathf.Max(0.5f, buttonScanInterval);
        ScanButtons();
    }

    [ContextMenu("Energia/Restaurar agora")]
    public void RestoreNow()
    {
        if (restoreRoutine != null)
            StopCoroutine(restoreRoutine);

        restoreRoutine = StartCoroutine(RestoreSequence());
    }

    private IEnumerator RestoreSequence()
    {
        ApplyFullEnergy();
        yield return new WaitForEndOfFrame();
        ApplyFullEnergy();

        if (finalReapplyDelay > 0f)
        {
            yield return new WaitForSecondsRealtime(finalReapplyDelay);
            ApplyFullEnergy();
        }

        restoreRoutine = null;
    }

    private void ApplyFullEnergy()
    {
        MiniMarketPlayerDatabase database = MiniMarketPlayerDatabase.Instance;
        if (database == null && Application.isPlaying)
            database = MiniMarketPlayerDatabase.ObterOuCriar();

        if (database != null)
            database.RestaurarEnergiaCompleta();

        CameraRelativeMovement[] movements = Object.FindObjectsByType<CameraRelativeMovement>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None
        );

        for (int i = 0; i < movements.Length; i++)
        {
            CameraRelativeMovement movement = movements[i];
            if (movement != null)
                movement.RestoreStaminaFull();
        }

        MiniMarketEnergySegmentHUD[] huds = Object.FindObjectsByType<MiniMarketEnergySegmentHUD>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None
        );

        for (int i = 0; i < huds.Length; i++)
        {
            if (huds[i] != null)
                huds[i].Refresh(true);
        }
    }

    private void ScanButtons()
    {
        Button[] buttons = Object.FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < buttons.Length; i++)
        {
            Button button = buttons[i];
            if (button == null || boundButtons.Contains(button) || !LooksLikeFreeEnergyButton(button))
                continue;

            button.onClick.AddListener(RestoreNow);
            boundButtons.Add(button);
        }
    }

    private bool LooksLikeFreeEnergyButton(Button button)
    {
        string combined = button.name.ToLowerInvariant();
        Text[] texts = button.GetComponentsInChildren<Text>(true);
        for (int i = 0; i < texts.Length; i++)
        {
            if (texts[i] != null)
                combined += " " + texts[i].text.ToLowerInvariant();
        }

        bool energy = ContainsAny(combined, "energia", "energy", "stamina");
        bool free = ContainsAny(combined, "gratis", "grátis", "free", "gemas", "gems");
        return energy && free;
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
