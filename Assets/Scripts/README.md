# Organização de Scripts

A pasta `Assets/Scripts` passa a ser organizada por responsabilidade:

- `Core`: modelos, contratos e classes-base.
- `Configuration`: configurações e preferências.
- `Data`: banco local, perfil e persistência.
- `Economy`: gold, carteira e moedas.
- `Purchasing`: compra de terrenos, lojas e cenas.
- `Interaction`: seleção, coleta e interação com objetos.
- `Physics`: comportamentos físicos que não sejam movimentação do personagem.
- `Stamina`: energia independente de câmera/movimentação.
- `Menus`: controladores de menus.
- `UI`: HUD, textos, painéis e botões.
- `World`: cidade, tempo, ciclo solar e cena.
- `Performance`: otimizações genéricas sem dependência de câmera/movimentação.
- `Diagnostics`: logs e ferramentas de diagnóstico genéricas.
- `Gameplay`: comportamentos de gameplay que não se encaixem nas categorias acima.

## Regras

1. Não usar o prefixo `MiniMarket` no nome de novos scripts ou classes.
2. O nome do arquivo deve ser igual ao nome da classe principal.
3. Cada script deve ficar na pasta da sua função.
4. Sistemas de câmera e movimentação do personagem foram removidos e não serão recriados nesta etapa.
5. Conteúdo dentro de pastas `Brick Project Studio` não deve ser alterado.
6. MonoBehaviours sem uso em cenas, prefabs ou dependências de código são removidos pela migração.

A migração é executada automaticamente uma única vez depois do `git pull`. Também pode ser executada manualmente em:

`Tools > Project Maintenance > Run Script Cleanup and Organization`

Ao terminar, ela gera `ScriptOrganizationReport.md` nesta pasta.
