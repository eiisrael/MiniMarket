using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Botao simples para UI da BuyScene usando RawImage + Texture2D.
/// Foi feito assim para funcionar mesmo quando os PNGs estao importados como Default Texture,
/// sem exigir Texture Type = Sprite.
/// </summary>
[RequireComponent(typeof(RawImage))]
public class BuySceneUIImageButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    [Header("Texturas")]
    public Texture2D texturaNormal;
    public Texture2D texturaHover;

    [Header("Estado")]
    public bool interagivel = true;

    [Header("Evento")]
    public UnityEvent aoClicar;

    private RawImage imagem;
    private bool mouseEmCima;

    public event Action Clique;

    private void Awake()
    {
        imagem = GetComponent<RawImage>();
        AplicarTexturaAtual();
    }

    private void OnEnable()
    {
        mouseEmCima = false;
        AplicarTexturaAtual();
    }

    public void Configurar(Texture2D normal, Texture2D hover)
    {
        texturaNormal = normal;
        texturaHover = hover;
        AplicarTexturaAtual();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!interagivel)
            return;

        mouseEmCima = true;
        AplicarTexturaAtual();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        mouseEmCima = false;
        AplicarTexturaAtual();
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

    private void AplicarTexturaAtual()
    {
        if (imagem == null)
            imagem = GetComponent<RawImage>();

        if (imagem == null)
            return;

        Texture2D texturaEscolhida = mouseEmCima && texturaHover != null ? texturaHover : texturaNormal;

        if (texturaEscolhida != null)
            imagem.texture = texturaEscolhida;
    }
}
