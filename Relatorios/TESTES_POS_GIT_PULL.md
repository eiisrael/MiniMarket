# Testes após Git Pull

Atualizado em: 2026-07-15

Execute este checklist depois de qualquer atualização relevante.

## 1. Proteger alterações locais e atualizar

Antes do pull:

```bash
cd ~/Desktop/MiniMarket/MiniMarket
git status
git add -A
git commit -m "chore(scene): save local Unity adjustments"
git pull --rebase origin main
git status
git log --oneline -20
```

Não usar `git reset --hard` ou `git clean -fd` sem backup e sem conferir os arquivos locais.

## 2. Compilação

1. Sair do Play Mode.
2. Aguardar importação e compilação.
3. Abrir Console.
4. Clicar em Clear.
5. Confirmar zero erros vermelhos.
6. Não executar ferramentas enquanto houver erro de compilação.

## 3. Regressão imediata de 2026-07-14

### Edição completa do jornal durante Play Mode

1. Salvar a cena antes do teste.
2. Entrar no Play Mode.
3. Alterar `Position`, `Rotation` e `Scale` de `Newspaper_PlacePrompt`.
4. Alterar `RectTransform` de `CircularPrompt`, `Instruction` e pelo menos um anel.
5. Alterar cor/transparência de uma `Image`.
6. Alterar texto, tamanho da fonte ou contorno de `Instruction`.
7. Alterar uma opção serializada de `NewspaperWorldPromptVisual`.
8. Alterar largura ou cor de um `LineRenderer` da Put Area.
9. Alterar Transform de `Placed_Newspaper_Runtime`.
10. Pressionar Stop.
11. Confirmar que todas as alterações manuais continuam visíveis na cena.
12. Confirmar que a cena fica com asterisco somente porque existem alterações reais reaplicadas.
13. Confirmar no Console a mensagem `[NewspaperPlayEdit]` informando alterações aplicadas.
14. Salvar com `Ctrl+S`, fechar e reabrir a cena.
15. Confirmar que os valores continuam iguais.
16. Repetir entrando e saindo do Play sem editar nada; a cena não deve receber alterações novas.
17. Confirmar que animação, billboard, progresso e visibilidade automáticos do gameplay não viram valores permanentes.
18. Criar/apagar/reordenar objetos somente fora do Play; esse fluxo não é responsabilidade do persistidor.

### Porta em terceira pessoa

1. Iniciar Play usando a câmera principal em terceira pessoa.
2. Aproximar-se de `Ext_Door_01` sem centralizar a mira nela.
3. Pressionar `E` uma vez.
4. Confirmar que o campo legado `Open` alterna e a porta abre/fecha.
5. Confirmar que um único pressionamento não executa a ação duas vezes.
6. Repetir com clique quando a porta estiver focada.
7. Repetir em primeira pessoa.
8. Colocar uma parede real entre jogador e porta e confirmar que a interação é bloqueada.
9. Confirmar ausência de spam, exceções de reflexão e `MissingReferenceException`.

### Jornal — desempenho

1. Aproximar-se do `Newspaper_Stand`.
2. Segurar `E` até concluir a coleta.
3. Confirmar que não existe travada perceptível no exato frame da coleta.
4. Ir até `Put_Area` e pressionar `E`.
5. Confirmar que não existe travada perceptível no exato frame da colocação.
6. Repetir coleta e colocação três vezes.
7. Confirmar que o banco grava depois da interação e que quantidade/local persistem após Stop/Play.
8. Confirmar que não são criadas cópias extras de `Placed_Newspaper_Runtime`.

### Newspaper_PlacePrompt

1. Fora do Play, selecionar `Newspaper_PlacePrompt` e todos os filhos.
2. Alterar levemente posição, escala ou tamanho de `CircularPrompt` e `Instruction`.
3. Salvar com `Ctrl+S`, entrar e sair do Play.
4. Confirmar que os ajustes permanecem.
5. No Play, aproximar o personagem da Put_Area.
6. Confirmar que o prompt está de frente para o jogador/câmera e permanece vertical.
7. Confirmar que não aponta para o céu nem para o chão.
8. Confirmar que o design continua circular e sem painel quadrado.
9. Alterar `Visible Opacity` e confirmar a transparência total.
10. Alterar a cor/transparência de um filho e confirmar que o script não sobrescreve por frame.
11. Confirmar que `CircularPrompt` não volta sozinho para outra rotação/escala.

