using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// MiniMarket Camera V2 - First Person CAM.
///
/// Use no GameObject: FirstPersonCAM.
/// Objetivo: câmera em primeira pessoa fluida, sem efeitos de movimentação/bob,
/// com mira, zoom e integração opcional com GetItemV2.
///
/// Regras do V2:
/// - Este script é a autoridade da primeira pessoa.
/// - Sem head bob, sem balanço, sem empurrões visuais.
/// - Todos os campos do Inspector podem ser editados em runtime.
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(20100)]
public class Camera1Person : MonoBehaviour
{
    [Header("Referências")]
    public Camera camera1Person;
    public Transform corpoPersonagem;
    public Transform pontoPOV;
    public GetItemV2 getItem;
    public Image miraImagem;
    public CanvasGroup miraCanvasGroup;

    [Header("Ativação")]
    public bool cameraAtiva;
    public bool controlarCameraComponent = true;
    public bool travarCursorAoAtivar = true;
    public bool aceitarInputMouse = true;
    public bool usarUnscaledTime = false;
    public bool ativarGetItemJunto = true;

    [Header("Input")]
    public string mouseXAxis = "Mouse X";
    public string mouseYAxis = "Mouse Y";
    [Range(0, 2)] public int botaoZoom = 1;
    public bool zoomEnquantoSegura = true;
    public bool inverterY = false;
    [Min(0f)] public float deadZoneMouse = 0.0008f;

    [Header("Sensibilidade")]
    [Min(1f)] public float sensibilidadeX = 175f;
    [Min(1f)] public float sensibilidadeY = 125f;
    [Range(0.05f, 1f)] public float multiplicadorSensibilidadeZoom = 0.72f;

    [Header("Ângulos")]
    [Range(-89f, 89f)] public float minPitch = -72f;
    [Range(-89f, 89f)] public float maxPitch = 78f;
    public float yawInicial;
    public float pitchInicial;
    public bool iniciarYawPeloCorpo = true;

    [Header("Posição POV")]
    public Vector3 offsetLocalSemPOV = new Vector3(0f, 1.68f, 0.08f);
    public Vector3 ajusteLocalPOV = Vector3.zero;
    public bool seguirPOVSemSuavizacao = true;
    [Min(0f)] public float tempoSuavizacaoPosicao = 0.02f;

    [Header("Rotação do Corpo")]
    public bool rotacionarCorpoComCamera = true;
    public bool rotacaoCorpoInstantanea = false;
    [Min(0f)] public float velocidadeRotacaoCorpo = 18f;

    [Header("Zoom / Mira")]
    public bool usarZoom = true;
    [Range(1f, 179f)] public float fovNormal = 60f;
    [Range(1f, 179f)] public float fovZoom = 42f;
    [Min(0f)] public float velocidadeFov = 16f;
    public bool exibirMira = true;
    [Min(0f)] public float velocidadeFadeMira = 20f;

    [Header("Sem efeitos de movimento")]
    public bool desativarHeadBob = true;
    public bool desativarSway = true;
    public bool estabilizarContraAnimacao = true;
    [Min(0f)] public float toleranciaTremorVertical = 0.01f;

    [Header("Debug")]
    public bool logarEventos;

    private Vector3 velocidadePosicao;
    private Vector3 ultimaPosicaoEstavel;
    private bool possuiPosicaoEstavel;
    private float yaw;
    private float pitch;
    private bool inicializado;

    public float YawAtual => yaw;
    public float PitchAtual => pitch;
    public bool ZoomAtivo => cameraAtiva && usarZoom && zoomEnquantoSegura && Input.GetMouseButton(botaoZoom);
    public Camera Camera => camera1Person;

    private void Reset()
    {
        camera1Person = GetComponent<Camera>();
        getItem = GetComponent<GetItemV2>();
    }

    private void Awake()
    {
        ResolverReferencias();
        InicializarEstado();
    }

    private void OnEnable()
    {
        ResolverReferencias();
        InicializarEstado();
        AplicarAtivacaoCamera();
    }

    private void OnDisable()
    {
        if (controlarCameraComponent && camera1Person != null)
            camera1Person.enabled = false;

        if (ativarGetItemJunto && getItem != null)
            getItem.enabled = false;
    }

    private void LateUpdate()
    {
        if (!cameraAtiva)
        {
            AplicarAtivacaoCamera();
            AtualizarMira(false, DeltaTimeSeguro());
            return;
        }

        ResolverReferencias();
        AplicarAtivacaoCamera();

        float dt = DeltaTimeSeguro();
        LerMouse(dt);
        AtualizarTransform(dt);
        AtualizarFov(dt);
        AtualizarMira(true, dt);
    }

    public void SetAtiva(bool ativa)
    {
        cameraAtiva = ativa;
        AplicarAtivacaoCamera();

        if (ativa)
        {
            ResolverReferencias();
            SincronizarYawComCorpoSeNecessario();
            possuiPosicaoEstavel = false;
        }
    }

    public void DefinirCorpo(Transform novoCorpo)
    {
        corpoPersonagem = novoCorpo;
        SincronizarYawComCorpoSeNecessario();
    }

    public void DefinirPOV(Transform novoPOV)
    {
        pontoPOV = novoPOV;
        possuiPosicaoEstavel = false;
    }

    public void DefinirAngulos(float novoYaw, float novoPitch)
    {
        yaw = novoYaw;
        pitch = Mathf.Clamp(novoPitch, minPitch, maxPitch);
    }

