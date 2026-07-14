# Changelog técnico

Este arquivo registra mudanças que alteram arquitetura, persistência, contratos públicos, cena ou comportamento de gameplay.

## 2026-07-14 — Toda a estrutura do jornal editável durante Play e persistente no Stop

### Problema

- o Unity normalmente descarta alterações feitas no Inspector ao sair do Play Mode;
- apenas a escala da raiz `Newspaper_PlacePrompt` possuía proteção específica;
- posições, rotações, tamanhos, âncoras, cores, textos, transparências e opções dos filhos voltavam aos valores anteriores ao pressionar Stop.

### Correção

O arquivo:

```text
Assets/Editor/ProjectMaintenance/NewspaperPlacePromptScalePersistence.cs
```

passou a conter `NewspaperPlayModeHierarchyPersistence`.

Comportamento:

- observa modificações manuais registradas pelo Undo do Editor durante o Play;
- reconhece objetos/componentes existentes sob `Newspaper_Stand`, `Jornal_Place`, `Put_Area`, prompts, linhas e `Placed_Newspaper_Runtime`;
- registra o alvo por cena, caminho de sibling, tipo e índice do componente;
- reaplica somente as propriedades realmente editadas pelo usuário ao voltar para Stop;
- suporta `Transform`, `RectTransform`, campos serializados, UI, textos, cores, transparências, LineRenderer e referências persistentes;
- mantém a última alteração de cada propriedade quando o mesmo campo é editado várias vezes;
- marca a cena como modificada e deixa o salvamento final para `Ctrl+S`;
- não copia alterações automáticas do gameplay, pois elas não passam pelo Undo do Editor;
- não possui `Update`, busca por frame ou gravação em disco por frame;
- mantém a proteção da escala-base e desativa em memória o reparo legado do `Newspaper_PlacePrompt`.

### Limite intencional

- persistem propriedades de objetos e componentes que já existiam antes do Play;
- criação, exclusão, reordenação, troca de parent ou adição/remoção de componentes deve continuar sendo feita fora do Play Mode.

### Arquitetura e impacto

- `NewspaperWorldPromptVisual` continua sendo a autoridade visual;
- nenhum controlador de gameplay paralelo foi criado;
- a função existe somente no Editor e não entra em builds Desktop/Mobile;
- nenhuma cena, prefab ou conteúdo de `Assets/Brick Project Studio` foi alterado.

### Validação

- revisão estática concluída;
- após o pull, editar vários objetos da estrutura durante o Play, pressionar Stop e confirmar os valores aplicados;
- salvar com `Ctrl+S` depois do Stop;
- compilação e execução final dependem do Unity local.

## 2026-07-14 — Escala do Newspaper_PlacePrompt preservada no Play/Stop

### Problema

- a escala editada diretamente no `Transform` da raiz `Newspaper_PlacePrompt` podia ser normalizada novamente pelo reparo legado;
- ao sair do Play Mode, a escala visual voltava para um valor anterior, principalmente quando estava abaixo do limite antigo de migração.

### Correção

Foi criado:

```text
Assets/Editor/ProjectMaintenance/NewspaperPlacePromptScalePersistence.cs
```

Comportamento:

- captura os três eixos de escala imediatamente antes do Play Mode;
- desativa em memória o reparo legado de escala para prompts da `Put_Area`;
- restaura a escala exata depois do Stop somente quando ela foi alterada por código;
- preserva escalas uniformes e não uniformes;
- não usa `Update`, não realiza busca por frame e não salva a cena automaticamente;
- não modifica posição, rotação, tamanho, filhos, cores ou transparências;
- inclui `.meta` com GUID persistente.

### Arquitetura e impacto

- `NewspaperWorldPromptVisual` permanece como autoridade visual;
- não foi criado outro controlador de prompt ou gameplay;
- a correção é exclusiva do Editor e não altera comportamento Desktop/Mobile em build;
- nenhum arquivo de cena, prefab ou conteúdo de `Assets/Brick Project Studio` foi alterado.

### Validação

- revisão estática concluída;
- teste final deve ser feito no Unity local: editar `Transform > Scale`, salvar com `Ctrl+S`, entrar no Play e pressionar Stop;
- a escala deve permanecer idêntica nos eixos X, Y e Z.

## 2026-07-14 — Porta em terceira pessoa e jornal sem travada de interação

### Contexto revisado

Antes da alteração foram conferidos:

- `AGENTS.md`;
- `Relatorios/README.md`;
- `Relatorios/ESTADO_ATUAL.md`;
- `Relatorios/INTERACOES.md`;
- `Relatorios/OBJETOS_RUNTIME_PERSISTENTES.md`;
- scripts ativos de câmera, interação, banco e jornal.

As autoridades existentes foram mantidas. Nenhum controlador paralelo de câmera, porta, inventário ou prompt foi criado.

