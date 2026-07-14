# Correção da duplicação da Bronze_Market

Atualizado em: 2026-07-14

## Problemas observados

Ao duplicar a raiz `Bronze_Market`:

- `PurchaseLotStatus` era recriado com escala e tamanho padrão;
- as edições feitas no Inspector eram preservadas somente na loja original;
- o reconciliador chamava métodos runtime com `SendMessage` em Edit Mode;
- o Unity emitia `Assertion failed on expression: ShouldRunBehaviour()`;
- os LineRenderers acessavam `renderer.material` durante a reconciliação e o Unity alertava sobre vazamento de materiais;
- o painel mostrava somente `DISPONÍVEL`, sem identificar qual lote estava sendo visualizado.

## Causa

`BronzeMarketPurchaseLotSetup.EnsureStatusView` aplicava novamente posição, escala, tamanho,
âncoras, fontes e cores sempre que uma loja era preparada. A cópia inicialmente continha o
layout correto, mas a automação substituía esses valores pelos defaults.

O reconciliador também usava `SendMessage` para chamar métodos privados de criação e
atualização visual em componentes que não estavam executando como behaviours no Edit Mode.

## Correção

### Cópia fiel do objeto

`BronzeMarketPurchaseLotSetup` agora:

- reutiliza toda a hierarquia copiada;
- aplica valores padrão somente quando um objeto ou componente realmente não existe;
- não altera o `RectTransform` de um `PurchaseLotStatus` já existente;
- não altera posição, rotação, escala, tamanho, âncoras, cores, fontes, sprites ou filhos da cópia;
- preserva o `BuySceneController` já configurado dentro da loja;
- gera somente um novo `idLote` para a cópia;
- religa trigger, terreno, câmera, painel e compra exclusivamente à própria loja;
- mantém `procurarTerrenosAutomaticamente = false`;
- mantém a lista de terrenos com exatamente o terreno local.

### ID visual

`BronzeMarketLotStatusView` passou a exibir:

```text
DISPONÍVEL
ID: A1B2C3D4
```

ou:

```text
INDISPONÍVEL
ID: A1B2C3D4
```

O ID persistente continua completo no componente `BronzeMarketPurchaseLot`. Por padrão, o
painel exibe os oito últimos caracteres para não estourar o layout. No Inspector é possível
ativar `Mostrar Id Completo` ou alterar `Caracteres Id Visiveis`.

### Reconciliação segura

`BronzeMarketLocalControllerReconciler` agora:

- não usa `SendMessage` em Edit Mode;
- não recria LineRenderers durante a reconciliação;
- usa somente `sharedMaterial` nos objetos visuais persistentes;
- preserva os ajustes de câmera e de layout;
- desativa controladores duplicados antigos;
- mantém somente o controlador local escolhido como autoridade da loja.

## Fluxo correto

1. Sair do Play Mode.
2. Atualizar o projeto.
3. Abrir a `SampleScene`.
4. Executar uma vez:

```text
Tools > MiniMarket > Bronze Market > Preparar Todas as Lojas Bronze
Tools > MiniMarket > Bronze Market > Reconciliar Controladores e Visuais
Tools > MiniMarket > Bronze Market > Validar Lojas Bronze
```

5. Editar o `PurchaseLotStatus` da loja original.
6. Duplicar a raiz completa `Bronze_Market` com `Ctrl+D` ou copiar/colar.
7. Aguardar a mensagem de novo ID.
8. Salvar com `Ctrl+S`.

Depois da preparação inicial, novas cópias recebem ID automaticamente e mantêm exatamente o
layout copiado.

## Validação esperada

- zero `Assertion failed on expression: ShouldRunBehaviour()`;
- zero aviso de material instanciado pela ferramenta de reconciliação;
- cada loja com ID diferente;
- cada trigger ligado somente ao terreno da mesma loja;
- clicar em uma loja não permite comprar terreno de outra;
- `PurchaseLotStatus` da cópia com o mesmo tamanho, posição, escala e estilo do original;
- painel mostrando o ID da própria loja;
- câmera de compra mantendo o comportamento anterior.

A compilação e o teste visual final dependem do Unity local.
