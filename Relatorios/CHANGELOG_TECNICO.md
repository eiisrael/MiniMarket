# Changelog técnico

Este arquivo registra mudanças que alteram arquitetura, persistência, contratos públicos, cena ou comportamento de gameplay.

## 2026-07-13 — Bronze_Market duplicável e compra isolada por loja

### Problema

- ao duplicar `Bronze_Market`, a cópia mantinha o mesmo ID persistente da loja original;
- apenas um `Buy_Area` recebia o trigger funcional;
- o reparo runtime ligava triggers e terrenos ao primeiro controlador global encontrado;
- `BuySceneLandPurchaseController` podia substituir referências locais por busca global;
- durante o modo aberto com `E`, era possível selecionar e comprar terrenos de outras lojas;
- objetos de status, trigger, terreno e linhas não possuíam uma hierarquia completa editável fora do Play.

### Autoridade por loja

- criado `Assets/Scripts/Purchasing/BronzeMarketPurchaseLot.cs`;
- cada raiz `Bronze_Market` passa a armazenar ID, nome, preço, `Buy_Area`, terreno, trigger, foco, controlador, ponte, compra e status próprios;
- referências são resolvidas dentro da própria hierarquia antes de qualquer fallback global;
- o `idLote` é aplicado ao `BuyableLandAreaMarker` para persistência independente no banco;
- cada trigger recebe somente o terreno pertencente à própria loja;
- busca automática por proximidade e busca global de terrenos ficam desligadas nos lotes Bronze.

### Seleção e confirmação

- `BuySceneLandPurchaseController` preserva o controlador local serializado;
- quando está dentro de `BronzeMarketPurchaseLot`, a lista de terrenos é forçada para exatamente um marcador local;
- hover, clique, seleção e confirmação rejeitam terrenos externos ao lote atual;
- comprar uma loja não altera o estado de outra cópia com ID diferente.

### Reparo runtime

- `PurchaseSystemBootstrapHost` separa lojas Bronze configuradas de objetos legados;
- lojas Bronze mantêm seus controladores locais;
- somente objetos legados usam o controlador global de fallback;
- todos os objetos nomeados `Buy_Area` podem receber seu próprio filho trigger, em vez de apenas um collider escolhido globalmente.

### Hierarquia e Editor

- criado `Assets/Editor/ProjectMaintenance/BronzeMarketPurchaseLotSetup.cs`;
- a ferramenta cria e salva `BuySceneEntryTrigger_Runtime`, marcador, foco e painel de status dentro de cada loja;
- duplicações da raiz são detectadas e recebem IDs exclusivos no formato `BRONZE_MARKET_XXXXXXXXXX`;
- criado `BronzeMarketLocalControllerReconciler.cs`, que prefere o `BuySceneController` já configurado e preserva os valores da câmera;
- controladores duplicados são desativados, não apagados;
- criado Inspector customizado `BronzeMarketPurchaseLotEditor`;
- ferramentas adicionadas em `Tools > MiniMarket > Bronze Market`.

### Visualização com E

- criado `Assets/Scripts/Purchasing/BronzeMarketLotStatusView.cs`;
- painel mundial persistente mostra `DISPONÍVEL`/`INDISPONÍVEL` e preço;
- somente o painel da loja que abriu seu controlador local fica visível;
- uma seta aparece e oscila ao passar o mouse sobre o terreno correto;
- outras lojas não recebem hover ou seleção durante o modo atual.

### Câmera

- `BuySceneCameraModeController` não foi reescrito;
- altura, rotação, ortográfico, zoom e transições existentes são preservados;
- o reconciliador escolhe preferencialmente o controlador existente em `BuySceneController`.

### Validação

- revisão estática concluída para trigger, controlador, marcador, ponte, bootstrap e banco;
- compilação e teste visual final dependem do Unity local;
- relatório: `BRONZE_MARKET_LOTES_INDEPENDENTES.md`.

## 2026-07-13 — Objetos runtime materializados na Hierarchy

### Problema

- diversos sistemas criavam GameObjects somente ao apertar Play;
- alterações de cor, posição, tamanho e referências feitas nesses objetos desapareciam ao pressionar Stop;
- energia, minimapa, HUD mobile, mira, hosts de compra, diagnóstico e perfil de renderização não possuíam uma hierarquia editável completa.

