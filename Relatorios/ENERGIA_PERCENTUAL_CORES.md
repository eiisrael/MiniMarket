# Energia por porcentagem e estados visuais

Atualizado em: 2026-07-13

## Objetivo

Substituir o texto segmentado `5/5` por uma porcentagem contínua da energia total e alterar o
ícone e a cor da progress bar conforme a energia diminui.

## Faixas

```text
100% até 61%: verde
60% até 26%: amarelo
25% até 0%: vermelho
```

Sprites usados:

```text
Assets/UI/Models/Textures/HUD/green_energy.png
Assets/UI/Models/Textures/HUD/yellow_energy.png
Assets/UI/Models/Textures/HUD/red_energy.png
```

## Comportamento

`MiniMarketEnergyProgressBar` agora:

- calcula a porcentagem com base na energia total segmentada;
- exibe `100%`, `99%`, `60%`, `25%`, `0%` etc.;
- troca o sprite do ícone de energia entre verde, amarelo e vermelho;
- troca a cor de `EnergyProgressFill` na mesma faixa;
- preserva o artwork original de `Energy`;
- mantém a animação suave de carga e descarga;
- evita que `MiniMarketEnergySegmentHUD` sobrescreva o texto percentual.

## Configuração persistente

Fora do Play Mode, executar:

```text
Tools > MiniMarket > Configurar Energia por Porcentagem e Cores
Tools > MiniMarket > Validar Energia por Porcentagem e Cores
```

A ferramenta liga automaticamente:

- `Txt_Qtd` ao campo de texto;
- a imagem do ícone ao campo `Icone Energia`;
- os três sprites pelos caminhos oficiais;
- `Limite Verde = 0.61`;
- `Limite Vermelho = 0.25`;
- `Mostrar Porcentagem = true`;
- `Manter Texto Segmentado = false`.

A ferramenta preserva posição, tamanho, escala, âncoras e as modificações de layout já feitas
na cena.

## Inspector

As cores continuam editáveis em:

```text
Cor Alta
Cor Media
Cor Baixa
```

Os limites também permanecem editáveis. Para manter a regra solicitada, usar 0.61 e 0.25.

## Validação

Foi feita revisão estática dos contratos do HUD, dos sprites importados como `Sprite (2D and
UI)` e da fonte de energia segmentada. A compilação e o teste visual final dependem do Unity
local.
