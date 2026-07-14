#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Mantém o visual já aprovado do Newspaper_Stand estritamente somente leitura e usa
/// esse prompt como referência para instalar o mesmo padrão no Newspaper_PlacePrompt.
///
/// A sincronização da Put_Area acontece uma única vez. Depois que a hierarquia é salva,
/// as edições manuais do Inspector não são sobrescritas em recompilações futuras.
/// </summary>
[InitializeOnLoad]
internal static class NewspaperPromptPremiumKeyInstaller
{
    private const string MenuPath =
        "Tools/MiniMarket/Jornal/Sincronizar Put Area com Visual do Newspaper Stand";

    private static bool installScheduled;

    static NewspaperPromptPremiumKeyInstaller()
    {
        ScheduleAutomaticInstall();
        EditorApplication.playModeStateChanged += HandlePlayModeStateChanged;
    }

    [MenuItem(MenuPath, priority = 2604)]
    private static void InstallFromMenu()
    {
        InstallLoadedPrompts(true, true);
    }

    [MenuItem(MenuPath, true)]
    private static bool ValidateInstallFromMenu()
    {
        return !EditorApplication.isPlayingOrWillChangePlaymode &&
               !EditorApplication.isCompiling &&
               !EditorApplication.isUpdating;
    }

    private static void HandlePlayModeStateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredEditMode)
            ScheduleAutomaticInstall();
    }

    private static void ScheduleAutomaticInstall()
    {
        if (installScheduled)
            return;

        installScheduled = true;
        EditorApplication.delayCall += RunScheduledInstall;
    }

    private static void RunScheduledInstall()
    {
        installScheduled = false;

        if (EditorApplication.isPlayingOrWillChangePlaymode ||
            EditorApplication.isCompiling ||
            EditorApplication.isUpdating)
        {
            ScheduleAutomaticInstall();
            return;
        }

        InstallLoadedPrompts(false, false);
    }

    private static void InstallLoadedPrompts(bool logResult, bool forcePlaceResync)
    {
        NewspaperWorldPromptVisual[] prompts =
            Object.FindObjectsByType<NewspaperWorldPromptVisual>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None
            );

        NewspaperPromptPremiumKeyVisual approvedStandVisual =
            ResolveApprovedStandVisual(prompts);

        int installedCount = 0;
        int synchronizedCount = 0;

        for (int i = 0; i < prompts.Length; i++)
        {
            NewspaperWorldPromptVisual prompt = prompts[i];
            if (prompt == null || prompt.name != "Newspaper_PlacePrompt")
                continue;

            Scene scene = prompt.gameObject.scene;
            if (!scene.IsValid() || !scene.isLoaded || string.IsNullOrEmpty(scene.path))
                continue;

            NewspaperPlacementAreaController controller =
                prompt.GetComponentInParent<NewspaperPlacementAreaController>(true);

            if (controller == null)
                continue;

            NewspaperPlacementAreaVisualGuard guard =
                controller.GetComponent<NewspaperPlacementAreaVisualGuard>();

            bool guardAdded = guard == null;
            if (guardAdded)
            {
                guard = Undo.AddComponent<NewspaperPlacementAreaVisualGuard>(
                    controller.gameObject
                );
                guard.controller = controller;
                installedCount++;
            }

            NewspaperPromptPremiumKeyVisual premium =
                prompt.GetComponent<NewspaperPromptPremiumKeyVisual>();

            bool componentAdded = premium == null;
            if (componentAdded)
            {
                premium = Undo.AddComponent<NewspaperPromptPremiumKeyVisual>(
                    prompt.gameObject
                );
                installedCount++;
            }

            bool hierarchyMissing = prompt.transform.Find(
                "CircularPrompt/PremiumKeyVisual"
            ) == null;

            bool requiresSynchronization = forcePlaceResync ||
                                           hierarchyMissing ||
                                           componentAdded ||
                                           guardAdded ||
                                           !guard.premiumVisualSynchronizedFromStand;

            if (!requiresSynchronization)
                continue;

            Undo.RecordObject(premium, "Sincronizar visual da Put Area");
            Undo.RecordObject(guard, "Registrar visual da Put Area");

            if (approvedStandVisual != null)
                CopyVisualSettings(approvedStandVisual, premium);

            premium.EnsureEditableHierarchy(true);

            if (approvedStandVisual != null)
                CopyActualVisualHierarchyReadOnly(approvedStandVisual, premium);

            guard.controller = controller;
            guard.premiumPrompt = premium;
            guard.premiumVisualSynchronizedFromStand = true;
            guard.ensurePremiumPromptAtRuntime = true;
            guard.restorePlacedNewspaperRenderers = true;

            prompt.useCircularPromptTransformAsSource = true;
            prompt.useGeneratedChildTransformsAsSource = true;
            prompt.useGeneratedGraphicStylesAsSource = true;
            prompt.useInstructionTransformAsSource = true;
            prompt.useInstructionGraphicAsSource = true;

            EditorUtility.SetDirty(premium);
            EditorUtility.SetDirty(guard);
            EditorUtility.SetDirty(prompt);
            EditorUtility.SetDirty(controller);
            EditorSceneManager.MarkSceneDirty(scene);
            synchronizedCount++;
        }

        if (logResult || installedCount > 0 || synchronizedCount > 0)
        {
            Debug.Log(
                "[NewspaperPremiumKey] Newspaper_Stand mantido somente leitura. " +
                "Componentes instalados na Put_Area: " + installedCount +
                ", prompts sincronizados: " + synchronizedCount +
                ". Confira e use Ctrl+S para salvar a cena."
            );
        }
    }

    private static NewspaperPromptPremiumKeyVisual ResolveApprovedStandVisual(
        NewspaperWorldPromptVisual[] prompts)
    {
        for (int i = 0; i < prompts.Length; i++)
        {
            NewspaperWorldPromptVisual prompt = prompts[i];
            if (prompt == null || prompt.name != "Newspaper_InteractionPrompt")
                continue;

            NewspaperPromptPremiumKeyVisual premium =
                prompt.GetComponent<NewspaperPromptPremiumKeyVisual>();

            if (premium == null)
                continue;

            if (prompt.transform.Find("CircularPrompt/PremiumKeyVisual") == null)
                continue;

            return premium;
        }

        return null;
    }

    private static void CopyVisualSettings(
        NewspaperPromptPremiumKeyVisual source,
        NewspaperPromptPremiumKeyVisual target)
    {
        if (source == null || target == null)
            return;

        target.visualEnabled = source.visualEnabled;
        target.previewInEditMode = source.previewInEditMode;
        target.animateInPlayMode = source.animateInPlayMode;
        target.animateInEditMode = source.animateInEditMode;
        target.disableLegacyCenterDisc = source.disableLegacyCenterDisc;
        target.useChildTransformsAsSource = true;
        target.useChildColorsAsSource = true;
        target.useCenterTextStyleAsSource = true;

        target.keyDiameter = source.keyDiameter;
        target.glowDiameter = source.glowDiameter;
        target.outerRingThickness = source.outerRingThickness;
        target.accentRingThickness = source.accentRingThickness;
        target.sparkleSize = source.sparkleSize;
        target.sparkleOrbitRadius = source.sparkleOrbitRadius;

        target.glowColor = source.glowColor;
        target.outerRingColor = source.outerRingColor;
        target.accentRingColor = source.accentRingColor;
        target.centerColor = source.centerColor;
        target.highlightColor = source.highlightColor;
        target.sparklePrimaryColor = source.sparklePrimaryColor;
        target.sparkleSecondaryColor = source.sparkleSecondaryColor;
        target.sparkleTertiaryColor = source.sparkleTertiaryColor;
        target.centerTextColor = source.centerTextColor;
        target.centerTextOutlineColor = source.centerTextOutlineColor;

        target.glowScalePulse = source.glowScalePulse;
        target.glowAlphaPulse = source.glowAlphaPulse;
        target.glowPulseSpeed = source.glowPulseSpeed;
        target.rotateOrbitEffects = source.rotateOrbitEffects;
        target.orbitRotationSpeed = source.orbitRotationSpeed;
        target.accentAlphaPulse = source.accentAlphaPulse;
        target.accentPulseSpeed = source.accentPulseSpeed;
        target.animateSparkles = source.animateSparkles;
        target.sparkleAlphaPulse = source.sparkleAlphaPulse;
        target.sparklePulseSpeed = source.sparklePulseSpeed;
        target.centerFontSize = source.centerFontSize;
        target.centerOutlineWidth = source.centerOutlineWidth;
    }

    private static void CopyActualVisualHierarchyReadOnly(
        NewspaperPromptPremiumKeyVisual source,
        NewspaperPromptPremiumKeyVisual target)
    {
        if (source == null || target == null)
            return;

        // Resolve somente o destino. Nenhum método, Undo, SetDirty ou alteração é
        // executado no Newspaper_Stand aprovado.
        target.RepairReferences();

        RectTransform sourceCircular = source.transform.Find("CircularPrompt") as RectTransform;
        RectTransform sourcePremium = sourceCircular != null
            ? sourceCircular.Find("PremiumKeyVisual") as RectTransform
            : null;

        if (sourcePremium == null)
            return;

        RectTransform sourceGlowMotion = sourcePremium.Find("GlowMotion") as RectTransform;
        RectTransform sourceOrbitMotion = sourcePremium.Find("OrbitMotion") as RectTransform;
        RectTransform sourceStaticLayer = sourcePremium.Find("StaticLayer") as RectTransform;

        CopyRect(sourcePremium, target.premiumRoot);
        CopyRect(sourceGlowMotion, target.glowMotion);
        CopyRect(sourceOrbitMotion, target.orbitMotion);
        CopyRect(sourceStaticLayer, target.staticLayer);

        CopyShape(FindShape(sourceGlowMotion, "DynamicGlow"), target.glowBack);
        CopyShape(FindShape(sourceOrbitMotion, "OuterRing"), target.outerRing);
        CopyShape(FindShape(sourceOrbitMotion, "AccentRing"), target.accentRing);
        CopyShape(FindShape(sourceOrbitMotion, "SparkleTop"), target.sparkleTop);
        CopyShape(FindShape(sourceOrbitMotion, "SparkleLeft"), target.sparkleLeft);
        CopyShape(FindShape(sourceOrbitMotion, "SparkleRight"), target.sparkleRight);
        CopyShape(FindShape(sourceStaticLayer, "CenterCircle"), target.premiumCenterDisc);
        CopyShape(FindShape(sourceStaticLayer, "CenterHighlight"), target.centerHighlight);

        TextMeshProUGUI sourceText = sourceCircular.Find("CenterText") != null
            ? sourceCircular.Find("CenterText").GetComponent<TextMeshProUGUI>()
            : null;
        CopyTextStyle(sourceText, target.centerText);
    }

    private static NewspaperPromptShapeGraphic FindShape(
        Transform parent,
        string childName)
    {
        if (parent == null)
            return null;

        Transform child = parent.Find(childName);
        return child != null
            ? child.GetComponent<NewspaperPromptShapeGraphic>()
            : null;
    }

    private static void CopyRect(RectTransform source, RectTransform target)
    {
        if (source == null || target == null)
            return;

        Undo.RecordObject(target, "Copiar layout premium da Put Area");
        target.anchorMin = source.anchorMin;
        target.anchorMax = source.anchorMax;
        target.pivot = source.pivot;
        target.anchoredPosition3D = source.anchoredPosition3D;
        target.sizeDelta = source.sizeDelta;
        target.localEulerAngles = source.localEulerAngles;
        target.localScale = source.localScale;
    }

    private static void CopyShape(
        NewspaperPromptShapeGraphic source,
        NewspaperPromptShapeGraphic target)
    {
        if (source == null || target == null)
            return;

        CopyRect(source.rectTransform, target.rectTransform);
        Undo.RecordObject(target, "Copiar forma premium da Put Area");
        target.shape = source.shape;
        target.segments = source.segments;
        target.ringThickness = source.ringThickness;
        target.geometryRotation = source.geometryRotation;
        target.color = source.color;
        target.material = source.material;
        target.raycastTarget = false;
        target.SetVerticesDirty();
        target.SetMaterialDirty();
    }

    private static void CopyTextStyle(TextMeshProUGUI source, TextMeshProUGUI target)
    {
        if (source == null || target == null)
            return;

        CopyRect(source.rectTransform, target.rectTransform);
        Undo.RecordObject(target, "Copiar estilo do E para Put Area");
        target.font = source.font;
        target.fontSharedMaterial = source.fontSharedMaterial;
        target.fontSize = source.fontSize;
        target.fontStyle = source.fontStyle;
        target.color = source.color;
        target.outlineColor = source.outlineColor;
        target.outlineWidth = source.outlineWidth;
        target.alignment = source.alignment;
        target.characterSpacing = source.characterSpacing;
        target.wordSpacing = source.wordSpacing;
        target.lineSpacing = source.lineSpacing;
        target.paragraphSpacing = source.paragraphSpacing;
        target.raycastTarget = false;
        target.textWrappingMode = TextWrappingModes.NoWrap;
    }
}
#endif
