# Ajustes de HUD, interação, compra, minimapa e mobile

Atualizado em: 2026-07-12

## Objetivo

Esta atualização corrige somente os pontos abaixo, sem alterar Brick Project Studio e sem reorganizar arquivos:

1. carga e descarga visual da stamina;
2. soltura segura de objetos ao sair da mira;
3. marcação da entrada de compra no objeto `Buy_Area`;
4. configuração persistente do minimapa pelo Inspector;
5. HUD funcional de controles mobile;
6. troca para o sprite `click_on` ao selecionar ou segurar objetos.

## 1. Stamina e HUD

Arquivo principal:

```text
Assets/Scripts/UI/MiniMarketEnergySegmentHUD.cs
```

Comportamento atual:

- a barra principal usa por padrão `ActiveSegment`;
- a barra ativa descarrega continuamente de 100% até 0%;
- a recarga também é animada continuamente;
- as barras segmentadas representam a energia total;
- quando não existem barras pequenas configuradas, o HUD cria cinco barras runtime abaixo da barra principal;
- o próximo segmento pode receber visualmente a reserva de recarga;
- imagens são convertidas para `Image.Type.Filled` horizontal;
- o valor visual usa interpolação com tolerância final para não ficar preso próximo de 0 ou 1;
- atualização continua orientada por eventos, com verificação lenta de segurança.

O reparo do Editor procura a imagem mais provável da barra usando nome e proporção e configura:

```text
Modo da barra principal: ActiveSegment
Auto detectar barras: ligado
Criar barras quando ausentes: ligado
Animar preenchimento: ligado
```

## 2. Soltura segura de objetos

Arquivo principal:

```text
Assets/Scripts/Interaction/GetItemController.cs
```

Causa anterior:

- a soltura comum herdava velocidade da mola física;
- a transição rápida da câmera adicionava velocidade calculada muito alta;
- sair da primeira pessoa e soltar o botão no mesmo momento podia lançar o objeto.

Correção:

- objetos pegos em primeira pessoa caem ao sair da primeira pessoa;
- soltura comum e arremesso são caminhos separados;
- soltura comum não herda velocidade da câmera por padrão;
- velocidade linear e angular são reduzidas e limitadas;
- somente `RequestThrow` ou a tecla de arremesso aplica força para frente;
- velocidades absurdas da câmera são descartadas.

Configuração segura padrão:

```text
Drop When Leaving First Person: ligado
Inherit Camera Velocity On Normal Release: desligado
Safe Drop Velocity Multiplier: 0.12
Safe Drop Angular Velocity Multiplier: 0.20
Maximum Safe Drop Speed: 1.25
```

## 3. Entrada de compra em Buy_Area

Arquivos principais:

```text
Assets/Scripts/Purchasing/PurchaseSystemBootstrapHost.cs
Assets/Scripts/Purchasing/BuySceneEntryTrigger.cs
Assets/Editor/ProjectMaintenance/GameplayPolishSetup.cs
```

O objeto mostrado na cena chama-se `Buy_Area`. A busca anterior não reconhecia esse nome porque exigia simultaneamente termos de entrada e compra.

A nova busca reconhece explicitamente:

```text
Buy_Area
BuyArea
Area_Buy
```

Segurança física:

- o collider original da calçada permanece sólido;
- um filho chamado `BuySceneEntryTrigger_Runtime` recebe um `BoxCollider` trigger;
- o tamanho do trigger é derivado dos bounds do collider original;
- o trigger recebe `BuySceneEntryTrigger`;
- a borda e o X são desenhados por `LineRenderer` acima do chão;
- a largura mínima foi aumentada para 0.14;
- o offset vertical mínimo foi aumentado para 0.12;
- controlador, jogador, painel e terrenos são religados.

Isso impede que transformar o collider da calçada em trigger faça o personagem atravessar o chão.

## 4. Minimapa editável no Inspector

Arquivo principal:

```text
Assets/Scripts/UI/RuntimeMiniMap.cs
```

O componente pode ser salvo em `GameSystemsConfiguration` e editado antes do Play Mode.

Campos editáveis:

