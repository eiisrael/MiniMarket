using UnityEngine;

/// <summary>
/// Diagnóstico F10 do Camera System V2.
///
/// Atalho:
/// - F10: abre/fecha o painel de diagnóstico na tela.
///
/// Não depende dos scripts antigos CameraGTAFollowHardcore, CameraRealtimeLogger ou CAM_GetItem.
/// Pode ficar no CameraSystemV2 ou ser criado automaticamente em runtime.
/// </summary>
[DefaultExecutionOrder(35000)]
public class CameraV2F10Diagnostics : MonoBehaviour
{
    [Header("Ativação")]
    public bool ativo = true;
    public KeyCode teclaAbrirFechar = KeyCode.F10;
    public bool iniciarAberto = false;
    public bool criarAutomaticamente = true;

    [Header("Referências")]
    public CameraV2Controller controller;
    public Camera3Person thirdPersonCAM;
    public Camera1Person firstPersonCAM;
    public GetItemV2 getItem;

    [Header("Busca Automática")]
    public bool procurarAutomaticamente = true;
    [Min(0.1f)] public float intervaloBusca = 1f;

    [Header("Visual")]
    public Vector2 posicao = new Vector2(18f, 18f);
    public Vector2 tamanho = new Vector2(520f, 520f);
    public int tamanhoFonte = 13;
    public bool mostrarAjuda = true;

    [Header("Debug")]
    public bool logarNoConsoleAoAbrir = false;

    private static CameraV2F10Diagnostics instancia;
    private bool aberto;
    private Rect janela;
    private Vector2 scroll;
    private float proximaBusca;
    private GUIStyle labelStyle;
    private GUIStyle titleStyle;
    private GUIStyle okStyle;
    private GUIStyle warnStyle;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void CriarAuto()
    {
        if (instancia != null)
            return;

        GameObject go = new GameObject("MiniMarket_CameraV2F10Diagnostics");
        DontDestroyOnLoad(go);
        instancia = go.AddComponent<CameraV2F10Diagnostics>();
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
        aberto = iniciarAberto;
        janela = new Rect(posicao.x, posicao.y, tamanho.x, tamanho.y);
        ResolverReferencias(true);
    }

    private void Update()
    {
        if (!ativo)
            return;

        if (procurarAutomaticamente && Time.unscaledTime >= proximaBusca)
        {
            proximaBusca = Time.unscaledTime + Mathf.Max(0.1f, intervaloBusca);
            ResolverReferencias(false);
        }

        if (Input.GetKeyDown(teclaAbrirFechar))
        {
            aberto = !aberto;
            if (logarNoConsoleAoAbrir)
                Debug.Log("[CameraV2F10Diagnostics] " + (aberto ? "Aberto" : "Fechado"));
        }
    }

    private void OnGUI()
    {
        if (!ativo || !aberto)
            return;

        PrepararStyles();
        janela = GUI.Window(70021, janela, DesenharJanela, "MiniMarket Camera V2 Diagnostics - F10");
    }

