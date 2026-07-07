using UnityEngine;

/// <summary>
/// Sincroniza a cor/status da marcacao da calcada da BuyScene com o status dos terrenos ligados a ela.
///
/// Use no mesmo objeto do BuySceneEntryTrigger, normalmente BUY_Area.
/// Quando o terreno associado for comprado e ficar Indisponivel, o X/borda da calcada tambem fica com a cor de Indisponivel.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(BuySceneEntryTrigger))]
public class BuySceneEntryAreaStatusSync : MonoBehaviour
{
    public enum ModoDeLeituraDosTerrenos
    {
        UsarTerrenosDoTrigger,
        UsarListaManual,
        UsarPrimeiroTerrenoDisponivelNoTrigger
    }

    [Header("Referencias")]
    [Tooltip("Trigger da area de entrada. Se vazio, o script pega automaticamente no mesmo objeto.")]
    public BuySceneEntryTrigger triggerDaArea;

    [Tooltip("Opcional. Use se quiser controlar manualmente quais terrenos definem o status da calcada.")]
    public BuyableLandAreaMarker[] terrenosManuais;

    [Header("Leitura do Status")]
    public ModoDeLeituraDosTerrenos modoDeLeitura = ModoDeLeituraDosTerrenos.UsarTerrenosDoTrigger;

    [Tooltip("Se ligado, a calcada so fica indisponivel quando TODOS os terrenos ligados a ela estiverem indisponiveis. Recomendado para areas com varios lotes.")]
    public bool ficarIndisponivelSomenteQuandoTodosOsTerrenosEstiveremIndisponiveis = true;

    [Tooltip("Se ligado, quando todos os terrenos forem comprados, a marcacao continua visivel em vermelho. Se desligar, voce pode esconder manualmente pelo trigger.")]
    public bool manterMarcacaoVisivelQuandoIndisponivel = true;

    [Header("Aplicacao")]
    [Tooltip("Atualiza a cor todo frame. Recomendado ligado para refletir compra, hover e mudancas de status sem depender de eventos.")]
    public bool atualizarEmTempoReal = true;

    [Tooltip("Forca a cor tambem no material das linhas. Ajuda em alguns shaders/URP.")]
    public bool aplicarCorNoMaterial = true;

    [Tooltip("Procura LineRenderers filhos automaticamente. Recomendado ligado.")]
    public bool procurarLinhasAutomaticamente = true;

    [Header("Linhas da Calcada")]
    public LineRenderer linhaBorda;
    public LineRenderer linhaDiagonalA;
    public LineRenderer linhaDiagonalB;

    [Header("Fallback")]
    [Tooltip("Cor usada se nenhum terreno for encontrado.")]
    public Color corFallback = new Color(1f, 0.92f, 0f, 1f);

    [Header("Debug")]
    public bool logarEventos;

    private BuyableLandAreaMarker.EstadoDoTerreno ultimoEstadoAplicado;
    private Color ultimaCorAplicada;
    private bool possuiEstadoAplicado;

    private const string NomeLinhaBorda = "BuyScene_Entrada_Borda";
    private const string NomeLinhaDiagonalA = "BuyScene_Entrada_Diagonal_A";
    private const string NomeLinhaDiagonalB = "BuyScene_Entrada_Diagonal_B";

    private void Awake()
    {
        ResolverReferencias();
        AplicarStatusAtual();
    }

    private void Start()
    {
        ResolverReferencias();
        AplicarStatusAtual();
    }

    private void LateUpdate()
    {
        if (!atualizarEmTempoReal)
            return;

        ResolverReferenciasLeves();
        AplicarStatusAtual();
    }

    private void OnValidate()
    {
        if (!Application.isPlaying)
            return;

        ResolverReferencias();
        AplicarStatusAtual();
    }

    public void AplicarStatusAtual()
    {
        BuyableLandAreaMarker terrenoReferencia = ObterTerrenoReferencia();

        if (terrenoReferencia == null)
        {
            AplicarCorNasLinhas(corFallback);
            return;
        }

        BuyableLandAreaMarker.EstadoDoTerreno estado = ObterEstadoAgregado(terrenoReferencia);
        Color cor = ObterCorPeloEstado(terrenoReferencia, estado);

        if (!manterMarcacaoVisivelQuandoIndisponivel && estado == BuyableLandAreaMarker.EstadoDoTerreno.Indisponivel)
            DefinirLinhasAtivas(false);
        else
            DefinirLinhasAtivas(true);

        AplicarCorNasLinhas(cor);

        if (logarEventos && (!possuiEstadoAplicado || estado != ultimoEstadoAplicado || cor != ultimaCorAplicada))
            Debug.Log("[BuySceneEntryAreaStatusSync] Calcada sincronizada com status: " + estado + " / cor: " + cor);

        ultimoEstadoAplicado = estado;
        ultimaCorAplicada = cor;
        possuiEstadoAplicado = true;
    }

    private void ResolverReferencias()
    {
        if (triggerDaArea == null)
            triggerDaArea = GetComponent<BuySceneEntryTrigger>();

        if (procurarLinhasAutomaticamente)
            ResolverLinhasAutomaticamente();
    }

    private void ResolverReferenciasLeves()
    {
        if (triggerDaArea == null)
            triggerDaArea = GetComponent<BuySceneEntryTrigger>();

        if (procurarLinhasAutomaticamente && (linhaBorda == null || linhaDiagonalA == null || linhaDiagonalB == null))
            ResolverLinhasAutomaticamente();
    }

