# Relatórios do projeto MiniMarket

Esta pasta é a fonte de contexto técnico do projeto. Ela existe para impedir decisões baseadas em suposições, scripts antigos ou arquiteturas já substituídas.

## Regra obrigatória para qualquer atualização

Antes de alterar código, cena, prefab, banco, HUD, interação, câmera, movimentação ou renderização:

1. Ler `ESTADO_ATUAL.md`.
2. Ler o relatório específico da área alterada.
3. Conferir os arquivos reais citados no relatório.
4. Não reutilizar automaticamente scripts marcados como obsoletos.
5. Atualizar `CHANGELOG_TECNICO.md` e o relatório da área no mesmo conjunto de alterações.
6. Executar `TESTES_POS_GIT_PULL.md` antes de considerar a atualização concluída.

## Relatórios disponíveis

- `ESTADO_ATUAL.md`: arquitetura válida, sistemas ativos, sistemas obsoletos e riscos conhecidos.
- `ARQUITETURA_DADOS.md`: banco do jogador, dados persistentes, migração e regras de escrita.
- `STAMINA_HUD.md`: energia segmentada, HUD, persistência e contratos públicos.
- `INTERACOES.md`: seleção, realce, portas, caixas, objetos móveis e entrada mobile.
- `DESKTOP_MOBILE.md`: perfil de renderização e entrada para as duas plataformas.
- `TESTES_POS_GIT_PULL.md`: checklist de compilação e validação manual.
- `CHANGELOG_TECNICO.md`: histórico das mudanças que afetam arquitetura ou comportamento.

## Arquitetura que deve ser considerada atual

- Movimento: `Assets/Scripts/Movement/CameraRelativeMovement.cs`.
- Câmera: `Assets/Scripts/Camera/PlayerCameraController.cs` com `ThirdPersonCamera` e `FirstPersonCamera`.
- Objetos móveis: `Assets/Scripts/Interaction/GetItemController.cs` e `GrabbableItem.cs`.
- Objetos interativos: `InteractionFocusController.cs`, `InteractiveObject.cs` e `InteractionHighlight.cs`.
- Banco: `Assets/Scripts/Database/MiniMarketPlayerDatabase.cs`.
- HUD de energia: `Assets/Scripts/UI/MiniMarketEnergySegmentHUD.cs`.
- Perfil de renderização: `Assets/Scripts/Performance/PlatformRenderProfile.cs`.

## Regra de nomenclatura

O projeto ainda contém classes antigas com prefixo `MiniMarket` por compatibilidade de cena e serialização. Novos sistemas devem usar nomes de função claros. Um nome antigo não significa que aquela classe seja a arquitetura ativa.

## Quando houver divergência

Se o relatório contradizer o código, o código real deve ser inspecionado e o relatório corrigido imediatamente. Nunca inventar a intenção de um campo, componente ou objeto de cena.
