using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Autoridade visual da barra Canvas/StaminaHUD/Energy.
/// Mostra a energia total segmentada de 0/5 até 5/5, anima o preenchimento e
/// troca entre energy_green, energy_yellow e energy_red.
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(25500)]
public sealed class MiniMarketEnergyProgressBar : MonoBehaviour
{
    [Header("Referências")]
    public Image barra;
    public Text textoQuantidade;
    public CameraRelativeMovement movimento;

    [Header("Sprites")]
    public Sprite energiaVerde;
    public Sprite energiaAmarela;
    public Sprite energiaVermelha;
    public bool procurarSpritesAutomaticamente = true;

    [Header("Faixas")]
    [Range(0f, 1f)] public float limiteAmarelo = 0.55f;
    [Range(0f, 1f)] public float limiteVermelho = 0.25f;

    [Header("Animação")]
    public bool animar = true;
    [Min(0.1f)] public float velocidade = 12f;
    [Min(0.0001f)] public float tolerancia = 0.001f;

    [Header("Comportamento")]
    public bool corrigirEstadoInconsistente = true;
    public bool manterTextoSegmentado = true;
    [Min(0.1f)] public float intervaloBusca = 0.5f;

    private MiniMarketPlayerDatabase database;
    private MiniMarketEnergySegmentHUD hudLegado;
    private float valorAlvo = 1f;
    private float valorVisual = 1f;
    private float proximaBusca;
    private int ultimoAtual = -1;
    private int ultimoMaximo = -1;
    private float ultimoAlvo = -1f;
    private bool inscritoMovimento;
    private bool inscritoBanco;

    private static readonly string[] NomesVerde =
    {
        "energy_green", "green_energy", "energygreen", "greenenergy"
    };

    private static readonly string[] NomesAmarelo =
    {
        "energy_yellow", "yellow_energy", "energyyellow", "yellowenergy"
    };

    private static readonly string[] NomesVermelho =
    {
        "energy_red", "red_energy", "energyred", "redenergy"
    };

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void InstalarAutomaticamente()
    {
        MiniMarketEnergyProgressBar existente =
            Object.FindAnyObjectByType<MiniMarketEnergyProgressBar>(FindObjectsInactive.Include);

        if (existente != null)
        {
            existente.gameObject.SetActive(true);
            existente.enabled = true;
            existente.RebuscarTudo();
            return;
        }

        Image alvo = EncontrarBarraEnergy();
        if (alvo == null)
        {
            Debug.LogWarning(
                "[EnergyProgressBar] Canvas/StaminaHUD/Energy não foi encontrado. " +
                "Execute Tools > MiniMarket > Criar ou Reparar Barra de Energia."
            );
            return;
        }

        MiniMarketEnergyProgressBar controlador =
            alvo.GetComponent<MiniMarketEnergyProgressBar>();

        if (controlador == null)
            controlador = alvo.gameObject.AddComponent<MiniMarketEnergyProgressBar>();

        controlador.barra = alvo;
        controlador.RebuscarTudo();
    }

    private static Image EncontrarBarraEnergy()
    {
        Image[] imagens = Object.FindObjectsByType<Image>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None
        );

        Image melhor = null;
        int melhorPontuacao = int.MinValue;

        for (int i = 0; i < imagens.Length; i++)
        {
            Image imagem = imagens[i];
            if (imagem == null || imagem.GetComponentInParent<Canvas>() == null)
                continue;

            string nome = NormalizarNome(imagem.name);
            int pontuacao = 0;

            if (nome == "energy")
                pontuacao += 1000;
            else if (nome == "energia" || nome == "energyfill" || nome == "barraenergia")
                pontuacao += 700;
            else
                continue;

            Transform pai = imagem.transform.parent;
            while (pai != null)
            {
                string nomePai = NormalizarNome(pai.name);
                if (nomePai == "staminahud")
                {
                    pontuacao += 1000;
                    break;
                }

                if (nomePai.Contains("stamina") || nomePai.Contains("energy") || nomePai.Contains("energia"))
                    pontuacao += 100;

                if (pai.GetComponent<Canvas>() != null)
                    break;

                pai = pai.parent;
            }

            if (imagem.gameObject.activeInHierarchy)
                pontuacao += 10;

            if (pontuacao > melhorPontuacao)
            {
                melhorPontuacao = pontuacao;
                melhor = imagem;
            }
        }

