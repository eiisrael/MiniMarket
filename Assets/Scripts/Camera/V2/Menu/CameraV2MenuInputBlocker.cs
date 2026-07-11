using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Bloqueia mouse, zoom e GetItem enquanto o menu estiver aberto.
///
/// Regras:
/// - caches de UI são atualizados apenas por intervalo;
/// - estados são alterados somente ao abrir/fechar;
/// - componentes colocados como filhos não chamam DontDestroyOnLoad;
/// - ao fechar, cursor e câmera voltam sem precisar marcar/desmarcar Ativo.
/// </summary>
[DefaultExecutionOrder(-50000)]
[DisallowMultipleComponent]
public class CameraV2MenuInputBlocker : MonoBehaviour
{
    [Header("Ativacao")]
    public bool ativo = true;
    public bool criarAutomaticamente = true;

    [Header("Deteccao Manual do Menu")]
    public GameObject[] objetosMenu;
    public CanvasGroup[] canvasGroupsMenu;
    public bool procurarCanvasGroupNosFilhos = true;
    public bool detectarGraficosUIVisiveisSemCanvasGroup = true;

    [Header("Deteccao Automatica")]
    public bool detectarMenusAutomaticamentePorNome = true;
    public string[] nomesMenu = { "menu", "pause", "pausa", "esc" };
    public bool exigirCanvasGroupVisivel = true;
    public bool considerarCursorLivreComoMenuAberto = true;
    public bool ignorarCursorLivreQuandoMenuManualConfigurado = true;
    public bool desbloquearCursorComMenuAberto = true;
    public bool retravarCursorAoFecharMenu = true;

    [Header("Restauracao Segura")]
    public bool forcarInputCameraAoFecharMenu = true;
    public bool restaurarGetItemAoFecharMenu = true;

    [Header("Bloqueio")]
    public bool bloquearThirdPersonMouse = true;
    public bool bloquearFirstPersonMouse = true;
    public bool bloquearZoomEnquantoMenuAberto = true;
    public bool bloquearGetItemEnquantoMenuAberto = true;
    public bool soltarObjetoAoAbrirMenu = true;

    [Header("Performance / Busca")]
    public bool procurarAutomaticamente = true;
    [Min(0.25f)] public float intervaloBusca = 1f;

    [Header("Debug")]
    public bool logarMudancaEstado;

    public static bool MenuAberto { get; private set; }

    private static CameraV2MenuInputBlocker instancia;
    private Camera3Person[] thirdPersonCameras = new Camera3Person[0];
    private Camera1Person[] firstPersonCameras = new Camera1Person[0];
    private GetItemV2[] getItems = new GetItemV2[0];
    private CanvasGroup[] canvasGroupsAuto = new CanvasGroup[0];
    private readonly List<MenuManualCache> cachesManuais = new List<MenuManualCache>(4);

    private ThirdPersonState[] thirdStates = new ThirdPersonState[0];
    private FirstPersonState[] firstStates = new FirstPersonState[0];
    private GetItemState[] getItemStates = new GetItemState[0];

    private float proximaBusca;
    private bool jaViuCursorTravado;
    private bool bloqueioAplicado;

