# Testes após Git Pull

Atualizado em: 2026-07-13

Execute este checklist depois de qualquer atualização relevante.

## 1. Atualização

```bash
cd ~/Desktop/MiniMarket/MiniMarket
git pull origin main
git status
git log --oneline -15
```

Se `UpgradeLog.htm` impedir o pull:

```bash
git restore UpgradeLog.htm
git pull origin main
```

Não usar `git reset --hard` ou `git clean -fd` sem backup e sem conferir os arquivos locais.

## 2. Compilação

1. Sair do Play Mode.
2. Aguardar importação e compilação.
3. Abrir Console.
4. Clicar em Clear.
5. Confirmar zero erros vermelhos.
6. Não executar ferramentas enquanto houver erro de compilação.

## 3. Materializar objetos editáveis

Com a `SampleScene` aberta, salva e fora do Play Mode:

```text
Tools > MiniMarket > Materializar Todos os Objetos Runtime na Hierarquia
Tools > MiniMarket > Validar Objetos Runtime Persistentes
```

Depois:

1. Confirmar `Erros=0` no validador.
2. Salvar com `Ctrl + S`.
3. Fechar e reabrir a `SampleScene`.
4. Confirmar que os objetos continuam na Hierarchy sem apertar Play.

## 4. Reparos adicionais seguros

Quando necessário, fora do Play Mode:

```text
Tools > Player System > Create or Repair Player System
Tools > Player System > Repair Player Animator
Tools > Game Systems > Apply Gameplay Polish (HUD Grab Purchase MiniMap Mobile)
Tools > Game Systems > Validate Gameplay Polish
Tools > MiniMarket > Criar ou Reparar Barra de Energia
Tools > MiniMarket > Validar Barra de Energia
```

Executar `Clean Cross-Scene References` apenas fora do Play Mode e somente quando o aviso realmente existir.

## 5. Hierarquia mínima persistente

Confirmar fora do Play Mode:

- `Character 01` com `CharacterController` e `CameraRelativeMovement`;
- Animator válido;
- `PlayerCameraRig` com uma Camera e um AudioListener;
- `PlayerCameraController`, `GetItemController` e `InteractionFocusController`;
- `Canvas > StaminaHUD > Energy` com `MiniMarketEnergyProgressBar`;
- `Energy > EnergyProgressArea > EnergyProgressFill`;
- `Canvas > Mira` ou outra imagem de mira reconhecida;
- objeto com `RuntimeMiniMap` e `RuntimeMiniMapHierarchyBinding`;
- `RuntimeMiniMapCamera` e `RuntimeMiniMapCanvas`;
- objeto com `MobileControlsHUD` e `MobileControlsHierarchyBinding`;
- `MobileControlsRuntime > SafeArea > MoveJoystick/Actions`;
- `PlatformRenderProfile`;
- `RuntimeDiagnosticsPanel`;
- `PurchaseSystemRuntimeRepair`;
- `Buy_Area` com collider sólido;
- `BuySceneEntryTrigger_Runtime` com BoxCollider trigger e linhas visuais persistentes;
- nenhuma câmera antiga ativa.

## 6. Edição fora do Play Mode

### Energia

1. Selecionar `Energy`.
2. No `MiniMarketEnergyProgressBar`, alterar `Cor Barra`.
3. Clicar em `Aplicar cor e área no Editor` quando necessário.
4. Confirmar que `EnergyProgressFill` muda imediatamente.
5. Alterar `Ancora Minima` e `Ancora Maxima`.
6. Confirmar que somente a área verde muda; o artwork `Energy` permanece estático.
7. Salvar, trocar de cena e retornar para confirmar persistência.

### Minimapa

1. Alterar cor de borda, ponto, botões, tamanho e posição.
2. Editar diretamente os filhos persistentes.
3. Confirmar que a câmera e Canvas não desaparecem ao sair do Play.
4. Confirmar que somente a `RenderTexture` é criada durante o Play.

### Mobile

1. Com o Canvas persistente visível no Editor, ajustar joystick e botões.
2. Alterar cores e textos no `MobileControlsHUD`.
3. Confirmar que os GameObjects permanecem depois de Stop.
4. Confirmar que no Desktop o Canvas pode ser ocultado somente durante o jogo.

