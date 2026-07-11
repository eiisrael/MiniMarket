# Interações e realce visual

Atualizado em: 2026-07-11

## Objetivo

Portas, caixas, objetos móveis, interruptores e outros elementos interativos devem mudar de cor quando estão sob a mira, tanto em primeira quanto em terceira pessoa.

## Componentes

### `InteractionHighlight`

Responsável apenas pelo visual.

- usa `MaterialPropertyBlock`;
- não instancia materiais;
- preserva o bloco de propriedades existente;
- suporta `_BaseColor` do URP e `_Color` do shader Standard;
- possui cor de foco e cor de interação ativa;
- restaura o estado original ao perder foco ou desabilitar.

### `InteractiveObject`

Marca objetos genéricos, como portas e mecanismos.

- eventos de foco, perda de foco e interação;
- `onInteract` configurável no Inspector;
- compatibilidade opcional com métodos públicos antigos sem parâmetros:
  - `Interact`
  - `Interagir`
  - `Toggle`
  - `Alternar`
  - `Open`
  - `Abrir`
  - `Use`
  - `Usar`
  - `Activate`
  - `Ativar`

A reflexão acontece somente quando o jogador interage, nunca por frame.

### `InteractionFocusController`

Fica no objeto da câmera do jogador.

- raycast/spherecast pelo centro da tela;
- funciona em primeira e terceira pessoa;
- tecla `E` e clique configurável no desktop;
- método `RequestInteract()` para botão mobile;
- posição de toque externa opcional;
- ignora colliders do personagem.

### `GrabbableItem`

Marca objetos que podem ser movidos fisicamente.

- integra automaticamente `InteractionHighlight`;
- foco usa cor de seleção;
- enquanto está sendo segurado usa cor ativa;
- mantém eventos de selecionar, pegar, soltar e desselecionar.

### `GetItemController`

- funciona em primeira e terceira pessoa;
- entrada desktop por mouse;
- entrada mobile por `RequestGrabPressed`, `RequestGrabReleased` e `RequestThrow`;
- posição de toque externa opcional;
- movimento físico com mola;
- proteção contra atravessar paredes;
- preserva e restaura configurações do Rigidbody.

## Configuração de objetos

### Caixa ou produto móvel

Componentes mínimos:

```text
Collider
Rigidbody
GrabbableItem
InteractionHighlight
```

### Porta ou mecanismo

Componentes mínimos:

```text
Collider
Renderer em si ou em filhos
InteractiveObject
InteractionHighlight
Script real da porta, quando existir
```

No `InteractiveObject.onInteract`, ligar explicitamente a função da porta é a opção mais segura. A invocação automática existe somente como compatibilidade.

## Conflito entre pegar e interagir

Objetos com `GrabbableItem` são controlados pelo `GetItemController`. O instalador não adiciona `InteractiveObject` ao mesmo objeto, evitando duas ações no mesmo clique.

## Cores

Cores devem ser configuradas no `InteractionHighlight`, não por troca de `sharedMaterial`.

Motivos:

- evita duplicação de material;
- reduz memória no mobile;
- impede que uma seleção altere todos os objetos que compartilham o material;
- funciona sem destruir a cor original.

## Testes obrigatórios

1. Mirar uma caixa em terceira pessoa: mudar cor.
2. Sair da caixa: restaurar cor original.
3. Pegar a caixa: usar cor ativa.
4. Soltar: restaurar estado correto.
5. Repetir em primeira pessoa.
6. Mirar porta: mudar cor.
7. Pressionar `E` ou clicar: executar ação uma vez.
8. Abrir menu: foco deve desaparecer e nenhuma ação deve ocorrer.
9. Testar dois objetos com o mesmo material e confirmar que apenas o alvo muda.
10. Testar botão mobile chamando `RequestInteract()`.