    private sealed class MenuManualCache
    {
        public GameObject raiz;
        public CanvasGroup[] grupos = new CanvasGroup[0];
        public Graphic[] graficos = new Graphic[0];
        public bool temGrupos;
    }

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

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetarStatics()
    {
        instancia = null;
        MenuAberto = false;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void CriarAuto()
    {
        if (instancia != null)
            return;

        GameObject go = new GameObject("MiniMarket_CameraV2MenuInputBlocker");
        DontDestroyOnLoad(go);
        go.AddComponent<CameraV2MenuInputBlocker>();
    }

    private void Awake()
    {
        if (instancia != null && instancia != this)
        {
            Destroy(gameObject);
            return;
        }

        instancia = this;

        // Objetos filhos não podem receber DontDestroyOnLoad diretamente.
        // A instância automática já nasce como objeto raiz e persistente.
        if (transform.parent == null)
            DontDestroyOnLoad(gameObject);

        AtualizarCaches(true);
    }

    private void OnDisable()
    {
        RestaurarTudo(true);
        MenuAberto = false;
    }

    private void OnDestroy()
    {
        if (instancia == this)
            instancia = null;
    }

    private void Update()
    {
        if (!ativo)
        {
            if (MenuAberto || bloqueioAplicado)
                RestaurarTudo(true);

            MenuAberto = false;
            return;
        }

        if (Cursor.lockState == CursorLockMode.Locked)
            jaViuCursorTravado = true;

        if (procurarAutomaticamente && Time.unscaledTime >= proximaBusca)
        {
            proximaBusca = Time.unscaledTime + Mathf.Max(0.25f, intervaloBusca);
            AtualizarCaches(false);
        }

        bool aberto = DetectarMenuAbertoSemAlocacao();
        if (aberto == MenuAberto)
            return;

        MenuAberto = aberto;

        if (aberto)
            AplicarBloqueio();
        else
            RestaurarTudo(false);

        if (logarMudancaEstado)
            Debug.Log("[CameraV2MenuInputBlocker] Menu " + (aberto ? "aberto: input bloqueado." : "fechado: input restaurado."));
    }

    private void LateUpdate()
    {
        if (!ativo)
            return;

        if (MenuAberto && desbloquearCursorComMenuAberto)
        {
            if (Cursor.lockState != CursorLockMode.None)
                Cursor.lockState = CursorLockMode.None;

            if (!Cursor.visible)
                Cursor.visible = true;
        }
        else if (!MenuAberto && retravarCursorAoFecharMenu && AlgumaCameraAtiva())
        {
            if (Cursor.lockState != CursorLockMode.Locked)
                Cursor.lockState = CursorLockMode.Locked;

            if (Cursor.visible)
                Cursor.visible = false;

            jaViuCursorTravado = true;
        }
    }

    [ContextMenu("Menu Input/Rebuscar referências agora")]
    public void RebuscarAgora()
    {
        AtualizarCaches(true);
    }

    private void AtualizarCaches(bool forcar)
    {
        if (forcar || PrecisaRebuscarCameras())
        {
            thirdPersonCameras = Object.FindObjectsByType<Camera3Person>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            firstPersonCameras = Object.FindObjectsByType<Camera1Person>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            getItems = Object.FindObjectsByType<GetItemV2>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        }

        ReconstruirCacheManual();

        if (!TemMenuManualConfigurado() && detectarMenusAutomaticamentePorNome)
            canvasGroupsAuto = Object.FindObjectsByType<CanvasGroup>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        else
            canvasGroupsAuto = new CanvasGroup[0];
    }

    private bool PrecisaRebuscarCameras()
    {
        return ArrayVazioOuInvalido(thirdPersonCameras) ||
               ArrayVazioOuInvalido(firstPersonCameras) ||
               getItems == null;
    }

    private bool ArrayVazioOuInvalido<T>(T[] array) where T : Object
    {
        if (array == null || array.Length == 0)
            return true;

        for (int i = 0; i < array.Length; i++)
        {
            if (array[i] != null)
                return false;
        }

        return true;
    }

    private void ReconstruirCacheManual()
    {
        cachesManuais.Clear();

        if (objetosMenu == null)
            return;

        for (int i = 0; i < objetosMenu.Length; i++)
        {
            GameObject raiz = objetosMenu[i];
            if (raiz == null)
                continue;

            MenuManualCache cache = new MenuManualCache();
            cache.raiz = raiz;

            if (procurarCanvasGroupNosFilhos)
            {
                cache.grupos = raiz.GetComponentsInChildren<CanvasGroup>(true);
            }
            else
            {
                CanvasGroup grupoRaiz = raiz.GetComponent<CanvasGroup>();
                cache.grupos = grupoRaiz != null ? new[] { grupoRaiz } : new CanvasGroup[0];
            }

            cache.temGrupos = cache.grupos != null && cache.grupos.Length > 0;

            if (!cache.temGrupos && detectarGraficosUIVisiveisSemCanvasGroup)
                cache.graficos = raiz.GetComponentsInChildren<Graphic>(true);

            cachesManuais.Add(cache);
        }
    }

    private bool DetectarMenuAbertoSemAlocacao()
    {
        bool manual = TemMenuManualConfigurado();

        if (manual)
        {
            if (CanvasGroupManualAberto() || ObjetoManualAberto())
                return true;

            if (ignorarCursorLivreQuandoMenuManualConfigurado)
                return false;
        }

        if (!manual && detectarMenusAutomaticamentePorNome && CanvasGroupAutoAberto())
            return true;

        return considerarCursorLivreComoMenuAberto &&
               jaViuCursorTravado &&
               Cursor.lockState != CursorLockMode.Locked &&
               Cursor.visible;
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

    private bool ObjetoManualAberto()
    {
        for (int i = 0; i < cachesManuais.Count; i++)
        {
            MenuManualCache cache = cachesManuais[i];
            if (cache == null || cache.raiz == null || !cache.raiz.activeInHierarchy)
                continue;

            if (cache.temGrupos)
            {
                for (int g = 0; g < cache.grupos.Length; g++)
                {
                    if (CanvasGroupEstaAberto(cache.grupos[g]))
                        return true;
                }

                continue;
            }

            if (cache.graficos != null && cache.graficos.Length > 0)
            {
                for (int g = 0; g < cache.graficos.Length; g++)
                {
                    Graphic grafico = cache.graficos[g];
                    if (grafico != null &&
                        grafico.gameObject.activeInHierarchy &&
                        grafico.enabled &&
                        grafico.color.a > 0.05f)
                    {
                        return true;
                    }
                }

                continue;
            }

            // Sem CanvasGroup ou Graphic: activeInHierarchy representa o estado.
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
            CanvasGroup grupo = canvasGroupsAuto[i];
            if (grupo != null && NomePareceMenu(grupo.transform) && CanvasGroupEstaAberto(grupo))
                return true;
        }

        return false;
    }

    private bool CanvasGroupEstaAberto(CanvasGroup grupo)
    {
        if (grupo == null || !grupo.gameObject.activeInHierarchy)
            return false;

        return !exigirCanvasGroupVisivel ||
               (grupo.alpha > 0.05f && (grupo.blocksRaycasts || grupo.interactable));
    }

    private bool NomePareceMenu(Transform atual)
    {
        while (atual != null)
        {
            string nome = atual.name.ToLowerInvariant();

            for (int i = 0; i < nomesMenu.Length; i++)
            {
                string chave = nomesMenu[i];
                if (!string.IsNullOrEmpty(chave) && nome.Contains(chave.ToLowerInvariant()))
                    return true;
            }

            atual = atual.parent;
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

        for (int i = 0; i < thirdPersonCameras.Length; i++)
        {
            Camera3Person cam = thirdPersonCameras[i];
            if (cam == null)
                continue;

            if (bloquearThirdPersonMouse)
                cam.aceitarInputMouse = false;

            cam.travarCursorAoAtivar = false;

            if (bloquearZoomEnquantoMenuAberto)
                cam.usarZoom = false;
        }

        for (int i = 0; i < firstPersonCameras.Length; i++)
        {
            Camera1Person cam = firstPersonCameras[i];
            if (cam == null)
                continue;

            if (bloquearFirstPersonMouse)
                cam.aceitarInputMouse = false;

            cam.travarCursorAoAtivar = false;

            if (bloquearZoomEnquantoMenuAberto)
                cam.usarZoom = false;

            cam.exibirMira = false;
        }

        if (!bloquearGetItemEnquantoMenuAberto)
            return;

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

    private void SalvarEstadosAtuais()
    {
        thirdStates = new ThirdPersonState[thirdPersonCameras.Length];
        for (int i = 0; i < thirdStates.Length; i++)
        {
            Camera3Person cam = thirdPersonCameras[i];
            thirdStates[i] = new ThirdPersonState
            {
                cam = cam,
                aceitarInputMouse = cam != null && cam.aceitarInputMouse,
                usarZoom = cam != null && cam.usarZoom,
                travarCursor = cam != null && cam.travarCursorAoAtivar
            };
        }

        firstStates = new FirstPersonState[firstPersonCameras.Length];
        for (int i = 0; i < firstStates.Length; i++)
        {
            Camera1Person cam = firstPersonCameras[i];
            firstStates[i] = new FirstPersonState
            {
                cam = cam,
                aceitarInputMouse = cam != null && cam.aceitarInputMouse,
                usarZoom = cam != null && cam.usarZoom,
                travarCursor = cam != null && cam.travarCursorAoAtivar,
                exibirMira = cam != null && cam.exibirMira
            };
        }

        getItemStates = new GetItemState[getItems.Length];
        for (int i = 0; i < getItemStates.Length; i++)
        {
            getItemStates[i] = new GetItemState
            {
                item = getItems[i],
                enabled = getItems[i] != null && getItems[i].enabled
            };
        }
    }

    private void RestaurarTudo(bool forcar)
    {
        if (!bloqueioAplicado && !forcar)
            return;

        for (int i = 0; i < thirdStates.Length; i++)
        {
            Camera3Person cam = thirdStates[i].cam;
            if (cam == null)
                continue;

            cam.aceitarInputMouse = forcarInputCameraAoFecharMenu
                ? true
                : thirdStates[i].aceitarInputMouse;

            cam.usarZoom = thirdStates[i].usarZoom;
            cam.travarCursorAoAtivar = thirdStates[i].travarCursor;
        }

        for (int i = 0; i < firstStates.Length; i++)
        {
            Camera1Person cam = firstStates[i].cam;
            if (cam == null)
                continue;

            cam.aceitarInputMouse = forcarInputCameraAoFecharMenu
                ? true
                : firstStates[i].aceitarInputMouse;

            cam.usarZoom = firstStates[i].usarZoom;
            cam.travarCursorAoAtivar = firstStates[i].travarCursor;
            cam.exibirMira = firstStates[i].exibirMira;
        }

        for (int i = 0; i < getItemStates.Length; i++)
        {
            GetItemV2 item = getItemStates[i].item;
            if (item == null)
                continue;

            if (restaurarGetItemAoFecharMenu)
                item.enabled = getItemStates[i].enabled;
        }

        bloqueioAplicado = false;

        if (retravarCursorAoFecharMenu && AlgumaCameraAtiva())
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            jaViuCursorTravado = true;
        }
    }

    private bool AlgumaCameraAtiva()
    {
        for (int i = 0; i < thirdPersonCameras.Length; i++)
        {
            if (thirdPersonCameras[i] != null && thirdPersonCameras[i].cameraAtiva)
                return true;
        }

        for (int i = 0; i < firstPersonCameras.Length; i++)
        {
            if (firstPersonCameras[i] != null && firstPersonCameras[i].cameraAtiva)
                return true;
        }

        return false;
    }
}
