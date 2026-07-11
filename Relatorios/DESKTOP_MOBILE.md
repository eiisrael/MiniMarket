# Desktop e Mobile

Atualizado em: 2026-07-11

## Perfil runtime

Arquivo:

```text
Assets/Scripts/Performance/PlatformRenderProfile.cs
```

O componente é criado automaticamente antes da cena e escolhe um perfil:

- `Desktop`
- `Mobile`
- `LowEndMobile`
- perfil forçado para testes

## Desktop

Configuração padrão:

- target de 60 FPS;
- render scale 1.0;
- VSync respeitado quando configurado;
- qualidade global definida pelo Quality Settings do projeto;
- nenhum material ou pipeline é substituído em runtime.

## Mobile

Configuração padrão:

- target de 60 FPS;
- VSync desligado;
- render scale 0.85;
- até 2 pixel lights;
- shadow distance limitada;
- MSAA limitado a 2x;
- reflexos em tempo real desligados;
- soft particles desligadas;
- LOD bias reduzido;
- aparelho impedido de dormir durante o jogo.

## Low-End Mobile

Ativado automaticamente quando RAM ou VRAM estão abaixo dos limites configurados.

- target de 30 FPS;
- render scale 0.70;
- shadow distance menor;
- duas influências de osso por vértice;
- LOD máximo mais agressivo;
- filtragem anisotrópica desligada.

## URP

O perfil tenta ajustar `renderScale`, MSAA, HDR e shadow distance no pipeline ativo por reflexão. Isso evita dependência rígida de uma versão específica do pacote Universal RP.

Se a versão do URP expuser uma propriedade somente para leitura, a alteração é ignorada e as configurações globais continuam válidas.

## Entrada mobile disponível

### Movimento

`CameraRelativeMovement`:

- `SetMoveInput(Vector2)`
- `SetRunInput(bool)`
- `RequestJump()`

### Interação

`InteractionFocusController`:

- `RequestInteract()`
- `SetPointerScreenPosition(Vector2)`
- `ClearPointerScreenPosition()`

### Pegar objetos

`GetItemController`:

- `RequestGrabPressed()`
- `RequestGrabReleased()`
- `RequestThrow()`
- `SetPointerScreenPosition(Vector2)`
- `ClearPointerScreenPosition()`

Esses métodos podem ser chamados por botões UI, joystick virtual ou pelo novo Input System.

## UI mobile ainda necessária na cena

O código oferece entrada externa, mas a cena precisa dos controles visuais:

- joystick esquerdo para movimento;
- área de arrasto direita para câmera;
- botão de pulo;
- botão de corrida;
- botão de interagir;
- botão de pegar/soltar;
- botão de arremessar, quando usado.

## Recomendações de assets

- Preferir luz baked/mixed em ambientes estáticos.
- Evitar várias Point Lights com sombras.
- Comprimir texturas por plataforma.
- Usar atlas e reduzir materiais únicos.
- Adicionar LOD a prédios e objetos distantes.
- Usar occlusion culling somente após validar o mapa.
- Evitar transparências grandes sobrepostas.
- Verificar overdraw do HUD.
- Limitar partículas em mobile.

## Testes obrigatórios

1. Testar no Editor com perfil Desktop.
2. Forçar perfil Mobile no Inspector.
3. Forçar perfil LowEndMobile.
4. Verificar uma câmera e um AudioListener.
5. Testar orientação e resolução alvo.
6. Fazer build Android real; o Game View não substitui teste no aparelho.
7. Verificar temperatura, memória e FPS por pelo menos dez minutos.
8. Testar pause/resume e retorno do aplicativo.
9. Confirmar save após o aplicativo ir para segundo plano.
