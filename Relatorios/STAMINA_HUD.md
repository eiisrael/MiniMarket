# Stamina, energia segmentada e HUD

Atualizado em: 2026-07-11

## Sistema ativo

A lógica runtime está em:

```text
Assets/Scripts/Movement/CameraRelativeMovement.cs
```

O HUD está em:

```text
Assets/Scripts/UI/MiniMarketEnergySegmentHUD.cs
```

A persistência está em:

```text
Assets/Scripts/Database/MiniMarketPlayerDatabase.cs
```

## Modelo de energia

Configuração padrão:

- 5 segmentos.
- cada segmento possui uma barra de `maxStamina`.
- corrida consome a barra ativa.
- quando a barra chega a zero e ainda há mais de um segmento, um segmento é consumido e a barra ativa volta ao máximo.
- quando o último segmento termina, o contador vai para `0/5`, a barra vai para zero e a corrida é interrompida.
- após o delay de recuperação, a barra final começa a recuperar; ao ficar positiva, o sistema volta para `1/5`.
- depois de completar a barra ativa, a reserva recupera os segmentos adicionais até o máximo.

## Dados públicos usados por HUD e menu

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

A movimentação não grava no banco a cada frame.

Ela marca o estado como alterado e sincroniza quando:

- a diferença mínima de stamina é atingida;
- um segmento muda;
- a reserva muda o suficiente;
- o intervalo de sincronização termina;
- o aplicativo pausa;
- o componente é desativado;
- o aplicativo fecha.

O banco faz a escrita física usando salvamento diferido e backup.

## HUD

`MiniMarketEnergySegmentHUD` é dirigido por eventos:

- assina `CameraRelativeMovement.OnStaminaChanged`;
- assina `MiniMarketPlayerDatabase.OnDatabaseChanged`;
- faz apenas uma verificação lenta de segurança;
- não usa reflexão;
- não busca o jogador a cada frame;
- altera texto, fill e cor somente quando o valor muda.

Campos principais:

- `textoEnergia`: texto `5/5`.
- `barraEnergia`: imagem Filled horizontal da barra ativa.
- `iconeEnergia`: ícone opcional.
- sprites alto/médio/baixo: opcionais.
- `mostrarPercentualDaBarra`: inclui percentual total quando ligado.

## Cores padrão

- alta: verde.
- média: amarelo/laranja.
- baixa: vermelho.

A cor é baseada no percentual total das cargas, enquanto o fill representa a barra ativa.

## Restauração de energia

O menu chama `CameraRelativeMovement.RestoreStaminaFull()` quando o jogador está presente. O método restaura:

- stamina atual para o máximo;
- segmentos atuais para o máximo;
- reserva para zero;
- estado de corrida para parado;
- banco imediatamente.

## Compatibilidade

`MiniMarketSegmentedStaminaRuntimeGuard` foi aposentado. A classe permanece vazia apenas para não quebrar cenas antigas. Ela não deve ser adicionada a novos objetos.

## Testes obrigatórios

1. Iniciar em `5/5` com barra cheia.
2. Correr e observar o fill diminuir suavemente.
3. Consumir uma barra: contador deve mudar para `4/5` e fill voltar ao máximo.
4. Consumir tudo: `0/5`, sem corrida.
5. Soltar corrida e aguardar recuperação: voltar para `1/5`.
6. Fechar/reabrir Play Mode e verificar continuidade.
7. Clicar em energia grátis e confirmar `5/5`.
8. Abrir menu durante corrida e confirmar que consumo/movimento não ficam presos.
