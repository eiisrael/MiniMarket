using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Controla somente a logica do painel de confirmacao da BuyScene.
///
/// O visual fica 100% nos objetos do Canvas:
/// - PainelWarning / RawImage
/// - TextAsking / Text
/// - ButtonConfirm / RawImage / BuySceneUIImageButton
/// - TextConfirm / Text
/// - ButtonClose / RawImage / BuySceneUIImageButton
///
/// Este script apenas:
/// - Encontra as referencias.
/// - Abre/fecha o painel.
/// - Atualiza o texto se solicitado.
/// - Liga os eventos dos botoes.
/// - Garante EventSystem/GraphicRaycaster para os botoes clicarem.
/// </summary>
public class BuyScenePurchaseConfirmationPanel : MonoBehaviour
{
    [Header("Referencias Obrigatorias")]
    [Tooltip("Objeto raiz da janela. Normalmente: PainelWarning. O script apenas ativa/desativa esse objeto.")]
    public GameObject painelRaiz;

    [Tooltip("Texto principal da janela. Normalmente: TextAsking.")]
    public Text textoPrincipal;

    [Tooltip("Botao de confirmar. Normalmente: ButtonConfirm com BuySceneUIImageButton.")]
    public BuySceneUIImageButton botaoConfirmar;

    [Tooltip("Botao de fechar. Normalmente: ButtonClose com BuySceneUIImageButton.")]
    public BuySceneUIImageButton botaoFechar;

    [Header("Busca Automatica")]
    [Tooltip("Se alguma referencia estiver vazia, tenta encontrar pelos nomes dos filhos e tambem pelos pais das referencias preenchidas.")]
    public bool procurarReferenciasAutomaticamente = true;

    [Tooltip("Se o Painel Raiz estiver vazio, tenta descobrir pelo pai do TextAsking/ButtonConfirm/ButtonClose.")]
    public bool inferirPainelRaizPelasReferencias = true;

    [Tooltip("Garante que exista um EventSystem na cena para os botoes UI receberem clique/hover.")]
    public bool garantirEventSystem = true;

    [Tooltip("Garante GraphicRaycaster no Canvas pai da janela. Sem isso, botoes de UI podem nao receber clique.")]
    public bool garantirGraphicRaycasterNoCanvas = true;

    [Header("Comportamento Inicial")]
    [Tooltip("Recomendado ligado. Esconde o painel ao iniciar o jogo para ele aparecer apenas quando clicar no terreno.")]
    public bool ocultarPainelAoIniciar = true;

    [Header("Texto")]
    [Tooltip("Se ligado, o script monta a mensagem ao abrir o painel. Se desligado, o texto fica exatamente como esta no componente Text.")]
    public bool atualizarTextoPeloScript = true;

    [TextArea(2, 4)]
    public string textoConfirmacao = "Você tem certeza que deseja comprar?";

    [Tooltip("Adiciona o nome do terreno abaixo da frase de confirmacao.")]
    public bool exibirNomeDoTerreno = true;

    [Tooltip("Adiciona o preco do terreno abaixo da frase de confirmacao.")]
    public bool exibirPrecoDoTerreno = true;

    [TextArea(2, 3)]
    public string textoGoldInsuficiente = "Gold insuficiente para comprar este terreno.";

    [Header("Seguranca dos Textos")]
    [Tooltip("Recomendado ligado. Evita que o Text fique bloqueando clique nos botoes ou no painel.")]
    public bool forcarRaycastTargetDosTextosDesligado = true;

    [Header("Debug")]
    public bool logarEventos;

    public static bool ExistePainelAberto { get; private set; }

    public bool PainelAberto => painelRaiz != null && painelRaiz.activeSelf;

    private BuyableLandAreaMarker terrenoAtual;
    private Action<BuyableLandAreaMarker> aoConfirmarCompra;
    private Action aoFecharPainel;
    private bool iniciou;

    private void Awake()
    {
        Inicializar();

        if (ocultarPainelAoIniciar)
            OcultarSemCallback();

        iniciou = true;
    }

    private void OnEnable()
    {
        Inicializar();

        if (!iniciou && ocultarPainelAoIniciar)
            OcultarSemCallback();
    }

    private void Start()
    {
        Inicializar();

        if (ocultarPainelAoIniciar && aoConfirmarCompra == null)
            OcultarSemCallback();
    }

    private void OnDisable()
    {
        ExistePainelAberto = false;
    }

    private void Reset()
    {
        procurarReferenciasAutomaticamente = true;
        inferirPainelRaizPelasReferencias = true;
        garantirEventSystem = true;
        garantirGraphicRaycasterNoCanvas = true;
        ocultarPainelAoIniciar = true;
        ResolverReferenciasAutomaticas();
    }

    private void OnValidate()
    {
        if (!Application.isPlaying)
            return;

        Inicializar();
    }