    private void ResolverReferencias()
    {
        if (camera1Person == null)
            camera1Person = GetComponent<Camera>();

        if (getItem == null)
            getItem = GetComponent<GetItemV2>();
    }

    private void InicializarEstado()
    {
        if (inicializado)
            return;

        yaw = iniciarYawPeloCorpo && corpoPersonagem != null ? corpoPersonagem.eulerAngles.y : yawInicial;
        pitch = Mathf.Clamp(pitchInicial, minPitch, maxPitch);
        inicializado = true;
    }

    private void SincronizarYawComCorpoSeNecessario()
    {
        if (iniciarYawPeloCorpo && corpoPersonagem != null)
            yaw = corpoPersonagem.eulerAngles.y;
    }

    private void AplicarAtivacaoCamera()
    {
        if (controlarCameraComponent && camera1Person != null)
            camera1Person.enabled = cameraAtiva;

        if (ativarGetItemJunto && getItem != null)
            getItem.enabled = cameraAtiva;

        if (cameraAtiva && travarCursorAoAtivar)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    private void LerMouse(float dt)
    {
        if (!aceitarInputMouse || Cursor.lockState != CursorLockMode.Locked)
            return;

        float mouseX = Input.GetAxisRaw(mouseXAxis);
        float mouseY = Input.GetAxisRaw(mouseYAxis);
        Vector2 input = new Vector2(mouseX, mouseY);

        if (input.sqrMagnitude <= deadZoneMouse * deadZoneMouse)
            return;

        float mult = ZoomAtivo ? multiplicadorSensibilidadeZoom : 1f;
        yaw += input.x * sensibilidadeX * mult * dt;
        pitch += (inverterY ? mouseY : -mouseY) * sensibilidadeY * mult * dt;
        pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
    }

    private void AtualizarTransform(float dt)
    {
        Vector3 posicaoAlvo = CalcularPosicaoPOV();

        if (seguirPOVSemSuavizacao || tempoSuavizacaoPosicao <= 0.0001f)
        {
            transform.position = posicaoAlvo;
            velocidadePosicao = Vector3.zero;
        }
        else
        {
            transform.position = Vector3.SmoothDamp(transform.position, posicaoAlvo, ref velocidadePosicao, tempoSuavizacaoPosicao, Mathf.Infinity, dt);
        }

        transform.rotation = Quaternion.Euler(pitch, yaw, 0f);

        if (rotacionarCorpoComCamera && corpoPersonagem != null)
        {
            Quaternion rotacaoCorpo = Quaternion.Euler(0f, yaw, 0f);
            corpoPersonagem.rotation = rotacaoCorpoInstantanea || velocidadeRotacaoCorpo <= 0.0001f
                ? rotacaoCorpo
                : Quaternion.Slerp(corpoPersonagem.rotation, rotacaoCorpo, Suavizacao(velocidadeRotacaoCorpo, dt));
        }
    }

    private Vector3 CalcularPosicaoPOV()
    {
        Vector3 basePos;

        if (pontoPOV != null)
        {
            basePos = pontoPOV.TransformPoint(ajusteLocalPOV);
        }
        else if (corpoPersonagem != null)
        {
            basePos = corpoPersonagem.TransformPoint(offsetLocalSemPOV);
        }
        else
        {
            basePos = transform.position;
        }

        if (!estabilizarContraAnimacao)
            return basePos;

        if (!possuiPosicaoEstavel)
        {
            ultimaPosicaoEstavel = basePos;
            possuiPosicaoEstavel = true;
            return basePos;
        }

        if (Mathf.Abs(basePos.y - ultimaPosicaoEstavel.y) <= toleranciaTremorVertical)
            basePos.y = ultimaPosicaoEstavel.y;

        ultimaPosicaoEstavel = basePos;
        return basePos;
    }

    private void AtualizarFov(float dt)
    {
        if (camera1Person == null)
            return;

        float alvo = ZoomAtivo ? fovZoom : fovNormal;
        camera1Person.fieldOfView = Mathf.Lerp(camera1Person.fieldOfView, alvo, Suavizacao(velocidadeFov, dt));
    }

    private void AtualizarMira(bool ativa, float dt)
    {
        bool mostrar = ativa && exibirMira;

        if (miraCanvasGroup != null)
        {
            float alvo = mostrar ? 1f : 0f;
            miraCanvasGroup.alpha = Mathf.Lerp(miraCanvasGroup.alpha, alvo, Suavizacao(velocidadeFadeMira, dt));
            miraCanvasGroup.blocksRaycasts = false;
            miraCanvasGroup.interactable = false;
        }

        if (miraImagem != null)
            miraImagem.enabled = mostrar || miraCanvasGroup != null;
    }

    private float DeltaTimeSeguro()
    {
        float dt = usarUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        return Mathf.Clamp(dt, 0.0001f, 0.05f);
    }

    private float Suavizacao(float velocidade, float dt)
    {
        if (velocidade <= 0.0001f)
            return 1f;
        return 1f - Mathf.Exp(-velocidade * dt);
    }

    private void OnValidate()
    {
        if (maxPitch < minPitch)
            maxPitch = minPitch;

        fovNormal = Mathf.Clamp(fovNormal, 1f, 179f);
        fovZoom = Mathf.Clamp(fovZoom, 1f, 179f);
    }
}
