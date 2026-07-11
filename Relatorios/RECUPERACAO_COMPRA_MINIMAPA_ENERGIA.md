# Recuperação de compra, minimapa, energia, diagnósticos e mira

Atualizado em: 2026-07-11

## Escopo

Este relatório registra a recuperação dos sistemas que deixaram de funcionar após a organização automática local de scripts e cena.

## Diagnóstico de causa raiz

### Compra de terrenos

O sistema de compra antigo continuava usando `BuySceneCameraModeController`, mas o novo `PlayerCameraController` também aplicava posição, rotação e FOV em `LateUpdate`. Os dois sistemas disputavam a mesma câmera. Além disso, componentes antigos da BuyScene foram removidos ou ficaram sem referências durante a organização local.

### Minimapa

A câmera principal desativava todas as outras câmeras da cena. Isso incluía a câmera do minimapa, mesmo ela renderizando para uma `RenderTexture`. O minimapa antigo também procurava `PlayerMove`, que não é mais o movimento oficial.

### Diagnósticos

O painel antigo pertencia à Camera V2 e foi removido junto com os scripts obsoletos.

### Energia grátis e HUD

O HUD estava colocado em um Text e procurava imagens somente nos próprios filhos. As barras visuais estavam como objetos irmãos ou em outro nível do container. A restauração gratuita também podia ser sobrescrita por listeners antigos no mesmo frame.

### Mira

Não existia um controlador de visibilidade ligado ao modo atual da câmera.

## Arquitetura corrigida

### `PlayerCameraController`

- preserva câmeras auxiliares com `targetTexture`;
- expõe `SetExternalPoseControl(bool)`;
- suspende atualização de pose e cursor quando outro sistema assume a câmera;
- retoma imediatamente a pose normal ao liberar o controle externo.

### `PurchaseModeBridge`

- observa `BuySceneCameraModeController.ModoCompraAtivo`;
- bloqueia o input do gameplay;
- entrega a câmera ao modo de compra;
- restaura câmera, tag, AudioListener e input ao sair;
- possui bootstrap runtime para reparar referências, painel, triggers, terrenos e linhas.

### `RuntimeMiniMap`

- inicialização automática;
- alvo oficial: `CameraRelativeMovement`;
- câmera ortográfica para `RenderTexture`;
- UI circular e zoom;
- tecla M;
- resolução 512 no desktop e 256 no mobile.

### `RuntimeDiagnosticsPanel`

- inicialização automática;
- tecla F10;
- informações de performance, câmeras, banco, energia, compra e minimapa;
- não depende da Camera V2.

### `MiniMarketEnergySegmentHUD`

- detecta barras no container visual;
- suporta barra total e barras segmentadas;
- anima o preenchimento;
- exibe reserva de recarga no próximo segmento;
- mantém atualização por eventos e verificação lenta de segurança.

### `FreeEnergyRestoreService`

- detecta o botão de energia grátis por nome/texto;
- restaura banco e movimento imediatamente;
- reaplica no fim do frame e após pequeno atraso;
- força atualização visual do HUD.

### `FirstPersonReticleController`

- detecta elementos de mira pelo nome;
- mostra somente em primeira pessoa;
- oculta em terceira pessoa, menu e compra.

## Ferramenta de reparo da cena

Executar fora do Play Mode:

```text
Tools > Game Systems > Repair Purchase Minimap Diagnostics Energy Reticle
```

A ferramenta:

- conecta `PurchaseModeBridge`;
- conecta controlador, triggers, terrenos e painel;
- reativa demarcações;
- liga `MiniMarketEnergySegmentHUD` ao movimento;
- rebusca as imagens das barras;
- salva a cena somente quando necessário.

Ela não remove objetos, não altera GUIDs e não toca em Brick Project Studio.

## Checklist após `git pull`

1. Console com zero erros vermelhos.
2. Executar a ferramenta de reparo.
3. Salvar a cena.
4. Play Mode.
5. Aproximar-se da calçada de compra.
6. Confirmar que a marcação aparece.
7. Pressionar E e conferir a visão de compra.
8. Selecionar um terreno e abrir o painel de confirmação.
9. Pressionar M e validar o minimapa e os botões de zoom.
10. Pressionar F10 e validar o painel de diagnóstico.
11. Gastar energia e observar as barras diminuindo.
12. Parar e observar recarga visual.
13. Usar Energia Grátis e aguardar pelo menos dois segundos; a energia deve permanecer completa.
14. Conferir que a mira está oculta em terceira pessoa.
15. Entrar em primeira pessoa e conferir que a mira aparece.

## Observação sobre a área da calçada

O reparo não inventa uma posição para a entrada. Deve existir um collider no objeto real da calçada com `BuySceneEntryTrigger`. Se nenhum trigger for encontrado, o Console informa exatamente essa pendência.

## Organização de scripts

A limpeza automática destrutiva foi desativada. O organizador agora apenas gera uma auditoria em Markdown e nunca move, renomeia ou apaga arquivos automaticamente.
