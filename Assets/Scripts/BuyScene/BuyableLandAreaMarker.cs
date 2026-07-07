using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Marca visualmente uma area/terreno a venda no chao.
/// Tambem oferece suporte a hover/selecao para compra na BuyScene.
/// </summary>
public class BuyableLandAreaMarker : MonoBehaviour
{
    public enum EstadoDoTerreno
    {
        Disponivel,
        Destacado,
        Indisponivel
    }

    [Header("Identificacao")]
    [Tooltip("Nome amigavel para organizar no Inspector e mostrar no painel de compra.")]
    public string nomeDoTerreno = "Terreno a Venda";

    [Tooltip("Preco do terreno em gold.")]
    [Min(0)]
    public int precoGold = 20000;

    [Header("Estado")]
    public EstadoDoTerreno estadoAtual = EstadoDoTerreno.Disponivel;

    [Tooltip("Se desligar, o terreno some da demarcacao visual.")]
    public bool exibirDemarcacao = true;

    [Header("Tamanho da Area")]
    [Tooltip("Tamanho X/Z da demarcacao no chao.")]
    public Vector2 tamanhoArea = new Vector2(4f, 4f);

    [Tooltip("Offset local do centro da demarcacao.")]
    public Vector3 centroLocal = Vector3.zero;

    [Tooltip("Altura extra para a linha nao brigar com o chao.")]
    [Min(0f)]
    public float alturaAcimaDoChao = 0.04f;

    [Header("Visual")]
    [Tooltip("Material opcional das linhas. Se deixar vazio, o script cria um material simples automaticamente.")]
    public Material materialLinha;

    [Tooltip("Largura da borda no mundo 3D.")]
    [Min(0.005f)]
    public float larguraLinha = 0.08f;

    public Color corDisponivel = new Color(1f, 0.92f, 0f, 1f);
    public Color corDestacado = new Color(0.25f, 1f, 0.1f, 1f);

    [Tooltip("Cor usada quando o mouse esta em cima do terreno durante a BuyScene.")]
    public Color corHover = new Color(0.1f, 0.95f, 1f, 1f);

    [Tooltip("Cor usada quando o terreno foi clicado e esta aguardando confirmacao.")]
    public Color corSelecionado = new Color(1f, 0.65f, 0.1f, 1f);

    public Color corIndisponivel = new Color(1f, 0.15f, 0.1f, 1f);

    [Tooltip("Mostra um X no centro do terreno, como na referencia enviada.")]
    public bool exibirXCentral = true;

    [Tooltip("Atualiza as linhas em tempo real. Deixe ligado se voce vai mover/escalar os terrenos no editor durante Play.")]
    public bool atualizarEmTempoReal = true;

    [Header("Debug")]
    public bool desenharGizmosNoEditor = true;

    private LineRenderer linhaBorda;
    private LineRenderer linhaDiagonalA;
    private LineRenderer linhaDiagonalB;
    private Material materialGerado;

    private bool hoverAtivo;
    private bool selecionadoAtivo;

    public Bounds BoundsMundo
    {
        get
        {
            Vector3 centro = transform.TransformPoint(centroLocal);
            Vector3 tamanho = new Vector3(Mathf.Abs(tamanhoArea.x), 0.1f, Mathf.Abs(tamanhoArea.y));
            return new Bounds(centro, tamanho);
        }
    }

    public bool EstaDisponivelParaCompra => estadoAtual != EstadoDoTerreno.Indisponivel;

    private void Awake()
    {
        CriarOuAtualizarLinhas();
    }

    private void OnEnable()
    {
        CriarOuAtualizarLinhas();
        AplicarVisibilidade();
    }

    private void Update()
    {
        if (atualizarEmTempoReal)
            AtualizarPosicoesDasLinhas();
    }

    private void OnValidate()
    {
        tamanhoArea.x = Mathf.Max(0.1f, tamanhoArea.x);
        tamanhoArea.y = Mathf.Max(0.1f, tamanhoArea.y);
        larguraLinha = Mathf.Max(0.005f, larguraLinha);
        alturaAcimaDoChao = Mathf.Max(0f, alturaAcimaDoChao);
        precoGold = Mathf.Max(0, precoGold);

        if (Application.isPlaying)
            CriarOuAtualizarLinhas();
    }