### Porta legada

Arquivos principais:

```text
Assets/Scripts/Interaction/InteractiveObject.cs
Assets/Scripts/Interaction/InteractionFocusController.cs
```

Alterações:

- `InteractiveObject` continua priorizando métodos públicos conhecidos;
- quando o script legado não possui método, o componente alterna campo/propriedade booleana pública em cache, incluindo `Open`;
- existe fallback final para parâmetro booleano do `Animator`;
- resolução ocorre uma vez e reflexão não roda por frame;
- interação em terceira pessoa usa proximidade com `Collider.ClosestPoint`;
- a direção da câmera e do personagem participam da escolha;
- ao pressionar `E`/INTERACT, uma busca ampliada é executada somente naquele pedido;
- itens `GrabbableItem` continuam excluídos;
- paredes permanecem bloqueando interação, com tolerância limitada para moldura fina e porta muito próxima.

Impacto Desktop/Mobile:

- tecla `E`, clique e `RequestInteract()` compartilham a mesma autoridade;
- a correção não depende de mira em terceira pessoa;
- primeira pessoa continua usando o raycast central.

### Microtravamento ao pegar/colocar jornal

Arquivos principais:

```text
Assets/Scripts/Newspaper/MiniMarketNewspaperInventoryService.cs
Assets/Scripts/Newspaper/NewspaperStandController.cs
Assets/Scripts/Newspaper/NewspaperPlacementAreaController.cs
```

Causa identificada:

- quantidade e local eram gravados de forma síncrona e repetida no banco criptografado;
- o mesmo frame ainda podia executar buscas recursivas, criação de componentes, sanitização completa e `Instantiate`;
- referências e renderers eram procurados repetidamente no loop.

Correção:

- alterações de quantidade e local são agrupadas em uma gravação após o frame de interação;
- o banco principal continua sendo a fonte de verdade;
- gravações pendentes são concluídas em pause, perda de foco, quit e destroy;
- referências do expositor são resolvidas somente quando ausentes;
- `Placed_Newspaper_Runtime` é preparado antes do clique;
- renderers do guia, física e componentes sanitizados ficam cacheados;
- a interação normal apenas ativa/desativa o objeto persistente.

### Newspaper_PlacePrompt

Arquivos principais:

```text
Assets/Scripts/Newspaper/NewspaperWorldPromptVisual.cs
Assets/Scripts/Newspaper/NewspaperPlacementAreaController.cs
Assets/Editor/ProjectMaintenance/MiniMarketNewspaperPlacePromptPersistence.cs
```

Alterações:

- mesmo design circular, nomes e hierarquia foram preservados;
- layout completo não é mais aplicado a cada `LateUpdate`;
- `CircularPrompt`, `Instruction` e filhos podem usar seus próprios RectTransforms e estilos;
- transparência total ficou disponível em `Visible Opacity`;
- animações de posição e escala ficam desligadas por padrão;
- animações habilitadas trabalham relativamente ao transform editado;
- billboard usa rotação vertical e não aponta para céu/chão;
- câmera oficial de `PlayerCameraController` é priorizada;
- escala minúscula e inclinação 3D de versões antigas são corrigidas uma única vez;
- reparo do Editor permanece manual para não reabrir o asterisco da cena após `Ctrl+S`;
- reconstrução por menu preserva RectTransforms, textos, cores e estilos reconhecidos.

### Migração

- não há migração destrutiva;
- não foram alterados arquivos de cena, prefabs ou `Assets/Brick Project Studio`;
- campos novos usam valores padrão compatíveis;
- para persistir a correção visual na cena, pode ser executado manualmente `Tools > MiniMarket > Jornal > Reparar Prompt da Put Area`, seguido de `Ctrl+S`.

### Validação

- revisão estática de contratos e referências concluída;
- checklist atualizado em `Relatorios/TESTES_POS_GIT_PULL.md`;
- compilação, perfil de frame e comportamento visual final dependem do Unity local.

## 2026-07-14 — Estabilidade do banco e objetos persistentes do jornal

- criação de banco durante `OnValidate` foi bloqueada;
- referências runtime ao banco deixam de ser serializadas entre cena e `DontDestroyOnLoad`;
- arquivo inválido pode ser isolado antes da inicialização;
- instaladores automáticos do jornal foram convertidos em manutenção manual/idempotente;
- `Newspaper_PlacePrompt` e `Placed_Newspaper_Runtime` passaram a existir como objetos persistentes editáveis;
- `Instruction` recebeu textos de estado editáveis e perfil persistente;
- `Ctrl+S` não deve ser invalidado por reconciliadores automáticos.

## 2026-07-13 — Bronze_Market duplicável e compra isolada por loja

### Autoridade por loja

