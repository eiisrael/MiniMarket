# Changelog técnico

Este arquivo registra mudanças que alteram arquitetura, persistência, contratos públicos, cena ou comportamento de gameplay.

## 2026-07-11 — Dados, stamina, interações e Mobile/Desktop

### Banco

- schema local elevado para V2;
- banco transformado em fonte única de verdade;
- persistência de nome, gold, gemas, stamina, energia segmentada, empresas, propriedades, posição e tempo jogado;
- gravação temporária, substituição atômica quando disponível e backup;
- recuperação por backup;
- migração do formato MMDB1 e das chaves PlayerPrefs antigas;
- singleton criado antes da cena e sem referências serializadas cross-scene;
- chave V2 não depende do identificador do aparelho.

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
- APIs públicas para joystick e botões mobile documentadas.

### Editor e cena

- criado reparo automático `GameplaySystemsSetup`;
- liga banco, stamina, HUD, interação e perfil mobile;
- adiciona realce a itens móveis;
- identifica portas/caixas por nome ou componente;
- remove guard obsoleto da cena;
- preserva conteúdo de Brick Project Studio.

### Documentação

- criada pasta `Relatorios`;
- criada regra obrigatória de leitura antes de futuras alterações;
- adicionados relatórios de dados, stamina, interação, plataformas e testes pós-pull.

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
