using UnityEngine;

public class CicloDiaNoite : MonoBehaviour
{
    [Header("Tempo")]
    [Tooltip("Duração de um ciclo completo de 24h dentro do jogo.")]
    public float duracaoCicloMinutos = 8f;

    [Range(0f, 24f)]
    public float horaInicial = 8f;

    [Range(0f, 24f)]
    public float horaAtual;


    [Header("Controle Visual Sol/Lua")]
    public bool desligarLuzesQuandoInvisiveis = true;
    public GameObject visualSol;
    public GameObject visualLua;

    private Flare flareOriginalSol;
    private Flare flareOriginalLua;


    [Header("Luzes")]
    public Light sol;
    public Light lua;

    [Header("Rotação do Sol")]
    public float rotacaoYDoSol = -45f;

    [Header("Intensidade")]
    public float intensidadeMaximaSol = 1.6f;
    public float intensidadeMaximaLua = 0.18f;

    [Header("Cores do Sol")]
    public Color corSolDia = new Color(1f, 0.95f, 0.82f);
    public Color corSolNascerPor = new Color(1f, 0.55f, 0.25f);

    [Header("Ambiente")]
    public Color ambienteDia = new Color(0.75f, 0.78f, 0.82f);
    public Color ambienteNoite = new Color(0.05f, 0.07f, 0.12f);

    [Header("Neblina / Clima")]
    public bool usarFog = true;
    public Color fogDia = new Color(0.72f, 0.78f, 0.85f);
    public Color fogNoite = new Color(0.04f, 0.05f, 0.09f);
    public float densidadeFogDia = 0.006f;
    public float densidadeFogNoite = 0.018f;

    [Header("Skybox")]
    public Material skyboxMaterial;
    public float exposicaoSkyboxDia = 1.25f;
    public float exposicaoSkyboxNoite = 0.28f;

    void Start()
    {
        horaAtual = horaInicial;

        if (sol != null)
            flareOriginalSol = sol.flare;

        if (lua != null)
            flareOriginalLua = lua.flare;


        if (sol != null)
        {
            RenderSettings.sun = sol;
        }



        if (usarFog)
        {
            RenderSettings.fog = true;
        }

        if (skyboxMaterial != null)
        {
            RenderSettings.skybox = skyboxMaterial;
        }

        AtualizarCiclo();
    }

    void Update()
    {
        if (duracaoCicloMinutos <= 0f)
            duracaoCicloMinutos = 1f;

        float velocidadeTempo = 24f / (duracaoCicloMinutos * 60f);

        horaAtual += Time.deltaTime * velocidadeTempo;

        if (horaAtual >= 24f)
            horaAtual = 0f;

        AtualizarCiclo();
    }

    void AtualizarCiclo()
    {
        float tempoNormalizado = horaAtual / 24f;

        AtualizarSolELua(tempoNormalizado);
        AtualizarAmbiente(tempoNormalizado);
        AtualizarSkybox(tempoNormalizado);
    }

    void AtualizarSolELua(float tempoNormalizado)
    {
        float anguloSol = tempoNormalizado * 360f - 90f;

        float luzDoDia = CalcularForcaDoDia(tempoNormalizado);
        float luzDaNoite = 1f - luzDoDia;

        bool solVisivel = luzDoDia > 0.02f;
        bool luaVisivel = luzDaNoite > 0.02f;

        if (sol != null)
        {
            sol.transform.rotation = Quaternion.Euler(anguloSol, rotacaoYDoSol, 0f);

            sol.intensity = luzDoDia * intensidadeMaximaSol;
            sol.color = Color.Lerp(corSolNascerPor, corSolDia, luzDoDia);

            if (desligarLuzesQuandoInvisiveis)
                sol.enabled = solVisivel;

            sol.flare = solVisivel ? flareOriginalSol : null;
        }

        if (lua != null)
        {
            lua.transform.rotation = Quaternion.Euler(anguloSol + 180f, rotacaoYDoSol, 0f);

            lua.intensity = luzDaNoite * intensidadeMaximaLua;
            lua.color = new Color(0.55f, 0.65f, 1f);

            if (desligarLuzesQuandoInvisiveis)
                lua.enabled = luaVisivel;

            lua.flare = luaVisivel ? flareOriginalLua : null;
        }

        if (visualSol != null)
            visualSol.SetActive(solVisivel);

        if (visualLua != null)
            visualLua.SetActive(luaVisivel);
    }
    void AtualizarAmbiente(float tempoNormalizado)
    {
        float luzDoDia = CalcularForcaDoDia(tempoNormalizado);

        RenderSettings.ambientLight = Color.Lerp(ambienteNoite, ambienteDia, luzDoDia);

        if (usarFog)
        {
            RenderSettings.fogColor = Color.Lerp(fogNoite, fogDia, luzDoDia);
            RenderSettings.fogDensity = Mathf.Lerp(densidadeFogNoite, densidadeFogDia, luzDoDia);
        }
    }

    void AtualizarSkybox(float tempoNormalizado)
    {
        if (RenderSettings.skybox == null)
            return;

        float luzDoDia = CalcularForcaDoDia(tempoNormalizado);
        float exposicao = Mathf.Lerp(exposicaoSkyboxNoite, exposicaoSkyboxDia, luzDoDia);

        if (RenderSettings.skybox.HasProperty("_Exposure"))
        {
            RenderSettings.skybox.SetFloat("_Exposure", exposicao);
        }

        if (RenderSettings.skybox.HasProperty("_Tint"))
        {
            Color corCeu = Color.Lerp(
                new Color(0.08f, 0.1f, 0.18f),
                Color.white,
                luzDoDia
            );

            RenderSettings.skybox.SetColor("_Tint", corCeu);
        }
    }

    float CalcularForcaDoDia(float tempoNormalizado)
    {
        float nascerDoSol = 0.25f; // 06:00
        float porDoSol = 0.75f;    // 18:00

        if (tempoNormalizado < nascerDoSol || tempoNormalizado > porDoSol)
            return 0f;

        float progressoDia = Mathf.InverseLerp(nascerDoSol, porDoSol, tempoNormalizado);

        return Mathf.Sin(progressoDia * Mathf.PI);
    }
}