    public void Mostrar(BuyableLandAreaMarker terreno, Action<BuyableLandAreaMarker> aoConfirmar, Action aoFechar)
    {
        Inicializar();

        terrenoAtual = terreno;
        aoConfirmarCompra = aoConfirmar;
        aoFecharPainel = aoFechar;

        if (atualizarTextoPeloScript)
            AtualizarTextoConfirmacao();

        if (painelRaiz == null)
        {
            Debug.LogWarning("[BuyScenePurchaseConfirmationPanel] Painel Raiz nao encontrado. Arraste o objeto PainelWarning no campo Painel Raiz.");
            ExistePainelAberto = false;
            return;
        }

        painelRaiz.SetActive(true);
        ExistePainelAberto = true;

        if (logarEventos)
            Debug.Log("[BuyScenePurchaseConfirmationPanel] Abriu painel de confirmacao.");
    }

    public void MostrarMensagemErro(string mensagem)
    {
        Inicializar();

        if (textoPrincipal == null)
            return;

        textoPrincipal.text = mensagem;
    }

    public void FecharPeloBotao()
    {
        Inicializar();
        OcultarSemCallback();

        if (aoFecharPainel != null)
            aoFecharPainel.Invoke();

        LimparCallbacks();

        if (logarEventos)
            Debug.Log("[BuyScenePurchaseConfirmationPanel] Fechou painel pelo botao close.");
    }

    public void OcultarSemCallback()
    {
        ResolverPainelRaizComFallbacks();

        if (painelRaiz != null)
            painelRaiz.SetActive(false);

        ExistePainelAberto = false;
    }

    public void LimparCallbacks()
    {
        terrenoAtual = null;
        aoConfirmarCompra = null;
        aoFecharPainel = null;
    }

    private void Inicializar()
    {
        if (procurarReferenciasAutomaticamente)
            ResolverReferenciasAutomaticas();
        else
            ResolverPainelRaizComFallbacks();

        if (garantirEventSystem)
            GarantirEventSystemNaCena();

        if (garantirGraphicRaycasterNoCanvas)
            GarantirGraphicRaycaster();

        ConfigurarEventosDosBotoes();
        ConfigurarBotoesParaReceberClique();
        ConfigurarTextosSemAlterarVisual();
    }

    private void ResolverReferenciasAutomaticas()
    {
        ResolverPainelRaizComFallbacks();

        Transform raizBusca = painelRaiz != null ? painelRaiz.transform : transform;

        if (textoPrincipal == null)
        {
            Transform texto = EncontrarFilhoPorNome(raizBusca, "TextAsking");

            if (texto == null)
                texto = EncontrarFilhoPorNome(raizBusca, "TextoConfirmacao");

            if (texto == null)
                texto = EncontrarFilhoPorNome(raizBusca, "Texto_Confirmacao");

            if (texto != null)
                textoPrincipal = texto.GetComponent<Text>();
        }

        if (botaoConfirmar == null)
        {
            Transform botao = EncontrarFilhoPorNome(raizBusca, "ButtonConfirm");

            if (botao == null)
                botao = EncontrarFilhoPorNome(raizBusca, "BotaoConfirmar");

            if (botao == null)
                botao = EncontrarFilhoPorNome(raizBusca, "Botao_Confirmar");

            if (botao != null)
                botaoConfirmar = botao.GetComponent<BuySceneUIImageButton>();
        }

        if (botaoFechar == null)
        {
            Transform botao = EncontrarFilhoPorNome(raizBusca, "ButtonClose");

            if (botao == null)
                botao = EncontrarFilhoPorNome(raizBusca, "BotaoFechar");

            if (botao == null)
                botao = EncontrarFilhoPorNome(raizBusca, "Botao_Fechar");

            if (botao != null)
                botaoFechar = botao.GetComponent<BuySceneUIImageButton>();
        }

        ResolverPainelRaizComFallbacks();
    }

    private void ResolverPainelRaizComFallbacks()
    {
        if (painelRaiz != null && painelRaiz != gameObject)
            return;

        Transform painelEncontrado = EncontrarFilhoPorNome(transform, "PainelWarning");

        if (painelEncontrado == null)
            painelEncontrado = EncontrarFilhoPorNome(transform, "PanelWarning");

        if (painelEncontrado == null)
            painelEncontrado = EncontrarFilhoPorNome(transform, "Painel_Confirmacao");

        if (painelEncontrado == null && inferirPainelRaizPelasReferencias)
            painelEncontrado = InferirPainelRaizPelasReferencias();

        if (painelEncontrado != null && painelEncontrado.gameObject != gameObject)
            painelRaiz = painelEncontrado.gameObject;
    }