        return melhor;
    }

    private void Awake()
    {
        ResolverReferencias(true);
        ResolverSprites();
        AssumirAutoridadeVisual();
        AtualizarAlvo(true);
    }

    private void OnEnable()
    {
        ResolverReferencias(true);
        ResolverSprites();
        AssumirAutoridadeVisual();
        InscreverEventos();
        AtualizarAlvo(true);
    }

    private void OnDisable()
    {
        DesinscreverEventos();
    }

    private void Update()
    {
        if (Time.unscaledTime >= proximaBusca &&
            (movimento == null || barra == null || database == null))
        {
            ResolverReferencias(false);
            ResolverSprites();
            AssumirAutoridadeVisual();
            InscreverEventos();
        }

        AtualizarAlvo(false);
        AnimarBarra();
    }

    [ContextMenu("Energia/Rebuscar tudo")]
    public void RebuscarTudo()
    {
        ResolverReferencias(true);
        ResolverSprites();
        AssumirAutoridadeVisual();
        DesinscreverEventos();
        InscreverEventos();
        ultimoAtual = -1;
        ultimoMaximo = -1;
        ultimoAlvo = -1f;
        AtualizarAlvo(true);
    }

    private void ResolverReferencias(bool forcar)
    {
        if (barra == null)
            barra = GetComponent<Image>();

        if (barra == null)
            barra = EncontrarBarraEnergy();

        if (forcar || movimento == null)
        {
            movimento = Object.FindAnyObjectByType<CameraRelativeMovement>(
                FindObjectsInactive.Include
            );
        }

        if (forcar || database == null)
        {
            database = MiniMarketPlayerDatabase.Instance;
            if (database == null && Application.isPlaying)
                database = MiniMarketPlayerDatabase.ObterOuCriar();
        }

        if (textoQuantidade == null && barra != null)
            textoQuantidade = EncontrarTextoQuantidade(barra.transform.parent);

        if (hudLegado == null && barra != null)
        {
            hudLegado = barra.GetComponentInParent<MiniMarketEnergySegmentHUD>();
            if (hudLegado == null && barra.transform.parent != null)
            {
                hudLegado = barra.transform.parent.GetComponentInChildren<MiniMarketEnergySegmentHUD>(
                    true
                );
            }
        }

        proximaBusca = Time.unscaledTime + Mathf.Max(0.1f, intervaloBusca);
    }

    private static Text EncontrarTextoQuantidade(Transform raiz)
    {
        if (raiz == null)
            return null;

        Text[] textos = raiz.GetComponentsInChildren<Text>(true);
        Text fallback = null;

        for (int i = 0; i < textos.Length; i++)
        {
            Text texto = textos[i];
            if (texto == null)
                continue;

            string nome = NormalizarNome(texto.name);
            if (nome == "txtqtd" || nome == "quantidade" || nome == "energytext" ||
                nome == "staminatext")
            {
                return texto;
            }

            if (fallback == null && PareceContadorSegmentado(texto.text))
                fallback = texto;
        }

        return fallback;
    }

    private void AssumirAutoridadeVisual()
    {
        if (barra == null)
            return;

        barra.raycastTarget = false;
        barra.type = Image.Type.Filled;
        barra.fillMethod = Image.FillMethod.Horizontal;
        barra.fillOrigin = (int)Image.OriginHorizontal.Left;
        barra.fillClockwise = true;
        barra.preserveAspect = false;

        if (hudLegado == null)
            return;

        hudLegado.autoDetectarBarras = false;
        hudLegado.criarBarrasSegmentadasQuandoAusentes = false;

        if (hudLegado.barraEnergia == barra)
            hudLegado.barraEnergia = null;

        if (textoQuantidade != null)
            hudLegado.textoEnergia = textoQuantidade;
    }

    private void ResolverSprites()
    {
        if (!procurarSpritesAutomaticamente)
            return;

        Sprite[] carregados = Resources.FindObjectsOfTypeAll<Sprite>();
        for (int i = 0; i < carregados.Length; i++)
            TentarAtribuirSprite(carregados[i]);

#if UNITY_EDITOR
        if (energiaVerde == null)
            energiaVerde = ProcurarSpriteNoProjeto(NomesVerde);
        if (energiaAmarela == null)
            energiaAmarela = ProcurarSpriteNoProjeto(NomesAmarelo);
        if (energiaVermelha == null)
            energiaVermelha = ProcurarSpriteNoProjeto(NomesVermelho);
#endif
    }

    private void TentarAtribuirSprite(Sprite sprite)
    {
        if (sprite == null)
            return;

        string nome = NormalizarNome(sprite.name);
        if (energiaVerde == null && NomeCorresponde(nome, NomesVerde))
            energiaVerde = sprite;
        if (energiaAmarela == null && NomeCorresponde(nome, NomesAmarelo))
            energiaAmarela = sprite;
        if (energiaVermelha == null && NomeCorresponde(nome, NomesVermelho))
            energiaVermelha = sprite;
    }

