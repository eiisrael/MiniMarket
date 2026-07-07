using UnityEngine;

/// <summary>
/// Marcador visual para a area de entrada da BuyScene.
/// Use este script no mesmo objeto que possui o BuySceneEntryTrigger.
/// Diferente do Gizmos, este desenho aparece tambem na Game View durante o jogo.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Collider))]
public class BuySceneEntryAreaVisibleMarker : MonoBehaviour
{
    [Header("Visual")]
    public bool mostrarMarcador = true;
    public bool mostrarXCentral = true;

    [Tooltip("Cor normal da area de entrada.")]
    public Color corNormal = new Color(1f, 0.92f, 0f, 1f);

    [Tooltip("Cor usada quando o player esta dentro da area.")]
    public Color corPlayerDentro = new Color(0.1f, 1f, 0.1f, 1f);

    [Min(0.01f)]
    public float larguraLinha = 0.08f;

    [Tooltip("Altura acima do topo do collider para evitar que a linha fique escondida no chao/calcada.")]
    public float alturaAcimaDoCollider = 0.05f;

    [Header("Deteccao do Player para destaque")]
    public string tagDoPlayer = "Player";
    public bool aceitarCharacterController = true;
    public bool aceitarScriptPlayerMove = true;

    [Header("Atualizacao")]
    public bool atualizarEmTempoReal = true;

    private Collider areaCollider;
    private LineRenderer linhaBorda;
    private LineRenderer linhaDiagonalA;
    private LineRenderer linhaDiagonalB;
    private bool playerDentro;

    private void Awake()
    {
        areaCollider = GetComponent<Collider>();
        PrepararCollider();
        CriarRenderizadores();
        AtualizarVisualCompleto();
    }

    private void OnEnable()
    {
        AtualizarVisualCompleto();
    }

    private void LateUpdate()
    {
        if (atualizarEmTempoReal)
            AtualizarVisualCompleto();
    }

    private void OnValidate()
    {
        if (larguraLinha < 0.01f)
            larguraLinha = 0.01f;

        if (!Application.isPlaying)
            return;

        areaCollider = GetComponent<Collider>();
        PrepararCollider();
        CriarRenderizadores();
        AtualizarVisualCompleto();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!EhPlayer(other))
            return;

