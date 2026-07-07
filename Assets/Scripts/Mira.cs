using UnityEngine;
using UnityEngine.UI;

public class CrosshairAim : MonoBehaviour
{
    [Header("Input")]
    [Tooltip("Botão que ativa a mão/mira. 0 = esquerdo, 1 = direito, 2 = meio.")]
    [Range(0, 2)]
    public int botaoDeMira = 1;

    [Tooltip("Botão que fecha a mão enquanto a mira estiver ativa. Normalmente 0 = esquerdo.")]
    [Range(0, 2)]
    public int botaoDePegar = 0;

    [Header("Mira / Mão UI")]
    public GameObject mira;

    [Tooltip("Componente Image da mira. Se deixar vazio, o script tenta pegar automaticamente.")]
    public Image imagemDaMira;

    [Tooltip("Sprite da mão aberta. Exemplo: hand_open.png")]
    public Sprite spriteMaoAberta;

    [Tooltip("Sprite da mão fechada. Exemplo: hand_close.png")]
    public Sprite spriteMaoFechada;

    [Tooltip("Tempo mínimo que a mão ficará fechada mesmo em um clique rápido.")]
    [Min(0f)]
    public float tempoMinimoMaoFechada = 0.12f;

    [Tooltip("Usa fade suave ao mostrar/esconder a mira.")]
    public bool usarFadeNaMira = true;

    [Tooltip("Velocidade do fade da mira.")]
    [Min(0.1f)]
    public float velocidadeFadeMira = 18f;

    [Tooltip("Desativa o GameObject da mira quando ela estiver invisível.")]
    public bool desativarMiraQuandoFechada = true;

    [Header("Posição da Mira")]
    [Tooltip("Offset da mira em relação ao centro da tela.")]
    public Vector2 offsetMira = Vector2.zero;

    [Tooltip("Força a mira a usar o centro da tela como referência.")]
    public bool forcarAncoraNoCentro = true;

    [Tooltip("Permite alterar a posição da mira em tempo real pelo Inspector durante o Play.")]
    public bool atualizarPosicaoEmTempoReal = true;

    [Header("Câmera")]
    public Camera cameraDoPlayer;

    [Tooltip("Script da câmera estilo GTA. Pode deixar vazio que o script tenta encontrar na câmera.")]
    public CameraGTAFollowHardcore cameraGTAFollow;

    [Header("Primeira Pessoa")]
    [Tooltip("Quando abrir a mira/mão, a câmera entra em primeira pessoa.")]
    public bool primeiraPessoaAoMirar = true;

    [Header("Delay")]
    [Tooltip("Tempo segurando o botão de mira antes da mão aparecer.")]
    [Min(0f)]
    public float delayParaAparecer = 0.08f;

    [Header("Zoom")]
    public bool usarZoom = true;

    [Tooltip("2 = zoom 2x.")]
    [Min(1f)]
    public float zoomMultiplicador = 2f;

    [Tooltip("Velocidade da transição do zoom.")]
    [Min(0.1f)]
    public float velocidadeZoom = 12f;

    [Tooltip("Menor FOV permitido durante o zoom.")]
    [Range(10f, 80f)]
    public float fovMinimoPermitido = 20f;

    [Header("Tempo")]
    [Tooltip("Se ativado, o zoom/fade funciona mesmo com Time.timeScale = 0.")]
    public bool usarTempoNaoEscalado = false;

    private RectTransform miraRect;
    private CanvasGroup miraCanvasGroup;

    private float tempoSegurando;
    private float fovNormal;
    private float fovZoom;
    private float tempoAteMaoFechada;

    private bool miraAberta;
    private bool maoFechadaAtual;
    private bool inicializado;

    public bool EstaMirando => miraAberta;
    public bool EstaPegando => maoFechadaAtual;

    private void Awake()
    {
        Inicializar();
    }

    private void Start()
    {
        TrocarSpriteDaMao(false);
        FecharMiraInstantaneo();
        AplicarPrimeiraPessoa(false);
    }

    private void Update()
    {
        if (!inicializado)
            Inicializar();

        if (mira == null || cameraDoPlayer == null)
            return;

        float deltaTime = usarTempoNaoEscalado ? Time.unscaledDeltaTime : Time.deltaTime;
        float tempoAtual = usarTempoNaoEscalado ? Time.unscaledTime : Time.time;

        if (atualizarPosicaoEmTempoReal)
            AtualizarPosicaoMira();

        AtualizarInputMira(deltaTime, tempoAtual);
        AtualizarEstadoDaMao(tempoAtual);
        AtualizarZoom(deltaTime);
        AtualizarFadeMira(deltaTime);
    }

    private void OnDisable()
    {
        AplicarPrimeiraPessoa(false);
        RestaurarCamera();
        FecharMiraInstantaneo();

        tempoSegurando = 0f;
        tempoAteMaoFechada = 0f;
        miraAberta = false;
        maoFechadaAtual = false;
    }

    private void Inicializar()
    {
        if (cameraDoPlayer == null)
            cameraDoPlayer = Camera.main;

        if (cameraGTAFollow == null && cameraDoPlayer != null)
            cameraGTAFollow = cameraDoPlayer.GetComponent<CameraGTAFollowHardcore>();

        if (mira != null)
        {
            miraRect = mira.GetComponent<RectTransform>();

            if (imagemDaMira == null)
                imagemDaMira = mira.GetComponent<Image>();

            if (usarFadeNaMira)
            {
                miraCanvasGroup = mira.GetComponent<CanvasGroup>();

                if (miraCanvasGroup == null)
                    miraCanvasGroup = mira.AddComponent<CanvasGroup>();

                miraCanvasGroup.interactable = false;
                miraCanvasGroup.blocksRaycasts = false;
            }

            PrepararMiraUI();
            AtualizarPosicaoMira();
        }

        if (cameraDoPlayer != null)
        {
            fovNormal = cameraDoPlayer.fieldOfView;
            RecalcularFovZoom();
        }

        inicializado = true;
    }

