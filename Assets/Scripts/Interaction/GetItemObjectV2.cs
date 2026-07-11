using UnityEngine;

/// <summary>
/// Marcador V2 para objetos que podem ser selecionados/pegos pelo GetItemV2.
/// Coloque este componente nos produtos/objetos pegáveis.
/// </summary>
[DisallowMultipleComponent]
public class GetItemObjectV2 : MonoBehaviour
{
    [Header("Estado")]
    public bool podeSerPego = true;
    public bool selecionado;
    public bool sendoPego;

    [Header("Limites")]
    [Min(0.01f)] public float massaVirtual = 1f;
    [Min(0.05f)] public float tamanhoMaximo = 4f;
    [Min(0.1f)] public float multiplicadorDistancia = 1f;

    [Header("Visual Opcional")]
    public Renderer[] renderersParaHighlight;
    public bool autoEncontrarRenderers = true;
    public Color corSelecionado = new Color(1f, 0.9f, 0.35f, 1f);
    public bool alterarEmission = false;

    private MaterialPropertyBlock propertyBlock;
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

    private void Awake()
    {
        PrepararRenderers();
    }

    private void OnEnable()
    {
        PrepararRenderers();
        AplicarHighlight();
    }

    public void Selecionar(bool ativo)
    {
        if (selecionado == ativo)
            return;

        selecionado = ativo;
        AplicarHighlight();
    }

    public void ComecarPegar()
    {
        sendoPego = true;
        Selecionar(false);
    }

    public void Soltar()
    {
        sendoPego = false;
    }

    private void PrepararRenderers()
    {
        if (!autoEncontrarRenderers)
            return;

        if (renderersParaHighlight == null || renderersParaHighlight.Length == 0)
            renderersParaHighlight = GetComponentsInChildren<Renderer>(true);

        if (propertyBlock == null)
            propertyBlock = new MaterialPropertyBlock();
    }

    private void AplicarHighlight()
    {
        if (renderersParaHighlight == null)
            return;

        if (propertyBlock == null)
            propertyBlock = new MaterialPropertyBlock();

        for (int i = 0; i < renderersParaHighlight.Length; i++)
        {
            Renderer r = renderersParaHighlight[i];
            if (r == null)
                continue;

            r.GetPropertyBlock(propertyBlock);

            if (selecionado)
            {
                propertyBlock.SetColor(BaseColorId, corSelecionado);
                if (alterarEmission)
                    propertyBlock.SetColor(EmissionColorId, corSelecionado);
            }
            else
            {
                propertyBlock.Clear();
            }

            r.SetPropertyBlock(propertyBlock);
        }
    }
}
