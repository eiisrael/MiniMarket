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

## Relatório mais recente

- `ENERGIA_PERCENTUAL_CORES.md`: troca `5/5` por porcentagem e alterna ícone/cor verde, amarelo e vermelho pela energia total.
- `OBJETOS_RUNTIME_PERSISTENTES.md`: materialização dos objetos visuais e hosts de configuração que antes apareciam somente no Play Mode.
- `CORRECAO_BARRA_ENERGIA_PROGRESSIVA.md`: preserva o artwork de `Canvas/StaminaHUD/Energy` e cria uma barra verde interna separada para descarga e recarga.

## Relatórios disponíveis

- `ESTADO_ATUAL.md`: arquitetura válida, sistemas ativos, sistemas obsoletos e riscos conhecidos.
- `ARQUITETURA_DADOS.md`: banco do jogador, dados persistentes, migração e regras de escrita.
- `STAMINA_HUD.md`: energia segmentada, HUD, persistência e contratos públicos.
- `INTERACOES.md`: seleção, realce, portas, caixas, objetos móveis e entrada mobile.
- `DESKTOP_MOBILE.md`: perfil de renderização e entrada para as duas plataformas.
- `RECUPERACAO_COMPRA_MINIMAPA_ENERGIA.md`: recuperação dos sistemas removidos pela organização antiga.
- `AJUSTES_HUD_INTERACAO_COMPRA_MINIMAP_MOBILE.md`: melhorias de 2026-07-12.
- `CORRECAO_BARRA_ENERGIA_PROGRESSIVA.md`: barra verde interna, cálculo segmentado e ferramenta de criação/reparo.
- `OBJETOS_RUNTIME_PERSISTENTES.md`: hierarquia persistente e editável para energia, minimapa, mobile, mira, compra e serviços.
- `ENERGIA_PERCENTUAL_CORES.md`: texto percentual, três sprites e três cores por faixa de energia.
- `TESTES_POS_GIT_PULL.md`: checklist de compilação e validação manual.
- `CHANGELOG_TECNICO.md`: histórico das mudanças que afetam arquitetura ou comportamento.

## Arquitetura que deve ser considerada atual

- Movimento: `Assets/Scripts/Movement/CameraRelativeMovement.cs`.
- Câmera: `Assets/Scripts/Camera/PlayerCameraController.cs` com `ThirdPersonCamera` e `FirstPersonCamera`.
- Objetos móveis: `Assets/Scripts/Interaction/GetItemController.cs` e `GrabbableItem.cs`.
- Objetos interativos: `InteractionFocusController.cs`, `InteractiveObject.cs` e `InteractionHighlight.cs`.
- Banco: `Assets/Scripts/Database/MiniMarketPlayerDatabase.cs`.
- HUD de energia segmentada: `Assets/Scripts/UI/MiniMarketEnergySegmentHUD.cs`.
- Barra visual principal `Canvas/StaminaHUD/Energy`: `Assets/Scripts/UI/MiniMarketEnergyProgressBar.cs`.
- Minimapa: `Assets/Scripts/UI/RuntimeMiniMap.cs` com `RuntimeMiniMapHierarchyBinding.cs`.
- Controles mobile: `Assets/Scripts/UI/MobileControlsHUD.cs` com `MobileControlsHierarchyBinding.cs`.
- Mira: `Assets/Scripts/UI/FirstPersonReticleController.cs`.
- Perfil de renderização: `Assets/Scripts/Performance/PlatformRenderProfile.cs` salvo na cena quando materializado.
- Materializador: `Assets/Editor/ProjectMaintenance/EditableRuntimeHierarchySetup.cs`.

## Regras permanentes

- Não alterar conteúdo de `Assets/Brick Project Studio`.
- Não mover ou apagar scripts automaticamente.
- Preservar GUIDs e referências de cena.
- Evitar referências serializadas entre a cena normal e `DontDestroyOnLoad`.
- Não gravar banco, PlayerPrefs ou arquivos a cada frame.
- Desktop e Mobile devem compartilhar a mesma lógica de gameplay.
- Objetos visuais configuráveis devem ser salvos na cena; somente recursos transitórios devem nascer apenas no Play Mode.
- Não afirmar que o Unity compilou ou executou sem validação no Editor local.

## Quando houver divergência

Se o relatório contradisser o código, o código real deve ser inspecionado e o relatório corrigido imediatamente. Nunca inventar a intenção de um campo, componente ou objeto de cena.
