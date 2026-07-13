#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>
/// Cria ou repara a barra verde interna de Canvas/StaminaHUD/Energy.
/// O objeto Energy original permanece como estrutura/artwork e não é diminuído.
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

            Undo.RecordObject(controlador, "Configurar barra verde interna");
            Undo.RecordObject(energy, "Preservar imagem Energy");

            controlador.imagemOriginal = energy;
            controlador.textoQuantidade = EncontrarTextoQuantidade(staminaHud);
            controlador.movimento = Object.FindAnyObjectByType<CameraRelativeMovement>(
                FindObjectsInactive.Include
            );
            controlador.usarEnergiaTotalSegmentada = true;
            controlador.corrigirEstadoInconsistente = true;
            controlador.manterTextoSegmentado = true;
            controlador.ocultarImagemOriginalComFundoSeparado = true;
            controlador.animar = true;
            controlador.velocidade = Mathf.Max(12f, controlador.velocidade);
            controlador.corBarra = new Color(0.18f, 0.95f, 0.22f, 1f);

            // Energy não é mais a própria progress bar.
            energy.type = Image.Type.Simple;
            energy.fillAmount = 1f;
            energy.raycastTarget = false;

            controlador.RecriarBarraInterna();

            MiniMarketEnergySegmentHUD hud =
                staminaHud.GetComponentInChildren<MiniMarketEnergySegmentHUD>(true);

            if (hud != null)
            {
                Undo.RecordObject(hud, "Transferir autoridade da barra de energia");
                hud.autoDetectarBarras = false;
                hud.criarBarrasSegmentadasQuandoAusentes = false;

                if (hud.barraEnergia == energy ||
                    hud.barraEnergia == controlador.preenchimentoVerde)
                {
                    hud.barraEnergia = null;
                }

                if (controlador.textoQuantidade != null)
                    hud.textoEnergia = controlador.textoQuantidade;

                EditorUtility.SetDirty(hud);
            }

            EditorUtility.SetDirty(controlador);
            EditorUtility.SetDirty(energy);

            if (controlador.areaPreenchimento != null)
                EditorUtility.SetDirty(controlador.areaPreenchimento.gameObject);
            if (controlador.preenchimentoVerde != null)
                EditorUtility.SetDirty(controlador.preenchimentoVerde.gameObject);

            EditorSceneManager.MarkSceneDirty(cena);
            EditorSceneManager.SaveScene(cena);
            AssetDatabase.SaveAssets();

            controlador.RebuscarTudo();

            Debug.Log(
                "[EnergyProgressBarSetup] Corrigido. Energy permanece estático e " +
                "EnergyProgressFill é a barra verde que aumenta/diminui.",
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
            if (controlador.imagemOriginal != energy)
            {
                erros++;
                Debug.LogError(
                    "[EnergyProgressBarValidator] Imagem Original não aponta para Energy."
                );
            }

            if (controlador.areaPreenchimento == null)
            {
                erros++;
                Debug.LogError(
                    "[EnergyProgressBarValidator] EnergyProgressArea ausente."
                );
            }

            if (controlador.preenchimentoVerde == null)
            {
                erros++;
                Debug.LogError(
                    "[EnergyProgressBarValidator] EnergyProgressFill ausente."
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

        if (energy != null && energy.type == Image.Type.Filled)
        {
            erros++;
            Debug.LogError(
                "[EnergyProgressBarValidator] Energy ainda está como Filled. " +
                "Apenas EnergyProgressFill deve diminuir."
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

        GameObject objeto = new GameObject("StaminaHUD", typeof(RectTransform));
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

        Undo.RegisterCreatedObjectUndo(objeto, "Criar Energy");
        objeto.transform.SetParent(staminaHud, false);

        RectTransform rect = objeto.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0f, 0.5f);
        rect.anchoredPosition = new Vector2(-144.6f, 25.8f);
        rect.sizeDelta = new Vector2(310.4f, 69.4f);

        Image image = objeto.GetComponent<Image>();
        image.type = Image.Type.Simple;
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

            if (fallback == null && nome == "energia")
                fallback = imagem;
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