### Materializador

- criado `Assets/Editor/ProjectMaintenance/EditableRuntimeHierarchySetup.cs`;
- criado menu `Tools > MiniMarket > Materializar Todos os Objetos Runtime na Hierarquia`;
- criado menu `Tools > MiniMarket > Validar Objetos Runtime Persistentes`;
- a ferramenta usa Undo, salva a cena e não move nem apaga arquivos existentes;
- são materializados energia, minimapa, HUD mobile, mira, compra, diagnóstico, EventSystem e perfil de renderização;
- assets de suporte são criados somente em `Assets/Generated/MiniMarket`.

### Barra de energia

- `EnergyProgressArea` e `EnergyProgressFill` ficam salvos como filhos de `Energy`;
- criado Inspector customizado `MiniMarketEnergyProgressBarEditor`;
- `Cor Barra`, `Ancora Minima` e `Ancora Maxima` podem ser ajustados fora do Play Mode;
- botão do Inspector aplica preview imediatamente;
- somente `EnergyProgressFill` muda de largura durante o jogo.

### Minimapa persistente

- criado `RuntimeMiniMapHierarchyBinding`;
- câmera, Canvas, borda, máscara, `MapImage`, ponto do jogador e botões ficam salvos na cena;
- somente a `RenderTexture` permanece temporária em runtime;
- callbacks de zoom são ligados na inicialização;
- nenhum objeto visual precisa ser recriado ao entrar no Play.

### HUD mobile persistente

- criado `MobileControlsHierarchyBinding`;
- Canvas, SafeArea, área de olhar, joystick, thumb e seis botões ficam salvos na cena;
- callbacks touch são ligados durante a execução;
- reflexão é cacheada e usada somente na inicialização/eventos, nunca por frame;
- objetos permanecem editáveis fora do Play, mesmo que o Canvas seja ocultado no Desktop durante o jogo.

### Compra, mira e serviços

- controlador de mira e imagem `Mira` são persistidos quando ausentes;
- `PurchaseSystemRuntimeRepair`, controlador de compra, trigger e LineRenderers são materializados;
- criado material persistente `Assets/Generated/MiniMarket/Materials/BuyAreaLine.mat`;
- `RuntimeDiagnosticsPanel` fica salvo como host configurável;
- `PlatformRenderProfile` passa a reutilizar componente existente na cena antes de criar fallback runtime;
- criado reparo complementar para materializar o perfil de renderização.

### Validação

- contratos de `RuntimeMiniMap`, `MobileControlsHUD`, energia, compra, mira, diagnóstico e renderização revisados estaticamente;
- compilação e teste visual final dependem do Unity local;
- relatório: `OBJETOS_RUNTIME_PERSISTENTES.md`.

## 2026-07-13 — Correção da barra verde interna de Energy

### Problema da primeira implementação

- a própria imagem `Canvas > StaminaHUD > Energy` foi convertida em `Image.Type.Filled`;
- como resultado, o artwork `energy.png` inteiro diminuía como uma progress bar;
- o comportamento esperado era manter o objeto original estático e alterar apenas uma barra verde interna.

### Barra visual corrigida

- `MiniMarketEnergyProgressBar` mantém a imagem original de `Energy` como `Image.Type.Simple` e `fillAmount = 1`;
- criada a hierarquia `Energy/EnergyProgressArea/EnergyProgressFill`;
- somente a largura de `EnergyProgressFill` aumenta e diminui;
- `Background_Ene`, ícone, texto e artwork não são redimensionados;
- quando existe `Background_Ene`, somente o componente `Image` antigo de `Energy` pode ser ocultado para evitar duplicação, mantendo o GameObject e seus filhos ativos;
- o preenchimento representa a energia total;
- a posição interna da barra é ajustável por âncoras no Inspector;
- `MiniMarketEnergySegmentHUD` deixa de disputar a mesma imagem.

### Ferramenta do Editor

- `Tools > MiniMarket > Criar ou Reparar Barra de Energia` foi corrigido;
- a ferramenta não transforma mais `Energy` em `Filled`;
- cria e salva `EnergyProgressArea` e `EnergyProgressFill`;
- liga texto e `CameraRelativeMovement`;
- o validador acusa erro quando `Energy` ainda estiver como `Filled`.