## 4. Materializar objetos editáveis

Com a `SampleScene` aberta, salva e fora do Play Mode:

```text
Tools > MiniMarket > Materializar Todos os Objetos Runtime na Hierarquia
Tools > MiniMarket > Validar Objetos Runtime Persistentes
```

Depois confirmar `Erros=0`, salvar com `Ctrl + S` e reabrir a cena.

Os reparos do jornal são manuais e só devem ser executados quando necessário:

```text
Tools > MiniMarket > Jornal > Configurar Sistema Automaticamente
Tools > MiniMarket > Jornal > Reparar Prompt da Put Area
Tools > MiniMarket > Jornal > Reconciliar Jornal Colocado Persistente
```

## 5. Preparar as lojas Bronze

Executar fora do Play Mode:

```text
Tools > MiniMarket > Bronze Market > Preparar Todas as Lojas Bronze
Tools > MiniMarket > Bronze Market > Reconciliar Controladores e Visuais
Tools > MiniMarket > Bronze Market > Validar Lojas Bronze
```

O validador deve terminar com:

```text
erros=0
```

## 6. Hierarquia mínima da Bronze_Market

Confirmar em cada raiz:

```text
Bronze_Market
├── BronzeMarketPurchaseLot
├── controlador local de câmera/compra
├── Buy_Area
│   └── BuySceneEntryTrigger_Runtime
├── PurchaseLotArea ou marcador existente
├── PurchaseCameraFocus
└── PurchaseLotStatus
```

Verificar:

- `Buy_Area` pertence à própria loja;
- o collider sólido continua `Is Trigger = false`;
- o BoxCollider do filho runtime está `Is Trigger = true`;
- `Terrenos Desta Area` possui somente o marcador da própria loja;
- `Usar Terrenos Proximos Se Lista Vazia` está desligado;
- `Sincronizar Com Terrenos Encontrados Automaticamente` está desligado;
- `Procurar Terrenos Automaticamente` está desligado no controlador local;
- `Id Lote` e `Id Persistente` correspondem;
- apenas um controlador de câmera permanece habilitado na loja.

## 7. Testar duplicação da Bronze_Market

1. Selecionar a raiz completa `Bronze_Market`.
2. Duplicar com `Ctrl + D` ou copiar/colar.
3. Aguardar a atualização da Hierarchy.
4. Mover a cópia para outro local.
5. Selecionar o componente `BronzeMarketPurchaseLot` da cópia.
6. Confirmar que o `Id Lote` é diferente do original.
7. Confirmar que referências da cópia apontam para filhos da própria cópia.
8. Salvar com `Ctrl + S`.
9. Executar `Validar Lojas Bronze` novamente.

## 8. Testar compra isolada

### Loja A

- entrar apenas no quadrado/X da Loja A;
- pressionar `E`;
- a câmera deve manter o mesmo movimento e configuração anteriores;
- somente `PurchaseLotStatus` da Loja A deve aparecer;
- passar o mouse no lote A deve mostrar/mover a seta;
- lote B não pode receber hover;
- clicar fora do lote A não pode abrir confirmação;
- clicar no lote A deve abrir o painel correto;
- comprar A deve registrar somente o ID da Loja A.

### Loja B

Repetir o teste na cópia:

- `E` da Loja B mostra apenas Loja B;
- clique em A é ignorado durante a visualização de B;
- compra de B usa seu próprio preço e ID;
- comprar A não torna B indisponível;
- é possível comprar A e B quando há Gold suficiente.

### Saída

- sair do modo restaura câmera, movimento e cursor;
- nenhum controlador de outra loja permanece ativo;
- Console não apresenta spam por frame.

