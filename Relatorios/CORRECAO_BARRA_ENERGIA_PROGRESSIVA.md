# Correção da barra de energia progressiva

Atualizado em: 2026-07-13

## Objetivo

Corrigir o objeto:

```text
Canvas > StaminaHUD > Energy
```

A imagem `Energy` deve funcionar como uma progress bar horizontal, acompanhar a energia
segmentada de `0/5` até `5/5` e trocar automaticamente entre:

```text
energy_green.png
energy_yellow.png
energy_red.png
```

## Causa observada

O HUD anterior tinha duas representações diferentes:

- o texto mostrava a quantidade de segmentos, por exemplo `5/5`;
- a barra principal podia usar somente `StaminaPercentual01`, isto é, a carga interna do
  segmento ativo.

Quando o save continha segmentos disponíveis e a stamina ativa estava em zero ou ainda não
havia sido sincronizada, o texto permanecia em `5/5`, mas a imagem `Energy` aparecia vazia.
Além disso, os sprites verde, amarelo e vermelho eram tratados como sprites opcionais do
ícone, não como apresentação autoritativa da progress bar principal.

## Solução

Foi criado:

```text
Assets/Scripts/UI/MiniMarketEnergyProgressBar.cs
```

Responsabilidades:

- localizar automaticamente `Canvas/StaminaHUD/Energy` ao entrar no Play Mode;
- adicionar o componente ao objeto `Energy` quando ele ainda não estiver salvo na cena;
- transformar a imagem em `Image.Type.Filled`, horizontal e da esquerda para a direita;
- calcular energia total contínua por segmentos;
- manter `Txt_Qtd` no formato `atual/maximo`;
- corrigir visualmente estados inconsistentes como `5/5` com stamina ativa igual a zero;
- trocar sprite pela energia total;
- preservar as cores originais dos PNGs usando `Color.white`;
- usar cor de fallback quando algum sprite não estiver disponível;
- atualizar por eventos de stamina/banco, mantendo apenas uma verificação lenta de segurança;
- retirar do HUD antigo a autoridade sobre a imagem `Energy`, evitando disputa de fill.

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

Quando existem segmentos disponíveis, mas o save informa stamina ativa igual a zero fora da
corrida, o componente considera o segmento ativo cheio para que o visual respeite o contador.

## Faixas de sprite

Padrão:

```text
acima de 55%: energy_green
de 25% até 55%: energy_yellow
até 25%: energy_red
```

Os limites permanecem editáveis no Inspector.

## Ferramenta do Editor

Foi criado:

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
- encontra `Txt_Qtd`;
- liga `CameraRelativeMovement`;
- procura os três PNGs por nome em qualquer pasta;
- converte os PNGs para `Sprite (2D and UI)` quando necessário;
- desativa mipmaps e ativa transparência;
- configura e salva a cena atual;
- não move arquivos;
- não altera `Assets/Brick Project Studio`.

## Inicialização automática

Mesmo sem executar a ferramenta, no Editor o componente é instalado automaticamente ao
apertar Play e procura os sprites pelo `AssetDatabase`.

Para persistir as referências na cena e garantir que os sprites sejam incluídos no build,
execute uma vez o menu `Criar ou Reparar Barra de Energia` fora do Play Mode.

## Desktop e Mobile

A lógica usa a mesma fonte de dados nos dois ambientes. O preenchimento usa
`Time.unscaledDeltaTime`, não depende de resolução e permanece compatível com o Canvas atual.

## Validação realizada

Foi feita revisão estática dos contratos de:

- `CameraRelativeMovement`;
- `MiniMarketPlayerDatabase`;
- `MiniMarketEnergySegmentHUD`;
- hierarquia mostrada no Unity.

A compilação e o teste visual final precisam ser confirmados no Unity local.

## Testes locais

1. Console sem erros vermelhos.
2. Fora do Play, executar `Criar ou Reparar Barra de Energia`.
3. Executar `Validar Barra de Energia`.
4. Iniciar em `5/5` e confirmar barra cheia verde.
5. Correr e confirmar redução suave.
6. Confirmar continuidade quando o contador muda para `4/5`.
7. Confirmar amarelo na faixa intermediária.
8. Confirmar vermelho próximo de `1/5` e `0/5`.
9. Parar e confirmar recuperação suave.
10. Fechar e abrir Play Mode para validar persistência.