        playerDentro = true;
        AtualizarCor();
    }

    private void OnTriggerExit(Collider other)
    {
        if (!EhPlayer(other))
            return;

        playerDentro = false;
        AtualizarCor();
    }

    private void PrepararCollider()
    {
        if (areaCollider == null)
            return;

        areaCollider.isTrigger = true;
    }

    private void CriarRenderizadores()
    {
        if (linhaBorda == null)
            linhaBorda = CriarLinha("BuyScene_Entrada_Borda", true, 5);

        if (linhaDiagonalA == null)
            linhaDiagonalA = CriarLinha("BuyScene_Entrada_Diagonal_A", false, 2);

        if (linhaDiagonalB == null)
            linhaDiagonalB = CriarLinha("BuyScene_Entrada_Diagonal_B", false, 2);
    }

    private LineRenderer CriarLinha(string nome, bool loop, int quantidadePontos)
    {
        Transform existente = transform.Find(nome);

        GameObject objetoLinha = existente != null ? existente.gameObject : new GameObject(nome);
        objetoLinha.transform.SetParent(transform, false);
        objetoLinha.transform.localPosition = Vector3.zero;
        objetoLinha.transform.localRotation = Quaternion.identity;
        objetoLinha.transform.localScale = Vector3.one;

        LineRenderer linha = objetoLinha.GetComponent<LineRenderer>();

        if (linha == null)
            linha = objetoLinha.AddComponent<LineRenderer>();

        linha.useWorldSpace = true;
        linha.loop = loop;
        linha.positionCount = quantidadePontos;
        linha.widthMultiplier = larguraLinha;
        linha.alignment = LineAlignment.View;
        linha.textureMode = LineTextureMode.Stretch;
        linha.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        linha.receiveShadows = false;
        linha.allowOcclusionWhenDynamic = false;

        Material material = linha.sharedMaterial;

        if (material == null)
        {
            Shader shader = Shader.Find("Sprites/Default");

            if (shader == null)
                shader = Shader.Find("Universal Render Pipeline/Unlit");

            material = new Material(shader);
            linha.sharedMaterial = material;
        }

        return linha;
    }

    private void AtualizarVisualCompleto()
    {
        CriarRenderizadores();
        AtualizarPosicoes();
        AtualizarCor();
        AtualizarAtivo();
    }

    private void AtualizarAtivo()
    {
        if (linhaBorda != null)
            linhaBorda.gameObject.SetActive(mostrarMarcador);

        if (linhaDiagonalA != null)
            linhaDiagonalA.gameObject.SetActive(mostrarMarcador && mostrarXCentral);

        if (linhaDiagonalB != null)
            linhaDiagonalB.gameObject.SetActive(mostrarMarcador && mostrarXCentral);
    }

    private void AtualizarCor()
    {
        Color corAtual = playerDentro ? corPlayerDentro : corNormal;

        AplicarCor(linhaBorda, corAtual);
        AplicarCor(linhaDiagonalA, corAtual);
        AplicarCor(linhaDiagonalB, corAtual);
    }

    private void AplicarCor(LineRenderer linha, Color cor)
    {
        if (linha == null)
            return;

        linha.widthMultiplier = larguraLinha;
        linha.startColor = cor;
        linha.endColor = cor;

        if (linha.sharedMaterial != null && linha.sharedMaterial.HasProperty("_Color"))
            linha.sharedMaterial.color = cor;
    }

    private void AtualizarPosicoes()
    {
        if (areaCollider == null)
            areaCollider = GetComponent<Collider>();

        if (areaCollider == null)
            return;

        Vector3 p0;
        Vector3 p1;
        Vector3 p2;
        Vector3 p3;
        CalcularCantosSuperiores(out p0, out p1, out p2, out p3);

        if (linhaBorda != null)
        {
            linhaBorda.positionCount = 5;
            linhaBorda.SetPosition(0, p0);
            linhaBorda.SetPosition(1, p1);
            linhaBorda.SetPosition(2, p2);
            linhaBorda.SetPosition(3, p3);
            linhaBorda.SetPosition(4, p0);
        }

        if (linhaDiagonalA != null)
        {
            linhaDiagonalA.positionCount = 2;
            linhaDiagonalA.SetPosition(0, p0);
            linhaDiagonalA.SetPosition(1, p2);
        }

        if (linhaDiagonalB != null)
        {
            linhaDiagonalB.positionCount = 2;
            linhaDiagonalB.SetPosition(0, p1);
            linhaDiagonalB.SetPosition(1, p3);
        }
    }

    private void CalcularCantosSuperiores(out Vector3 p0, out Vector3 p1, out Vector3 p2, out Vector3 p3)
    {
        BoxCollider box = areaCollider as BoxCollider;

        if (box != null)
        {
            Vector3 centro = box.center;
            Vector3 metade = box.size * 0.5f;
            float y = centro.y + metade.y + alturaAcimaDoCollider;

            p0 = transform.TransformPoint(new Vector3(centro.x - metade.x, y, centro.z - metade.z));
            p1 = transform.TransformPoint(new Vector3(centro.x - metade.x, y, centro.z + metade.z));
            p2 = transform.TransformPoint(new Vector3(centro.x + metade.x, y, centro.z + metade.z));
            p3 = transform.TransformPoint(new Vector3(centro.x + metade.x, y, centro.z - metade.z));
            return;
        }

        Bounds b = areaCollider.bounds;
        float topo = b.max.y + alturaAcimaDoCollider;

        p0 = new Vector3(b.min.x, topo, b.min.z);
        p1 = new Vector3(b.min.x, topo, b.max.z);
        p2 = new Vector3(b.max.x, topo, b.max.z);
        p3 = new Vector3(b.max.x, topo, b.min.z);
    }

    private bool EhPlayer(Collider other)
    {
        if (other == null)
            return false;

        if (!string.IsNullOrEmpty(tagDoPlayer) && other.CompareTag(tagDoPlayer))
            return true;

        if (aceitarCharacterController && other.GetComponentInParent<CharacterController>() != null)
            return true;

        if (aceitarScriptPlayerMove && TemComponenteComNome(other.transform, "PlayerMove"))
            return true;

        return false;
    }

    private bool TemComponenteComNome(Transform origem, string nomeDoTipo)
    {
        if (origem == null || string.IsNullOrEmpty(nomeDoTipo))
            return false;

        MonoBehaviour[] componentes = origem.GetComponentsInParent<MonoBehaviour>(true);

        for (int i = 0; i < componentes.Length; i++)
        {
            MonoBehaviour componente = componentes[i];

            if (componente != null && componente.GetType().Name == nomeDoTipo)
                return true;
        }

        return false;
    }
}
