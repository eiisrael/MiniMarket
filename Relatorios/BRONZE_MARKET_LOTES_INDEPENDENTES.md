# Bronze_Market como lotes de compra independentes

Atualizado em: 2026-07-15

## Objetivo

Permitir que a raiz completa:

```text
Bronze_Market
```

seja duplicada na Hierarchy e que cada cópia passe a representar uma loja/lote diferente,
com compra, ID, calçada, terreno, status e controlador próprios.

O fluxo esperado passa a ser:

```text
1. selecionar Bronze_Market;
2. copiar e colar/duplicar a raiz;
3. mover a cópia para outro local;
4. salvar a cena;
5. comprar as lojas de forma independente.
```

## Problemas anteriores

- apenas um `Buy_Area` recebia o trigger funcional;
- o reparo runtime encontrava um controlador global e conectava todas as lojas nele;
- `BuySceneLandPurchaseController` substituía referências serializadas por
  `FindAnyObjectByType` durante `Awake/Start`;
- o controlador de compra podia pesquisar todos os `BuyableLandAreaMarker` da cena;
- no modo aberto com `E`, era possível clicar em lotes pertencentes a outras lojas;
- lojas duplicadas preservavam o mesmo `idPersistente`, portanto representavam a mesma
  propriedade no banco;
- alguns objetos de compra só apareciam no Play Mode.

## Nova autoridade por loja

Foi criado:

```text
Assets/Scripts/Purchasing/BronzeMarketPurchaseLot.cs
```

O componente fica na raiz de cada `Bronze_Market` e armazena:

- ID persistente exclusivo;
- nome e preço da loja;
- `Buy_Area`;
- collider sólido da calçada;
- trigger de entrada;
- terreno principal;
- ponto de foco da câmera;
- controlador local de câmera;
- `PurchaseModeBridge` local;
- controlador local de compra;
- painel de confirmação;
- painel mundial de status.

As referências são resolvidas primeiro dentro da própria hierarquia da loja. A busca global é
usada apenas para objetos realmente compartilhados, como câmera do jogador e banco.

## Hierarquia persistente

A ferramenta cria ou reaproveita:

```text
Bronze_Market
├── BronzeMarketPurchaseLot
├── BUY_SYSTEM
│   └── controlador local de câmera/compra
├── Buy_Area
│   └── BuySceneEntryTrigger_Runtime
│       ├── BoxCollider Trigger
│       ├── BuySceneEntryTrigger
│       ├── BuyScene_Entrada_Borda
│       ├── BuyScene_Entrada_Diagonal_A
│       └── BuyScene_Entrada_Diagonal_B
├── PurchaseLotArea
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

Todos esses objetos permanecem na cena fora do Play Mode e podem ser ajustados no Inspector.

## IDs exclusivos ao duplicar

A ferramenta do Editor acompanha mudanças na Hierarchy. Quando uma cópia de
`Bronze_Market` mantém o ID da original, ela gera automaticamente outro ID no formato:

```text
BRONZE_MARKET_XXXXXXXXXX
```

O novo ID é aplicado também ao `BuyableLandAreaMarker`. Assim, o banco salva cada compra
como uma propriedade diferente e o jogador pode comprar várias lojas Bronze.

## Restrição de escopo

Cada trigger recebe somente:

```text
terrenosDestaArea = terreno da própria Bronze_Market
```

E mantém desligados:

```text
usarTerrenosProximosSeListaVazia
sincronizarComTerrenosEncontradosAutomaticamente
procurarTerrenosAutomaticamente
```

`BuySceneLandPurchaseController` foi corrigido para:

- preservar o controlador local serializado;
- não substituir a referência por um controlador global em `Awake/Start`;
- usar somente o terreno da `BronzeMarketPurchaseLot` pai;
- rejeitar clique, seleção e confirmação de terreno externo à própria loja.

## Reparo runtime

`PurchaseSystemBootstrapHost` agora separa dois fluxos:

1. lojas com `BronzeMarketPurchaseLot`, que preservam seu escopo local;
2. objetos legados, que ainda podem usar o controlador global de fallback.

O reparo não conecta mais todos os triggers Bronze ao primeiro controlador encontrado.
Também cria triggers de fallback para todos os objetos `Buy_Area`, e não somente para um.
O bootstrap e o materializador chamam `AtualizarVisualRuntime()` por API tipada; não há despacho por string com `SendMessage`.

## Visualização ao pressionar E

Foi criado:

```text
Assets/Scripts/Purchasing/BronzeMarketLotStatusView.cs
```

No modo de compra da loja atual:

- apenas o painel daquela loja fica visível;
- é mostrado `DISPONÍVEL` ou `INDISPONÍVEL`;
- o preço é mostrado em Gold;
- ao passar o mouse sobre o terreno correto, uma seta aparece, oscila e aumenta levemente;
- o painel muda de cor no hover;
- outras lojas não recebem hover, seleção ou clique.

A câmera continua usando `BuySceneCameraModeController` e conserva os valores já configurados.
O reconciliador prefere o controlador existente no objeto `BuySceneController`, evitando
substituir os ajustes por valores padrão.

## Ferramentas

Com a `SampleScene` aberta e fora do Play Mode:

```text
Tools > MiniMarket > Bronze Market > Preparar Todas as Lojas Bronze
Tools > MiniMarket > Bronze Market > Preparar Loja Bronze Selecionada
Tools > MiniMarket > Bronze Market > Gerar Novo ID para Loja Selecionada
Tools > MiniMarket > Bronze Market > Validar Lojas Bronze
Tools > MiniMarket > Bronze Market > Reconciliar Controladores e Visuais
```

Também foi criado um Inspector personalizado para `BronzeMarketPurchaseLot` com os mesmos
atalhos.

## Primeira migração

1. Fazer backup/commit das alterações locais.
2. Atualizar o projeto.
3. Abrir a `SampleScene` fora do Play Mode.
4. Executar `Preparar Todas as Lojas Bronze`.
5. Executar `Reconciliar Controladores e Visuais`.
6. Executar `Validar Lojas Bronze`.
7. Confirmar `erros=0`.
8. Salvar com `Ctrl+S`.
9. Testar a loja original.
10. Duplicar a raiz `Bronze_Market`, mover a cópia e salvar.
11. Confirmar que a cópia recebeu ID diferente.
12. Testar as duas lojas separadamente.

## Testes obrigatórios

- a marcação de calçada fica dentro da respectiva raiz;
- cada loja possui apenas um controlador de câmera habilitado;
- a câmera mantém altura, rotação, ortográfico e transição anteriores;
- `E` na loja A mostra apenas o status/terreno da loja A;
- `E` na loja B mostra apenas o status/terreno da loja B;
- hover na loja A não destaca a loja B;
- clique na loja A não abre compra da loja B;
- compra da loja A não marca a loja B como comprada;
- IDs das lojas são diferentes;
- é possível comprar mais de uma loja quando há Gold suficiente;
- sair do modo de compra restaura a câmera normalmente;
- todos os objetos continuam visíveis/editáveis fora do Play Mode.

## Validação realizada

Foram revisados estaticamente:

- `PurchaseSystemBootstrapHost`;
- `BuySceneEntryTrigger`;
- `BuySceneCameraModeController`;
- `BuySceneLandPurchaseController`;
- `BuyableLandAreaMarker`;
- `PurchaseModeBridge`;
- a hierarquia mostrada na captura do Unity.

A compilação, o posicionamento final do painel e o teste visual dependem do Unity local.
