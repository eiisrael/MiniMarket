using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Troca o sprite de um botao entre normal/hover/pressionado.
/// Use para Gemas Gratis e Close sem depender do Transition padrao do Button.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Button))]
public class MiniMarketUIButtonSpriteSwap : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
{
    [Header("Imagem")]
    [Tooltip("Imagem do botao. Se vazio, tenta pegar Image no mesmo GameObject.")]
    public Image imagemDoBotao;

    [Header("Sprites")]
    public Sprite spriteOff;
    public Sprite spriteOn;
    public Sprite spritePressionado;

    [Header("Opcoes")]
    public bool usarSpriteOnComoPressionadoSeVazio = true;
    public bool aplicarOffNoStart = true;

    private bool mouseEmCima;
    private bool pressionado;
    private Button button;

    private void Awake()
    {
        button = GetComponent<Button>();

        if (imagemDoBotao == null)
            imagemDoBotao = GetComponent<Image>();
    }

    private void Start()
    {
        if (aplicarOffNoStart)
            AplicarSpriteAtual();
    }

    private void OnEnable()
    {
        pressionado = false;
        mouseEmCima = false;
        AplicarSpriteAtual();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        mouseEmCima = true;
        AplicarSpriteAtual();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        mouseEmCima = false;
        pressionado = false;
        AplicarSpriteAtual();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (button != null && !button.interactable)
            return;

        pressionado = true;
        AplicarSpriteAtual();
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        pressionado = false;
        AplicarSpriteAtual();
    }

    public void AplicarSpriteAtual()
    {
        if (imagemDoBotao == null)
            return;

        Sprite spriteEscolhido = spriteOff;

        if (button != null && !button.interactable)
        {
            spriteEscolhido = spriteOff;
        }
        else if (pressionado)
        {
            spriteEscolhido = spritePressionado != null
                ? spritePressionado
                : usarSpriteOnComoPressionadoSeVazio ? spriteOn : spriteOff;
        }
        else if (mouseEmCima)
        {
            spriteEscolhido = spriteOn != null ? spriteOn : spriteOff;
        }

        if (spriteEscolhido != null)
            imagemDoBotao.sprite = spriteEscolhido;
    }
}
