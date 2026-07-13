#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

[CustomEditor(typeof(MiniMarketEnergyProgressBar))]
public sealed class MiniMarketEnergyProgressBarEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        EditorGUI.BeginChangeCheck();
        DrawDefaultInspector();
        bool changed = EditorGUI.EndChangeCheck();
        serializedObject.ApplyModifiedProperties();

        MiniMarketEnergyProgressBar bar = (MiniMarketEnergyProgressBar)target;

        EditorGUILayout.Space(8f);
        EditorGUILayout.HelpBox(
            "Energy permanece estático. Somente EnergyProgressFill aumenta e diminui. " +
            "A cor e a área podem ser editadas aqui fora do Play Mode.",
            MessageType.Info
        );

        if (GUILayout.Button("Aplicar cor e área no Editor"))
        {
            AplicarPreview(bar, true);
        }

        if (GUILayout.Button("Recriar barra verde persistente"))
        {
            Undo.RegisterFullObjectHierarchyUndo(bar.gameObject, "Recriar barra verde");
            bar.RecriarBarraInterna();
            AplicarPreview(bar, true);
        }

        if (changed && !Application.isPlaying)
            AplicarPreview(bar, false);
    }

    private static void AplicarPreview(MiniMarketEnergyProgressBar bar, bool selecionarFill)
    {
        if (bar == null)
            return;

        if (bar.imagemOriginal == null)
            bar.imagemOriginal = bar.GetComponent<Image>();

        if (bar.areaPreenchimento == null || bar.preenchimentoVerde == null)
            bar.RecriarBarraInterna();

        if (bar.imagemOriginal != null)
        {
            Undo.RecordObject(bar.imagemOriginal, "Atualizar imagem Energy");
            bar.imagemOriginal.type = Image.Type.Simple;
            bar.imagemOriginal.fillAmount = 1f;
            bar.imagemOriginal.raycastTarget = false;
            EditorUtility.SetDirty(bar.imagemOriginal);
        }

        if (bar.areaPreenchimento != null)
        {
            Undo.RecordObject(bar.areaPreenchimento, "Atualizar área da energia");
            bar.areaPreenchimento.anchorMin = bar.ancoraMinima;
            bar.areaPreenchimento.anchorMax = bar.ancoraMaxima;
            bar.areaPreenchimento.pivot = new Vector2(0f, 0.5f);
            bar.areaPreenchimento.offsetMin = Vector2.zero;
            bar.areaPreenchimento.offsetMax = Vector2.zero;
            EditorUtility.SetDirty(bar.areaPreenchimento);
        }

        if (bar.preenchimentoVerde != null)
        {
            Undo.RecordObject(bar.preenchimentoVerde, "Atualizar cor da energia");
            bar.preenchimentoVerde.sprite = null;
            bar.preenchimentoVerde.type = Image.Type.Simple;
            bar.preenchimentoVerde.color = bar.corBarra;
            bar.preenchimentoVerde.raycastTarget = false;
            bar.preenchimentoVerde.preserveAspect = false;

            RectTransform fillRect = bar.preenchimentoVerde.rectTransform;
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.pivot = new Vector2(0f, 0.5f);
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;

            bar.preenchimentoVerde.enabled = true;
            EditorUtility.SetDirty(bar.preenchimentoVerde);

            if (selecionarFill)
                Selection.activeGameObject = bar.preenchimentoVerde.gameObject;
        }

        EditorUtility.SetDirty(bar);
        if (bar.gameObject.scene.IsValid())
            EditorSceneManager.MarkSceneDirty(bar.gameObject.scene);

        SceneView.RepaintAll();
    }
}
#endif