## 9. Visual e edição fora do Play

### Compra Bronze

- `PurchaseLotStatus`, textos e seta existem antes do Play;
- cores, posição, escala e textos podem ser editados no Inspector;
- LineRenderers da calçada e terreno aparecem na Hierarchy;
- material persistente está em `Assets/Generated/MiniMarket/Materials/BuyAreaLine.mat`;
- Stop não remove os objetos configuráveis.

### Jornal

- `Newspaper_InteractionPrompt`, `Newspaper_PlacePrompt` e `Placed_Newspaper_Runtime` existem antes do Play;
- `CircularPrompt`, `Instruction`, anéis, brilhos, disco e textos são selecionáveis;
- Stop não remove os objetos;
- propriedades alteradas manualmente durante o Play são reaplicadas no Stop;
- estados automáticos do gameplay não são persistidos;
- `Ctrl+S` salva os valores reaplicados;
- o prompt da Put_Area fica vertical e visível apenas quando oferecido ao jogador;
- posição/escala dos filhos não são redefinidas a cada frame.

### Energia

- texto mostra `100%`, `60%`, `25%` e inclui `%`;
- ícone alterna entre `green_energy`, `yellow_energy` e `red_energy`;
- progress bar faz transição suave de cor;
- ícone pulsa ao correr/pressionar Shift;
- artwork original de `Energy` permanece estático.

### Minimapa e mobile

- hierarquias visuais permanecem salvas;
- somente RenderTexture/callbacks transitórios são criados no Play;
- HUD mobile permanece editável fora do Play.

## 10. Banco

1. Faça uma cópia externa de `player_database.mmdb`.
2. Alterar gold e nome.
3. Comprar duas lojas Bronze com IDs diferentes.
4. Coletar e colocar um jornal.
5. Parar Play.
6. Iniciar novamente.
7. Confirmar que gold, nome e as duas propriedades continuam corretos.
8. Confirmar que quantidade e local do jornal continuam corretos.
9. Parar e iniciar Play mais uma vez com o mesmo save `MMDB2`.
10. Confirmar que não surgiu arquivo `player_database.mmdb.corrupt_*.bak`.
11. Confirmar que uma terceira cópia de loja com ID novo continua disponível.
12. Confirmar ausência de referência cross-scene inválida.
13. Confirmar que não há múltiplas gravações no mesmo frame da coleta/colocação.
14. No painel de diagnósticos, confirmar que banco, gold, stamina e empresas refletem o mesmo estado usado pela compra.

## 11. Interação e jogador

- WASD, Shift e Space funcionam;
- troca primeira/terceira pessoa funciona;
- objetos podem ser selecionados, pegos, soltos e arremessados;
- portas e caixas mantêm highlight;
- porta abre em terceira pessoa com `E` sem exigir mira central;
- menu bloqueia input;
- uma câmera e um AudioListener de gameplay;
- compra assume e devolve a câmera sem disputa;
- aproximar-se de objetos com `MeshCollider` não convexo não gera warning de `Collider.ClosestPoint` no Console.

## 12. Desktop e Mobile

### Desktop

- mouse e hover de compra corretos;
- `E` interage com porta em terceira pessoa;
- HUD mobile oculto durante Play;
- FPS sem spam no Console.

### Mobile

Validar em aparelho real:

- joystick, olhar, RUN, JUMP, interação, GRAB, THROW e AIM;
- botão INTERACT usa a mesma busca de proximidade da tecla `E`;
- safe area e multitouch;
- dados persistem ao ir para segundo plano;
- sistema de compra não mistura lojas;
- temperatura, memória e FPS aceitáveis por dez minutos.

## 13. Relatórios

Antes de encerrar:

- atualizar `CHANGELOG_TECNICO.md`;
- atualizar relatório específico;
- corrigir divergências entre código e documentação;
- registrar testes não executados e riscos.

## Validação ainda necessária no ambiente local

A revisão desta alteração é estática. Compilação do projeto, comportamento visual e persistência final precisam ser confirmados no Unity local depois do pull.
