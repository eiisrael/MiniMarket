using UnityEngine;

public class CrosshairAim : MonoBehaviour
{
    [Header("Mira UI")]
    public GameObject mira;

    [Header("Posição da Mira")]
    [Tooltip("Offset da mira em relação ao centro da tela. X = direita/esquerda, Y = cima/baixo.")]
    public Vector2 offsetMira = Vector2.zero;

    [Tooltip("Força a mira a usar o centro da tela como referência.")]
    public bool forcarAncoraNoCentro = true;

    [Tooltip("Permite alterar a posição da mira em tempo real pelo Inspector durante o Play.")]
    public bool atualizarPosicaoEmTempoReal = true;

    [Header("Camera")]
    public Camera cameraDoPlayer;

    [Header("Delay")]
    [Tooltip("Tempo segurando o botão direito antes da mira aparecer.")]
    public float delayParaAparecer = 0.08f;

    [Header("Zoom")]
    [Tooltip("2 = zoom 2x")]
    public float zoomMultiplicador = 2f;

    [Tooltip("Velocidade da transição do zoom.")]
    public float velocidadeZoom = 12f;

    private float tempoSegurando = 0f;
    private float fovNormal;
    private float fovZoom;
    private bool miraAberta = false;

    private RectTransform miraRect;

    void Start()
    {
        if (mira != null)
        {
            miraRect = mira.GetComponent<RectTransform>();

            PrepararMiraUI();
            AtualizarPosicaoMira();

            mira.SetActive(false);
        }

        if (cameraDoPlayer == null)
            cameraDoPlayer = Camera.main;

        if (cameraDoPlayer != null)
        {
            fovNormal = cameraDoPlayer.fieldOfView;
            fovZoom = fovNormal / zoomMultiplicador;
        }
    }

    void Update()
    {
        if (mira == null || cameraDoPlayer == null)
            return;

        if (atualizarPosicaoEmTempoReal)
            AtualizarPosicaoMira();

        bool segurandoBotaoDireito = Input.GetMouseButton(1);

        if (segurandoBotaoDireito)
        {
            tempoSegurando += Time.deltaTime;

            if (tempoSegurando >= delayParaAparecer)
            {
                miraAberta = true;
                mira.SetActive(true);
            }
        }
        else
        {
            tempoSegurando = 0f;
            miraAberta = false;
            mira.SetActive(false);
        }

        AtualizarZoom();
    }

    void PrepararMiraUI()
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

    void AtualizarPosicaoMira()
    {
        if (miraRect == null)
            return;

        miraRect.anchoredPosition = offsetMira;
    }

    void AtualizarZoom()
    {
        float fovAlvo = miraAberta ? fovZoom : fovNormal;

        cameraDoPlayer.fieldOfView = Mathf.Lerp(
            cameraDoPlayer.fieldOfView,
            fovAlvo,
            velocidadeZoom * Time.deltaTime
        );
    }
}