### Desktop e Mobile

- a mesma fonte de stamina continua válida para as duas plataformas;
- animação usa `Time.unscaledDeltaTime`;
- nenhuma gravação em disco ocorre por frame.

### Validação

- contratos de movimento, banco e HUD revisados estaticamente;
- compilação e teste visual final dependem do Unity local;
- relatório: `CORRECAO_BARRA_ENERGIA_PROGRESSIVA.md`.

## 2026-07-12 — HUD, soltura segura, Buy_Area, minimapa e controles mobile

### Stamina e HUD

- `MiniMarketEnergySegmentHUD` passou a usar por padrão a carga do segmento ativo na barra principal;
- carga e descarga visual usam interpolação com tolerância de conclusão;
- barras segmentadas são detectadas por nomes específicos;
- quando não existem, cinco barras runtime são criadas abaixo da barra principal;
- a reserva de recarga pode preencher visualmente o próximo segmento;
- criado reparo explícito para localizar a barra correta no HUD sem reorganizar a cena.

### Interação física

- soltura comum e arremesso foram separados em `GetItemController`;
- liberar o botão de pegar ou sair da primeira pessoa faz queda segura;
- soltura comum não herda velocidade da transição da câmera por padrão;
- velocidades linear e angular são amortecidas e limitadas;
- somente a ação explícita de arremesso aplica força para frente.

### Entrada de compra

- `PurchaseSystemBootstrapHost` reconhece explicitamente `Buy_Area`;
- o collider sólido da calçada é preservado;
- um filho `BuySceneEntryTrigger_Runtime` recebe o collider trigger;
- borda e X visual são recriados acima do chão;
- controlador, painel, terrenos e jogador são religados;
- criado reparo de Editor seguro e manual para persistir a configuração na cena.

### Minimapa

- `RuntimeMiniMap` expõe posição, tamanhos Desktop/Mobile, margens, cores, botões, zoom, altura, camadas, resolução e sorting order;
- o componente pode ser salvo na cena e reutilizado pelo bootstrap runtime;
- adicionados menus de contexto para aplicar configurações e recriar o visual.

### Mira e click_on

- `FirstPersonReticleController` procura `click_off` e `click_on`;
- `click_on` é aplicado ao selecionar ou segurar um `GrabbableItem`;
- a mira continua oculta em terceira pessoa, menus e modo de compra;
- o reparo do Editor atribui os sprites pelo `AssetDatabase` quando encontrados.

### Mobile

- criado `MobileControlsHUD` com joystick, área de olhar, correr, pular, interagir, pegar/soltar, arremessar e mirar;
- o HUD aparece automaticamente apenas em Android/iOS;
- Desktop permanece oculto, com opção de teste forçado no Inspector;
- layout respeita `Screen.safeArea`;
- controladores de câmera receberam entrada touch externa sem remover teclado/mouse.

### Ferramentas e migração

- criado `Tools > Game Systems > Apply Gameplay Polish (HUD Grab Purchase MiniMap Mobile)`;
- criado `Tools > Game Systems > Validate Gameplay Polish`;
- as ferramentas não movem arquivos e não tocam em Brick Project Studio;
- relatório completo: `AJUSTES_HUD_INTERACAO_COMPRA_MINIMAP_MOBILE.md`.

### Validação

- contratos públicos e dependências foram revisados estaticamente;
- compilação, teste físico, UI e build Android devem ser confirmados no Unity local.

## 2026-07-11 — Recuperação de compra, minimapa, diagnósticos, energia e mira

### Causa raiz

- a organização automática anterior removeu/moveu sistemas ainda usados pela cena;
- `PlayerCameraController` desligava câmeras auxiliares com `RenderTexture`;
- o controlador da câmera do jogador disputava Transform/FOV com a compra;
- o HUD de energia procurava barras no nível errado;
- a restauração gratuita podia ser sobrescrita por eventos antigos;
- não existia autoridade única para visibilidade da mira.

### Compra de terrenos

