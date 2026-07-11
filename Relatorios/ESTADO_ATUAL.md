# Estado atual do MiniMarket

Atualizado em: 2026-07-11

## Objetivo atual

O projeto possui uma única arquitetura de jogador para Desktop e Mobile. Dados persistentes, stamina, HUD, interações e renderização devem funcionar sem depender de referências entre a cena normal e objetos `DontDestroyOnLoad`.

## Sistemas ativos

### Jogador

- `CameraRelativeMovement`: movimento, corrida, pulo, gravidade, rotação, stamina segmentada, Animator e input mobile externo.
- `PlayerCameraController`: autoridade da única câmera real do jogador.
- `ThirdPersonCamera` e `FirstPersonCamera`: calculam poses para a câmera central.

### Dados

- `MiniMarketPlayerDatabase`: fonte única de verdade local.
- `PlayerGold`: fachada compatível para sistemas antigos de gold.
- `MiniMarketPlayerProfile`: fachada compatível para nome e empresas.

### Energia

- A energia segmentada pertence a `CameraRelativeMovement`.
- A persistência pertence ao banco.
- O HUD oficial é `MiniMarketEnergySegmentHUD`.

### Interação

- `GetItemController`: selecionar, pegar, mover, soltar e arremessar objetos físicos.
- `GrabbableItem`: regras do objeto móvel.
- `InteractionFocusController`: detectar objetos interativos pela câmera.
- `InteractiveObject`: ação genérica para portas, caixas e mecanismos.
- `InteractionHighlight`: troca de cor sem instanciar materiais.

### Plataforma

- `PlatformRenderProfile`: escolhe Desktop, Mobile ou Low-End Mobile e aplica limites de renderização.
- Entrada mobile é feita por métodos públicos nos sistemas de movimento e interação. A UI touch pode chamar esses métodos por `Button`, `EventTrigger` ou outro controlador de joystick.

## Sistemas obsoletos

Os itens abaixo podem existir apenas por compatibilidade e não devem receber novas funcionalidades:

- `PlayerMove` antigo.
- `CameraV2Controller`, `Camera3Person`, `Camera1Person` e auxiliares V2 antigos.
- `MiniMarketSegmentedStaminaRuntimeGuard`: agora é um componente vazio de compatibilidade.
- diagnósticos antigos específicos de Camera V2.

Antes de apagar uma classe obsoleta, conferir se alguma cena ou prefab ainda guarda seu GUID.

## Cena esperada

Estrutura lógica mínima:

```text
Character 01
├── CharacterController
├── CameraRelativeMovement
├── Animator em si ou em um filho
├── CameraTarget
└── FirstPersonEye

PlayerCameraRig
├── Camera
├── AudioListener
├── ThirdPersonCamera
├── FirstPersonCamera
├── PlayerCameraController
├── GetItemController
└── InteractionFocusController
```

Não deve existir mais de uma câmera ou um `AudioListener` ativo durante o gameplay.

## Dados persistidos

- ID local do jogador.
- nome.
- gold.
- gemas.
- stamina atual e máxima.
- quantidade atual e máxima de segmentos de energia.
- reserva de recarga dos segmentos.
- IDs das empresas compradas.
- estado das propriedades.
- última cena, posição e rotação, quando o sistema correspondente chamar o banco.
- tempo jogado.
- datas de criação e atualização.

## Riscos conhecidos que devem ser verificados

1. Objetos antigos podem manter componentes de câmera ou movimento obsoletos.
2. Animator Controller pode usar parâmetros ou nomes de estados diferentes; usar a ferramenta de diagnóstico antes de alterar animações.
3. Portas antigas podem exigir configurar `InteractiveObject.onInteract` se o método existente não usar um dos nomes reconhecidos.
4. UI mobile ainda precisa de botões/joystick visuais na cena; os métodos de entrada já existem.
5. O perfil mobile reduz custo global, mas assets pesados ainda precisam de LOD, compressão e lightmaps adequados.

## Ferramentas do Editor

- `Tools > Player System > Create or Repair Player System`
- `Tools > Player System > Repair Player Animator`
- `Tools > Player System > Print Animator Diagnostics`
- `Tools > Game Systems > Repair Data HUD Interactions and Mobile`
- `Tools > Project Maintenance > Clean Cross-Scene References`
