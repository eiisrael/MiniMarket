# Objetos runtime persistentes e editáveis

Atualizado em: 2026-07-14

## Atualização de 2026-07-14 — sistema de jornal

### Hierarquia persistente

O sistema de jornal mantém na cena:

```text
Newspaper_Stand
└── Newspaper_InteractionPrompt
    ├── CircularPrompt
    │   ├── SoftGlow
    │   ├── GoldenOuterRing
    │   ├── RotatingSegmentedRing
    │   ├── CircularProgress
    │   ├── PinkInnerAccent
    │   ├── CenterDisc
    │   └── CenterText
    └── Instruction

Put_Area
├── Newspaper_PlacePrompt
│   ├── CircularPrompt
│   └── Instruction
└── Placed_Newspaper_Runtime
```

### Regras do prompt

- o design circular e os nomes existentes foram preservados;
- `NewspaperWorldPromptVisual` não reaplica layout completo a cada frame;
- posição, rotação, escala e tamanho de `CircularPrompt` e dos filhos podem ser controlados pelo Inspector;
- cores, fonte, contorno e transparência podem vir diretamente dos componentes da Hierarchy;
- `Visible Opacity` controla a transparência total sem alterar os filhos;
- animação de posição e escala do círculo fica desligada por padrão;
- a rotação do anel continua opcional e usa a rotação editada como base;
- o prompt da Put_Area usa billboard vertical e não inclina para céu/chão;
- escala e inclinação deixadas pelo sistema antigo são reparadas apenas uma vez;
- o reparo manual preserva `Ctrl+S` e não roda continuamente.

### Performance ao pegar e colocar

- o inventário agrupa quantidade e estado do local em uma única gravação criptografada;
- a gravação acontece após o frame de interação;
- o `Placed_Newspaper_Runtime` é preparado antes do clique;
- renderers, física, prompts e referências são cacheados;
- não ocorre `Instantiate`, varredura recursiva ou criação de componentes no frame normal de colocar quando a hierarquia persistente está configurada.

## Objetivo

Os sistemas visuais e os hosts de configuração que antes surgiam somente ao apertar Play passam a existir como objetos reais da `SampleScene`.

Isso permite:

- selecionar os objetos na Hierarchy fora do Play Mode;
- editar posição, tamanho, cor, sprites e opções no Inspector;
- salvar as alterações com a cena;
- impedir que ajustes feitos durante o Play sejam perdidos ao pressionar Stop;
- manter o runtime responsável apenas por estado, eventos e recursos realmente temporários.

## Ferramenta principal

Com a `SampleScene` aberta e fora do Play Mode:

```text
Tools > MiniMarket > Materializar Todos os Objetos Runtime na Hierarquia
```

Validação:

```text
Tools > MiniMarket > Validar Objetos Runtime Persistentes
```

A ferramenta usa `Undo`, marca a cena como modificada, salva a cena e não move nem apaga arquivos existentes.

Para o jornal, os reparos específicos são manuais:

```text
Tools > MiniMarket > Jornal > Configurar Sistema Automaticamente
Tools > MiniMarket > Jornal > Reparar Prompt da Put Area
Tools > MiniMarket > Jornal > Reconciliar Jornal Colocado Persistente
```

## Hierarquia materializada

### Barra de energia

```text
Canvas
└── StaminaHUD
    └── Energy
        └── EnergyProgressArea
            └── EnergyProgressFill
```

- `Energy` permanece como artwork estático;
- `EnergyProgressArea` define a área interna da barra;
- `EnergyProgressFill` é a imagem verde que aumenta e diminui;
- a cor pode ser alterada diretamente na `Image` ou em `Cor Barra` do `MiniMarketEnergyProgressBar`;
- o Inspector customizado aplica a cor e a área imediatamente fora do Play Mode.

### Minimapa

```text
GameSystemsConfiguration
└── MiniMapSystem ou objeto existente com RuntimeMiniMap
    ├── RuntimeMiniMapCamera
    └── RuntimeMiniMapCanvas
        └── MiniMap
            ├── Border
            │   └── CircularMask
            │       ├── MapImage
            │       └── PlayerDot
            ├── ZoomIn
            │   └── Label
            └── ZoomOut
                └── Label
```

`RuntimeMiniMapHierarchyBinding` liga essa hierarquia persistente ao controlador existente. A câmera, Canvas, borda, botões e ponto do jogador são editáveis na cena.

A `RenderTexture` continua sendo criada em runtime porque é um recurso de GPU temporário, não um GameObject da Hierarchy.

### HUD mobile

```text
GameSystemsConfiguration
└── MobileControlsSystem ou objeto existente com MobileControlsHUD
    └── MobileControlsRuntime
        └── SafeArea
            ├── LookArea
            ├── MoveJoystick
            │   └── Thumb
            └── Actions
                ├── Aim
                ├── Jump
                ├── Run
                ├── Grab
                ├── Interact
                └── Throw
```