- criado `PurchaseModeBridge` para entregar temporariamente o controle da câmera ao modo de compra;
- criado reparo runtime de controladores, painel, triggers, terrenos e linhas visuais;
- criado reparo seguro de cena;
- entrada da calçada continua exigindo collider válido.

### Minimapa

- criado `RuntimeMiniMap`, independente do minimapa legado;
- câmera ortográfica renderiza em `RenderTexture`;
- alvo oficial: `CameraRelativeMovement`;
- UI circular, tecla M e zoom.

### Diagnósticos

- criado `RuntimeDiagnosticsPanel`;
- F10 exibe desempenho, câmeras, banco, energia, movimento, compra e minimapa.

### Stamina e HUD

- HUD detecta barra principal e barras segmentadas;
- preenchimento visual animado;
- criado `FreeEnergyRestoreService`.

### Mira

- criado `FirstPersonReticleController`;
- mira apenas em primeira pessoa;
- oculta em terceira pessoa, menus e compra.

### Segurança da organização

- `ScriptProjectOrganizer` tornou-se somente auditoria;
- nenhuma organização automática move, renomeia ou apaga arquivos;
- Brick Project Studio continua ignorado.

### Testes locais obrigatórios

- compilação sem erros;
- linha da calçada visível e tecla E abrindo compra;
- seleção e painel de confirmação;
- minimapa e F10 funcionais;
- energia e mira validadas.

## 2026-07-11 — Correção de compilação pós-organização

### Economia

- `PlayerGold` movido definitivamente para `Assets/Scripts/Economy/PlayerGold.cs`;
- removido arquivo legado duplicado;
- GUID original preservado;
- corrigidos erros de classe/métodos duplicados.

### Editor e referências entre cenas

- `CrossSceneReferenceCleaner` executa alterações somente em Edit Mode estável;
- nenhuma chamada de edição de cena ocorre durante Play Mode ou transições;
- referências runtime do banco continuam não serializadas no menu.

### Migração local necessária

Se a organização antiga já tiver criado uma cópia local não rastreada de `PlayerGold`, apagar a cópia e seu `.meta` antes do próximo pull, após backup.

### Validação

- estrutura e dependências revisadas;
- compilação final depende do Unity local.

## 2026-07-11 — Dados, stamina, interações e Mobile/Desktop

### Banco

- schema local elevado para V2;
- banco transformado em fonte única de verdade;
- persistência de perfil, economia, stamina, empresas, propriedades, posição e tempo;
- gravação temporária, substituição atômica e backup;
- recuperação e migração de formatos antigos;
- singleton sem referência serializada cross-scene.

### Stamina e HUD

- stamina movida para `CameraRelativeMovement`;
- sincronização do banco com debounce;
- HUD convertido para eventos;
- fill, cor e sprites opcionais.

### Economia e perfil

- `PlayerGold` sincronizado com banco;
- `MiniMarketPlayerProfile` mantido como fachada de compatibilidade.

### Interações

- criados `InteractionHighlight`, `InteractiveObject` e `InteractionFocusController`;
- `GrabbableItem` integrado ao realce;
- `GetItemController` habilitado para primeira/terceira pessoa e mobile.

### Desktop e Mobile

- criado `PlatformRenderProfile`;
- perfis Desktop, Mobile e Low-End Mobile;
- render scale, FPS, sombras, luzes, MSAA, LOD e efeitos ajustados por plataforma.

### Editor e cena

- criados reparo e validador de arquitetura;
- liga banco, stamina, HUD e interação;
- preserva Brick Project Studio.

### Documentação

- criada pasta `Relatorios` e `AGENTS.md`;
- adicionados relatórios e checklist.

### Validação

- revisão estática concluída;
- compilação e execução final dependem do Unity local.

## 2026-07-11 — Jogador e Animator

- substituição progressiva dos sistemas antigos pelo `PlayerCameraController` e `CameraRelativeMovement`;
- correção de variáveis `out` não atribuídas;
- reparo seguro de referências cross-scene;
- suporte ampliado a parâmetros e estados de Animator;
- adicionada ferramenta `Repair Player Animator`.

## Política para próximas entradas

Toda nova entrada deve informar:

- data;
- área alterada;
- arquivos principais;
- migração necessária;
- impacto em Desktop/Mobile;
- testes executados;
- riscos ou pendências.
