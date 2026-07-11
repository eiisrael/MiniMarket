# Testes após Git Pull

Atualizado em: 2026-07-11

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

## 2. Compilação do Unity

1. Sair do Play Mode.
2. Aguardar importação e compilação terminarem.
3. Abrir Console.
4. Clicar em Clear.
5. Confirmar zero erros vermelhos.
6. Avisos devem ser analisados, mas não devem ser confundidos com erro de compilação.

Não adicionar componentes enquanto existir erro vermelho.

## 3. Reparo automático/manual

Se a cena não tiver sido atualizada automaticamente:

```text
Tools > Player System > Create or Repair Player System
Tools > Player System > Repair Player Animator
Tools > Game Systems > Repair Data HUD Interactions and Mobile
Tools > Project Maintenance > Clean Cross-Scene References
```

Salvar a cena com `Ctrl + S`.

## 4. Hierarquia mínima

Confirmar:

- `Character 01` com `CharacterController` e `CameraRelativeMovement`.
- Animator válido no personagem ou filho.
- `PlayerCameraRig` com uma Camera e um AudioListener.
- `PlayerCameraController`, `GetItemController` e `InteractionFocusController` no rig.
- nenhuma câmera antiga ativa.

## 5. Banco

1. Iniciar Play.
2. Alterar gold.
3. Alterar nome.
4. Comprar/registrar empresa.
5. Gastar energia.
6. Parar Play.
7. Iniciar novamente.
8. Confirmar persistência.
9. Confirmar ausência de `Cross scene references are not supported`.
10. Confirmar ausência de objetos órfãos ao dar Stop.

## 6. Stamina e HUD

- iniciar em `5/5` ou no valor salvo;
- correr e verificar consumo;
- consumir um segmento e verificar refill da barra;
- recuperar energia;
- testar botão de energia grátis;
- abrir/fechar menu durante movimento;
- verificar que o HUD só muda quando o dado muda;
- confirmar que o Console não recebe logs por frame.

## 7. Interação

- caixa muda de cor em terceira pessoa;
- caixa muda de cor em primeira pessoa;
- pegar, mover e soltar preserva colisão;
- porta muda de cor;
- `E` ou clique executa a porta somente uma vez;
- objetos que compartilham material não mudam juntos;
- menu aberto bloqueia interação.

## 8. Desktop

- movimento WASD;
- corrida Shift;
- pulo Space;
- troca primeira/terceira pessoa;
- cursor trava e destrava corretamente;
- uma câmera e um AudioListener;
- FPS estável sem spam de Console.

## 9. Mobile

No mínimo, testar o perfil forçado no Editor. Antes de publicar, testar build real:

- joystick chama `SetMoveInput`;
- botão corrida chama `SetRunInput`;
- botão pulo chama `RequestJump`;
- botão interação chama `RequestInteract`;
- botão pegar usa pressed/released;
- pause/resume mantém dados;
- orientação e safe area corretas;
- temperatura e memória aceitáveis.

## 10. Relatórios

Antes de encerrar a atualização:

- atualizar `Relatorios/CHANGELOG_TECNICO.md`;
- atualizar o relatório da área alterada;
- corrigir qualquer divergência entre relatório e código;
- registrar limitações ou testes não executados.
