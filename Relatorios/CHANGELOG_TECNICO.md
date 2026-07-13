# Changelog técnico

Este arquivo registra mudanças que alteram arquitetura, persistência, contratos públicos, cena ou comportamento de gameplay.

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
- criada a hierarquia `Energy/ EnergyProgressArea/ EnergyProgressFill`;
- somente a largura de `EnergyProgressFill` aumenta e diminui;
- a cor permanece verde durante descarga e recuperação;
- `Background_Ene`, ícone, texto e artwork não são redimensionados;
- quando existe `Background_Ene`, somente o componente `Image` antigo de `Energy` pode ser ocultado para evitar duplicação, mantendo o GameObject e seus filhos ativos;
- o preenchimento continua representando a energia total segmentada entre `0/5` e `5/5`;
- a posição interna da barra é ajustável por âncoras no Inspector;
- `MiniMarketEnergySegmentHUD` deixa de disputar a mesma imagem.

### Ferramenta do Editor

- `Tools > MiniMarket > Criar ou Reparar Barra de Energia` foi corrigido;
- a ferramenta não transforma mais `Energy` em `Filled`;
- cria e salva `EnergyProgressArea` e `EnergyProgressFill`;
- liga `Txt_Qtd` e `CameraRelativeMovement`;
- `Tools > MiniMarket > Validar Barra de Energia` acusa erro quando `Energy` ainda estiver como `Filled`.

### Desktop e Mobile

- a mesma fonte de stamina segmentada continua válida para as duas plataformas;
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

- `RuntimeMiniMap` agora expõe no Inspector posição, tamanhos Desktop/Mobile, margens, cores, botões, zoom, altura, camadas, resolução e sorting order;
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
- `PlayerCameraController`, `FirstPersonCamera` e `ThirdPersonCamera` receberam entrada touch externa sem remover teclado/mouse.

### Ferramentas e migração

- criado `Tools > Game Systems > Apply Gameplay Polish (HUD Grab Purchase MiniMap Mobile)`;
- criado `Tools > Game Systems > Validate Gameplay Polish`;
- as ferramentas não executam automaticamente, não movem arquivos e não tocam em Brick Project Studio;
- relatório completo: `AJUSTES_HUD_INTERACAO_COMPRA_MINIMAP_MOBILE.md`.

### Validação

- contratos públicos e dependências foram revisados estaticamente no repositório;
- compilação, teste físico, UI e build Android devem ser confirmados no Unity local.

## 2026-07-11 — Recuperação de compra, minimapa, diagnósticos, energia e mira

### Causa raiz

- a organização automática anterior removeu/moveu sistemas ainda usados pela cena;
- `PlayerCameraController` desligava todas as câmeras auxiliares, inclusive câmeras com `RenderTexture`;
- o controlador da câmera do jogador disputava o Transform/FOV com o modo de compra;
- o HUD de energia procurava barras apenas nos filhos do texto, sem alcançar imagens irmãs;
- a restauração gratuita podia ser sobrescrita por eventos antigos no mesmo frame;
- não existia uma autoridade única para a visibilidade da mira.

### Compra de terrenos

- criado `PurchaseModeBridge` para entregar temporariamente o controle da câmera ao modo de compra;
- criado reparo runtime de controladores, painel, triggers, terrenos e linhas visuais;
- criado reparo seguro de cena em `Tools > Game Systems > Repair Purchase Minimap Diagnostics Energy Reticle`;
- entrada da calçada continua exigindo `BuySceneEntryTrigger` em um collider válido; nenhum trigger é criado em posição arbitrária.

### Minimapa

- criado `RuntimeMiniMap`, independente do minimapa legado;
- câmera ortográfica renderiza em `RenderTexture` e é preservada pelo controlador principal;
- busca `CameraRelativeMovement` como alvo;
- possui UI circular, ponto central, tecla M, zoom e perfil Desktop/Mobile.

### Diagnósticos

- criado `RuntimeDiagnosticsPanel`, inicializado automaticamente;
- F10 exibe desempenho, câmeras, AudioListeners, banco, energia, movimento, compra e minimapa;
- removida dependência dos diagnósticos antigos da Camera V2.

### Stamina e HUD

- HUD detecta barra principal e múltiplas barras segmentadas no container visual;
- preenchimento visual é animado;
- progresso da reserva passa a preencher visualmente o próximo segmento;
- criado `FreeEnergyRestoreService`, que restaura banco, movimento e HUD em sequência autoritativa.

### Mira

- criado `FirstPersonReticleController`;
- mira é exibida apenas em primeira pessoa;
- mira fica oculta em terceira pessoa, menus e modo de compra.

### Segurança da organização

- `ScriptProjectOrganizer` tornou-se somente auditoria;
- nenhuma organização automática move, renomeia, apaga arquivos, cenas ou componentes;
- Brick Project Studio continua ignorado.

### Impacto Desktop/Mobile

