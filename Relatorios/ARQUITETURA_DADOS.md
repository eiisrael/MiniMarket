# Arquitetura de dados do jogador

Atualizado em: 2026-07-15

## Autoridade

`Assets/Scripts/Database/MiniMarketPlayerDatabase.cs` é a única fonte persistente de verdade.

`PlayerGold` e `MiniMarketPlayerProfile` são fachadas de compatibilidade. Elas não devem manter saves paralelos em `PlayerPrefs`.

`PlayerProfile`, `BuyableLandAreaMarker` e `RuntimeDiagnostics` também consomem `MiniMarketPlayerDatabase`. A classe `Assets/Scripts/Data/PlayerDatabase.cs` é legado preservado por compatibilidade de GUID e não deve ser instanciada por código novo.

`CameraRelativeMovement` mantém o estado runtime da stamina para responder imediatamente ao gameplay, mas sincroniza esse estado com o banco em intervalos controlados e nos eventos de pausa/saída.

## Schema atual

Versão: `2`

Arquivo local:

```text
Application.persistentDataPath/player_database.mmdb
```

Backup:

```text
Application.persistentDataPath/player_database.mmdb.bak
```

O formato possui:

- JSON serializado pelo Unity.
- criptografia AES-CBC.
- assinatura HMAC-SHA256.
- arquivo temporário antes da substituição.
- backup do save anterior.
- recuperação pelo backup quando o arquivo principal falha.

## Dados salvos

### Identidade

- `playerId`
- `nome`

### Economia

- `gold`
- `gemas`
- `goldInicializado`

### Stamina e energia

- `staminaAtual`
- `staminaMaxima`
- `energiaSegmentosAtuais`
- `energiaSegmentosMaximos`
- `energiaRecargaReserva`
- `staminaInicializada`

### Empresas e propriedades

- lista normalizada de empresas compradas.
- estado por propriedade: ID, nome, comprada, disponível e status.

### Mundo e sessão

- última cena.
- última posição.
- última rotação.
- indicador de posição salva.
- tempo jogado em segundos.
- timestamps de criação e atualização.

## Migração

O banco V2 deve aceitar:

1. arquivo `MMDB2` atual;
2. arquivo criptografado `MMDB1` antigo;
3. JSON não criptografado quando a opção de criptografia estiver desligada;
4. segmentos antigos encontrados nas chaves `PlayerPrefs` do sistema anterior.

O bootstrap de recuperação estrutural aceita os prefixos `MMDB1` e `MMDB2`; arquivos JSON são validados contra `MiniMarketPlayerDatabase.MiniMarketPlayerData`.

Depois da migração, o conteúdo é normalizado e salvo no formato atual.

## Regras de escrita

- Alterações críticas, como compra, gold, nome e restauração completa, podem salvar imediatamente.
- Stamina em movimento usa debounce para não escrever no disco a cada frame.
- O banco salva ao pausar, perder foco e sair.
- Eventos `OnDatabaseChanged` atualizam HUD/menu sem polling pesado.
- Nenhum componente da cena deve guardar uma referência serializada para o objeto runtime do banco.

## APIs principais

### Gold

- `GarantirGoldInicial`
- `DefinirGold`
- `AdicionarGold`
- `RemoverGold`
- `TemGoldSuficiente`

### Gemas

- `DefinirGemas`
- `AdicionarGemas`
- `RemoverGemas`

### Energia

- `GarantirStaminaInicial`
- `DefinirStamina`
- `DefinirStaminaAtual`
- `DefinirEnergiaSegmentada`
- `RestaurarEnergiaCompleta`
- `ObterPercentualStamina01`

### Perfil e propriedade

- `DefinirNome`
- `RegistrarEmpresaComprada`
- `ResetarEmpresasCompradas`
- `RegistrarPropriedadeComprada`
- `DefinirStatusPropriedade`

### Mundo

- `SalvarPosicaoMundo`
- `TentarObterPosicaoMundo`

## Não fazer

- Não criar outra classe que grave gold, empresas ou stamina em arquivo separado.
- Não usar nem instanciar `Assets/Scripts/Data/PlayerDatabase.cs` em novos fluxos; migrar consumidores para `MiniMarketPlayerDatabase`.
- Não chamar `PlayerPrefs.Save()` por frame.
- Não serializar o banco no Inspector de objetos da cena.
- Não alterar o salt/algoritmo sem manter migração do formato anterior.
- Não resetar o banco inteiro para apagar somente empresas.

## Testes obrigatórios

1. Alterar nome e reiniciar Play Mode.
2. Adicionar/remover gold e reiniciar.
3. Comprar empresa e verificar duplicidade.
4. Gastar energia, aguardar debounce e reiniciar.
5. Restaurar energia e verificar segmentos completos.
6. Pausar aplicativo no mobile/emulação e retornar.
7. Abrir um save `MMDB2` válido duas vezes e confirmar que ele não recebe sufixo `.corrupt_*.bak`.
8. Corromper uma cópia de teste do arquivo e verificar recuperação pelo `.bak`.
