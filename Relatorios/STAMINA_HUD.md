# Stamina, energia segmentada e HUD

Atualizado em: 2026-07-12

## Sistema ativo

Lógica runtime:

```text
Assets/Scripts/Movement/CameraRelativeMovement.cs
```

HUD:

```text
Assets/Scripts/UI/MiniMarketEnergySegmentHUD.cs
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

## HUD atual

`MiniMarketEnergySegmentHUD`:

- assina `CameraRelativeMovement.OnStaminaChanged`;
- assina `MiniMarketPlayerDatabase.OnDatabaseChanged`;
- mantém verificação lenta de segurança;
- não usa reflexão;
- não busca o jogador a cada frame;
- atualiza texto, fill, cor e sprite somente quando necessário.

### Barra principal

O modo padrão é:

```text
PrimaryBarDisplayMode.ActiveSegment
```

Assim, a barra grande mostra exatamente a carga ativa:

```text
100% -> 0% ao correr
0% -> 100% ao recuperar
```

Também existem os modos `TotalEnergy` e `Auto` para layouts alternativos.

### Barras segmentadas

- imagens com nomes de segmento são detectadas automaticamente;
- se a cena não possuir barras pequenas, o componente cria barras runtime abaixo da barra principal;
- a quantidade acompanha `StaminaSegmentosMaximos`;
- cada barra usa `Image.Type.Filled` horizontal;
- a reserva pode preencher parcialmente o próximo segmento;
- a ordem pode ser invertida pelo Inspector.

Campos importantes:

- `modoBarraPrincipal`;
- `barraEnergia`;
- `barrasSegmentos`;
- `criarBarrasSegmentadasQuandoAusentes`;
- `alturaBarrasAutomaticas`;
- `espacoEntreBarras`;
- `deslocamentoVerticalBarras`;
- `animarPreenchimento`;
- `velocidadePreenchimento`;
- `toleranciaVisual`.

Menus de contexto:

```text
HUD > Rebuscar barras e atualizar
HUD > Recriar barras segmentadas automáticas
```

## Cores padrão

- alta: verde;
- média: amarelo/laranja;
- baixa: vermelho.

A cor usa o percentual total, enquanto a barra principal usa a carga ativa.

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

## Ferramenta de configuração

Fora do Play Mode:

```text
Tools > Game Systems > Apply Gameplay Polish (HUD Grab Purchase MiniMap Mobile)
```

Ela encontra a barra mais provável, liga o movimento, ativa preenchimento animado e habilita barras automáticas.

## Testes obrigatórios

1. Iniciar em `5/5` com barra cheia.
2. Correr e observar a barra grande diminuir suavemente.
3. Consumir uma barra e confirmar `4/5` com barra ativa restaurada.
4. Confirmar cinco barras pequenas visualmente.
5. Consumir tudo e confirmar `0/5`.
6. Aguardar recuperação e observar a barra carregar suavemente.
7. Confirmar recuperação de segmentos adicionais.
8. Fechar e reabrir Play Mode para validar persistência.
9. Usar energia grátis e confirmar que permanece em `5/5`.
10. Abrir menu durante corrida e confirmar que entrada e consumo não ficam presos.
