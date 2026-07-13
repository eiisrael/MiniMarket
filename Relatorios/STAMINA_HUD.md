# Stamina, energia segmentada e HUD

Atualizado em: 2026-07-13

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
- mantém o texto `atual/maximo` e as barras segmentadas auxiliares.

## Barra visual Canvas/StaminaHUD/Energy

`MiniMarketEnergyProgressBar` é a autoridade da imagem:

```text
Canvas > StaminaHUD > Energy
```

Comportamento:

- instala-se automaticamente no objeto `Energy` ao entrar no Play Mode;
- usa `Image.Type.Filled` horizontal da esquerda para a direita;
- representa a energia total contínua dos cinco segmentos;
- mantém `Txt_Qtd` no formato `5/5`, `4/5`, `3/5`, `2/5`, `1/5` ou `0/5`;
- usa `energy_green`, `energy_yellow` e `energy_red`;
- mantém `Color.white` quando existe sprite, preservando a cor original da textura;
- usa cor verde, amarela ou vermelha como fallback;
- corrige visualmente um save inconsistente que informe segmentos disponíveis e stamina ativa zerada fora da corrida;
- impede que `MiniMarketEnergySegmentHUD` dispute o `fillAmount` da mesma imagem.

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

### Faixas visuais

Padrão:

- acima de 55%: `energy_green`;
- de 25% até 55%: `energy_yellow`;
- até 25%: `energy_red`.

Os limites podem ser alterados no Inspector.

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
- liga `Txt_Qtd` e `CameraRelativeMovement`;
- procura `energy_green.png`, `energy_yellow.png` e `energy_red.png` em qualquer pasta;
- aceita também os nomes antigos `green_energy`, `yellow_energy` e `red_energy`;
- configura as texturas como `Sprite (2D and UI)`;
- ativa transparência e desativa mipmaps;
- salva a cena atual;
- não move arquivos nem altera `Assets/Brick Project Studio`.

## Inicialização automática e builds

No Editor, apertar Play é suficiente para instalar o componente e procurar sprites pelo `AssetDatabase`.

Para serializar as referências e garantir inclusão correta dos três sprites em builds Desktop/Mobile, executar uma vez `Criar ou Reparar Barra de Energia` e salvar a cena.

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
4. Iniciar em `5/5` com barra cheia e verde.
5. Correr e observar redução suave.
6. Consumir um segmento e confirmar continuidade visual em `4/5`.
7. Confirmar amarelo na faixa intermediária.
8. Confirmar vermelho próximo de `1/5` e `0/5`.
9. Parar e observar recuperação suave.
10. Fechar e reabrir Play Mode para validar persistência.
11. Testar em build Desktop e Android.

A compilação e o teste visual final dependem do Unity local.
