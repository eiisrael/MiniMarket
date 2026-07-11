using UnityEngine;

[DisallowMultipleComponent]
public class GrabbableObjectHardcore : MonoBehaviour
{
    [Header("Objeto Pegável")]
    public bool podeSerPego = true;

    [Tooltip("Peso visual/lógico do objeto. Por enquanto usado apenas como referência.")]
    [Min(0.1f)]
    public float peso = 1f;

    [Header("Destaque / Seleção")]
    public bool usarDestaqueVisual = true;

    [Tooltip("Cor quando o objeto está selecionado pela mira.")]
    public Color corSelecionado = new Color(0.25f, 1f, 0.05f, 1f);

    [Tooltip("Cor quando o objeto está sendo segurado.")]
    public Color corPegando = new Color(1f, 0.65f, 0.05f, 1f);

    [Header("Física")]
    [Tooltip("Se não tiver Rigidbody, cria automaticamente quando for pego.")]
    public bool criarRigidbodyAutomaticamente = true;

    [Tooltip("Usa gravidade quando soltar o objeto.")]
    public bool usarGravidadeAoSoltar = true;

    private Renderer[] renderers;
    private Material[][] materiais;
    private Color[][] coresOriginais;

    private Rigidbody rb;
    private bool estavaKinematic;
    private bool usavaGravidade;

    private bool estaSelecionado;
    private bool estaPegando;

    public Rigidbody RigidbodyDoObjeto => rb;

    private void Awake()
    {
        CacheRenderers();
        rb = GetComponent<Rigidbody>();
    }

    private void CacheRenderers()
    {
        renderers = GetComponentsInChildren<Renderer>(true);

        materiais = new Material[renderers.Length][];
        coresOriginais = new Color[renderers.Length][];

        for (int i = 0; i < renderers.Length; i++)
        {
            materiais[i] = renderers[i].materials;
            coresOriginais[i] = new Color[materiais[i].Length];

            for (int j = 0; j < materiais[i].Length; j++)
            {
                coresOriginais[i][j] = LerCorDoMaterial(materiais[i][j]);
            }
        }
    }

    public void Selecionar(bool ativo)
    {
        if (estaPegando)
            return;

        if (estaSelecionado == ativo)
            return;

        estaSelecionado = ativo;

        if (!usarDestaqueVisual)
            return;

        if (ativo)
            AplicarCor(corSelecionado);
        else
            RestaurarCores();
    }

    public void ComecarPegar()
    {
        if (!podeSerPego)
            return;

        estaPegando = true;
        estaSelecionado = false;

        if (usarDestaqueVisual)
            AplicarCor(corPegando);

        PrepararRigidbody();
    }

    public void Soltar()
    {
        estaPegando = false;
        estaSelecionado = false;

        RestaurarRigidbody();
        RestaurarCores();
    }

    private void PrepararRigidbody()
    {
        if (rb == null && criarRigidbodyAutomaticamente)
            rb = gameObject.AddComponent<Rigidbody>();

        if (rb == null)
            return;

        estavaKinematic = rb.isKinematic;
        usavaGravidade = rb.useGravity;

        rb.useGravity = false;
        rb.isKinematic = true;
    }

    private void RestaurarRigidbody()
    {
        if (rb == null)
            return;

        rb.isKinematic = estavaKinematic;
        rb.useGravity = usarGravidadeAoSoltar ? true : usavaGravidade;
    }

    private void AplicarCor(Color cor)
    {
        if (materiais == null)
            CacheRenderers();

        for (int i = 0; i < materiais.Length; i++)
        {
            for (int j = 0; j < materiais[i].Length; j++)
            {
                DefinirCorDoMaterial(materiais[i][j], cor);
            }
        }
    }

    private void RestaurarCores()
    {
        if (materiais == null || coresOriginais == null)
            return;

        for (int i = 0; i < materiais.Length; i++)
        {
            for (int j = 0; j < materiais[i].Length; j++)
            {
                DefinirCorDoMaterial(materiais[i][j], coresOriginais[i][j]);
            }
        }
    }

    private Color LerCorDoMaterial(Material material)
    {
        if (material == null)
            return Color.white;

        if (material.HasProperty("_BaseColor"))
            return material.GetColor("_BaseColor");

        if (material.HasProperty("_Color"))
            return material.GetColor("_Color");

        return Color.white;
    }

    private void DefinirCorDoMaterial(Material material, Color cor)
    {
        if (material == null)
            return;

        if (material.HasProperty("_BaseColor"))
        {
            material.SetColor("_BaseColor", cor);
            return;
        }

        if (material.HasProperty("_Color"))
        {
            material.SetColor("_Color", cor);
        }
    }
}