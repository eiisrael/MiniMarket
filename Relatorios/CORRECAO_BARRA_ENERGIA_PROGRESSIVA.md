# Correção da barra de energia progressiva

Atualizado em: 2026-07-13

## Objetivo correto

O objeto existente:

```text
Canvas > StaminaHUD > Energy
```

não deve ter a própria imagem reduzida como se fosse a progress bar.

O artwork original de `Energy` deve permanecer parado. Dentro dele deve existir uma barra
verde separada que aumenta e diminui conforme a stamina, respeitando o contador `5/5`.

## Problema da primeira implementação

A primeira implementação transformava a própria `Image` do objeto `Energy` em
`Image.Type.Filled`. Como consequência, a imagem `energy.png` inteira diminuía da esquerda
para a direita.

Isso estava incorreto porque `Energy` já faz parte da estrutura visual do HUD. Somente uma
camada verde interna deve representar o carregamento.

## Solução atual

Arquivo runtime:

```text
Assets/Scripts/UI/MiniMarketEnergyProgressBar.cs
```

O componente agora mantém esta estrutura:

```text
Energy
└── EnergyProgressArea
    └── EnergyProgressFill
```

Responsabilidades:

- preservar a imagem e o RectTransform originais de `Energy`;
- manter `Energy` como `Image.Type.Simple` e `fillAmount = 1`;
- criar automaticamente uma área interna de preenchimento;
- criar uma imagem verde separada dentro dessa área;
- diminuir somente a largura de `EnergyProgressFill`;
- animar descarga e recarga suavemente;
- respeitar `5/5`, `4/5`, `3/5`, `2/5`, `1/5` e `0/5`;
- manter `Txt_Qtd` sincronizado;
- impedir que `MiniMarketEnergySegmentHUD` altere a mesma barra;
- corrigir visualmente estados inconsistentes do save;
- não trocar nem diminuir o sprite original `energy.png`.

## Fundo separado

Na hierarquia mostrada existe também:

```text
Background_Ene
```

Quando esse fundo separado é encontrado, o componente desativa somente o componente
`Image` antigo de `Energy`, sem desativar o GameObject. Assim, o fundo e os demais elementos
do HUD permanecem, enquanto a nova barra verde interna é exibida sem duplicar a imagem
antiga.

## Área da barra

A barra interna usa por padrão a região normalizada:

```text
Ancora Minima: X 0.22 / Y 0.36
Ancora Maxima: X 0.93 / Y 0.64
```

Esses valores ficam disponíveis no Inspector do `MiniMarketEnergyProgressBar` para pequenos
ajustes visuais sem alterar código.

## Cálculo visual

A energia total usa:

```text
((segmentosAtuais - 1) + percentualDoSegmentoAtivo) / segmentosMaximos
```

Exemplos:

```text
5/5 com segmento ativo cheio     = 100%
5/5 com segmento ativo na metade = 90%
4/5 com segmento ativo cheio     = 80%
3/5 com segmento ativo cheio     = 60%
1/5 com segmento ativo cheio     = 20%
0/5                              = 0%
```

A cor permanece verde durante todo o carregamento. O estado da energia é indicado pelo
tamanho da barra e pelo texto segmentado.

## Ferramenta do Editor

Arquivo:

```text
Assets/Editor/ProjectMaintenance/EnergyProgressBarSetup.cs
```

Menus:

```text
Tools > MiniMarket > Criar ou Reparar Barra de Energia
Tools > MiniMarket > Validar Barra de Energia
```

A ferramenta:

- encontra ou cria `StaminaHUD`;
- encontra ou cria `Energy`;
- adiciona `MiniMarketEnergyProgressBar`;
- mantém a imagem original como `Simple`, nunca como `Filled`;
- cria `EnergyProgressArea` e `EnergyProgressFill`;
- liga `Txt_Qtd` e `CameraRelativeMovement`;
- desliga a autoridade antiga sobre a mesma barra;
- salva a cena atual;
- não move arquivos;
- não altera `Assets/Brick Project Studio`.

## Inicialização automática

Mesmo sem executar a ferramenta, ao apertar Play o componente procura `Energy`, instala-se e
cria a barra verde interna.

Executar o menu uma vez fora do Play Mode é recomendado para salvar a nova estrutura na cena.

## Desktop e Mobile

A mesma fonte de dados é usada nos dois ambientes. A animação usa
`Time.unscaledDeltaTime`, não grava em disco por frame e não depende da resolução da tela.

## Validação realizada

Foi feita revisão estática dos contratos de:

- `CameraRelativeMovement`;
- `MiniMarketPlayerDatabase`;
- `MiniMarketEnergySegmentHUD`;
- hierarquia `StaminaHUD` mostrada no Unity.

A compilação e o teste visual final precisam ser confirmados no Unity local.

## Testes locais

1. Console sem erros vermelhos.
2. Fora do Play, executar `Criar ou Reparar Barra de Energia`.
3. Executar `Validar Barra de Energia`.
4. Confirmar que `Energy` não está como `Image.Type.Filled`.
5. Confirmar os filhos `EnergyProgressArea/EnergyProgressFill`.
6. Iniciar em `5/5` com a barra verde interna cheia.
7. Correr e confirmar que apenas a barra verde diminui.
8. Confirmar que `energy.png`, `Background_Ene`, ícone e texto não diminuem.
9. Parar e confirmar recuperação suave.
10. Consumir um segmento e confirmar continuidade correta em `4/5`.