    public void DefinirDestaque(bool destacar)
    {
        if (estadoAtual == EstadoDoTerreno.Indisponivel)
        {
            CriarOuAtualizarLinhas();
            return;
        }

        estadoAtual = destacar ? EstadoDoTerreno.Destacado : EstadoDoTerreno.Disponivel;
        CriarOuAtualizarLinhas();
    }

    public void DefinirHover(bool ativo)
    {
        if (estadoAtual == EstadoDoTerreno.Indisponivel)
        {
            hoverAtivo = false;
            CriarOuAtualizarLinhas();
            return;
        }

        hoverAtivo = ativo;
        CriarOuAtualizarLinhas();
    }

    public void DefinirSelecionado(bool ativo)
    {
        if (estadoAtual == EstadoDoTerreno.Indisponivel)
        {
            selecionadoAtivo = false;
            CriarOuAtualizarLinhas();
            return;
        }

        selecionadoAtivo = ativo;
        CriarOuAtualizarLinhas();
    }

    public void DefinirDisponivel(bool disponivel)
    {
        estadoAtual = disponivel ? EstadoDoTerreno.Disponivel : EstadoDoTerreno.Indisponivel;
        hoverAtivo = false;
        selecionadoAtivo = false;
        CriarOuAtualizarLinhas();
    }

    public void MarcarComoComprado()
    {
        DefinirDisponivel(false);
    }

    public Vector3 ObterPontoDeFoco()
    {
        return transform.TransformPoint(centroLocal);
    }

    public float ObterAlturaDoPlanoDeSelecao()
    {
        return ObterPontoDeFoco().y + alturaAcimaDoChao;
    }

    public bool ContemPontoMundo(Vector3 pontoMundo, float margemExtra = 0f)
    {
        Vector3 local = transform.InverseTransformPoint(pontoMundo) - centroLocal;
        float metadeX = Mathf.Abs(tamanhoArea.x) * 0.5f + Mathf.Max(0f, margemExtra);
        float metadeZ = Mathf.Abs(tamanhoArea.y) * 0.5f + Mathf.Max(0f, margemExtra);

        return local.x >= -metadeX &&
               local.x <= metadeX &&
               local.z >= -metadeZ &&
               local.z <= metadeZ;
    }

    private void CriarOuAtualizarLinhas()
    {
        linhaBorda = ObterOuCriarLinha("BuyScene_Borda_Terreno", linhaBorda, 5);
        linhaDiagonalA = ObterOuCriarLinha("BuyScene_X_Diagonal_A", linhaDiagonalA, 2);
        linhaDiagonalB = ObterOuCriarLinha("BuyScene_X_Diagonal_B", linhaDiagonalB, 2);

        ConfigurarLinha(linhaBorda);
        ConfigurarLinha(linhaDiagonalA);
        ConfigurarLinha(linhaDiagonalB);

        AtualizarPosicoesDasLinhas();
        AplicarVisibilidade();
    }

    private LineRenderer ObterOuCriarLinha(string nome, LineRenderer linhaAtual, int quantidadePontos)
    {
        if (linhaAtual != null)
        {
            linhaAtual.positionCount = quantidadePontos;
            return linhaAtual;
        }

        Transform existente = transform.Find(nome);
        if (existente != null)
        {
            LineRenderer linhaExistente = existente.GetComponent<LineRenderer>();
            if (linhaExistente != null)
            {
                linhaExistente.positionCount = quantidadePontos;
                return linhaExistente;
            }
        }

        GameObject objetoLinha = new GameObject(nome);
        objetoLinha.transform.SetParent(transform, false);

        LineRenderer novaLinha = objetoLinha.AddComponent<LineRenderer>();
        novaLinha.positionCount = quantidadePontos;
        return novaLinha;
    }

