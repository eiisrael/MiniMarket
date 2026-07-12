# Desktop e Mobile

Atualizado em: 2026-07-12

## Perfil runtime

Arquivo:

```text
Assets/Scripts/Performance/PlatformRenderProfile.cs
```

Perfis:

- `Desktop`;
- `Mobile`;
- `LowEndMobile`;
- perfil forçado para testes.

O perfil é reaplicado depois do carregamento da cena para impedir sobrescrita de FPS, VSync e render scale.

## Desktop

Configuração padrão:

- alvo de 60 FPS;
- render scale 1.0;
- VSync conforme Quality Settings;
- teclado e mouse ativos;
- HUD touch oculto;
- nenhum material ou pipeline substituído em runtime.

## Mobile

Configuração padrão:

- alvo de 60 FPS;
- VSync desligado;
- render scale 0.85;
- até 2 pixel lights;
- distância de sombra limitada;
- MSAA até 2x;
- reflexos em tempo real desligados;
- soft particles desligadas;
- LOD bias reduzido;
- tela impedida de dormir.

## Low-End Mobile

Ativado por limites de RAM/VRAM configurados:

- alvo de 30 FPS;
- render scale 0.70;
- sombra reduzida;
- bone weights limitados;
- LOD mais agressivo;
- anisotropic filtering desligado.

## URP

`PlatformRenderProfile` tenta ajustar propriedades do URP por reflexão para manter compatibilidade entre versões do pacote. Propriedades indisponíveis são ignoradas sem quebrar o perfil global.

## HUD touch oficial

Arquivo:

```text
Assets/Scripts/UI/MobileControlsHUD.cs
```

Visibilidade:

- Android/iOS: visível automaticamente;
- Desktop: oculto por padrão;
- Editor/Desktop de teste: ligar `forcarVisivelParaTestes`.

O HUD respeita `Screen.safeArea` e limpa toda entrada ao pausar, desabilitar ou bloquear gameplay.

### Controles

- joystick esquerdo: movimento;
- área direita: olhar/câmera;
- `RUN`: correr enquanto pressionado;
- `JUMP`: pular;
- `E`: interagir;
- `GRAB`: pegar e soltar;
- `THROW`: arremessar;
- `AIM`: primeira pessoa/mira enquanto pressionado.

### APIs conectadas

`CameraRelativeMovement`:

- `SetMoveInput(Vector2)`;
- `SetRunInput(bool)`;
- `RequestJump()`.

`PlayerCameraController`:

- `AddMobileLookDelta(Vector2)`;
- `SetMobileFirstPersonHeld(bool)`;
- `SetMobileFirstPerson(bool)`.

`InteractionFocusController`:

- `RequestInteract()`;
- `SetPointerScreenPosition(Vector2)`;
- `ClearPointerScreenPosition()`.

`GetItemController`:

- `RequestGrabPressed()`;
- `RequestGrabReleased()`;
- `RequestThrow()`;
- `SetPointerScreenPosition(Vector2)`;
- `ClearPointerScreenPosition()`.

## Câmera touch

`FirstPersonCamera` e `ThirdPersonCamera` aceitam deltas externos de olhar sem remover o input do mouse. `PlayerCameraController` acumula o delta touch e aplica uma única vez por frame.

O botão AIM:

- entra na primeira pessoa ao pressionar;
- ativa FOV de mira;
- retorna à terceira pessoa ao soltar;
- faz objetos segurados em primeira pessoa caírem com segurança ao sair da mira.

## Configuração no Inspector

A ferramenta:

```text
Tools > Game Systems > Apply Gameplay Polish (HUD Grab Purchase MiniMap Mobile)
```

cria ou reutiliza `MobileControlsHUD` na cena e preenche referências do movimento, câmera, interação e objetos.

## Recomendações de assets

- preferir luz baked/mixed;
- limitar Point Lights com sombra;
- comprimir texturas por plataforma;
- usar atlas;
- reduzir materiais únicos;
- usar LOD em prédios e objetos distantes;
- validar occlusion culling antes de habilitar;
- reduzir transparências sobrepostas e overdraw do HUD;
- limitar partículas no mobile.

## Testes obrigatórios

1. Validar perfil Desktop.
2. Forçar perfil Mobile.
3. Forçar LowEndMobile.
4. Recarregar a cena e confirmar persistência do perfil.
5. Confirmar uma câmera e um AudioListener de gameplay.
6. Ativar `forcarVisivelParaTestes` e testar todos os controles com o mouse.
7. Confirmar joystick, olhar, corrida, pulo e interação.
8. Confirmar pegar, soltar e arremessar.
9. Confirmar AIM e retorno à terceira pessoa.
10. Fazer build Android real.
11. Validar safe area em aparelho com notch.
12. Testar multitouch: mover + olhar + correr.
13. Medir temperatura, memória e FPS por pelo menos dez minutos.
14. Testar pause/resume e segundo plano.
15. Confirmar save após o aplicativo ir para segundo plano.
