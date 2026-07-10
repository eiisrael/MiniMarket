using UnityEngine;

/// <summary>
/// Bloqueia input de mouse da Camera V2 enquanto o menu estiver aberto.
///
/// Correção V2.1:
/// - Se você arrastar o Menu ESC em Objetos Menu/Canvas Groups Menu, a detecção manual manda.
/// - O fallback de cursor livre NÃO mantém o menu como aberto quando existe menu manual configurado.
/// - Ao fechar o menu, restaura os valores originais e trava o cursor de novo para a câmera voltar a girar.
/// </summary>
[DefaultExecutionOrder(-50000)]
public class CameraV2MenuInputBlocker : MonoBehaviour
{
    [Header("Ativação")]
    public bool ativo = true;
    public bool criarAutomaticamente = true;

    [Header("Detecção Manual do Menu")]
    [Tooltip("Arraste aqui o GameObject do Menu ESC. Quando preenchido, esta detecção tem prioridade.")]
    public GameObject[] objetosMenu;

    [Tooltip("Arraste aqui CanvasGroups do Menu ESC, se houver.")]
    public CanvasGroup[] canvasGroupsMenu;

    [Header("Detecção Automática")]
    public bool detectarMenusAutomaticamentePorNome = true;
    public string[] nomesMenu = { "menu", "pause", "pausa", "esc" };
    public bool exigirCanvasGroupVisivel = true;

    [Tooltip("Use apenas como fallback. Se houver menu manual configurado, este teste é ignorado por padrão.")]
    public bool considerarCursorLivreComoMenuAberto = true;

    [Tooltip("Ligado: quando Objetos Menu ou Canvas Groups Menu estiverem preenchidos, o cursor livre não segura o menu como aberto.")]
    public bool ignorarCursorLivreQuandoMenuManualConfigurado = true;

    public bool desbloquearCursorComMenuAberto = true;
    public bool retravarCursorAoFecharMenu = true;

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
    private Camera3Person[] thirdPersonCameras = new Camera3Person[0];
    private Camera1Person[] firstPersonCameras = new Camera1Person[0];
    private GetItemV2[] getItems = new GetItemV2[0];
    private CanvasGroup[] canvasGroupsAuto = new CanvasGroup[0];
    private float proximaBusca;
    private bool ultimoMenuAberto;
    private bool jaViuCursorTravado;
    private bool bloqueioAplicado;

    private struct ThirdPersonState
    {
        public Camera3Person cam;
        public bool aceitarInputMouse;
        public bool usarZoom;
        public bool travarCursor;
    }

    private struct FirstPersonState
    {
        public Camera1Person cam;
        public bool aceitarInputMouse;
        public bool usarZoom;
        public bool travarCursor;
        public bool exibirMira;
    }

    private struct GetItemState
    {
        public GetItemV2 item;
        public bool enabled;
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
            if (bloqueioAplicado)
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
        if (!ativo)
            return;

        if (MenuAberto)
        {
            if (desbloquearCursorComMenuAberto)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
        }
        else if (retravarCursorAoFecharMenu && AlgumaCameraAtivaQuerCursorTravado())
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
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
        bool temMenuManual = TemMenuManualConfigurado();

        if (temMenuManual)
        {
            if (ObjetoMenuManualAberto())
                return true;

            if (CanvasGroupManualAberto())
                return true;

            if (ignorarCursorLivreQuandoMenuManualConfigurado)
                return false;
        }

        if (detectarMenusAutomaticamentePorNome && CanvasGroupAutoAberto())
            return true;

        if (considerarCursorLivreComoMenuAberto && jaViuCursorTravado && Cursor.lockState != CursorLockMode.Locked && Cursor.visible)
            return true;

        return false;
    }

