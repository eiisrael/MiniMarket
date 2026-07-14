# Jornal — Visual Premium da Tecla E

Atualizado em: 2026-07-14

## Referência visual aprovada

O visual atual de:

```text
Newspaper_Stand/Newspaper_InteractionPrompt
```

foi aprovado e passa a ser a referência oficial do sistema.

Regras permanentes:

- não redefinir layout, tamanho, cores, transparências, animações ou filhos do `Newspaper_Stand`;
- não executar sincronização inversa da `Put_Area` para o expositor;
- usar o expositor somente como fonte de leitura para configurar o prompt de colocação;
- novas correções de `Put_Area` não podem modificar o prompt de pegar jornal.

## Objetivo

Usar a mesma apresentação circular, infantil e dinâmica nos dois fluxos:

- pegar o jornal no `Newspaper_Stand`;
- colocar o jornal na `Put_Area`.

A lógica de interação não foi duplicada nem substituída. `NewspaperWorldPromptVisual`, `NewspaperStandController` e `NewspaperPlacementAreaController` continuam sendo as autoridades existentes.

## Arquivos

```text
Assets/Scripts/Newspaper/NewspaperPromptShapeGraphic.cs
Assets/Scripts/Newspaper/NewspaperPromptPremiumKeyVisual.cs
Assets/Scripts/Newspaper/NewspaperPlacementAreaVisualGuard.cs
Assets/Editor/ProjectMaintenance/NewspaperPromptPremiumKeyInstaller.cs
```

## Sincronização exclusiva da Put Area

O instalador agora:

1. localiza o `Newspaper_InteractionPrompt` já aprovado;
2. lê dimensões, cores, transparências, animações e estilo do `E`;
3. instala ou repara somente o `Newspaper_PlacePrompt`;
4. copia o layout real dos filhos premium para a `Put_Area`;
5. registra que a sincronização foi concluída;
6. não volta a sobrescrever ajustes manuais depois que a cena foi salva.

O menu manual é:

```text
Tools > MiniMarket > Jornal > Sincronizar Put Area com Visual do Newspaper Stand
```

Esse comando também atua somente na `Put_Area`.

## Hierarquia adicionada

Em cada `CircularPrompt` existe:

```text
CircularPrompt
├── PremiumKeyVisual
│   ├── GlowMotion
│   │   └── DynamicGlow
│   ├── OrbitMotion
│   │   ├── OuterRing
│   │   ├── AccentRing
│   │   ├── SparkleTop
│   │   ├── SparkleLeft
│   │   └── SparkleRight
│   └── StaticLayer
│       ├── CenterCircle
│       └── CenterHighlight
└── CenterText
```

Todos os objetos são persistentes, aparecem na Hierarchy e possuem `RectTransform` e componentes editáveis no Inspector.

## Design

- círculo central escuro no lugar do painel quadrado antigo;
- anel externo dourado;
- anel de destaque rosa;
- halo azul translúcido;
- brilho interno suave;
- três partículas orbitais com cores diferentes;
- texto `E` permanece em TextMeshPro e continua editável;
- o disco quadrado legado é desativado, não apagado.

## Animação

- pulsação de escala e transparência somente no wrapper `GlowMotion`;
- rotação contínua somente no wrapper `OrbitMotion`;
- partículas com pulsos de transparência em fases diferentes;
- transforms dos elementos gráficos filhos não são reescritos pela animação;
- todos os efeitos podem ser desligados individualmente no componente premium.

## Jornal colocado invisível

Foi criado `NewspaperPlacementAreaVisualGuard` para corrigir o caso em que o guia da `Put_Area` desligava os renderizadores do `Placed_Newspaper_Runtime` logo depois da colocação.

Comportamento:

- registra os estados originais dos renderizadores antes da interação;
- espera o controlador concluir a ativação e ocultação do guia;
- restaura os renderizadores uma única vez quando o local passa a estar ocupado;
- não altera posição, rotação ou escala do jornal;
- não interfere no inventário, no banco ou no prompt;
- permite desativar manualmente renderizadores depois da restauração sem ficar forçando por frame.

## Edição e persistência

O componente `NewspaperPromptPremiumKeyVisual` expõe:

- diâmetros e espessuras;
- cores e transparências;
- velocidades e amplitudes;
- tamanho e contorno do `E`;
- referências de toda a hierarquia;
- opções para usar diretamente os transforms, cores e texto dos filhos.

Com `Use Child Transforms As Source`, `Use Child Colors As Source` e `Use Center Text Style As Source` marcados, os valores reais vêm diretamente dos componentes filhos.

As alterações manuais feitas durante Play Mode continuam sendo capturadas pelo sistema `NewspaperPlayModeHierarchyPersistence` e reaplicadas ao voltar para Stop. Depois do Stop, usar `Ctrl+S` para salvar a cena.

## Warnings corrigidos no mesmo ajuste

### Physics.ClosestPoint

`InteractionFocusController` não chama mais `Collider.ClosestPoint` em `MeshCollider` não convexo, `TerrainCollider` ou collider incompatível. Nesses casos usa `Bounds.ClosestPoint`, mantendo a porta em terceira pessoa e removendo o spam do Console.

### Referência cross-scene do banco

`PlayerDatabaseCrossSceneReferenceGuard` limpa referências serializadas de objetos da `SampleScene` para o `MiniMarket_PlayerDatabase` em `DontDestroyOnLoad`. O banco continua sendo resolvido pelo singleton em runtime.

### Arquivo local inválido

Quando o arquivo inválido é movido corretamente para backup, o evento passa a ser informação normal no Console. Warning permanece apenas quando a recuperação realmente falha.

## Teste obrigatório

1. Aguardar a compilação com zero erros.
2. Confirmar que o `Newspaper_Stand` manteve exatamente o visual atual.
3. Confirmar `PremiumKeyVisual` dentro de `Newspaper_PlacePrompt/CircularPrompt`.
4. Salvar a cena com `Ctrl+S`.
5. Entrar no Play e pegar o jornal.
6. Ir à `Put_Area` e confirmar que o `E` possui o mesmo design do expositor.
7. Colocar o jornal e confirmar que `Placed_Newspaper_Runtime` aparece na posição salva.
8. Confirmar ausência do warning `Physics.ClosestPoint can only be used...`.
9. Confirmar ausência da referência `Menu -> MiniMarket_PlayerDatabase` entre cenas.
10. Durante o Play, alterar uma cor, escala ou tamanho de um filho da `Put_Area`.
11. Pressionar Stop e confirmar que o valor foi reaplicado.
12. Salvar novamente com `Ctrl+S`.

## Validação pendente

A revisão do código foi estática. Compilação, aparência final e persistência precisam ser confirmadas no Unity 6.7 local.
