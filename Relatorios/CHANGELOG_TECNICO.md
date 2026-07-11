# Changelog técnico

Este arquivo registra mudanças que alteram arquitetura, persistência, contratos públicos, cena ou comportamento de gameplay.

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
