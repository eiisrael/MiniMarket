using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Botao simples para UI da BuyScene usando RawImage + Texture2D.
///
/// Funciona em dois modos:
/// 1. Textura normal/hover preenchidas no Inspector: troca button_off por button_on, close_off por close_on etc.
/// 2. Sem textura hover preenchida: usa cor normal/hover como fallback para o usuario perceber o hover.
///
/// Importante:
/// - O objeto precisa estar dentro de um Canvas com GraphicRaycaster.
/// - Precisa existir EventSystem na cena.
/// - O RawImage precisa estar com Raycast Target ligado.
/// O BuyScenePurchaseConfirmationPanel ja tenta garantir isso automaticamente.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(RawImage))]
public class BuySceneUIImageButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler, IPointerClickHandler
{
    [Header("Texturas")]
    [Tooltip("Imagem normal do botao. Exemplo: button_off / close_off. Se vazio, captura a textura atual do RawImage ao iniciar.")]
    public Texture2D texturaNormal;

    [Tooltip("Imagem hover do botao. Exemplo: button_on / close_on.")]
    public Texture2D texturaHover;

    [Tooltip("Se Textura Normal estiver vazia, usa automaticamente a textura atual do RawImage como normal.")]
    public bool capturarTexturaNormalDoRawImage = true;

    [Header("Cores")]
    [Tooltip("Cor normal aplicada junto com a textura normal.")]
    public Color corNormal = Color.white;

    [Tooltip("Cor aplicada quando o mouse passa por cima. Usada principalmente quando nao houver Textura Hover.")]
    public Color corHover = new Color(1.12f, 1.12f, 1.12f, 1f);

    [Tooltip("Cor aplicada enquanto o botao esquerdo esta pressionado em cima do botao.")]
    public Color corPressionado = new Color(0.88f, 0.88f, 0.88f, 1f);

    [Tooltip("Se ligado, mesmo usando textura hover, tambem aplica Cor Hover.")]
    public bool aplicarCorMesmoComTexturaHover = false;

    [Header("Estado")]
    public bool interagivel = true;

    [Tooltip("Forca o RawImage Raycast Target ligado para PointerEnter/Click funcionar.")]
    public bool forcarRaycastTargetLigado = true;

    [Header("Evento")]
    public UnityEvent aoClicar;

    private RawImage imagem;
    private bool mouseEmCima;
    private bool pressionado;

    public event Action Clique;

    private void Awake()
    {
        Inicializar();
        AplicarVisualAtual();
    }

    private void OnEnable()
    {
        Inicializar();
        mouseEmCima = false;
        pressionado = false;
        AplicarVisualAtual();
    }

    private void Reset()
    {
        imagem = GetComponent<RawImage>();

        if (imagem != null)
        {
            imagem.raycastTarget = true;
            corNormal = imagem.color.a > 0f ? imagem.color : Color.white;

            if (imagem.texture is Texture2D texturaAtual)
                texturaNormal = texturaAtual;
        }
    }

    private void OnValidate()
    {
        if (!Application.isPlaying)
            return;

        Inicializar();
        AplicarVisualAtual();
    }

    public void Configurar(Texture2D normal, Texture2D hover)
    {
        if (normal != null)
            texturaNormal = normal;

        if (hover != null)
            texturaHover = hover;

        Inicializar();
        AplicarVisualAtual();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!interagivel)
            return;

        mouseEmCima = true;
        AplicarVisualAtual();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        mouseEmCima = false;
        pressionado = false;
        AplicarVisualAtual();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (!interagivel)
            return;

        if (eventData != null && eventData.button != PointerEventData.InputButton.Left)
            return;

        pressionado = true;
        AplicarVisualAtual();
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (!interagivel)
            return;

        pressionado = false;
        AplicarVisualAtual();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (!interagivel)
            return;

        if (eventData != null && eventData.button != PointerEventData.InputButton.Left)
            return;

        if (aoClicar != null)
            aoClicar.Invoke();

        if (Clique != null)
            Clique.Invoke();
    }

    private void Inicializar()
    {
        if (imagem == null)
            imagem = GetComponent<RawImage>();

        if (imagem == null)
            return;

        if (forcarRaycastTargetLigado)
            imagem.raycastTarget = true;

        if (capturarTexturaNormalDoRawImage && texturaNormal == null && imagem.texture is Texture2D texturaAtual)
            texturaNormal = texturaAtual;
    }

    private void AplicarVisualAtual()
    {
        if (imagem == null)
            imagem = GetComponent<RawImage>();

        if (imagem == null)
            return;

        Texture2D texturaEscolhida = texturaNormal;
        Color corEscolhida = corNormal;

        if (!interagivel)
        {
            corEscolhida = new Color(corNormal.r, corNormal.g, corNormal.b, corNormal.a * 0.45f);
        }
        else if (pressionado)
        {
            texturaEscolhida = texturaHover != null ? texturaHover : texturaNormal;
            corEscolhida = corPressionado;
        }
        else if (mouseEmCima)
        {
            texturaEscolhida = texturaHover != null ? texturaHover : texturaNormal;
            corEscolhida = texturaHover != null && !aplicarCorMesmoComTexturaHover ? corNormal : corHover;
        }

        if (texturaEscolhida != null)
            imagem.texture = texturaEscolhida;

        imagem.color = corEscolhida;
    }
}
