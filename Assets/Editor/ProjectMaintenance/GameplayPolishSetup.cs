#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>
/// Reparo explícito e não destrutivo dos sistemas desta atualização.
/// Não executa automaticamente, não move arquivos e não toca em Brick Project Studio.
/// </summary>
public static class GameplayPolishSetup
{
    private const string ConfigurationObjectName = "GameSystemsConfiguration";
    private const string PurchaseTriggerChildName = "BuySceneEntryTrigger_Runtime";

    [MenuItem("Tools/Game Systems/Apply Gameplay Polish (HUD Grab Purchase MiniMap Mobile)", priority = 3)]
    public static void Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("[GameplayPolishSetup] Saia do Play Mode antes de executar o reparo.");
            return;
        }

        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded || string.IsNullOrEmpty(scene.path))
        {
            Debug.LogWarning("[GameplayPolishSetup] Abra e salve a SampleScene antes de executar.");
            return;
        }

        CameraRelativeMovement movement = Object.FindAnyObjectByType<CameraRelativeMovement>(FindObjectsInactive.Include);
        PlayerCameraController playerCamera = Object.FindAnyObjectByType<PlayerCameraController>(FindObjectsInactive.Include);
        GetItemController getItem = Object.FindAnyObjectByType<GetItemController>(FindObjectsInactive.Include);
        InteractionFocusController interaction = Object.FindAnyObjectByType<InteractionFocusController>(FindObjectsInactive.Include);

        if (movement == null || playerCamera == null)
        {
            Debug.LogWarning(
                "[GameplayPolishSetup] Jogador/câmera atual não encontrados. Execute primeiro " +
                "Tools > Player System > Create or Repair Player System."
            );
            return;
        }

        bool changed = false;
        int hudCount = 0;
        int purchaseTriggerCount = 0;

        try
        {
            GameObject configuration = FindOrCreateConfigurationRoot(ref changed);

            RuntimeMiniMap miniMap = EnsureComponent<RuntimeMiniMap>(configuration, ref changed);
            MobileControlsHUD mobileHud = EnsureComponent<MobileControlsHUD>(configuration, ref changed);
            FirstPersonReticleController reticle = EnsureComponent<FirstPersonReticleController>(configuration, ref changed);

            changed |= ConfigureMiniMap(miniMap, movement);
            changed |= ConfigureMobileHud(mobileHud, movement, playerCamera, interaction, getItem);
            changed |= ConfigureReticle(reticle, playerCamera, getItem);
            changed |= ConfigureSafeGrab(getItem);
            changed |= ConfigureEnergyHuds(movement, ref hudCount);
            changed |= ConfigurePurchaseEntry(ref purchaseTriggerCount);

            if (changed)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                AssetDatabase.SaveAssets();
            }

            Debug.Log(
                "[GameplayPolishSetup] Concluído. HUDs de energia=" + hudCount +
                ", entradas de compra=" + purchaseTriggerCount +
                ", cena alterada=" + changed + "."
            );
        }
        catch (Exception exception)
        {
            Debug.LogError("[GameplayPolishSetup] Falha controlada: " + exception);
        }
    }

    [MenuItem("Tools/Game Systems/Validate Gameplay Polish", priority = 4)]
    public static void Validate()
    {
        int errors = 0;
        int warnings = 0;

        CameraRelativeMovement movement = Object.FindAnyObjectByType<CameraRelativeMovement>(FindObjectsInactive.Include);
        PlayerCameraController camera = Object.FindAnyObjectByType<PlayerCameraController>(FindObjectsInactive.Include);
        GetItemController getItem = Object.FindAnyObjectByType<GetItemController>(FindObjectsInactive.Include);
        MiniMarketEnergySegmentHUD[] huds = Object.FindObjectsByType<MiniMarketEnergySegmentHUD>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        BuySceneEntryTrigger[] triggers = Object.FindObjectsByType<BuySceneEntryTrigger>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        RuntimeMiniMap miniMap = Object.FindAnyObjectByType<RuntimeMiniMap>(FindObjectsInactive.Include);
        MobileControlsHUD mobile = Object.FindAnyObjectByType<MobileControlsHUD>(FindObjectsInactive.Include);
        FirstPersonReticleController reticle = Object.FindAnyObjectByType<FirstPersonReticleController>(FindObjectsInactive.Include);

        if (movement == null) { errors++; Debug.LogError("[GameplayPolishValidator] CameraRelativeMovement ausente."); }
        if (camera == null) { errors++; Debug.LogError("[GameplayPolishValidator] PlayerCameraController ausente."); }
        if (getItem == null) { errors++; Debug.LogError("[GameplayPolishValidator] GetItemController ausente."); }
        if (huds.Length == 0) { warnings++; Debug.LogWarning("[GameplayPolishValidator] HUD de energia ausente."); }
        if (triggers.Length == 0) { warnings++; Debug.LogWarning("[GameplayPolishValidator] BuySceneEntryTrigger ausente."); }
        if (miniMap == null) { warnings++; Debug.LogWarning("[GameplayPolishValidator] RuntimeMiniMap não está salvo na cena."); }
        if (mobile == null) { warnings++; Debug.LogWarning("[GameplayPolishValidator] MobileControlsHUD não está salvo na cena."); }
        if (reticle == null) { warnings++; Debug.LogWarning("[GameplayPolishValidator] FirstPersonReticleController ausente."); }

        Debug.Log("[GameplayPolishValidator] Finalizado. Erros=" + errors + ", avisos=" + warnings + ".");
    }

    private static GameObject FindOrCreateConfigurationRoot(ref bool changed)
    {
        GameObject existing = GameObject.Find(ConfigurationObjectName);
        if (existing != null)
            return existing;

        GameObject created = new GameObject(ConfigurationObjectName);
        Undo.RegisterCreatedObjectUndo(created, "Criar configuração dos sistemas");
        changed = true;
        return created;
    }

    private static T EnsureComponent<T>(GameObject host, ref bool changed) where T : Component
    {
        T component = Object.FindAnyObjectByType<T>(FindObjectsInactive.Include);
        if (component != null)
            return component;

        component = Undo.AddComponent<T>(host);
        changed = true;
        return component;
    }

    private static bool ConfigureMiniMap(RuntimeMiniMap miniMap, CameraRelativeMovement movement)
    {
        if (miniMap == null)
            return false;

        bool changed = false;
        Undo.RecordObject(miniMap, "Configurar minimapa");

        changed |= SetReference(ref miniMap.target, movement.transform);
        changed |= SetValue(ref miniMap.startOpen, true);
        changed |= SetValue(ref miniMap.showZoomButtons, true);
        changed |= SetValue(ref miniMap.showPlayerDot, true);

        if (changed)
            EditorUtility.SetDirty(miniMap);

        return changed;
    }

    private static bool ConfigureMobileHud(
        MobileControlsHUD mobile,
        CameraRelativeMovement movement,
        PlayerCameraController camera,
        InteractionFocusController interaction,
        GetItemController getItem)
    {
        if (mobile == null)
            return false;

        bool changed = false;
        Undo.RecordObject(mobile, "Configurar controles mobile");

        changed |= SetReference(ref mobile.movimento, movement);
        changed |= SetReference(ref mobile.cameraController, camera);
        changed |= SetReference(ref mobile.interactionController, interaction);
        changed |= SetReference(ref mobile.getItemController, getItem);
        changed |= SetValue(ref mobile.ocultarNoDesktop, true);
        changed |= SetValue(ref mobile.forcarVisivelParaTestes, false);
        changed |= SetValue(ref mobile.respeitarSafeArea, true);

        if (changed)
            EditorUtility.SetDirty(mobile);

        return changed;
    }

    private static bool ConfigureReticle(
        FirstPersonReticleController reticle,
        PlayerCameraController camera,
        GetItemController getItem)
    {
        if (reticle == null)
            return false;

        bool changed = false;
        Undo.RecordObject(reticle, "Configurar mira de primeira pessoa");

        Sprite clickOff = FindSpriteByName("click_off");
        Sprite clickOn = FindSpriteByName("click_on");

        if (clickOff != null && reticle.idleSprite != clickOff)
        {
            reticle.idleSprite = clickOff;
            changed = true;
        }

        if (clickOn != null)
        {
            if (reticle.selectedSprite != clickOn)
            {
                reticle.selectedSprite = clickOn;
                changed = true;
            }
            if (reticle.holdingSprite != clickOn)
            {
                reticle.holdingSprite = clickOn;
                changed = true;
            }
        }
        else
        {
            Debug.LogWarning(
                "[GameplayPolishSetup] Sprite click_on não encontrado no AssetDatabase. " +
                "Arraste-o manualmente em Selected Sprite e Holding Sprite do FirstPersonReticleController."
            );
        }

        if (changed)
            EditorUtility.SetDirty(reticle);

        return changed;
    }

    private static bool ConfigureSafeGrab(GetItemController getItem)
    {
        if (getItem == null)
            return false;

        bool changed = false;
        Undo.RecordObject(getItem, "Configurar soltura segura");

        changed |= SetValue(ref getItem.dropWhenLeavingFirstPerson, true);
        changed |= SetValue(ref getItem.inheritCameraVelocityOnNormalRelease, false);
        changed |= SetFloat(ref getItem.safeDropVelocityMultiplier, 0.12f);
        changed |= SetFloat(ref getItem.safeDropAngularVelocityMultiplier, 0.2f);
        changed |= SetFloat(ref getItem.maximumSafeDropSpeed, 1.25f);

        if (changed)
            EditorUtility.SetDirty(getItem);

        return changed;
    }

    private static bool ConfigureEnergyHuds(CameraRelativeMovement movement, ref int configuredCount)
    {
        MiniMarketEnergySegmentHUD[] huds = Object.FindObjectsByType<MiniMarketEnergySegmentHUD>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None
        );

        if (huds.Length == 0)
        {
            Text candidate = FindEnergyTextCandidate();
            if (candidate != null)
            {
                MiniMarketEnergySegmentHUD created = Undo.AddComponent<MiniMarketEnergySegmentHUD>(candidate.gameObject);
                created.textoEnergia = candidate;
                huds = new[] { created };
            }
        }

        bool changed = false;
        for (int i = 0; i < huds.Length; i++)
        {
            MiniMarketEnergySegmentHUD hud = huds[i];
            if (hud == null)
                continue;

            configuredCount++;
            Undo.RecordObject(hud, "Configurar HUD de energia");

            changed |= SetReference(ref hud.movimento, movement);
            changed |= SetValue(ref hud.autoDetectarBarras, true);
            changed |= SetValue(ref hud.criarBarrasSegmentadasQuandoAusentes, true);
            changed |= SetValue(ref hud.animarPreenchimento, true);

            if (hud.modoBarraPrincipal != MiniMarketEnergySegmentHUD.PrimaryBarDisplayMode.ActiveSegment)
            {
                hud.modoBarraPrincipal = MiniMarketEnergySegmentHUD.PrimaryBarDisplayMode.ActiveSegment;
                changed = true;
            }

            Image bestBar = FindBestEnergyBar(hud);
            if (bestBar != null && hud.barraEnergia != bestBar)
            {
                hud.barraEnergia = bestBar;
                changed = true;
            }

            if (changed)
                EditorUtility.SetDirty(hud);
        }

        return changed;
    }

    private static Image FindBestEnergyBar(MiniMarketEnergySegmentHUD hud)
    {
        Transform root = hud.transform;
        Transform current = hud.transform;

        while (current != null && current.GetComponent<Canvas>() == null)
        {
            string lower = current.name.ToLowerInvariant();
            if (ContainsAny(lower, "hud", "energia", "energy", "stamina"))
                root = current;
            current = current.parent;
        }

        Image[] images = root.GetComponentsInChildren<Image>(true);
        Image best = null;
        float bestScore = float.MinValue;

        for (int i = 0; i < images.Length; i++)
        {
            Image image = images[i];
            if (image == null)
                continue;

            string lower = image.name.ToLowerInvariant();
            if (ContainsAny(lower, "icon", "icone", "fundo", "background", "frame", "moldura", "mask"))
                continue;

            float width = Mathf.Abs(image.rectTransform.rect.width);
            float height = Mathf.Max(1f, Mathf.Abs(image.rectTransform.rect.height));
            float score = width / height;

            if (lower.Contains("progress")) score += 100f;
            if (lower.Contains("fill")) score += 80f;
            if (ContainsAny(lower, "barraenergia", "barra_energia", "stamina", "energy", "energia")) score += 45f;
            if (lower.Contains("bar")) score += 25f;

            if (score > bestScore)
            {
                bestScore = score;
                best = image;
            }
        }

        return best;
    }

    private static Text FindEnergyTextCandidate()
    {
        Text[] texts = Object.FindObjectsByType<Text>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        Text fallback = null;

        for (int i = 0; i < texts.Length; i++)
        {
            Text text = texts[i];
            if (text == null || text.GetComponentInParent<Canvas>() == null)
                continue;

            string lower = text.name.ToLowerInvariant();
            if (ContainsAny(lower, "energia", "energy", "stamina"))
                return text;

            if (fallback == null && !string.IsNullOrEmpty(text.text) && text.text.Contains("/"))
                fallback = text;
        }

        return fallback;
    }

    private static bool ConfigurePurchaseEntry(ref int configuredCount)
    {
        GameObject source = FindBestBuyAreaObject();
        if (source == null)
        {
            Debug.LogWarning("[GameplayPolishSetup] Objeto Buy_Area não encontrado na cena.");
            return false;
        }

        Collider sourceCollider = source.GetComponent<Collider>();
        if (sourceCollider == null)
            sourceCollider = source.GetComponentInChildren<Collider>(true);

        if (sourceCollider == null)
        {
            Debug.LogWarning("[GameplayPolishSetup] Buy_Area encontrado, mas sem Collider.", source);
            return false;
        }

        bool changed = false;
        BuySceneEntryTrigger directTrigger = sourceCollider.GetComponent<BuySceneEntryTrigger>();
        Transform child = sourceCollider.transform.Find(PurchaseTriggerChildName);

        if (child == null)
        {
            GameObject created = new GameObject(PurchaseTriggerChildName);
            Undo.RegisterCreatedObjectUndo(created, "Criar entrada visual de compra");
            created.transform.SetParent(sourceCollider.transform, false);
            child = created.transform;
            changed = true;
        }

        BoxCollider childCollider = child.GetComponent<BoxCollider>();
        if (childCollider == null)
        {
            childCollider = Undo.AddComponent<BoxCollider>(child.gameObject);
            changed = true;
        }

        ConfigurePurchaseTriggerBounds(sourceCollider, child, childCollider);

        BuySceneEntryTrigger trigger = child.GetComponent<BuySceneEntryTrigger>();
        if (trigger == null)
        {
            trigger = Undo.AddComponent<BuySceneEntryTrigger>(child.gameObject);
            changed = true;
        }

        if (directTrigger != null && directTrigger != trigger)
        {
            EditorUtility.CopySerialized(directTrigger, trigger);
            Undo.DestroyObjectImmediate(directTrigger);
            changed = true;
        }

        Undo.RecordObject(sourceCollider, "Preservar colisão da calçada");
        if (sourceCollider.isTrigger)
        {
            sourceCollider.isTrigger = false;
            changed = true;
        }

        Undo.RecordObject(trigger, "Configurar entrada de compra");
        trigger.mostrarMarcacaoVisual = true;
        trigger.mostrarXCentral = true;
        trigger.atualizarVisualEmTempoReal = true;
        trigger.larguraLinha = Mathf.Max(0.14f, trigger.larguraLinha);
        trigger.alturaAcimaDoCollider = Mathf.Max(0.12f, trigger.alturaAcimaDoCollider);

        BuySceneCameraModeController controller = Object.FindAnyObjectByType<BuySceneCameraModeController>(FindObjectsInactive.Include);
        if (controller != null)
            trigger.controladorBuyScene = controller;

        EditorUtility.SetDirty(sourceCollider);
        EditorUtility.SetDirty(childCollider);
        EditorUtility.SetDirty(trigger);
        configuredCount++;
        return true;
    }

    private static GameObject FindBestBuyAreaObject()
    {
        GameObject[] objects = Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        GameObject best = null;
        int bestScore = int.MinValue;

        for (int i = 0; i < objects.Length; i++)
        {
            GameObject candidate = objects[i];
            if (!IsSceneObject(candidate) || candidate.GetComponentInParent<Canvas>() != null)
                continue;

            string lower = candidate.name.ToLowerInvariant();
            string compact = Compact(lower);
            bool exact = compact.Contains("buyarea") || compact.Contains("areabuy");
            bool named = ContainsAny(lower, "compra", "buy", "entrada", "entry") &&
                         ContainsAny(lower, "area", "área", "terreno", "land", "calcada", "calçada");

            if (!exact && !named)
                continue;

            int score = exact ? 100 : 20;
            if (candidate.GetComponent<Collider>() != null) score += 10;
            if (candidate.activeInHierarchy) score += 5;

            if (score > bestScore)
            {
                best = candidate;
                bestScore = score;
            }
        }

        return best;
    }

    private static void ConfigurePurchaseTriggerBounds(
        Collider source,
        Transform triggerTransform,
        BoxCollider triggerCollider)
    {
        Bounds bounds = source.bounds;
        Vector3 scale = source.transform.lossyScale;
        float sx = Mathf.Max(0.0001f, Mathf.Abs(scale.x));
        float sy = Mathf.Max(0.0001f, Mathf.Abs(scale.y));
        float sz = Mathf.Max(0.0001f, Mathf.Abs(scale.z));

        triggerTransform.localPosition = source.transform.InverseTransformPoint(bounds.center);
        triggerTransform.localRotation = Quaternion.identity;
        triggerTransform.localScale = Vector3.one;

        triggerCollider.center = Vector3.zero;
        triggerCollider.size = new Vector3(
            Mathf.Max(0.8f, bounds.size.x / sx),
            Mathf.Max(1.2f, bounds.size.y / sy + 1f),
            Mathf.Max(0.8f, bounds.size.z / sz)
        );
        triggerCollider.isTrigger = true;
        triggerCollider.enabled = true;
    }

    private static Sprite FindSpriteByName(string desiredName)
    {
        string[] guids = AssetDatabase.FindAssets(desiredName + " t:Sprite", new[] { "Assets" });

        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            Sprite direct = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (direct != null && direct.name.IndexOf(desiredName, StringComparison.OrdinalIgnoreCase) >= 0)
                return direct;

            Object[] assets = AssetDatabase.LoadAllAssetsAtPath(path);
            for (int a = 0; a < assets.Length; a++)
            {
                if (assets[a] is Sprite sprite &&
                    sprite.name.IndexOf(desiredName, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return sprite;
                }
            }
        }

        return null;
    }

    private static bool IsSceneObject(GameObject target)
    {
        return target != null && target.scene.IsValid() && target.scene.isLoaded &&
               !EditorUtility.IsPersistent(target);
    }

    private static string Compact(string value)
    {
        return value.Replace("_", string.Empty)
                    .Replace("-", string.Empty)
                    .Replace(" ", string.Empty)
                    .Replace("á", "a")
                    .Replace("ç", "c");
    }

    private static bool ContainsAny(string value, params string[] terms)
    {
        for (int i = 0; i < terms.Length; i++)
        {
            if (value.Contains(terms[i]))
                return true;
        }
        return false;
    }

    private static bool SetValue(ref bool current, bool desired)
    {
        if (current == desired)
            return false;
        current = desired;
        return true;
    }

    private static bool SetFloat(ref float current, float desired)
    {
        if (Mathf.Approximately(current, desired))
            return false;
        current = desired;
        return true;
    }

    private static bool SetReference<T>(ref T current, T desired) where T : Object
    {
        if (current == desired)
            return false;
        current = desired;
        return true;
    }
}
#endif