- alvo;
- posição: quatro cantos;
- tamanho Desktop e Mobile;
- margens;
- cores;
- tamanho e espaço dos botões;
- exibir ou ocultar botões e ponto do jogador;
- altura da câmera;
- zoom mínimo, atual e máximo;
- passo de zoom;
- rotação com o jogador;
- camadas visíveis;
- resolução da RenderTexture Desktop e Mobile;
- tecla de abrir e estado inicial;
- sorting order do Canvas.

Menus de contexto:

```text
MiniMap > Aplicar configurações do Inspector
MiniMap > Recriar câmera e visual runtime
```

## 5. HUD mobile

Arquivo principal:

```text
Assets/Scripts/UI/MobileControlsHUD.cs
```

Visibilidade:

- Android/iOS: visível automaticamente;
- Desktop: oculto por padrão;
- teste no Editor: ativar `Forçar Visível Para Testes`.

Controles disponíveis:

- joystick esquerdo: movimentação;
- arrasto na metade direita: câmera;
- `RUN`: segurar para correr;
- `JUMP`: pular;
- `E`: interagir;
- `GRAB`: segurar para pegar e soltar;
- `THROW`: arremessar explicitamente;
- `AIM`: segurar para primeira pessoa e mira.

Integrações:

```text
CameraRelativeMovement.SetMoveInput
CameraRelativeMovement.SetRunInput
CameraRelativeMovement.RequestJump
PlayerCameraController.AddMobileLookDelta
PlayerCameraController.SetMobileFirstPersonHeld
InteractionFocusController.RequestInteract
GetItemController.RequestGrabPressed
GetItemController.RequestGrabReleased
GetItemController.RequestThrow
```

O layout respeita `Screen.safeArea`, limpa entradas em pausa/desativação e não executa buscas por frame.

## 6. click_on

Arquivo principal:

```text
Assets/Scripts/UI/FirstPersonReticleController.cs
```

Estados:

- sem objeto: `click_off` ou sprite atual da mira;
- objeto selecionado: `click_on`;
- objeto segurado: `click_on`;
- terceira pessoa, menu ou compra: mira oculta.

O reparo do Editor procura os sprites no `AssetDatabase` pelo nome e preenche automaticamente:

```text
Idle Sprite: click_off
Selected Sprite: click_on
Holding Sprite: click_on
```

Se o asset não for encontrado, o Console mostra somente um aviso e permite configuração manual.

## Ferramenta de aplicação

Executar fora do Play Mode:

```text
Tools > Game Systems > Apply Gameplay Polish (HUD Grab Purchase MiniMap Mobile)
```

A ferramenta:

- cria ou reutiliza `GameSystemsConfiguration`;
- adiciona configuração persistente de minimapa, mobile e mira;
- configura HUD de energia;
- configura soltura segura;
- cria o trigger filho de `Buy_Area`;
- procura `click_on` e `click_off`;
- salva somente a cena atual;
- não executa automaticamente;
- não move nem apaga arquivos.

Validação:

```text
Tools > Game Systems > Validate Gameplay Polish
```

## Checklist local obrigatório

1. Console sem erros vermelhos.
2. Executar a ferramenta de aplicação fora do Play.
3. Salvar a cena.
4. Correr e observar a barra principal descarregar suavemente.
5. Parar e observar a barra carregar suavemente.
6. Confirmar as cinco barras segmentadas.
7. Pegar objeto em primeira pessoa, liberar AIM e confirmar queda sem lançamento.
8. Usar THROW e confirmar que apenas essa ação arremessa.
9. Confirmar borda/X sobre `Buy_Area`.
10. Entrar na área e pressionar E.
11. Editar `RuntimeMiniMap` no Inspector e testar zoom/posição.
12. Em Desktop, confirmar que o HUD mobile está oculto.
13. Ativar teste forçado e validar todos os botões touch com o mouse.
14. Confirmar `click_on` ao mirar para item e ao segurá-lo.
15. Fazer build Android e validar safe area e multitouch.

## Validação realizada

Foi feita revisão estática dos contratos públicos, referências e caminhos de execução no repositório. A compilação e o teste visual/físico final dependem do Unity local e do build Android do projeto.