`MobileControlsHierarchyBinding` liga os objetos persistentes ao `MobileControlsHUD` e adiciona os callbacks de toque somente durante a execução.

Os objetos permanecem visíveis fora do Play para edição. Em Desktop, o controlador ainda pode ocultar o Canvas durante o jogo conforme `ocultarNoDesktop`.

### Mira

O `FirstPersonReticleController` fica salvo em `GameSystemsConfiguration`. Caso não exista uma imagem de mira reconhecida, é criado:

```text
Canvas
└── Mira
```

O sprite normal e o `click_on` continuam editáveis pelo Inspector.

### Compra de terrenos

São persistidos:

```text
PurchaseSystemRuntimeRepair
BUY_SYSTEM
Buy_Area
└── BuySceneEntryTrigger_Runtime
    ├── BuyScene_Entrada_Borda
    ├── BuyScene_Entrada_Diagonal_A
    └── BuyScene_Entrada_Diagonal_B
```

Também são materializadas as linhas dos `BuyableLandAreaMarker`:

```text
BuyScene_Borda_Terreno
BuyScene_X_Diagonal_A
BuyScene_X_Diagonal_B
```

É criado um material persistente em:

```text
Assets/Generated/MiniMarket/Materials/BuyAreaLine.mat
```

### Serviços configuráveis

São salvos como objetos reais:

```text
RuntimeDiagnosticsPanel
PurchaseSystemRuntimeRepair
PlatformRenderProfile
```

O bootstrap do `PlatformRenderProfile` foi alterado para reutilizar o componente salvo na cena antes de criar um fallback runtime.

## Assets gerados pela ferramenta

Apenas quando a ferramenta é executada:

```text
Assets/Generated/MiniMarket/UI/MiniMapCircle.png
Assets/Generated/MiniMarket/Materials/BuyAreaLine.mat
```

Esses assets são persistentes, possuem `.meta` gerado pelo Unity e podem ser editados ou substituídos depois.

## Bindings

Foram adicionados:

```text
Assets/Scripts/UI/RuntimeMiniMapHierarchyBinding.cs
Assets/Scripts/UI/MobileControlsHierarchyBinding.cs
```

Os bindings usam reflexão cacheada apenas na inicialização e em eventos de UI, nunca em busca ou escrita por frame. Isso preserva compatibilidade com os campos privados dos controladores atuais sem reescrever o gameplay estabilizado.

## Objetos e recursos que permanecem temporários

Nem tudo que existe em runtime deve ser serializado na cena. Permanecem temporários:

- `RenderTexture` do minimapa;
- callbacks/delegates dos botões mobile;
- materiais internos que algum sistema antigo ainda recrie durante o Play;
- estado do banco e o singleton `MiniMarket_PlayerDatabase` em `DontDestroyOnLoad`;
- estilos IMGUI do painel F10;
- caches e estados de input.

Esses itens não são layouts editáveis da Hierarchy. Os respectivos hosts e parâmetros editáveis foram materializados quando aplicável.

## Regras de edição

- editar fora do Play Mode;
- usar `Ctrl + S` depois das alterações;
- não editar objetos sob `DontDestroyOnLoad` durante o Play esperando persistência;
- para o jornal, editar `Newspaper_InteractionPrompt`, `Newspaper_PlacePrompt`, `Instruction`, `CircularPrompt` e `Placed_Newspaper_Runtime` fora do Play;
- quando `Face Camera` estiver marcado, a rotação mundial da raiz do prompt é controlada pelo billboard durante o Play; usar `Billboard Euler Offset` para ajuste fino ou desmarcar `Face Camera` para rotação manual;
- para a energia, editar `EnergyProgressFill` ou `MiniMarketEnergyProgressBar`;
- para mobile, editar os objetos persistentes e também os valores do `MobileControlsHUD`;
- para minimapa, editar os objetos persistentes e os parâmetros do `RuntimeMiniMap`;
- não alterar `Assets/Brick Project Studio`.

## Validação estática

Foram revisados:

- `RuntimeMiniMap`;
- `MobileControlsHUD`;
- `FirstPersonReticleController`;
- `MiniMarketEnergyProgressBar`;
- `PurchaseSystemBootstrapHost`;
- `BuySceneEntryTrigger`;
- `BuyableLandAreaMarker`;
- `RuntimeDiagnosticsPanel`;
- `PlatformRenderProfile`;
- `NewspaperStandController`;
- `NewspaperPlacementAreaController`;
- `MiniMarketNewspaperInventoryService`;
- `NewspaperWorldPromptVisual`.

A compilação e o comportamento visual final precisam ser confirmados no Unity local.