    private void DesenharJanela(int id)
    {
        scroll = GUILayout.BeginScrollView(scroll);

        LinhaTitulo("Status Geral");
        Linha("Controller", controller != null ? "OK" : "NÃO ENCONTRADO", controller != null);
        Linha("ThirdPersonCAM", thirdPersonCAM != null ? "OK" : "NÃO ENCONTRADO", thirdPersonCAM != null);
        Linha("FirstPersonCAM", firstPersonCAM != null ? "OK" : "NÃO ENCONTRADO", firstPersonCAM != null);
        Linha("GetItemV2", getItem != null ? "OK" : "NÃO ENCONTRADO", getItem != null);
        Linha("Câmera ativa", ObterCameraAtiva(), true);
        Linha("TimeScale", Time.timeScale.ToString("0.000"), Time.timeScale > 0f);
        Linha("FPS aprox", ObterFPS(), true);

        GUILayout.Space(8);
        LinhaTitulo("ThirdPersonCAM");
        if (thirdPersonCAM != null)
        {
            Linha("GameObject", thirdPersonCAM.name, true);
            Linha("Camera ativa", thirdPersonCAM.cameraAtiva.ToString(), thirdPersonCAM.cameraAtiva);
            Linha("Target", thirdPersonCAM.target != null ? thirdPersonCAM.target.name : "null", thirdPersonCAM.target != null);
            Linha("Yaw / Pitch", thirdPersonCAM.YawAtual.ToString("0.00") + " / " + thirdPersonCAM.PitchAtual.ToString("0.00"), true);
            Linha("Zoom", thirdPersonCAM.ZoomAtivo.ToString(), true);
            Linha("Distância", thirdPersonCAM.distancia.ToString("0.00"), true);
            Linha("Colisão", thirdPersonCAM.usarColisao.ToString(), true);
            Linha("Auto Align", thirdPersonCAM.autoAlinharAtrasDoPersonagem.ToString(), !thirdPersonCAM.autoAlinharAtrasDoPersonagem);
        }
        else
        {
            LinhaTexto("ThirdPersonCAM não encontrado na cena.", false);
        }

        GUILayout.Space(8);
        LinhaTitulo("FirstPersonCAM");
        if (firstPersonCAM != null)
        {
            Linha("GameObject", firstPersonCAM.name, true);
            Linha("Camera ativa", firstPersonCAM.cameraAtiva.ToString(), true);
            Linha("Corpo", firstPersonCAM.corpoPersonagem != null ? firstPersonCAM.corpoPersonagem.name : "null", firstPersonCAM.corpoPersonagem != null);
            Linha("POV", firstPersonCAM.pontoPOV != null ? firstPersonCAM.pontoPOV.name : "null", firstPersonCAM.pontoPOV != null);
            Linha("Yaw / Pitch", firstPersonCAM.YawAtual.ToString("0.00") + " / " + firstPersonCAM.PitchAtual.ToString("0.00"), true);
            Linha("Zoom", firstPersonCAM.ZoomAtivo.ToString(), true);
            Linha("HeadBob/Sway", "desativados por configuração", firstPersonCAM.desativarHeadBob && firstPersonCAM.desativarSway);
        }
        else
        {
            LinhaTexto("FirstPersonCAM não encontrado na cena.", false);
        }

        GUILayout.Space(8);
        LinhaTitulo("GetItemV2");
        if (getItem != null)
        {
            Linha("Enabled", getItem.enabled.ToString(), true);
            Linha("Selecionado", getItem.Selecionado != null ? getItem.Selecionado.name : "null", true);
            Linha("Pegando", getItem.Pegando != null ? getItem.Pegando.name : "null", true);
            Linha("Está pegando", getItem.EstaPegando.ToString(), true);
            Linha("Distância seleção", getItem.distanciaSelecao.ToString("0.00"), true);
            Linha("Raio seleção", getItem.raioSelecao.ToString("0.00"), true);
        }
        else
        {
            LinhaTexto("GetItemV2 não encontrado. Coloque GetItemV2 no FirstPersonCAM.", false);
        }

        if (mostrarAjuda)
        {
            GUILayout.Space(8);
            LinhaTitulo("Ajuda rápida");
            LinhaTexto("F10 abre/fecha este painel.", true);
            LinhaTexto("Se CameraV2Controller aparecer null, coloque este script no CameraSystemV2 ou deixe a busca automática ligada.", true);
            LinhaTexto("Se GetItemV2 aparecer null, adicione GetItemV2 no FirstPersonCAM.", true);
            LinhaTexto("Se houver erro vermelho no Console, o Unity não deixa adicionar scripts novos.", true);
        }

        GUILayout.EndScrollView();
        GUI.DragWindow(new Rect(0, 0, 10000, 22));
    }

    private void ResolverReferencias(bool forcar)
    {
        if (!forcar && controller != null && thirdPersonCAM != null && firstPersonCAM != null && getItem != null)
            return;

        if (controller == null)
            controller = Object.FindFirstObjectByType<CameraV2Controller>(FindObjectsInactive.Include);

        if (thirdPersonCAM == null)
            thirdPersonCAM = Object.FindFirstObjectByType<Camera3Person>(FindObjectsInactive.Include);

        if (firstPersonCAM == null)
            firstPersonCAM = Object.FindFirstObjectByType<Camera1Person>(FindObjectsInactive.Include);

        if (getItem == null)
        {
            if (firstPersonCAM != null)
                getItem = firstPersonCAM.GetComponent<GetItemV2>();

            if (getItem == null)
                getItem = Object.FindFirstObjectByType<GetItemV2>(FindObjectsInactive.Include);
        }
    }

    private string ObterCameraAtiva()
    {
        if (controller != null)
            return controller.PrimeiraPessoaAtiva ? "FirstPersonCAM" : "ThirdPersonCAM";

        if (firstPersonCAM != null && firstPersonCAM.cameraAtiva)
            return "FirstPersonCAM";

        if (thirdPersonCAM != null && thirdPersonCAM.cameraAtiva)
            return "ThirdPersonCAM";

        return "Nenhuma";
    }

    private string ObterFPS()
    {
        float dt = Mathf.Max(Time.unscaledDeltaTime, 0.0001f);
        return (1f / dt).ToString("0");
    }

    private void PrepararStyles()
    {
        if (labelStyle != null)
            return;

        labelStyle = new GUIStyle(GUI.skin.label);
        labelStyle.fontSize = tamanhoFonte;
        labelStyle.normal.textColor = Color.white;
        labelStyle.wordWrap = true;

        titleStyle = new GUIStyle(labelStyle);
        titleStyle.fontStyle = FontStyle.Bold;
        titleStyle.fontSize = tamanhoFonte + 2;
        titleStyle.normal.textColor = new Color(1f, 0.85f, 0.25f, 1f);

        okStyle = new GUIStyle(labelStyle);
        okStyle.normal.textColor = new Color(0.45f, 1f, 0.65f, 1f);

        warnStyle = new GUIStyle(labelStyle);
        warnStyle.normal.textColor = new Color(1f, 0.35f, 0.35f, 1f);
    }

    private void LinhaTitulo(string texto)
    {
        GUILayout.Label(texto, titleStyle);
    }

    private void Linha(string nome, string valor, bool ok)
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label(nome + ":", labelStyle, GUILayout.Width(170));
        GUILayout.Label(valor, ok ? okStyle : warnStyle);
        GUILayout.EndHorizontal();
    }

    private void LinhaTexto(string texto, bool ok)
    {
        GUILayout.Label(texto, ok ? okStyle : warnStyle);
    }
}
