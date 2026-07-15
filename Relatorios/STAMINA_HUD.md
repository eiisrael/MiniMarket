# Stamina, energia segmentada e HUD

Atualizado em: 2026-07-15

## Sistema ativo

Lógica runtime:

```text
Assets/Scripts/Movement/CameraRelativeMovement.cs
```

HUD segmentado e texto:

```text
Assets/Scripts/UI/MiniMarketEnergySegmentHUD.cs
```

Barra visual principal:

```text
Assets/Scripts/UI/MiniMarketEnergyProgressBar.cs
```

Persistência:

```text
Assets/Scripts/Database/MiniMarketPlayerDatabase.cs
```

## Modelo de energia

Configuração padrão:

- 5 segmentos;
- cada segmento usa uma barra de `maxStamina`;
- corrida consome a barra ativa;
- ao zerar uma barra e ainda existir outro segmento, o contador diminui e a barra ativa volta ao máximo;
- ao consumir o último segmento, o sistema chega a `0/5` e impede corrida;
- depois do delay, a barra recupera e o sistema volta a `1/5`;
- após completar a barra ativa, a reserva recupera os segmentos adicionais.

## Dados públicos

- `StaminaAtual`
- `StaminaMaxima`
- `StaminaPercentual01`
- `StaminaSegmentosAtuais`
- `StaminaSegmentosMaximos`
- `StaminaSegmentadaTexto`
- `StaminaRecargaReserva`
- `EnergiaPercentual01`
- `EstaCorrendo`
- `EstaGastandoStamina`
- `EstaCansado`

## Persistência

A movimentação não grava no banco a cada frame. Ela sincroniza quando a diferença mínima, mudança de segmentos, reserva ou intervalo justificam a atualização e força save em pausa, desativação e encerramento.

## HUD segmentado

`MiniMarketEnergySegmentHUD`:

- assina `CameraRelativeMovement.OnStaminaChanged`;
- assina `MiniMarketPlayerDatabase.OnDatabaseChanged`;
- mantém verificação lenta de segurança;
- não usa reflexão;
- não busca o jogador a cada frame;
- mantém as barras segmentadas auxiliares; o texto usa porcentagem quando `mostrarPercentualDaBarra` está ativo e usa `atual/maximo` no modo alternativo.

## Barra visual Canvas/StaminaHUD/Energy

`MiniMarketEnergyProgressBar` controla o preenchimento interno do objeto:

```text
Canvas > StaminaHUD > Energy
```

A imagem original de `Energy` não é a progress bar. Ela permanece estática.

Estrutura criada:

```text
Energy
└── EnergyProgressArea
    └── EnergyProgressFill
```

Comportamento:

- instala-se automaticamente no objeto `Energy` ao entrar no Play Mode;
- mantém `Energy` como `Image.Type.Simple` e `fillAmount = 1`;
- cria uma barra verde separada dentro de `Energy`;
- altera somente a largura de `EnergyProgressFill`;
- representa a energia total contínua dos cinco segmentos;
- mantém `Txt_Qtd` no formato `5/5`, `4/5`, `3/5`, `2/5`, `1/5` ou `0/5`;
- a cor permanece verde durante descarga e recuperação;
- corrige visualmente um save inconsistente que informe segmentos disponíveis e stamina ativa zerada fora da corrida;
- impede que `MiniMarketEnergySegmentHUD` dispute o mesmo preenchimento.

Quando existe um `Background_Ene` separado, o componente pode ocultar somente a `Image` antiga de `Energy`, sem desativar o GameObject e sem afetar os filhos. Isso evita duplicar a imagem enquanto mantém a barra verde interna.

### Cálculo da progress bar

```text
((segmentosAtuais - 1) + staminaDoSegmentoAtivo) / segmentosMaximos
```

Exemplos:

```text
5/5 cheio        = 100%
4/5 cheio        = 80%
3/5 cheio        = 60%
2/5 cheio        = 40%
1/5 cheio        = 20%
0/5              = 0%
```

O segmento ativo continua sendo representado de forma contínua. Assim, com `5/5` e a barra ativa na metade, a progress bar mostra aproximadamente 90%.

### Área visual interna

Padrão:

```text
Ancora Minima: X 0.22 / Y 0.36
Ancora Maxima: X 0.93 / Y 0.64
```

Esses valores são editáveis no Inspector para encaixar a barra verde precisamente dentro do artwork existente.

## Ferramenta de configuração

Fora do Play Mode:

```text
Tools > MiniMarket > Criar ou Reparar Barra de Energia
Tools > MiniMarket > Validar Barra de Energia
```

A ferramenta:

- encontra ou cria `StaminaHUD`;
- encontra ou cria `Energy`;
- adiciona `MiniMarketEnergyProgressBar`;
- mantém a imagem original como `Simple`;
- cria `EnergyProgressArea` e `EnergyProgressFill`;
- liga `Txt_Qtd` e `CameraRelativeMovement`;
- salva a cena atual;
- não move arquivos nem altera `Assets/Brick Project Studio`.

## Inicialização automática e builds

Apertar Play já é suficiente para instalar o componente e criar a barra verde interna.

Para salvar a estrutura na cena e facilitar ajustes de posição, executar uma vez `Criar ou Reparar Barra de Energia` fora do Play Mode e salvar a cena.

## Restauração de energia

`CameraRelativeMovement.RestoreStaminaFull()` restaura:

- stamina atual;
- segmentos;
- reserva;
- estado de corrida;
- banco.

`FreeEnergyRestoreService` reaplica a restauração no fluxo de UI para impedir que listeners antigos recuperem o valor anterior.

## Compatibilidade

`MiniMarketSegmentedStaminaRuntimeGuard` está aposentado e não deve ser usado em novos objetos.

A barra progressiva usa a mesma fonte de dados no Desktop e Mobile, sem buscas globais no loop normal e sem gravação em disco por frame.

## Testes obrigatórios

1. Console sem erros vermelhos.
2. Executar `Criar ou Reparar Barra de Energia` fora do Play Mode.
3. Executar `Validar Barra de Energia`.
4. Confirmar que `Energy` não está como Filled.
5. Confirmar `EnergyProgressArea/EnergyProgressFill`.
6. Iniciar em `5/5` com a barra verde interna cheia.
7. Correr e observar somente a barra verde diminuir.
8. Confirmar que `energy.png`, fundo, ícone e texto permanecem estáticos.
9. Consumir um segmento e confirmar continuidade visual em `4/5`.
10. Parar e observar recuperação suave.
11. Fechar e reabrir Play Mode para validar persistência.
12. Testar em build Desktop e Android.

A compilação e o teste visual final dependem do Unity local.
