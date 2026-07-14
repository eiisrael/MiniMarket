# Jornal — Visual Premium da Tecla E

Atualizado em: 2026-07-14

## Objetivo

Substituir o aspecto quadrado e genérico da tecla `E` por uma apresentação circular, infantil e dinâmica, utilizada nos dois fluxos:

- pegar o jornal no `Newspaper_Stand`;
- colocar o jornal na `Put_Area`.

A lógica de interação não foi duplicada nem substituída. `NewspaperWorldPromptVisual`, `NewspaperStandController` e `NewspaperPlacementAreaController` continuam sendo as autoridades existentes.

## Arquivos

```text
Assets/Scripts/Newspaper/NewspaperPromptShapeGraphic.cs
Assets/Scripts/Newspaper/NewspaperPromptPremiumKeyVisual.cs
Assets/Editor/ProjectMaintenance/NewspaperPromptPremiumKeyInstaller.cs
```

## Hierarquia adicionada

Em cada `CircularPrompt` é criada uma única vez:

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

## Instalação

A instalação ocorre automaticamente uma única vez após a compilação. Ela é idempotente e não deve voltar a marcar a cena como modificada depois que a nova estrutura for salva.

Também existe o comando manual:

```text
Tools > MiniMarket > Jornal > Instalar Visual Premium da Tecla E
```

## Teste obrigatório

1. Aguardar a compilação com zero erros.
2. Confirmar `PremiumKeyVisual` nos dois prompts.
3. Salvar a cena com `Ctrl+S`.
4. Entrar no Play e testar pegar o jornal.
5. Confirmar círculo, brilho, anéis e partículas.
6. Testar colocar o jornal e confirmar o mesmo padrão.
7. Durante o Play, alterar uma cor, escala ou tamanho de um filho.
8. Pressionar Stop e confirmar que o valor foi reaplicado.
9. Salvar novamente com `Ctrl+S`.
10. Reabrir a cena e confirmar que a estrutura e os valores permanecem.

## Validação pendente

A revisão do código foi estática. Compilação, aparência final e persistência precisam ser confirmadas no Unity 6.7 local.
