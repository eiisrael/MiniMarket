using UnityEngine;

/// <summary>
/// Perfil opcional de fisica para objetos que podem ser pegos.
/// Coloque este componente em produtos, caixas e objetos especiais quando quiser configurar peso/limites individualmente.
/// Se nao existir, PlayerObjectGrabberHardcore usa os valores globais do Inspector.
/// </summary>
[DisallowMultipleComponent]
public class ObjectPhysicsProfile : MonoBehaviour
{
    [Header("Permissao")]
    public bool podeSerPego = true;

    [Tooltip("Se ligado, o objeto ignora os limites globais de massa/tamanho do grabber.")]
    public bool sobrescreverLimitesGlobais;

    [Header("Massa / Peso")]
    [Tooltip("Massa maxima permitida para este objeto ser pego. So vale se sobrescreverLimitesGlobais estiver ligado.")]
    [Min(0.01f)] public float massaMaximaParaPegar = 25f;

    [Tooltip("Objeto sem Rigidbody usa esta massa virtual ate receber Rigidbody automaticamente.")]
    [Min(0.01f)] public float massaVirtual = 1f;

    [Header("Tamanho")]
    [Min(0.05f)] public float alturaMaximaParaPegar = 2.2f;
    [Min(0.05f)] public float larguraMaximaParaPegar = 2.4f;
    [Min(0.05f)] public float comprimentoMaximoParaPegar = 2.4f;

    [Header("Segurando")]
    [Tooltip("Multiplica a forca de segurar apenas deste objeto. Maior = mais firme.")]
    [Min(0.05f)] public float multiplicadorForcaSegurar = 1f;

    [Tooltip("Multiplica a distancia de segurar apenas deste objeto.")]
    [Min(0.1f)] public float multiplicadorDistanciaSegurar = 1f;

    [Tooltip("Se ligado, o objeto usa gravidade enquanto esta sendo segurado. Mais realista, mas objetos muito pesados podem ceder um pouco.")]
    public bool usarGravidadeEnquantoSegura = true;

    [Header("Soltar / Colocar")]
    [Tooltip("Quanto da velocidade fisica fica ao soltar. 1 = preserva tudo, 0 = para seco.")]
    [Range(0f, 1.5f)] public float multiplicadorVelocidadeAoSoltar = 0.65f;

    [Tooltip("Quanto da rotacao fisica fica ao soltar. 1 = preserva tudo, 0 = para seco.")]
    [Range(0f, 1.5f)] public float multiplicadorRotacaoAoSoltar = 0.65f;
}
