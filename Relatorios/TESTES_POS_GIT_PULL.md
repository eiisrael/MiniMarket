# Testes após Git Pull

Atualizado em: 2026-07-12

Execute este checklist depois de qualquer atualização relevante.

## 1. Atualização

```bash
cd ~/Desktop/MiniMarket/MiniMarket
git pull origin main
git status
git log --oneline -12
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
6. Não adicionar componentes enquanto houver erro de compilação.

## 3. Reparos manuais seguros

Com a `SampleScene` aberta e fora do Play Mode:

```text
Tools > Player System > Create or Repair Player System
Tools > Player System > Repair Player Animator
Tools > Game Systems > Apply Gameplay Polish (HUD Grab Purchase MiniMap Mobile)
Tools > Game Systems > Validate Gameplay Polish
```

Executar `Clean Cross-Scene References` apenas fora do Play Mode e somente quando o aviso realmente existir.

Salvar a cena com `Ctrl + S`.

## 4. Hierarquia mínima

Confirmar:

- `Character 01` com `CharacterController` e `CameraRelativeMovement`;
- Animator válido;
- `PlayerCameraRig` com uma Camera e um AudioListener;
- `PlayerCameraController`, `GetItemController` e `InteractionFocusController`;
- `GameSystemsConfiguration` com minimapa, mobile e mira;
- `Buy_Area` com collider sólido;
- filho `BuySceneEntryTrigger_Runtime` com BoxCollider trigger;
- nenhuma câmera antiga ativa.

## 5. Banco

1. Alterar gold.
2. Alterar nome.
3. Comprar/registrar empresa.
4. Gastar energia.
5. Parar Play.
6. Iniciar novamente.
7. Confirmar persistência.
8. Confirmar ausência de referência cross-scene inválida.
9. Confirmar ausência de objetos órfãos ao dar Stop.

## 6. Stamina e HUD

- iniciar em `5/5` ou no valor salvo;
- correr e observar a barra principal descarregar suavemente;
- consumir um segmento e confirmar mudança para `4/5`;
- confirmar cinco barras segmentadas;
- aguardar recuperação e observar a barra carregar;
- confirmar recuperação dos segmentos adicionais;
- testar energia grátis e aguardar pelo menos dois segundos;
- abrir e fechar menu durante movimento;
- confirmar ausência de logs por frame.

## 7. Interação e objetos físicos

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

## 8. Compra de terrenos

- marcação de borda/X aparece sobre `Buy_Area`;
- personagem não atravessa a calçada;
- entrar na área muda a cor da marcação;
- tecla E abre a vista de compra;
- terrenos recebem destaque;
- painel de confirmação abre;
- gold insuficiente é bloqueado;
- compra válida debita gold e persiste empresa/propriedade;
- sair restaura câmera, movimento e cursor.

## 9. Minimapa

- componente `RuntimeMiniMap` aparece no Inspector;
- alterar canto, tamanho, margem e zoom;
- M abre e fecha;
- botões + e − funcionam;
- ponto do jogador permanece centralizado;
- câmera do minimapa não cria AudioListener;
- RenderTexture usa resolução mobile/desktop correta.

## 10. Desktop

- WASD;
- Shift;
- Space;
- mouse e troca primeira/terceira pessoa;
- cursor correto;
- uma câmera e um AudioListener de gameplay;
- HUD mobile oculto;
- FPS sem spam de Console.

## 11. Mobile

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
- pause/resume limpa inputs presos;
- dados persistem ao ir para segundo plano;
- temperatura, memória e FPS aceitáveis por dez minutos.

## 12. Relatórios

Antes de encerrar:

- atualizar `CHANGELOG_TECNICO.md`;
- atualizar relatório específico;
- corrigir divergências código/documentação;
- registrar testes não executados e riscos.
