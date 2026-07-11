#if UNITY_EDITOR
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>
/// Repara a ligação entre CameraRelativeMovement e o Animator real do personagem.
/// Não cria nem troca animações. Apenas reutiliza o Animator Controller já configurado
/// no modelo e ativa a compatibilidade de parâmetros do sistema de movimento.
/// </summary>
public static class PlayerAnimatorRepair
{
    [MenuItem("Tools/Player System/Repair Player Animator", priority = 3)]
    public static void RepairFromMenu()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("[PlayerAnimatorRepair] Saia do Play Mode antes de reparar o Animator.");
            return;
        }

        CameraRelativeMovement movement = FindMovement();
        if (movement == null)
        {
            Debug.LogError(
                "[PlayerAnimatorRepair] CameraRelativeMovement não encontrado. " +
                "Selecione o objeto principal do personagem e execute novamente."
            );
            return;
        }

        Animator animator = FindBestAnimator(movement.gameObject);
        if (animator == null)
        {
            Debug.LogError(
                "[PlayerAnimatorRepair] Nenhum componente Animator foi encontrado em " +
                movement.name + " ou nos seus filhos.",
                movement
            );
            return;
        }

        Undo.RecordObject(movement, "Repair Player Animator");
        Undo.RecordObject(animator, "Repair Player Animator");

        movement.animator = animator;
        animator.enabled = true;
        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

        bool ready = movement.RefreshAnimatorConfiguration();

        EditorUtility.SetDirty(movement);
        EditorUtility.SetDirty(animator);

        Scene scene = movement.gameObject.scene;
        if (scene.IsValid() && !string.IsNullOrEmpty(scene.path))
        {
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        AssetDatabase.SaveAssets();

        RuntimeAnimatorController controller = animator.runtimeAnimatorController;
        string controllerName = controller != null ? controller.name : "NENHUM";
        string parameters = BuildParameterList(animator);

        if (ready)
        {
            Debug.Log(
                "[PlayerAnimatorRepair] Animator reparado com sucesso. " +
                "Player=" + movement.name +
                ", Animator=" + animator.name +
                ", Controller=" + controllerName +
                ", Parâmetros=" + parameters + ".",
                movement
            );
        }
        else
        {
            Debug.LogWarning(
                "[PlayerAnimatorRepair] A referência foi reparada, mas o Animator Controller precisa ser verificado. " +
                "Animator=" + animator.name +
                ", Controller=" + controllerName +
                ", Parâmetros=" + parameters + ".",
                animator
            );
        }
    }

    [MenuItem("Tools/Player System/Print Animator Diagnostics", priority = 4)]
    public static void PrintDiagnostics()
    {
        CameraRelativeMovement movement = FindMovement();
        if (movement == null)
        {
            Debug.LogError("[PlayerAnimatorRepair] CameraRelativeMovement não encontrado.");
            return;
        }

        Animator animator = movement.animator != null
            ? movement.animator
            : FindBestAnimator(movement.gameObject);

        if (animator == null)
        {
            Debug.LogError("[PlayerAnimatorRepair] Animator não encontrado.", movement);
            return;
        }

        RuntimeAnimatorController controller = animator.runtimeAnimatorController;
        Debug.Log(
            "[PlayerAnimatorRepair] Diagnóstico: " +
            "Player=" + movement.name +
            ", Animator=" + animator.name +
            ", Enabled=" + animator.enabled +
            ", RootMotion=" + animator.applyRootMotion +
            ", Avatar=" + (animator.avatar != null ? animator.avatar.name : "null") +
            ", Controller=" + (controller != null ? controller.name : "null") +
            ", Parâmetros=" + BuildParameterList(animator) + ".",
            animator
        );
    }

    private static CameraRelativeMovement FindMovement()
    {
        GameObject selected = Selection.activeGameObject;
        if (selected != null)
        {
            CameraRelativeMovement selectedMovement = selected.GetComponentInParent<CameraRelativeMovement>();
            if (selectedMovement == null)
                selectedMovement = selected.GetComponentInChildren<CameraRelativeMovement>(true);

            if (selectedMovement != null)
                return selectedMovement;
        }

        return Object.FindAnyObjectByType<CameraRelativeMovement>(FindObjectsInactive.Include);
    }

    private static Animator FindBestAnimator(GameObject player)
    {
        if (player == null)
            return null;

        Animator[] animators = player.GetComponentsInChildren<Animator>(true);
        Animator fallback = null;

        for (int i = 0; i < animators.Length; i++)
        {
            Animator candidate = animators[i];
            if (candidate == null)
                continue;

            if (fallback == null)
                fallback = candidate;

            if (candidate.runtimeAnimatorController != null && candidate.avatar != null)
                return candidate;
        }

        for (int i = 0; i < animators.Length; i++)
        {
            Animator candidate = animators[i];
            if (candidate != null && candidate.runtimeAnimatorController != null)
                return candidate;
        }

        return fallback;
    }

    private static string BuildParameterList(Animator animator)
    {
        if (animator == null || animator.runtimeAnimatorController == null)
            return "nenhum";

        AnimatorControllerParameter[] parameters = animator.parameters;
        if (parameters == null || parameters.Length == 0)
            return "nenhum";

        StringBuilder builder = new StringBuilder();
        for (int i = 0; i < parameters.Length; i++)
        {
            if (i > 0)
                builder.Append(", ");

            builder.Append(parameters[i].name);
            builder.Append("[");
            builder.Append(parameters[i].type);
            builder.Append("]");
        }

        return builder.ToString();
    }
}
#endif