    private Transform InferirPainelRaizPelasReferencias()
    {
        Transform candidato = InferirPainelRaizPorTransform(textoPrincipal != null ? textoPrincipal.transform : null);

        if (candidato != null)
            return candidato;

        candidato = InferirPainelRaizPorTransform(botaoConfirmar != null ? botaoConfirmar.transform : null);

        if (candidato != null)
            return candidato;

        candidato = InferirPainelRaizPorTransform(botaoFechar != null ? botaoFechar.transform : null);

        if (candidato != null)
            return candidato;

        return null;
    }

    private Transform InferirPainelRaizPorTransform(Transform origem)
    {
        if (origem == null)
            return null;

        Transform atual = origem.parent;

        while (atual != null && atual != transform)
        {
            string nome = atual.name.ToLowerInvariant();

            if (nome.Contains("painel") || nome.Contains("panel") || nome.Contains("warning"))
                return atual;

            if (atual.GetComponent<RawImage>() != null && atual.childCount > 0)
                return atual;

            atual = atual.parent;
        }

        return origem.parent != null && origem.parent != transform ? origem.parent : null;
    }

    private Transform EncontrarFilhoPorNome(Transform raiz, string nome)
    {
        if (raiz == null || string.IsNullOrEmpty(nome))
            return null;

        if (raiz.name == nome)
            return raiz;

        Transform[] filhos = raiz.GetComponentsInChildren<Transform>(true);

        for (int i = 0; i < filhos.Length; i++)
        {
            Transform filho = filhos[i];

            if (filho != null && filho.name == nome)
                return filho;
        }

        return null;
    }

    private void GarantirEventSystemNaCena()
    {
        if (FindObjectOfType<EventSystem>() != null)
            return;

        new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
    }

    private void GarantirGraphicRaycaster()
    {
        Canvas canvas = null;

        if (painelRaiz != null)
            canvas = painelRaiz.GetComponentInParent<Canvas>(true);

        if (canvas == null && transform != null)
            canvas = transform.GetComponentInParent<Canvas>(true);

        if (canvas == null)
            canvas = FindObjectOfType<Canvas>();

        if (canvas == null)
            return;

        if (canvas.GetComponent<GraphicRaycaster>() == null)
            canvas.gameObject.AddComponent<GraphicRaycaster>();
    }

    private void ConfigurarEventosDosBotoes()
    {
        if (botaoConfirmar != null)
        {
            botaoConfirmar.Clique -= ConfirmarCompra;
            botaoConfirmar.Clique += ConfirmarCompra;
        }

        if (botaoFechar != null)
        {
            botaoFechar.Clique -= FecharPeloBotao;
            botaoFechar.Clique += FecharPeloBotao;
        }
    }

    private void ConfigurarBotoesParaReceberClique()
    {
        ConfigurarBotaoParaClique(botaoConfirmar);
        ConfigurarBotaoParaClique(botaoFechar);
    }

    private void ConfigurarBotaoParaClique(BuySceneUIImageButton botao)
    {
        if (botao == null)
            return;

        botao.interagivel = true;

        RawImage imagem = botao.GetComponent<RawImage>();

        if (imagem != null)
            imagem.raycastTarget = true;
    }

    private void ConfigurarTextosSemAlterarVisual()
    {
        if (!forcarRaycastTargetDosTextosDesligado)
            return;

        if (textoPrincipal != null)
            textoPrincipal.raycastTarget = false;

        if (botaoConfirmar != null)
        {
            Text[] textosConfirmar = botaoConfirmar.GetComponentsInChildren<Text>(true);

            for (int i = 0; i < textosConfirmar.Length; i++)
            {
                if (textosConfirmar[i] != null)
                    textosConfirmar[i].raycastTarget = false;
            }
        }

        if (botaoFechar != null)
        {
            Text[] textosFechar = botaoFechar.GetComponentsInChildren<Text>(true);

            for (int i = 0; i < textosFechar.Length; i++)
            {
                if (textosFechar[i] != null)
                    textosFechar[i].raycastTarget = false;
            }
        }
    }

    private void ConfirmarCompra()
    {
        if (aoConfirmarCompra != null)
            aoConfirmarCompra.Invoke(terrenoAtual);
        else if (logarEventos)
            Debug.LogWarning("[BuyScenePurchaseConfirmationPanel] Clique em Confirmar ignorado: painel nao foi aberto por um terreno valido.");
    }

    private void AtualizarTextoConfirmacao()
    {
        if (textoPrincipal == null)
            return;

        string textoFinal = textoConfirmacao;

        if (terrenoAtual != null)
        {
            if (exibirNomeDoTerreno)
                textoFinal += "\n\n" + terrenoAtual.nomeDoTerreno;

            if (exibirPrecoDoTerreno)
                textoFinal += "\nPreço: " + terrenoAtual.precoGold.ToString("N0") + " Gold";
        }

        textoPrincipal.text = textoFinal;
    }
}
