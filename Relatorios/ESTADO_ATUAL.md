# Estado atual do MiniMarket

Atualizado em: 2026-07-12

## Objetivo atual

O projeto possui uma única arquitetura de jogador para Desktop e Mobile. Dados, stamina, HUD, interação, compra, minimapa, diagnósticos e renderização devem compartilhar lógica e evitar referências serializadas inválidas entre a cena e `DontDestroyOnLoad`.

## Sistemas ativos

### Jogador

- `CameraRelativeMovement`: movimento, corrida, pulo, gravidade, rotação, stamina segmentada, Animator e input externo.
- `PlayerCameraController`: autoridade da câmera do jogador.
- `ThirdPersonCamera` e `FirstPersonCamera`: calculam poses e aceitam deltas externos touch.
- `SetExternalPoseControl`: entrega temporariamente a câmera à compra.
- `AddMobileLookDelta` e `SetMobileFirstPersonHeld`: entrada oficial de câmera mobile.

### Dados

- `MiniMarketPlayerDatabase`: fonte local de verdade.
- `PlayerGold`: fachada compatível de gold.
- `MiniMarketPlayerProfile`: fachada compatível de nome e empresas.

### Energia

- lógica segmentada em `CameraRelativeMovement`;
- persistência no banco;
- HUD oficial `MiniMarketEnergySegmentHUD`;
- `FreeEnergyRestoreService` para restauração autoritativa;
- barra principal mostra a carga ativa;
- barras menores mostram os segmentos totais;
- barras ausentes podem ser criadas automaticamente em runtime;
- preenchimento de carga e descarga é animado.

### Interação

- `GetItemController`: selecionar, pegar, mover, soltar com segurança e arremessar explicitamente.
- `GrabbableItem`: regras do objeto móvel.
- `InteractionFocusController`: foco pela câmera em primeira ou terceira pessoa.
- `InteractiveObject`: ação genérica.
- `InteractionHighlight`: cor por `MaterialPropertyBlock`.
- sair da primeira pessoa enquanto segura um item pego nessa visão causa queda segura, não arremesso.

### Compra de terrenos

- `BuySceneEntryTrigger`: detecção e tecla E.
- `BuySceneCameraModeController`: vista de compra.
- `PurchaseModeBridge`: autoridade temporária da câmera.
- `BuySceneLandPurchaseController`: seleção, painel, gold e confirmação.
- `BuyableLandAreaMarker`: destaque e persistência.
- `PurchaseSystemBootstrapHost`: reparo runtime.
- objetos `Buy_Area` são reconhecidos explicitamente.
- o collider sólido da calçada é preservado; o trigger fica em um filho `BuySceneEntryTrigger_Runtime`.

### Minimapa

- `RuntimeMiniMap`: minimapa oficial Desktop/Mobile.
- câmera ortográfica e `RenderTexture`.
- posição, tamanho, margens, cores, zoom, camadas, resolução e botões editáveis pelo Inspector.
- resolução independente para Desktop e Mobile.
- câmera preservada pelo controlador principal por possuir `targetTexture`.

### Diagnósticos

- `RuntimeDiagnosticsPanel`: painel F10.
- exibe FPS, memória, câmeras, AudioListeners, banco, energia, movimento, compra e minimapa.

### Mira

- `FirstPersonReticleController`: mira somente em primeira pessoa.
- oculta em terceira pessoa, menu e compra.
- `click_off` no estado normal.
- `click_on` em objeto selecionado ou segurado.

### Plataforma

- `PlatformRenderProfile`: Desktop, Mobile e Low-End Mobile.
- `MobileControlsHUD`: joystick, olhar, corrida, pulo, interação, pegar/soltar, arremessar e AIM.
- HUD touch visível apenas em mobile, salvo teste forçado.
- layout respeita `Screen.safeArea`.

## Sistemas obsoletos

Não adicionar novas funcionalidades a:

- `PlayerMove` antigo;
- Camera V2 antiga (`CameraV2Controller`, `Camera3Person`, `Camera1Person`);
- `MiniMarketSegmentedStaminaRuntimeGuard`;
- diagnósticos específicos da Camera V2;
- `MiniMarketMiniMapController` legado.

Antes de remover qualquer classe antiga, conferir GUIDs de cenas e prefabs.

## Organização

- organização automática destrutiva desativada;
- `ScriptProjectOrganizer` apenas audita;
- nenhuma ferramenta deve mover, renomear ou apagar scripts automaticamente;
- Brick Project Studio não pode ser alterado.

## Cena esperada

```text
Character 01
├── CharacterController
├── CameraRelativeMovement
├── Animator
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

GameSystemsConfiguration
├── RuntimeMiniMap
├── MobileControlsHUD
└── FirstPersonReticleController

Buy_Area
├── Collider sólido da calçada
└── BuySceneEntryTrigger_Runtime
    ├── BoxCollider trigger
    └── BuySceneEntryTrigger
```

Durante gameplay normal existe uma câmera e um AudioListener de jogador. Câmeras auxiliares com `RenderTexture` podem permanecer ativas sem AudioListener.

## Dados persistidos

- ID;
- nome;
- gold;
- gemas;
- stamina atual e máxima;
- segmentos atuais e máximos;
- reserva de recarga;
- empresas;
- propriedades;
- última cena, posição e rotação quando registrados;
- tempo jogado;
- datas de criação e atualização.

## Riscos e verificações

1. `Buy_Area` precisa manter um collider válido.
2. O painel de compra precisa preservar filhos reconhecidos ou referências explícitas.
3. Animator Controller pode exigir parâmetros próprios.
4. Portas antigas podem exigir configurar `InteractiveObject.onInteract`.
5. `click_on` e `click_off` devem estar importados como Sprite (2D and UI).
6. O HUD de energia precisa apontar para a imagem de preenchimento, não para a moldura.
7. O HUD mobile deve ser validado em aparelho real com multitouch e notch.
8. Assets pesados ainda precisam de LOD, compressão e lightmaps.
9. Alterações locais devem ser commitadas antes de pull; não usar `git clean -fd` ou `git reset --hard` sem backup.

## Ferramentas do Editor

- `Tools > Player System > Create or Repair Player System`
- `Tools > Player System > Repair Player Animator`
- `Tools > Player System > Print Animator Diagnostics`
- `Tools > Game Systems > Repair Data HUD Interactions and Mobile`
- `Tools > Game Systems > Repair Purchase Minimap Diagnostics Energy Reticle`
- `Tools > Game Systems > Apply Gameplay Polish (HUD Grab Purchase MiniMap Mobile)`
- `Tools > Game Systems > Validate Gameplay Polish`
- `Tools > Project Maintenance > Clean Cross-Scene References`
- `Tools > Project Maintenance > Generate Safe Script Organization Audit`
