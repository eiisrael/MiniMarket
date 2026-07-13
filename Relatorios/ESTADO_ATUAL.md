# Estado atual do MiniMarket

Atualizado em: 2026-07-13

## Objetivo atual

O projeto possui uma única arquitetura de jogador para Desktop e Mobile. Dados, stamina, HUD, interação, compra, minimapa, diagnósticos e renderização devem compartilhar lógica e evitar referências serializadas inválidas entre a cena e `DontDestroyOnLoad`.

Objetos visuais e hosts configuráveis não devem existir apenas como cópias temporárias do Play Mode. A cena possui uma ferramenta explícita para materializá-los e permitir edição pela Hierarchy e Inspector.

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
- o singleton do banco permanece em `DontDestroyOnLoad` porque representa estado transitório e persistência, não layout visual.

### Energia

- lógica segmentada em `CameraRelativeMovement`;
- persistência no banco;
- `MiniMarketEnergySegmentHUD` mantém texto e compatibilidade segmentada;
- `MiniMarketEnergyProgressBar` controla a barra verde interna;
- `FreeEnergyRestoreService` para restauração autoritativa;
- `Energy` permanece estático;
- `EnergyProgressArea/EnergyProgressFill` ficam salvos na cena e editáveis;
- a barra verde representa a energia total entre `0/5` e `5/5`;
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
- `PurchaseSystemBootstrapHost`: reparo runtime e fallback.
- objetos `Buy_Area` são reconhecidos explicitamente.
- o collider sólido da calçada é preservado; o trigger fica em um filho `BuySceneEntryTrigger_Runtime`.
- trigger, controlador, host de reparo e LineRenderers podem ser materializados e salvos na cena.

### Minimapa

- `RuntimeMiniMap`: minimapa oficial Desktop/Mobile.
- `RuntimeMiniMapHierarchyBinding`: liga a hierarquia persistente ao controlador.
- câmera ortográfica e Canvas são salvos na cena.
- posição, tamanho, margens, cores, zoom, camadas, resolução, botões e filhos visuais são editáveis pelo Inspector.
- resolução independente para Desktop e Mobile.
- câmera preservada pelo controlador principal por possuir `targetTexture` no Play Mode.
- somente a `RenderTexture` continua temporária.

### Diagnósticos

- `RuntimeDiagnosticsPanel`: painel F10.
- exibe FPS, memória, câmeras, AudioListeners, banco, energia, movimento, compra e minimapa.
- o host pode ficar salvo como objeto real da cena.

### Mira

- `FirstPersonReticleController`: mira somente em primeira pessoa.
- oculta em terceira pessoa, menu e compra.
- `click_off` no estado normal.
- `click_on` em objeto selecionado ou segurado.
- controlador e imagem `Mira` podem ficar persistentes na cena.

### Plataforma

- `PlatformRenderProfile`: Desktop, Mobile e Low-End Mobile.
- o bootstrap reutiliza o componente persistente salvo na cena antes de criar fallback runtime.
- `MobileControlsHUD`: joystick, olhar, corrida, pulo, interação, pegar/soltar, arremessar e AIM.
- `MobileControlsHierarchyBinding`: liga a hierarquia persistente de controles ao runtime.
- HUD touch visível apenas em mobile durante o jogo, salvo teste forçado.
- objetos do HUD mobile permanecem salvos para edição fora do Play.
- layout respeita `Screen.safeArea`.

### Hierarquia persistente

Ferramenta oficial:

```text
Tools > MiniMarket > Materializar Todos os Objetos Runtime na Hierarquia
Tools > MiniMarket > Validar Objetos Runtime Persistentes
```

Ela materializa energia, minimapa, mobile, mira, compra, diagnóstico, EventSystem e perfil de renderização. O relatório detalhado é `OBJETOS_RUNTIME_PERSISTENTES.md`.

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
- Brick Project Studio não pode ser alterado;
- o materializador cria somente filhos, componentes e assets de suporte conhecidos;
- assets gerados ficam em `Assets/Generated/MiniMarket`.

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

Canvas
├── Mira
└── StaminaHUD
    └── Energy
        └── EnergyProgressArea
            └── EnergyProgressFill

GameSystemsConfiguration
├── objeto com RuntimeMiniMap
│   ├── RuntimeMiniMapCamera
│   └── RuntimeMiniMapCanvas
│       └── MiniMap
├── objeto com MobileControlsHUD
│   └── MobileControlsRuntime
│       └── SafeArea
└── objeto com FirstPersonReticleController

PlatformRenderProfile
RuntimeDiagnosticsPanel
PurchaseSystemRuntimeRepair

Buy_Area
├── Collider sólido da calçada
└── BuySceneEntryTrigger_Runtime
    ├── BoxCollider trigger
    ├── BuySceneEntryTrigger
    ├── BuyScene_Entrada_Borda
    ├── BuyScene_Entrada_Diagonal_A
    └── BuyScene_Entrada_Diagonal_B
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

1. Executar o materializador somente fora do Play Mode e com a `SampleScene` salva.
2. Confirmar zero erros vermelhos antes de executar ferramentas.
3. `Buy_Area` precisa manter um collider válido.
4. O painel de compra precisa preservar filhos reconhecidos ou referências explícitas.
5. Animator Controller pode exigir parâmetros próprios.
6. Portas antigas podem exigir configurar `InteractiveObject.onInteract`.
7. `click_on` e `click_off` devem estar importados como Sprite (2D and UI).
8. O HUD de energia deve apontar para `EnergyProgressFill`, nunca para o artwork/moldura.
9. O HUD mobile deve ser validado em aparelho real com multitouch e notch.
10. Assets pesados ainda precisam de LOD, compressão e lightmaps.
11. Alterações locais devem ser commitadas antes de pull; não usar `git clean -fd` ou `git reset --hard` sem backup.
12. A `RenderTexture` do minimapa e estados de input continuam temporários por design.

## Ferramentas do Editor

- `Tools > Player System > Create or Repair Player System`
- `Tools > Player System > Repair Player Animator`
- `Tools > Player System > Print Animator Diagnostics`
- `Tools > Game Systems > Repair Data HUD Interactions and Mobile`
- `Tools > Game Systems > Repair Purchase Minimap Diagnostics Energy Reticle`
- `Tools > Game Systems > Apply Gameplay Polish (HUD Grab Purchase MiniMap Mobile)`
- `Tools > Game Systems > Validate Gameplay Polish`
- `Tools > MiniMarket > Materializar Todos os Objetos Runtime na Hierarquia`
- `Tools > MiniMarket > Validar Objetos Runtime Persistentes`
- `Tools > MiniMarket > Criar ou Reparar Barra de Energia`
- `Tools > MiniMarket > Validar Barra de Energia`
- `Tools > Project Maintenance > Clean Cross-Scene References`
- `Tools > Project Maintenance > Generate Safe Script Organization Audit`
