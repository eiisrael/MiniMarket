# Testes após Git Pull

Atualizado em: 2026-07-13

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

## 3. Materializar objetos editáveis

Com a `SampleScene` aberta, salva e fora do Play Mode:

```text
Tools > MiniMarket > Materializar Todos os Objetos Runtime na Hierarquia
Tools > MiniMarket > Validar Objetos Runtime Persistentes
```

Depois confirmar `Erros=0`, salvar com `Ctrl + S` e reabrir a cena.

## 4. Preparar as lojas Bronze

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

## 5. Hierarquia mínima da Bronze_Market

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

## 6. Testar duplicação da Bronze_Market

1. Selecionar a raiz completa `Bronze_Market`.
2. Duplicar com `Ctrl + D` ou copiar/colar.
3. Aguardar a atualização da Hierarchy.
4. Mover a cópia para outro local.
5. Selecionar o componente `BronzeMarketPurchaseLot` da cópia.
6. Confirmar que o `Id Lote` é diferente do original.
7. Confirmar que referências da cópia apontam para filhos da própria cópia.
8. Salvar com `Ctrl + S`.
9. Executar `Validar Lojas Bronze` novamente.

## 7. Testar compra isolada

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

## 8. Visual e edição fora do Play

### Compra Bronze

- `PurchaseLotStatus`, textos e seta existem antes do Play;
- cores, posição, escala e textos podem ser editados no Inspector;
- LineRenderers da calçada e terreno aparecem na Hierarchy;
- material persistente está em `Assets/Generated/MiniMarket/Materials/BuyAreaLine.mat`;
- Stop não remove os objetos configuráveis.

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

## 9. Banco

1. Alterar gold e nome.
2. Comprar duas lojas Bronze com IDs diferentes.
3. Parar Play.
4. Iniciar novamente.
5. Confirmar que as duas propriedades continuam compradas.
6. Confirmar que uma terceira cópia com ID novo continua disponível.
7. Confirmar ausência de referência cross-scene inválida.

## 10. Interação e jogador

- WASD, Shift e Space funcionam;
- troca primeira/terceira pessoa funciona;
- objetos podem ser selecionados, pegos, soltos e arremessados;
- portas e caixas mantêm highlight;
- menu bloqueia input;
- uma câmera e um AudioListener de gameplay;
- compra assume e devolve a câmera sem disputa.

## 11. Desktop e Mobile

### Desktop

- mouse e hover de compra corretos;
- HUD mobile oculto durante Play;
- FPS sem spam no Console.

### Mobile

Validar em aparelho real:

- joystick, olhar, RUN, JUMP, interação, GRAB, THROW e AIM;
- safe area e multitouch;
- dados persistem ao ir para segundo plano;
- sistema de compra não mistura lojas;
- temperatura, memória e FPS aceitáveis por dez minutos.

## 12. Relatórios

Antes de encerrar:

- atualizar `CHANGELOG_TECNICO.md`;
- atualizar relatório específico;
- corrigir divergências entre código e documentação;
- registrar testes não executados e riscos.
