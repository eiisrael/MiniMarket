#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>
/// Cria ou repara a barra Canvas/StaminaHUD/Energy e persiste as referências
/// dos sprites energy_green, energy_yellow e energy_red na cena.
/// </summary>
public static class EnergyProgressBarSetup
{
    private const string MenuCriar =
        "Tools/MiniMarket/Criar ou Reparar Barra de Energia";
    private const string MenuValidar =
        "Tools/MiniMarket/Validar Barra de Energia";

    [MenuItem(MenuCriar, priority = 1)]
    public static void CriarOuReparar()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning(
                "[EnergyProgressBarSetup] Saia do Play Mode antes de executar."
            );
            return;
        }

        Scene cena = SceneManager.GetActiveScene();
        if (!cena.IsValid() || !cena.isLoaded || string.IsNullOrWhiteSpace(cena.path))
        {
            Debug.LogWarning(
                "[EnergyProgressBarSetup] Abra e salve a SampleScene antes de executar."
            );
            return;
        }

        try
        {
            Transform staminaHud = EncontrarTransform("StaminaHUD");
            if (staminaHud == null)
                staminaHud = CriarStaminaHud();

            if (staminaHud == null)
            {
                Debug.LogError(
                    "[EnergyProgressBarSetup] Canvas/StaminaHUD não pôde ser criado."
                );
                return;
            }

            Image energy = EncontrarEnergy(staminaHud);
            if (energy == null)
                energy = CriarEnergy(staminaHud);

            if (energy == null)
            {
                Debug.LogError(
                    "[EnergyProgressBarSetup] O objeto Energy não pôde ser criado."
                );
                return;
            }

            MiniMarketEnergyProgressBar controlador =
                energy.GetComponent<MiniMarketEnergyProgressBar>();

            if (controlador == null)
                controlador = Undo.AddComponent<MiniMarketEnergyProgressBar>(energy.gameObject);

            Undo.RecordObject(controlador, "Configurar barra de energia");
            Undo.RecordObject(energy, "Configurar imagem de energia");

            controlador.barra = energy;
            controlador.textoQuantidade = EncontrarTextoQuantidade(staminaHud);
            controlador.movimento = Object.FindAnyObjectByType<CameraRelativeMovement>(
                FindObjectsInactive.Include
            );

            controlador.energiaVerde = EncontrarEPrepararSprite(
                "energy_green", "green_energy"
            );
            controlador.energiaAmarela = EncontrarEPrepararSprite(
                "energy_yellow", "yellow_energy"
            );
            controlador.energiaVermelha = EncontrarEPrepararSprite(
                "energy_red", "red_energy"
            );

            controlador.procurarSpritesAutomaticamente = true;
            controlador.corrigirEstadoInconsistente = true;
            controlador.manterTextoSegmentado = true;
            controlador.animar = true;
            controlador.velocidade = Mathf.Max(12f, controlador.velocidade);
            controlador.limiteAmarelo = 0.55f;
            controlador.limiteVermelho = 0.25f;

            energy.type = Image.Type.Filled;
            energy.fillMethod = Image.FillMethod.Horizontal;
            energy.fillOrigin = (int)Image.OriginHorizontal.Left;
            energy.fillClockwise = true;
            energy.fillAmount = 1f;
            energy.color = Color.white;
            energy.raycastTarget = false;
            energy.preserveAspect = false;

            MiniMarketEnergySegmentHUD hud =
                staminaHud.GetComponentInChildren<MiniMarketEnergySegmentHUD>(true);

            if (hud != null)
            {
                Undo.RecordObject(hud, "Transferir autoridade da barra de energia");
                hud.autoDetectarBarras = false;
                hud.criarBarrasSegmentadasQuandoAusentes = false;

                if (hud.barraEnergia == energy)
                    hud.barraEnergia = null;

                if (controlador.textoQuantidade != null)
                    hud.textoEnergia = controlador.textoQuantidade;

                EditorUtility.SetDirty(hud);
            }

            EditorUtility.SetDirty(controlador);
            EditorUtility.SetDirty(energy);
            EditorSceneManager.MarkSceneDirty(cena);
            EditorSceneManager.SaveScene(cena);
            AssetDatabase.SaveAssets();

            controlador.RebuscarTudo();

            Debug.Log(
                "[EnergyProgressBarSetup] Barra reparada em Canvas/StaminaHUD/Energy. " +
                "Verde=" + NomeSprite(controlador.energiaVerde) +
                ", Amarelo=" + NomeSprite(controlador.energiaAmarela) +
                ", Vermelho=" + NomeSprite(controlador.energiaVermelha) + ".",
                controlador
            );
        }
        catch (Exception exception)
        {
            Debug.LogError(
                "[EnergyProgressBarSetup] Falha controlada: " + exception
            );
        }
    }

    [MenuItem(MenuValidar, priority = 2)]
    public static void Validar()
    {
        int erros = 0;
        int avisos = 0;

        Transform staminaHud = EncontrarTransform("StaminaHUD");
        if (staminaHud == null)
        {
            erros++;
            Debug.LogError("[EnergyProgressBarValidator] StaminaHUD ausente.");
        }

        Image energy = staminaHud != null ? EncontrarEnergy(staminaHud) : null;
        if (energy == null)
        {
            erros++;
            Debug.LogError("[EnergyProgressBarValidator] Energy ausente.");
        }

        MiniMarketEnergyProgressBar controlador =
            energy != null ? energy.GetComponent<MiniMarketEnergyProgressBar>() : null;

        if (controlador == null)
        {
            erros++;
            Debug.LogError(
                "[EnergyProgressBarValidator] MiniMarketEnergyProgressBar ausente."
            );
        }
        else
        {
            if (controlador.energiaVerde == null)
            {
                avisos++;
                Debug.LogWarning(
                    "[EnergyProgressBarValidator] energy_green não foi atribuído."
                );
            }

            if (controlador.energiaAmarela == null)
            {
                avisos++;
                Debug.LogWarning(
                    "[EnergyProgressBarValidator] energy_yellow não foi atribuído."
                );
            }

            if (controlador.energiaVermelha == null)
            {
                avisos++;
                Debug.LogWarning(
                    "[EnergyProgressBarValidator] energy_red não foi atribuído."
                );
            }

            if (controlador.textoQuantidade == null)
            {
                avisos++;
                Debug.LogWarning(
                    "[EnergyProgressBarValidator] Txt_Qtd não foi localizado."
                );
            }

            if (controlador.movimento == null)
            {
                avisos++;
                Debug.LogWarning(
                    "[EnergyProgressBarValidator] CameraRelativeMovement não está serializado; " +
                    "será procurado no Play Mode."
                );
            }
        }

        if (energy != null && energy.type != Image.Type.Filled)
        {
            erros++;
            Debug.LogError(
                "[EnergyProgressBarValidator] Energy não está como Image.Type.Filled."
            );
        }

        Debug.Log(
            "[EnergyProgressBarValidator] Finalizado. Erros=" + erros +
            ", avisos=" + avisos + "."
        );
    }

    private static Transform CriarStaminaHud()
    {
        Canvas canvas = Object.FindAnyObjectByType<Canvas>(FindObjectsInactive.Include);
        if (canvas == null)
            return null;

        GameObject objeto = new GameObject(
            "StaminaHUD",
            typeof(RectTransform)
        );

        Undo.RegisterCreatedObjectUndo(objeto, "Criar StaminaHUD");
        objeto.transform.SetParent(canvas.transform, false);

        RectTransform rect = objeto.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(1f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(1f, 1f);
        rect.anchoredPosition = new Vector2(-40f, -40f);
        rect.sizeDelta = new Vector2(360f, 110f);

        return objeto.transform;
    }

    private static Image CriarEnergy(Transform staminaHud)
    {
        GameObject objeto = new GameObject(
            "Energy",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image)
        );

        Undo.RegisterCreatedObjectUndo(objeto, "Criar barra Energy");
        objeto.transform.SetParent(staminaHud, false);

        RectTransform rect = objeto.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0f, 0.5f);
        rect.anchoredPosition = new Vector2(-144.6f, 25.8f);
        rect.sizeDelta = new Vector2(310.4f, 69.4f);

        Image image = objeto.GetComponent<Image>();
        image.raycastTarget = false;
        return image;
    }

    private static Image EncontrarEnergy(Transform staminaHud)
    {
        Image[] imagens = staminaHud.GetComponentsInChildren<Image>(true);
        Image fallback = null;

        for (int i = 0; i < imagens.Length; i++)
        {
            Image imagem = imagens[i];
            if (imagem == null)
                continue;

            string nome = Normalizar(imagem.name);
            if (nome == "energy")
                return imagem;

            if (fallback == null &&
                (nome == "energia" || nome == "energyfill" || nome == "barraenergia"))
            {
                fallback = imagem;
            }
        }

        return fallback;
    }

    private static Text EncontrarTextoQuantidade(Transform staminaHud)
    {
        Text[] textos = staminaHud.GetComponentsInChildren<Text>(true);
        Text fallback = null;

        for (int i = 0; i < textos.Length; i++)
        {
            Text texto = textos[i];
            if (texto == null)
                continue;

            string nome = Normalizar(texto.name);
            if (nome == "txtqtd" || nome == "quantidade" ||
                nome == "energytext" || nome == "staminatext")
            {
                return texto;
            }

            if (fallback == null && PareceContador(texto.text))
                fallback = texto;
        }

        return fallback;
    }

    private static Transform EncontrarTransform(string nomeExato)
    {
        Transform[] transforms = Object.FindObjectsByType<Transform>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None
        );

        for (int i = 0; i < transforms.Length; i++)
        {
            Transform atual = transforms[i];
            if (atual != null &&
                string.Equals(atual.name, nomeExato, StringComparison.OrdinalIgnoreCase))
            {
                return atual;
            }
        }

        return null;
    }

    private static Sprite EncontrarEPrepararSprite(params string[] aliases)
    {
        for (int a = 0; a < aliases.Length; a++)
        {
            string[] guids = AssetDatabase.FindAssets(aliases[a]);

            for (int i = 0; i < guids.Length; i++)
            {
                string caminho = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (string.IsNullOrWhiteSpace(caminho))
                    continue;

                string extensao = Path.GetExtension(caminho);
                if (!string.Equals(extensao, ".png", StringComparison.OrdinalIgnoreCase))
                    continue;

                string nome = Normalizar(Path.GetFileNameWithoutExtension(caminho));
                if (!Corresponde(nome, aliases))
                    continue;

                TextureImporter importer = AssetImporter.GetAtPath(caminho) as TextureImporter;
                if (importer != null)
                {
                    bool alterado = false;

                    if (importer.textureType != TextureImporterType.Sprite)
                    {
                        importer.textureType = TextureImporterType.Sprite;
                        alterado = true;
                    }

                    if (importer.spriteImportMode != SpriteImportMode.Single)
                    {
                        importer.spriteImportMode = SpriteImportMode.Single;
                        alterado = true;
                    }

                    if (!importer.alphaIsTransparency)
                    {
                        importer.alphaIsTransparency = true;
                        alterado = true;
                    }

                    if (importer.mipmapEnabled)
                    {
                        importer.mipmapEnabled = false;
                        alterado = true;
                    }

                    if (alterado)
                        importer.SaveAndReimport();
                }

                Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(caminho);
                if (sprite != null)
                    return sprite;
            }
        }

        return null;
    }

    private static bool Corresponde(string nome, string[] aliases)
    {
        for (int i = 0; i < aliases.Length; i++)
        {
            string alias = Normalizar(aliases[i]);
            if (nome == alias || nome.Contains(alias))
                return true;
        }

        return false;
    }

    private static bool PareceContador(string valor)
    {
        if (string.IsNullOrWhiteSpace(valor))
            return false;

        int indice = valor.IndexOf('/');
        return indice > 0 &&
               indice < valor.Length - 1 &&
               char.IsDigit(valor[indice - 1]) &&
               char.IsDigit(valor[indice + 1]);
    }

    private static string NomeSprite(Sprite sprite)
    {
        return sprite != null ? sprite.name : "não encontrado";
    }

    private static string Normalizar(string valor)
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
}
#endif