    private void AtualizarInputMira(float deltaTime, float tempoAtual)
    {
        bool segurandoBotaoMira = Input.GetMouseButton(botaoDeMira);

        if (segurandoBotaoMira)
        {
            tempoSegurando += deltaTime;

            if (tempoSegurando >= delayParaAparecer)
                AbrirMira();

            if (miraAberta && Input.GetMouseButtonDown(botaoDePegar))
                tempoAteMaoFechada = tempoAtual + tempoMinimoMaoFechada;
        }
        else
        {
            tempoSegurando = 0f;
            tempoAteMaoFechada = 0f;
            FecharMira();
        }
    }

    private void AtualizarEstadoDaMao(float tempoAtual)
    {
        if (!miraAberta)
        {
            TrocarSpriteDaMao(false);
            return;
        }

        bool segurandoBotaoDePegar = Input.GetMouseButton(botaoDePegar);
        bool aindaNoTempoMinimo = tempoAtual < tempoAteMaoFechada;

        bool deveFecharMao = segurandoBotaoDePegar || aindaNoTempoMinimo;

        TrocarSpriteDaMao(deveFecharMao);
    }

    private void TrocarSpriteDaMao(bool fechada)
    {
        if (imagemDaMira == null)
            return;

        if (maoFechadaAtual == fechada && imagemDaMira.sprite != null)
            return;

        maoFechadaAtual = fechada;

        if (fechada && spriteMaoFechada != null)
        {
            imagemDaMira.sprite = spriteMaoFechada;
        }
        else if (!fechada && spriteMaoAberta != null)
        {
            imagemDaMira.sprite = spriteMaoAberta;
        }
    }

    private void AbrirMira()
    {
        if (miraAberta)
            return;

        miraAberta = true;

        if (mira != null && !mira.activeSelf)
            mira.SetActive(true);

        TrocarSpriteDaMao(false);
        AplicarPrimeiraPessoa(true);
    }

    private void FecharMira()
    {
        if (!miraAberta)
            return;

        miraAberta = false;
        TrocarSpriteDaMao(false);
        AplicarPrimeiraPessoa(false);

        if (!usarFadeNaMira)
        {
            if (mira != null && desativarMiraQuandoFechada)
                mira.SetActive(false);
        }
    }

    private void AplicarPrimeiraPessoa(bool ativa)
    {
        if (!primeiraPessoaAoMirar)
            return;

        if (cameraGTAFollow == null)
            return;

        cameraGTAFollow.SetPrimeiraPessoa(ativa);
    }

    private void PrepararMiraUI()
    {
        if (miraRect == null)
            return;

        if (forcarAncoraNoCentro)
        {
            miraRect.anchorMin = new Vector2(0.5f, 0.5f);
            miraRect.anchorMax = new Vector2(0.5f, 0.5f);
            miraRect.pivot = new Vector2(0.5f, 0.5f);
        }
    }

    private void AtualizarPosicaoMira()
    {
        if (miraRect == null)
            return;

        miraRect.anchoredPosition = offsetMira;
    }

    private void AtualizarZoom(float deltaTime)
    {
        if (!usarZoom || cameraDoPlayer == null)
            return;

        float fovAlvo = miraAberta ? fovZoom : fovNormal;
        float suavizacao = CalcularSuavizacao(velocidadeZoom, deltaTime);

        cameraDoPlayer.fieldOfView = Mathf.Lerp(
            cameraDoPlayer.fieldOfView,
            fovAlvo,
            suavizacao
        );
    }

    private void AtualizarFadeMira(float deltaTime)
    {
        if (!usarFadeNaMira || miraCanvasGroup == null)
            return;

        float alphaAlvo = miraAberta ? 1f : 0f;
        float suavizacao = CalcularSuavizacao(velocidadeFadeMira, deltaTime);

        miraCanvasGroup.alpha = Mathf.Lerp(
            miraCanvasGroup.alpha,
            alphaAlvo,
            suavizacao
        );

        if (!miraAberta && desativarMiraQuandoFechada && miraCanvasGroup.alpha <= 0.01f)
        {
            miraCanvasGroup.alpha = 0f;

            if (mira != null)
                mira.SetActive(false);
        }
    }

    private void FecharMiraInstantaneo()
    {
        if (miraCanvasGroup != null)
            miraCanvasGroup.alpha = 0f;

        if (mira != null && desativarMiraQuandoFechada)
            mira.SetActive(false);
    }

    private void RestaurarCamera()
    {
        if (cameraDoPlayer == null)
            return;

        if (fovNormal > 1f)
            cameraDoPlayer.fieldOfView = fovNormal;
    }

    private void RecalcularFovZoom()
    {
        if (cameraDoPlayer == null)
            return;

        fovZoom = fovNormal / zoomMultiplicador;
        fovZoom = Mathf.Clamp(fovZoom, fovMinimoPermitido, fovNormal);
    }

    private float CalcularSuavizacao(float velocidade, float deltaTime)
    {
        return 1f - Mathf.Exp(-velocidade * deltaTime);
    }
}