#if UNITY_EDITOR
    private static Sprite ProcurarSpriteNoProjeto(string[] aliases)
    {
        for (int a = 0; a < aliases.Length; a++)
        {
            string[] guids = AssetDatabase.FindAssets(aliases[a]);
            for (int i = 0; i < guids.Length; i++)
            {
                string caminho = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (string.IsNullOrWhiteSpace(caminho))
                    continue;

                string nomeArquivo = System.IO.Path.GetFileNameWithoutExtension(caminho);
                if (!NomeCorresponde(NormalizarNome(nomeArquivo), aliases))
                    continue;

                Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(caminho);
                if (sprite != null)
                    return sprite;
            }
        }

        return null;
    }
#endif

    private void AtualizarAlvo(bool forcar)
    {
        int atual;
        int maximo;
        float stamina01;
        bool correndo;

        if (movimento != null)
        {
            atual = movimento.StaminaSegmentosAtuais;
            maximo = movimento.StaminaSegmentosMaximos;
            stamina01 = movimento.StaminaPercentual01;
            correndo = movimento.EstaCorrendo;
        }
        else if (database != null)
        {
            atual = database.EnergiaSegmentosAtuais;
            maximo = database.EnergiaSegmentosMaximos;
            stamina01 = database.ObterPercentualStamina01();
            correndo = false;
        }
        else
        {
            atual = 5;
            maximo = 5;
            stamina01 = 1f;
            correndo = false;
        }

        maximo = Mathf.Max(1, maximo);
        atual = Mathf.Clamp(atual, 0, maximo);
        stamina01 = Mathf.Clamp01(stamina01);

        if (corrigirEstadoInconsistente && atual > 0 && stamina01 <= 0.001f && !correndo)
            stamina01 = 1f;

        float unidades = atual <= 0
            ? 0f
            : Mathf.Max(0, atual - 1) + stamina01;

        float novoAlvo = Mathf.Clamp01(unidades / maximo);

        bool mudou = forcar ||
                     atual != ultimoAtual ||
                     maximo != ultimoMaximo ||
                     Mathf.Abs(novoAlvo - ultimoAlvo) > 0.0005f;

        if (!mudou)
            return;

        ultimoAtual = atual;
        ultimoMaximo = maximo;
        ultimoAlvo = novoAlvo;
        valorAlvo = novoAlvo;

        if (forcar)
            valorVisual = valorAlvo;

        if (manterTextoSegmentado && textoQuantidade != null)
            textoQuantidade.text = atual + "/" + maximo;

        AplicarSpriteECor(valorAlvo);

        if (forcar)
            AplicarFill(valorVisual);
    }

    private void AnimarBarra()
    {
        if (barra == null)
            return;

        if (!animar)
        {
            valorVisual = valorAlvo;
        }
        else
        {
            float t = 1f - Mathf.Exp(
                -Mathf.Max(0.1f, velocidade) * Time.unscaledDeltaTime
            );

            valorVisual = Mathf.Lerp(valorVisual, valorAlvo, t);
            if (Mathf.Abs(valorVisual - valorAlvo) <= tolerancia)
                valorVisual = valorAlvo;
        }

        AplicarFill(valorVisual);
    }

    private void AplicarFill(float valor)
    {
        if (barra == null)
            return;

        AssumirAutoridadeVisual();
        barra.fillAmount = Mathf.Clamp01(valor);
    }

    private void AplicarSpriteECor(float valor)
    {
        if (barra == null)
            return;

        Sprite desejado;
        Color fallback;

        if (valor <= limiteVermelho)
        {
            desejado = energiaVermelha;
            fallback = new Color(1f, 0.18f, 0.08f, 1f);
        }
        else if (valor <= limiteAmarelo)
        {
            desejado = energiaAmarela;
            fallback = new Color(1f, 0.78f, 0.08f, 1f);
        }
        else
        {
            desejado = energiaVerde;
            fallback = new Color(0.15f, 0.9f, 0.25f, 1f);
        }

        if (desejado != null)
        {
            if (barra.sprite != desejado)
                barra.sprite = desejado;
            barra.color = Color.white;
        }
        else
        {
            barra.color = fallback;
        }
    }

    private void InscreverEventos()
    {
        if (movimento != null && !inscritoMovimento)
        {
            movimento.OnStaminaChanged += AoAlterarStamina;
            inscritoMovimento = true;
        }

        if (database != null && !inscritoBanco)
        {
            database.OnDatabaseChanged += AoAlterarBanco;
            inscritoBanco = true;
        }
    }

    private void DesinscreverEventos()
    {
        if (movimento != null && inscritoMovimento)
            movimento.OnStaminaChanged -= AoAlterarStamina;

        if (database != null && inscritoBanco)
            database.OnDatabaseChanged -= AoAlterarBanco;

        inscritoMovimento = false;
        inscritoBanco = false;
    }

    private void AoAlterarStamina()
    {
        AtualizarAlvo(false);
    }

    private void AoAlterarBanco(MiniMarketPlayerDatabase.MiniMarketPlayerData dados)
    {
        AtualizarAlvo(false);
    }

    private static bool PareceContadorSegmentado(string valor)
    {
        if (string.IsNullOrWhiteSpace(valor))
            return false;

        int barraIndex = valor.IndexOf('/');
        return barraIndex > 0 &&
               barraIndex < valor.Length - 1 &&
               char.IsDigit(valor[barraIndex - 1]) &&
               char.IsDigit(valor[barraIndex + 1]);
    }

    private static bool NomeCorresponde(string nomeNormalizado, string[] aliases)
    {
        for (int i = 0; i < aliases.Length; i++)
        {
            string alias = NormalizarNome(aliases[i]);
            if (nomeNormalizado == alias || nomeNormalizado.Contains(alias))
                return true;
        }

        return false;
    }

    private static string NormalizarNome(string valor)
    {
        if (string.IsNullOrWhiteSpace(valor))
            return string.Empty;

        return valor.Trim()
            .ToLowerInvariant()
            .Replace("_", string.Empty)
            .Replace("-", string.Empty)
            .Replace(" ", string.Empty)
            .Replace("á", "a")
            .Replace("ã", "a")
            .Replace("â", "a")
            .Replace("é", "e")
            .Replace("ê", "e")
            .Replace("í", "i")
            .Replace("ó", "o")
            .Replace("ô", "o")
            .Replace("õ", "o")
            .Replace("ú", "u")
            .Replace("ç", "c");
    }

    private void OnValidate()
    {
        limiteVermelho = Mathf.Clamp01(limiteVermelho);
        limiteAmarelo = Mathf.Clamp(limiteAmarelo, limiteVermelho, 1f);
        velocidade = Mathf.Max(0.1f, velocidade);
        tolerancia = Mathf.Max(0.0001f, tolerancia);
        intervaloBusca = Mathf.Max(0.1f, intervaloBusca);
    }
}
