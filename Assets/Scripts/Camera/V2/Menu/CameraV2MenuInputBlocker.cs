using UnityEngine;

/// <summary>
/// Bloqueia input de mouse da Camera V2 enquanto o menu estiver aberto.
///
/// Use no CameraSystemV2 ou deixe o auto-create funcionar.
/// Objetivo:
/// - ESC/menu aberto: câmera não gira, zoom não ativa e GetItem fica bloqueado.
/// - menu fechado: valores originais são restaurados e a câmera volta a funcionar.
///
/// Não depende dos scripts antigos.
/// </summary>
[DefaultExecutionOrder(-50000)]
public class CameraV2MenuInputBlocker : MonoBehaviour
{
    [Header("Ativação")]
    public bool ativo = true;
    public bool criarAutomaticamente = true;

    [Header("Detecção do Menu")]
    [Tooltip("Arraste aqui o GameObject do Menu ESC, se quiser detecção 100% manual.")]
    public GameObject[] objetosMenu;

    [Tooltip("Arraste aqui CanvasGroups do Menu ESC, se houver.")]
    public CanvasGroup[] canvasGroupsMenu;

    public bool detectarMenusAutomaticamentePorNome = true;
    public string[] nomesMenu = { "menu", "pause", "pausa", "esc" };
    public bool exigirCanvasGroupVisivel = true;
    public bool considerarCursorLivreComoMenuAberto = true;
    public bool desbloquearCursorComMenuAberto = true;

    [Header("Bloqueio")]
    public bool bloquearThirdPersonMouse = true;
    public bool bloquearFirstPersonMouse = true;
    public bool bloquearZoomEnquantoMenuAberto = true;
    public bool bloquearGetItemEnquantoMenuAberto = true;
    public bool soltarObjetoAoAbrirMenu = true;

    [Header("Busca Automática")]
    public bool procurarAutomaticamente = true;
    [Min(0.1f)] public float intervaloBusca = 0.5f;

    [Header("Debug")]
    public bool logarMudancaEstado;

    public static bool MenuAberto { get; private set; }

    private static CameraV2MenuInputBlocker instancia;
    private Camera3Person[] thirdPersonCameras;
    private Camera1Person[] firstPersonCameras;
    private GetItemV2[] getItems;
    private CanvasGroup[] canvasGroupsAuto;
    private float proximaBusca;
    private bool ultimoMenuAberto;
    private bool jaViuCursorTravado;

    private struct ThirdPersonState
    {
        public Camera3Person cam;
        public bool aceitarInputMouse;
        public bool usarZoom;
        public bool travarCursor;
        public bool salvo;
    }

    private struct FirstPersonState
    {
        public Camera1Person cam;
        public bool aceitarInputMouse;
        public bool usarZoom;
        public bool travarCursor;
        public bool exibirMira;
        public bool salvo;
    }

    private struct GetItemState
    {
        public GetItemV2 item;
        public bool enabled;
        public bool salvo;
    }

    private ThirdPersonState[] thirdStates = new ThirdPersonState[0];
    private FirstPersonState[] firstStates = new FirstPersonState[0];
    private GetItemState[] getItemStates = new GetItemState[0];

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void CriarAuto()
    {
        if (instancia != null)
            return;

        GameObject go = new GameObject("MiniMarket_CameraV2MenuInputBlocker");
        DontDestroyOnLoad(go);
        instancia = go.AddComponent<CameraV2MenuInputBlocker>();
    }

    private void Awake()
    {
        if (instancia != null && instancia != this)
        {
            Destroy(gameObject);
            return;
        }

        instancia = this;
        DontDestroyOnLoad(gameObject);
        BuscarReferencias(true);
    }

    private void Update()
    {
        if (!ativo)
        {
            if (MenuAberto)
                RestaurarTudo();

            MenuAberto = false;
            return;
        }

        if (Cursor.lockState == CursorLockMode.Locked)
            jaViuCursorTravado = true;

        if (procurarAutomaticamente && Time.unscaledTime >= proximaBusca)
        {
            proximaBusca = Time.unscaledTime + Mathf.Max(0.1f, intervaloBusca);
            BuscarReferencias(false);
        }

        bool aberto = DetectarMenuAberto();
        MenuAberto = aberto;

        if (aberto)
            AplicarBloqueio();
        else
            RestaurarTudo();

        if (aberto != ultimoMenuAberto)
        {
            ultimoMenuAberto = aberto;
            if (logarMudancaEstado)
                Debug.Log("[CameraV2MenuInputBlocker] Menu " + (aberto ? "aberto: input da câmera bloqueado." : "fechado: input da câmera restaurado."));
        }
    }

