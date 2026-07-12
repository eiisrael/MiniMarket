# Interações, realce visual e objetos móveis

Atualizado em: 2026-07-12

## Objetivo

Portas, caixas, objetos móveis, interruptores e outros elementos interativos devem mudar de cor sob a mira em primeira e terceira pessoa. Objetos físicos devem ser soltos com segurança e somente a ação explícita de arremesso pode lançá-los.

## Componentes

### `InteractionHighlight`

Responsável apenas pelo visual.

- usa `MaterialPropertyBlock`;
- não instancia materiais;
- preserva propriedades existentes;
- suporta `_BaseColor` do URP e `_Color` do shader Standard;
- possui cor de foco e cor ativa;
- restaura o estado original ao perder foco ou desabilitar.

### `InteractiveObject`

Marca portas e mecanismos.

- eventos de foco, perda de foco e interação;
- `onInteract` configurável no Inspector;
- compatibilidade opcional com métodos antigos sem parâmetros;
- reflexão somente no momento da interação.

### `InteractionFocusController`

Fica na câmera do jogador.

- raycast/spherecast pelo centro;
- funciona em primeira e terceira pessoa;
- tecla `E` e clique no Desktop;
- `RequestInteract()` para mobile;
- posição de toque opcional;
- ignora o personagem.

### `GrabbableItem`

Marca objetos físicos.

- integra `InteractionHighlight`;
- foco usa cor de seleção;
- segurando usa cor ativa;
- eventos de selecionar, pegar, soltar e desselecionar.

### `GetItemController`

- funciona em primeira e terceira pessoa;
- mouse no Desktop;
- `RequestGrabPressed`, `RequestGrabReleased` e `RequestThrow` no mobile;
- movimento físico com mola;
- proteção contra paredes;
- preserva e restaura o Rigidbody.

## Soltura segura

A soltura comum e o arremesso são fluxos diferentes.

### Soltura comum

Acontece quando:

- o botão de pegar é liberado;
- o input fica bloqueado;
- o componente é desativado;
- o objeto sai muito do ponto seguro;
- o jogador sai da primeira pessoa enquanto segura um item pego nessa visão.

Comportamento:

- não adiciona força para frente;
- não herda a velocidade da câmera por padrão;
- reduz velocidade linear e angular;
- limita a velocidade final;
- restaura gravidade, damping, constraints, interpolação e collision mode.

Campos:

```text
dropWhenLeavingFirstPerson
inheritCameraVelocityOnNormalRelease
safeDropVelocityMultiplier
safeDropAngularVelocityMultiplier
maximumSafeDropSpeed
```

### Arremesso

Somente ocorre por:

```text
throwKey
RequestThrow()
```

Nesse caminho é aplicada `throwForce` para frente.

## Mira click_on

`FirstPersonReticleController` usa:

- `click_off`: estado normal;
- `click_on`: item selecionado;
- `click_on`: item segurado.

A mira inteira fica oculta em terceira pessoa, menus e modo de compra.

A ferramenta `GameplayPolishSetup` procura os sprites pelo nome no `AssetDatabase` e preenche os campos automaticamente.

## Configuração de objetos

### Caixa ou produto móvel

```text
Collider
Rigidbody
GrabbableItem
InteractionHighlight
```

### Porta ou mecanismo

```text
Collider
Renderer em si ou em filhos
InteractiveObject
InteractionHighlight
Script real da porta
```

No `InteractiveObject.onInteract`, ligar explicitamente a função real da porta é a opção mais segura.

## Conflito entre pegar e interagir

Objetos com `GrabbableItem` pertencem ao `GetItemController`. O instalador não adiciona `InteractiveObject` ao mesmo alvo, evitando duas ações no mesmo clique.

## Performance

- nenhuma troca de `sharedMaterial`;
- nenhuma criação de material por seleção;
- spherecasts usam buffers reutilizados;
- busca de referências acontece somente quando faltam referências;
- reflexão não ocorre por frame.

## Testes obrigatórios

1. Mirar uma caixa em terceira pessoa e confirmar cor.
2. Sair da caixa e confirmar restauração.
3. Repetir em primeira pessoa.
4. Confirmar `click_on` ao selecionar.
5. Pegar e confirmar `click_on` durante a retenção.
6. Liberar o botão e confirmar queda, sem lançamento.
7. Pegar em primeira pessoa e liberar AIM; confirmar queda segura.
8. Usar THROW e confirmar que apenas essa ação arremessa.
9. Confirmar proteção contra parede.
10. Mirar porta e confirmar realce.
11. Pressionar `E` uma vez e executar uma ação.
12. Abrir menu e confirmar remoção do foco.
13. Testar dois objetos com o mesmo material e confirmar alteração somente no alvo.
14. Testar os botões mobile de interagir, pegar, soltar e arremessar.
