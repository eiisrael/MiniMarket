#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public static class EnergyVisualEffectsSetup
{
    private const string GreenPath = "Assets/UI/Models/Textures/HUD/green_energy.png";
    private const string YellowPath = "Assets/UI/Models/Textures/HUD/yellow_energy.png";
    private const string RedPath = "Assets/UI/Models/Textures/HUD/red_energy.png";

    [MenuItem("Tools/MiniMarket/Aplicar Efeitos Visuais da Energia", priority = 5)]
    public static void Aplicar()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("[EnergyVisualEffectsSetup] Saia do Play Mode antes de executar.");
            return;
        }

        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded || string.IsNullOrWhiteSpace(scene.path))
        {
            Debug.LogWarning("[EnergyVisualEffectsSetup] Abra e salve a SampleScene primeiro.");
            return;
        }

        MiniMarketEnergyProgressBar bar =
            Object.FindAnyObjectByType<MiniMarketEnergyProgressBar>(FindObjectsInactive.Include);

        if (bar == null)
        {
            EnergyProgressBarSetup.CriarOuReparar();
            bar = Object.FindAnyObjectByType<MiniMarketEnergyProgressBar>(FindObjectsInactive.Include);
        }

        if (bar == null)
        {
            Debug.LogError("[EnergyVisualEffectsSetup] MiniMarketEnergyProgressBar não encontrado.");
            return;
        }

        Undo.RecordObject(bar, "Configurar porcentagem e sprites da energia");

        Sprite green = AssetDatabase.LoadAssetAtPath<Sprite>(GreenPath);
        Sprite yellow = AssetDatabase.LoadAssetAtPath<Sprite>(YellowPath);
        Sprite red = AssetDatabase.LoadAssetAtPath<Sprite>(RedPath);

        bar.mostrarPorcentagem = true;
        bar.manterTextoSegmentado = false;
        bar.formatoPorcentagem = "{0}%";
        bar.limiteVerde = 0.61f;
        bar.limiteVermelho = 0.25f;
        bar.energiaVerdeSprite = green;
        bar.energiaAmarelaSprite = yellow;
        bar.energiaVermelhaSprite = red;

        Image icon = EncontrarRaio(bar);
        Text text = bar.textoQuantidade != null
            ? bar.textoQuantidade
            : EncontrarTexto(bar.transform.parent);

        bar.iconeEnergia = icon;
        bar.textoQuantidade = text;

        MiniMarketEnergyVisualEffects effects =
            bar.GetComponent<MiniMarketEnergyVisualEffects>();
        if (effects == null)
            effects = Undo.AddComponent<MiniMarketEnergyVisualEffects>(bar.gameObject);

        Undo.RecordObject(effects, "Configurar efeitos visuais da energia");
        effects.progressBar = bar;
        effects.movimento = bar.movimento;
        effects.textoPorcentagem = text;
        effects.iconeRaio = icon;
        effects.preenchimento = bar.preenchimentoVerde;
        effects.raioVerde = green;
        effects.raioAmarelo = yellow;
        effects.raioVermelho = red;
        effects.inicioVerde = 0.61f;
        effects.inicioVermelho = 0.25f;
        effects.larguraTransicao = 0.07f;
        effects.pulsarAoCorrer = true;
        effects.detectarShiftComoFallback = true;
        effects.intensidadePulsacao = 0.14f;
        effects.batimentosPorMinuto = 118f;
        effects.segundoBatimento = 0.55f;

        if (text != null)
        {
            Undo.RecordObject(text, "Exibir porcentagem da energia");
            text.text = "100%";
            EditorUtility.SetDirty(text);
        }

        if (icon != null && green != null)
        {
            Undo.RecordObject(icon, "Aplicar raio verde inicial");
            icon.sprite = green;
            icon.color = Color.white;
            icon.preserveAspect = true;
            EditorUtility.SetDirty(icon);
        }

        if (bar.preenchimentoVerde != null)
        {
            Undo.RecordObject(bar.preenchimentoVerde, "Aplicar cor inicial da energia");
            bar.preenchimentoVerde.color = effects.corVerde;
            EditorUtility.SetDirty(bar.preenchimentoVerde);
        }

        EditorUtility.SetDirty(bar);
        EditorUtility.SetDirty(effects);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();

        effects.RebuscarReferencias();

        Debug.Log(
            "[EnergyVisualEffectsSetup] Aplicado: porcentagem com %, raio por cor, degradê suave e pulsação ao correr/Shift.",
            effects
        );
    }

    [MenuItem("Tools/MiniMarket/Validar Efeitos Visuais da Energia", priority = 6)]
    public static void Validar()
    {
        int errors = 0;
        int warnings = 0;

        MiniMarketEnergyProgressBar bar =
            Object.FindAnyObjectByType<MiniMarketEnergyProgressBar>(FindObjectsInactive.Include);
        MiniMarketEnergyVisualEffects effects =
            Object.FindAnyObjectByType<MiniMarketEnergyVisualEffects>(FindObjectsInactive.Include);

        if (bar == null)
        {
            Debug.LogError("[EnergyVisualValidator] MiniMarketEnergyProgressBar ausente.");
            return;
        }

        if (effects == null)
        {
            errors++;
            Debug.LogError("[EnergyVisualValidator] MiniMarketEnergyVisualEffects ausente.", bar);
        }
        else
        {
            if (effects.textoPorcentagem == null) { errors++; Debug.LogError("[EnergyVisualValidator] Texto percentual ausente.", effects); }
            if (effects.iconeRaio == null) { errors++; Debug.LogError("[EnergyVisualValidator] Imagem do raio ausente.", effects); }
            if (effects.preenchimento == null) { errors++; Debug.LogError("[EnergyVisualValidator] Progress fill ausente.", effects); }
            if (effects.raioVerde == null) { errors++; Debug.LogError("[EnergyVisualValidator] green_energy ausente.", effects); }
            if (effects.raioAmarelo == null) { errors++; Debug.LogError("[EnergyVisualValidator] yellow_energy ausente.", effects); }
            if (effects.raioVermelho == null) { errors++; Debug.LogError("[EnergyVisualValidator] red_energy ausente.", effects); }
            if (!effects.pulsarAoCorrer) { warnings++; Debug.LogWarning("[EnergyVisualValidator] Pulsação ao correr está desligada.", effects); }
        }

        if (bar.textoQuantidade != null && !bar.textoQuantidade.text.EndsWith("%"))
        {
            warnings++;
            Debug.LogWarning("[EnergyVisualValidator] Preview do texto ainda não termina em %.", bar.textoQuantidade);
        }

        Debug.Log("[EnergyVisualValidator] Finalizado. Erros=" + errors + ", avisos=" + warnings + ".");
    }

    private static Image EncontrarRaio(MiniMarketEnergyProgressBar bar)
    {
        if (bar.iconeEnergia != null)
            return bar.iconeEnergia;

        Transform root = bar.transform.parent;
        if (root == null)
            return null;

        Image[] images = root.GetComponentsInChildren<Image>(true);
        Image fallback = null;

        for (int i = 0; i < images.Length; i++)
        {
            Image image = images[i];
            if (image == null || image == bar.imagemOriginal || image == bar.preenchimentoVerde)
                continue;
            if (bar.imagemOriginal != null && image.transform.IsChildOf(bar.imagemOriginal.transform))
                continue;

            string name = Compact(image.name);
            string sprite = image.sprite != null ? Compact(image.sprite.name) : string.Empty;

            if (name == "image" || name.Contains("raio") || name.Contains("energyicon") || name.Contains("iconeenergia"))
                return image;

            if (fallback == null && sprite.Contains("energy"))
                fallback = image;
        }

        return fallback;
    }

    private static Text EncontrarTexto(Transform root)
    {
        if (root == null)
            return null;

        Text[] texts = root.GetComponentsInChildren<Text>(true);
        for (int i = 0; i < texts.Length; i++)
        {
            Text text = texts[i];
            if (text == null)
                continue;

            string name = Compact(text.name);
            if (name == "txtqtd" || name.Contains("percent") || name.Contains("porcent"))
                return text;
        }

        return null;
    }

    private static string Compact(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim().ToLowerInvariant()
                .Replace("_", string.Empty)
                .Replace("-", string.Empty)
                .Replace(" ", string.Empty)
                .Replace("á", "a")
                .Replace("ã", "a")
                .Replace("ç", "c");
    }
}
#endif
