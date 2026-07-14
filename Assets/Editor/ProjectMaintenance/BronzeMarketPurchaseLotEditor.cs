#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(BronzeMarketPurchaseLot))]
public sealed class BronzeMarketPurchaseLotEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        DrawDefaultInspector();
        serializedObject.ApplyModifiedProperties();

        BronzeMarketPurchaseLot lot = (BronzeMarketPurchaseLot)target;

        EditorGUILayout.Space(10f);
        EditorGUILayout.HelpBox(
            "Cada raiz Bronze_Market deve possuir ID, Buy_Area, terreno, controlador e painel " +
            "de status próprios. Ao duplicar a raiz, o Editor gera outro ID automaticamente.",
            MessageType.Info
        );

        if (GUILayout.Button("Preparar/Reparar esta Loja Bronze"))
        {
            Selection.activeGameObject = lot.gameObject;
            BronzeMarketPurchaseLotSetup.PrepararSelecionada();
        }

        if (GUILayout.Button("Gerar Novo ID para esta Loja"))
        {
            Selection.activeGameObject = lot.gameObject;
            BronzeMarketPurchaseLotSetup.GenerateNewIdForSelected();
        }

        if (GUILayout.Button("Reconciliar Controlador e Visuais"))
        {
            Selection.activeGameObject = lot.gameObject;
            BronzeMarketLocalControllerReconciler.RunAndSave();
        }

        if (GUILayout.Button("Aplicar Vínculos sem Recriar Layout"))
        {
            Undo.RecordObject(lot, "Aplicar vínculos da Bronze_Market");
            lot.AplicarVinculosRuntime();
            if (lot.visualStatus != null)
                lot.visualStatus.AtualizarVisualImediato();
            EditorUtility.SetDirty(lot);
        }

        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField("Fluxo para criar outra loja", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("1. Duplique a raiz Bronze_Market.");
        EditorGUILayout.LabelField("2. Mova a cópia para outro local.");
        EditorGUILayout.LabelField("3. Salve a cena com Ctrl+S.");
    }
}
#endif