    private bool TemMenuManualConfigurado()
    {
        if (objetosMenu != null)
        {
            for (int i = 0; i < objetosMenu.Length; i++)
            {
                if (objetosMenu[i] != null)
                    return true;
            }
        }

        if (canvasGroupsMenu != null)
        {
            for (int i = 0; i < canvasGroupsMenu.Length; i++)
            {
                if (canvasGroupsMenu[i] != null)
                    return true;
            }
        }

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
        if (!bloqueioAplicado)
            SalvarEstadosAtuais();

        bloqueioAplicado = true;

        if (desbloquearCursorComMenuAberto)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        if (bloquearThirdPersonMouse && thirdPersonCameras != null)
        {
            for (int i = 0; i < thirdPersonCameras.Length; i++)
            {
                Camera3Person cam = thirdPersonCameras[i];
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
            for (int i = 0; i < firstPersonCameras.Length; i++)
            {
                Camera1Person cam = firstPersonCameras[i];
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
            for (int i = 0; i < getItems.Length; i++)
            {
                GetItemV2 item = getItems[i];
                if (item == null)
                    continue;

                if (soltarObjetoAoAbrirMenu)
                    item.SoltarObjeto();

                item.enabled = false;
            }
        }
    }

    private void SalvarEstadosAtuais()
    {
        BuscarReferencias(true);

        thirdStates = new ThirdPersonState[thirdPersonCameras != null ? thirdPersonCameras.Length : 0];
        for (int i = 0; i < thirdStates.Length; i++)
        {
            Camera3Person cam = thirdPersonCameras[i];
            thirdStates[i].cam = cam;
            if (cam == null)
                continue;

            thirdStates[i].aceitarInputMouse = cam.aceitarInputMouse;
            thirdStates[i].usarZoom = cam.usarZoom;
            thirdStates[i].travarCursor = cam.travarCursorAoAtivar;
        }

        firstStates = new FirstPersonState[firstPersonCameras != null ? firstPersonCameras.Length : 0];
        for (int i = 0; i < firstStates.Length; i++)
        {
            Camera1Person cam = firstPersonCameras[i];
            firstStates[i].cam = cam;
            if (cam == null)
                continue;

            firstStates[i].aceitarInputMouse = cam.aceitarInputMouse;
            firstStates[i].usarZoom = cam.usarZoom;
            firstStates[i].travarCursor = cam.travarCursorAoAtivar;
            firstStates[i].exibirMira = cam.exibirMira;
        }

        getItemStates = new GetItemState[getItems != null ? getItems.Length : 0];
        for (int i = 0; i < getItemStates.Length; i++)
        {
            GetItemV2 item = getItems[i];
            getItemStates[i].item = item;
            if (item == null)
                continue;

            getItemStates[i].enabled = item.enabled;
        }
    }

    private void RestaurarTudo()
    {
        if (!bloqueioAplicado)
            return;

        for (int i = 0; i < thirdStates.Length; i++)
        {
            Camera3Person cam = thirdStates[i].cam;
            if (cam == null)
                continue;

            cam.aceitarInputMouse = thirdStates[i].aceitarInputMouse;
            cam.usarZoom = thirdStates[i].usarZoom;
            cam.travarCursorAoAtivar = thirdStates[i].travarCursor;
        }

        for (int i = 0; i < firstStates.Length; i++)
        {
            Camera1Person cam = firstStates[i].cam;
            if (cam == null)
                continue;

            cam.aceitarInputMouse = firstStates[i].aceitarInputMouse;
            cam.usarZoom = firstStates[i].usarZoom;
            cam.travarCursorAoAtivar = firstStates[i].travarCursor;
            cam.exibirMira = firstStates[i].exibirMira;
        }

        for (int i = 0; i < getItemStates.Length; i++)
        {
            GetItemV2 item = getItemStates[i].item;
            if (item == null)
                continue;

            item.enabled = getItemStates[i].enabled;
        }

        bloqueioAplicado = false;

        if (retravarCursorAoFecharMenu && AlgumaCameraAtivaQuerCursorTravado())
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            jaViuCursorTravado = true;
        }
    }

    private bool AlgumaCameraAtivaQuerCursorTravado()
    {
        if (thirdPersonCameras != null)
        {
            for (int i = 0; i < thirdPersonCameras.Length; i++)
            {
                Camera3Person cam = thirdPersonCameras[i];
                if (cam != null && cam.cameraAtiva && cam.travarCursorAoAtivar)
                    return true;
            }
        }

        if (firstPersonCameras != null)
        {
            for (int i = 0; i < firstPersonCameras.Length; i++)
            {
                Camera1Person cam = firstPersonCameras[i];
                if (cam != null && cam.cameraAtiva && cam.travarCursorAoAtivar)
                    return true;
            }
        }

        return false;
    }
}