    private void ResolverLinhasAutomaticamente()
    {
        if (linhaBorda == null)
            linhaBorda = EncontrarLinhaPorNome(NomeLinhaBorda);

        if (linhaDiagonalA == null)
            linhaDiagonalA = EncontrarLinhaPorNome(NomeLinhaDiagonalA);

        if (linhaDiagonalB == null)
            linhaDiagonalB = EncontrarLinhaPorNome(NomeLinhaDiagonalB);
    }

    private LineRenderer EncontrarLinhaPorNome(string nome)
    {
        Transform filho = transform.Find(nome);

        if (filho != null)
        {
            LineRenderer linha = filho.GetComponent<LineRenderer>();

            if (linha != null)
                return linha;
        }

        LineRenderer[] linhas = GetComponentsInChildren<LineRenderer>(true);

        for (int i = 0; i < linhas.Length; i++)
        {
            if (linhas[i] != null && linhas[i].name == nome)
                return linhas[i];
        }

        return null;
    }

    private BuyableLandAreaMarker[] ObterTerrenos()
    {
        if (modoDeLeitura == ModoDeLeituraDosTerrenos.UsarListaManual)
            return terrenosManuais;

        if (triggerDaArea != null && triggerDaArea.terrenosDestaArea != null && triggerDaArea.terrenosDestaArea.Length > 0)
            return triggerDaArea.terrenosDestaArea;

        if (terrenosManuais != null && terrenosManuais.Length > 0)
            return terrenosManuais;

        return null;
    }

    private BuyableLandAreaMarker ObterTerrenoReferencia()
    {
        BuyableLandAreaMarker[] terrenos = ObterTerrenos();

        if (terrenos == null || terrenos.Length == 0)
            return null;

        if (modoDeLeitura == ModoDeLeituraDosTerrenos.UsarPrimeiroTerrenoDisponivelNoTrigger)
        {
            for (int i = 0; i < terrenos.Length; i++)
            {
                BuyableLandAreaMarker terreno = terrenos[i];

                if (terreno != null && terreno.estadoAtual != BuyableLandAreaMarker.EstadoDoTerreno.Indisponivel)
                    return terreno;
            }
        }

        for (int i = 0; i < terrenos.Length; i++)
        {
            if (terrenos[i] != null)
                return terrenos[i];
        }

        return null;
    }

    private BuyableLandAreaMarker.EstadoDoTerreno ObterEstadoAgregado(BuyableLandAreaMarker terrenoReferencia)
    {
        BuyableLandAreaMarker[] terrenos = ObterTerrenos();

        if (terrenos == null || terrenos.Length == 0)
            return terrenoReferencia.estadoAtual;

        bool encontrouTerrenoValido = false;
        bool todosIndisponiveis = true;
        bool existeDestacado = false;
        bool existeDisponivel = false;

        for (int i = 0; i < terrenos.Length; i++)
        {
            BuyableLandAreaMarker terreno = terrenos[i];

            if (terreno == null)
                continue;

            encontrouTerrenoValido = true;

            if (terreno.estadoAtual != BuyableLandAreaMarker.EstadoDoTerreno.Indisponivel)
                todosIndisponiveis = false;

            if (terreno.estadoAtual == BuyableLandAreaMarker.EstadoDoTerreno.Destacado)
                existeDestacado = true;

            if (terreno.estadoAtual == BuyableLandAreaMarker.EstadoDoTerreno.Disponivel)
                existeDisponivel = true;
        }

        if (!encontrouTerrenoValido)
            return terrenoReferencia.estadoAtual;

        if (ficarIndisponivelSomenteQuandoTodosOsTerrenosEstiveremIndisponiveis)
        {
            if (todosIndisponiveis)
                return BuyableLandAreaMarker.EstadoDoTerreno.Indisponivel;

            if (existeDestacado)
                return BuyableLandAreaMarker.EstadoDoTerreno.Destacado;

            if (existeDisponivel)
                return BuyableLandAreaMarker.EstadoDoTerreno.Disponivel;

            return terrenoReferencia.estadoAtual;
        }

        return terrenoReferencia.estadoAtual;
    }

    private Color ObterCorPeloEstado(BuyableLandAreaMarker terrenoReferencia, BuyableLandAreaMarker.EstadoDoTerreno estado)
    {
        if (terrenoReferencia == null)
            return corFallback;

        switch (estado)
        {
            case BuyableLandAreaMarker.EstadoDoTerreno.Indisponivel:
                return terrenoReferencia.corIndisponivel;

            case BuyableLandAreaMarker.EstadoDoTerreno.Destacado:
                return terrenoReferencia.corDestacado;

            case BuyableLandAreaMarker.EstadoDoTerreno.Disponivel:
            default:
                return terrenoReferencia.corDisponivel;
        }
    }

    private void AplicarCorNasLinhas(Color cor)
    {
        AplicarCorNaLinha(linhaBorda, cor);
        AplicarCorNaLinha(linhaDiagonalA, cor);
        AplicarCorNaLinha(linhaDiagonalB, cor);
    }

    private void AplicarCorNaLinha(LineRenderer linha, Color cor)
    {
        if (linha == null)
            return;

        linha.startColor = cor;
        linha.endColor = cor;

        if (aplicarCorNoMaterial && linha.material != null && linha.material.HasProperty("_Color"))
            linha.material.color = cor;
    }

    private void DefinirLinhasAtivas(bool ativo)
    {
        if (linhaBorda != null)
            linhaBorda.gameObject.SetActive(ativo);

        if (linhaDiagonalA != null)
            linhaDiagonalA.gameObject.SetActive(ativo);

        if (linhaDiagonalB != null)
            linhaDiagonalB.gameObject.SetActive(ativo);
    }
}
