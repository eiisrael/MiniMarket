# Estado atual do MiniMarket

Atualizado em: 2026-07-15

## Objetivo atual

O projeto possui uma arquitetura compartilhada entre Desktop e Mobile. Dados, stamina, HUD, interação, compra, minimapa, diagnósticos e renderização devem evitar autoridades duplicadas e referências inválidas entre a cena e `DontDestroyOnLoad`.

Objetos visuais e hosts configuráveis devem permanecer salvos na cena para edição pela Hierarchy e Inspector. Recursos realmente transitórios, como RenderTexture e estado de input, podem continuar runtime.

## Sistemas ativos

### Jogador

- `CameraRelativeMovement`: movimento, corrida, pulo, gravidade, rotação, Animator, stamina e input externo.
- `PlayerCameraController`: autoridade da câmera do jogador.
- `ThirdPersonCamera` e `FirstPersonCamera`: cálculo de pose e entrada touch.
- `SetExternalPoseControl`: entrega temporariamente a câmera ao modo de compra.

### Dados

- `MiniMarketPlayerDatabase`: fonte local de verdade.
- `PlayerGold`: fachada compatível para gold.
- `MiniMarketPlayerProfile`: fachada compatível para nome e empresas.
- `PlayerProfile` e `BuyableLandAreaMarker`: consumidores compatíveis ligados ao mesmo `MiniMarketPlayerDatabase`.
- `PlayerDatabaseFileRecoveryBootstrap`: aceita saves estruturais `MMDB1`, `MMDB2` e JSON válido.
- o singleton do banco permanece em `DontDestroyOnLoad` porque representa estado, não layout visual.
- `PlayerGold` permanece na cena como fachada; ele não move mais o jogador nem a câmera para `DontDestroyOnLoad`.
- `MiniMarketMenuController` resolve banco, gold e movimento em runtime e não serializa referências para objetos persistentes.

### Energia

- lógica e energia total segmentada em `CameraRelativeMovement`;
- persistência no banco;
- `MiniMarketEnergyProgressBar` controla `EnergyProgressFill`;
- texto em porcentagem;
- ícone verde, amarelo e vermelho por faixa;
- degradê suave na progress bar;
- pulsação do ícone ao correr/pressionar Shift;
- objetos visuais salvos e editáveis fora do Play Mode.

### Interação

- `GetItemController`: selecionar, pegar, mover, soltar com segurança e arremessar.
- `GrabbableItem`: regras do objeto móvel.
- `InteractionFocusController`: foco em primeira ou terceira pessoa, com fallback seguro para `MeshCollider` não convexo e colliders customizados.
- `InteractiveObject`: ação genérica.
- `InteractionHighlight`: destaque por `MaterialPropertyBlock`.

### Compra de lojas Bronze

A autoridade de cada loja é:

```text
BronzeMarketPurchaseLot
```

Cada raiz `Bronze_Market` deve possuir:

- ID persistente exclusivo;
- `Buy_Area` dentro da própria hierarquia;
- collider sólido da calçada;
- filho `BuySceneEntryTrigger_Runtime`;
- exatamente um `BuyableLandAreaMarker` principal;
- controlador local de câmera;
- `PurchaseModeBridge` local;
- `BuySceneLandPurchaseController` local;
- ponto de foco da câmera;
- painel mundial de status/hover.

Regras atuais:

- duplicar a raiz `Bronze_Market` cria outra loja, não uma referência ao mesmo lote;
- IDs copiados são substituídos automaticamente por IDs únicos;
- trigger e controlador recebem somente o terreno pertencente à própria raiz;
- buscas globais ou por proximidade ficam desligadas nas lojas configuradas;
- hover, clique, painel e confirmação rejeitam terrenos externos ao lote atual;
- o modo aberto com `E` mostra apenas o status da loja que o ativou;
- a câmera continua usando `BuySceneCameraModeController` e seus valores já configurados;
- o reconciliador prefere o controlador existente chamado `BuySceneController`, preservando altura, rotação, ortográfico e transições;
- `PurchaseSystemBootstrapHost` mantém lojas Bronze isoladas e usa fallback global somente para objetos legados.
- atualização de linhas usa APIs tipadas de `BuySceneEntryTrigger` e `BuyableLandAreaMarker`, sem `SendMessage`.

### Minimapa

- `RuntimeMiniMap`: minimapa oficial Desktop/Mobile.
- `RuntimeMiniMapHierarchyBinding`: liga a hierarquia persistente ao controlador.
- câmera ortográfica e Canvas salvos na cena.
- somente a `RenderTexture` continua temporária.

### Diagnósticos

- `RuntimeDiagnosticsPanel`: painel F10.
- exibe performance, câmeras, banco, energia, movimento, compra e minimapa.

### Mira

- `FirstPersonReticleController`: mira em primeira pessoa.
- `click_off` no estado normal.
- `click_on` ao selecionar ou segurar objeto.
- oculta em terceira pessoa, menus e compra.

