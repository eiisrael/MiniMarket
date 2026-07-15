using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Marca visualmente uma area/terreno a venda no chao.
/// Sincroniza status com MiniMarketPlayerDatabase.
///
/// Correção:
/// - OnValidate não cria mais GameObject/LineRenderer/SetParent.
/// - Isso remove spam "SendMessage cannot be called during OnValidate" e evita travadas no Editor.
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
    public string idPersistente;
    public string nomeDoTerreno = "Terreno a Venda";
    [Min(0)] public int precoGold = 20000;

    [Header("Banco de Dados")]
    public bool sincronizarComBancoDeDados = true;
    public bool aplicarEstadoSalvoAoIniciar = true;

    [Header("Estado")]
    public EstadoDoTerreno estadoAtual = EstadoDoTerreno.Disponivel;
    public bool exibirDemarcacao = true;

    [Header("Tamanho da Area")]
    public Vector2 tamanhoArea = new Vector2(4f, 4f);
    public Vector3 centroLocal = Vector3.zero;
    [Min(0f)] public float alturaAcimaDoChao = 0.04f;

    [Header("Visual")]
    public Material materialLinha;
    [Min(0.005f)] public float larguraLinha = 0.08f;
    public Color corDisponivel = new Color(1f, 0.92f, 0f, 1f);
    public Color corDestacado = new Color(0.25f, 1f, 0.1f, 1f);
    public Color corHover = new Color(0.1f, 0.95f, 1f, 1f);
    public Color corSelecionado = new Color(1f, 0.65f, 0.1f, 1f);
    public Color corIndisponivel = new Color(1f, 0.15f, 0.1f, 1f);
    public bool exibirXCentral = true;
    public bool atualizarEmTempoReal = true;

    [Header("Debug")]
    public bool desenharGizmosNoEditor = true;
    public bool logarBancoDeDados = false;

    private LineRenderer linhaBorda;
    private LineRenderer linhaDiagonalA;
    private LineRenderer linhaDiagonalB;
    private Material materialGerado;
    private bool hoverAtivo;
    private bool selecionadoAtivo;
    private MiniMarketPlayerDatabase banco;

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
    public string IdPersistente => ObterIdPersistente();

    private void Awake()
    {
        ResolverBanco();
        CriarOuAtualizarLinhas();
    }

    private void OnEnable()
    {
        ResolverBanco();

        if (banco != null)
            banco.OnDatabaseChanged += AoBancoAlterado;

        CriarOuAtualizarLinhas();

        if (aplicarEstadoSalvoAoIniciar)
            SincronizarEstadoComBanco();

        AplicarVisibilidade();
    }

    private void Start()
    {
        if (aplicarEstadoSalvoAoIniciar)
            SincronizarEstadoComBanco();
    }

    private void OnDisable()
    {
        if (banco != null)
            banco.OnDatabaseChanged -= AoBancoAlterado;
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

        // Importante: não criar GameObject/SetParent/AddComponent em OnValidate.
        // Apenas atualiza linhas já existentes para evitar warnings e travadas no Editor.
        ConfigurarLinhaSeExistir(linhaBorda);
        ConfigurarLinhaSeExistir(linhaDiagonalA);
        ConfigurarLinhaSeExistir(linhaDiagonalB);
        AtualizarPosicoesDasLinhas();
        AplicarVisibilidade();
    }

    public void SincronizarEstadoComBanco()
    {
        if (!sincronizarComBancoDeDados)
            return;

        ResolverBanco();
        if (banco == null)
            return;

        string id = ObterIdPersistente();
        if (string.IsNullOrEmpty(id))
            return;

        if (banco.PropriedadeComprada(id) || banco.PropriedadeIndisponivel(id))
        {
            DefinirDisponivelInterno(false, false);

            if (logarBancoDeDados)
                Debug.Log("[BuyableLandAreaMarker] Estado carregado do banco: " + id + " = Indisponivel/Comprado");
        }
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
        DefinirDisponivelInterno(disponivel, true);
    }

    private void DefinirDisponivelInterno(bool disponivel, bool salvarNoBanco)
    {
        estadoAtual = disponivel ? EstadoDoTerreno.Disponivel : EstadoDoTerreno.Indisponivel;
        hoverAtivo = false;
        selecionadoAtivo = false;
        CriarOuAtualizarLinhas();

        if (salvarNoBanco && sincronizarComBancoDeDados)
        {
            ResolverBanco();
            if (banco != null)
            {
                banco.DefinirStatusPropriedade(ObterIdPersistente(), nomeDoTerreno, !disponivel, disponivel, disponivel ? "Disponivel" : "Indisponivel");
            }
        }
    }

    public void MarcarComoComprado()
    {
        estadoAtual = EstadoDoTerreno.Indisponivel;
        hoverAtivo = false;
        selecionadoAtivo = false;
        CriarOuAtualizarLinhas();

        if (sincronizarComBancoDeDados)
        {
            ResolverBanco();
            if (banco != null)
            {
                banco.RegistrarPropriedadeComprada(ObterIdPersistente(), nomeDoTerreno);

                if (logarBancoDeDados)
                    Debug.Log("[BuyableLandAreaMarker] Compra salva no banco: " + ObterIdPersistente());
            }
        }
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

        return local.x >= -metadeX && local.x <= metadeX && local.z >= -metadeZ && local.z <= metadeZ;
    }

    private void ResolverBanco()
    {
        if (!sincronizarComBancoDeDados)
            return;

        if (banco == null)
            banco = MiniMarketPlayerDatabase.ObterOuCriar();
    }

    private void AoBancoAlterado(MiniMarketPlayerDatabase.MiniMarketPlayerData dados)
    {
        if (Application.isPlaying)
            SincronizarEstadoComBanco();
    }

    private string ObterIdPersistente()
    {
        if (!string.IsNullOrWhiteSpace(idPersistente))
            return NormalizarId(idPersistente);

        if (!string.IsNullOrWhiteSpace(nomeDoTerreno) && nomeDoTerreno != "Terreno a Venda")
            return NormalizarId(nomeDoTerreno);

        return NormalizarId(gameObject.name);
    }

    private string NormalizarId(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return string.Empty;

        return id.Trim().ToUpperInvariant().Replace(' ', '_');
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

    private void ConfigurarLinhaSeExistir(LineRenderer linha)
    {
        if (linha != null)
            ConfigurarLinha(linha);
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
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Unlit/Color");

            materialGerado = new Material(shader);
            materialGerado.name = "Material_Gerado_BuyScene_Linha";
            materialGerado.hideFlags = HideFlags.HideAndDontSave;
        }

        materialGerado.color = ObterCorAtual();
        return materialGerado;
    }

    private Color ObterCorAtual()
    {
        if (estadoAtual == EstadoDoTerreno.Indisponivel) return corIndisponivel;
        if (selecionadoAtivo) return corSelecionado;
        if (hoverAtivo) return corHover;
        if (estadoAtual == EstadoDoTerreno.Destacado) return corDestacado;
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

        if (linhaBorda != null) linhaBorda.gameObject.SetActive(mostrarLinhas);
        if (linhaDiagonalA != null) linhaDiagonalA.gameObject.SetActive(mostrarX);
        if (linhaDiagonalB != null) linhaDiagonalB.gameObject.SetActive(mostrarX);
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
