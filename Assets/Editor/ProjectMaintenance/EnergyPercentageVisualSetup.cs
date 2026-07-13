#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>
/// Configura o HUD persistente para exibir porcentagem e alternar ícone/cor por faixa.
/// Preserva posição, escala, âncoras e modificações visuais já feitas pelo usuário.
/// </summary>
public static class EnergyPercentageVisualSetup
{
    private const string GreenPath = "Assets/UI/Models/Textures/HUD/green_energy.png";
    private const string YellowPath = "Assets/UI/Models/Textures/HUD/yellow_energy.png";
    private const string RedPath = "Assets/UI/Models/Textures/HUD/red_energy.png";

    [MenuItem("Tools/MiniMarket/Configurar Energia por Porcentagem e Cores", priority = 3)]
    public static void Configurar()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("[EnergyPercentageVisualSetup] Saia do Play Mode antes de executar.");
            return;
        }

        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded || string.IsNullOrWhiteSpace(scene.path))
        {
            Debug.LogWarning("[EnergyPercentageVisualSetup] Abra e salve a SampleScene primeiro.");
            return;
        }

        MiniMarketEnergyProgressBar controller =
            Object.FindAnyObjectByType<MiniMarketEnergyProgressBar>(FindObjectsInactive.Include);

        if (controller == null)
        {
            EnergyProgressBarSetup.CriarOuReparar();
            controller = Object.FindAnyObjectByType<MiniMarketEnergyProgressBar>(FindObjectsInactive.Include);
        }

        if (controller == null)
        {
            Debug.LogError("[EnergyPercentageVisualSetup] MiniMarketEnergyProgressBar não foi encontrado.");
            return;
        }

        Undo.RecordObject(controller, "Configurar energia percentual");

        controller.mostrarPorcentagem = true;
        controller.manterTextoSegmentado = false;
        controller.formatoPorcentagem = "{0}%";
        controller.usarEnergiaTotalSegmentada = true;
        controller.limiteVerde = 0.61f;
        controller.limiteVermelho = 0.25f;

        controller.energiaVerdeSprite = AssetDatabase.LoadAssetAtPath<Sprite>(GreenPath);
        controller.energiaAmarelaSprite = AssetDatabase.LoadAssetAtPath<Sprite>(YellowPath);
        controller.energiaVermelhaSprite = AssetDatabase.LoadAssetAtPath<Sprite>(RedPath);

        if (controller.textoQuantidade == null)
            controller.textoQuantidade = EncontrarTexto(controller.transform.parent);

        if (controller.iconeEnergia == null)
            controller.iconeEnergia = EncontrarIcone(controller.transform.parent, controller.imagemOriginal);

        if (controller.preenchimentoVerde != null)
        {
            Undo.RecordObject(controller.preenchimentoVerde, "Aplicar cor verde inicial");
            controller.preenchimentoVerde.color = controller.corAlta;
            EditorUtility.SetDirty(controller.preenchimentoVerde);
        }

        if (controller.iconeEnergia != null && controller.energiaVerdeSprite != null)
        {
            Undo.RecordObject(controller.iconeEnergia, "Aplicar ícone verde inicial");
            controller.iconeEnergia.sprite = controller.energiaVerdeSprite;
            controller.iconeEnergia.color = Color.white;
            controller.iconeEnergia.preserveAspect = true;
            EditorUtility.SetDirty(controller.iconeEnergia);
        }

        if (controller.textoQuantidade != null)
        {
            Undo.RecordObject(controller.textoQuantidade, "Aplicar texto percentual");
            controller.textoQuantidade.text = "100%";
            EditorUtility.SetDirty(controller.textoQuantidade);
        }

        EditorUtility.SetDirty(controller);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();

        Debug.Log(
            "[EnergyPercentageVisualSetup] Configurado: 100%-61% verde, 60%-26% amarelo, 25%-0% vermelho; texto em porcentagem.",
            controller
        );
    }

    [MenuItem("Tools/MiniMarket/Validar Energia por Porcentagem e Cores", priority = 4)]
    public static void Validar()
    {
        int errors = 0;
        int warnings = 0;

        MiniMarketEnergyProgressBar controller =
            Object.FindAnyObjectByType<MiniMarketEnergyProgressBar>(FindObjectsInactive.Include);

        if (controller == null)
        {
            Debug.LogError("[EnergyPercentageValidator] MiniMarketEnergyProgressBar ausente.");
            return;
        }

        if (!controller.mostrarPorcentagem)
        {
            errors++;
            Debug.LogError("[EnergyPercentageValidator] Mostrar Porcentagem está desligado.", controller);
        }

        if (controller.textoQuantidade == null)
        {
            errors++;
            Debug.LogError("[EnergyPercentageValidator] Texto da porcentagem não está ligado.", controller);
        }

        if (controller.iconeEnergia == null)
        {
            errors++;
            Debug.LogError("[EnergyPercentageValidator] Ícone de energia não está ligado.", controller);
        }

        if (controller.energiaVerdeSprite == null) { errors++; Debug.LogError("[EnergyPercentageValidator] green_energy.png ausente.", controller); }
        if (controller.energiaAmarelaSprite == null) { errors++; Debug.LogError("[EnergyPercentageValidator] yellow_energy.png ausente.", controller); }
        if (controller.energiaVermelhaSprite == null) { errors++; Debug.LogError("[EnergyPercentageValidator] red_energy.png ausente.", controller); }

        if (!Mathf.Approximately(controller.limiteVerde, 0.61f))
        {
            warnings++;
            Debug.LogWarning("[EnergyPercentageValidator] Limite Verde está diferente de 61%.", controller);
        }

        if (!Mathf.Approximately(controller.limiteVermelho, 0.25f))
        {
            warnings++;
            Debug.LogWarning("[EnergyPercentageValidator] Limite Vermelho está diferente de 25%.", controller);
        }

        Debug.Log("[EnergyPercentageValidator] Finalizado. Erros=" + errors + ", avisos=" + warnings + ".");
    }

    private static Text EncontrarTexto(Transform root)
    {
        if (root == null)
            return null;

        Text[] texts = root.GetComponentsInChildren<Text>(true);
        Text fallback = null;

        for (int i = 0; i < texts.Length; i++)
        {
            Text text = texts[i];
            if (text == null)
                continue;

            string name = Compact(text.name);
            if (name == "txtqtd" || name.Contains("percent") || name.Contains("porcent"))
                return text;

            if (fallback == null && (text.text.Contains("/") || text.text.Contains("%")))
                fallback = text;
        }

        return fallback;
    }

    private static Image EncontrarIcone(Transform root, Image energy)
    {
        if (root == null)
            return null;

        Image[] images = root.GetComponentsInChildren<Image>(true);
        Image best = null;
        int bestScore = int.MinValue;

        for (int i = 0; i < images.Length; i++)
        {
            Image image = images[i];
            if (image == null || image == energy)
                continue;
            if (energy != null && image.transform.IsChildOf(energy.transform))
                continue;

            string name = Compact(image.name);
            if (name.Contains("background") || name.Contains("fundo") ||
                name.Contains("progress") || name.Contains("fill"))
                continue;

            int score = 0;
            if (name.Contains("energyicon") || name.Contains("iconeenergia")) score += 1000;
            if (name.Contains("icon") || name.Contains("icone")) score += 500;
            if (name == "image") score += 350;
            if (image.sprite != null && Compact(image.sprite.name).Contains("energy")) score += 700;

            if (score > bestScore)
            {
                bestScore = score;
                best = image;
            }
        }

        return bestScore > 0 ? best : null;
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