- sistemas runtime são compatíveis com teclado/mouse e inicialização mobile;
- minimapa usa resolução menor no mobile;
- nenhum layout touch existente foi removido.

### Testes locais obrigatórios

- compilação sem erros;
- linha da calçada visível e tecla E abrindo o modo de compra;
- seleção e painel de confirmação dos terrenos;
- minimapa visível e tecla M funcional;
- F10 funcional;
- energia grátis permanece cheia após alguns segundos;
- barras do HUD carregam visualmente;
- mira oculta em terceira pessoa e visível somente em primeira pessoa.

## 2026-07-11 — Correção de compilação pós-organização

### Economia

- `PlayerGold` movido definitivamente para `Assets/Scripts/Economy/PlayerGold.cs`;
- removido o arquivo legado duplicado `Assets/Scripts/Player_Gold.cs`;
- GUID original do MonoScript preservado para não perder referências de cena e prefab;
- corrigidos os erros `CS0101` e `CS0111` causados por duas definições da classe `PlayerGold`.

### Editor e referências entre cenas

- `CrossSceneReferenceCleaner` agora executa alterações somente em Edit Mode estável;
- nenhuma chamada a `SerializedObject`, `MarkSceneDirty` ou `SaveScene` ocorre durante Play Mode ou suas transições;
- referências runtime do banco continuam não serializadas no menu.

### Migração local necessária

Se a organização antiga já tiver criado uma cópia local não rastreada em `Assets/Scripts/Economy/PlayerGold.cs`, apagar essa cópia e seu `.meta` antes do próximo `git pull`. O Git baixará a versão canônica preservando o GUID correto.

### Validação

- estrutura e dependências revisadas no repositório;
- compilação final deve ser confirmada no Unity local depois da limpeza da cópia duplicada.

## 2026-07-11 — Dados, stamina, interações e Mobile/Desktop

### Banco

- schema local elevado para V2;
- banco transformado em fonte única de verdade;
- persistência de nome, gold, gemas, stamina, energia segmentada, empresas, propriedades, posição e tempo jogado;
- gravação temporária, substituição atômica quando disponível e backup;
- recuperação por backup;
- migração do formato MMDB1 e das chaves PlayerPrefs antigas;
- singleton criado antes da cena e sem referências serializadas cross-scene;
- chave V2 não depende do identificador do aparelho;
- inicialização das variáveis de migração corrigida em todos os caminhos de execução.

### Stamina e HUD

- stamina segmentada movida para `CameraRelativeMovement`;
- removido guard runtime antigo baseado em reflexão;
- sincronização do banco com debounce e eventos de pausa/saída;
- HUD convertido para atualização por eventos;
- fill, cor e sprites opcionais;
- menu passou a ler a fonte atual sem reflexão.

### Economia e perfil

- `PlayerGold` sincronizado com o banco e protegido contra referências nulas;
- `MiniMarketPlayerProfile` mantido como fachada de compatibilidade;
- reset de empresas não reseta mais todo o perfil.

### Interações

- criado `InteractionHighlight` com MaterialPropertyBlock;
- criado `InteractiveObject` para portas e mecanismos;
- criado `InteractionFocusController` para primeira e terceira pessoa;
- `GrabbableItem` integrado ao realce;
- `GetItemController` habilitado para primeira/terceira pessoa e entrada mobile;
- compatibilidade opcional com métodos antigos de porta/interação.

### Desktop e Mobile

- criado `PlatformRenderProfile`;
- perfis Desktop, Mobile e Low-End Mobile;
- render scale, FPS, sombras, luzes, MSAA, LOD e custo de efeitos ajustados por plataforma;
- perfil reaplicado após o carregamento da cena para impedir sobrescrita por sistemas legados;
- APIs públicas para joystick e botões mobile documentadas.

### Editor e cena

- criado reparo automático `GameplaySystemsSetup`;
- criado `GameplaySystemsAutoRepair` para reaplicar a ligação após recriação do PlayerCameraRig;
- criado `GameplayArchitectureValidator` sem efeitos colaterais;
- liga banco, stamina, HUD e interação;
- adiciona realce a itens móveis;
- identifica portas/caixas por nome ou componente;
- remove guard obsoleto da cena;
- preserva conteúdo de Brick Project Studio.

### Documentação

- criada pasta `Relatorios`;
- criada regra obrigatória de leitura antes de futuras alterações;
- criado `AGENTS.md` na raiz;
- adicionados relatórios de dados, stamina, interação, plataformas e testes pós-pull.

### Validação

- revisão estática de referências públicas e caminhos de inicialização concluída;
- compilação e execução final dependem do Unity local do projeto;
- checklist manual registrado em `TESTES_POS_GIT_PULL.md`.

## 2026-07-11 — Jogador e Animator

- substituição progressiva dos sistemas antigos pelo `PlayerCameraController` e `CameraRelativeMovement`;
- correção de variáveis `out` não atribuídas no controlador da câmera;
- reparo de referências cross-scene seguro durante transições do Play Mode;
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