    private void LateUpdate()
    {
        if (!ativo || !MenuAberto)
            return;

        if (desbloquearCursorComMenuAberto)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }

    private void BuscarReferencias(bool forcar)
    {
        if (!forcar && thirdPersonCameras != null && firstPersonCameras != null && getItems != null)
            return;

        thirdPersonCameras = Object.FindObjectsByType<Camera3Person>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        firstPersonCameras = Object.FindObjectsByType<Camera1Person>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        getItems = Object.FindObjectsByType<GetItemV2>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        canvasGroupsAuto = Object.FindObjectsByType<CanvasGroup>(FindObjectsInactive.Include, FindObjectsSortMode.None);
    }

    private bool DetectarMenuAberto()
    {
        if (ObjetoMenuManualAberto())
            return true;

        if (CanvasGroupManualAberto())
            return true;

        if (detectarMenusAutomaticamentePorNome && CanvasGroupAutoAberto())
            return true;

        if (considerarCursorLivreComoMenuAberto && jaViuCursorTravado && Cursor.lockState != CursorLockMode.Locked && Cursor.visible)
            return true;

        return false;
    }

    private bool ObjetoMenuManualAberto()
    {
        if (objetosMenu == null)
            return false;

        for (int i = 0; i < objetosMenu.Length; i++)
        {
            GameObject go = objetosMenu[i];
            if (go == null || !go.activeInHierarchy)
                continue;

            CanvasGroup cg = go.GetComponent<CanvasGroup>();
            if (cg == null)
                return true;

            if (CanvasGroupEstaAberto(cg))
                return true;
        }

        return false;
    }

    private bool CanvasGroupManualAberto()
    {
        if (canvasGroupsMenu == null)
            return false;

        for (int i = 0; i < canvasGroupsMenu.Length; i++)
        {
            if (CanvasGroupEstaAberto(canvasGroupsMenu[i]))
                return true;
        }

        return false;
    }

    private bool CanvasGroupAutoAberto()
    {
        if (canvasGroupsAuto == null)
            return false;

        for (int i = 0; i < canvasGroupsAuto.Length; i++)
        {
            CanvasGroup cg = canvasGroupsAuto[i];
            if (cg == null || !NomePareceMenu(cg.transform))
                continue;

            if (CanvasGroupEstaAberto(cg))
                return true;
        }

        return false;
    }

    private bool CanvasGroupEstaAberto(CanvasGroup cg)
    {
        if (cg == null || !cg.gameObject.activeInHierarchy)
            return false;

        if (!exigirCanvasGroupVisivel)
            return true;

        return cg.alpha > 0.05f && (cg.blocksRaycasts || cg.interactable);
    }

    private bool NomePareceMenu(Transform t)
    {
        while (t != null)
        {
            string nome = t.name.ToLowerInvariant();
            for (int i = 0; i < nomesMenu.Length; i++)
            {
                string chave = nomesMenu[i];
                if (!string.IsNullOrEmpty(chave) && nome.Contains(chave.ToLowerInvariant()))
                    return true;
            }

            t = t.parent;
        }

        return false;
    }

    private void AplicarBloqueio()
    {
        if (desbloquearCursorComMenuAberto)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        if (bloquearThirdPersonMouse && thirdPersonCameras != null)
        {
            AjustarThirdStates();
            for (int i = 0; i < thirdStates.Length; i++)
            {
                Camera3Person cam = thirdStates[i].cam;
                if (cam == null)
                    continue;

                cam.aceitarInputMouse = false;
                cam.travarCursorAoAtivar = false;
                if (bloquearZoomEnquantoMenuAberto)
                    cam.usarZoom = false;
            }
        }

        if (bloquearFirstPersonMouse && firstPersonCameras != null)
        {
            AjustarFirstStates();
            for (int i = 0; i < firstStates.Length; i++)
            {
                Camera1Person cam = firstStates[i].cam;
                if (cam == null)
                    continue;

                cam.aceitarInputMouse = false;
                cam.travarCursorAoAtivar = false;
                if (bloquearZoomEnquantoMenuAberto)
                    cam.usarZoom = false;
                cam.exibirMira = false;
            }
        }

        if (bloquearGetItemEnquantoMenuAberto && getItems != null)
        {
            AjustarGetItemStates();
            for (int i = 0; i < getItemStates.Length; i++)
            {
                GetItemV2 item = getItemStates[i].item;
                if (item == null)
                    continue;

                if (soltarObjetoAoAbrirMenu)
                    item.SoltarObjeto();

                item.enabled = false;
            }
        }
    }