    private void ConfigurarLinha(LineRenderer linha)
    {
        if (linha == null)
            return;

        linha.useWorldSpace = true;
        linha.loop = false;
        linha.startWidth = larguraLinha;
        linha.endWidth = larguraLinha;
        linha.numCornerVertices = 4;
        linha.numCapVertices = 4;
        linha.shadowCastingMode = ShadowCastingMode.Off;
        linha.receiveShadows = false;
        linha.material = ObterMaterialLinha();

        Color cor = ObterCorAtual();
        linha.startColor = cor;
        linha.endColor = cor;

        if (linha.material != null && linha.material.HasProperty("_Color"))
            linha.material.color = cor;
    }

    private Material ObterMaterialLinha()
    {
        if (materialLinha != null)
            return materialLinha;

        if (materialGerado == null)
        {
            Shader shader = Shader.Find("Sprites/Default");

            if (shader == null)
                shader = Shader.Find("Universal Render Pipeline/Unlit");

            if (shader == null)
                shader = Shader.Find("Unlit/Color");

            materialGerado = new Material(shader);
            materialGerado.name = "Material_Gerado_BuyScene_Linha";
            materialGerado.hideFlags = HideFlags.HideAndDontSave;
        }

        materialGerado.color = ObterCorAtual();
        return materialGerado;
    }

    private Color ObterCorAtual()
    {
        if (estadoAtual == EstadoDoTerreno.Indisponivel)
            return corIndisponivel;

        if (selecionadoAtivo)
            return corSelecionado;

        if (hoverAtivo)
            return corHover;

        if (estadoAtual == EstadoDoTerreno.Destacado)
            return corDestacado;

        return corDisponivel;
    }

    private void AtualizarPosicoesDasLinhas()
    {
        if (linhaBorda == null || linhaDiagonalA == null || linhaDiagonalB == null)
            return;

        Vector3 centro = transform.TransformPoint(centroLocal) + Vector3.up * alturaAcimaDoChao;
        Vector3 direita = transform.right * (tamanhoArea.x * 0.5f);
        Vector3 frente = transform.forward * (tamanhoArea.y * 0.5f);

        Vector3 p0 = centro - direita - frente;
        Vector3 p1 = centro - direita + frente;
        Vector3 p2 = centro + direita + frente;
        Vector3 p3 = centro + direita - frente;

        linhaBorda.SetPosition(0, p0);
        linhaBorda.SetPosition(1, p1);
        linhaBorda.SetPosition(2, p2);
        linhaBorda.SetPosition(3, p3);
        linhaBorda.SetPosition(4, p0);

        linhaDiagonalA.SetPosition(0, p0);
        linhaDiagonalA.SetPosition(1, p2);

        linhaDiagonalB.SetPosition(0, p1);
        linhaDiagonalB.SetPosition(1, p3);
    }

    private void AplicarVisibilidade()
    {
        bool mostrarLinhas = exibirDemarcacao;
        bool mostrarX = exibirDemarcacao && exibirXCentral;

        if (linhaBorda != null)
            linhaBorda.gameObject.SetActive(mostrarLinhas);

        if (linhaDiagonalA != null)
            linhaDiagonalA.gameObject.SetActive(mostrarX);

        if (linhaDiagonalB != null)
            linhaDiagonalB.gameObject.SetActive(mostrarX);
    }

    private void OnDrawGizmosSelected()
    {
        if (!desenharGizmosNoEditor)
            return;

        Vector3 centro = transform.TransformPoint(centroLocal) + Vector3.up * alturaAcimaDoChao;
        Vector3 direita = transform.right * (tamanhoArea.x * 0.5f);
        Vector3 frente = transform.forward * (tamanhoArea.y * 0.5f);

        Gizmos.color = ObterCorAtual();
        Gizmos.DrawLine(centro - direita - frente, centro - direita + frente);
        Gizmos.DrawLine(centro - direita + frente, centro + direita + frente);
        Gizmos.DrawLine(centro + direita + frente, centro + direita - frente);
        Gizmos.DrawLine(centro + direita - frente, centro - direita - frente);
    }

    private void OnDestroy()
    {
        if (Application.isPlaying && materialGerado != null)
            Destroy(materialGerado);
    }
}