- criado `BronzeMarketPurchaseLot`;
- cada raiz `Bronze_Market` armazena ID, nome, preço, `Buy_Area`, terreno, trigger, foco, controlador, ponte, compra e status próprios;
- referências locais têm prioridade sobre fallback global;
- duplicações recebem IDs exclusivos;
- trigger e controlador listam somente o terreno da própria loja.

### Seleção e confirmação

- hover, clique, seleção e confirmação rejeitam terrenos externos ao lote atual;
- comprar uma loja não altera cópias com ID diferente;
- apenas o painel da loja ativa aparece no modo de compra.

### Editor

- adicionadas ferramentas de preparação, reconciliação e validação de lojas Bronze;
- o controlador existente `BuySceneController` é preferido;
- controladores duplicados são desativados, não apagados;
- layout, escala, posição, cores, fontes e filhos copiados são preservados.

### Câmera

- `BuySceneCameraModeController` continua sendo a autoridade;
- altura, rotação, ortográfico, zoom e transições existentes são preservados.

## 2026-07-13 — Objetos runtime materializados na Hierarchy

- criado `EditableRuntimeHierarchySetup`;
- energia, minimapa, HUD mobile, mira, compra, diagnóstico e perfil de renderização passaram a possuir hosts/visuais salvos na cena;
- somente recursos realmente temporários, como `RenderTexture` e callbacks, continuam runtime;
- assets gerados ficam em `Assets/Generated/MiniMarket`;
- ferramentas usam `Undo`, não movem scripts e não alteram Brick Project Studio.

### Energia

- `Energy` permanece como artwork estático;
- `EnergyProgressArea/EnergyProgressFill` controla somente a barra interna;
- texto percentual, sprites por faixa, degradê e pulsação foram adicionados;
- o Inspector permite editar área e cor fora do Play.

### Minimapa e mobile

- câmera, Canvas, máscara, botões e ponto do minimapa ficam persistentes;
- HUD mobile e SafeArea ficam persistentes;
- bindings conectam a hierarquia ao gameplay sem reflexão por frame.

## 2026-07-12 — HUD, interação física, compra, minimapa e mobile

- stamina segmentada e HUD receberam interpolação e reparo de referências;
- soltura comum foi separada do arremesso;
- `Buy_Area` passou a manter collider sólido e trigger filho;
- minimapa oficial foi exposto para configuração Desktop/Mobile;
- `FirstPersonReticleController` passou a usar `click_off`/`click_on`;
- criado `MobileControlsHUD` com movimento, olhar, corrida, pulo, interação, grab, throw e aim;
- ferramentas de gameplay polish e validação foram adicionadas.

## 2026-07-11 — Recuperação de compra, minimapa, diagnósticos, energia e mira

- criado `PurchaseModeBridge` para entrega temporária da câmera ao modo de compra;
- controladores, painel, triggers, terrenos e linhas visuais foram recuperados;
- criado `RuntimeMiniMap` independente do minimapa legado;
- criado `RuntimeDiagnosticsPanel` no F10;
- HUD de energia e mira em primeira pessoa foram restaurados;
- `ScriptProjectOrganizer` tornou-se somente auditoria.

## 2026-07-11 — Correção de compilação pós-organização

- `PlayerGold` foi consolidado em `Assets/Scripts/Economy/PlayerGold.cs` com GUID preservado;
- duplicações de classe/métodos foram removidas;
- limpeza de referências cross-scene foi limitada ao Edit Mode estável;
- nenhuma edição de cena deve ocorrer durante transições de Play Mode.

## 2026-07-11 — Dados, stamina, interações e Desktop/Mobile

### Banco

- schema elevado para V2;
- `MiniMarketPlayerDatabase` tornou-se fonte única de verdade;
- persistência cobre perfil, economia, energia, empresas, propriedades, posição e tempo;
- gravação usa temporário, substituição atômica e backup;
- formatos antigos podem ser recuperados/migrados.

### Gameplay

- movimento consolidado em `CameraRelativeMovement`;
- câmera consolidada em `PlayerCameraController`;
- criados `InteractionHighlight`, `InteractiveObject` e `InteractionFocusController`;
- `GrabbableItem` foi integrado ao realce;
- `GetItemController` passou a compartilhar lógica Desktop/Mobile.

### Plataforma

- criado `PlatformRenderProfile`;
- adicionados perfis Desktop, Mobile e Low-End Mobile;
- render scale, FPS, sombras, luzes, MSAA, LOD e efeitos são ajustáveis por plataforma.

## 2026-07-11 — Jogador e Animator

- substituição progressiva de sistemas antigos por `PlayerCameraController` e `CameraRelativeMovement`;
- correção de variáveis `out` não atribuídas;
- reparo seguro de referências cross-scene;
- suporte ampliado a parâmetros/estados do Animator;
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