### Plataforma

- `PlatformRenderProfile`: Desktop, Mobile e Low-End Mobile.
- `MobileControlsHUD`: joystick, olhar, corrida, pulo, interação, pegar/soltar, arremessar e AIM.
- `MobileControlsHierarchyBinding`: referências persistentes do HUD touch.

## Sistemas obsoletos

Não adicionar novas funcionalidades a:

- `PlayerMove` antigo;
- Camera V2 antiga (`CameraV2Controller`, `Camera3Person`, `Camera1Person`);
- `MiniMarketSegmentedStaminaRuntimeGuard`;
- diagnósticos específicos da Camera V2;
- `MiniMarketMiniMapController` legado;
- `Assets/Scripts/Data/PlayerDatabase.cs`, mantido somente para preservar compatibilidade de GUID enquanto referências antigas são eliminadas.
- `SceneReferenceRepair`, desativado porque mover raízes durante o Play mascarava referências inválidas.

Antes de remover qualquer classe antiga, conferir GUIDs de cenas e prefabs.

## Organização

- organização destrutiva desativada;
- `ScriptProjectOrganizer` apenas audita;
- nenhuma ferramenta deve mover, renomear ou apagar scripts automaticamente;
- `Assets/Brick Project Studio` não pode ser alterado;
- assets gerados ficam em `Assets/Generated/MiniMarket`.
- limpeza de referências cross-scene é manual, registra Undo, marca a cena como alterada e nunca salva automaticamente.

## Hierarquia esperada da loja Bronze

```text
Bronze_Market
├── BronzeMarketPurchaseLot
├── BUY_SYSTEM ou Buy_SystemShop
│   └── BuySceneController
│       ├── BuySceneCameraModeController
│       ├── PurchaseModeBridge
│       └── BuySceneLandPurchaseController
├── Buy_Area
│   ├── Collider sólido
│   └── BuySceneEntryTrigger_Runtime
│       ├── BoxCollider Trigger
│       ├── BuySceneEntryTrigger
│       ├── BuyScene_Entrada_Borda
│       ├── BuyScene_Entrada_Diagonal_A
│       └── BuyScene_Entrada_Diagonal_B
├── PurchaseLotArea ou marcador existente
│   ├── BuyableLandAreaMarker
│   ├── BuyScene_Borda_Terreno
│   ├── BuyScene_X_Diagonal_A
│   └── BuyScene_X_Diagonal_B
├── PurchaseCameraFocus
└── PurchaseLotStatus
    ├── Panel
    │   ├── StatusText
    │   └── PriceText
    └── HoverArrow
```

## Dados persistidos

- ID, nome, gold e gemas;
- stamina e porcentagem de energia;
- empresas e propriedades compradas;
- última cena, posição e rotação quando registradas;
- tempo jogado e datas de criação/atualização;
- cada loja Bronze usa um `idLote` diferente para ser comprada independentemente.

## Ferramentas principais

```text
Tools > MiniMarket > Materializar Todos os Objetos Runtime na Hierarquia
Tools > MiniMarket > Validar Objetos Runtime Persistentes

Tools > MiniMarket > Bronze Market > Preparar Todas as Lojas Bronze
Tools > MiniMarket > Bronze Market > Preparar Loja Bronze Selecionada
Tools > MiniMarket > Bronze Market > Gerar Novo ID para Loja Selecionada
Tools > MiniMarket > Bronze Market > Validar Lojas Bronze
Tools > MiniMarket > Bronze Market > Reconciliar Controladores e Visuais
```

## Riscos e verificações

1. Executar ferramentas somente fora do Play Mode e com a cena salva.
2. Confirmar zero erros vermelhos antes de executar ferramentas.
3. Fazer commit das alterações locais antes de `git pull`.
4. A raiz completa `Bronze_Market` deve ser duplicada, não apenas `Buy_Area`.
5. `Buy_Area` precisa permanecer dentro da raiz da loja e manter collider válido.
6. Cada loja precisa de ID exclusivo.
7. Cada controlador de compra deve listar apenas seu próprio marcador.
8. `procurarTerrenosAutomaticamente`, busca por proximidade e sincronização global devem permanecer desligados nos lotes Bronze.
9. O painel compartilhado de confirmação precisa preservar os filhos reconhecidos.
10. A compilação, posicionamento do status e comportamento visual dependem do Unity local.
11. Não usar `git reset --hard` ou `git clean -fd` sem backup.
12. Confirmar no Console que nenhum save `MMDB2` válido foi movido para `.corrupt_*.bak`.
13. Confirmar ausência de warnings de `ClosestPoint` ao interagir perto de malhas não convexas.

## Relatório específico

Consultar:

```text
Relatorios/BRONZE_MARKET_LOTES_INDEPENDENTES.md
```