### Compra

1. Alterar cores/largura nos componentes de entrada e terreno.
2. Confirmar LineRenderers visíveis na Hierarchy fora do Play.
3. Confirmar material persistente em `Assets/Generated/MiniMarket/Materials/BuyAreaLine.mat`.

## 7. Banco

1. Alterar gold.
2. Alterar nome.
3. Comprar/registrar empresa.
4. Gastar energia.
5. Parar Play.
6. Iniciar novamente.
7. Confirmar persistência.
8. Confirmar ausência de referência cross-scene inválida.
9. Confirmar ausência de objetos órfãos ao dar Stop.

## 8. Stamina e HUD

- iniciar em `5/5` com `EnergyProgressFill` cheio e verde;
- confirmar que a imagem original de `Energy` não diminui;
- confirmar que `Energy` não está como `Image.Type.Filled`;
- confirmar que somente a largura de `EnergyProgressFill` diminui;
- confirmar que `Background_Ene`, ícone e `Txt_Qtd` permanecem estáticos;
- correr e observar a barra verde descarregar suavemente;
- confirmar que o texto e a barra representam a mesma energia segmentada;
- consumir um segmento e confirmar mudança para `4/5` sem salto incorreto;
- aguardar recuperação e observar a barra verde carregar;
- confirmar recuperação dos segmentos adicionais;
- testar energia grátis e aguardar pelo menos dois segundos;
- abrir e fechar menu durante movimento;
- confirmar ausência de logs por frame.

## 9. Interação e objetos físicos

- caixa muda de cor em terceira pessoa;
- caixa muda de cor em primeira pessoa;
- `click_on` aparece ao selecionar;
- `click_on` permanece ao segurar;
- soltar normalmente faz o objeto cair sem lançamento;
- sair da primeira pessoa enquanto segura faz queda segura;
- THROW arremessa explicitamente;
- proteção contra parede funciona;
- porta muda de cor;
- `E` executa ação uma vez;
- materiais compartilhados não mudam juntos;
- menu bloqueia foco e interação.

## 10. Compra de terrenos

- marcação de borda/X existe antes do Play;
- personagem não atravessa a calçada;
- entrar na área muda a cor da marcação;
- tecla E abre a vista de compra;
- terrenos recebem destaque;
- painel de confirmação abre;
- gold insuficiente é bloqueado;
- compra válida debita gold e persiste empresa/propriedade;
- sair restaura câmera, movimento e cursor.

## 11. Minimapa

- hierarquia visual existe antes do Play;
- M abre e fecha;
- botões + e − funcionam;
- ponto do jogador permanece centralizado;
- câmera do minimapa não cria AudioListener;
- RenderTexture é ligada ao `MapImage` durante o Play;
- RenderTexture usa resolução mobile/desktop correta;
- Stop remove somente o recurso temporário, não os GameObjects persistentes.

## 12. Desktop

- WASD;
- Shift;
- Space;
- mouse e troca primeira/terceira pessoa;
- cursor correto;
- uma câmera e um AudioListener de gameplay;
- HUD mobile oculto durante o Play;
- objetos do HUD mobile continuam editáveis fora do Play;
- FPS sem spam de Console.

## 13. Mobile

No Editor, ligar temporariamente `forcarVisivelParaTestes`. Depois validar build Android real:

- joystick move;
- arrasto direito gira a câmera;
- RUN funciona com toque simultâneo;
- JUMP funciona;
- E interage;
- GRAB pega e solta;
- THROW arremessa;
- AIM entra e sai da primeira pessoa;
- safe area correta;
- multitouch: mover + olhar + correr;
- a barra verde interna acompanha os mesmos dados do Desktop;
- pause/resume limpa inputs presos;
- dados persistem ao ir para segundo plano;
- temperatura, memória e FPS aceitáveis por dez minutos.

## 14. Relatórios

Antes de encerrar:

- atualizar `CHANGELOG_TECNICO.md`;
- atualizar relatório específico;
- corrigir divergências código/documentação;
- registrar testes não executados e riscos.