    private void RestaurarTudo()
    {
        for (int i = 0; i < thirdStates.Length; i++)
        {
            if (!thirdStates[i].salvo || thirdStates[i].cam == null)
                continue;

            thirdStates[i].cam.aceitarInputMouse = thirdStates[i].aceitarInputMouse;
            thirdStates[i].cam.usarZoom = thirdStates[i].usarZoom;
            thirdStates[i].cam.travarCursorAoAtivar = thirdStates[i].travarCursor;
            thirdStates[i].salvo = false;
        }

        for (int i = 0; i < firstStates.Length; i++)
        {
            if (!firstStates[i].salvo || firstStates[i].cam == null)
                continue;

            firstStates[i].cam.aceitarInputMouse = firstStates[i].aceitarInputMouse;
            firstStates[i].cam.usarZoom = firstStates[i].usarZoom;
            firstStates[i].cam.travarCursorAoAtivar = firstStates[i].travarCursor;
            firstStates[i].cam.exibirMira = firstStates[i].exibirMira;
            firstStates[i].salvo = false;
        }

        for (int i = 0; i < getItemStates.Length; i++)
        {
            if (!getItemStates[i].salvo || getItemStates[i].item == null)
                continue;

            getItemStates[i].item.enabled = getItemStates[i].enabled;
            getItemStates[i].salvo = false;
        }
    }

    private void AjustarThirdStates()
    {
        if (thirdPersonCameras == null)
            thirdPersonCameras = new Camera3Person[0];

        if (thirdStates.Length != thirdPersonCameras.Length)
            thirdStates = new ThirdPersonState[thirdPersonCameras.Length];

        for (int i = 0; i < thirdPersonCameras.Length; i++)
        {
            if (thirdStates[i].salvo && thirdStates[i].cam == thirdPersonCameras[i])
                continue;

            Camera3Person cam = thirdPersonCameras[i];
            thirdStates[i].cam = cam;
            if (cam == null)
                continue;

            thirdStates[i].aceitarInputMouse = cam.aceitarInputMouse;
            thirdStates[i].usarZoom = cam.usarZoom;
            thirdStates[i].travarCursor = cam.travarCursorAoAtivar;
            thirdStates[i].salvo = true;
        }
    }

    private void AjustarFirstStates()
    {
        if (firstPersonCameras == null)
            firstPersonCameras = new Camera1Person[0];

        if (firstStates.Length != firstPersonCameras.Length)
            firstStates = new FirstPersonState[firstPersonCameras.Length];

        for (int i = 0; i < firstPersonCameras.Length; i++)
        {
            if (firstStates[i].salvo && firstStates[i].cam == firstPersonCameras[i])
                continue;

            Camera1Person cam = firstPersonCameras[i];
            firstStates[i].cam = cam;
            if (cam == null)
                continue;

            firstStates[i].aceitarInputMouse = cam.aceitarInputMouse;
            firstStates[i].usarZoom = cam.usarZoom;
            firstStates[i].travarCursor = cam.travarCursorAoAtivar;
            firstStates[i].exibirMira = cam.exibirMira;
            firstStates[i].salvo = true;
        }
    }

    private void AjustarGetItemStates()
    {
        if (getItems == null)
            getItems = new GetItemV2[0];

        if (getItemStates.Length != getItems.Length)
            getItemStates = new GetItemState[getItems.Length];

        for (int i = 0; i < getItems.Length; i++)
        {
            if (getItemStates[i].salvo && getItemStates[i].item == getItems[i])
                continue;

            GetItemV2 item = getItems[i];
            getItemStates[i].item = item;
            if (item == null)
                continue;

            getItemStates[i].enabled = item.enabled;
            getItemStates[i].salvo = true;
        }
    }
}
