using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Graphic procedural para círculos e anéis de UI.
/// Não depende de sprite, textura ou material gerado em runtime e permanece totalmente
/// editável no Inspector. Foi criado para evitar o aspecto quadrado do botão de interação.
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
[RequireComponent(typeof(CanvasRenderer))]
public sealed class NewspaperPromptShapeGraphic : MaskableGraphic
{
    public enum ShapeMode
    {
        FilledCircle,
        Ring
    }

    [Header("Forma")]
    public ShapeMode shape = ShapeMode.FilledCircle;

    [Range(12, 128)]
    public int segments = 64;

    [Tooltip("Espessura relativa do anel. Usada apenas no modo Ring.")]
    [Range(0.02f, 0.48f)]
    public float ringThickness = 0.14f;

    [Tooltip("Gira a geometria sem alterar o Transform.")]
    public float geometryRotation;

    protected override void OnPopulateMesh(VertexHelper vertexHelper)
    {
        vertexHelper.Clear();

        Rect rect = rectTransform.rect;
        float radius = Mathf.Max(0.01f, Mathf.Min(rect.width, rect.height) * 0.5f);
        Vector2 center = rect.center;
        int safeSegments = Mathf.Clamp(segments, 12, 128);
        float rotationRadians = geometryRotation * Mathf.Deg2Rad;
        Color32 vertexColor = color;

        if (shape == ShapeMode.Ring)
        {
            PopulateRing(
                vertexHelper,
                center,
                radius,
                safeSegments,
                rotationRadians,
                vertexColor
            );
            return;
        }

        PopulateFilledCircle(
            vertexHelper,
            center,
            radius,
            safeSegments,
            rotationRadians,
            vertexColor
        );
    }

    private void PopulateFilledCircle(
        VertexHelper vertexHelper,
        Vector2 center,
        float radius,
        int safeSegments,
        float rotationRadians,
        Color32 vertexColor)
    {
        vertexHelper.AddVert(center, vertexColor, new Vector2(0.5f, 0.5f));

        for (int i = 0; i <= safeSegments; i++)
        {
            float normalized = i / (float)safeSegments;
            float angle = normalized * Mathf.PI * 2f + rotationRadians;
            Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            Vector2 position = center + direction * radius;
            Vector2 uv = direction * 0.5f + Vector2.one * 0.5f;
            vertexHelper.AddVert(position, vertexColor, uv);
        }

        for (int i = 0; i < safeSegments; i++)
            vertexHelper.AddTriangle(0, i + 1, i + 2);
    }

    private void PopulateRing(
        VertexHelper vertexHelper,
        Vector2 center,
        float radius,
        int safeSegments,
        float rotationRadians,
        Color32 vertexColor)
    {
        float innerRadius = radius * (1f - Mathf.Clamp(ringThickness, 0.02f, 0.48f));

        for (int i = 0; i <= safeSegments; i++)
        {
            float normalized = i / (float)safeSegments;
            float angle = normalized * Mathf.PI * 2f + rotationRadians;
            Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));

            Vector2 outerPosition = center + direction * radius;
            Vector2 innerPosition = center + direction * innerRadius;

            vertexHelper.AddVert(
                outerPosition,
                vertexColor,
                direction * 0.5f + Vector2.one * 0.5f
            );
            vertexHelper.AddVert(
                innerPosition,
                vertexColor,
                direction * 0.35f + Vector2.one * 0.5f
            );
        }

        for (int i = 0; i < safeSegments; i++)
        {
            int outerA = i * 2;
            int innerA = outerA + 1;
            int outerB = outerA + 2;
            int innerB = outerA + 3;

            vertexHelper.AddTriangle(outerA, outerB, innerB);
            vertexHelper.AddTriangle(outerA, innerB, innerA);
        }
    }

    protected override void OnValidate()
    {
        base.OnValidate();
        segments = Mathf.Clamp(segments, 12, 128);
        ringThickness = Mathf.Clamp(ringThickness, 0.02f, 0.48f);
        SetVerticesDirty();
        SetMaterialDirty();
    }
}
