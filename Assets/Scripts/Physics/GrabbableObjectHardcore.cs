using UnityEngine;

/// <summary>
/// Componente simples para objetos que podem ser pegos pelo PlayerObjectGrabberHardcore.
/// Mantem estado visual/funcional sem obrigar cada produto a ter script manual.
/// </summary>
[DisallowMultipleComponent]
public class GrabbableObjectHardcore : MonoBehaviour
{
    [Header("Permissao")]
    public bool podeSerPego = true;

    [Header("Visual opcional")]
    public bool destacarQuandoSelecionado = true;
    public Color corSelecionado = new Color(1f, 0.9f, 0.25f, 1f);

    [Header("Estado")]
    [SerializeField] private bool selecionado;
    [SerializeField] private bool sendoPego;

    private Rigidbody rb;
    private Renderer[] renderers;
    private MaterialPropertyBlock propertyBlock;
    private bool temCorOriginal;
    private Color corOriginal;

    public bool Selecionado => selecionado;
    public bool SendoPego => sendoPego;
    public Rigidbody RigidbodyDoObjeto => rb != null ? rb : (rb = GetComponent<Rigidbody>());

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        renderers = GetComponentsInChildren<Renderer>(true);
        propertyBlock = new MaterialPropertyBlock();
        CapturarCorOriginal();
    }

    private void OnDisable()
    {
        selecionado = false;
        sendoPego = false;
        AplicarDestaque(false);
    }

    public void Selecionar(bool ativo)
    {
        if (!podeSerPego)
            ativo = false;

        selecionado = ativo;
        AplicarDestaque(ativo && destacarQuandoSelecionado && !sendoPego);
    }

    public void ComecarPegar()
    {
        sendoPego = true;
        selecionado = false;
        AplicarDestaque(false);
    }

    public void Soltar()
    {
        sendoPego = false;
        selecionado = false;
        AplicarDestaque(false);
    }

    private void CapturarCorOriginal()
    {
        if (renderers == null || renderers.Length == 0)
            return;

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer r = renderers[i];
            if (r == null || r.sharedMaterial == null)
                continue;

            if (r.sharedMaterial.HasProperty("_BaseColor"))
            {
                corOriginal = r.sharedMaterial.GetColor("_BaseColor");
                temCorOriginal = true;
                return;
            }

            if (r.sharedMaterial.HasProperty("_Color"))
            {
                corOriginal = r.sharedMaterial.GetColor("_Color");
                temCorOriginal = true;
                return;
            }
        }
    }

    private void AplicarDestaque(bool ativo)
    {
        if (renderers == null)
            renderers = GetComponentsInChildren<Renderer>(true);

        if (propertyBlock == null)
            propertyBlock = new MaterialPropertyBlock();

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer r = renderers[i];
            if (r == null)
                continue;

            r.GetPropertyBlock(propertyBlock);

            Color cor = ativo ? corSelecionado : (temCorOriginal ? corOriginal : Color.white);

            if (r.sharedMaterial != null && r.sharedMaterial.HasProperty("_BaseColor"))
                propertyBlock.SetColor("_BaseColor", cor);
            else
                propertyBlock.SetColor("_Color", cor);

            r.SetPropertyBlock(propertyBlock);
        }
    }
}
