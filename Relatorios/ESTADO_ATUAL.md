# Estado atual do MiniMarket

Atualizado em: 2026-07-11

## Objetivo atual

O projeto possui uma única arquitetura de jogador para Desktop e Mobile. Dados persistentes, stamina, HUD, interações, compra de terrenos, minimapa, diagnósticos e renderização devem funcionar sem depender de referências entre a cena normal e objetos `DontDestroyOnLoad`.

## Sistemas ativos

### Jogador

- `CameraRelativeMovement`: movimento, corrida, pulo, gravidade, rotação, stamina segmentada, Animator e input mobile externo.
- `PlayerCameraController`: autoridade da câmera real do jogador.
- `ThirdPersonCamera` e `FirstPersonCamera`: calculam poses para a câmera central.
- `PlayerCameraController.SetExternalPoseControl`: entrega temporariamente a câmera a sistemas como compra de terrenos.

### Dados

- `MiniMarketPlayerDatabase`: fonte única de verdade local.
- `PlayerGold`: fachada compatível para sistemas antigos de gold.
- `MiniMarketPlayerProfile`: fachada compatível para nome e empresas.

### Energia

- A energia segmentada pertence a `CameraRelativeMovement`.
- A persistência pertence ao banco.
- O HUD oficial é `MiniMarketEnergySegmentHUD`.
- `FreeEnergyRestoreService` torna a restauração gratuita autoritativa e atualiza banco, runtime e HUD.
- O HUD suporta barra principal e múltiplas barras segmentadas com preenchimento animado.

### Interação

- `GetItemController`: selecionar, pegar, mover, soltar e arremessar objetos físicos.
- `GrabbableItem`: regras do objeto móvel.
- `InteractionFocusController`: detectar objetos interativos pela câmera.
- `InteractiveObject`: ação genérica para portas, caixas e mecanismos.
- `InteractionHighlight`: troca de cor sem instanciar materiais.

### Compra de terrenos

- `BuySceneEntryTrigger`: detecta o personagem na área da calçada e processa a tecla E.
- `BuySceneCameraModeController`: calcula/aplica a vista de compra.
- `PurchaseModeBridge`: impede disputa entre a câmera do jogador e a câmera de compra.
- `BuySceneLandPurchaseController`: seleção, painel, débito de gold e confirmação.
- `BuyableLandAreaMarker`: linhas, destaque e estado persistido do terreno.
- `PurchaseSystemBootstrapHost`: reparo runtime de referências e visuais removidos pela organização antiga.

### Minimapa

- `RuntimeMiniMap`: minimapa autônomo para Desktop/Mobile.
- Usa câmera ortográfica com `RenderTexture`, UI circular, ponto do jogador, tecla M e botões de zoom.
- A câmera do minimapa é preservada pelo `PlayerCameraController` por renderizar para `RenderTexture`.

### Diagnósticos

- `RuntimeDiagnosticsPanel`: painel F10 autônomo.
- Exibe FPS, memória, câmeras, AudioListeners, banco, energia, movimento, compra e minimapa.

### Mira

- `FirstPersonReticleController`: mostra elementos de mira apenas em primeira pessoa.
- Oculta a mira em terceira pessoa, menu/input bloqueado e modo de compra.

### Plataforma

- `PlatformRenderProfile`: escolhe Desktop, Mobile ou Low-End Mobile e aplica limites de renderização.
- Entrada mobile é feita por métodos públicos nos sistemas de movimento e interação. A UI touch pode chamar esses métodos por `Button`, `EventTrigger` ou outro controlador de joystick.
- O minimapa usa RenderTexture de menor resolução no mobile.

## Sistemas obsoletos

Os itens abaixo podem existir apenas por compatibilidade e não devem receber novas funcionalidades:

- `PlayerMove` antigo.
- `CameraV2Controller`, `Camera3Person`, `Camera1Person` e auxiliares V2 antigos.
- `MiniMarketSegmentedStaminaRuntimeGuard`.
- diagnósticos antigos específicos de Camera V2.
- minimapa legado `MiniMarketMiniMapController`.

Antes de apagar uma classe obsoleta, conferir se alguma cena ou prefab ainda guarda seu GUID.

## Organização de scripts

- A organização automática destrutiva está desativada.
- `ScriptProjectOrganizer` gera somente `Relatorios/AUDITORIA_SCRIPTS.md`.
- Nenhuma ferramenta de organização deve mover, renomear ou apagar arquivos automaticamente.
- Brick Project Studio permanece fora de qualquer auditoria ou reparo.

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

Área de compra existente
├── Collider com BuySceneEntryTrigger
├── BuySceneCameraModeController + PurchaseModeBridge
├── BuySceneLandPurchaseController
├── BuyableLandAreaMarker(s)
└── Canvas/Painel de confirmação
```

Durante gameplay normal deve existir uma câmera de jogador e um AudioListener ativos. Câmeras auxiliares com `RenderTexture`, como o minimapa, podem permanecer ativas sem AudioListener.

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

1. A compra exige que o collider correto da calçada possua `BuySceneEntryTrigger`.
2. O painel de confirmação precisa manter seus filhos `PainelWarning`, `TextAsking`, `ButtonConfirm` e `ButtonClose`, ou nomes equivalentes reconhecidos.
3. Animator Controller pode usar parâmetros ou nomes de estados diferentes; usar a ferramenta de diagnóstico antes de alterar animações.
4. Portas antigas podem exigir configurar `InteractiveObject.onInteract` se o método existente não usar um dos nomes reconhecidos.
5. UI mobile ainda precisa de botões/joystick visuais na cena; os métodos de entrada já existem.
6. O perfil mobile reduz custo global, mas assets pesados ainda precisam de LOD, compressão e lightmaps adequados.
7. Alterações locais grandes devem ser commitadas antes de `git pull`; não usar `git clean -fd` ou `git reset --hard` sem backup.

## Ferramentas do Editor

- `Tools > Player System > Create or Repair Player System`
- `Tools > Player System > Repair Player Animator`
- `Tools > Player System > Print Animator Diagnostics`
- `Tools > Game Systems > Repair Data HUD Interactions and Mobile`
- `Tools > Game Systems > Repair Purchase Minimap Diagnostics Energy Reticle`
- `Tools > Project Maintenance > Clean Cross-Scene References`
- `Tools > Project Maintenance > Generate Safe Script Organization Audit`
