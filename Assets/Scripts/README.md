# Organização dos Scripts

Os scripts do projeto são separados por responsabilidade:

- `Camera`: câmera do jogador e modos de visão.
- `Movement`: movimentação e controle do personagem.
- `Interaction`: seleção, GetItem e objetos manipuláveis.
- `Core`: estado compartilhado e infraestrutura de gameplay.
- `Data`: banco local, perfil e persistência.
- `Economy`: gold, carteira e moedas.
- `Purchasing`: compra de terrenos, lojas e cenas.
- `Stamina`: energia independente da câmera.
- `Menus`: controladores de menus.
- `UI`: HUD, textos, painéis e botões.
- `World`: cidade, tempo, ciclo solar e cena.
- `Performance`: otimizações genéricas.
- `Diagnostics`: logs e ferramentas de diagnóstico.

## Sistema do jogador

A arquitetura atual usa apenas uma câmera real:

```text
SampleScene
├── Character 01
│   ├── CameraTarget
│   └── FirstPersonEye
└── PlayerCameraRig
```

### Character 01

Componentes principais:

- `CharacterController`
- `CameraRelativeMovement`
- `Animator`, quando disponível

### PlayerCameraRig

Componentes principais:

- `Camera`
- `AudioListener`
- `ThirdPersonCamera`
- `FirstPersonCamera`
- `PlayerCameraController`
- `GetItemController`

`PlayerCameraController` é a única autoridade que altera posição, rotação e FOV da câmera. Os componentes de primeira e terceira pessoa apenas calculam a pose desejada.

## Controles padrão

- `WASD`: movimentação relativa à câmera.
- `Shift`: correr.
- `Espaço`: pular.
- `Mouse`: girar câmera.
- `V`: alternar primeira/terceira pessoa.
- Segurar botão direito: primeira pessoa temporária.
- Botão esquerdo: pegar/soltar objeto selecionado.
- Rodinha do mouse: ajustar distância do objeto.
- `F`: arremessar objeto segurado.

## GetItem

Cada produto manipulável deve possuir:

- `Collider`
- `Rigidbody`
- `GrabbableItem`

O sistema usa mola e amortecimento físicos, preserva colisões, impede atravessar paredes e restaura o estado original do Rigidbody ao soltar.

## Menu e input

`GameplayInputState` bloqueia câmera, movimentação e GetItem quando:

- o jogo está pausado por `Time.timeScale = 0`;
- o cursor está visível e destravado;
- outro sistema aplica um bloqueio manual.

## Setup automático

Depois do `git pull`, o Unity executa uma migração única que remove componentes legados e cria a estrutura nova. Também é possível executar manualmente:

`Tools > Player System > Create or Repair Player System`

Para remover referências inválidas ao `DontDestroyOnLoad`:

`Tools > Project Maintenance > Clean Cross-Scene References`

## Regras de nomenclatura

1. Não usar o prefixo `MiniMarket` em novos scripts.
2. O nome do arquivo deve ser igual ao nome da classe principal.
3. Cada script deve ficar na pasta de sua função.
4. Conteúdo em `Brick Project Studio` não deve ser alterado.
